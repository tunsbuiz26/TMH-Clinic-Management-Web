using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMH.Shared.Models;

namespace TMH.Shared.DTOs
{
    /// <summary>
    /// DTO bệnh nhân gửi lên khi đặt lịch
    /// </summary>
    public class BookAppointmentDto
    {
        [Required(ErrorMessage = "Vui lòng chọn hồ sơ bệnh nhân")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn bác sĩ")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn khung giờ")]
        public int ScheduleId { get; set; }

        public string? Note { get; set; }
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
    /// Chi tiết một lịch khám — dùng để hiển thị trên UI
    /// </summary>
    public class AppointmentDetailDto
    {
        public int Id { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public DateTime WorkDate { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string? Diagnosis { get; set; }
        public DateTime BookedAt { get; set; }
    }

    /// <summary>
    /// DTO cho lễ tân/bác sĩ cập nhật trạng thái lịch khám
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
        public int NewScheduleId { get; set; }   // WorkSchedule mới

        public string? Note { get; set; }        // Lý do đổi lịch (tuỳ chọn)
    }

    /// <summary>
    /// DTO trả về danh sách bác sĩ + lịch làm việc còn trống
    /// — dùng để render dropdown chọn bác sĩ và khung giờ trên form đặt lịch
    /// </summary>
    public class DoctorScheduleDto
    {
        public int DoctorId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string? Degree { get; set; }
        public List<ScheduleSlotDto> AvailableSlots { get; set; } = new();
    }

    public class ScheduleSlotDto
    {
        public int ScheduleId { get; set; }
        public DateTime WorkDate { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public int MaxPatients { get; set; }
        public int CurrentPatients { get; set; }
        public int RemainingSlots { get; set; }   // MaxPatients - CurrentPatients
    }
}
