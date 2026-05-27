using Microsoft.EntityFrameworkCore;
using TMH.API.Data;
using TMH.Shared.Enums;
using TMH.Shared.Models;

namespace TMH.API.Services
{
    // ──────────────────────────────────────────────────────────────
    // DTOs dùng cho API & SignalR (không lưu DB)
    // ──────────────────────────────────────────────────────────────
    public class ConversationSummaryDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = "";         // "PatientStaff" | "Internal"
        public string Title { get; set; } = "";        // Tên hiển thị
        public string? LastMessage { get; set; }
        public DateTime LastMessageAt { get; set; }
        public int UnreadCount { get; set; }
        public bool IsOnline { get; set; } = false;
    }

    public class MessageDto
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = "";
        public string SenderRole { get; set; } = "";   // "Patient" | "Staff" | "Doctor" | "Admin"
        public string Content { get; set; } = "";
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
    }

    // ──────────────────────────────────────────────────────────────
    // MessageService
    // ──────────────────────────────────────────────────────────────
    public class MessageService
    {
        private readonly AppDbContext _db;

        public MessageService(AppDbContext db)
        {
            _db = db;
        }

        // ── Lấy danh sách cuộc hội thoại của user hiện tại ────────
        public async Task<List<ConversationSummaryDto>> GetConversationsAsync(int userId, string role)
        {
            IQueryable<Conversation> query;

            if (role == "Patient")
            {
                // Bệnh nhân chỉ thấy cuộc hội thoại PatientStaff của chính họ
                query = _db.Conversations
                    .Where(c => c.Type == ConversationType.PatientStaff && c.PatientUserId == userId);
            }
            else
            {
                // Staff / Doctor / Admin: thấy tất cả PatientStaff + các Internal mà họ tham gia
                query = _db.Conversations
                    .Where(c =>
                        (c.Type == ConversationType.PatientStaff) ||
                        (c.Type == ConversationType.Internal &&
                         c.Participants.Any(p => p.UserId == userId))
                    );
            }

            var conversations = await query
                .Include(c => c.PatientUser)
                .Include(c => c.Participants).ThenInclude(p => p.User)
                .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
                .OrderByDescending(c => c.LastMessageAt)
                .ToListAsync();

            return conversations.Select(c =>
            {
                var lastMsg = c.Messages.FirstOrDefault();
                var participant = c.Participants.FirstOrDefault(p => p.UserId == userId);
                int unread = c.Messages.Count(m =>
                    !m.IsRead && m.SenderId != userId &&
                    (participant == null || m.SentAt > (participant.LastReadAt ?? DateTime.MinValue)));

                string title;
                if (c.Type == ConversationType.PatientStaff)
                {
                    title = c.PatientUser?.FullName ?? "Bệnh nhân";
                }
                else
                {
                    title = c.Title ?? string.Join(", ",
                        c.Participants
                            .Where(p => p.UserId != userId)
                            .Select(p => p.User.FullName)
                            .Take(3));
                }

                return new ConversationSummaryDto
                {
                    Id             = c.Id,
                    Type           = c.Type.ToString(),
                    Title          = title,
                    LastMessage    = lastMsg?.Content,
                    LastMessageAt  = c.LastMessageAt,
                    UnreadCount    = unread
                };
            }).ToList();
        }

        // ── Lấy messages của một cuộc hội thoại ───────────────────
        public async Task<List<MessageDto>> GetMessagesAsync(int conversationId, int userId, int skip = 0, int take = 50)
        {
            // Kiểm tra quyền truy cập
            if (!await HasAccessAsync(conversationId, userId))
                return new List<MessageDto>();

            return await _db.Messages
                .Where(m => m.ConversationId == conversationId)
                .Include(m => m.Sender)
                .OrderByDescending(m => m.SentAt)
                .Skip(skip)
                .Take(take)
                .OrderBy(m => m.SentAt)
                .Select(m => new MessageDto
                {
                    Id             = m.Id,
                    ConversationId = m.ConversationId,
                    SenderId       = m.SenderId,
                    SenderName     = m.Sender.FullName,
                    SenderRole     = m.Sender.Role.ToString(),
                    Content        = m.Content,
                    SentAt         = m.SentAt,
                    IsRead         = m.IsRead
                })
                .ToListAsync();
        }

        // ── Bệnh nhân bắt đầu / lấy lại cuộc hội thoại với lễ tân ─
        public async Task<ConversationSummaryDto> GetOrCreatePatientConversationAsync(int patientUserId)
        {
            var conv = await _db.Conversations
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c =>
                    c.Type == ConversationType.PatientStaff &&
                    c.PatientUserId == patientUserId);

            if (conv == null)
            {
                conv = new Conversation
                {
                    Type          = ConversationType.PatientStaff,
                    PatientUserId = patientUserId,
                    CreatedAt     = DateTime.UtcNow,
                    LastMessageAt = DateTime.UtcNow
                };
                _db.Conversations.Add(conv);
                await _db.SaveChangesAsync();

                // Thêm bệnh nhân vào participants
                _db.ConversationParticipants.Add(new ConversationParticipant
                {
                    ConversationId = conv.Id,
                    UserId         = patientUserId,
                    JoinedAt       = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            var patient = await _db.Users.FindAsync(patientUserId);
            return new ConversationSummaryDto
            {
                Id            = conv.Id,
                Type          = "PatientStaff",
                Title         = patient?.FullName ?? "Bệnh nhân",
                LastMessageAt = conv.LastMessageAt,
                UnreadCount   = 0
            };
        }

        // ── Tạo / lấy cuộc hội thoại nội bộ (2 người) ────────────
        public async Task<ConversationSummaryDto> GetOrCreateInternalConversationAsync(
            int requesterId, int targetUserId, string? title = null)
        {
            // Tìm Direct Message đã tồn tại giữa 2 người
            var existing = await _db.Conversations
                .Include(c => c.Participants)
                .Where(c =>
                    c.Type == ConversationType.Internal &&
                    c.Participants.Any(p => p.UserId == requesterId) &&
                    c.Participants.Any(p => p.UserId == targetUserId) &&
                    c.Participants.Count() == 2)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                var targetUser = await _db.Users.FindAsync(targetUserId);
                return new ConversationSummaryDto
                {
                    Id            = existing.Id,
                    Type          = "Internal",
                    Title         = title ?? targetUser?.FullName ?? "Nội bộ",
                    LastMessageAt = existing.LastMessageAt
                };
            }

            var conv = new Conversation
            {
                Type          = ConversationType.Internal,
                Title         = title,
                CreatedAt     = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow
            };
            _db.Conversations.Add(conv);
            await _db.SaveChangesAsync();

            _db.ConversationParticipants.AddRange(
                new ConversationParticipant { ConversationId = conv.Id, UserId = requesterId, JoinedAt = DateTime.UtcNow },
                new ConversationParticipant { ConversationId = conv.Id, UserId = targetUserId, JoinedAt = DateTime.UtcNow }
            );
            await _db.SaveChangesAsync();

            var target = await _db.Users.FindAsync(targetUserId);
            return new ConversationSummaryDto
            {
                Id            = conv.Id,
                Type          = "Internal",
                Title         = title ?? target?.FullName ?? "Nội bộ",
                LastMessageAt = conv.LastMessageAt
            };
        }

        // ── Gửi tin nhắn ──────────────────────────────────────────
        public async Task<MessageDto?> SendMessageAsync(int conversationId, int senderId, string content)
        {
            if (!await HasAccessAsync(conversationId, senderId))
                return null;

            // Với PatientStaff: tự động thêm nhân viên vào participants khi họ reply lần đầu
            var conv = await _db.Conversations
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conv == null) return null;

            if (!conv.Participants.Any(p => p.UserId == senderId))
            {
                _db.ConversationParticipants.Add(new ConversationParticipant
                {
                    ConversationId = conversationId,
                    UserId         = senderId,
                    JoinedAt       = DateTime.UtcNow
                });
            }

            var msg = new Message
            {
                ConversationId = conversationId,
                SenderId       = senderId,
                Content        = content.Trim(),
                SentAt         = DateTime.UtcNow,
                IsRead         = false
            };
            _db.Messages.Add(msg);

            conv.LastMessageAt = msg.SentAt;
            await _db.SaveChangesAsync();

            var sender = await _db.Users.FindAsync(senderId);
            return new MessageDto
            {
                Id             = msg.Id,
                ConversationId = msg.ConversationId,
                SenderId       = msg.SenderId,
                SenderName     = sender?.FullName ?? "",
                SenderRole     = sender?.Role.ToString() ?? "",
                Content        = msg.Content,
                SentAt         = msg.SentAt,
                IsRead         = msg.IsRead
            };
        }

        // ── Đánh dấu đã đọc ───────────────────────────────────────
        public async Task MarkReadAsync(int conversationId, int userId)
        {
            // Cập nhật LastReadAt của participant
            var participant = await _db.ConversationParticipants
                .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);

            if (participant == null)
            {
                _db.ConversationParticipants.Add(new ConversationParticipant
                {
                    ConversationId = conversationId,
                    UserId         = userId,
                    JoinedAt       = DateTime.UtcNow,
                    LastReadAt     = DateTime.UtcNow
                });
            }
            else
            {
                participant.LastReadAt = DateTime.UtcNow;
            }

            // Đánh dấu IsRead cho các tin nhắn không phải do user gửi
            await _db.Messages
                .Where(m => m.ConversationId == conversationId && m.SenderId != userId && !m.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));

            await _db.SaveChangesAsync();
        }

        // ── Đếm tổng unread của user ───────────────────────────────
        public async Task<int> GetUnreadCountAsync(int userId, string role)
        {
            if (role == "Patient")
            {
                return await _db.Messages
                    .Where(m =>
                        m.Conversation.Type == ConversationType.PatientStaff &&
                        m.Conversation.PatientUserId == userId &&
                        m.SenderId != userId &&
                        !m.IsRead)
                    .CountAsync();
            }
            else
            {
                return await _db.Messages
                    .Where(m =>
                        m.SenderId != userId &&
                        !m.IsRead &&
                        (m.Conversation.Type == ConversationType.PatientStaff ||
                         m.Conversation.Participants.Any(p => p.UserId == userId)))
                    .CountAsync();
            }
        }

        // ── Lấy danh sách user có thể chat nội bộ ─────────────────
        public async Task<List<object>> GetInternalUsersAsync(int requesterId)
        {
            var users = await _db.Users
                .Where(u => u.Id != requesterId &&
                            u.IsActive &&
                            (u.Role == UserRole.Admin || u.Role == UserRole.Doctor || u.Role == UserRole.Staff))
                .Select(u => new
                {
                    id       = u.Id,
                    name     = u.FullName,
                    role     = u.Role.ToString(),
                    initials = u.Ten.Length >= 2
                               ? u.Ten.Substring(0, 2).ToUpper()
                               : u.Ten.ToUpper()
                })
                .ToListAsync<object>();

            return users;
        }

        // ── Lấy IDs của tất cả PatientStaff conversations ─────────
        // Dùng bởi ChatHub.OnConnectedAsync để Staff join group
        public async Task<List<int>> GetPatientStaffConvIdsAsync()
        {
            return await _db.Conversations
                .Where(c => c.Type == ConversationType.PatientStaff)
                .Select(c => c.Id)
                .ToListAsync();
        }

        // ── Kiểm tra conv có phải PatientStaff không ───────────────
        public async Task<bool> IsPatientStaffConvAsync(int conversationId)
        {
            return await _db.Conversations
                .AnyAsync(c => c.Id == conversationId && c.Type == ConversationType.PatientStaff);
        }

        // ── Helpers ────────────────────────────────────────────────
        private async Task<bool> HasAccessAsync(int conversationId, int userId)
        {
            var conv = await _db.Conversations
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conv == null) return false;

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return false;

            // Bệnh nhân: chỉ truy cập conv của chính họ
            if (user.Role == UserRole.Patient)
                return conv.PatientUserId == userId;

            // Internal: phải là participant
            if (conv.Type == ConversationType.Internal)
                return conv.Participants.Any(p => p.UserId == userId);

            // Staff/Doctor/Admin: truy cập tất cả PatientStaff
            return conv.Type == ConversationType.PatientStaff;
        }
    }
}
