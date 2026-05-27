using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TMH.API.Services;
using TMH.Shared.DTOs;

namespace TMH.API.Controllers
{
    /// <summary>
    /// PatientController quản lý hồ sơ bệnh nhân (bản thân + người thân).
    ///
    /// Tất cả endpoint yêu cầu đăng nhập với role Patient.
    /// Mọi thao tác đều kiểm tra UserId từ JWT — bệnh nhân chỉ
    /// thao tác được với hồ sơ mà họ là chủ sở hữu.
    ///
    /// Endpoints:
    ///   GET    /api/patient/my-profiles       → Danh sách hồ sơ của tôi
    ///   GET    /api/patient/{id}              → Chi tiết một hồ sơ
    ///   POST   /api/patient                  → Tạo hồ sơ mới
    ///   PUT    /api/patient                  → Cập nhật hồ sơ
    ///   DELETE /api/patient/{id}             → Xóa hồ sơ
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Patient")]   // Toàn bộ controller chỉ Patient mới truy cập được
    public class PatientController : ControllerBase
    {
        private readonly PatientService _svc;
        private readonly ILogger<PatientController> _logger;

        public PatientController(PatientService svc, ILogger<PatientController> logger)
        {
            _svc = svc;
            _logger = logger;
        }

        // =====================================================================
        // GET /api/patient/my-profiles
        // Lấy tất cả hồ sơ của user đang đăng nhập (bản thân + người thân đã tạo)
        // Đây là endpoint BookingController.Index gọi đầu tiên để populate dropdown
        // =====================================================================
        [HttpGet("my-profiles")]
        public async Task<IActionResult> GetMyProfiles()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var profiles = await _svc.GetByUserIdAsync(userId.Value);

            // Trả về mảng rỗng thay vì 404 nếu chưa có hồ sơ nào
            // — giúp frontend handle đơn giản hơn
            return Ok(profiles);
        }

        // =====================================================================
        // GET /api/patient/{id}
        // Xem chi tiết một hồ sơ cụ thể (có kiểm tra quyền sở hữu)
        // =====================================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _svc.GetByIdAsync(id, userId.Value);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // =====================================================================
        // POST /api/patient
        // Tạo hồ sơ mới (bản thân hoặc người thân)
        // RecordCode được sinh tự động phía service, client không cần truyền
        // =====================================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PatientUpsertDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _svc.CreateAsync(dto, userId.Value);

            _logger.LogInformation("Tạo hồ sơ mới cho UserId={UserId}: {Name}", userId, dto.FullName);

            // 201 Created kèm location header trỏ đến GET endpoint của hồ sơ vừa tạo
            return result.Success
                ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result)
                : BadRequest(result);
        }

        // =====================================================================
        // PUT /api/patient
        // Cập nhật hồ sơ — dto.Id xác định hồ sơ cần sửa
        // =====================================================================
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] PatientUpsertDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (dto.Id <= 0)
                return BadRequest(new { Success = false, Message = "Id hồ sơ không hợp lệ." });

            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _svc.UpdateAsync(dto, userId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // =====================================================================
        // DELETE /api/patient/{id}
        // Xóa hồ sơ — chỉ được xóa nếu không còn lịch khám đang hoạt động
        // =====================================================================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _svc.DeleteAsync(id, userId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // =====================================================================
        // HELPER: Đọc UserId từ JWT claim (được inject tự động bởi middleware)
        // =====================================================================
        private int? GetUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }
}