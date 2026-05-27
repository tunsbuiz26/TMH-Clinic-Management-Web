using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TMH.API.Data;
using TMH.Shared.DTOs;
using TMH.Shared.Models;

namespace TMH.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ReviewController(AppDbContext db) { _db = db; }

        // ── PUBLIC ──────────────────────────────────────────────────────────
        // GET /api/review/summary — tóm tắt điểm đánh giá tất cả bác sĩ (trang Đánh giá)
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var doctors = await _db.Doctors
                .Where(d => d.IsAvailable)
                .Select(d => new
                {
                    d.Id, d.FullName, d.Degree, d.Specialty,
                    Reviews = d.Reviews.Select(r => new { r.Rating, r.Comment, r.CreatedAt,
                        PatientName = r.Patient.FullName })
                })
                .ToListAsync();

            var result = doctors.Select(d =>
            {
                var reviews = d.Reviews.ToList();
                return new DoctorRatingSummaryDto
                {
                    DoctorId      = d.Id,
                    DoctorName    = d.FullName,
                    DoctorDegree  = d.Degree ?? "",
                    Specialty     = d.Specialty,
                    AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0,
                    TotalReviews  = reviews.Count,
                    RecentReviews = reviews
                        .OrderByDescending(r => r.CreatedAt)
                        .Take(5)
                        .Select(r => new ReviewDto
                        {
                            Rating      = r.Rating,
                            Comment     = r.Comment,
                            CreatedAt   = r.CreatedAt,
                            PatientName = MaskName(r.PatientName)
                        })
                        .ToList()
                };
            })
            .OrderByDescending(d => d.AverageRating)
            .ThenByDescending(d => d.TotalReviews)
            .ToList();

            return Ok(result);
        }

        // GET /api/review/doctor/{id} — chi tiết đánh giá 1 bác sĩ
        [HttpGet("doctor/{id:int}")]
        public async Task<IActionResult> GetByDoctor(int id, [FromQuery] int page = 1)
        {
            const int pageSize = 10;
            var query = _db.Reviews
                .Include(r => r.Patient)
                .Where(r => r.DoctorId == id)
                .OrderByDescending(r => r.CreatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ReviewDto
                {
                    Id          = r.Id,
                    DoctorId    = r.DoctorId,
                    Rating      = r.Rating,
                    Comment     = r.Comment,
                    CreatedAt   = r.CreatedAt,
                    PatientName = MaskName(r.Patient.FullName)
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        // ── PATIENT ─────────────────────────────────────────────────────────
        // POST /api/review — bệnh nhân gửi đánh giá
        [HttpPost]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> Submit([FromBody] SubmitReviewDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ReviewResponseDto { Success = false, Message = "Dữ liệu không hợp lệ." });

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

            var appointment = await _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Reviews)
                .FirstOrDefaultAsync(a => a.Id == dto.AppointmentId);

            if (appointment == null)
                return NotFound(new ReviewResponseDto { Success = false, Message = "Không tìm thấy lịch khám." });

            if (appointment.Patient.UserId != userId)
                return Forbid();

            if (appointment.Status != AppointmentStatus.HoanThanh)
                return BadRequest(new ReviewResponseDto { Success = false, Message = "Chỉ có thể đánh giá lịch khám đã hoàn thành." });

            if (appointment.Reviews.Any())
                return BadRequest(new ReviewResponseDto { Success = false, Message = "Lịch khám này đã được đánh giá rồi." });

            var review = new Review
            {
                AppointmentId = dto.AppointmentId,
                PatientId     = appointment.PatientId,
                DoctorId      = appointment.DoctorId!.Value,
                Rating        = dto.Rating,
                Comment       = dto.Comment?.Trim(),
                CreatedAt     = DateTime.UtcNow
            };

            _db.Reviews.Add(review);
            await _db.SaveChangesAsync();

            return Ok(new ReviewResponseDto { Success = true, Message = "Đánh giá đã được ghi nhận. Cảm ơn bạn!" });
        }

        // ── ADMIN ────────────────────────────────────────────────────────────
        // DELETE /api/review/{id} — admin xoá đánh giá không phù hợp
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var review = await _db.Reviews.FindAsync(id);
            if (review == null) return NotFound();
            _db.Reviews.Remove(review);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã xoá đánh giá." });
        }

        // ── Helper: ẩn bớt tên bệnh nhân (Nguyễn Văn A → Nguyễn V** A) ────
        private static string MaskName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Ẩn danh";
            var parts = name.Trim().Split(' ');
            if (parts.Length <= 1) return name[0] + "***";
            // Giữ họ + ẩn tên đệm + giữ tên
            return parts[0] + " " + string.Join(" ", parts[1..^1].Select(p => p[0] + "**")) + (parts.Length > 1 ? " " + parts[^1] : "");
        }
    }
}
