using System.ComponentModel.DataAnnotations;
using TMH.Shared.Enums;

namespace TMH.Shared.DTOs
{
    // =========================================================
    // DTOs (Data Transfer Objects) là những class trung gian.
    // Chúng tách biệt dữ liệu truyền qua API với entity DB,
    // giúp tránh over-posting và kiểm soát schema API độc lập.
    // =========================================================

    /// <summary>
    /// Dữ liệu bệnh nhân gửi lên khi đăng ký tài khoản.
    /// Data Annotations đảm nhận validate phía server.
    /// </summary>
    public class RegisterDto
    {
        // --- Bước 1: Thông tin cá nhân ---
        [Required(ErrorMessage = "Vui lòng nhập họ và tên đệm")]
        [StringLength(100)]
        public string HoTenDem { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên")]
        [StringLength(50)]
        public string Ten { get; set; } = string.Empty;

        public DateTime? NgaySinh { get; set; }
        public string? GioiTinh { get; set; }
        public string? NhomMau { get; set; }

        // --- Bước 2: Liên hệ & định danh ---
        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại không đúng định dạng (VD: 0901234567)")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [RegularExpression(@"^\d{12}$", ErrorMessage = "Số CCCD phải đủ 12 chữ số")]
        public string? SoCCCD { get; set; }

        public string? DiaChi { get; set; }

        // --- Bước 3: Tài khoản ---
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        [RegularExpression(@"^[a-z0-9]{4,20}$",
            ErrorMessage = "Tên đăng nhập từ 4–20 ký tự, chỉ chữ thường và số")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [MinLength(8, ErrorMessage = "Mật khẩu tối thiểu 8 ký tự")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string ConfirmPassword { get; set; } = string.Empty;

        // Mặc định đăng ký ngoài trang chủ luôn là Patient.
        // Admin có thể tạo tài khoản Doctor/Staff qua giao diện riêng.
        public UserRole Role { get; set; } = UserRole.Patient;
    }

    /// <summary>
    /// Dữ liệu người dùng gửi lên khi đăng nhập.
    /// </summary>
    public class LoginDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email")]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; } = false;
    }

    /// <summary>
    /// Phản hồi của API sau khi đăng nhập thành công.
    /// Web App nhận object này để lưu JWT và thông tin user vào session/cookie.
    /// </summary>
    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        // JWT Token người dùng sẽ đính kèm vào header "Authorization: Bearer {token}"
        public string? Token { get; set; }

        // Thời điểm token hết hạn (Unix timestamp) để Web App tự refresh
        public DateTime? TokenExpiry { get; set; }

        // Thông tin cơ bản của user để Web App hiển thị (tên, role, avatar...)
        public UserInfoDto? User { get; set; }
    }

    /// <summary>
    /// Thông tin tối giản của user trả về trong token response.
    /// Không bao giờ trả về PasswordHash hay thông tin nhạy cảm.
    /// </summary>
    public class UserInfoDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;      // "Admin" | "Doctor" | "Staff" | "Patient"
        public string RoleDisplay { get; set; } = string.Empty; // "Quản trị viên" | "Bác sĩ" | ...
    }
}
