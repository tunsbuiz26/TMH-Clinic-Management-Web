using Microsoft.AspNetCore.Mvc;
using TMH.API.Services;

namespace TMH.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ChatService _chat;

        public ChatController(ChatService chat)
        {
            _chat = chat;
        }

        // POST /api/chat
        // Công khai — không yêu cầu đăng nhập (bệnh nhân chưa đăng nhập cũng hỏi được)
        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] ChatRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { reply = "Vui lòng nhập nội dung câu hỏi." });

            // Giới hạn độ dài tin nhắn để tránh lạm dụng
            if (dto.Message.Length > 500)
                return BadRequest(new { reply = "Tin nhắn quá dài (tối đa 500 ký tự)." });

            var reply = await _chat.AskAsync(dto.Message, dto.History);
            return Ok(new { reply });
        }
    }
}
    