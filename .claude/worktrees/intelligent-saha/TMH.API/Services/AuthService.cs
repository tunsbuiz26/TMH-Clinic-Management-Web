using Microsoft.EntityFrameworkCore;
using TMH.API.Data;
using TMH.API.Helpers;
using TMH.Shared.DTOs;
using TMH.Shared.Enums;
using TMH.Shared.Models;

namespace TMH.API.Services
{
    /// <summary>
    /// AuthService chứa toàn bộ business logic cho đăng ký và đăng nhập.
    /// Controller chỉ nhận/trả HTTP, còn logic xử lý đặt hết ở đây.
    /// Đây là nguyên tắc "Thin Controller, Fat Service" — dễ test và bảo trì.
    /// </summary>
    public class AuthService
    {
        private readonly AppDbContext _db;
        private readonly JwtHelper    _jwt;

        public AuthService(AppDbContext db, JwtHelper jwt)
        {
            _db  = db;
            _jwt = jwt;
        }

        // =====================================================================
        // ĐĂNG KÝ
        // =====================================================================

        /// <summary>
        /// Xử lý đăng ký tài khoản mới.
        ///
        /// Luồng xử lý:
        ///   1. Kiểm tra username và email đã tồn tại chưa (unique constraint)
        ///   2. Hash mật khẩu bằng BCrypt (work factor 12 — đủ chậm để chống brute-force)
        ///   3. Lưu user mới vào DB
        ///   4. Trả về AuthResponse với token (đăng ký xong là đăng nhập luôn)
        /// </summary>
        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            // --- Bước 1: Kiểm tra trùng lặp ---
            bool usernameTaken = await _db.Users
                .AnyAsync(u => u.Username == dto.Username.ToLower());
            if (usernameTaken)
                return Fail("Tên đăng nhập đã được sử dụng, vui lòng chọn tên khác.");

            bool emailTaken = await _db.Users
                .AnyAsync(u => u.Email == dto.Email.ToLower());
            if (emailTaken)
                return Fail("Email này đã được đăng ký, vui lòng dùng email khác.");

            // --- Bước 2: Hash mật khẩu ---
            // BCrypt.HashPassword tự tạo salt ngẫu nhiên và nhúng vào hash.
            // Không bao giờ tự implement hash — dùng thư viện đã được kiểm chứng.
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12);

            // --- Bước 3: Tạo entity User ---
            var user = new User
            {
                HoTenDem        = dto.HoTenDem.Trim(),
                Ten             = dto.Ten.Trim(),
                NgaySinh        = dto.NgaySinh,
                GioiTinh        = dto.GioiTinh,
                NhomMau         = dto.NhomMau,
                DiaChi          = dto.DiaChi?.Trim(),
                SoCCCD          = dto.SoCCCD?.Trim(),
                Phone           = dto.Phone.Trim(),
                Email           = dto.Email.ToLower().Trim(),
                Username        = dto.Username.ToLower().Trim(),
                PasswordHash    = passwordHash,
                Role            = dto.Role,      // Patient nếu tự đăng ký, hoặc role được Admin chỉ định
                IsActive        = true,
                IsEmailVerified = false,
                CreatedAt       = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // --- Bước 3b: Tự động tạo hồ sơ bệnh nhân (Patient) cho tài khoản mới ---
            // Giúp người dùng có thể đặt lịch ngay mà không cần tạo hồ sơ thủ công
            if (dto.Role == UserRole.Patient)
            {
                int patientCount = await _db.Patients.CountAsync();
                string recordCode = $"BN-{DateTime.Now.Year}-{(patientCount + 1):D4}";
                // Đảm bảo mã không trùng
                while (await _db.Patients.AnyAsync(p => p.RecordCode == recordCode))
                {
                    patientCount++;
                    recordCode = $"BN-{DateTime.Now.Year}-{(patientCount + 1):D4}";
                }

                var patient = new Patient
                {
                    UserId        = user.Id,
                    FullName      = user.FullName,
                    DateOfBirth   = user.NgaySinh ?? DateTime.Today.AddYears(-18),
                    Gender        = user.GioiTinh ?? "Khác",
                    RecordCode    = recordCode,
                    MedicalHistory= null,
                    CreatedAt     = DateTime.UtcNow
                };
                _db.Patients.Add(patient);
                await _db.SaveChangesAsync();
            }

            // --- Bước 4: Cấp token ngay sau khi đăng ký ---
            var (token, expiry) = _jwt.GenerateToken(user);

            return new AuthResponseDto
            {
                Success      = true,
                Message      = $"Đăng ký thành công! Chào mừng {user.Ten} đến với Phòng Khám TMH.",
                Token        = token,
                TokenExpiry  = expiry,
                User         = MapToUserInfo(user)
            };
        }

        // =====================================================================
        // ĐĂNG NHẬP
        // =====================================================================

        /// <summary>
        /// Xử lý đăng nhập.
        ///
        /// Luồng xử lý:
        ///   1. Tìm user theo username hoặc email (cho phép cả hai)
        ///   2. Xác minh mật khẩu bằng BCrypt.Verify
        ///   3. Kiểm tra tài khoản có bị khoá không (IsActive)
        ///   4. Cập nhật LastLoginAt và trả token
        ///
        /// Lưu ý bảo mật: Không nói rõ "sai username" hay "sai mật khẩu"
        /// — chỉ nói chung chung để tránh kẻ tấn công biết đâu là đúng.
        /// </summary>
        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            // --- Bước 1: Tìm user ---
            string input = dto.UsernameOrEmail.Trim().ToLower();
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Username == input || u.Email == input || u.Phone == input);

            if (user == null)
                return Fail("Thông tin đăng nhập không chính xác.");

            // --- Bước 2: Xác minh mật khẩu ---
            // BCrypt.Verify so sánh plain password với hash đã lưu — an toàn khỏi timing attack
            bool passwordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!passwordValid)
                return Fail("Thông tin đăng nhập không chính xác.");

            // --- Bước 3: Kiểm tra tài khoản ---
            if (!user.IsActive)
                return Fail("Tài khoản của bạn đã bị tạm khoá. Vui lòng liên hệ 1800 5678 để được hỗ trợ.");

            // --- Bước 4: Cập nhật lần đăng nhập cuối & tạo token ---
            user.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var (token, expiry) = _jwt.GenerateToken(user);

            return new AuthResponseDto
            {
                Success     = true,
                Message     = $"Đăng nhập thành công. Xin chào, {user.Ten}!",
                Token       = token,
                TokenExpiry = expiry,
                User        = MapToUserInfo(user)
            };
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private static AuthResponseDto Fail(string message) =>
            new() { Success = false, Message = message };

        private static UserInfoDto MapToUserInfo(User user) => new()
        {
            Id          = user.Id,
            FullName    = $"{user.HoTenDem} {user.Ten}".Trim(),
            Email       = user.Email,
            Username    = user.Username,
            Role        = user.Role.ToString(),
            RoleDisplay = user.Role switch
            {
                UserRole.Admin   => "Quản trị viên",
                UserRole.Doctor  => "Bác sĩ",
                UserRole.Staff   => "Lễ tân / Nhân viên",
                UserRole.Patient => "Bệnh nhân",
                _                => "Không xác định"
            }
        };
    }
}
