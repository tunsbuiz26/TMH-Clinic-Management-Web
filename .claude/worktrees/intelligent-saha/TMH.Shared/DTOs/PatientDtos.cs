using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMH.Shared.DTOs
{
    /// <summary>
    /// DTO bệnh nhân gửi lên khi tạo hoặc cập nhật hồ sơ.
    /// Dùng chung cho cả Create và Update (Id = 0 → tạo mới, Id > 0 → cập nhật).
    /// </summary>
    public class PatientUpsertDto
    {
        // Id = 0 khi tạo mới, > 0 khi cập nhật
        public int Id { get; set; } = 0;

        [Required(ErrorMessage = "Vui lòng nhập họ và tên người được khám")]
        [StringLength(150, ErrorMessage = "Tên không được vượt quá 150 ký tự")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập ngày sinh")]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn giới tính")]
        public string Gender { get; set; } = string.Empty; // "Nam" | "Nữ" | "Khác"

        [StringLength(1000, ErrorMessage = "Tiền sử bệnh không được vượt quá 1000 ký tự")]
        public string? MedicalHistory { get; set; }
    }

    /// <summary>
    /// DTO trả về thông tin một hồ sơ bệnh nhân — dùng để hiển thị UI và
    /// populate dropdown chọn hồ sơ khi đặt lịch khám.
    /// </summary>
    public class PatientDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }

        /// <summary>Tuổi tính từ DateOfBirth đến thời điểm hiện tại.</summary>
        public int Age => (int)((DateTime.Today - DateOfBirth).TotalDays / 365.25);

        public string Gender { get; set; } = string.Empty;
        public string RecordCode { get; set; } = string.Empty;
        public string? MedicalHistory { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Label hiển thị trên dropdown đặt lịch:
        /// "Nguyễn Văn An (Nam, 34 tuổi) — BN-2026-0001"
        /// </summary>
        public string DisplayLabel =>
            $"{FullName} ({Gender}, {Age} tuổi) — {RecordCode}";
    }

    /// <summary>Response bọc kết quả CRUD hồ sơ bệnh nhân.</summary>
    public class PatientResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public PatientDto? Data { get; set; }
    }
}