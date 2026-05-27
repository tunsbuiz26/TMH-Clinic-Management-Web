using TMH.API.Data;
using TMH.API.Helpers;
using TMH.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using TMH.Shared.Models;

namespace TMH.API.Services
{
    public class AppointmentService
    {
        private readonly AppDbContext _db;
        private readonly EmailService _email;

        public AppointmentService(AppDbContext db, EmailService email)
        {
            _db    = db;
            _email = email;
        }

        // =====================================================================
        // LẤY DANH SÁCH BÁC SĨ + KHUNG GIỜ CÒN TRỐNG
        // =====================================================================
        public async Task<List<DoctorScheduleDto>> GetAvailableDoctorsAsync(DateTime? date = null)
        {
            var doctors = await _db.Doctors
                .Where(d => d.IsAvailable)
                .Include(d => d.WorkSchedules)
                .ToListAsync();

            // Pre-load rating tất cả bác sĩ trong một query (tránh N+1)
            var doctorIds = doctors.Select(d => d.Id).ToList();
            var reviewStats = await _db.Reviews
                .Where(r => doctorIds.Contains(r.DoctorId))
                .GroupBy(r => r.DoctorId)
                .Select(g => new
                {
                    DoctorId = g.Key,
                    Avg      = Math.Round(g.Average(r => (double)r.Rating), 1),
                    Count    = g.Count()
                })
                .ToDictionaryAsync(x => x.DoctorId);

            var result = new List<DoctorScheduleDto>();

            foreach (var doc in doctors)
            {
                List<ScheduleSlotDto> slots;

                if (date.HasValue)
                {
                    // Người dùng chọn ngày cụ thể → trả đúng slot ngày đó
                    slots = doc.WorkSchedules
                        .Where(w => w.WorkDate.Date == date.Value.Date
                                 && w.Status == "Approved")
                        .OrderBy(w => w.StartTime)
                        .Select(w => new ScheduleSlotDto
                        {
                            ScheduleId      = w.Id,
                            WorkDate        = w.WorkDate,
                            StartTime       = $"{(int)w.StartTime.TotalHours:D2}:{w.StartTime.Minutes:D2}",
                            EndTime         = $"{(int)w.EndTime.TotalHours:D2}:{w.EndTime.Minutes:D2}",
                            MaxPatients     = w.MaxPatients,
                            CurrentPatients = w.CurrentPatients,
                            RemainingSlots  = w.MaxPatients - w.CurrentPatients,
                            IsFull          = w.CurrentPatients >= w.MaxPatients
                        })
                        .ToList();
                }
                else
                {
                    // Load trang ban đầu → lấy tất cả slot trong 30 ngày tới
                    var cutoff = DateTime.Today.AddDays(30);
                    slots = doc.WorkSchedules
                        .Where(w => w.WorkDate.Date >= DateTime.Today
                                 && w.WorkDate.Date <= cutoff
                                 && w.Status == "Approved")
                        .OrderBy(w => w.WorkDate)
                        .ThenBy(w => w.StartTime)
                        .Select(w => new ScheduleSlotDto
                        {
                            ScheduleId      = w.Id,
                            WorkDate        = w.WorkDate,
                            StartTime       = $"{(int)w.StartTime.TotalHours:D2}:{w.StartTime.Minutes:D2}",
                            EndTime         = $"{(int)w.EndTime.TotalHours:D2}:{w.EndTime.Minutes:D2}",
                            MaxPatients     = w.MaxPatients,
                            CurrentPatients = w.CurrentPatients,
                            RemainingSlots  = w.MaxPatients - w.CurrentPatients,
                            IsFull          = w.CurrentPatients >= w.MaxPatients
                        })
                        .ToList();
                }

                reviewStats.TryGetValue(doc.Id, out var rs);

                result.Add(new DoctorScheduleDto
                {
                    DoctorId       = doc.Id,
                    FullName       = doc.FullName,
                    Specialty      = doc.Specialty,
                    Degree         = doc.Degree,
                    Description    = doc.Description,
                    AvailableSlots = slots,
                    AverageRating  = rs?.Avg ?? 0,
                    ReviewCount    = rs?.Count ?? 0
                });
            }

            return result;
        }

