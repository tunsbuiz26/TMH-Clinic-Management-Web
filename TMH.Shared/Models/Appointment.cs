using System;
using System.Collections.Generic;

namespace TMH.Shared.Models
{
    public class Appointment
    {
        public int Id { get; set; }

        public int PatientId { get; set; }
        public Patient Patient { get; set; } = null!;

        // THAY ĐỔI: nullable — null khi chưa được phân công bác sĩ
        public int? DoctorId { get; set; }
        public Doctor? Doctor { get; set; }

        // THAY ĐỔI: nullable — null khi chưa có khung giờ (lịch chờ phân công)
        public int? ScheduleId { get; set; }
        public WorkSchedule? Schedule { get; set; }

        // THÊM MỚI: ngày mong muốn khám (khi không chọn slot cụ thể)
        public DateTime? PreferredDate { get; set; }

        public string BookingCode { get; set; } = string.Empty;
        public DateTime BookedAt { get; set; } = DateTime.UtcNow;

        // Mặc định ChoXacNhan; ChoPhanCong được set khi không có ScheduleId
        public AppointmentStatus Status { get; set; } = AppointmentStatus.ChoXacNhan;

        public string? Note { get; set; }
        public string? Diagnosis { get; set; }

        /// <summary>
        /// true = Tái khám (bệnh nhân đã từng khám hoàn thành với bác sĩ này).
        /// Được tự động phát hiện khi đặt lịch, không cần người dùng tự chọn.
        /// </summary>
        public bool IsReturn { get; set; } = false;

        /// <summary>Lý do huỷ lịch (do bệnh nhân chọn khi bấm Huỷ)</summary>
        public string? CancellationReason { get; set; }

        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        // Navigation property — mỗi lịch khám có tối đa một giao dịch thanh toán
        public Payment? Payment { get; set; }

        // null = chưa kê đơn, có giá trị = đã kê đơn
        public Prescription? Prescription { get; set; }
    }

    public enum AppointmentStatus
    {
        // THÊM MỚI: chưa có bác sĩ / khung giờ, lễ tân sẽ xếp
        ChoPhanCong = 0,

        ChoXacNhan  = 1,
        DaXacNhan   = 2,
        DaDen       = 3,
        DangKham    = 4,
        HoanThanh   = 5,
        DaHuy       = 6,
        VangMat     = 7
    }
}
