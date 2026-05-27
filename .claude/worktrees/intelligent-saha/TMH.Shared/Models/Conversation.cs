using TMH.Shared.Enums;

namespace TMH.Shared.Models
{
    public class Conversation
    {
        public int Id { get; set; }

        public ConversationType Type { get; set; }

        /// <summary>
        /// Chỉ dùng cho PatientStaff: UserId của bệnh nhân khởi tạo cuộc hội thoại.
        /// </summary>
        public int? PatientUserId { get; set; }
        public User? PatientUser { get; set; }

        /// <summary>
        /// Tiêu đề tuỳ chỉnh cho chat nội bộ (ví dụ: "Thảo luận ca khám 23/3").
        /// </summary>
        public string? Title { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

        public ICollection<Message> Messages { get; set; } = new List<Message>();
        public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
    }
}
