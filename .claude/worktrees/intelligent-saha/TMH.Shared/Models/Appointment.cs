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
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
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
