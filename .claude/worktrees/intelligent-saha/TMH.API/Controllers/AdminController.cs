using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TMH.API.Data;
using TMH.Shared.Enums;
using TMH.Shared.Models;

namespace TMH.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        public AdminController(AppDbContext db) { _db = db; }

        // ── Thống kê tổng quan ────────────────────────────────────────
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var today = DateTime.Today;
            return Ok(new
            {
                TotalAppointments = await _db.Appointments.CountAsync(),
                TodayAppointments = await _db.Appointments.Include(a => a.Schedule)
                                       .CountAsync(a => a.Schedule.WorkDate.Date == today),
                TotalPatients     = await _db.Patients.CountAsync(),
                TotalUsers        = await _db.Users.CountAsync(),
                ActiveDoctors     = await _db.Doctors.CountAsync(d => d.IsAvailable),
                CompletedToday    = await _db.Appointments.Include(a => a.Schedule)
                                       .CountAsync(a => a.Schedule.WorkDate.Date == today
                                                     && a.Status == AppointmentStatus.HoanThanh),
                CancelledToday    = await _db.Appointments.Include(a => a.Schedule)
                                       .CountAsync(a => a.Schedule.WorkDate.Date == today
                                                     && (a.Status == AppointmentStatus.DaHuy
                                                      || a.Status == AppointmentStatus.VangMat))
            });
        }

        // ── Thống kê 7 ngày ───────────────────────────────────────────
        [HttpGet("stats/weekly")]
        public async Task<IActionResult> GetWeeklyStats()
        {
            var from = DateTime.Today.AddDays(-6);
            var apts = await _db.Appointments.Include(a => a.Schedule)
                           .Where(a => a.Schedule.WorkDate.Date >= from).ToListAsync();

            var result = Enumerable.Range(0, 7).Select(i =>
            {
                var d   = from.AddDays(i);
                var day = apts.Where(a => a.Schedule.WorkDate.Date == d).ToList();
                return new
                {
                    Date      = d.ToString("dd/MM"),
                    Total     = day.Count,
                    Completed = day.Count(a => a.Status == AppointmentStatus.HoanThanh),
                    Cancelled = day.Count(a => a.Status == AppointmentStatus.DaHuy
                                           || a.Status == AppointmentStatus.VangMat)
                };
            });
            return Ok(result);
        }

        // ── Thống kê theo bác sĩ ─────────────────────────────────────
        [HttpGet("stats/by-doctor")]
        public async Task<IActionResult> GetStatsByDoctor()
        {
            var result = await _db.Appointments.Include(a => a.Doctor)
                .GroupBy(a => new { a.DoctorId, a.Doctor.FullName })
                .Select(g => new
                {
                    DoctorName = g.Key.FullName,
                    Total      = g.Count(),
                    Completed  = g.Count(a => a.Status == AppointmentStatus.HoanThanh),
                    Cancelled  = g.Count(a => a.Status == AppointmentStatus.DaHuy
                                          || a.Status == AppointmentStatus.VangMat)
                })
                .OrderByDescending(x => x.Total)
                .ToListAsync();
            return Ok(result);
        }

        // ── Quản lý tài khoản ────────────────────────────────────────
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _db.Users.OrderBy(u => u.Role).ThenBy(u => u.Id)
                .Select(u => new
                {
                    u.Id,
                    FullName = u.HoTenDem + " " + u.Ten,
                    u.Username, u.Email, u.Phone,
                    Role     = u.Role.ToString(),
                    u.IsActive, u.CreatedAt
                }).ToListAsync();
            return Ok(users);
        }

        [HttpPut("users/{id:int}/toggle-active")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound(new { Success = false, Message = "Không tìm thấy tài khoản." });
            if (user.Role == UserRole.Admin)
                return BadRequest(new { Success = false, Message = "Không thể khóa tài khoản Admin." });
            user.IsActive = !user.IsActive;
            await _db.SaveChangesAsync();
            return Ok(new { Success = true, Message = user.IsActive ? "Đã mở khóa." : "Đã khóa.", IsActive = user.IsActive });
        }

        // ── Quản lý bác sĩ ───────────────────────────────────────────
        [HttpGet("doctors")]
        public async Task<IActionResult> GetDoctors()
        {
            var doctors = await _db.Doctors.Include(d => d.User)
                .Select(d => new
                {
                    d.Id, d.FullName, d.Specialty, d.Degree, d.Description, d.IsAvailable,
                    Email = d.User.Email, Phone = d.User.Phone
                }).ToListAsync();
            return Ok(doctors);
        }

        [HttpPut("doctors/{id:int}/toggle-available")]
        public async Task<IActionResult> ToggleAvailable(int id)
        {
            var doc = await _db.Doctors.FindAsync(id);
            if (doc == null) return NotFound(new { Success = false, Message = "Không tìm thấy bác sĩ." });
            doc.IsAvailable = !doc.IsAvailable;
            await _db.SaveChangesAsync();
            return Ok(new { Success = true, Message = doc.IsAvailable ? "Đã hiển thị bác sĩ." : "Đã ẩn bác sĩ.", IsAvailable = doc.IsAvailable });
        }

        // ── Quản lý lịch làm việc ────────────────────────────────────
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
                    w.Id, w.DoctorId,
                    DoctorName     = w.Doctor.FullName,
                    WorkDate       = w.WorkDate.ToString("yyyy-MM-dd"),
                    StartTime      = w.StartTime.ToString(@"hh\:mm"),
                    EndTime        = w.EndTime.ToString(@"hh\:mm"),
                    w.MaxPatients, w.CurrentPatients,
                    RemainingSlots = w.MaxPatients - w.CurrentPatients
                }).ToListAsync();
            return Ok(result);
        }

        [HttpPost("schedules")]
        public async Task<IActionResult> CreateSchedule([FromBody] WorkScheduleUpsertDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            bool overlap = await _db.WorkSchedules.AnyAsync(w =>
                w.DoctorId == dto.DoctorId && w.WorkDate.Date == dto.WorkDate.Date &&
                w.StartTime < dto.EndTime && w.EndTime > dto.StartTime);

            if (overlap)
                return BadRequest(new { Success = false, Message = "Khung giờ bị trùng với lịch đã có." });

            var schedule = new WorkSchedule
            {
                DoctorId = dto.DoctorId, WorkDate = dto.WorkDate.Date,
                StartTime = dto.StartTime, EndTime = dto.EndTime,
                MaxPatients = dto.MaxPatients, CurrentPatients = 0
            };
            _db.WorkSchedules.Add(schedule);
            await _db.SaveChangesAsync();
            return Ok(new { Success = true, Message = "Tạo lịch thành công.", Id = schedule.Id });
        }

        [HttpPost("schedules/batch")]
        public async Task<IActionResult> CreateScheduleBatch([FromBody] WorkScheduleBatchDto dto)
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
                            DoctorId = docId, WorkDate = date,
                            StartTime = shift.StartTime, EndTime = shift.EndTime,
                            MaxPatients = dto.MaxPatients, CurrentPatients = 0
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

        // ── Thống kê theo tháng (12 tháng gần nhất) ─────────────────
        [HttpGet("stats/monthly")]
        public async Task<IActionResult> GetMonthlyStats()
        {
            var from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
            var apts = await _db.Appointments.Include(a => a.Schedule)
                           .Where(a => a.Schedule.WorkDate >= from).ToListAsync();

            var result = Enumerable.Range(0, 12).Select(i =>
            {
                var d   = from.AddMonths(i);
                var month = apts.Where(a => a.Schedule.WorkDate.Year  == d.Year
                                         && a.Schedule.WorkDate.Month == d.Month).ToList();
                return new
                {
                    Label     = d.ToString("MM/yyyy"),
                    Total     = month.Count,
                    Completed = month.Count(a => a.Status == AppointmentStatus.HoanThanh),
                    Cancelled = month.Count(a => a.Status == AppointmentStatus.DaHuy
                                             || a.Status == AppointmentStatus.VangMat)
                };
            });
            return Ok(result);
        }

        // ── Thống kê chi tiết để xuất báo cáo ───────────────────────
        [HttpGet("stats/report")]
        public async Task<IActionResult> GetReportStats(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var f = from?.Date ?? DateTime.Today.AddMonths(-1);
            var t = to?.Date   ?? DateTime.Today;

            var apts = await _db.Appointments
                .Include(a => a.Schedule)
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .Where(a => a.Schedule.WorkDate.Date >= f && a.Schedule.WorkDate.Date <= t)
                .OrderBy(a => a.Schedule.WorkDate)
                .ToListAsync();

            var total     = apts.Count;
            var completed = apts.Count(a => a.Status == AppointmentStatus.HoanThanh);
            var cancelled = apts.Count(a => a.Status == AppointmentStatus.DaHuy || a.Status == AppointmentStatus.VangMat);
            var pending   = total - completed - cancelled;

            var byDoctor = apts
                .GroupBy(a => a.Doctor?.FullName ?? "—")
                .Select(g => new {
                    Doctor    = g.Key,
                    Total     = g.Count(),
                    Completed = g.Count(a => a.Status == AppointmentStatus.HoanThanh),
                    Cancelled = g.Count(a => a.Status == AppointmentStatus.DaHuy || a.Status == AppointmentStatus.VangMat)
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            return Ok(new {
                From      = f.ToString("dd/MM/yyyy"),
                To        = t.ToString("dd/MM/yyyy"),
                total,completed, cancelled, Pending = pending,
                CompletionRate = total > 0 ? Math.Round((double)completed / total * 100, 1) : 0,
                CancellationRate = total > 0 ? Math.Round((double)cancelled / total * 100, 1) : 0,
                ByDoctor  = byDoctor
            });
        }

        // ── Tạo tài khoản Staff / Doctor (Admin tạo hộ) ─────────────
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (dto.Role != UserRole.Staff && dto.Role != UserRole.Doctor)
                return BadRequest(new { Success = false, Message = "Chỉ được tạo tài khoản Staff hoặc Doctor." });

            bool usernameTaken = await _db.Users.AnyAsync(u => u.Username == dto.Username.ToLower());
            if (usernameTaken)
                return Conflict(new { Success = false, Message = "Tên đăng nhập đã tồn tại." });

            bool emailTaken = await _db.Users.AnyAsync(u => u.Email == dto.Email.ToLower());
            if (emailTaken)
                return Conflict(new { Success = false, Message = "Email đã được sử dụng." });

            string hash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12);
            var user = new User
            {
                HoTenDem = dto.HoTenDem.Trim(),
                Ten      = dto.Ten.Trim(),
                Phone    = dto.Phone.Trim(),
                Email    = dto.Email.ToLower().Trim(),
                Username = dto.Username.ToLower().Trim(),
                PasswordHash = hash,
                Role     = dto.Role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Nếu là Doctor thì tự động tạo bản ghi Doctor
            if (dto.Role == UserRole.Doctor)
            {
                var doctor = new Doctor
                {
                    UserId      = user.Id,
                    FullName    = user.FullName,
                    Specialty   = dto.Specialty?.Trim() ?? "Tai Mũi Họng",
                    Degree      = dto.Degree?.Trim(),
                    Description = dto.Description?.Trim(),
                    IsAvailable = true
                };
                _db.Doctors.Add(doctor);
                await _db.SaveChangesAsync();
            }

            return Ok(new { Success = true, Message = "Tạo tài khoản thành công.", UserId = user.Id });
        }

        // ── Reset mật khẩu tài khoản ─────────────────────────────────
        [HttpPut("users/{id:int}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordDto dto)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound(new { Success = false, Message = "Không tìm thấy tài khoản." });
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
                return BadRequest(new { Success = false, Message = "Mật khẩu mới phải ít nhất 8 ký tự." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 12);
            await _db.SaveChangesAsync();
            return Ok(new { Success = true, Message = "Đã đặt lại mật khẩu thành công." });
        }

        // ── Sửa thông tin bác sĩ ────────────────────────────────────
        [HttpPut("doctors/{id:int}")]
        public async Task<IActionResult> UpdateDoctor(int id, [FromBody] DoctorUpdateDto dto)
        {
            var doc = await _db.Doctors.FindAsync(id);
            if (doc == null) return NotFound(new { Success = false, Message = "Không tìm thấy bác sĩ." });

            if (!string.IsNullOrWhiteSpace(dto.FullName))    doc.FullName    = dto.FullName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Specialty))   doc.Specialty   = dto.Specialty.Trim();
            if (dto.Degree      != null) doc.Degree      = dto.Degree.Trim();
            if (dto.Description != null) doc.Description = dto.Description.Trim();

            await _db.SaveChangesAsync();
            return Ok(new { Success = true, Message = "Cập nhật thông tin bác sĩ thành công." });
        }

        [HttpDelete("schedules/{id:int}")]
        public async Task<IActionResult> DeleteSchedule(int id)
        {
            var schedule = await _db.WorkSchedules.Include(w => w.Appointments)
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

    public class WorkScheduleUpsertDto
    {
        public int DoctorId { get; set; }
        public DateTime WorkDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int MaxPatients { get; set; } = 10;
    }

    public class WorkScheduleBatchDto
    {
        public List<int> DoctorIds    { get; set; } = new();
        public DateTime FromDate      { get; set; }
        public DateTime ToDate        { get; set; }
        public List<int> Weekdays     { get; set; } = new();
        public List<ShiftDto> Shifts  { get; set; } = new();
        public int MaxPatients        { get; set; } = 10;
    }

    public class ShiftDto
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime   { get; set; }
    }

    public class AdminCreateUserDto
    {
        public string HoTenDem   { get; set; } = string.Empty;
        public string Ten        { get; set; } = string.Empty;
        public string Phone      { get; set; } = string.Empty;
        public string Email      { get; set; } = string.Empty;
        public string Username   { get; set; } = string.Empty;
        public string Password   { get; set; } = string.Empty;
        public UserRole Role     { get; set; }
        // Doctor-specific (chỉ dùng khi Role == Doctor)
        public string? Specialty    { get; set; }
        public string? Degree       { get; set; }
        public string? Description  { get; set; }
    }

    public class ResetPasswordDto
    {
        public string NewPassword { get; set; } = string.Empty;
    }

    public class DoctorUpdateDto
    {
        public string? FullName    { get; set; }
        public string? Specialty   { get; set; }
        public string? Degree      { get; set; }
        public string? Description { get; set; }
    }
}
