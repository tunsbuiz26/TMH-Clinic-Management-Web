using System;
using System.ComponentModel.DataAnnotations;

namespace TMH.Shared.DTOs
{
    public class SubmitReviewDto
    {
        [Required]
        public int AppointmentId { get; set; }

        [Required, Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }
    }

    public class ReviewDto
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string DoctorDegree { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DoctorRatingSummaryDto
    {
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string DoctorDegree { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public List<ReviewDto> RecentReviews { get; set; } = new();
    }

    public class ReviewResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
