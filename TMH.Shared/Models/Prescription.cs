namespace TMH.Shared.Models
{
    /// <summary>
    /// Đơn thuốc — quan hệ 1-1 với Appointment.
    /// Mỗi lần khám hoàn thành có tối đa một đơn thuốc.
    /// </summary>
    public class Prescription
    {
        public int Id { get; set; }

        /// <summary>FK → Appointments.Id (UNIQUE — 1 lịch = 1 đơn thuốc)</summary>
        public int AppointmentId { get; set; }
        public Appointment Appointment { get; set; } = null!;

        /// <summary>Bác sĩ kê đơn — lưu lại ngay cả khi bác sĩ bị xoá</summary>
        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; } = null!;

        /// <summary>Ghi chú tổng của đơn (VD: "Uống sau ăn, tái khám sau 7 ngày")</summary>
        public string? Notes { get; set; }

        /// <summary>Thời gian kê đơn</summary>
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

        // Navigation — danh sách chi tiết thuốc
        public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
    }

    /// <summary>
    /// Chi tiết từng loại thuốc trong đơn.
    /// </summary>
    public class PrescriptionItem
    {
        public int Id { get; set; }

        /// <summary>FK → Prescriptions.Id</summary>
        public int PrescriptionId { get; set; }
        public Prescription Prescription { get; set; } = null!;

        /// <summary>Tên thuốc (VD: "Amoxicillin 500mg")</summary>
        public string MedicineName { get; set; } = string.Empty;

        /// <summary>Liều lượng mỗi lần dùng (VD: "1 viên", "5ml")</summary>
        public string Dosage { get; set; } = string.Empty;

        /// <summary>Tần suất dùng (VD: "3 lần/ngày", "Sáng - Trưa - Tối")</summary>
        public string Frequency { get; set; } = string.Empty;

        /// <summary>Số ngày dùng thuốc (VD: 7)</summary>
        public int DurationDays { get; set; }

        /// <summary>Tổng số lượng xuất (VD: 21 viên)</summary>
        public int Quantity { get; set; }

        /// <summary>Hướng dẫn cách dùng (VD: "Uống sau ăn no", "Bôi vùng da tổn thương")</summary>
        public string? Instructions { get; set; }
    }
}
