using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TMH.API.Data;
using TMH.API.Services;
using TMH.Shared.DTOs;

namespace TMH.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly ILogger<AuthController> _logger;
        private readonly AppDbContext _db;

        public AuthController(AuthService authService, ILogger<AuthController> logger, AppDbContext db)
        {
            _authService = authService;
            _logger = logger;
            _db = db;
        }

        // POST /api/auth/register
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var result = await _authService.RegisterAsync(dto);
            if (!result.Success) return Conflict(result);
            _logger.LogInformation("Dang ky thanh cong: {Username}", dto.Username);
            return Ok(result);
        }

        // POST /api/auth/login
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var result = await _authService.LoginAsync(dto);
            if (!result.Success) return Unauthorized(result);
            _logger.LogInformation("Dang nhap thanh cong: {User}", dto.UsernameOrEmail);
            return Ok(result);
        }

        // GET /api/auth/profile
        [HttpGet("profile")]
        [Authorize]
        public IActionResult GetProfile()
        {
            var userId   = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var username = User.FindFirstValue("username");
            var fullname = User.FindFirstValue("fullname");
            var role     = User.FindFirstValue(ClaimTypes.Role);
            return Ok(new { userId, username, fullname, role });
        }

        // GET /api/auth/me
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMe()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound();
            return Ok(new
            {
                user.Id, user.HoTenDem, user.Ten, user.FullName,
                user.Phone, user.Email, user.Username,
                NgaySinh = user.NgaySinh.HasValue ? user.NgaySinh.Value.ToString("yyyy-MM-dd") : null,
                user.GioiTinh, user.NhomMau, user.DiaChi
            });
        }

        // PUT /api/auth/me
        [HttpPut("me")]
        [Authorize]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound();
            if (dto.HoTenDem  != null) user.HoTenDem = dto.HoTenDem;
            if (dto.Ten       != null) user.Ten       = dto.Ten;
            if (dto.GioiTinh  != null) user.GioiTinh  = dto.GioiTinh;
            if (dto.NgaySinh.HasValue) user.NgaySinh  = dto.NgaySinh;
            if (dto.DiaChi    != null) user.DiaChi    = dto.DiaChi;
            if (dto.NhomMau   != null) user.NhomMau   = dto.NhomMau;
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Cap nhat thanh cong." });
        }

        // GET /api/auth/notifications
        [HttpGet("notifications")]
        [Authorize]
        public async Task<IActionResult> GetNotifications()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();
            var notifs = await _db.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.SentAt)
                .Take(20)
                .Select(n => new
                {
                    n.Id, n.Title, n.Content, n.IsRead, n.AppointmentId,
                    Type   = n.Type.ToString(),
                    SentAt = n.SentAt.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();
            return Ok(notifs);
        }

        // PUT /api/auth/notifications/{id}/read
        [HttpPut("notifications/{id:int}/read")]
        [Authorize]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();
            var n = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (n == null) return NotFound();
            n.IsRead = true;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // Endpoints test phan quyen
        [HttpGet("admin-only")]
        [Authorize(Roles = "Admin")]
        public IActionResult AdminOnly()
            => Ok(new { message = "Chao Quan tri vien! Ban co toan quyen he thong." });

        [HttpGet("doctor-staff")]
        [Authorize(Roles = "Admin,Doctor,Staff")]
        public IActionResult DoctorAndStaff()
            => Ok(new { message = "Khu vuc danh cho Bac si va Nhan vien phong kham." });

        [HttpGet("patient-area")]
        [Authorize(Roles = "Patient")]
        public IActionResult PatientArea()
            => Ok(new { message = "Cong benh nhan." });

        [HttpGet("genhash")]
        [AllowAnonymous]
        public IActionResult GenHash()
        {
            string hash = BCrypt.Net.BCrypt.HashPassword("TMH@123456", workFactor: 12);
            return Ok(new { hash });
        }

        // GET /api/auth/test-email?to=abc@gmail.com
        // Chỉ dùng để test, xoá hoặc comment lại sau khi verify xong
        [HttpGet("test-email")]
        [AllowAnonymous]
        public async Task<IActionResult> TestEmail([FromQuery] string to, [FromServices] EmailService emailSvc)
        {
            if (string.IsNullOrWhiteSpace(to))
                return BadRequest(new { success = false, message = "Thiếu tham số ?to=email" });

            await emailSvc.SendBookingConfirmationAsync(
                toEmail    : to,
                toName     : "Bệnh Nhân Test",
                bookingCode: "APT-2026-99999",
                doctorName : "TS.BS. Nguyễn Thanh Vân",
                workDate   : DateTime.Today.AddDays(1).ToString("dd/MM/yyyy"),
                startTime  : "09:00",
                endTime    : "11:00",
                patientName: "Nguyễn Văn Test",
                note       : "Đây là email test từ hệ thống TMH."
            );

            return Ok(new { success = true, message = $"Đã gửi email test đến {to}. Kiểm tra hộp thư (kể cả thư mục Spam)." });
        }
    }

    public class UpdateProfileDto
    {
        public string?   HoTenDem { get; set; }
        public string?   Ten      { get; set; }
        public string?   GioiTinh { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string?   DiaChi   { get; set; }
        public string?   NhomMau  { get; set; }
    }
}
