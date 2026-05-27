using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TMH.Shared.DTOs;

namespace TMH.API.Services
{
    /// <summary>
    /// DoctorRecommendationService — gợi ý bác sĩ TMH phù hợp nhất dựa trên triệu chứng.
    ///
    /// Phòng khám không phân khoa → gợi ý theo tổ hợp 4 tiêu chí:
    ///   1. Điểm đánh giá (Rating)          — trọng số 40%
    ///   2. Kinh nghiệm & Bằng cấp          — trọng số 30%
    ///   3. Số lịch khám còn trống          — trọng số 20%
    ///   4. Khớp triệu chứng với Description — trọng số 10%
    ///
    /// Luồng hoạt động:
    ///   1. Nhận danh sách bác sĩ đang hoạt động từ AppointmentService
    ///   2. Gắn điểm đánh giá vào danh sách (từ controller)
    ///   3. Gọi Anthropic Claude API với system prompt mới (không dùng Specialty)
    ///   4. Nếu API không khả dụng → fallback scoring nội bộ
    /// </summary>
    public class DoctorRecommendationService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<DoctorRecommendationService> _logger;

        // ===================================================================
        // SYSTEM PROMPT — không dùng Specialty vì phòng khám không phân khoa
        // ===================================================================
        private const string SYSTEM_PROMPT = @"
Bạn là trợ lý tư vấn của Phòng Khám Tai Mũi Họng (TMH).
Phòng khám KHÔNG phân chia khoa riêng — tất cả bác sĩ đều khám Tai Mũi Họng tổng quát.
Nhiệm vụ: dựa vào triệu chứng bệnh nhân và danh sách bác sĩ được cung cấp,
hãy gợi ý MỘT bác sĩ phù hợp nhất.

TIÊU CHÍ GỢI Ý (theo thứ tự ưu tiên):
1. Điểm đánh giá cao và nhiều lượt đánh giá (quan trọng nhất)
2. Bằng cấp và số năm kinh nghiệm (trích từ Description)
   - Thứ tự bằng cấp: PGS > TS > CKII > ThS > BS
3. Số lịch khám còn trống (ưu tiên bác sĩ còn nhiều slot)
4. Mô tả (Description) của bác sĩ có liên quan đến triệu chứng bệnh nhân

QUY TẮC BẮT BUỘC:
- Chỉ chọn bác sĩ có trong danh sách được cung cấp (dùng đúng doctorId)
- Trả lời CHÍNH XÁC theo định dạng JSON sau, KHÔNG thêm bất kỳ văn bản nào khác:
{""doctorId"": <số_nguyên>, ""reason"": ""<lý do tư vấn, 2-3 câu ngắn gọn bằng tiếng Việt>""}
- Lý do cần đề cập: điểm đánh giá, số năm kinh nghiệm, và liên quan đến triệu chứng (nếu có)
- KHÔNG chẩn đoán bệnh cụ thể, KHÔNG kê đơn thuốc
- Nếu không có bác sĩ nào liên quan rõ ràng đến triệu chứng, chọn bác sĩ có điểm đánh giá cao nhất
";

        // ===================================================================
        // BẢNG BẬC BẰNG CẤP — dùng cho fallback scoring
        // ===================================================================
        private static readonly Dictionary<string, int> DegreeScore = new(StringComparer.OrdinalIgnoreCase)
        {
            { "PGS", 5 },
            { "GS",  6 },
            { "TS",  4 },
            { "CKII",3 },
            { "ThS", 2 },
            { "CKI", 2 },
            { "BS",  1 },
        };