        // =====================================================================
        // ĐẶT LỊCH KHÁM — LINH HOẠT
        //
        // Nhánh A: Có ScheduleId  → validate + đặt ngay (ChoXacNhan)
        // Nhánh B: Không có ScheduleId → tạo lịch ChoPhanCong, lễ tân xếp sau
        //          B1: Có DoctorId  → biết bác sĩ, chưa có slot
        //          B2: Không DoctorId → chưa biết cả bác sĩ lẫn slot
        // =====================================================================
        public async Task<AppointmentResponseDto> BookAsync(BookAppointmentDto dto)
        {
            // 1. Kiểm tra hồ sơ bệnh nhân
            var patient = await _db.Patients.FindAsync(dto.PatientId);
            if (patient == null)
                return Fail("Không tìm thấy hồ sơ bệnh nhân.");

            // ── NHÁNH A: Có slot cụ thể ──────────────────────────────────────
            if (dto.ScheduleId.HasValue)
            {
                var schedule = await _db.WorkSchedules
                    .Include(w => w.Doctor)
                    .FirstOrDefaultAsync(w => w.Id == dto.ScheduleId.Value);

                if (schedule == null)
                    return Fail("Khung giờ không tồn tại.");

                if (schedule.CurrentPatients >= schedule.MaxPatients)
                    return Fail("Khung giờ này đã đầy, vui lòng chọn khung giờ khác.");

                if (schedule.WorkDate.Date < DateTime.Today)
                    return Fail("Không thể đặt lịch cho ngày đã qua.");

                bool alreadyBooked = await _db.Appointments.AnyAsync(a =>
                    a.PatientId   == dto.PatientId &&
                    a.ScheduleId  == dto.ScheduleId &&
                    a.Status      != AppointmentStatus.DaHuy &&
                    a.Status      != AppointmentStatus.VangMat);

                if (alreadyBooked)
                    return Fail("Hồ sơ này đã có lịch trong khung giờ đó rồi.");

                // Sinh mã đặt lịch an toàn với race condition
                string bookingCode = await CodeGenerator.NextAppointmentCodeAsync(_db);

                // Tự động phát hiện tái khám
                bool isReturn = await _db.Appointments.AnyAsync(a =>
                    a.PatientId == dto.PatientId &&
                    a.DoctorId  == schedule.DoctorId &&
                    a.Status    == AppointmentStatus.HoanThanh);

                var appointment = new Appointment
                {
                    PatientId   = dto.PatientId,
                    DoctorId    = schedule.DoctorId,
                    ScheduleId  = schedule.Id,
                    BookingCode = bookingCode,
                    BookedAt    = DateTime.UtcNow,
                    Status      = AppointmentStatus.ChoXacNhan,
                    Note        = dto.Note?.Trim(),
                    IsReturn    = isReturn
                };

                _db.Appointments.Add(appointment);
                schedule.CurrentPatients += 1;
                await _db.SaveChangesAsync();

                var notification = new Notification
                {
                    UserId        = patient.UserId,
                    AppointmentId = appointment.Id,
                    Title         = "Đặt lịch thành công",
                    Content       = $"Lịch khám ngày {schedule.WorkDate:dd/MM/yyyy} " +
                                    $"lúc {(int)schedule.StartTime.TotalHours:D2}:{schedule.StartTime.Minutes:D2} " +
                                    $"với {schedule.Doctor.FullName} đã được ghi nhận. Mã lịch: {bookingCode}",
                    Type   = NotificationType.XacNhanLich,
                    SentAt = DateTime.UtcNow,
                    IsRead = false
                };
                _db.Notifications.Add(notification);
                await _db.SaveChangesAsync();

                var (userEmail, userName) = await GetUserContactAsync(patient.UserId);
                _ = _email.SendBookingConfirmationAsync(
                    userEmail, userName, bookingCode,
                    schedule.Doctor.FullName,
                    schedule.WorkDate.ToString("dd/MM/yyyy"),
                    $"{(int)schedule.StartTime.TotalHours:D2}:{schedule.StartTime.Minutes:D2}",
                    $"{(int)schedule.EndTime.TotalHours:D2}:{schedule.EndTime.Minutes:D2}",
                    patient.FullName, appointment.Note);

                return new AppointmentResponseDto
                {
                    Success = true,
                    Message = $"Đặt lịch thành công! Mã lịch của bạn: {bookingCode}",
                    Data    = MapToDetail(appointment, patient, schedule)
                };
            }

            // ── NHÁNH B: Không có slot → tạo lịch ChoPhanCong ───────────────
            {
                string bookingCode = await CodeGenerator.NextAppointmentCodeAsync(_db);

                var pendingAppointment = new Appointment
                {
                    PatientId     = dto.PatientId,
                    DoctorId      = dto.DoctorId,        // null = chưa biết bác sĩ
                    ScheduleId    = null,                 // lễ tân sẽ gán sau
                    PreferredDate = dto.PreferredDate,
                    BookingCode   = bookingCode,
                    BookedAt      = DateTime.UtcNow,
                    Status        = AppointmentStatus.ChoPhanCong,
                    Note          = dto.Note?.Trim()
                };

                _db.Appointments.Add(pendingAppointment);
                await _db.SaveChangesAsync();

                string preferredInfo = dto.PreferredDate.HasValue
                    ? $" vào ngày {dto.PreferredDate.Value:dd/MM/yyyy}"
                    : string.Empty;

                string doctorInfo = dto.DoctorId.HasValue
                    ? " với bác sĩ bạn đã chọn"
                    : string.Empty;

                var pendingNotification = new Notification
                {
                    UserId        = patient.UserId,
                    AppointmentId = pendingAppointment.Id,
                    Title         = "Yêu cầu đặt lịch đã được ghi nhận",
                    Content       = $"Yêu cầu khám{preferredInfo}{doctorInfo} (mã: {bookingCode}) đã được tiếp nhận. " +
                                    "Lễ tân sẽ xếp lịch và thông báo cho bạn sớm nhất.",
                    Type   = NotificationType.XacNhanLich,
                    SentAt = DateTime.UtcNow,
                    IsRead = false
                };
                _db.Notifications.Add(pendingNotification);
                await _db.SaveChangesAsync();

                var (pendingEmail, pendingName) = await GetUserContactAsync(patient.UserId);
                _ = _email.SendPendingBookingNoticeAsync(
                    pendingEmail, pendingName, bookingCode,
                    dto.PreferredDate, patient.FullName, pendingAppointment.Note);

                return new AppointmentResponseDto
                {
                    Success = true,
                    Message = $"Yêu cầu đặt lịch đã được ghi nhận (mã: {bookingCode}). " +
                              "Lễ tân sẽ liên hệ xác nhận lịch khám cụ thể.",
                    Data = MapToDetail(pendingAppointment, patient, null)
                };
            }
        }

