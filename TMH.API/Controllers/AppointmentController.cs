using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TMH.API.Services;
using TMH.Shared.DTOs;
using Microsoft.EntityFrameworkCore; // Thêm using này nếu chưa có
using TMH.API.Data; 
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

        // GET /api/appointment/available-doctors?date=2026-05-20
        // Công khai — ai cũng xem được danh sách bác sĩ và slot trống
        [HttpGet("available-doctors")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailableDoctors([FromQuery] DateTime? date)
        {
            var result = await _svc.GetAvailableDoctorsAsync(date);
            return Ok(result);
        }

        // POST /api/appointment/book
        // Bệnh nhân đặt lịch — DoctorId và ScheduleId đều tuỳ chọn
        [HttpPost("book")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> Book([FromBody] BookAppointmentDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _svc.BookAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        // GET /api/appointment/search-patients?q=...
        [HttpGet("search-patients")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> SearchPatients([FromQuery] string? q, [FromServices] AppDbContext db)
        {
            var query = db.Patients.Include(p => p.User).AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var lq = q.Trim().ToLower();
                query = query.Where(p =>
                    p.FullName.ToLower().Contains(lq) ||
                    p.RecordCode.ToLower().Contains(lq) ||
                    p.User.Phone.Contains(lq));
            }

            var result = await query
                .OrderBy(p => p.FullName)
                .Take(20)
                .Select(p => new {
                    id = p.Id,
                    fullName = p.FullName,
                    recordCode = p.RecordCode,
                    gender = p.Gender,
                    phone = p.User.Phone
                })
                .ToListAsync();

            return Ok(result);
        }
        // POST /api/appointment/walk-in
        [HttpPost("walk-in")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> BookWalkIn([FromBody] BookAppointmentDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Dùng chung logic BookAsync đã viết rất chuẩn của bạn
            var result = await _svc.BookAsync(dto);

            return result.Success ? Ok(result) : BadRequest(result);
        }
        // PUT /api/appointment/assign
        // Lễ tân gán bác sĩ + khung giờ cho lịch đang ChoPhanCong
        [HttpPut("assign")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Assign([FromBody] AssignScheduleDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _svc.AssignScheduleAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // GET /api/appointment/pending-assignment
        // Lễ tân xem danh sách lịch đang chờ phân công
        [HttpGet("pending-assignment")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> PendingAssignment([FromQuery] PendingAssignmentFilterDto filter)
        {
            var result = await _svc.GetPendingAssignmentAsync(filter);
            return Ok(result);
        }

        // GET /api/appointment/my-appointments
        // Bệnh nhân xem lịch của chính mình
        [HttpGet("my-appointments")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> MyAppointments()
        {
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

        // GET /api/appointment/by-date?date=2026-05-20&doctorId=1
        // Lễ tân và bác sĩ xem lịch theo ngày (bao gồm cả lịch ChoPhanCong)
        [HttpGet("by-date")]
        [Authorize(Roles = "Admin,Doctor,Staff")]
        public async Task<IActionResult> ByDate([FromQuery] DateTime date,
                                                [FromQuery] int? doctorId)
        {
            var result = await _svc.GetByDateAsync(date, doctorId);
            return Ok(result);
        }

        // PUT /api/appointment/reschedule
        // Lễ tân đổi lịch khám (bao gồm cả lịch ChoPhanCong)
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
        // Lễ tân tìm kiếm theo BookingCode hoặc tên bệnh nhân
        [HttpGet("search")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            var result = await _svc.SearchAsync(q);
            return Ok(result);
        }

        // PUT /api/appointment/update-status
        // Lễ tân và bác sĩ cập nhật trạng thái lịch khám
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
