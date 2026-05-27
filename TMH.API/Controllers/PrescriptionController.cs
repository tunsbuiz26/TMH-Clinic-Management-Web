using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TMH.API.Data;
using TMH.API.Services;
using TMH.Shared.DTOs;

namespace TMH.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PrescriptionController : ControllerBase
    {
        private readonly PrescriptionService _svc;
        private readonly AppDbContext _db;

        public PrescriptionController(PrescriptionService svc, AppDbContext db)
        {
            _svc = svc;
            _db  = db;
        }

        // ── Lấy đơn thuốc theo AppointmentId ─────────────────────────
        // GET /api/prescription/by-appointment/5
        [HttpGet("by-appointment/{appointmentId}")]
        [Authorize(Roles = "Admin,Doctor,Staff,Patient")]
        public async Task<IActionResult> GetByAppointment(int appointmentId)
        {
            var result = await _svc.GetByAppointmentAsync(appointmentId);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // ── Lấy đơn thuốc theo Id ────────────────────────────────────
        // GET /api/prescription/10
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Doctor,Staff,Patient")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _svc.GetByIdAsync(id);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // ── Tạo đơn thuốc mới ─────────────────────────────────────────
        // POST /api/prescription
        [HttpPost]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> Create([FromBody] CreatePrescriptionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });

            var doctorId = await ResolveDoctorIdAsync();
            if (doctorId == null)
                return Unauthorized(new { success = false, message = "Không xác định được bác sĩ." });

            var result = await _svc.CreateAsync(dto, doctorId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Cập nhật đơn thuốc ────────────────────────────────────────
        // PUT /api/prescription/10
        [HttpPut("{id}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdatePrescriptionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });

            var doctorId = await ResolveDoctorIdAsync();
            if (doctorId == null)
                return Unauthorized(new { success = false, message = "Không xác định được bác sĩ." });

            var result = await _svc.UpdateAsync(id, dto, doctorId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Xoá đơn thuốc ────────────────────────────────────────────
        // DELETE /api/prescription/10
        [HttpDelete("{id}")]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var doctorId = await ResolveDoctorIdAsync();
            if (doctorId == null)
                return Unauthorized(new { success = false, message = "Không xác định được bác sĩ." });

            var result = await _svc.DeleteAsync(id, doctorId.Value);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Helper: lấy DoctorId từ userId trong JWT → tra DB ────────
        // JWT chỉ chứa sub (userId), không có doctorId
        // → dùng userId để tìm Doctor.Id trong bảng Doctors
        private async Task<int?> ResolveDoctorIdAsync()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (!int.TryParse(userIdStr, out int userId)) return null;

            var doctor = await _db.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == userId);

            return doctor?.Id;
        }
    }
}

