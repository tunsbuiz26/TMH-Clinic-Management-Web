namespace TMH.Shared.DTOs
{
    /// <summary>Request bệnh nhân gửi lên để nhận gợi ý bác sĩ phù hợp.</summary>
    public class DoctorRecommendationRequestDto
    {
        /// <summary>Triệu chứng bệnh nhân mô tả (ví dụ: "ù tai, nghe kém 2 tuần nay")</summary>
        public string Symptoms { get; set; } = string.Empty;
    }

    /// <summary>Kết quả gợi ý bác sĩ trả về cho client.</summary>
    public class DoctorRecommendationResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>ID bác sĩ được AI gợi ý (null nếu không tìm thấy)</summary>
        public int? RecommendedDoctorId { get; set; }

        public string DoctorName { get; set; } = string.Empty;
        public string Specialty   { get; set; } = string.Empty;
        public string Degree      { get; set; } = string.Empty;

        /// <summary>Lý do AI gợi ý bác sĩ này (2-3 câu tiếng Việt)</summary>
        public string Reason { get; set; } = string.Empty;
    }
}