        // =====================================================================
        // PHÂN CÔNG LỊCH — lễ tân gán bác sĩ + khung giờ cho lịch ChoPhanCong
        // PUT /api/appointment/assign
        // =====================================================================
        public async Task<AppointmentResponseDto> AssignScheduleAsync(AssignScheduleDto dto)
        {
            var appointment = await _db.Appointments
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a =>
                    a.Id     == dto.AppointmentId &&
                    a.Status == AppointmentStatus.ChoPhanCong);

            if (appointment == null)
                return Fail("Không tìm thấy lịch chờ phân công hoặc lịch đã được xử lý.");

            var schedule = await _db.WorkSchedules
                .Include(w => w.Doctor)
                .FirstOrDefaultAsync(w => w.Id == dto.ScheduleId);

            if (schedule == null)
                return Fail("Khung giờ không tồn tại.");

            if (schedule.CurrentPatients >= schedule.MaxPatients)
                return Fail("Khung giờ này đã đầy, vui lòng chọn khung giờ khác.");

            if (schedule.WorkDate.Date < DateTime.Today)
                return Fail("Không thể xếp vào khung giờ đã qua.");

            bool alreadyBooked = await _db.Appointments.AnyAsync(a =>
                a.PatientId  == appointment.PatientId &&
                a.ScheduleId == dto.ScheduleId &&
                a.Id         != appointment.Id &&
                a.Status     != AppointmentStatus.DaHuy &&
                a.Status     != AppointmentStatus.VangMat);

            if (alreadyBooked)
                return Fail("Bệnh nhân đã có lịch trong khung giờ này.");

            appointment.ScheduleId = schedule.Id;
            appointment.DoctorId   = schedule.DoctorId;
            appointment.Status     = AppointmentStatus.ChoXacNhan;

            if (!string.IsNullOrWhiteSpace(dto.Note))
                appointment.Note = dto.Note;

            schedule.CurrentPatients += 1;

