using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TMH.API.Services;
using TMH.Shared.DTOs;

namespace TMH.API.Controllers
    
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppointmentController : ControllerBase
    {
        private readonly AppointmentService _svc;

        public AppointmentController(AppointmentService svc)
        {
            _svc = svc;
        }

        // GET /api/appointment/available-doctors?date=2026-03-20
        // Công khai — ai cũng xem được danh sách bác sĩ và slot trống
        [HttpGet("available-doctors")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailableDoctors([FromQuery] DateTime? date)
        {
            var result = await _svc.GetAvailableDoctorsAsync(date);
            return Ok(result);
        }

        // POST /api/appointment/book
        // Chỉ bệnh nhân đã đăng nhập mới được đặt lịch
        [HttpPost("book")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> Book([FromBody] BookAppointmentDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _svc.BookAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // GET /api/appointment/my-appointments
        // Bệnh nhân xem lịch của chính mình
        [HttpGet("my-appointments")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> MyAppointments()
        {
            // Đọc userId từ JWT claim
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            var result = await _svc.GetByPatientUserIdAsync(userId);
            return Ok(result);
        }

        // POST /api/appointment/cancel/{id}
        // Bệnh nhân huỷ lịch của chính mình
        [HttpPost("cancel/{id}")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> Cancel(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            var result = await _svc.CancelAsync(id, userId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // GET /api/appointment/by-date?date=2026-03-20&doctorId=1
        // Lễ tân và bác sĩ xem lịch theo ngày
        [HttpGet("by-date")]
        [Authorize(Roles = "Admin,Doctor,Staff")]
        public async Task<IActionResult> ByDate([FromQuery] DateTime date,
                                                 [FromQuery] int? doctorId)
        {
            var result = await _svc.GetByDateAsync(date, doctorId);
            return Ok(result);
        }

        // PUT /api/appointment/reschedule
        // Lễ tân đổi lịch khám (đổi bác sĩ hoặc đổi khung giờ)
        [HttpPut("reschedule")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Reschedule([FromBody] RescheduleDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _svc.RescheduleAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // GET /api/appointment/search?q=...
        // Lễ tân tìm kiếm toàn bộ lịch khám theo BookingCode hoặc tên bệnh nhân
        [HttpGet("search")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            var result = await _svc.SearchAsync(q);
            return Ok(result);
        }

        // PUT /api/appointment/update-status
        // Lễ tân và bác sĩ cập nhật trạng thái
        [HttpPut("update-status")]
        [Authorize(Roles = "Admin,Doctor,Staff")]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateAppointmentStatusDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _svc.UpdateStatusAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
