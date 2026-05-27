using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TMH.Shared.DTOs;
using TMH.Web.Services;

// Bring ForgotPasswordDto / ResetPasswordDto into scope (defined in TMH.Shared.DTOs)

namespace TMH.Web.Controllers
{
    /// <summary>
    /// AccountController của Web App xử lý các trang Đăng ký / Đăng nhập.
    ///
    /// Khác với API Controller, Web Controller:
    ///   - Trả về View (HTML) thay vì JSON
    ///   - Lưu JWT token vào Session sau khi đăng nhập
    ///   - Redirect đến đúng Dashboard theo Role
    ///   - Dùng TempData để truyền thông báo lỗi/thành công qua redirect
    /// </summary>
    public class AccountController : Controller
    {
        private readonly ApiService _api;

        public AccountController(ApiService api)
        {
            _api = api;
        }

        // =====================================================================
        // GET /Account/Login  — Hiển thị trang đăng nhập
        // =====================================================================
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Nếu đã đăng nhập rồi thì không cho vào lại trang login
            if (HttpContext.Session.GetString("JwtToken") != null)
                return RedirectByRole();

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // =====================================================================
        // POST /Account/Login  — Xử lý form đăng nhập
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]  // Chống CSRF attack — form phải có @Html.AntiForgeryToken()
        public async Task<IActionResult> Login(LoginDto dto, string? returnUrl = null)
        {
            // Validate phía client (ModelState dùng Data Annotations trong LoginDto)
            if (!ModelState.IsValid)
                return View(dto);

            var result = await _api.LoginAsync(dto);

            // API không phản hồi (server tắt, mạng lỗi...)
            if (result == null)
            {
                ModelState.AddModelError("", "Không kết nối được đến máy chủ. Vui lòng thử lại sau.");
                return View(dto);
            }

            if (!result.Success)
            {
                ModelState.AddModelError("", result.Message);
                return View(dto);
            }

            // --- Đăng nhập thành công: lưu thông tin vào Session ---
            // Session được lưu phía server (không phải cookie plain text)
            HttpContext.Session.SetString("JwtToken",  result.Token!);
            HttpContext.Session.SetString("UserInfo",  JsonSerializer.Serialize(result.User));
            HttpContext.Session.SetString("UserRole",  result.User!.Role);
            HttpContext.Session.SetString("UserName",  result.User.FullName);
            HttpContext.Session.SetString("UserId",    result.User.Id.ToString());
            HttpContext.Session.SetString("TokenExpiry",
                result.TokenExpiry?.ToString("o") ?? "");

            TempData["SuccessMessage"] = result.Message;

            // Nếu có returnUrl hợp lệ (user bị chuyển hướng đến login do chưa xác thực),
            // quay lại trang đó sau khi đăng nhập. Kiểm tra IsLocalUrl để tránh open redirect.
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectByRole();
        }

        // =====================================================================
        // GET /Account/Register  — Hiển thị trang đăng ký
        // =====================================================================
        [HttpGet]
        public IActionResult Register()
        {
            if (HttpContext.Session.GetString("JwtToken") != null)
                return RedirectByRole();

            return View();
        }

        // =====================================================================
        // POST /Account/Register  — Xử lý form đăng ký
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var result = await _api.RegisterAsync(dto);

            if (result == null)
            {
                ModelState.AddModelError("", "Không kết nối được đến máy chủ. Vui lòng thử lại sau.");
                return View(dto);
            }

            if (!result.Success)
            {
                // API trả về lỗi cụ thể (trùng username, email...) — hiển thị thẳng cho user
                ModelState.AddModelError("", result.Message);
                return View(dto);
            }

            // --- Đăng ký thành công: tự động đăng nhập luôn ---
            HttpContext.Session.SetString("JwtToken",  result.Token!);
            HttpContext.Session.SetString("UserInfo",  JsonSerializer.Serialize(result.User));
            HttpContext.Session.SetString("UserRole",  result.User!.Role);
            HttpContext.Session.SetString("UserName",  result.User.FullName);
            HttpContext.Session.SetString("UserId",    result.User.Id.ToString());
            HttpContext.Session.SetString("TokenExpiry",
                result.TokenExpiry?.ToString("o") ?? "");

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("Index", "Patient");  // Bệnh nhân vào trang đặt lịch
        }

        // =====================================================================
        // GET /Account/ForgotPassword
        // =====================================================================
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            if (HttpContext.Session.GetString("JwtToken") != null)
                return RedirectByRole();
            return View();
        }

        // POST /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var callbackBase = $"{Request.Scheme}://{Request.Host}";
            dto.CallbackBaseUrl = callbackBase;

            var result = await _api.ForgotPasswordAsync(dto);
            if (result == null)
            {
                ModelState.AddModelError("", "Không kết nối được đến máy chủ. Vui lòng thử lại sau.");
                return View(dto);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("ForgotPasswordConfirm");
        }

        // GET /Account/ForgotPasswordConfirm — trang thông báo kiểm tra email
        [HttpGet]
        public IActionResult ForgotPasswordConfirm() => View();

        // GET /Account/ResetPassword?token=xxx
        [HttpGet]
        public IActionResult ResetPassword(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["ErrorMessage"] = "Liên kết không hợp lệ.";
                return RedirectToAction("Login");
            }
            return View(new ResetPasswordDto { Token = token });
        }

        // POST /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var result = await _api.ResetPasswordAsync(dto);
            if (result == null)
            {
                ModelState.AddModelError("", "Không kết nối được đến máy chủ. Vui lòng thử lại sau.");
                return View(dto);
            }
            if (!result.Success)
            {
                ModelState.AddModelError("", result.Message);
                return View(dto);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("Login");
        }

        // =====================================================================
        // GET /Account/Logout  — Xoá session, về trang chủ
        // =====================================================================
        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["SuccessMessage"] = "Đăng xuất thành công. Hẹn gặp lại!";
            return RedirectToAction("Index", "Home");
        }

        // =====================================================================
        // GET /Account/AccessDenied  — Hiển thị khi không đủ quyền (403)
        // =====================================================================
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // =====================================================================
        // Helper: Chuyển hướng đến Dashboard theo Role
        // Admin → /Admin/Dashboard
        // Doctor → /Doctor/Dashboard
        // Staff  → /Staff/Dashboard
        // Patient→ /Patient/Index
        // =====================================================================
        private IActionResult RedirectByRole()
        {
            var role = HttpContext.Session.GetString("UserRole");
            return role switch
            {
                "Admin"   => RedirectToAction("Dashboard", "Admin"),
                "Doctor"  => RedirectToAction("Dashboard", "Doctor"),
                "Staff"   => RedirectToAction("Dashboard", "Staff"),
                "Patient" => RedirectToAction("Index",     "Patient"),
                _         => RedirectToAction("Index",     "Home")
            };
        }
    }
}
