using System;

namespace TMH.Shared.Models
{
    /// <summary>
    /// Đánh giá của bệnh nhân sau khi hoàn thành ca khám.
    /// Mỗi lịch khám (AppointmentId) chỉ được đánh giá 1 lần.
    /// </summary>
    public class Review
    {
        public int Id { get; set; }

        public int AppointmentId { get; set; }
        public Appointment Appointment { get; set; } = null!;

        public int PatientId { get; set; }
        public Patient Patient { get; set; } = null!;

        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; } = null!;

        /// <summary>1–5 sao</summary>
        public int Rating { get; set; }

        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
