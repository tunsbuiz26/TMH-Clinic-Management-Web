using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TMH.API.Data;
using TMH.Shared.Enums;
using TMH.Shared.Models;

namespace TMH.API.Controllers
{
    /// <summary>
    /// API endpoints lịch làm việc dành cho Staff (Lễ tân).
    /// Staff được phép: xem, tạo (tự động DaDuyet), xóa (nếu chưa có BN đặt).
    /// Dùng route api/staff/schedules để tách biệt với api/admin/schedules.
    /// </summary>
    [ApiController]
    [Route("api/staff")]
    [Authorize(Roles = "Admin,Staff")]
    public class StaffScheduleController : ControllerBase
    {
        private readonly AppDbContext _db;
        public StaffScheduleController(AppDbContext db) { _db = db; }

        // ── GET api/staff/schedules ───────────────────────────────────
        [HttpGet("schedules")]
        public async Task<IActionResult> GetSchedules(
            [FromQuery] int? doctorId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var query = _db.WorkSchedules.Include(w => w.Doctor).AsQueryable();
            if (doctorId.HasValue) query = query.Where(w => w.DoctorId == doctorId.Value);
            if (from.HasValue)     query = query.Where(w => w.WorkDate.Date >= from.Value.Date);
            if (to.HasValue)       query = query.Where(w => w.WorkDate.Date <= to.Value.Date);

            var result = await query.OrderBy(w => w.WorkDate).ThenBy(w => w.StartTime)
                .Select(w => new
                {
                    w.Id,
                    w.DoctorId,
                    DoctorName     = w.Doctor.FullName,
                    WorkDate       = w.WorkDate.ToString("yyyy-MM-dd"),
                    StartTime      = w.StartTime.ToString(@"hh\:mm"),
                    EndTime        = w.EndTime.ToString(@"hh\:mm"),
                    w.MaxPatients,
                    w.CurrentPatients,
                    RemainingSlots = w.MaxPatients - w.CurrentPatients,
                    Status         = w.Status ?? "Approved"
                }).ToListAsync();

            return Ok(result);
        }

        // ── POST api/staff/schedules/batch ────────────────────────────
        // Tạo lịch hàng loạt — tự động đặt trạng thái DaDuyet (không cần admin duyệt)
        [HttpPost("schedules/batch")]
        public async Task<IActionResult> CreateScheduleBatch([FromBody] StaffScheduleBatchDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!dto.DoctorIds.Any())
                return BadRequest(new { Success = false, Message = "Vui lòng chọn ít nhất 1 bác sĩ." });
            if (!dto.Shifts.Any())
                return BadRequest(new { Success = false, Message = "Vui lòng thêm ít nhất 1 ca." });
            if (!dto.Weekdays.Any())
                return BadRequest(new { Success = false, Message = "Vui lòng chọn ít nhất 1 thứ." });
            if (dto.FromDate > dto.ToDate)
                return BadRequest(new { Success = false, Message = "Ngày bắt đầu phải trước ngày kết thúc." });

            int created = 0, skipped = 0;

            for (var date = dto.FromDate.Date; date <= dto.ToDate.Date; date = date.AddDays(1))
            {
                if (!dto.Weekdays.Contains((int)date.DayOfWeek)) continue;

                foreach (var docId in dto.DoctorIds)
                {
                    foreach (var shift in dto.Shifts)
                    {
                        bool overlap = await _db.WorkSchedules.AnyAsync(w =>
                            w.DoctorId == docId && w.WorkDate.Date == date &&
                            w.StartTime < shift.EndTime && w.EndTime > shift.StartTime);

                        if (overlap) { skipped++; continue; }

                        _db.WorkSchedules.Add(new WorkSchedule
                        {
                            DoctorId        = docId,
                            WorkDate        = date,
                            StartTime       = shift.StartTime,
                            EndTime         = shift.EndTime,
                            MaxPatients     = dto.MaxPatients,
                            CurrentPatients = 0,
                            Status          = "Approved"   // Tự động duyệt — không cần admin
                        });
                        created++;
                    }
                }
            }

            await _db.SaveChangesAsync();
            var msg = $"Đã tạo {created} lịch thành công.";
            if (skipped > 0) msg += $" Bỏ qua {skipped} lịch trùng.";
            return Ok(new { Success = true, Message = msg, Created = created, Skipped = skipped });
        }

        // ── DELETE api/staff/schedules/{id} ──────────────────────────
        [HttpDelete("schedules/{id:int}")]
        public async Task<IActionResult> DeleteSchedule(int id)
        {
            var schedule = await _db.WorkSchedules
                .Include(w => w.Appointments)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (schedule == null)
                return NotFound(new { Success = false, Message = "Không tìm thấy lịch." });

            bool hasActive = schedule.Appointments.Any(a =>
                a.Status != AppointmentStatus.DaHuy &&
                a.Status != AppointmentStatus.HoanThanh &&
                a.Status != AppointmentStatus.VangMat);

            if (hasActive)
                return BadRequest(new { Success = false, Message = "Không thể xóa — khung giờ đang có lịch đặt." });

            _db.WorkSchedules.Remove(schedule);
            await _db.SaveChangesAsync();
            return Ok(new { Success = true, Message = "Đã xóa lịch làm việc." });
        }
    }

    // DTOs riêng cho Staff (tránh conflict với AdminController DTOs)
    public class StaffScheduleBatchDto
    {
        public List<int> DoctorIds   { get; set; } = new();
        public DateTime FromDate     { get; set; }
        public DateTime ToDate       { get; set; }
        public List<int> Weekdays    { get; set; } = new();
        public List<StaffShiftDto> Shifts { get; set; } = new();
        public int MaxPatients       { get; set; } = 10;
    }

    public class StaffShiftDto
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime   { get; set; }
    }
}
