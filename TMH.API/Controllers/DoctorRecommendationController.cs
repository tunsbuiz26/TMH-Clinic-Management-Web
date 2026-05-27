using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TMH.API.Data;
using TMH.API.Services;
using TMH.Shared.DTOs;

namespace TMH.API.Controllers
{
    /// <summary>
    /// DoctorRecommendationController — endpoint gợi ý bác sĩ phù hợp dựa trên triệu chứng.
    ///
    /// Endpoint công khai (không cần đăng nhập) vì bệnh nhân cần tham khảo
    /// trước khi quyết định đặt lịch.
    /// </summary>
    [ApiController]
    [Route("api/recommendation")]
    public class DoctorRecommendationController : ControllerBase
    {
        private readonly DoctorRecommendationService _recommendationService;
        private readonly AppointmentService _appointmentService;
        private readonly AppDbContext _db;
        private readonly ILogger<DoctorRecommendationController> _logger;

        public DoctorRecommendationController(
            DoctorRecommendationService recommendationService,
            AppointmentService appointmentService,
            AppDbContext db,
            ILogger<DoctorRecommendationController> logger)
        {
            _recommendationService = recommendationService;
            _appointmentService    = appointmentService;
            _db                    = db;
            _logger                = logger;
        }

        /// <summary>
        /// POST /api/recommendation/doctor
        ///
        /// Nhận triệu chứng bệnh nhân → tự động lấy danh sách bác sĩ đang hoạt động
        /// → gọi AI phân tích → trả về gợi ý bác sĩ phù hợp nhất.
        /// </summary>
        [HttpPost("doctor")]
        public async Task<IActionResult> RecommendDoctor([FromBody] DoctorRecommendationRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Symptoms))
                return BadRequest(new DoctorRecommendationResponseDto
                {
                    Success = false,
                    Message = "Vui lòng mô tả triệu chứng để nhận gợi ý bác sĩ."
                });

            try
            {
                // Lấy danh sách bác sĩ có lịch trống (tái dụng endpoint đã có)
                var doctors = await _appointmentService.GetAvailableDoctorsAsync();

                if (doctors == null || !doctors.Any())
                    return Ok(new DoctorRecommendationResponseDto
                    {
                        Success = false,
                        Message = "Hiện chưa có bác sĩ nào có lịch trống. Vui lòng thử lại sau."
                    });

                // Gắn điểm đánh giá vào danh sách bác sĩ trước khi gọi AI
                var ratings = await _db.Reviews
                    .GroupBy(r => r.DoctorId)
                    .Select(g => new { DoctorId = g.Key, Avg = g.Average(r => (double)r.Rating), Count = g.Count() })
                    .ToListAsync();
                foreach (var doc in doctors)
                {
                    var r = ratings.FirstOrDefault(x => x.DoctorId == doc.DoctorId);
                    doc.AverageRating = r?.Avg ?? 0;
                    doc.ReviewCount   = r?.Count ?? 0;
                }

                var result = await _recommendationService.RecommendAsync(dto.Symptoms, doctors);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DoctorRecommendation] Lỗi xử lý gợi ý bác sĩ");
                return StatusCode(500, new DoctorRecommendationResponseDto
                {
                    Success = false,
                    Message = "Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại."
                });
            }
        }
    }
}
