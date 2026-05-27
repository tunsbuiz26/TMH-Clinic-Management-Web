using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMH.Shared.Models
{
    public class Appointment
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public Patient Patient { get; set; } = null!;
        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; } = null!;
        public int ScheduleId { get; set; }
        public WorkSchedule Schedule { get; set; } = null!;
        public string BookingCode { get; set; } = string.Empty;
        public DateTime BookedAt { get; set; } = DateTime.UtcNow;
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
    }

    public enum AppointmentStatus
    {
        ChoXacNhan = 1,
        DaXacNhan = 2,
        DaDen = 3,
        DangKham = 4,
        HoanThanh = 5,
        DaHuy = 6,
        VangMat = 7
    }
}
