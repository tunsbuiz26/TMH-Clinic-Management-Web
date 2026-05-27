using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TMH.Shared.DTOs;

namespace TMH.API.Services
{
    /// <summary>
    /// DoctorRecommendationService — phân tích triệu chứng bệnh nhân và gợi ý bác sĩ TMH phù hợp nhất.
    ///
    /// Luồng hoạt động:
    ///   1. Nhận danh sách bác sĩ đang hoạt động từ AppointmentService
    ///   2. Gọi Anthropic Claude API với system prompt chứa thông tin bác sĩ + triệu chứng bệnh nhân
    ///   3. Parse JSON từ response của Claude → trả về DoctorRecommendationResponseDto
    ///   4. Nếu API không khả dụng (chưa cấu hình key / hết credit) → fallback keyword matching
    /// </summary>
    public class DoctorRecommendationService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<DoctorRecommendationService> _logger;

        // Hướng dẫn Claude trả về JSON thuần túy, không giải thích thêm
        private const string SYSTEM_PROMPT = @"
Bạn là trợ lý tư vấn của Phòng Khám Tai Mũi Họng (TMH).
Nhiệm vụ: dựa vào triệu chứng bệnh nhân và danh sách bác sĩ được cung cấp,
hãy gợi ý MỘT bác sĩ phù hợp nhất.

QUY TẮC BẮT BUỘC:
- Chỉ chọn bác sĩ có trong danh sách được cung cấp (dùng đúng doctorId)
- Trả lời CHÍNH XÁC theo định dạng JSON sau, KHÔNG thêm bất kỳ văn bản nào khác:
{""doctorId"": <số_nguyên>, ""reason"": ""<lý do tư vấn, 2-3 câu ngắn gọn bằng tiếng Việt>""}
- Ưu tiên chuyên môn phù hợp với triệu chứng trước; sau đó ưu tiên điểm đánh giá cao hơn
- Nếu triệu chứng không rõ ràng, chọn bác sĩ có điểm đánh giá cao nhất hoặc nhiều slot trống nhất
- KHÔNG chẩn đoán bệnh cụ thể, KHÔNG kê đơn thuốc
";

        // ===================================================================
        // FALLBACK — keyword mapping khi không có Anthropic API key
        // ===================================================================
        // Mỗi entry: (từ khóa triệu chứng) → (từ khóa khớp với chuyên khoa bác sĩ)
        private static readonly List<(string[] SymptomKeywords, string[] SpecialtyKeywords)> KeywordMap = new()
        {
            // Triệu chứng tai → bác sĩ thính học / tai giữa
            (
                new[] { "ù tai", "nghe kém", "điếc", "tai ù", "viêm tai", "đau tai", "chảy mủ tai",
                        "mất thính lực", "tiếng ồn trong tai", "ốc tai", "thính lực" },
                new[] { "thính", "ốc tai", "tai giữa", "thính học", "nghe", "cấy ốc" }
            ),
            // Triệu chứng mũi / xoang → bác sĩ nội soi xoang
            (
                new[] { "xoang", "nghẹt mũi", "viêm xoang", "polyp", "chảy mũi", "đau đầu mũi",
                        "chảy nước mũi", "nội soi mũi", "mũi", "viêm mũi", "chảy máu mũi" },
                new[] { "xoang", "nội soi xoang", "polyp", "mũi", "viêm mũi" }
            ),
            // Triệu chứng họng / thanh quản → bác sĩ họng / TMH tổng quát
            (
                new[] { "đau họng", "viêm họng", "amidan", "khàn tiếng", "khó nuốt", "ho",
                        "mất giọng", "thanh quản", "nuốt đau", "viêm amidan" },
                new[] { "họng", "amidan", "thanh quản", "khàn tiếng", "nuốt" }
            ),
            // Triệu chứng trẻ em → bác sĩ nhi TMH
            (
                new[] { "trẻ em", "trẻ nhỏ", "bé", "nhi", "con", "cháu", "em bé", "trẻ sơ sinh" },
                new[] { "nhi", "trẻ em", "viêm tai giữa trẻ" }
            ),
        };

        public DoctorRecommendationService(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<DoctorRecommendationService> logger)
        {
            _http   = httpClientFactory.CreateClient("AnthropicClient");
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Phân tích triệu chứng bệnh nhân và trả về gợi ý bác sĩ phù hợp nhất.
        /// </summary>
        /// <param name="symptoms">Triệu chứng do bệnh nhân mô tả</param>
        /// <param name="doctors">Danh sách bác sĩ đang hoạt động có lịch trống</param>
        public async Task<DoctorRecommendationResponseDto> RecommendAsync(
            string symptoms,
            List<DoctorScheduleDto> doctors)
        {
            if (string.IsNullOrWhiteSpace(symptoms))
                return Fail("Vui lòng mô tả triệu chứng để nhận gợi ý bác sĩ.");

            if (doctors == null || !doctors.Any())
                return Fail("Hiện không có bác sĩ nào sẵn sàng để gợi ý. Vui lòng thử lại sau.");

            var apiKey = _config["Anthropic:ApiKey"];

            // Không có API key → dùng fallback keyword matching
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogInformation("[DoctorRecommendation] Anthropic API key chưa cấu hình — dùng keyword fallback.");
                return GetKeywordFallback(symptoms, doctors);
            }

            return await CallClaudeApiAsync(symptoms, doctors, apiKey);
        }

