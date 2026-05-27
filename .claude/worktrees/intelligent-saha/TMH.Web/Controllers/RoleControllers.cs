using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TMH.Shared.DTOs;
using TMH.Web.Services;

namespace TMH.Web.Controllers
{
    // ================================================================
    // Các controller này bảo vệ bằng cách kiểm tra Session trực tiếp.
    // Vì Web App không dùng JWT middleware, chúng ta tự kiểm tra role
    // từ Session thay vì dùng [Authorize(Roles="...")] của ASP.NET Core.
    //
    // Trong thực tế nên tạo một custom AuthorizeAttribute hoặc
    // ActionFilter để tái sử dụng, tránh lặp code ở mỗi action.
    // ================================================================

    // ----------------------------------------------------------------
    // PATIENT CONTROLLER — Bệnh nhân
    // ----------------------------------------------------------------
    public class PatientController : Controller
    {
        private readonly ApiService _api;
        public PatientController(ApiService api) { _api = api; }

        private bool IsPatient() =>
            HttpContext.Session.GetString("UserRole") == "Patient";

        public async Task<IActionResult> Index()
        {
            if (!IsPatient()) return RedirectToAction("AccessDenied", "Account");
            ViewBag.UserName = HttpContext.Session.GetString("UserName");

            var raw = await _api.GetRawJsonAsync("api/appointment/my-appointments");
            // Escape </script> để tránh HTML parser đóng thẻ sớm khi JSON
            // chứa chuỗi đó trong các field Note hoặc Diagnosis
            ViewBag.AppointmentsJson = (raw ?? "[]").Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);
            return View();
        }

