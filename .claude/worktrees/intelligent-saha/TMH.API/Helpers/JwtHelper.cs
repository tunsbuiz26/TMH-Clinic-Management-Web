using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TMH.Shared.Models;

namespace TMH.API.Helpers
{
    /// <summary>
    /// Dịch vụ chuyên tạo và xác thực JWT Token.
    ///
    /// Cách JWT hoạt động:
    ///   1. Sau khi đăng nhập, server tạo một token có chứa "claims" (thông tin user).
    ///   2. Client lưu token này và đính vào mọi request tiếp theo.
    ///   3. Server chỉ cần verify chữ ký của token — không cần truy vấn DB mỗi request.
    ///
    /// Cấu trúc JWT = Header.Payload.Signature
    ///   - Header: thuật toán ký (HS256)
    ///   - Payload: claims (userId, username, role, thời hạn)
    ///   - Signature: HMAC-SHA256(Header + Payload, SecretKey)
    /// </summary>
    public class JwtHelper
    {
        private readonly IConfiguration _config;

        public JwtHelper(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Tạo JWT token cho một user vừa đăng nhập thành công.
        /// Token chứa đủ thông tin để Web App biết user là ai, có quyền gì,
        /// mà không cần gọi thêm API lấy thông tin user.
        /// </summary>
        public (string token, DateTime expiry) GenerateToken(User user)
        {
            var jwtSettings  = _config.GetSection("JwtSettings");
            var secretKey    = jwtSettings["SecretKey"]!;
            var issuer       = jwtSettings["Issuer"]!;
            var audience     = jwtSettings["Audience"]!;
            var expireHours  = int.Parse(jwtSettings["ExpireHours"] ?? "8");

            var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // "Claims" là các thông tin được nhúng vào token.
            // Web App sẽ đọc từ đây thay vì gọi thêm API.
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("username",                    user.Username),
                new Claim("fullname",                    $"{user.HoTenDem} {user.Ten}".Trim()),

                // ClaimTypes.Role là claim đặc biệt mà [Authorize(Roles="...")] đọc tự động
                new Claim(ClaimTypes.Role,               user.Role.ToString()),
                new Claim("roleValue",                   ((int)user.Role).ToString()),

                // Jti giúp blacklist token nếu cần (logout sớm)
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
            };

            var expiry = DateTime.UtcNow.AddHours(expireHours);
            var token  = new JwtSecurityToken(
                issuer:             issuer,
                audience:           audience,
                claims:             claims,
                notBefore:          DateTime.UtcNow,
                expires:            expiry,
                signingCredentials: creds
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expiry);
        }

        /// <summary>
        /// Xác thực token và trả về ClaimsPrincipal (đối tượng chứa thông tin user).
        /// Dùng khi cần đọc thông tin user từ token trong các request tiếp theo.
        /// </summary>
        public ClaimsPrincipal? ValidateToken(string token)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = key,
                ValidateIssuer           = true,
                ValidIssuer              = jwtSettings["Issuer"],
                ValidateAudience         = true,
                ValidAudience            = jwtSettings["Audience"],
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.Zero   // Không có vùng chấp nhận hết hạn
            };

            try
            {
                return new JwtSecurityTokenHandler()
                    .ValidateToken(token, validationParams, out _);
            }
            catch
            {
                return null; // Token không hợp lệ hoặc đã hết hạn
            }
        }
    }
}
