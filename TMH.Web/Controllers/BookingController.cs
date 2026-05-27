using Microsoft.AspNetCore.Mvc;
using TMH.Shared.DTOs;
using TMH.Web.Services;

namespace TMH.Web.Controllers
{
    public class BookingController : Controller
    {
        private readonly ApiService _api;

        public BookingController(ApiService api)
        {
            _api = api;
        }

        // GET /Booking/GetDoctorsJson — public, dùng cho hero carousel
        [HttpGet]
        public async Task<IActionResult> GetDoctorsJson()
        {
            var raw = await _api.GetRawJsonAsync("api/appointment/available-doctors");
            return Content(raw ?? "[]", "application/json");
        }

        // GET /Booking/Index — trang chọn bác sĩ + khung giờ
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "Account", new { returnUrl = "/Booking/Index" });

            // Gọi song song để nhanh hơn
            var doctorsTask = _api.GetAvailableDoctorsAsync();
            var patientsTask = _api.GetMyPatientsAsync();
            await Task.WhenAll(doctorsTask, patientsTask);

            ViewBag.Patients = patientsTask.Result ?? new List<PatientDto>();
            return View(doctorsTask.Result ?? new List<DoctorScheduleDto>());
        }

        // POST /Booking/Book
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(int patientId, int scheduleId, string? note)
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "Account");

            var result = await _api.BookAppointmentAsync(new BookAppointmentDto
            {
                PatientId = patientId,
                ScheduleId = scheduleId,
                Note = note
            });

            if (result == null) { TempData["ErrorMessage"] = "Không kết nối được đến máy chủ."; return RedirectToAction("Index"); }
            if (!result.Success) { TempData["ErrorMessage"] = result.Message; return RedirectToAction("Index"); }

            // ĐÃ SỬA: Thay đổi câu thông báo nổi bật hơn khi đặt lịch thành công
            TempData["SuccessMessage"] = "🎉 Đặt lịch khám thành công! Bạn có thể theo dõi trạng thái lịch tại đây.";
            return RedirectToAction("Index", "Patient");
        }

        // POST /Booking/BookAndPay (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookAndPay([FromBody] BookAppointmentDto dto)
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập lại." });
            if (dto.PatientId <= 0)
                return Json(new { success = false, message = "Vui lòng chọn hồ sơ bệnh nhân." });
            if (dto.ScheduleId <= 0)
                return Json(new { success = false, message = "Vui lòng chọn khung giờ." });

            var result = await _api.BookAppointmentAsync(dto);
            if (result == null) return Json(new { success = false, message = "Không kết nối được đến máy chủ." });
            if (!result.Success) return Json(new { success = false, message = result.Message });

            return Json(new { success = true, appointmentId = result.Data?.Id ?? 0, message = result.Message });
        }

        // GET /Booking/MyAppointments
        [HttpGet]
        public async Task<IActionResult> MyAppointments()
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "Account");

            var list = await _api.GetMyAppointmentsAsync();
            return View("MyAppoitments", list ?? new List<AppointmentDetailDto>());
        }

        // POST /Booking/Cancel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return RedirectToAction("Login", "Account");

            var result = await _api.CancelAppointmentAsync(id);
            TempData[result?.Success == true ? "SuccessMessage" : "ErrorMessage"]
                = result?.Message ?? "Có lỗi xảy ra.";
            return RedirectToAction("MyAppointments");
        }

        // POST /Booking/CancelAjax/{id} (AJAX) — huỷ lịch kèm lý do, trả JSON
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelAjax(int id, [FromBody] CancelReasonDto dto)
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập lại." });

            var result = await _api.CancelAppointmentAsync(id);
            if (result == null) return Json(new { success = false, message = "Không kết nối được đến máy chủ." });
            if (!result.Success) return Json(new { success = false, message = result.Message });
            return Json(new { success = true, message = result.Message });
        }

        // GET /Booking/GetSlotsByDate?doctorId=1&date=2026-04-05 (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetSlotsByDate(int doctorId, DateTime date)
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return Json(new { success = false, message = "Chưa đăng nhập." });

            var doctors = await _api.GetAvailableDoctorsAsync(date);
            var doc = doctors?.FirstOrDefault(d => d.DoctorId == doctorId);
            if (doc == null)
                return Json(new { success = true, slots = new List<object>() });

            return Json(new { success = true, slots = doc.AvailableSlots });
        }

        // POST /Booking/CreatePatient (AJAX) — tạo hồ sơ mới ngay trên trang đặt lịch
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePatient([FromBody] PatientUpsertDto dto)
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập lại." });
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Thông tin hồ sơ không hợp lệ." });

            var result = await _api.CreatePatientAsync(dto);
            if (result == null) return Json(new { success = false, message = "Không kết nối được đến máy chủ." });
            if (!result.Success) return Json(new { success = false, message = result.Message });
            return Json(new { success = true, patient = result.Data });
        }

        // POST /Booking/BookPending (AJAX) — đặt lịch chờ phân công (không chọn slot)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookPending([FromBody] BookAppointmentDto dto)
        {
            if (HttpContext.Session.GetString("JwtToken") == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập lại." });
            if (dto.PatientId <= 0)
                return Json(new { success = false, message = "Vui lòng chọn hồ sơ bệnh nhân." });

            // Đảm bảo không có ScheduleId (luồng ChoPhanCong)
            dto.ScheduleId = null;
            dto.PaymentMethod = dto.PaymentMethod ?? "cash";

            var result = await _api.BookAppointmentAsync(dto);
            if (result == null) return Json(new { success = false, message = "Không kết nối được đến máy chủ." });
            if (!result.Success) return Json(new { success = false, message = result.Message });

            // ĐÃ SỬA: Đặt TempData trước khi trả JSON. Khi Javascript gọi window.location.href, TempData này sẽ được đọc và hiện lên.
            TempData["SuccessMessage"] = "📋 Đã gửi yêu cầu đặt lịch! Lễ tân sẽ sớm liên hệ để xếp lịch khám cho bạn.";

            return Json(new
            {
                success = true,
                message = result.Message,
                data = new { bookingCode = result.Data?.BookingCode ?? "" }
            });
        }
    }

    // DTO nhận lý do huỷ từ AJAX
    public class CancelReasonDto
    {
        public string? CancellationReason { get; set; }
    }
}