            var notification = new Notification
            {
                UserId        = appointment.Patient.UserId,
                AppointmentId = appointment.Id,
                Title         = "Lịch khám đã được xếp lịch",
                Content       = $"Lịch khám (mã: {appointment.BookingCode}) đã được xếp vào " +
                                $"ngày {schedule.WorkDate:dd/MM/yyyy} " +
                                $"lúc {(int)schedule.StartTime.TotalHours:D2}:{schedule.StartTime.Minutes:D2} " +
                                $"với {schedule.Doctor.FullName}. Vui lòng chờ xác nhận từ phòng khám.",
                Type   = NotificationType.XacNhanLich,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            var (userEmail, userName) = await GetUserContactAsync(appointment.Patient.UserId);
            _ = _email.SendBookingConfirmationAsync(
                userEmail, userName, appointment.BookingCode,
                schedule.Doctor.FullName,
                schedule.WorkDate.ToString("dd/MM/yyyy"),
                $"{(int)schedule.StartTime.TotalHours:D2}:{schedule.StartTime.Minutes:D2}",
                $"{(int)schedule.EndTime.TotalHours:D2}:{schedule.EndTime.Minutes:D2}",
                appointment.Patient.FullName, appointment.Note);

            return new AppointmentResponseDto
            {
                Success = true,
                Message = $"Đã xếp lịch và thông báo bệnh nhân. " +
                          $"Ngày {schedule.WorkDate:dd/MM/yyyy} " +
                          $"lúc {(int)schedule.StartTime.TotalHours:D2}:{schedule.StartTime.Minutes:D2} " +
                          $"với {schedule.Doctor.FullName}.",
                Data = MapToDetail(appointment, appointment.Patient, schedule)
            };
        }

        // =====================================================================
        // LẤY DANH SÁCH LỊCH CHỜ PHÂN CÔNG — dành cho lễ tân
        // GET /api/appointment/pending-assignment
        // =====================================================================
        public async Task<List<AppointmentDetailDto>> GetPendingAssignmentAsync(
            PendingAssignmentFilterDto? filter = null)
        {
            var query = _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.Reviews)
                .Include(a => a.Payment)
                .Where(a => a.Status == AppointmentStatus.ChoPhanCong)
                .AsQueryable();

            if (filter != null)
            {
                if (filter.FromDate.HasValue)
                    query = query.Where(a =>
                        a.PreferredDate == null ||
                        a.PreferredDate.Value.Date >= filter.FromDate.Value.Date);

                if (filter.ToDate.HasValue)
                    query = query.Where(a =>
                        a.PreferredDate == null ||
                        a.PreferredDate.Value.Date <= filter.ToDate.Value.Date);

                if (!string.IsNullOrWhiteSpace(filter.Specialty) && filter.Specialty != "all")
                    query = query.Where(a =>
                        a.Doctor != null && a.Doctor.Specialty == filter.Specialty);

                if (!string.IsNullOrWhiteSpace(filter.Keyword))
                {
                    var kw = filter.Keyword.Trim().ToLower();
                    query = query.Where(a =>
                        a.BookingCode.ToLower().Contains(kw) ||
                        a.Patient.FullName.ToLower().Contains(kw));
                }
            }

            var list = await query
                .OrderBy(a => a.PreferredDate)
                .ThenBy(a => a.BookedAt)
                .ToListAsync();

            return list.Select(a => MapToDetail(a, a.Patient, a.Schedule)).ToList();
        }

        // =====================================================================
        // LẤY DANH SÁCH LỊCH KHÁM CỦA BỆNH NHÂN
        // =====================================================================
        public async Task<List<AppointmentDetailDto>> GetByPatientUserIdAsync(int userId)
        {
            var appointments = await _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.Schedule)
                .Include(a => a.Reviews)
                .Include(a => a.Payment)
                .Where(a => a.Patient.UserId == userId)
                .OrderByDescending(a => a.BookedAt)
                .ToListAsync();

