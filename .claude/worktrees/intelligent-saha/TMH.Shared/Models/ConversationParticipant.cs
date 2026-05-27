namespace TMH.Shared.Models
{
    /// <summary>
    /// Người tham gia cuộc hội thoại — dùng cho cả PatientStaff lẫn Internal.
    /// Với PatientStaff: lưu cả bệnh nhân lẫn các nhân viên đã reply.
    /// Với Internal: lưu tất cả thành viên nhóm / DM.
    /// </summary>
    public class ConversationParticipant
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Thời điểm người dùng đọc tin nhắn lần cuối — để tính badge unread.</summary>
        public DateTime? LastReadAt { get; set; }
    }
}
