using Microsoft.AspNetCore.Mvc;
using TMH.Web.Services;

namespace TMH.Web.Controllers
{
    /// <summary>
    /// RecommendationProxyController — proxy chuyển tiếp yêu cầu gợi ý bác sĩ
    /// từ Web App sang TMH.API.
    ///
    /// Pattern giống ChatProxyController: Web không gọi Anthropic trực tiếp,
    /// mà đi qua API để tập trung logic + bảo vệ API key.
    /// </summary>
    [Route("api/recommendation-proxy")]
    [ApiController]
    public class RecommendationProxyController : ControllerBase
    {
        private readonly ApiService _api;

        public RecommendationProxyController(ApiService api)
        {
            _api = api;
        }

        /// <summary>
        /// POST /api/recommendation-proxy/doctor
        /// Body: { "symptoms": "mô tả triệu chứng" }
        /// </summary>
        [HttpPost("doctor")]
        public async Task<IActionResult> RecommendDoctor([FromBody] RecommendationProxyRequest dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Symptoms))
                return BadRequest(new { success = false, message = "Vui lòng nhập triệu chứng." });

            var result = await _api.GetDoctorRecommendationAsync(dto.Symptoms);

            if (result == null)
                return Ok(new { success = false, message = "Không kết nối được đến máy chủ. Vui lòng thử lại." });

            return Ok(result);
        }
    }

    public class RecommendationProxyRequest
    {
        public string Symptoms { get; set; } = string.Empty;
    }
}
