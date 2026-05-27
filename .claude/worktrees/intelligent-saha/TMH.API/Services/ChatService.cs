// Chat Call API Claude có trả phí
//using System.Text;
//using System.Text.Json;

//namespace TMH.API.Services
//{
//    /// <summary>
//    /// ChatService gọi Anthropic API (claude-haiku) để trả lời câu hỏi
//    /// của bệnh nhân về phòng khám Tai Mũi Họng.
//    ///
//    /// System prompt giới hạn chatbot chỉ tư vấn trong phạm vi:
//    ///   - Triệu chứng tai mũi họng
//    ///   - Hướng dẫn đặt lịch, quy trình khám
//    ///   - Thông tin phòng khám (giờ làm việc, địa chỉ)
//    /// </summary>
//    public class ChatService
//    {
//        private readonly HttpClient _http;
//        private readonly IConfiguration _config;
//        private readonly ILogger<ChatService> _logger;

//        private const string SYSTEM_PROMPT = @"
//Bạn là trợ lý ảo của Phòng Khám Tai Mũi Họng (TMH). Nhiệm vụ của bạn là hỗ trợ bệnh nhân với các vấn đề sau:

//1. TƯ VẤN TRIỆU CHỨNG TAI MŨI HỌNG:
//   - Tai: ù tai, nghe kém, viêm tai giữa, đau tai, chảy mủ tai
//   - Mũi: viêm xoang, nghẹt mũi, chảy máu mũi, polyp mũi, dị ứng
//   - Họng: viêm amidan, đau họng, khàn tiếng, khó nuốt, ho

//2. HƯỚNG DẪN ĐẶT LỊCH KHÁM:
//   - Bệnh nhân đăng nhập → vào mục Đặt lịch → chọn bác sĩ → chọn khung giờ → xác nhận
//   - Có thể đặt lịch cho người thân bằng cách thêm hồ sơ mới

//3. THÔNG TIN PHÒNG KHÁM:
//   - Giờ làm việc: Thứ 2 – Thứ 6: 7:00 – 17:00, Thứ 7: 7:00 – 12:00
//   - Địa chỉ: 123 Đường Giải Phóng, Hà Nội
//   - Hotline: 1900 xxxx
//   - Phí khám: 100.000 VNĐ/lượt

//4. QUY TẮC TRẢ LỜI:
//   - Chỉ trả lời về tai mũi họng và phòng khám, từ chối lịch sự các chủ đề khác
//   - Không chẩn đoán bệnh cụ thể — chỉ gợi ý triệu chứng và khuyên khám trực tiếp
//   - Trả lời ngắn gọn, thân thiện, dùng tiếng Việt
//   - Nếu triệu chứng nghiêm trọng (khó thở, chảy máu nhiều), khuyên đến cấp cứu ngay
//";

//        public ChatService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<ChatService> logger)
//        {
//            _http = httpClientFactory.CreateClient("AnthropicClient");
//            _config = config;
//            _logger = logger;
//        }

//        public async Task<string> AskAsync(string userMessage, List<ChatMessageDto>? history = null)
//        {
//            var apiKey = _config["Anthropic:ApiKey"];
//            if (string.IsNullOrWhiteSpace(apiKey))
//                return "Chatbot chưa được cấu hình. Vui lòng liên hệ quản trị viên.";

//            // Xây messages: lịch sử + tin nhắn mới
//            var messages = new List<object>();
//            if (history != null)
//            {
//                foreach (var h in history.TakeLast(10)) // tối đa 10 tin trước
//                {
//                    messages.Add(new { role = h.Role, content = h.Content });
//                }
//            }
//            messages.Add(new { role = "user", content = userMessage });

//            var body = new
//            {
//                model = "claude-haiku-4-5-20251001",
//                max_tokens = 512,
//                system = SYSTEM_PROMPT.Trim(),
//                messages
//            };

