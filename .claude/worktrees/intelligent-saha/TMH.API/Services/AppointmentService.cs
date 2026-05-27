using TMH.API.Data;
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
        // Dùng để render dropdown trên form đặt lịch
        // =====================================================================
        public async Task<List<DoctorScheduleDto>> GetAvailableDoctorsAsync(DateTime? date = null)
        {
            var targetDate = date ?? DateTime.Today;

            var doctors = await _db.Doctors
                .Where(d => d.IsAvailable)
                .Include(d => d.WorkSchedules)
                .ToListAsync();

            var result = new List<DoctorScheduleDto>();

            foreach (var doc in doctors)
            {
                // Chỉ lấy slot trong tương lai và còn chỗ trống
                var slots = doc.WorkSchedules
                    .Where(w => w.WorkDate.Date >= targetDate.Date
                             && w.CurrentPatients < w.MaxPatients)
                    .OrderBy(w => w.WorkDate)
                    .ThenBy(w => w.StartTime)
                    .Select(w => new ScheduleSlotDto
                    {
                        ScheduleId = w.Id,
                        WorkDate = w.WorkDate,
                        StartTime = $"{(int)w.StartTime.TotalHours:D2}:{w.StartTime.Minutes:D2}",
                        EndTime = $"{(int)w.EndTime.TotalHours:D2}:{w.EndTime.Minutes:D2}",
                        MaxPatients = w.MaxPatients,
                        CurrentPatients = w.CurrentPatients,
                        RemainingSlots = w.MaxPatients - w.CurrentPatients
                    })
                    .ToList();

                result.Add(new DoctorScheduleDto
                {
                    DoctorId = doc.Id,
                    FullName = doc.FullName,
                    Specialty = doc.Specialty,
                    Degree = doc.Degree,
                    AvailableSlots = slots
                });
            }

            return result;
        }

        // =====================================================================
        // ĐẶT LỊCH KHÁM
        // =====================================================================
        public async Task<AppointmentResponseDto> BookAsync(BookAppointmentDto dto)
        {
            // 1. Kiểm tra hồ sơ bệnh nhân tồn tại
            var patient = await _db.Patients.FindAsync(dto.PatientId);
            if (patient == null)
                return Fail("Không tìm thấy hồ sơ bệnh nhân.");

            // 2. Kiểm tra khung giờ tồn tại và còn chỗ
            var schedule = await _db.WorkSchedules
                .Include(w => w.Doctor)
                .FirstOrDefaultAsync(w => w.Id == dto.ScheduleId);

            if (schedule == null)
                return Fail("Khung giờ không tồn tại.");

            if (schedule.CurrentPatients >= schedule.MaxPatients)
                return Fail("Khung giờ này đã đầy, vui lòng chọn khung giờ khác.");

            if (schedule.WorkDate.Date < DateTime.Today)
                return Fail("Không thể đặt lịch cho ngày đã qua.");

            // 3. Kiểm tra bệnh nhân chưa đặt trùng khung giờ này
            bool alreadyBooked = await _db.Appointments.AnyAsync(a =>
                a.PatientId == dto.PatientId &&
                a.ScheduleId == dto.ScheduleId &&
                a.Status != AppointmentStatus.DaHuy &&
                a.Status != AppointmentStatus.VangMat);

            if (alreadyBooked)
                return Fail("Hồ sơ này đã có lịch trong khung giờ đó rồi.");

            // 4. Tạo mã đặt lịch tự động: APT-2026-00006
            int count = await _db.Appointments.CountAsync();
            string bookingCode = $"APT-{DateTime.Now.Year}-{(count + 1):D5}";

            // 5. Tạo Appointment
            var appointment = new Appointment
            {
                PatientId = dto.PatientId,
                DoctorId = schedule.DoctorId,
                ScheduleId = dto.ScheduleId,
                BookingCode = bookingCode,
                BookedAt = DateTime.UtcNow,
                Status = AppointmentStatus.ChoXacNhan,
                Note = dto.Note?.Trim()
            };

            _db.Appointments.Add(appointment);

            // 6. Tăng CurrentPatients lên 1
            schedule.CurrentPatients += 1;

            await _db.SaveChangesAsync();

            // 7. Tạo Notification xác nhận gửi cho bệnh nhân
            var notification = new Notification
            {
                UserId = patient.UserId,
                AppointmentId = appointment.Id,
                Title = "Đặt lịch thành công",
                Content = $"Lịch khám ngày {schedule.WorkDate:dd/MM/yyyy} " +
                                $"lúc {(int)schedule.StartTime.TotalHours:D2}:{schedule.StartTime.Minutes:D2} với {schedule.Doctor.FullName} " +
                                $"đã được ghi nhận. Mã lịch: {bookingCode}",
                Type = NotificationType.XacNhanLich,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            // 8. Gửi email xác nhận (fire-and-forget, không chặn response)
            var userEmail  = await _db.Users
                .Where(u => u.Id == patient.UserId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();
            var userName = await _db.Users
                .Where(u => u.Id == patient.UserId)
                .Select(u => u.HoTenDem + " " + u.Ten)
                .FirstOrDefaultAsync();

            _ = _email.SendBookingConfirmationAsync(
                userEmail ?? "", (userName ?? "").Trim(),
                bookingCode,
                schedule.Doctor.FullName,
                schedule.WorkDate.ToString("dd/MM/yyyy"),
                $"{(int)schedule.StartTime.TotalHours:D2}:{schedule.StartTime.Minutes:D2}",
                $"{(int)schedule.EndTime.TotalHours:D2}:{schedule.EndTime.Minutes:D2}",
                patient.FullName,
                appointment.Note
            );

            return new AppointmentResponseDto
            {
                Success = true,
                Message = $"Đặt lịch thành công! Mã lịch của bạn: {bookingCode}",
                Data = MapToDetail(appointment, patient, schedule)
            };
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
                .Where(a => a.Patient.UserId == userId)
                .OrderByDescending(a => a.Schedule.WorkDate)
                .ToListAsync();

            return appointments.Select(a => MapToDetail(a, a.Patient, a.Schedule)).ToList();
        }

        // =====================================================================
        // HUỶ LỊCH KHÁM
        // =====================================================================
        public async Task<AppointmentResponseDto> CancelAsync(int appointmentId, int userId)
        {
            var appointment = await _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Schedule)
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
                return Fail("Không tìm thấy lịch khám.");

            // Chỉ chủ hồ sơ mới được huỷ
            if (appointment.Patient.UserId != userId)
                return Fail("Bạn không có quyền huỷ lịch này.");

            // Chỉ huỷ được khi chưa đến ngày khám
            if (appointment.Schedule.WorkDate.Date <= DateTime.Today)
                return Fail("Không thể huỷ lịch khám trong ngày hoặc đã qua.");

            if (appointment.Status == AppointmentStatus.DaHuy)
                return Fail("Lịch này đã được huỷ trước đó rồi.");

            appointment.Status = AppointmentStatus.DaHuy;

            // Giảm CurrentPatients về
            appointment.Schedule.CurrentPatients =
                Math.Max(0, appointment.Schedule.CurrentPatients - 1);

            // Tạo Notification huỷ lịch
            var notification = new Notification
            {
                UserId = userId,
                AppointmentId = appointment.Id,
                Title = "Lịch khám đã được huỷ",
                Content = $"Lịch khám ngày {appointment.Schedule.WorkDate:dd/MM/yyyy} " +
                                $"với {appointment.Doctor.FullName} đã được huỷ theo yêu cầu của bạn.",
                Type = NotificationType.HuyLich,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            // Gửi email thông báo huỷ (fire-and-forget)
            var cancelUserEmail = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();
            var cancelUserName = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.HoTenDem + " " + u.Ten)
                .FirstOrDefaultAsync();

            _ = _email.SendCancellationAsync(
                cancelUserEmail ?? "", (cancelUserName ?? "").Trim(),
                appointment.BookingCode,
                appointment.Doctor.FullName,
                appointment.Schedule.WorkDate.ToString("dd/MM/yyyy"),
                $"{(int)appointment.Schedule.StartTime.TotalHours:D2}:{appointment.Schedule.StartTime.Minutes:D2}",
                $"{(int)appointment.Schedule.EndTime.TotalHours:D2}:{appointment.Schedule.EndTime.Minutes:D2}",
                appointment.Patient.FullName
            );

            return new AppointmentResponseDto
            {
                Success = true,
                Message = "Huỷ lịch thành công."
            };
        }

        // =====================================================================
        // LẤY DANH SÁCH LỊCH KHÁM THEO NGÀY — dùng cho lễ tân/bác sĩ
        // =====================================================================
        public async Task<List<AppointmentDetailDto>> GetByDateAsync(DateTime date, int? doctorId = null)
        {
            var query = _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.Schedule)
                .Where(a => a.Schedule.WorkDate.Date == date.Date);

            if (doctorId.HasValue)
                query = query.Where(a => a.DoctorId == doctorId.Value);

            var list = await query.OrderBy(a => a.Schedule.StartTime).ToListAsync();
            return list.Select(a => MapToDetail(a, a.Patient, a.Schedule)).ToList();
        }

        // =====================================================================
        // ĐỔI LỊCH KHÁM — dùng cho lễ tân (đổi bác sĩ hoặc đổi khung giờ)
        // =====================================================================
        public async Task<AppointmentResponseDto> RescheduleAsync(RescheduleDto dto)
        {
            // 1. Lấy lịch khám cần đổi
            var appointment = await _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Schedule)
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == dto.AppointmentId);

            if (appointment == null)
                return Fail("Không tìm thấy lịch khám.");

            // 2. Chỉ đổi được khi chưa bắt đầu khám
            var allowedStatuses = new[] {
                AppointmentStatus.ChoXacNhan,
                AppointmentStatus.DaXacNhan,
                AppointmentStatus.DaDen
            };
            if (!allowedStatuses.Contains(appointment.Status))
                return Fail("Không thể đổi lịch đã hoàn thành, đang khám, đã huỷ hoặc vắng mặt.");

            // 3. Lấy khung giờ mới
            var newSchedule = await _db.WorkSchedules
                .Include(w => w.Doctor)
                .FirstOrDefaultAsync(w => w.Id == dto.NewScheduleId);

            if (newSchedule == null)
                return Fail("Khung giờ mới không tồn tại.");

            if (newSchedule.CurrentPatients >= newSchedule.MaxPatients)
                return Fail("Khung giờ mới đã đầy, vui lòng chọn khung giờ khác.");

            if (newSchedule.WorkDate.Date < DateTime.Today)
                return Fail("Không thể đổi sang khung giờ đã qua.");

            // 4. Không đổi sang đúng slot cũ
            if (newSchedule.Id == appointment.ScheduleId)
                return Fail("Đây đã là khung giờ hiện tại của lịch khám.");

            // 5. Kiểm tra bệnh nhân chưa có lịch trùng ở slot mới
            bool alreadyBooked = await _db.Appointments.AnyAsync(a =>
                a.PatientId == appointment.PatientId &&
                a.ScheduleId == dto.NewScheduleId &&
                a.Id != dto.AppointmentId &&
                a.Status != AppointmentStatus.DaHuy &&
                a.Status != AppointmentStatus.VangMat);

            if (alreadyBooked)
                return Fail("Bệnh nhân đã có lịch trong khung giờ mới này.");

            var oldSchedule = appointment.Schedule;

            // 6. Hoàn trả slot cũ
            oldSchedule.CurrentPatients = Math.Max(0, oldSchedule.CurrentPatients - 1);

            // 7. Trừ slot mới
            newSchedule.CurrentPatients += 1;

            // 8. Cập nhật appointment
            appointment.ScheduleId = newSchedule.Id;
            appointment.DoctorId   = newSchedule.DoctorId;
            if (!string.IsNullOrWhiteSpace(dto.Note))
                appointment.Note = dto.Note;

            // 9. Tạo notification đổi lịch
            var notification = new Notification
            {
                UserId        = appointment.Patient.UserId,
                AppointmentId = appointment.Id,
                Title         = "Lịch khám đã được đổi",
                Content       = $"Lịch khám của bạn (mã {appointment.BookingCode}) đã được đổi sang " +
                                $"ngày {newSchedule.WorkDate:dd/MM/yyyy} " +
                                $"lúc {(int)newSchedule.StartTime.TotalHours:D2}:{newSchedule.StartTime.Minutes:D2} " +
                                $"với {newSchedule.Doctor.FullName}.",
                Type          = NotificationType.DoiLich,
                SentAt        = DateTime.UtcNow,
                IsRead        = false
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
        // TÌM KIẾM TOÀN BỘ LỊCH KHÁM — dùng cho lễ tân (không giới hạn ngày)
        // Tìm theo BookingCode hoặc tên bệnh nhân (không phân biệt hoa/thường)
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
                .Where(a => a.BookingCode.ToLower().Contains(q) ||
                            a.Patient.FullName.ToLower().Contains(q))
                .OrderByDescending(a => a.Schedule.WorkDate)
                .ThenBy(a => a.Schedule.StartTime)
                .Take(50) // giới hạn tối đa 50 kết quả
                .ToListAsync();

            return list.Select(a => MapToDetail(a, a.Patient, a.Schedule)).ToList();
        }

        // =====================================================================
        // CẬP NHẬT TRẠNG THÁI — dùng cho lễ tân và bác sĩ
        // =====================================================================
        public async Task<AppointmentResponseDto> UpdateStatusAsync(UpdateAppointmentStatusDto dto)
        {
            var appointment = await _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Schedule)
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == dto.AppointmentId);

            if (appointment == null)
                return Fail("Không tìm thấy lịch khám.");

            appointment.Status = dto.NewStatus;

            if (!string.IsNullOrWhiteSpace(dto.Note))
                appointment.Note = dto.Note;

            if (!string.IsNullOrWhiteSpace(dto.Diagnosis))
                appointment.Diagnosis = dto.Diagnosis;

            await _db.SaveChangesAsync();

            return new AppointmentResponseDto
            {
                Success = true,
                Message = "Cập nhật trạng thái thành công.",
                Data = MapToDetail(appointment, appointment.Patient, appointment.Schedule)
            };
        }

        // =====================================================================
        // HELPERS
        // =====================================================================
        private static AppointmentResponseDto Fail(string msg) =>
            new() { Success = false, Message = msg };

        private static AppointmentDetailDto MapToDetail(
            Appointment a, Patient p, WorkSchedule w) => new()
            {
                Id = a.Id,
                BookingCode = a.BookingCode,
                PatientName = p.FullName,
                DoctorName = a.Doctor?.FullName ?? "",
                Specialty = a.Doctor?.Specialty ?? "",
                WorkDate = w.WorkDate,
                StartTime = $"{(int)w.StartTime.TotalHours:D2}:{w.StartTime.Minutes:D2}",
                EndTime   = $"{(int)w.EndTime.TotalHours:D2}:{w.EndTime.Minutes:D2}",
                Status = a.Status.ToString(),
                StatusDisplay = a.Status switch
                {
                    AppointmentStatus.ChoXacNhan => "Chờ xác nhận",
                    AppointmentStatus.DaXacNhan => "Đã xác nhận",
                    AppointmentStatus.DaDen => "Đã đến",
                    AppointmentStatus.DangKham => "Đang khám",
                    AppointmentStatus.HoanThanh => "Hoàn thành",
                    AppointmentStatus.DaHuy => "Đã huỷ",
                    AppointmentStatus.VangMat => "Vắng mặt",
                    _ => "Không xác định"
                },
                Note = a.Note,
                Diagnosis = a.Diagnosis,
                BookedAt = a.BookedAt
            };
    }
}