        // ===================================================================
        // GỌI CLAUDE API
        // ===================================================================
        private async Task<DoctorRecommendationResponseDto> CallClaudeApiAsync(
            string symptoms,
            List<DoctorScheduleDto> doctors,
            string apiKey)
        {
            // Xây danh sách bác sĩ để đưa vào prompt
            var doctorListText = string.Join("\n", doctors.Select(d =>
            {
                var ratingText = d.ReviewCount > 0
                    ? $"Điểm đánh giá: {d.AverageRating:F1}/5 ({d.ReviewCount} lượt)"
                    : "Chưa có đánh giá";
                return $"- doctorId={d.DoctorId}, Tên: {d.FullName}, Bằng cấp: {d.Degree}, " +
                       $"Chuyên khoa: {d.Specialty}, Số lịch còn trống: {d.AvailableSlots.Count}, {ratingText}";
            }));

            var userMessage =
                $"Danh sách bác sĩ hiện có:\n{doctorListText}\n\n" +
                $"Triệu chứng bệnh nhân: {symptoms}\n\n" +
                $"Hãy gợi ý bác sĩ phù hợp nhất theo đúng định dạng JSON.";

            var body = new
            {
                model      = "claude-haiku-4-5-20251001",
                max_tokens = 256,
                system     = SYSTEM_PROMPT.Trim(),
                messages   = new[] { new { role = "user", content = userMessage } }
            };

            var json    = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            try
            {
                var response = await _http.PostAsync("https://api.anthropic.com/v1/messages", content);
                var raw      = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[DoctorRecommendation] Claude API lỗi {Status}: {Body}", response.StatusCode, raw);
                    return GetKeywordFallback(symptoms, doctors);
                }

                // Lấy text từ response
                using var doc  = JsonDocument.Parse(raw);
                var replyText  = doc.RootElement
                                    .GetProperty("content")[0]
                                    .GetProperty("text")
                                    .GetString() ?? "";

                return ParseClaudeResponse(replyText, doctors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DoctorRecommendation] Lỗi gọi Claude API");
                return GetKeywordFallback(symptoms, doctors);
            }
        }

        // ===================================================================
        // PARSE JSON TỪ CLAUDE
        // ===================================================================
        private DoctorRecommendationResponseDto ParseClaudeResponse(
            string replyText,
            List<DoctorScheduleDto> doctors)
        {
            try
            {
                // Trích xuất JSON từ text (Claude đôi khi thêm text xung quanh)
                var match = Regex.Match(replyText, @"\{[^{}]*""doctorId""[^{}]*\}", RegexOptions.Singleline);
                var jsonStr = match.Success ? match.Value : replyText.Trim();

                using var parsed = JsonDocument.Parse(jsonStr);
                var root         = parsed.RootElement;

                var doctorId = root.GetProperty("doctorId").GetInt32();
                var reason   = root.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";

                // Tìm bác sĩ trong danh sách
                var doctor = doctors.FirstOrDefault(d => d.DoctorId == doctorId);
                if (doctor == null)
                {
                    _logger.LogWarning("[DoctorRecommendation] Claude trả về doctorId={Id} không tồn tại trong danh sách.", doctorId);
                    // Lấy bác sĩ đầu tiên làm fallback
                    doctor = doctors.First();
                    reason = $"Chúng tôi gợi ý {doctor.FullName} — {doctor.Specialty}.";
                }

                return new DoctorRecommendationResponseDto
                {
                    Success              = true,
                    Message              = "Gợi ý bác sĩ thành công.",
                    RecommendedDoctorId  = doctor.DoctorId,
                    DoctorName           = doctor.FullName,
                    Specialty            = doctor.Specialty,
                    Degree               = doctor.Degree,
                    Reason               = reason
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DoctorRecommendation] Lỗi parse JSON từ Claude: {Text}", replyText);
                // Trả về bác sĩ đầu tiên với thông báo chung
                var fallback = doctors.First();
                return new DoctorRecommendationResponseDto
                {
                    Success             = true,
                    Message             = "Gợi ý bác sĩ thành công.",
                    RecommendedDoctorId = fallback.DoctorId,
                    DoctorName          = fallback.FullName,
                    Specialty           = fallback.Specialty,
                    Degree              = fallback.Degree,
                    Reason              = $"Dựa trên triệu chứng của bạn, {fallback.FullName} là bác sĩ phù hợp để thăm khám."
                };
            }
        }

        // ===================================================================
        // FALLBACK — KEYWORD MATCHING (không cần API)
        // ===================================================================
        private static DoctorRecommendationResponseDto GetKeywordFallback(
            string symptoms,
            List<DoctorScheduleDto> doctors)
        {
            var lowerSymptoms = NormalizeVn(symptoms);
            DoctorScheduleDto? bestDoctor = null;
            string reason = "";

            // Duyệt qua bảng keyword, tìm nhóm triệu chứng khớp đầu tiên
            foreach (var (symptomKws, specialtyKws) in KeywordMap)
            {
                bool matchSymptom = symptomKws.Any(kw => lowerSymptoms.Contains(NormalizeVn(kw)));
                if (!matchSymptom) continue;

                // Tìm bác sĩ có chuyên khoa khớp với nhóm triệu chứng này
                bestDoctor = doctors.FirstOrDefault(d =>
                    specialtyKws.Any(skw =>
                        NormalizeVn(d.Specialty ?? "").Contains(NormalizeVn(skw)) ||
                        NormalizeVn(d.Degree    ?? "").Contains(NormalizeVn(skw))));

                if (bestDoctor != null)
                {
                    reason = BuildFallbackReason(bestDoctor, symptoms);
                    break;
                }
            }

            // Nếu không khớp keyword nào → chọn bác sĩ có nhiều slot trống nhất
            if (bestDoctor == null)
            {
                bestDoctor = doctors.OrderByDescending(d => d.AvailableSlots.Count).First();
                reason = $"Dựa trên mô tả của bạn, {bestDoctor.FullName} ({bestDoctor.Specialty}) " +
                         $"là bác sĩ phù hợp để thăm khám và tư vấn trực tiếp. " +
                         $"Hiện bác sĩ còn {bestDoctor.AvailableSlots.Count} lịch trống trong thời gian tới.";
            }

            return new DoctorRecommendationResponseDto
            {
                Success             = true,
                Message             = "Gợi ý bác sĩ thành công.",
                RecommendedDoctorId = bestDoctor.DoctorId,
                DoctorName          = bestDoctor.FullName,
                Specialty           = bestDoctor.Specialty,
                Degree              = bestDoctor.Degree,
                Reason              = reason
            };
        }

        private static string BuildFallbackReason(DoctorScheduleDto doctor, string symptoms)
        {
            return $"Với triệu chứng bạn mô tả, {doctor.FullName} — chuyên gia về {doctor.Specialty} — " +
                   $"là lựa chọn phù hợp nhất. " +
                   $"Bác sĩ có kinh nghiệm chuyên sâu trong lĩnh vực này và hiện còn " +
                   $"{doctor.AvailableSlots.Count} lịch khám trống. " +
                   $"Bạn nên đặt lịch sớm để được tư vấn và điều trị kịp thời.";
        }

        // Chuẩn hóa tiếng Việt (bỏ dấu, lowercase) để so sánh keyword
        private static string NormalizeVn(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.ToLower()
                .Replace("à","a").Replace("á","a").Replace("ả","a").Replace("ã","a").Replace("ạ","a")
                .Replace("ă","a").Replace("ắ","a").Replace("ặ","a").Replace("ằ","a").Replace("ẳ","a").Replace("ẵ","a")
                .Replace("â","a").Replace("ấ","a").Replace("ầ","a").Replace("ẩ","a").Replace("ẫ","a").Replace("ậ","a")
                .Replace("è","e").Replace("é","e").Replace("ẻ","e").Replace("ẽ","e").Replace("ẹ","e")
                .Replace("ê","e").Replace("ế","e").Replace("ề","e").Replace("ể","e").Replace("ễ","e").Replace("ệ","e")
                .Replace("ì","i").Replace("í","i").Replace("ỉ","i").Replace("ĩ","i").Replace("ị","i")
                .Replace("ò","o").Replace("ó","o").Replace("ỏ","o").Replace("õ","o").Replace("ọ","o")
                .Replace("ô","o").Replace("ố","o").Replace("ồ","o").Replace("ổ","o").Replace("ỗ","o").Replace("ộ","o")
                .Replace("ơ","o").Replace("ớ","o").Replace("ờ","o").Replace("ở","o").Replace("ỡ","o").Replace("ợ","o")
                .Replace("ù","u").Replace("ú","u").Replace("ủ","u").Replace("ũ","u").Replace("ụ","u")
                .Replace("ư","u").Replace("ứ","u").Replace("ừ","u").Replace("ử","u").Replace("ữ","u").Replace("ự","u")
                .Replace("ỳ","y").Replace("ý","y").Replace("ỷ","y").Replace("ỹ","y").Replace("ỵ","y")
                .Replace("đ","d");
        }

        private static DoctorRecommendationResponseDto Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
