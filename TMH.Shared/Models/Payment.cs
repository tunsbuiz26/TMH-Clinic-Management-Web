using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMH.Shared.Models
{
    /// <summary>
    /// Lưu thông tin giao dịch thanh toán cho từng lịch khám.
    /// Quan hệ: APPOINTMENT 1-1 PAYMENT (một lịch khám có tối đa một giao dịch).
    /// 
    /// Luồng trạng thái:
    ///   Pending → Success  (thanh toán thành công qua IPN callback)
    ///   Pending → Failed   (thanh toán thất bại hoặc timeout 15 phút)
    ///   Success → Refunded (hoàn tiền khi bệnh nhân hủy lịch đã thanh toán)
    /// </summary>
    public class Payment
    {
        public int Id { get; set; }

        // --- Liên kết lịch khám ---
        public int AppointmentId { get; set; }
        public Appointment Appointment { get; set; } = null!;

        // --- Thông tin giao dịch ---

        /// <summary>
        /// Số tiền thanh toán, đơn vị VND (ví dụ: 200000 = 200.000 đồng).
        /// VNPay yêu cầu nhân 100 khi gọi API (200000 * 100 = 20000000).
        /// </summary>
        public long Amount { get; set; }

        /// <summary>
        /// Phương thức thanh toán: vnpay | cash | transfer
        /// </summary>
        public string Method { get; set; } = "vnpay";

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        // --- Dữ liệu từ VNPay ---

        /// <summary>
        /// Mã đơn hàng gửi sang VNPay (vnp_TxnRef).
        /// Sinh theo quy tắc: "TMH-{AppointmentId}-{Timestamp}" để đảm bảo unique.
        /// </summary>
        public string? OrderRef { get; set; }

        /// <summary>
        /// Mã giao dịch VNPay trả về (vnp_TransactionNo) sau khi thanh toán thành công.
        /// Lưu để đối soát và hoàn tiền nếu cần.
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// Mã ngân hàng bệnh nhân dùng để thanh toán (vnp_BankCode).
        /// Ví dụ: "VCB", "TCB", "VNPAYQR".
        /// </summary>
        public string? BankCode { get; set; }

        /// <summary>
        /// Mã kết quả VNPay trả về (vnp_ResponseCode).
        /// "00" = thành công; các mã khác = lỗi cụ thể.
        /// Lưu lại để debug khi giao dịch thất bại.
        /// </summary>
        public string? VnpayResponseCode { get; set; }

        /// <summary>
        /// Toàn bộ query string VNPay gửi về qua IPN — lưu để audit và debug.
        /// Không dùng cho logic nghiệp vụ, chỉ dùng để tra cứu khi có sự cố.
        /// </summary>
        public string? RawIpnData { get; set; }

        // --- Thời gian ---
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Thời điểm IPN callback xác nhận thành công.
        /// Null nếu chưa thanh toán hoặc thất bại.
        /// </summary>
        public DateTime? PaidAt { get; set; }

        /// <summary>
        /// Thời điểm hết hạn thanh toán — 15 phút sau khi tạo đơn.
        /// Sau thời điểm này, nếu chưa có IPN success thì slot sẽ bị tự động giải phóng.
        /// </summary>
        public DateTime ExpiresAt { get; set; }
    }

    public enum PaymentStatus
    {
        Pending = 1,   // Đang chờ thanh toán
        Success = 2,   // Đã thanh toán thành công (IPN xác nhận)
        Failed = 3,   // Thanh toán thất bại hoặc bị hủy
        Refunded = 4    // Đã hoàn tiền
    }
}
