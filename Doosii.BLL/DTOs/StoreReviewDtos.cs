using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class CreateStoreReviewRequest
    {
        public int? OrderId { get; set; }

        [Required(ErrorMessage = "Rating la bat buoc.")]
        [Range(1, 5, ErrorMessage = "Rating phai tu 1 den 5.")]
        public int Rating { get; set; }

        [MaxLength(2000, ErrorMessage = "Comment khong duoc vuot qua 2000 ky tu.")]
        public string? Comment { get; set; }

        public List<string>? Images { get; set; }
    }

    public class UpdateStoreReviewRequest
    {
        [Required(ErrorMessage = "Rating la bat buoc.")]
        [Range(1, 5, ErrorMessage = "Rating phai tu 1 den 5.")]
        public int Rating { get; set; }

        [MaxLength(2000, ErrorMessage = "Comment khong duoc vuot qua 2000 ky tu.")]
        public string? Comment { get; set; }

        public List<string>? Images { get; set; }
    }

    public class StoreReviewQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; } = "newest";
    }

    public class StoreReviewDto
    {
        public int ReviewId { get; set; }
        public int UserId { get; set; }
        public string UserDisplayName { get; set; } = string.Empty;
        public string? UserAvatarUrl { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public List<string> Images { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class StoreReviewsSummaryDto
    {
        public int StoreId { get; set; }
        public decimal RatingAverage { get; set; }
        public int TotalReviews { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public List<StoreReviewDto> Items { get; set; } = new();
    }
}
