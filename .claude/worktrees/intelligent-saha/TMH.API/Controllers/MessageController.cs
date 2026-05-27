using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using TMH.API.Hubs;
using TMH.API.Services;

namespace TMH.API.Controllers
{
    [ApiController]
    [Route("api/messages")]
    [Authorize]
    public class MessageController : ControllerBase
    {
        private readonly MessageService _msg;

        public MessageController(MessageService msg) => _msg = msg;

        // ── Lấy danh sách conversations ───────────────────────────
        // GET /api/messages/conversations
        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            var (userId, role) = GetClaims();
            var list = await _msg.GetConversationsAsync(userId, role);
            return Ok(list);
        }

        // ── Lấy messages của 1 conversation ───────────────────────
        // GET /api/messages/conversations/{id}/messages?skip=0&take=50
        [HttpGet("conversations/{id:int}/messages")]
        public async Task<IActionResult> GetMessages(int id,
            [FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            var (userId, _) = GetClaims();
            var messages = await _msg.GetMessagesAsync(id, userId, skip, take);
            return Ok(messages);
        }

        // ── Bệnh nhân tạo / lấy conversation với lễ tân ──────────
        // POST /api/messages/conversations/patient-staff
        [HttpPost("conversations/patient-staff")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> StartPatientStaff()
        {
            var (userId, _) = GetClaims();
            var conv = await _msg.GetOrCreatePatientConversationAsync(userId);
            return Ok(conv);
        }

        // ── Tạo / lấy cuộc hội thoại nội bộ ─────────────────────
        // POST /api/messages/conversations/internal
        // Body: { targetUserId, title? }
        [HttpPost("conversations/internal")]
        [Authorize(Roles = "Admin,Doctor,Staff")]
        public async Task<IActionResult> StartInternal([FromBody] StartInternalDto dto)
        {
            if (dto.TargetUserId <= 0)
                return BadRequest(new { error = "targetUserId không hợp lệ." });

            var (userId, _) = GetClaims();
            var conv = await _msg.GetOrCreateInternalConversationAsync(userId, dto.TargetUserId, dto.Title);
            return Ok(conv);
        }

        // ── Đánh dấu đã đọc ───────────────────────────────────────
        // PUT /api/messages/conversations/{id}/read
        [HttpPut("conversations/{id:int}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var (userId, _) = GetClaims();
            await _msg.MarkReadAsync(id, userId);
            return Ok(new { ok = true });
        }

        // ── Lấy tổng unread (badge) ────────────────────────────────
        // GET /api/messages/unread
        [HttpGet("unread")]
        public async Task<IActionResult> GetUnread()
        {
            var (userId, role) = GetClaims();
            var count = await _msg.GetUnreadCountAsync(userId, role);
            return Ok(new { count });
        }

        // ── Danh sách user có thể chat nội bộ ─────────────────────
        // GET /api/messages/internal-users
        [HttpGet("internal-users")]
        [Authorize(Roles = "Admin,Doctor,Staff")]
        public async Task<IActionResult> GetInternalUsers()
        {
            var (userId, _) = GetClaims();
            var users = await _msg.GetInternalUsersAsync(userId);
            return Ok(users);
        }

        // ── Gửi tin nhắn qua REST (fallback khi SignalR không dùng được) ──
        // POST /api/messages/conversations/{id}/send
        // Body: { content }
        [HttpPost("conversations/{id:int}/send")]
        public async Task<IActionResult> SendMessage(int id, [FromBody] SendMessageDto dto,
            [FromServices] IHubContext<ChatHub> hub)
        {
            if (string.IsNullOrWhiteSpace(dto.Content))
                return BadRequest(new { error = "Nội dung không được để trống." });

            if (dto.Content.Length > 2000)
                return BadRequest(new { error = "Tin nhắn quá dài." });

            var (userId, _) = GetClaims();
            var msg = await _msg.SendMessageAsync(id, userId, dto.Content);
            if (msg == null)
                return Forbid();

            // Broadcast qua SignalR dù gửi bằng REST — đảm bảo real-time cho phía nhận
            await hub.Clients.Group($"conv_{id}").SendAsync("ReceiveMessage", msg);
            if (await _msg.IsPatientStaffConvAsync(id))
                await hub.Clients.Group("staff_inbox").SendAsync("ReceiveMessage", msg);

            return Ok(msg);
        }

        // ── Helpers ────────────────────────────────────────────────
        private (int userId, string role) GetClaims()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;
            int.TryParse(idClaim, out var uid);
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            return (uid, role);
        }
    }

    public class StartInternalDto
    {
        public int TargetUserId { get; set; }
        public string? Title    { get; set; }
    }

    public class SendMessageDto
    {
        public string Content { get; set; } = string.Empty;
    }
}
