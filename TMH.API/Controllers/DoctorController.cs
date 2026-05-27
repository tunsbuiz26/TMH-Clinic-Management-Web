using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TMH.API.Data;
using TMH.Shared.Models;

namespace TMH.API.Controllers
{
    /// <summary>
    /// API endpoints dành riêng cho Bác sĩ quản lý lịch làm việc của mình.
    /// DoctorId được xác định tự động từ JWT token (không cần truyền qua body).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Doctor")]
    public class DoctorController : ControllerBase
    {
        private readonly AppDbContext _db;
        public DoctorController(AppDbContext db) { _db = db; }

        // Lấy Doctor từ JWT (UserId → Doctor record)
        private async Task<Doctor?> GetMyDoctor()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return null;
            return await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
        }

        // ── GET api/doctor/my-schedules ───────────────────────────────
        [HttpGet("my-schedules")]
        public async Task<IActionResult> GetMySchedules(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var doc = await GetMyDoctor();
            if (doc == null)
                return BadRequest(new { Success = false, Message = "Không tìm thấy hồ sơ bác sĩ. Vui lòng liên hệ quản trị viên." });

            var query = _db.WorkSchedules
                           .Where(w => w.DoctorId == doc.Id)
                           .AsQueryable();

            if (from.HasValue) query = query.Where(w => w.WorkDate.Date >= from.Value.Date);
            if (to.HasValue) query = query.Where(w => w.WorkDate.Date <= to.Value.Date);

            var result = await query
                .OrderBy(w => w.WorkDate)
                .ThenBy(w => w.StartTime)
                .Select(w => new
                {
                    w.Id,
                    w.DoctorId,
                    WorkDate = w.WorkDate.ToString("yyyy-MM-dd"),
                    StartTime = w.StartTime.ToString(@"hh\:mm"),
                    EndTime = w.EndTime.ToString(@"hh\:mm"),
                    w.MaxPatients,
                    w.CurrentPatients,
                    RemainingSlots = w.MaxPatients - w.CurrentPatients,
                    w.Status
                })
                .ToListAsync();

            return Ok(result);
        }

        // ── POST api/doctor/my-schedules/batch ───────────────────────
        [HttpPost("my-schedules/batch")]
        public async Task<IActionResult> CreateMyScheduleBatch([FromBody] DoctorScheduleBatchDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var doc = await GetMyDoctor();
            if (doc == null)
                return BadRequest(new { Success = false, Message = "Không tìm thấy hồ sơ bác sĩ. Vui lòng liên hệ quản trị viên." });

            if (dto.Shifts == null || !dto.Shifts.Any())
                return BadRequest(new { Success = false, Message = "Vui lòng thêm ít nhất 1 ca làm việc." });
            if (dto.Weekdays == null || !dto.Weekdays.Any())
                return BadRequest(new { Success = false, Message = "Vui lòng chọn ít nhất 1 thứ trong tuần." });
            if (dto.FromDate > dto.ToDate)
                return BadRequest(new { Success = false, Message = "Ngày bắt đầu phải trước ngày kết thúc." });

            int created = 0, skipped = 0;

            for (var date = dto.FromDate.Date; date <= dto.ToDate.Date; date = date.AddDays(1))
            {
                if (!dto.Weekdays.Contains((int)date.DayOfWeek)) continue;

                foreach (var shift in dto.Shifts)
                {
                    bool overlap = await _db.WorkSchedules.AnyAsync(w =>
                        w.DoctorId == doc.Id &&
                        w.WorkDate.Date == date &&
                        w.StartTime < shift.EndTime &&
                        w.EndTime > shift.StartTime);

                    if (overlap) { skipped++; continue; }

                    _db.WorkSchedules.Add(new WorkSchedule
                    {
                        DoctorId = doc.Id,
                        WorkDate = date,
                        StartTime = shift.StartTime,
                        EndTime = shift.EndTime,
                        MaxPatients = dto.MaxPatients > 0 ? dto.MaxPatients : 10,
                        CurrentPatients = 0,
                        Status = "ChoDuyet"
                    });
                    created++;
                }
            }

            await _db.SaveChangesAsync();
            var msg = $"Đã tạo {created} lịch làm việc thành công.";
            if (skipped > 0) msg += $" Bỏ qua {skipped} lịch bị trùng khung giờ.";
            return Ok(new { Success = true, Message = msg, Created = created, Skipped = skipped });
        }