//            var json = JsonSerializer.Serialize(body);
//            var content = new StringContent(json, Encoding.UTF8, "application/json");

//            _http.DefaultRequestHeaders.Clear();
//            _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
//            _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

//            try
//            {
//                var response = await _http.PostAsync("https://api.anthropic.com/v1/messages", content);
//                var raw = await response.Content.ReadAsStringAsync();

//                if (!response.IsSuccessStatusCode)
//                {
//                    _logger.LogError("Anthropic API lỗi {Status}: {Body}", response.StatusCode, raw);
//                    Console.WriteLine($"=== ANTHROPIC ERROR: {response.StatusCode} | {raw} ===");
//                    return "Xin lỗi, tôi đang gặp sự cố. Vui lòng thử lại sau.";
//                }

//                using var doc = JsonDocument.Parse(raw);
//                return doc.RootElement
//                          .GetProperty("content")[0]
//                          .GetProperty("text")
//                          .GetString() ?? "Không có phản hồi.";
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Lỗi gọi Anthropic API");

//                return "Xin lỗi, tôi đang gặp sự cố kết nối. Vui lòng thử lại.";
//            }
//        }
//    }

//    /// <summary>DTO cho một tin nhắn trong lịch sử hội thoại.</summary>
//    public class ChatMessageDto
//    {
//        public string Role { get; set; } = "user";   // "user" | "assistant"
//        public string Content { get; set; } = string.Empty;
//    }

//    /// <summary>Request body bệnh nhân gửi lên endpoint /api/chat.</summary>
//    public class ChatRequestDto
//    {
//        public string Message { get; set; } = string.Empty;
//        public List<ChatMessageDto>? History { get; set; }
//    }
//}
using System.Text;
using System.Text.Json;

namespace TMH.API.Services
{
    public class ChatService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<ChatService> _logger;

        private const string SYSTEM_PROMPT = @"
Bạn là trợ lý ảo của Phòng Khám Tai Mũi Họng (TMH). Nhiệm vụ của bạn là hỗ trợ bệnh nhân với các vấn đề sau:

1. TƯ VẤN TRIỆU CHỨNG TAI MŨI HỌNG:
   - Tai: ù tai, nghe kém, viêm tai giữa, đau tai, chảy mủ tai
   - Mũi: viêm xoang, nghẹt mũi, chảy máu mũi, polyp mũi, dị ứng
   - Họng: viêm amidan, đau họng, khàn tiếng, khó nuốt, ho

2. HƯỚNG DẪN ĐẶT LỊCH KHÁM:
   - Bệnh nhân đăng nhập → vào mục Đặt lịch → chọn bác sĩ → chọn khung giờ → xác nhận
   - Có thể đặt lịch cho người thân bằng cách thêm hồ sơ mới

3. THÔNG TIN PHÒNG KHÁM:
   - Giờ làm việc: Thứ 2 – Thứ 6: 7:00 – 17:00, Thứ 7: 7:00 – 12:00
   - Địa chỉ: 123 Đường Giải Phóng, Hà Nội
   - Hotline: 1900 xxxx
   - Phí khám: 100.000 VNĐ/lượt

4. QUY TẮC TRẢ LỜI:
   - Chỉ trả lời về tai mũi họng và phòng khám, từ chối lịch sự các chủ đề khác
   - Không chẩn đoán bệnh cụ thể — chỉ gợi ý triệu chứng và khuyên khám trực tiếp
   - Trả lời ngắn gọn, thân thiện, dùng tiếng Việt
   - Nếu triệu chứng nghiêm trọng (khó thở, chảy máu nhiều), khuyên đến cấp cứu ngay
";

