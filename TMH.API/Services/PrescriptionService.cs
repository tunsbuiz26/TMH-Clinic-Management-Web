using Microsoft.EntityFrameworkCore;
using TMH.API.Data;
using TMH.Shared.DTOs;
using TMH.Shared.Models;

namespace TMH.API.Services
{
    /// <summary>
    /// Xử lý toàn bộ logic liên quan đến đơn thuốc:
    ///   - Tạo đơn thuốc mới cho lịch khám
    ///   - Lấy đơn thuốc theo lịch khám
    ///   - Cập nhật đơn thuốc
    ///   - Xoá đơn thuốc
    ///
    /// FIX: DoctorId giờ là int? → so sánh phải dùng .Value hoặc null-check.
    ///      Schedule giờ có thể null (lịch ChoPhanCong) → MapToDto dùng ?. 
    /// </summary>
    public class PrescriptionService
    {
        private readonly AppDbContext _db;

        public PrescriptionService(AppDbContext db)
        {
            _db = db;
        }

        // ══════════════════════════════════════════════════════════
        // TẠO ĐƠN THUỐC MỚI
        // ══════════════════════════════════════════════════════════
        public async Task<PrescriptionResponseDto> CreateAsync(CreatePrescriptionDto dto, int doctorId)
        {
            // 1. Kiểm tra lịch khám tồn tại
            var appointment = await _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.Schedule)
                .FirstOrDefaultAsync(a => a.Id == dto.AppointmentId);

            if (appointment == null)
                return Fail("Không tìm thấy lịch khám.");

            // FIX: DoctorId là int? — phải so sánh với .Value hoặc dùng ==
            // Lịch ChoPhanCong (DoctorId = null) → bác sĩ không được kê đơn
            if (appointment.DoctorId == null || appointment.DoctorId.Value != doctorId)
                return Fail("Bạn không có quyền kê đơn cho lịch khám này.");

            if (appointment.Status != AppointmentStatus.DangKham &&
                appointment.Status != AppointmentStatus.HoanThanh)
                return Fail("Chỉ kê đơn được khi đang khám hoặc đã hoàn thành.");

            // 2. Kiểm tra đã có đơn thuốc chưa
            var exists = await _db.Prescriptions.AnyAsync(p => p.AppointmentId == dto.AppointmentId);
            if (exists)
                return Fail("Lịch khám này đã có đơn thuốc. Vui lòng dùng chức năng Cập nhật.");

            // 3. Tạo đơn thuốc
            var prescription = new Prescription
            {
                AppointmentId = dto.AppointmentId,
                DoctorId      = doctorId,
                Notes         = dto.Notes?.Trim(),
                IssuedAt      = DateTime.UtcNow,
                Items         = dto.Items.Select(i => new PrescriptionItem
                {
                    MedicineName = i.MedicineName.Trim(),
                    Dosage       = i.Dosage.Trim(),
                    Frequency    = i.Frequency.Trim(),
                    DurationDays = i.DurationDays,
                    Quantity     = i.Quantity,
                    Instructions = i.Instructions?.Trim()
                }).ToList()
            };

            _db.Prescriptions.Add(prescription);
            await _db.SaveChangesAsync();

            // 4. Load lại để trả về đầy đủ
            await _db.Entry(prescription).Reference(p => p.Doctor).LoadAsync();

            return new PrescriptionResponseDto
            {
                Success = true,
                Message = "Tạo đơn thuốc thành công.",
                Data    = MapToDto(prescription, appointment)
            };
        }

        // ══════════════════════════════════════════════════════════
        // LẤY ĐƠN THUỐC THEO APPOINTMENT ID
        // ══════════════════════════════════════════════════════════
        public async Task<PrescriptionResponseDto> GetByAppointmentAsync(int appointmentId)
        {
            var prescription = await _db.Prescriptions
                .Include(p => p.Items)
                .Include(p => p.Doctor)
                .Include(p => p.Appointment)
                    .ThenInclude(a => a.Patient)
                .Include(p => p.Appointment)
                    .ThenInclude(a => a.Schedule)
                .FirstOrDefaultAsync(p => p.AppointmentId == appointmentId);

            if (prescription == null)
                return Fail("Lịch khám này chưa có đơn thuốc.");

            return new PrescriptionResponseDto
            {
                Success = true,
                Message = "OK",
                Data    = MapToDto(prescription, prescription.Appointment)
            };
        }

