using TMH.Shared.Enums;

namespace TMH.Shared.Models
{
    /// <summary>
    /// Entity ánh xạ trực tiếp tới bảng Users trong SQL Server.
    /// Không dùng ASP.NET Identity để giữ schema đơn giản, kiểm soát hoàn toàn.
    /// </summary>
    public class User
    {
        public int Id { get; set; }

        // --- Thông tin cá nhân ---
        public string HoTenDem { get; set; } = string.Empty;   // Họ và tên đệm
        public string Ten { get; set; } = string.Empty;         // Tên
        public string FullName => $"{HoTenDem} {Ten}".Trim();  // Computed, không lưu DB

        public DateTime? NgaySinh { get; set; }
        public string? GioiTinh { get; set; }   // "Nam" | "Nữ" | "Khác"
        public string? NhomMau { get; set; }    // "A+" | "B-" | ...
        public string? DiaChi { get; set; }
        public string? SoCCCD { get; set; }     // 12 chữ số

        // --- Tài khoản ---
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        /// <summary>
        /// Mật khẩu được hash bằng BCrypt trước khi lưu.
        /// KHÔNG BAO GIỜ lưu plain text.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        // --- Phân quyền ---
        public UserRole Role { get; set; } = UserRole.Patient;

        // --- Trạng thái ---
        public bool IsActive { get; set; } = true;
        public bool IsEmailVerified { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
    }
}
