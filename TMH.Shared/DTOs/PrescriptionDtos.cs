using System.ComponentModel.DataAnnotations;

namespace TMH.Shared.DTOs
{
    // ══════════════════════════════════════════════════════════════
    // INPUT DTOs — bác sĩ gửi lên khi kê đơn
    // ══════════════════════════════════════════════════════════════

    /// <summary>DTO bác sĩ gửi lên để tạo đơn thuốc mới</summary>
    public class CreatePrescriptionDto
    {
        [Required(ErrorMessage = "Vui lòng chọn lịch khám")]
        public int AppointmentId { get; set; }

        /// <summary>Ghi chú tổng (tuỳ chọn)</summary>
        public string? Notes { get; set; }

        [Required(ErrorMessage = "Đơn thuốc phải có ít nhất 1 loại thuốc")]
        [MinLength(1, ErrorMessage = "Đơn thuốc phải có ít nhất 1 loại thuốc")]
        public List<CreatePrescriptionItemDto> Items { get; set; } = new();
    }

    /// <summary>DTO 1 dòng thuốc trong đơn</summary>
    public class CreatePrescriptionItemDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tên thuốc")]
        [MaxLength(200)]
        public string MedicineName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập liều lượng")]
        [MaxLength(100)]
        public string Dosage { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tần suất dùng thuốc")]
        [MaxLength(100)]
        public string Frequency { get; set; } = string.Empty;

        [Range(1, 365, ErrorMessage = "Số ngày dùng phải từ 1 đến 365")]
        public int DurationDays { get; set; }

        [Range(1, 10000, ErrorMessage = "Số lượng phải từ 1 trở lên")]
        public int Quantity { get; set; }

        [MaxLength(500)]
        public string? Instructions { get; set; }
    }

    /// <summary>DTO cập nhật đơn thuốc (ghi đè toàn bộ items)</summary>
    public class UpdatePrescriptionDto
    {
        public string? Notes { get; set; }

        [Required]
        [MinLength(1)]
        public List<CreatePrescriptionItemDto> Items { get; set; } = new();
    }

    // ══════════════════════════════════════════════════════════════
    // OUTPUT DTOs — trả về cho client
    // ══════════════════════════════════════════════════════════════

    /// <summary>Đơn thuốc đầy đủ trả về cho UI / in phiếu</summary>
    public class PrescriptionDto
    {
        public int Id { get; set; }
        public int AppointmentId { get; set; }
        public string BookingCode { get; set; } = string.Empty;

        // Thông tin bệnh nhân (để in phiếu)
        public string PatientName { get; set; } = string.Empty;
        public string PatientRecordCode { get; set; } = string.Empty;
        public DateTime PatientDateOfBirth { get; set; }
        public string PatientGender { get; set; } = string.Empty;

        // Thông tin bác sĩ (để in phiếu)
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string? DoctorDegree { get; set; }

        // Thông tin lịch khám
        // THAY ĐỔI: DateTime? vì lịch ChoPhanCong chưa có Schedule
        // Frontend hiển thị: workDate?.toLocaleDateString() hoặc "Chưa xác định"
        public DateTime? WorkDate { get; set; }

        public string Diagnosis { get; set; } = string.Empty;

        // Đơn thuốc
        public string? Notes { get; set; }
        public DateTime IssuedAt { get; set; }

        public List<PrescriptionItemDto> Items { get; set; } = new();
    }

    /// <summary>1 dòng thuốc trong đơn</summary>
    public class PrescriptionItemDto
    {
        public int Id { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string Dosage { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public int DurationDays { get; set; }
        public int Quantity { get; set; }
        public string? Instructions { get; set; }
    }

    /// <summary>Response chung cho các thao tác với đơn thuốc</summary>
    public class PrescriptionResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public PrescriptionDto? Data { get; set; }
    }
}
