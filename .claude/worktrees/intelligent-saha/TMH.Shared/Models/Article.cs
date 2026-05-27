namespace TMH.Shared.Models
{
    public class Article
    {
        public int Id { get; set; }

        /// <summary>Bác sĩ viết bài — FK tới Doctor.Id</summary>
        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; } = null!;

        public string Title       { get; set; } = string.Empty;
        public string Slug        { get; set; } = string.Empty; // URL-friendly title
        public string Summary     { get; set; } = string.Empty; // Tóm tắt ngắn
        public string Content     { get; set; } = string.Empty; // Nội dung đầy đủ
        public string Category    { get; set; } = string.Empty; // Viêm xoang, Thính học...
        public string? Thumbnail  { get; set; }                 // URL ảnh thumbnail

        public ArticleStatus Status { get; set; } = ArticleStatus.Draft;

        public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAt { get; set; }
        public DateTime? UpdatedAt  { get; set; }
    }

    public enum ArticleStatus
    {
        Draft     = 0,  // Nháp — chỉ bác sĩ và admin thấy
        Published = 1   // Đã đăng — hiển thị công khai
    }
}