        // ===================================================================
        // TỪ KHÓA TRIỆU CHỨNG — match với Description bác sĩ (không dùng Specialty)
        // ===================================================================
        private static readonly List<string[]> SymptomKeywords = new()
        {
            // Tai / thính lực
            new[] { "ù tai", "nghe kém", "điếc", "tai ù", "viêm tai", "đau tai",
                    "chảy mủ tai", "mất thính lực", "tiếng ồn trong tai", "ốc tai",
                    "thính lực", "thính học", "tai giữa" },
            // Mũi / xoang
            new[] { "xoang", "nghẹt mũi", "viêm xoang", "polyp", "chảy mũi",
                    "đau đầu mũi", "chảy nước mũi", "nội soi mũi", "mũi",
                    "viêm mũi", "chảy máu mũi" },
            // Họng / thanh quản
            new[] { "đau họng", "viêm họng", "amidan", "khàn tiếng", "khó nuốt",
                    "ho", "mất giọng", "thanh quản", "nuốt đau", "viêm amidan" },
            // Trẻ em / nhi
            new[] { "trẻ em", "trẻ nhỏ", "bé", "nhi", "con", "cháu",
                    "em bé", "trẻ sơ sinh", "viêm tai giữa trẻ" },
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

        // ===================================================================
        // ENTRY POINT
        // ===================================================================
        public async Task<DoctorRecommendationResponseDto> RecommendAsync(
            string symptoms,
            List<DoctorScheduleDto> doctors)
        {
            if (string.IsNullOrWhiteSpace(symptoms))
                return Fail("Vui lòng mô tả triệu chứng để nhận gợi ý bác sĩ.");

            if (doctors == null || !doctors.Any())
                return Fail("Hiện không có bác sĩ nào sẵn sàng để gợi ý. Vui lòng thử lại sau.");

            var apiKey = _config["Anthropic:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogInformation("[DoctorRecommendation] Anthropic API key chưa cấu hình — dùng scoring fallback.");
                return GetScoringFallback(symptoms, doctors);
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
            // Xây danh sách bác sĩ đưa vào prompt — bao gồm Description để AI match triệu chứng
            var doctorListText = string.Join("\n", doctors.Select(d =>
            {
                var ratingText = d.ReviewCount > 0
                    ? $"Đánh giá: {d.AverageRating:F1}/5 ({d.ReviewCount} lượt)"
                    : "Chưa có đánh giá";
                return $"- doctorId={d.DoctorId}, Tên: {d.FullName}, Bằng cấp: {d.Degree ?? "BS"}, " +
                       $"Mô tả: {d.Description ?? "(chưa có)"}, " +
                       $"Lịch còn trống: {d.AvailableSlots.Count} slot, {ratingText}";
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
                    return GetScoringFallback(symptoms, doctors);
                }

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
                return GetScoringFallback(symptoms, doctors);
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
                var match  = Regex.Match(replyText, @"\{[^{}]*""doctorId""[^{}]*\}", RegexOptions.Singleline);
                var jsonStr = match.Success ? match.Value : replyText.Trim();

                using var parsed = JsonDocument.Parse(jsonStr);
                var root         = parsed.RootElement;

                var doctorId = root.GetProperty("doctorId").GetInt32();
                var reason   = root.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";

                var doctor = doctors.FirstOrDefault(d => d.DoctorId == doctorId);
                if (doctor == null)
                {
                    _logger.LogWarning("[DoctorRecommendation] Claude trả về doctorId={Id} không hợp lệ.", doctorId);
                    return GetScoringFallback("", doctors); // fallback an toàn
                }

                return BuildResponse(doctor, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DoctorRecommendation] Lỗi parse JSON từ Claude: {Text}", replyText);
                return GetScoringFallback("", doctors);
            }
        }

        // ===================================================================
        // FALLBACK — SCORING NỘI BỘ (không cần API)
        //
        // Tính điểm cho mỗi bác sĩ theo 4 tiêu chí, chọn bác sĩ điểm cao nhất.
        // ===================================================================
        private DoctorRecommendationResponseDto GetScoringFallback(
            string symptoms,
            List<DoctorScheduleDto> doctors)
        {
            var lowerSymptoms = NormalizeVn(symptoms);

            var scored = doctors.Select(d =>
            {
                // ── Tiêu chí 1: Rating (0–40 điểm) ──────────────────────
                // Rating 5.0 + nhiều lượt → 40đ; chưa có rating → 20đ (trung lập)
                double ratingScore;
                if (d.ReviewCount == 0)
                    ratingScore = 20.0; // trung lập khi chưa có đánh giá
                else
                {
                    // Rating càng cao + càng nhiều lượt càng đáng tin
                    var confidence = Math.Min(d.ReviewCount / 10.0, 1.0); // bão hòa ở 10 lượt
                    ratingScore = d.AverageRating / 5.0 * 40.0 * (0.6 + 0.4 * confidence);
                }

                // ── Tiêu chí 2: Kinh nghiệm & Bằng cấp (0–30 điểm) ─────
                // Bằng cấp: tối đa 15đ
                var degreeScore = 0;
                if (d.Degree != null)
                    DegreeScore.TryGetValue(d.Degree.Trim(), out degreeScore);
                var degreePoints = degreeScore / 6.0 * 15.0;

                // Số năm kinh nghiệm: tối đa 15đ (bão hòa ở 20 năm)
                var yearMatch   = Regex.Match(d.Description ?? "", @"(\d+)\s*năm");
                var years       = yearMatch.Success ? int.Parse(yearMatch.Groups[1].Value) : 0;
                var expPoints   = Math.Min(years / 20.0, 1.0) * 15.0;

                var experienceScore = degreePoints + expPoints;

                // ── Tiêu chí 3: Lịch trống (0–20 điểm) ──────────────────
                // Bão hòa ở 10 slot trở lên
                var slotScore = Math.Min(d.AvailableSlots.Count / 10.0, 1.0) * 20.0;

                // ── Tiêu chí 4: Khớp triệu chứng với Description (0–10đ) ─
                var matchScore = 0.0;
                if (!string.IsNullOrWhiteSpace(symptoms))
                {
                    var descNorm = NormalizeVn(d.Description ?? "");
                    var anyGroupMatches = SymptomKeywords.Any(group =>
                        group.Any(kw => lowerSymptoms.Contains(NormalizeVn(kw))) &&
                        group.Any(kw => descNorm.Contains(NormalizeVn(kw)))
                    );
                    if (anyGroupMatches) matchScore = 10.0;
                    else
                    {
                        // Kiểm tra khớp từng từ riêng lẻ trong Description
                        var symptomWords = lowerSymptoms.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var descWords    = descNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var overlap      = symptomWords.Intersect(descWords).Count();
                        matchScore = Math.Min(overlap * 2.0, 5.0); // tối đa 5đ nếu không có group match
                    }
                }

                var total = ratingScore + experienceScore + slotScore + matchScore;

                return new { Doctor = d, Total = total, RatingScore = ratingScore,
                             ExperienceScore = experienceScore, SlotScore = slotScore,
                             MatchScore = matchScore };
            })
            .OrderByDescending(x => x.Total)
            .ToList();

            var best   = scored.First();
            var doctor = best.Doctor;
            var reason = BuildFallbackReason(doctor, symptoms, best.MatchScore > 0);

            return BuildResponse(doctor, reason);
        }

        // ===================================================================
        // HELPERS
        // ===================================================================
        private static string BuildFallbackReason(DoctorScheduleDto doctor, string symptoms, bool hasSymptomMatch)
        {
            var parts = new List<string>();

            // Đề cập kinh nghiệm nếu có
            var yearMatch = Regex.Match(doctor.Description ?? "", @"(\d+)\s*năm");
            if (yearMatch.Success)
                parts.Add($"với {yearMatch.Value} kinh nghiệm");

            // Đề cập rating nếu có
            if (doctor.ReviewCount > 0)
                parts.Add($"được đánh giá {doctor.AverageRating:F1}/5 sao ({doctor.ReviewCount} lượt)");

            // Đề cập slot
            if (doctor.AvailableSlots.Count > 0)
                parts.Add($"hiện còn {doctor.AvailableSlots.Count} lịch khám trống");

            var detail = parts.Any() ? string.Join(", ", parts) + ". " : "";

            var matchNote = hasSymptomMatch
                ? $"Mô tả chuyên môn của bác sĩ phù hợp với triệu chứng bạn mô tả. "
                : "";

            return $"Dựa trên triệu chứng của bạn, chúng tôi gợi ý {doctor.FullName} ({doctor.Degree}). " +
                   $"{detail}{matchNote}" +
                   $"Bạn nên đặt lịch sớm để được tư vấn và điều trị kịp thời.";
        }

        private static DoctorRecommendationResponseDto BuildResponse(DoctorScheduleDto doctor, string reason) =>
            new()
            {
                Success             = true,
                Message             = "Gợi ý bác sĩ thành công.",
                RecommendedDoctorId = doctor.DoctorId,
                DoctorName          = doctor.FullName,
                Specialty           = "Tai Mũi Họng",
                Degree              = doctor.Degree ?? "BS",
                Reason              = reason
            };

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
