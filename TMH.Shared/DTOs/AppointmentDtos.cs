using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TMH.Shared.Models;

namespace TMH.Shared.DTOs
{
    /// <summary>
    /// DTO bệnh nhân gửi lên khi đặt lịch.
    /// DoctorId và ScheduleId đều nullable:
    ///   - Không chọn bác sĩ  → hệ thống/lễ tân tự xếp
    ///   - Không chọn slot    → lịch tạo ở trạng thái ChoPhanCong
    /// </summary>
    public class BookAppointmentDto
    {
        [Required(ErrorMessage = "Vui lòng chọn hồ sơ bệnh nhân")]
        public int PatientId { get; set; }

        // THAY ĐỔI: nullable — không bắt buộc chọn bác sĩ
        public int? DoctorId { get; set; }

        // THAY ĐỔI: nullable — không bắt buộc chọn khung giờ
        public int? ScheduleId { get; set; }

        // THÊM MỚI: ngày mong muốn khám (tuỳ chọn, dùng khi không có slot)
        public DateTime? PreferredDate { get; set; }

        public string? Note { get; set; }

        /// <summary>"cash" | "vnpay" — phương thức thanh toán</summary>
        public string? PaymentMethod { get; set; }
    }

    /// <summary>
    /// DTO trả về sau khi đặt lịch thành công
    /// </summary>
    public class AppointmentResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public AppointmentDetailDto? Data { get; set; }
    }

    /// <summary>
    /// Chi tiết một lịch khám — dùng để hiển thị trên UI.
    /// Giữ lại tất cả field gốc + thêm field cho lịch ChoPhanCong.
    /// </summary>
    public class AppointmentDetailDto
    {
        public int Id { get; set; }
        public string BookingCode { get; set; } = string.Empty;

        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;

        // THAY ĐỔI: nullable khi chưa phân công bác sĩ
        public int? DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public string? Specialty { get; set; }

        // THAY ĐỔI: nullable khi chưa có lịch khám
        public DateTime? WorkDate { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }

        // THÊM MỚI: ngày mong muốn khi chưa có slot
        public DateTime? PreferredDate { get; set; }
        public string? PreferredDateDisplay { get; set; }

        public string Status { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string? Diagnosis { get; set; }
        public DateTime BookedAt { get; set; }

        public bool IsReturn { get; set; }
        public string VisitTypeDisplay => IsReturn ? "Tái khám" : "Khám mới";
        public string? CancellationReason { get; set; }
        public bool HasReview { get; set; }

        // --- Thanh toán ---
        /// <summary>"vnpay" | "cash" | null (chưa có payment record)</summary>
        public string? PaymentMethod { get; set; }

        /// <summary>"Pending" | "Success" | "Failed" | "Refunded" | null</summary>
        public string? PaymentStatus { get; set; }

        /// <summary>Số tiền thanh toán (VND)</summary>
        public long? PaymentAmount { get; set; }

        /// <summary>Hạn thanh toán VNPay (null nếu không phải VNPay)</summary>
        public DateTime? PaymentExpiresAt { get; set; }
    }

    /// <summary>DTO bệnh nhân gửi lý do huỷ lịch</summary>
    public class CancelAppointmentDto
    {
        public string? CancellationReason { get; set; }
    }

    /// <summary>
    /// DTO lễ tân dùng để gán bác sĩ + khung giờ cho lịch ChoPhanCong
    /// PUT /api/appointment/assign
    /// </summary>
    public class AssignScheduleDto
    {
        [Required(ErrorMessage = "Vui lòng chỉ định lịch hẹn cần phân công")]
        public int AppointmentId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn khung giờ")]
        public int ScheduleId { get; set; }

        // Ghi chú thêm của lễ tân (tuỳ chọn)
        public string? Note { get; set; }
    }

    /// <summary>
    /// DTO lễ tân/bác sĩ cập nhật trạng thái lịch khám
    /// </summary>
    public class UpdateAppointmentStatusDto
    {
        [Required]
        public int AppointmentId { get; set; }

        [Required]
        public AppointmentStatus NewStatus { get; set; }

        public string? Note { get; set; }
        public string? Diagnosis { get; set; }
    }

    /// <summary>
    /// DTO lễ tân dùng khi đổi lịch khám (đổi bác sĩ hoặc đổi giờ)
    /// </summary>
    public class RescheduleDto
    {
        [Required]
        public int AppointmentId { get; set; }

        [Required]
        public int NewScheduleId { get; set; }

        public string? Note { get; set; }
    }

    /// <summary>
    /// DTO trả về danh sách bác sĩ + lịch làm việc còn trống
    /// </summary>
    public class DoctorScheduleDto
    {
        public int DoctorId { get; set; }
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Luôn là "Tai Mũi Họng" — phòng khám không phân chia khoa riêng.
        /// Giữ lại field này để tương thích với các UI đang dùng.
        /// </summary>
        public string Specialty { get; set; } = "Tai Mũi Họng";

        public string? Degree { get; set; }

        /// <summary>
        /// Mô tả kinh nghiệm và chuyên môn của bác sĩ.
        /// Dùng bởi DoctorRecommendationService để match triệu chứng.
        /// </summary>
        public string? Description { get; set; }

        public List<ScheduleSlotDto> AvailableSlots { get; set; } = new();
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }

    public class ScheduleSlotDto
    {
        public int ScheduleId { get; set; }
        public DateTime WorkDate { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public int MaxPatients { get; set; }
        public int CurrentPatients { get; set; }
        public int RemainingSlots { get; set; }
        public bool IsFull { get; set; }
    }

    /// <summary>
    /// DTO lễ tân lọc danh sách lịch ChoPhanCong
    /// GET /api/appointment/pending-assignment
    /// </summary>
    public class PendingAssignmentFilterDto
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Specialty { get; set; }
        public string? Keyword { get; set; }  // BookingCode hoặc tên bệnh nhân
    }
}