        // ══════════════════════════════════════════════════════════
        // LẤY ĐƠN THUỐC THEO PRESCRIPTION ID
        // ══════════════════════════════════════════════════════════
        public async Task<PrescriptionResponseDto> GetByIdAsync(int id)
        {
            var prescription = await _db.Prescriptions
                .Include(p => p.Items)
                .Include(p => p.Doctor)
                .Include(p => p.Appointment)
                    .ThenInclude(a => a.Patient)
                .Include(p => p.Appointment)
                    .ThenInclude(a => a.Schedule)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prescription == null)
                return Fail("Không tìm thấy đơn thuốc.");

            return new PrescriptionResponseDto
            {
                Success = true,
                Message = "OK",
                Data    = MapToDto(prescription, prescription.Appointment)
            };
        }

        // ══════════════════════════════════════════════════════════
        // CẬP NHẬT ĐƠN THUỐC (ghi đè toàn bộ items)
        // ══════════════════════════════════════════════════════════
        public async Task<PrescriptionResponseDto> UpdateAsync(int id, UpdatePrescriptionDto dto, int doctorId)
        {
            var prescription = await _db.Prescriptions
                .Include(p => p.Items)
                .Include(p => p.Appointment)
                    .ThenInclude(a => a.Patient)
                .Include(p => p.Appointment)
                    .ThenInclude(a => a.Schedule)
                .Include(p => p.Doctor)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prescription == null)
                return Fail("Không tìm thấy đơn thuốc.");

            if (prescription.DoctorId != doctorId)
                return Fail("Bạn không có quyền chỉnh sửa đơn thuốc này.");

            prescription.Notes = dto.Notes?.Trim();

            _db.PrescriptionItems.RemoveRange(prescription.Items);
            prescription.Items = dto.Items.Select(i => new PrescriptionItem
            {
                PrescriptionId = prescription.Id,
                MedicineName   = i.MedicineName.Trim(),
                Dosage         = i.Dosage.Trim(),
                Frequency      = i.Frequency.Trim(),
                DurationDays   = i.DurationDays,
                Quantity       = i.Quantity,
                Instructions   = i.Instructions?.Trim()
            }).ToList();

            await _db.SaveChangesAsync();

            return new PrescriptionResponseDto
            {
                Success = true,
                Message = "Cập nhật đơn thuốc thành công.",
                Data    = MapToDto(prescription, prescription.Appointment)
            };
        }

        // ══════════════════════════════════════════════════════════
        // XOÁ ĐƠN THUỐC
        // ══════════════════════════════════════════════════════════
        public async Task<PrescriptionResponseDto> DeleteAsync(int id, int doctorId)
        {
            var prescription = await _db.Prescriptions
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prescription == null)
                return Fail("Không tìm thấy đơn thuốc.");

            if (prescription.DoctorId != doctorId)
                return Fail("Bạn không có quyền xoá đơn thuốc này.");

            _db.Prescriptions.Remove(prescription);
            await _db.SaveChangesAsync();

            return new PrescriptionResponseDto { Success = true, Message = "Đã xoá đơn thuốc." };
        }

        // ══════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════
        private static PrescriptionResponseDto Fail(string msg) =>
            new() { Success = false, Message = msg };

        /// <summary>
        /// Map Prescription + Appointment → DTO.
        /// FIX: a.Schedule có thể null (lịch ChoPhanCong) → dùng ?. để tránh NullReferenceException.
        /// </summary>
        private static PrescriptionDto MapToDto(Prescription p, Appointment a) => new()
        {
            Id                  = p.Id,
            AppointmentId       = a.Id,
            BookingCode         = a.BookingCode,
            PatientName         = a.Patient.FullName,
            PatientRecordCode   = a.Patient.RecordCode,
            PatientDateOfBirth  = a.Patient.DateOfBirth,
            PatientGender       = a.Patient.Gender,
            DoctorId            = p.DoctorId,
            DoctorName          = p.Doctor?.FullName ?? "",
            DoctorDegree        = p.Doctor?.Degree,
            // FIX: Schedule nullable — WorkDate là DateTime? trong DTO
            WorkDate            = a.Schedule?.WorkDate,
            Diagnosis           = a.Diagnosis ?? "",
            Notes               = p.Notes,
            IssuedAt            = p.IssuedAt,
            Items               = p.Items.Select(i => new PrescriptionItemDto
            {
                Id           = i.Id,
                MedicineName = i.MedicineName,
                Dosage       = i.Dosage,
                Frequency    = i.Frequency,
                DurationDays = i.DurationDays,
                Quantity     = i.Quantity,
                Instructions = i.Instructions
            }).ToList()
        };
    }
}
