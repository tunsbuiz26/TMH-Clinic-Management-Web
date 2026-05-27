using Microsoft.AspNetCore.Mvc;
using TMH.Web.Services;

namespace TMH.Web.Controllers
{
    [Route("api/chat-proxy")]
    [ApiController]
    public class ChatProxyController : ControllerBase
    {
        private readonly ApiService _api;
        public ChatProxyController(ApiService api) { _api = api; }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] ChatProxyRequest dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { reply = "Vui lòng nhập câu hỏi." });

            var reply = await _api.AskChatAsync(dto.Message, dto.History?.Cast<object>());
            return Ok(new { reply });
        }
    }

    public class ChatProxyRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<ChatHistoryItem>? History { get; set; }
    }

    public class ChatHistoryItem
    {
        public string Role    { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
