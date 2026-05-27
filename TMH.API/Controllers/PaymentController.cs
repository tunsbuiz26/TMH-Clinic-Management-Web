using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TMH.API.Data;
using TMH.API.Services;
using TMH.Shared.DTOs;
using TMH.Shared.Models;

namespace TMH.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly VnPayService _vnPayService;
        private readonly AppDbContext _db;

        public PaymentController(VnPayService vnPayService, AppDbContext db)
        {
            _vnPayService = vnPayService;
            _db           = db;
        }

        // POST /api/payment/create-payment-url
        [HttpPost("create-payment-url")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> CreatePaymentUrl([FromBody] VnPaymentRequestDto dto)
        {
            try
            {
                var paymentUrl = await _vnPayService.CreatePaymentUrl(HttpContext, dto);
                return Ok(new { url = paymentUrl });
            }
            catch (Exception ex)
            {
                var message = ex.InnerException == null
                    ? ex.Message
                    : $"{ex.Message} | {ex.InnerException.Message}";

                return BadRequest(new { message });
            }
        }

        // GET /api/payment/payment-return
        [HttpGet("payment-return")]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentReturn()
        {
            var response = await _vnPayService.PaymentExecute(Request.Query);
            return Ok(response);
        }

        // POST /api/payment/ipn
        [HttpGet("ipn")]
        [AllowAnonymous]
        public async Task<IActionResult> Ipn()
        {
            var response = await _vnPayService.PaymentExecute(Request.Query);

            if (response.Success)
            {
                return Ok(new { RspCode = "00", Message = "Confirm Success" });
            }

            return Ok(new { RspCode = "97", Message = "Invalid signature" });
        }


        // POST /api/payment/mark-cash-paid — lễ tân thu tiền mặt tại quầy sau khi khám
        [HttpPost("mark-cash-paid")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> MarkCashPaid([FromBody] int appointmentId)
        {
            // 1. Tìm thông tin lịch khám (cần lấy thêm thông tin Patient để gửi thông báo)
            var appointment = await _db.Appointments
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
                return BadRequest(new { message = "Không tìm thấy lịch khám này." });

            // 2. Tính toán số tiền dựa trên loại hình khám (Dùng cột IsReturn)
            // SỬA LỖI Ở ĐÂY: Dùng kiểu long thay vì decimal để khớp với Model Payment
            long originalPrice = 200000;
            long finalAmount = appointment.IsReturn ? (long)(originalPrice * 0.9m) : originalPrice;

            // 3. Xử lý bản ghi thanh toán (Payment)
            // Tìm xem đã có bản ghi thanh toán nào cho lịch này chưa (để tránh tạo trùng nếu bấm 2 lần)
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.AppointmentId == appointmentId);

            if (payment != null)
            {
                // Nếu đã thanh toán thành công rồi thì báo lỗi
                if (payment.Status == PaymentStatus.Success)
                    return BadRequest(new { message = "Lịch khám này đã hoàn tất thanh toán trước đó." });

                // Cập nhật lại bản ghi cũ sang Tiền mặt và Thành công
                payment.Method = "cash";
                payment.Amount = finalAmount;
                payment.Status = PaymentStatus.Success;
                payment.PaidAt = DateTime.UtcNow;
            }
            else
            {
                // Nếu chưa có (luồng khám xong mới thu tiền), tạo mới hoàn toàn
                payment = new Payment
                {
                    AppointmentId = appointmentId,
                    Amount = finalAmount, // Đã hết lỗi gán giá trị
                    Method = "cash",
                    Status = PaymentStatus.Success,
                    PaidAt = DateTime.UtcNow
                };
                _db.Payments.Add(payment);
            }

            // 4. Cập nhật trạng thái lịch khám
            // Vì đã khám xong và thu tiền, ta chuyển sang trạng thái HoanThanh
            appointment.Status = AppointmentStatus.HoanThanh;

            // 5. Thêm thông báo cho bệnh nhân
            if (appointment.Patient != null)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = appointment.Patient.UserId,
                    AppointmentId = appointmentId,
                    Title = "Thanh toán thành công",
                    Content = $"Lịch khám #{appointment.BookingCode} đã được xác nhận thanh toán tại quầy với số tiền {finalAmount:N0} VNĐ.",
                    Type = NotificationType.ThanhToan,
                    SentAt = DateTime.UtcNow,
                    IsRead = false
                });
            }

            try
            {
                await _db.SaveChangesAsync();
                return Ok(new
                {
                    success = true,
                    message = "Xác nhận thanh toán tiền mặt thành công.",
                    amount = finalAmount
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lưu dữ liệu: " + ex.Message });
            }
        }
    }
}
