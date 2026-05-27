using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TMH.Web.Controllers
{
    /// <summary>
    /// Proxy mọi request chat/message từ frontend → TMH.API.
    /// Kèm endpoint /api/msgs/token để JS lấy JWT cho SignalR.
    /// </summary>
    [Route("api/msgs")]
    [ApiController]
    public class MessageProxyController : ControllerBase
    {
        private readonly IHttpClientFactory _factory;
        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _ctx;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public MessageProxyController(IHttpClientFactory factory,
                                      IConfiguration config,
                                      IHttpContextAccessor ctx)
        {
            _factory = factory;
            _config  = config;
            _ctx     = ctx;
        }

        // ── Cấp JWT cho SignalR JS client ─────────────────────────
        // GET /api/msgs/token
        [HttpGet("token")]
        public IActionResult GetToken()
        {
            var token = _ctx.HttpContext?.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return Unauthorized();
            return Ok(new { token });
        }

        // ── Danh sách conversations ────────────────────────────────
        // GET /api/msgs/conversations
        [HttpGet("conversations")]
        public Task<IActionResult> GetConversations()
            => ProxyGet("api/messages/conversations");

        // ── Messages của 1 conversation ──────────────────────────
        // GET /api/msgs/conversations/{id}/messages
        [HttpGet("conversations/{id:int}/messages")]
        public Task<IActionResult> GetMessages(int id, [FromQuery] int skip = 0, [FromQuery] int take = 50)
            => ProxyGet($"api/messages/conversations/{id}/messages?skip={skip}&take={take}");

        // ── Bệnh nhân tạo/lấy conv với lễ tân ─────────────────────
        // POST /api/msgs/conversations/patient-staff
        [HttpPost("conversations/patient-staff")]
        public Task<IActionResult> StartPatientStaff()
            => ProxyPost("api/messages/conversations/patient-staff", null);

        // ── Tạo conv nội bộ ───────────────────────────────────────
        // POST /api/msgs/conversations/internal
        [HttpPost("conversations/internal")]
        public async Task<IActionResult> StartInternal([FromBody] JsonElement body)
            => await ProxyPost("api/messages/conversations/internal", body);

        // ── Gửi tin nhắn (REST fallback khi SignalR không dùng được) ─
        // POST /api/msgs/conversations/{id}/send
        [HttpPost("conversations/{id:int}/send")]
        public async Task<IActionResult> SendMessage(int id, [FromBody] JsonElement body)
            => await ProxyPost($"api/messages/conversations/{id}/send", body);

        // ── Đánh dấu đã đọc ───────────────────────────────────────
        // PUT /api/msgs/conversations/{id}/read
        [HttpPut("conversations/{id:int}/read")]
        public Task<IActionResult> MarkRead(int id)
            => ProxyPut($"api/messages/conversations/{id}/read");

        // ── Tổng unread ───────────────────────────────────────────
        // GET /api/msgs/unread
        [HttpGet("unread")]
        public Task<IActionResult> GetUnread()
            => ProxyGet("api/messages/unread");

        // ── Danh sách user nội bộ ─────────────────────────────────
        // GET /api/msgs/internal-users
        [HttpGet("internal-users")]
        public Task<IActionResult> GetInternalUsers()
            => ProxyGet("api/messages/internal-users");

        // ──────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────
        private async Task<IActionResult> ProxyGet(string apiPath)
        {
            var client = BuildClient();
            if (client == null) return Unauthorized();
            try
            {
                var res = await client.GetAsync(apiPath);
                var body = await res.Content.ReadAsStringAsync();
                return Content(body, "application/json");
            }
            catch { return StatusCode(502, new { error = "Không kết nối được đến API." }); }
        }

        private async Task<IActionResult> ProxyPost(string apiPath, object? payload)
        {
            var client = BuildClient();
            if (client == null) return Unauthorized();
            try
            {
                StringContent? content = null;
                if (payload != null)
                {
                    var json = JsonSerializer.Serialize(payload);
                    content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                var res = await client.PostAsync(apiPath, content ?? new StringContent("{}", Encoding.UTF8, "application/json"));
                var body = await res.Content.ReadAsStringAsync();
                return Content(body, "application/json");
            }
            catch { return StatusCode(502, new { error = "Không kết nối được đến API." }); }
        }

        private async Task<IActionResult> ProxyPut(string apiPath)
        {
            var client = BuildClient();
            if (client == null) return Unauthorized();
            try
            {
                var res = await client.PutAsync(apiPath, new StringContent("{}", Encoding.UTF8, "application/json"));
                var body = await res.Content.ReadAsStringAsync();
                return Content(body, "application/json");
            }
            catch { return StatusCode(502, new { error = "Không kết nối được đến API." }); }
        }

        private HttpClient? BuildClient()
        {
            var token = _ctx.HttpContext?.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token)) return null;

            var client = _factory.CreateClient("ApiClient");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            return client;
        }
    }
}
