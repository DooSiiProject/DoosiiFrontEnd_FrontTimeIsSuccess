namespace Doosii.BLL.DTOs
{
    /// <summary>
    /// Response sau khi tao bai Spot Sharing thanh cong.
    /// </summary>
    public class CreateSpotPostResponse
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? LocationName { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public SpotPostAuthorDto Author { get; set; } = null!;
        public SpotPostTaggedStoreDto? TaggedStore { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public List<SpotImageDto> Images { get; set; } = new();
    }

    /// <summary>
    /// Response cho chi tiet bai Spot Sharing.
    /// </summary>
    public class SpotPostDetailDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? LocationName { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public SpotPostAuthorDto Author { get; set; } = null!;
        public SpotPostTaggedStoreDto? TaggedStore { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public List<SpotImageDto> Images { get; set; } = new();
    }

    /// <summary>
    /// Summary DTO dung trong danh sach (list endpoint).
    /// </summary>
    public class SpotPostSummaryDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? LocationName { get; set; }
        public DateTime CreatedAt { get; set; }
        public SpotPostAuthorDto Author { get; set; } = null!;
        public SpotPostTaggedStoreDto? TaggedStore { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        /// <summary>
        /// Chi lay anh dau tien (DisplayOrder = 0) lam thumbnail.
        /// </summary>
        public string? ThumbnailUrl { get; set; }
        public int TotalImages { get; set; }
    }

    /// <summary>
    /// Thong tin tac gia - khong bao gom email hay password.
    /// </summary>
    public class SpotPostAuthorDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }

    /// <summary>
    /// Thong tin Store duoc tag trong bai.
    /// </summary>
    public class SpotPostTaggedStoreDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    /// <summary>
    /// Anh cua bai Spot Sharing.
    /// </summary>
    public class SpotImageDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }

    /// <summary>
    /// Response phan trang cho danh sach Spot Posts.
    /// </summary>
    public class SpotPostPagedResult
    {
        public List<SpotPostSummaryDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    }
}