            return appointments.Select(a => MapToDetail(a, a.Patient, a.Schedule)).ToList();
        }

        // =====================================================================
        // HUỶ LỊCH KHÁM
        // =====================================================================
        public async Task<AppointmentResponseDto> CancelAsync(
            int appointmentId, int userId, string? cancellationReason = null)
        {
            var appointment = await _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Schedule)
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
                return Fail("Không tìm thấy lịch khám.");

            if (appointment.Patient.UserId != userId)
                return Fail("Bạn không có quyền huỷ lịch này.");

            if (appointment.Status == AppointmentStatus.DaHuy)
                return Fail("Lịch này đã được huỷ trước đó rồi.");

            // Lịch ChoPhanCong: huỷ tự do (chưa có slot, không cần kiểm tra ngày)
            if (appointment.Status != AppointmentStatus.ChoPhanCong)
            {
                if (appointment.Schedule != null &&
                    appointment.Schedule.WorkDate.Date <= DateTime.Today)
                    return Fail("Không thể huỷ lịch khám trong ngày hoặc đã qua.");
            }

            appointment.Status = AppointmentStatus.DaHuy;

            if (!string.IsNullOrWhiteSpace(cancellationReason))
                appointment.CancellationReason = cancellationReason.Trim();

            // Hoàn trả slot nếu đã có schedule
            if (appointment.Schedule != null)
                appointment.Schedule.CurrentPatients =
                    Math.Max(0, appointment.Schedule.CurrentPatients - 1);

            string doctorInfo = appointment.Doctor != null
                ? $" với {appointment.Doctor.FullName}" : string.Empty;
            string dateInfo = appointment.Schedule != null
                ? $" ngày {appointment.Schedule.WorkDate:dd/MM/yyyy}" : string.Empty;

            var notification = new Notification
            {
                UserId        = userId,
                AppointmentId = appointment.Id,
                Title         = "Lịch khám đã được huỷ",
                Content       = $"Lịch khám{dateInfo}{doctorInfo} (mã: {appointment.BookingCode}) " +
                                "đã được huỷ theo yêu cầu của bạn.",
                Type   = NotificationType.HuyLich,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            if (appointment.Schedule != null)
            {
                var (cancelEmail, cancelName) = await GetUserContactAsync(userId);
                _ = _email.SendCancellationAsync(
                    cancelEmail, cancelName, appointment.BookingCode,
                    appointment.Doctor?.FullName ?? "N/A",
                    appointment.Schedule.WorkDate.ToString("dd/MM/yyyy"),
                    $"{(int)appointment.Schedule.StartTime.TotalHours:D2}:{appointment.Schedule.StartTime.Minutes:D2}",
                    $"{(int)appointment.Schedule.EndTime.TotalHours:D2}:{appointment.Schedule.EndTime.Minutes:D2}",
                    appointment.Patient.FullName);
            }

            return new AppointmentResponseDto { Success = true, Message = "Huỷ lịch thành công." };
        }

        // =====================================================================
        // LẤY DANH SÁCH LỊCH KHÁM THEO NGÀY
        // =====================================================================
        public async Task<List<AppointmentDetailDto>> GetByDateAsync(DateTime date, int? doctorId = null)
        {
            var query = _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.Schedule)
                .Include(a => a.Reviews)
                .Include(a => a.Payment)
                // Lấy cả lịch ChoPhanCong cùng ngày (theo PreferredDate)
                .Where(a => (a.Schedule != null && a.Schedule.WorkDate.Date == date.Date) ||
                            (a.Schedule == null && a.PreferredDate.HasValue &&
                             a.PreferredDate.Value.Date == date.Date));

            if (doctorId.HasValue)
                query = query.Where(a => a.DoctorId == doctorId.Value);

            var list = await query.ToListAsync();

            // Ưu tiên hiển thị: DaDen > DangKham > DaXacNhan > ChoXacNhan > HoanThanh > VangMat > DaHuy
            // Trong cùng nhóm: BookedAt mới nhất lên đầu (ai đến / xác nhận sau lên trên)
            static int StatusPriority(AppointmentStatus s) => s switch
            {
                AppointmentStatus.DaDen      => 0,
                AppointmentStatus.DangKham   => 1,
                AppointmentStatus.DaXacNhan  => 2,
                AppointmentStatus.ChoXacNhan => 3,
                AppointmentStatus.HoanThanh  => 4,
                AppointmentStatus.VangMat    => 5,
                AppointmentStatus.DaHuy      => 6,
                _                            => 7
            };

            list = list
                .OrderBy(a => StatusPriority(a.Status))
                .ThenByDescending(a => a.BookedAt)
                .ToList();

            return list.Select(a => MapToDetail(a, a.Patient, a.Schedule)).ToList();
        }

        // =====================================================================
        // ĐỔI LỊCH KHÁM
        // =====================================================================
        public async Task<AppointmentResponseDto> RescheduleAsync(RescheduleDto dto)
        {
            var appointment = await _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Schedule)
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == dto.AppointmentId);

            if (appointment == null)
                return Fail("Không tìm thấy lịch khám.");

            var allowedStatuses = new[]
            {
                AppointmentStatus.ChoPhanCong,
                AppointmentStatus.ChoXacNhan,
                AppointmentStatus.DaXacNhan,
                AppointmentStatus.DaDen
            };
            if (!allowedStatuses.Contains(appointment.Status))
                return Fail("Không thể đổi lịch đã hoàn thành, đang khám, đã huỷ hoặc vắng mặt.");

            var newSchedule = await _db.WorkSchedules
                .Include(w => w.Doctor)
                .FirstOrDefaultAsync(w => w.Id == dto.NewScheduleId);

            if (newSchedule == null)             return Fail("Khung giờ mới không tồn tại.");
            if (newSchedule.CurrentPatients >= newSchedule.MaxPatients)
                                                 return Fail("Khung giờ mới đã đầy.");
            if (newSchedule.WorkDate.Date < DateTime.Today)
                                                 return Fail("Không thể đổi sang khung giờ đã qua.");
            if (newSchedule.Id == appointment.ScheduleId)
                                                 return Fail("Đây đã là khung giờ hiện tại của lịch khám.");

            bool alreadyBooked = await _db.Appointments.AnyAsync(a =>
                a.PatientId  == appointment.PatientId &&
                a.ScheduleId == dto.NewScheduleId &&
                a.Id         != dto.AppointmentId &&
                a.Status     != AppointmentStatus.DaHuy &&
                a.Status     != AppointmentStatus.VangMat);
            if (alreadyBooked) return Fail("Bệnh nhân đã có lịch trong khung giờ mới này.");

            // Hoàn trả slot cũ nếu đã có
            if (appointment.Schedule != null)
                appointment.Schedule.CurrentPatients =
                    Math.Max(0, appointment.Schedule.CurrentPatients - 1);

            newSchedule.CurrentPatients += 1;

            appointment.ScheduleId = newSchedule.Id;
            appointment.DoctorId   = newSchedule.DoctorId;
            appointment.Status     = AppointmentStatus.DaXacNhan;
            if (!string.IsNullOrWhiteSpace(dto.Note)) appointment.Note = dto.Note;

            var notification = new Notification
            {
                UserId        = appointment.Patient.UserId,
                AppointmentId = appointment.Id,
                Title         = "Lịch khám đã được đổi",
                Content       = $"Lịch khám (mã {appointment.BookingCode}) đã được đổi sang " +
                                $"ngày {newSchedule.WorkDate:dd/MM/yyyy} " +
                                $"lúc {(int)newSchedule.StartTime.TotalHours:D2}:{newSchedule.StartTime.Minutes:D2} " +
                                $"với {newSchedule.Doctor.FullName}.",
                Type   = NotificationType.DoiLich,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            return new AppointmentResponseDto
            {
                Success = true,
                Message = $"Đổi lịch thành công sang ngày {newSchedule.WorkDate:dd/MM/yyyy} " +
                          $"lúc {(int)newSchedule.StartTime.TotalHours:D2}:{newSchedule.StartTime.Minutes:D2} " +
                          $"với {newSchedule.Doctor.FullName}.",
                Data = MapToDetail(appointment, appointment.Patient, newSchedule)
            };
        }

        // =====================================================================
        // TÌM KIẾM LỊCH KHÁM
        // =====================================================================
        public async Task<List<AppointmentDetailDto>> SearchAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<AppointmentDetailDto>();

            var q = keyword.Trim().ToLower();

            var list = await _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.Schedule)
                .Include(a => a.Reviews)
                .Include(a => a.Payment)
                .Where(a => a.BookingCode.ToLower().Contains(q) ||
                            a.Patient.FullName.ToLower().Contains(q))
                .OrderByDescending(a => a.BookedAt)
                .Take(50)
                .ToListAsync();

            return list.Select(a => MapToDetail(a, a.Patient, a.Schedule)).ToList();
        }

        // =====================================================================
        // CẬP NHẬT TRẠNG THÁI
        // =====================================================================
        public async Task<AppointmentResponseDto> UpdateStatusAsync(UpdateAppointmentStatusDto dto)
        {
            var appointment = await _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Schedule)
                .Include(a => a.Doctor)
                .Include(a => a.Reviews)
                .Include(a => a.Payment)
                .FirstOrDefaultAsync(a => a.Id == dto.AppointmentId);

            if (appointment == null)
                return Fail("Không tìm thấy lịch khám.");

            // Lịch ChoPhanCong phải được assign trước, không được chuyển trạng thái trực tiếp
            // (ngoại lệ: huỷ vẫn cho phép)
            if (appointment.Status == AppointmentStatus.ChoPhanCong &&
                dto.NewStatus != AppointmentStatus.DaHuy)
                return Fail("Lịch chờ phân công phải được xếp lịch trước (dùng API assign).");

            appointment.Status = dto.NewStatus;
            if (!string.IsNullOrWhiteSpace(dto.Note))       appointment.Note      = dto.Note;
            if (!string.IsNullOrWhiteSpace(dto.Diagnosis))  appointment.Diagnosis = dto.Diagnosis;

            await _db.SaveChangesAsync();

            return new AppointmentResponseDto
            {
                Success = true,
                Message = "Cập nhật trạng thái thành công.",
                Data    = MapToDetail(appointment, appointment.Patient, appointment.Schedule)
            };
        }

        // =====================================================================
        // HELPERS
        // =====================================================================
        private static AppointmentResponseDto Fail(string msg) =>
            new() { Success = false, Message = msg };

        /// <summary>
        /// Map Appointment entity → DTO.
        /// Xử lý an toàn khi Schedule/Doctor là null (lịch ChoPhanCong).
        /// </summary>
        private static AppointmentDetailDto MapToDetail(
            Appointment a, Patient p, WorkSchedule? w) => new()
        {
            Id          = a.Id,
            BookingCode = a.BookingCode,
            PatientId   = p.Id,
            PatientName = p.FullName,

            // DoctorId và DoctorName: nullable khi ChoPhanCong
            DoctorId   = a.DoctorId,
            DoctorName = a.Doctor?.FullName,
            Specialty  = a.Doctor?.Specialty,

            // WorkDate/StartTime/EndTime: nullable khi chưa có schedule
            WorkDate  = w?.WorkDate,
            StartTime = w is not null
                ? $"{(int)w.StartTime.TotalHours:D2}:{w.StartTime.Minutes:D2}" : null,
            EndTime   = w is not null
                ? $"{(int)w.EndTime.TotalHours:D2}:{w.EndTime.Minutes:D2}" : null,

            PreferredDate = a.PreferredDate,
            PreferredDateDisplay = a.PreferredDate.HasValue
                ? $"Ngày mong muốn: {a.PreferredDate.Value:dd/MM/yyyy}" : null,

            Status        = a.Status.ToString(),
            StatusDisplay = a.Status switch
            {
                AppointmentStatus.ChoPhanCong => "Chờ phân công",
                AppointmentStatus.ChoXacNhan  => "Chờ xác nhận",
                AppointmentStatus.DaXacNhan   => "Đã xác nhận",
                AppointmentStatus.DaDen       => "Đã đến",
                AppointmentStatus.DangKham    => "Đang khám",
                AppointmentStatus.HoanThanh   => "Hoàn thành",
                AppointmentStatus.DaHuy       => "Đã huỷ",
                AppointmentStatus.VangMat     => "Vắng mặt",
                _                             => "Không xác định"
            },

            Note      = a.Note,
            Diagnosis = a.Diagnosis,
            BookedAt  = a.BookedAt,

            // Fields gốc — giữ lại để frontend không bị vỡ
            IsReturn           = a.IsReturn,
            CancellationReason = a.CancellationReason,
            HasReview          = a.Reviews?.Any() == true,
            PaymentMethod      = a.Payment?.Method,
            PaymentStatus      = a.Payment?.Status.ToString(),
            PaymentAmount      = a.Payment?.Amount,
            PaymentExpiresAt   = a.Payment?.ExpiresAt
        };

        /// <summary>Lấy email và tên đầy đủ của user theo UserId</summary>
        private async Task<(string email, string name)> GetUserContactAsync(int userId)
        {
            var user = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => new { u.Email, FullName = u.HoTenDem + " " + u.Ten })
                .FirstOrDefaultAsync();

            return (user?.Email ?? "", (user?.FullName ?? "").Trim());
        }
    }
}