        // GET /Patient/GetMyAppointments — AJAX, trả JSON để JS reload sau hủy lịch
        [HttpGet]
        public async Task<IActionResult> GetMyAppointments()
        {
            if (!IsPatient()) return Unauthorized();
            var raw = await _api.GetRawJsonAsync("api/appointment/my-appointments");
            return Content(raw ?? "[]", "application/json");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            if (!IsPatient()) return Unauthorized();
            var raw = await _api.PostRawJsonAsync($"api/appointment/cancel/{id}", new { });
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        // ── Hồ sơ người thân ──────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetMyProfiles()
        {
            if (!IsPatient()) return Unauthorized();
            var raw = await _api.GetRawJsonAsync("api/patient/my-profiles");
            return Content(raw ?? "[]", "application/json");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveProfile([FromBody] System.Text.Json.JsonElement dto)
        {
            if (!IsPatient()) return Unauthorized();
            var id = dto.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
            string raw;
            if (id > 0)
                raw = await _api.PutRawJsonAsync($"api/patient", dto) ?? @"{""success"":false}";
            else
                raw = await _api.PostRawJsonAsync("api/patient", dto) ?? @"{""success"":false}";
            return Content(raw, "application/json");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProfile(int id)
        {
            if (!IsPatient()) return Unauthorized();
            var raw = await _api.DeleteRawJsonAsync($"api/patient/{id}");
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        // ── Thông tin cá nhân ─────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            if (!IsPatient()) return Unauthorized();
            var raw = await _api.GetRawJsonAsync("api/auth/me");
            return Content(raw ?? "{}", "application/json");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile([FromBody] System.Text.Json.JsonElement dto)
        {
            if (!IsPatient()) return Unauthorized();
            var raw = await _api.PutRawJsonAsync("api/auth/me", dto);
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        // Alias cho Profile.cshtml gọi /Patient/UpdateInfo
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateInfo([FromBody] System.Text.Json.JsonElement dto)
        {
            if (!IsPatient()) return Unauthorized();
            var raw = await _api.PutRawJsonAsync("api/auth/me", dto);
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        // Alias cho Profile.cshtml gọi /Patient/CreatePatientProfile
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePatientProfile([FromBody] System.Text.Json.JsonElement dto)
        {
            if (!IsPatient()) return Unauthorized();
            var raw = await _api.PostRawJsonAsync("api/patient", dto) ?? @"{""success"":false}";
            return Content(raw, "application/json");
        }

        // Alias cho Profile.cshtml gọi /Patient/UpdatePatientProfile
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePatientProfile([FromBody] System.Text.Json.JsonElement dto)
        {
            if (!IsPatient()) return Unauthorized();
            var raw = await _api.PutRawJsonAsync("api/patient", dto) ?? @"{""success"":false}";
            return Content(raw, "application/json");
        }

        // Alias cho Profile.cshtml gọi /Patient/DeletePatientProfile
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePatientProfile(int id)
        {
            if (!IsPatient()) return Unauthorized();
            var raw = await _api.DeleteRawJsonAsync($"api/patient/{id}");
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        // ── Thông báo ─────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            if (!IsPatient()) return Unauthorized();
            var raw = await _api.GetRawJsonAsync("api/auth/notifications");
            return Content(raw ?? "[]", "application/json");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotifRead(int id)
        {
            if (!IsPatient()) return Unauthorized();
            var raw = await _api.PutRawJsonAsync($"api/auth/notifications/{id}/read", new { });
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        // Alias cho Notifications.cshtml gọi /Patient/MarkRead
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            if (!IsPatient()) return Unauthorized();
            var raw = await _api.PutRawJsonAsync($"api/auth/notifications/{id}/read", new { });
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        // ── Kết quả khám (appointments đã hoàn thành) ────────────
        [HttpGet]
        public async Task<IActionResult> GetCompletedAppointments()
        {
            if (!IsPatient()) return Unauthorized();
            var raw = await _api.GetRawJsonAsync("api/appointment/my-appointments");
            return Content(raw ?? "[]", "application/json");
        }

        public IActionResult Book()
        {
            if (!IsPatient()) return RedirectToAction("AccessDenied", "Account");
            return View();
        }

        public async Task<IActionResult> Results()
        {
            if (!IsPatient()) return RedirectToAction("AccessDenied", "Account");
            var raw = await _api.GetRawJsonAsync("api/appointment/my-appointments");
            ViewBag.AppointmentsJson = (raw ?? "[]").Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);
            return View();
        }

        public async Task<IActionResult> Profile()
        {
            if (!IsPatient()) return RedirectToAction("AccessDenied", "Account");
            var userRaw = await _api.GetRawJsonAsync("api/auth/me");
            ViewBag.UserInfoJson = (userRaw ?? "{}").Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);
            var profilesRaw = await _api.GetRawJsonAsync("api/patient/my-profiles");
            ViewBag.ProfilesJson = (profilesRaw ?? "[]").Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);
            return View();
        }

        public async Task<IActionResult> Notifications()
        {
            if (!IsPatient()) return RedirectToAction("AccessDenied", "Account");
            var raw = await _api.GetRawJsonAsync("api/auth/notifications");
            ViewBag.NotificationsJson = (raw ?? "[]").Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);
            return View();
        }
    }

    // ----------------------------------------------------------------
    // DOCTOR CONTROLLER — Bác sĩ
    // ----------------------------------------------------------------
    public class DoctorController : Controller
    {
        private readonly ApiService _api;
        public DoctorController(ApiService api) { _api = api; }

        private bool IsDoctor() =>
            HttpContext.Session.GetString("UserRole") == "Doctor";

        // GET /Doctor/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            if (!IsDoctor()) return RedirectToAction("AccessDenied", "Account");
            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            var list = await _api.GetAppointmentsByDateAsync(DateTime.Today);
            ViewBag.TodayDate = DateTime.Today.ToString("dd/MM/yyyy");
            return View(list ?? new List<TMH.Shared.DTOs.AppointmentDetailDto>());
        }

        // GET /Doctor/GetAppointments?date=2026-04-01 — AJAX
        [HttpGet]
        public async Task<IActionResult> GetAppointments(DateTime date)
        {
            if (!IsDoctor()) return Unauthorized();
            var list = await _api.GetAppointmentsByDateAsync(date);
            return Json(list ?? new List<TMH.Shared.DTOs.AppointmentDetailDto>());
        }

        // POST /Doctor/SaveDiagnosis — ghi chuẩn đoán + cập nhật trạng thái
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDiagnosis([FromBody] TMH.Shared.DTOs.UpdateAppointmentStatusDto dto)
        {
            if (!IsDoctor()) return Unauthorized();
            var result = await _api.UpdateAppointmentStatusAsync(dto);
            return Json(result);
        }

        // ── Bài viết ─────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetMyArticles()
        {
            if (!IsDoctor()) return Unauthorized();
            return await ForwardDocJson("api/article/my");
        }

        [HttpGet]
        public async Task<IActionResult> GetArticleDetail(int id)
        {
            if (!IsDoctor()) return Unauthorized();
            return await ForwardDocJson($"api/article/{id}");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateArticle([FromBody] JsonElement dto)
        {
            if (!IsDoctor()) return Unauthorized();
            var raw = await _api.PostRawJsonAsync("api/article", dto);
            return Content(raw ?? @"{""success"":false,""message"":""Loi server""}", "application/json");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateArticle(int id, [FromBody] JsonElement dto)
        {
            if (!IsDoctor()) return Unauthorized();
            var raw = await _api.PutRawJsonAsync($"api/article/{id}", dto);
            return Content(raw ?? @"{""success"":false,""message"":""Loi server""}", "application/json");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteArticle(int id)
        {
            if (!IsDoctor()) return Unauthorized();
            var raw = await _api.DeleteRawJsonAsync($"api/article/{id}");
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UploadArticleImage()
        {
            if (!IsDoctor()) return Unauthorized();
            var file = Request.Form.Files.FirstOrDefault();
            if (file == null) return BadRequest(new { success = false, message = "Không có file." });

            // Lưu file vào wwwroot/uploads/articles/
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
                return BadRequest(new { success = false, message = "Chỉ chấp nhận ảnh." });
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { success = false, message = "Ảnh tối đa 5MB." });

            var env = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "articles");
            Directory.CreateDirectory(uploadsDir);

            var ext      = Path.GetExtension(file.FileName).ToLower();
            var fileName = Guid.NewGuid().ToString("N") + ext;
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            return Json(new { success = true, url = "/uploads/articles/" + fileName });
        }

        private async Task<ContentResult> ForwardDocJson(string endpoint)
        {
            var raw = await _api.GetRawJsonAsync(endpoint);
            return Content(raw ?? "[]", "application/json");
        }
    }

    // ----------------------------------------------------------------
    // STAFF CONTROLLER — Lễ tân / Nhân viên
    // ----------------------------------------------------------------
    public class StaffController : Controller
    {
        private readonly ApiService _api;

        public StaffController(ApiService api) { _api = api; }

        private bool IsStaff() =>
            HttpContext.Session.GetString("UserRole") == "Staff";

        // GET /Staff/Dashboard — trang chính lễ tân
        public async Task<IActionResult> Dashboard()
        {
            if (!IsStaff()) return RedirectToAction("AccessDenied", "Account");
            ViewBag.UserName = HttpContext.Session.GetString("UserName");

            // Load danh sách bác sĩ để populate filter dropdown
            var doctors = await _api.GetAvailableDoctorsAsync();
            ViewBag.Doctors = doctors ?? new List<DoctorScheduleDto>();
            return View();
        }

        // GET /Staff/GetAppointments?date=2026-04-01&doctorId=1
        // AJAX — trả JSON danh sách lịch khám theo ngày
        [HttpGet]
        public async Task<IActionResult> GetAppointments(DateTime date, int? doctorId)
        {
            if (!IsStaff()) return Unauthorized();
            var list = await _api.GetAppointmentsByDateAsync(date, doctorId);
            return Json(list ?? new List<AppointmentDetailDto>());
        }

        // GET /Staff/SearchAppointments?q=... — AJAX tìm kiếm toàn bộ DB
        [HttpGet]
        public async Task<IActionResult> SearchAppointments(string q)
        {
            if (!IsStaff()) return Unauthorized();
            if (string.IsNullOrWhiteSpace(q))
                return Content("[]", "application/json");
            var raw = await _api.GetRawJsonAsync($"api/appointment/search?q={Uri.EscapeDataString(q)}");
            return Content(raw ?? "[]", "application/json");
        }

        // POST /Staff/UpdateStatus — AJAX cập nhật trạng thái
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateAppointmentStatusDto dto)
        {
            if (!IsStaff()) return Unauthorized();
            var result = await _api.UpdateAppointmentStatusAsync(dto);
            return Json(result);
        }

        // GET /Staff/GetSlotsForReschedule?date=2026-04-10&doctorId=1
        // Trả về danh sách slot còn trống theo ngày + bác sĩ — dùng cho modal đổi lịch
        [HttpGet]
        public async Task<IActionResult> GetSlotsForReschedule(string date, int? doctorId)
        {
            if (!IsStaff()) return Unauthorized();
            var url = $"api/appointment/available-doctors?date={date}";
            var raw = await _api.GetRawJsonAsync(url);
            return Content(raw ?? "[]", "application/json");
        }

        // PUT /Staff/Reschedule — AJAX đổi lịch khám
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reschedule([FromBody] RescheduleDto dto)
        {
            if (!IsStaff()) return Unauthorized();
            var raw = await _api.PutRawJsonAsync("api/appointment/reschedule", dto);
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        // POST /Staff/CreateWalkIn — tạo lịch walk-in tại chỗ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWalkIn([FromBody] BookAppointmentDto dto)
        {
            if (!IsStaff()) return Unauthorized();
            var result = await _api.BookAppointmentAsync(dto);
            return Json(result);
        }
    }

    // ----------------------------------------------------------------
    // ADMIN CONTROLLER — Quản trị viên
    // ----------------------------------------------------------------
    public class AdminController : Controller
    {
        private readonly ApiService _api;
        public AdminController(ApiService api) { _api = api; }

        private bool IsAdmin() =>
            HttpContext.Session.GetString("UserRole") == "Admin";

        public IActionResult Dashboard()
        {
            if (!IsAdmin()) return RedirectToAction("AccessDenied", "Account");
            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            return View();
        }

        // AJAX endpoints cho Admin dashboard — dùng raw JSON forwarding tránh lỗi JSON casing
        [HttpGet]
        public async Task<IActionResult> GetStats()
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.GetRawJsonAsync("api/admin/stats");
            return Content(raw ?? "{}", "application/json");
        }

        [HttpGet]
        public async Task<IActionResult> GetWeeklyStats()
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.GetRawJsonAsync("api/admin/stats/weekly");
            return Content(raw ?? "[]", "application/json");
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.GetRawJsonAsync("api/admin/users");
            return Content(raw ?? "[]", "application/json");
        }

        [HttpGet]
        public async Task<IActionResult> GetDoctors()
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.GetRawJsonAsync("api/admin/doctors");
            return Content(raw ?? "[]", "application/json");
        }

        [HttpGet]
        public async Task<IActionResult> GetStatsByDoctor()
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.GetRawJsonAsync("api/admin/stats/by-doctor");
            return Content(raw ?? "[]", "application/json");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserActive(int id)
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.PutRawJsonAsync($"api/admin/users/{id}/toggle-active", new { });
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleDoctorAvailable(int id)
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.PutRawJsonAsync($"api/admin/doctors/{id}/toggle-available", new { });
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        // ── Lịch làm việc ────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetSchedules(int? doctorId, string? from, string? to)
        {
            if (!IsAdmin()) return Unauthorized();
            var url = "api/admin/schedules?";
            if (doctorId.HasValue)           url += $"doctorId={doctorId}&";
            if (!string.IsNullOrEmpty(from)) url += $"from={from}&";
            if (!string.IsNullOrEmpty(to))   url += $"to={to}&";
            var raw = await _api.GetRawJsonAsync(url.TrimEnd('?', '&'));
            return Content(raw ?? "[]", "application/json");
        }

        // ── Thống kê theo tháng ───────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetMonthlyStats()
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.GetRawJsonAsync("api/admin/stats/monthly");
            return Content(raw ?? "[]", "application/json");
        }

        // ── Báo cáo tổng hợp theo khoảng ngày ────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetReportStats(string? from, string? to)
        {
            if (!IsAdmin()) return Unauthorized();
            var url = "api/admin/stats/report?";
            if (!string.IsNullOrEmpty(from)) url += $"from={from}&";
            if (!string.IsNullOrEmpty(to))   url += $"to={to}&";
            var raw = await _api.GetRawJsonAsync(url.TrimEnd('?', '&'));
            return Content(raw ?? "{}", "application/json");
        }

        // ── Tạo tài khoản Staff / Doctor ─────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser([FromBody] System.Text.Json.JsonElement dto)
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.PostRawJsonAsync("api/admin/users", dto);
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        // ── Reset mật khẩu ────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] System.Text.Json.JsonElement dto)
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.PutRawJsonAsync($"api/admin/users/{id}/reset-password", dto);
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        // ── Sửa thông tin bác sĩ ─────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDoctor(int id, [FromBody] System.Text.Json.JsonElement dto)
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.PutRawJsonAsync($"api/admin/doctors/{id}", dto);
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSchedule([FromBody] System.Text.Json.JsonElement dto)
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.PostRawJsonAsync("api/admin/schedules", dto);
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSchedule(int id)
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.DeleteRawJsonAsync($"api/admin/schedules/{id}");
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateScheduleBatch([FromBody] System.Text.Json.JsonElement dto)
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.PostRawJsonAsync("api/admin/schedules/batch", dto);
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        // ── Bài viết (Admin duyệt) ────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetAllArticles()
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.GetRawJsonAsync("api/article/all");
            return Content(raw ?? "[]", "application/json");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleArticleStatus(int id)
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.PutRawJsonAsync($"api/article/{id}/toggle-status", new { });
            return Content(raw ?? @"{""success"":false}", "application/json");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminDeleteArticle(int id)
        {
            if (!IsAdmin()) return Unauthorized();
            var raw = await _api.DeleteRawJsonAsync($"api/article/{id}");
            return Content(raw ?? @"{""success"":false}", "application/json");
        }
    }
}