        // =====================================================================
        // MOCK RESPONSES — dùng khi chưa có Anthropic credit
        // Mỗi entry: keywords khớp → reply tương ứng
        // =====================================================================
        private static readonly List<(string[] Keywords, string Reply)> MockReplies = new()
        {
            (
                new[] { "giờ", "mở cửa", "làm việc", "thời gian" },
                "Phòng khám mở cửa:\n• Thứ 2 – Thứ 6: 7:00 – 17:00\n• Thứ 7: 7:00 – 12:00\n• Chủ nhật: Nghỉ\n\nBạn có thể đặt lịch trực tuyến 24/7 trên hệ thống này."
            ),
            (
                new[] { "địa chỉ", "ở đâu", "đường", "vị trí" },
                "Phòng khám Tai Mũi Họng tọa lạc tại:\n📍 123 Đường Giải Phóng, Hà Nội\n\nHotline hỗ trợ: 1900 6868"
            ),
            (
                new[] { "phí", "giá", "tiền", "bao nhiêu", "chi phí" },
                "Phí khám cơ bản: 100.000 VNĐ/lượt.\n\nMột số dịch vụ chuyên sâu (nội soi, đo thính lực...) có phụ phí riêng — bác sĩ sẽ tư vấn cụ thể sau khi thăm khám."
            ),
            (
                new[] { "đặt lịch", "đặt khám", "book", "hẹn khám" },
                "Để đặt lịch khám, bạn làm theo các bước:\n1. Đăng nhập tài khoản\n2. Vào mục **Đặt lịch khám**\n3. Chọn loại khám (Tai / Mũi / Họng)\n4. Chọn bác sĩ và khung giờ phù hợp\n5. Xác nhận — hệ thống sẽ gửi thông báo ngay"
            ),
            (
                new[] { "ù tai", "nghe kém", "điếc", "tai ù", "tiếng ồn trong tai" },
                "Ù tai, nghe kém có thể do nhiều nguyên nhân: viêm tai giữa, tắc ráy tai, tổn thương thần kinh thính giác hoặc tiếp xúc tiếng ồn lớn.\n\n⚠️ Nếu tình trạng kéo dài trên 2 ngày hoặc đi kèm chóng mặt, bạn nên đến khám sớm. Bác sĩ của chúng tôi chuyên về thính học & tai giữa, có thể hỗ trợ bạn."
            ),
            (
                new[] { "đau tai", "viêm tai", "chảy mủ tai", "tai chảy" },
                "Đau tai và chảy mủ thường là dấu hiệu viêm tai giữa cấp — cần được điều trị sớm để tránh biến chứng. Bạn nên đặt lịch khám trong ngày hôm nay hoặc ngày mai. Tôi có thể hướng dẫn bạn đặt lịch ngay bây giờ nếu cần."
            ),
            (
                new[] { "viêm xoang", "nghẹt mũi", "xoang", "đau đầu mũi" },
                "Viêm xoang thường gây nghẹt mũi, đau vùng mặt, đau đầu và chảy dịch mũi sau. Nếu triệu chứng kéo dài trên 10 ngày hoặc tái phát nhiều lần, đây có thể là viêm xoang mãn tính cần điều trị chuyên sâu.\n\nPhòng khám có dịch vụ nội soi mũi xoang và điều trị polyp mũi — bạn có muốn đặt lịch không?"
            ),
            (
                new[] { "chảy máu mũi", "chảy máu mũi", "máu mũi" },
                "Chảy máu mũi thường do niêm mạc mũi khô, va chạm hoặc tăng huyết áp. Sơ cứu: ngồi thẳng, cúi nhẹ đầu về phía trước, bóp nhẹ cánh mũi 10 phút.\n\n⚠️ Nếu chảy máu nhiều, không cầm được sau 20 phút hoặc tái phát thường xuyên, hãy đến cấp cứu ngay."
            ),
            (
                new[] { "viêm amidan", "amidan", "đau họng", "khó nuốt", "nuốt đau" },
                "Viêm amidan gây đau họng, khó nuốt và đôi khi sốt cao. Nếu tái phát trên 5 lần/năm, bác sĩ có thể tư vấn phẫu thuật cắt amidan.\n\nPhòng khám có chuyên khoa Họng — bạn có muốn đặt lịch để được thăm khám không?"
            ),
            (
                new[] { "khàn tiếng", "mất giọng", "giọng khàn", "khàn họng" },
                "Khàn tiếng thường do viêm thanh quản, căng dây thanh hoặc trào ngược dạ dày. Nếu tình trạng kéo dài trên 2 tuần, cần nội soi thanh quản để loại trừ các bệnh lý nghiêm trọng hơn.\n\nBạn muốn đặt lịch khám không?"
            ),
            (
                new[] { "bác sĩ", "chuyên gia", "chuyên khoa" },
                "Phòng khám hiện có các bác sĩ chuyên khoa:\n• TS.BS. Nguyễn Thanh Vân — Phẫu thuật nội soi xoang\n• PGS.BS. Trần Minh Linh — Thính học & cấy ốc tai\n• TS.BS. Phạm Thị Hương — TMH Nhi, viêm tai giữa trẻ em\n\nBạn muốn đặt lịch với bác sĩ nào?"
            ),
            (
                new[] { "cảm ơn", "thanks", "thank", "tốt", "được rồi" },
                "Rất vui được hỗ trợ bạn! Nếu có thêm câu hỏi về sức khỏe tai mũi họng hoặc cần hỗ trợ đặt lịch, tôi luôn sẵn sàng. Chúc bạn sức khỏe! 😊"
            ),
        };