        // ── DELETE api/doctor/my-schedules/{id} ─────────────────────
        [HttpDelete("my-schedules/{id:int}")]
        public async Task<IActionResult> DeleteMySchedule(int id)
        {
            var doc = await GetMyDoctor();
            if (doc == null)
                return BadRequest(new { Success = false, Message = "Không tìm thấy hồ sơ bác sĩ." });

            var schedule = await _db.WorkSchedules.FindAsync(id);
            if (schedule == null)
                return NotFound(new { Success = false, Message = "Không tìm thấy lịch làm việc." });
            if (schedule.DoctorId != doc.Id)
                return Forbid();
            if (schedule.CurrentPatients > 0)
                return BadRequest(new { Success = false, Message = "Không thể xóa lịch đã có bệnh nhân đặt." });

            _db.WorkSchedules.Remove(schedule);
            await _db.SaveChangesAsync();
            return Ok(new { Success = true, Message = "Đã xóa lịch làm việc." });
        }

        // ── GET api/doctor/my-info ────────────────────────────────────
        [HttpGet("my-info")]
        public async Task<IActionResult> GetMyInfo()
        {
            var doc = await GetMyDoctor();
            if (doc == null)
                return NotFound(new { Success = false, Message = "Không tìm thấy hồ sơ bác sĩ." });

            return Ok(new
            {
                doc.Id,
                doc.FullName,
                doc.Specialty,
                doc.Degree,
                doc.Description,
                doc.IsAvailable
            });
        }

        // ── PUT api/doctor/my-info ────────────────────────────────────
        [HttpPut("my-info")]
        public async Task<IActionResult> UpdateMyInfo([FromBody] DoctorUpdateMyInfoDto dto)
        {
            var doc = await GetMyDoctor();
            if (doc == null)
                return NotFound(new { Success = false, Message = "Không tìm thấy hồ sơ bác sĩ." });

            if (!string.IsNullOrWhiteSpace(dto.Specialty)) doc.Specialty = dto.Specialty.Trim();
            if (dto.Degree != null) doc.Degree = dto.Degree.Trim();
            if (dto.Description != null) doc.Description = dto.Description.Trim();

            // Đồng bộ FullName với User nếu User đã thay đổi tên
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId))
            {
                var user = await _db.Users.FindAsync(userId);
                if (user != null)
                    doc.FullName = user.FullName;
            }

            await _db.SaveChangesAsync();
            return Ok(new { Success = true, Message = "Cập nhật thông tin chuyên môn thành công." });
        }
    } // <-- KẾT THÚC CLASS DoctorController TẠI ĐÂY

    // ====================================================================
    // CÁC LỚP DTO (Data Transfer Objects) NẰM NGOÀI CONTROLLER
    // ====================================================================

    public class DoctorScheduleBatchDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<int> Weekdays { get; set; } = new();
        public List<ShiftDto> Shifts { get; set; } = new();
        public int MaxPatients { get; set; } = 10;
    }

    public class DoctorUpdateMyInfoDto
    {
        public string? Specialty { get; set; }
        public string? Degree { get; set; }
        public string? Description { get; set; }
    }

    // Đã thêm class này để fix lỗi "ShiftDto could not be found"
    // Nếu bạn đã tạo nó ở file khác, bạn có thể xóa đoạn này đi.
   
} // <-- KẾT THÚC NAMESPACE TẠI ĐÂY