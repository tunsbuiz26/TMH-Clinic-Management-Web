using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using TMH.API.Services;

namespace TMH.API.Hubs
{
    /// <summary>
    /// SignalR Hub cho real-time chat.
    /// URL: /hubs/chat
    ///
    /// Client → Server methods:
    ///   JoinConversation(int conversationId)
    ///   LeaveConversation(int conversationId)
    ///   SendMessage(int conversationId, string content)
    ///   MarkRead(int conversationId)
    ///
    /// Server → Client callbacks:
    ///   ReceiveMessage(MessageDto)
    ///   MessagesRead(int conversationId)
    ///   UnreadUpdate(int totalUnread)
    ///   Error(string message)
    /// </summary>
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly MessageService _msg;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(MessageService msg, ILogger<ChatHub> logger)
        {
            _msg    = msg;
            _logger = logger;
        }

        // ── Khi client kết nối: tự động join group cá nhân ────────
        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            var role   = GetUserRole();

            if (userId > 0)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

                // Staff: join staff_inbox + tất cả PatientStaff conv groups
                // để nhận real-time khi bệnh nhân gửi tin mà không cần mở từng conv
                if (role == "Staff")
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, "staff_inbox");
                    var convIds = await _msg.GetPatientStaffConvIdsAsync();
                    foreach (var cid in convIds)
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"conv_{cid}");
                }
            }

            await base.OnConnectedAsync();
        }

        // ── Join vào room của 1 cuộc hội thoại ────────────────────
        public async Task JoinConversation(int conversationId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"conv_{conversationId}");
        }

        // ── Rời room ───────────────────────────────────────────────
        public async Task LeaveConversation(int conversationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv_{conversationId}");
        }

        // ── Gửi tin nhắn real-time ─────────────────────────────────
        public async Task SendMessage(int conversationId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return;

            content = content.Trim();
            if (content.Length > 2000)
            {
                await Clients.Caller.SendAsync("Error", "Tin nhắn quá dài (tối đa 2000 ký tự).");
                return;
            }

            var userId = GetUserId();
            if (userId <= 0)
            {
                await Clients.Caller.SendAsync("Error", "Không xác định được người dùng.");
                return;
            }

            var msg = await _msg.SendMessageAsync(conversationId, userId, content);
            if (msg == null)
            {
                await Clients.Caller.SendAsync("Error", "Không có quyền gửi tin vào cuộc hội thoại này.");
                return;
            }

            // Broadcast đến tất cả thành viên đang xem cuộc hội thoại
            await Clients.Group($"conv_{conversationId}").SendAsync("ReceiveMessage", msg);

            // Với PatientStaff: đảm bảo staff online nhận được dù chưa vào tab Tin nhắn
            if (await _msg.IsPatientStaffConvAsync(conversationId))
                await Clients.Group("staff_inbox").SendAsync("ReceiveMessage", msg);

            // Cập nhật unread badge cho phía nhận (Staff nhận khi bệnh nhân gửi và ngược lại)
            // Gọi UpdateUnread cho tất cả members không phải sender
            _logger.LogInformation("Message {MsgId} sent in conv {ConvId} by user {UserId}", msg.Id, conversationId, userId);
        }

        // ── Đánh dấu đã đọc ───────────────────────────────────────
        public async Task MarkRead(int conversationId)
        {
            var userId = GetUserId();
            if (userId <= 0) return;

            var role = GetUserRole();
            await _msg.MarkReadAsync(conversationId, userId);

            // Thông báo cho các thành viên khác trong room
            await Clients.Group($"conv_{conversationId}")
                          .SendAsync("MessagesRead", conversationId, userId);

            // Cập nhật badge unread của chính user
            var unread = await _msg.GetUnreadCountAsync(userId, role);
            await Clients.Caller.SendAsync("UnreadUpdate", unread);
        }

        // ── Helpers ────────────────────────────────────────────────
        private int GetUserId()
        {
            var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? Context.User?.FindFirst("sub")?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        private string GetUserRole()
        {
            return Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? "";
        }
    }
}
