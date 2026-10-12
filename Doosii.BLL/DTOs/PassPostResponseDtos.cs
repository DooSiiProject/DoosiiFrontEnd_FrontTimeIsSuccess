namespace Doosii.BLL.DTOs
{
    /// <summary>
    /// Response cho bai PassPost chi tiet.
    /// </summary>
    public class PassPostDetailDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Size { get; set; } = string.Empty;
        public int ConditionPercent { get; set; }
        public bool AllowEscrow { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? StyleTags { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public PassPostAuthorDto Author { get; set; } = null!;
        public List<PassPostImageDto> Images { get; set; } = new();
    }

    public class PassPostAuthorDto
    {
        public int Id { get; set; }
        /// <summary>
        /// Ten hien thi, khong bao gom email hay password.
        /// </summary>
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }

    public class PassPostImageDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }

    /// <summary>
    /// Response sau khi tao bai Pass thanh cong.
    /// </summary>
    public class CreatePassPostResponse
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<PassPostImageDto> Images { get; set; } = new();
    }
}