        public ChatService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<ChatService> logger)
        {
            _http = httpClientFactory.CreateClient("AnthropicClient");
            _config = config;
            _logger = logger;
        }

        public async Task<string> AskAsync(string userMessage, List<ChatMessageDto>? history = null)
        {
            var apiKey = _config["Anthropic:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return GetMockReply(userMessage);

            // Xây messages: lịch sử + tin nhắn mới
            var messages = new List<object>();
            if (history != null)
                foreach (var h in history.TakeLast(10))
                    messages.Add(new { role = h.Role, content = h.Content });

            messages.Add(new { role = "user", content = userMessage });

            var body = new
            {
                model = "claude-haiku-4-5-20251001",
                max_tokens = 512,
                system = SYSTEM_PROMPT.Trim(),
                messages
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            try
            {
                var response = await _http.PostAsync("https://api.anthropic.com/v1/messages", content);
                var raw = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Anthropic API lỗi {Status}: {Body}", response.StatusCode, raw);
                    // Fallback sang mock khi API lỗi (hết credit, rate limit...)
                    return GetMockReply(userMessage);
                }

                using var doc = JsonDocument.Parse(raw);
                return doc.RootElement
                          .GetProperty("content")[0]
                          .GetProperty("text")
                          .GetString() ?? GetMockReply(userMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gọi Anthropic API");
                return GetMockReply(userMessage);
            }
        }

        // =====================================================================
        // MOCK ENGINE — tìm reply phù hợp nhất theo keyword
        // =====================================================================
        private static string GetMockReply(string message)
        {
            var lower = message.ToLower();

            foreach (var (keywords, reply) in MockReplies)
                if (keywords.Any(kw => lower.Contains(kw)))
                    return reply;

            // Fallback mặc định
            return "Xin chào! Tôi là trợ lý của Phòng Khám Tai Mũi Họng. Tôi có thể giúp bạn về:\n" +
                   "• Triệu chứng tai, mũi, họng\n" +
                   "• Đặt lịch khám\n" +
                   "• Thông tin phòng khám (giờ mở cửa, địa chỉ, phí khám)\n\n" +
                   "Bạn cần hỗ trợ gì?";
        }
    }

    public class ChatMessageDto
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }

    public class ChatRequestDto
    {
        public string Message { get; set; } = string.Empty;
        public List<ChatMessageDto>? History { get; set; }
    }
}