using Microsoft.AspNetCore.Mvc;
using TMH.Shared.DTOs;
using TMH.Web.Services;

namespace TMH.Web.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApiService _api;

        public PaymentController(ApiService api)
        {
            _api = api;
        }

        // POST /Payment/Pay — bệnh nhân bấm nút "Thanh toán qua VNPay"
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int appointmentId, double amount)
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "Account");

            var dto = new VnPaymentRequestDto
            {
                AppointmentId = appointmentId,
                Amount = amount,
                OrderInfo = $"Thanh toan lich kham #{appointmentId}"
            };

            var paymentUrl = await _api.CreateVnPayUrlAsync(dto);

            if (string.IsNullOrEmpty(paymentUrl))
            {
                TempData["ErrorMessage"] = "Không thể tạo link thanh toán. Vui lòng thử lại.";
                return RedirectToAction("MyAppointments", "Booking");
            }

            // Redirect thẳng sang trang VNPay
            return Redirect(paymentUrl);
        }

        // GET /Payment/PaymentReturn — VNPay redirect về đây sau khi thanh toán
        [HttpGet]
        public async Task<IActionResult> PaymentReturn()
        {
            // Lấy toàn bộ query string VNPay gửi về rồi forward sang API xử lý
            var queryString = Request.QueryString.Value ?? "";
            var result = await _api.GetPaymentResultAsync(queryString);

            if (result == null)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi xử lý kết quả thanh toán.";
                return RedirectToAction("MyAppointments", "Booking");
            }

            // Truyền kết quả sang View
            return View(result);
        }
    }
}