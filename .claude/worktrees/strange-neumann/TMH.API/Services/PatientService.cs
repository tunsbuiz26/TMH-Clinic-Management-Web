using Microsoft.EntityFrameworkCore;
using TMH.API.Data;
using TMH.Shared.DTOs;
using TMH.Shared.Models;

namespace TMH.API.Services
{
    /// <summary>
    /// PatientService xử lý toàn bộ business logic liên quan đến hồ sơ bệnh nhân.
    ///
    /// Quy tắc nghiệp vụ quan trọng:
    ///   - Một tài khoản User có thể sở hữu nhiều hồ sơ Patient (bản thân + người thân).
    ///   - Chỉ chủ sở hữu (UserId khớp) mới được xem, sửa, xóa hồ sơ của mình.
    ///   - Không xóa vật lý hồ sơ nếu đã có lịch khám liên quan — trả về lỗi rõ ràng.
    ///   - RecordCode sinh tự động theo quy tắc "BN-{năm}-{số thứ tự 4 chữ số}".
    /// </summary>
    public class PatientService
    {
        private readonly AppDbContext _db;

        public PatientService(AppDbContext db)
        {
            _db = db;
        }

        // =====================================================================
        // LẤY DANH SÁCH HỒ SƠ CỦA MỘT USER
        // Dùng để populate dropdown chọn hồ sơ trên form đặt lịch
        // =====================================================================
        public async Task<List<PatientDto>> GetByUserIdAsync(int userId)
        {
            var patients = await _db.Patients
                .Where(p => p.UserId == userId)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            return patients.Select(MapToDto).ToList();
        }

        // =====================================================================
        // LẤY CHI TIẾT MỘT HỒ SƠ
        // Kiểm tra quyền sở hữu trước khi trả về
        // =====================================================================
        public async Task<PatientResponseDto> GetByIdAsync(int patientId, int userId)
        {
            var patient = await _db.Patients.FindAsync(patientId);

            if (patient == null)
                return Fail("Không tìm thấy hồ sơ bệnh nhân.");

            // Chỉ chủ sở hữu mới được xem chi tiết
            if (patient.UserId != userId)
                return Fail("Bạn không có quyền xem hồ sơ này.");

            return new PatientResponseDto
            {
                Success = true,
                Message = "Lấy hồ sơ thành công.",
                Data = MapToDto(patient)
            };
        }

        // =====================================================================
        // TẠO HỒ SƠ MỚI
        // RecordCode sinh tự động, gắn với UserId của người đang đăng nhập
        // =====================================================================
        public async Task<PatientResponseDto> CreateAsync(PatientUpsertDto dto, int userId)
        {
            // Sinh mã hồ sơ tự động: BN-2026-0006 (nối tiếp số lượng hiện có)
            int count = await _db.Patients.CountAsync();
            string code = $"BN-{DateTime.Now.Year}-{(count + 1):D4}";

            // Đảm bảo không trùng mã (trường hợp race condition)
            while (await _db.Patients.AnyAsync(p => p.RecordCode == code))
            {
                count++;
                code = $"BN-{DateTime.Now.Year}-{(count + 1):D4}";
            }

            var patient = new Patient
            {
                UserId = userId,
                FullName = dto.FullName.Trim(),
                DateOfBirth = dto.DateOfBirth,
                Gender = dto.Gender.Trim(),
                RecordCode = code,
                MedicalHistory = dto.MedicalHistory?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _db.Patients.Add(patient);
            await _db.SaveChangesAsync();

            return new PatientResponseDto
            {
                Success = true,
                Message = $"Tạo hồ sơ thành công! Mã hồ sơ: {code}",
                Data = MapToDto(patient)
            };
        }

        // =====================================================================
        // CẬP NHẬT HỒ SƠ
        // Chỉ cho phép sửa thông tin cá nhân, không đổi RecordCode
        // =====================================================================
        public async Task<PatientResponseDto> UpdateAsync(PatientUpsertDto dto, int userId)
        {
            var patient = await _db.Patients.FindAsync(dto.Id);

            if (patient == null)
                return Fail("Không tìm thấy hồ sơ bệnh nhân.");

            if (patient.UserId != userId)
                return Fail("Bạn không có quyền chỉnh sửa hồ sơ này.");

            // Chỉ cập nhật các trường được phép thay đổi
            patient.FullName = dto.FullName.Trim();
            patient.DateOfBirth = dto.DateOfBirth;
            patient.Gender = dto.Gender.Trim();
            patient.MedicalHistory = dto.MedicalHistory?.Trim();
            // RecordCode và CreatedAt KHÔNG thay đổi

            await _db.SaveChangesAsync();

            return new PatientResponseDto
            {
                Success = true,
                Message = "Cập nhật hồ sơ thành công.",
                Data = MapToDto(patient)
            };
        }

        // =====================================================================
        // XÓA HỒ SƠ
        // Không xóa nếu hồ sơ đang có lịch khám chưa hoàn thành
        // =====================================================================
        public async Task<PatientResponseDto> DeleteAsync(int patientId, int userId)
        {
            var patient = await _db.Patients
                .Include(p => p.Appointments)
                .FirstOrDefaultAsync(p => p.Id == patientId);

            if (patient == null)
                return Fail("Không tìm thấy hồ sơ bệnh nhân.");

            if (patient.UserId != userId)
                return Fail("Bạn không có quyền xóa hồ sơ này.");

            // Kiểm tra còn lịch khám đang hoạt động
            bool hasActiveAppointments = patient.Appointments.Any(a =>
                a.Status != AppointmentStatus.DaHuy &&
                a.Status != AppointmentStatus.HoanThanh &&
                a.Status != AppointmentStatus.VangMat);

            if (hasActiveAppointments)
                return Fail("Không thể xóa hồ sơ đang có lịch khám. Vui lòng hủy các lịch khám trước.");

            _db.Patients.Remove(patient);
            await _db.SaveChangesAsync();

            return new PatientResponseDto
            {
                Success = true,
                Message = $"Đã xóa hồ sơ '{patient.FullName}' thành công."
            };
        }

        // =====================================================================
        // HELPERS
        // =====================================================================
        private static PatientResponseDto Fail(string message) =>
            new() { Success = false, Message = message };

        private static PatientDto MapToDto(Patient p) => new()
        {
            Id = p.Id,
            FullName = p.FullName,
            DateOfBirth = p.DateOfBirth,
            Gender = p.Gender,
            RecordCode = p.RecordCode,
            MedicalHistory = p.MedicalHistory,
            CreatedAt = p.CreatedAt
        };
    }
}