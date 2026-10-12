namespace Doosii.BLL.DTOs
{
    /// <summary>
    /// Tham so query cho endpoint GET /api/forum/feed
    /// </summary>
    public class CommunityFeedQuery
    {
        /// <summary>
        /// Tab can lay: newest | near-you | by-style. Mac dinh: newest.
        /// </summary>
        public string? Tab { get; set; } = "newest";

        /// <summary>
        /// Style tag can loc, bat buoc khi tab = by-style.
        /// </summary>
        public string? Style { get; set; }

        /// <summary>
        /// Vi do cua nguoi dung, bat buoc khi tab = near-you. Tu -90 den 90.
        /// </summary>
        public double? Lat { get; set; }

        /// <summary>
        /// Kinh do cua nguoi dung, bat buoc khi tab = near-you. Tu -180 den 180.
        /// </summary>
        public double? Lng { get; set; }

        /// <summary>
        /// Ban kinh loc (km), chi nhan 1, 3, 5 hoac 10. Mac dinh: 5.
        /// </summary>
        public int? RadiusKm { get; set; } = 5;

        /// <summary>
        /// Trang hien tai (1-based). Mac dinh: 1.
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// So luong item moi trang, mac dinh 20, toi da 50.
        /// </summary>
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// Item trong Community Feed (thong nhat giua PassPost va SpotSharingPost).
    /// </summary>
    public class CommunityFeedItemDto
    {
        public int PostId { get; set; }

        /// <summary>
        /// Loai bai dang: PASS hoac SPOT.
        /// </summary>
        public string PostType { get; set; } = string.Empty;

        /// <summary>
        /// Tieu de bai viet (chi co o PassPost).
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Noi dung bai dang (Content cua SpotSharingPost hoac Description cua PassPost).
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gia san pham (chi co o PassPost).
        /// </summary>
        public decimal? Price { get; set; }

        /// <summary>
        /// Size san pham (chi co o PassPost).
        /// </summary>
        public string? Size { get; set; }

        /// <summary>
        /// Tinh trang san pham 0-100% (chi co o PassPost).
        /// </summary>
        public int? ConditionPercent { get; set; }

        /// <summary>
        /// Cho phep giao dich qua Escrow (chi co o PassPost).
        /// </summary>
        public bool? AllowEscrow { get; set; }

        /// <summary>
        /// Tags phong cach (PassPost hoac Spot).
        /// </summary>
        public string? StyleTags { get; set; }

        public int AuthorId { get; set; }
        public string AuthorDisplayName { get; set; } = string.Empty;
        public string? AuthorAvatarUrl { get; set; }

        /// <summary>
        /// Danh sach anh dinh kem.
        /// </summary>
        public List<FeedImageDto> Images { get; set; } = new();

        /// <summary>
        /// Cua hang duoc tag (chi co o SpotSharingPost).
        /// </summary>
        public FeedTaggedStoreDto? TaggedStore { get; set; }

        /// <summary>
        /// Vi do dia diem cua bai.
        /// </summary>
        public decimal? Latitude { get; set; }

        /// <summary>
        /// Kinh do dia diem cua bai.
        /// </summary>
        public decimal? Longitude { get; set; }

        /// <summary>
        /// Khoang cach tinh theo Haversine (km), chi co khi tab = near-you.
        /// </summary>
        public double? DistanceKm { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class FeedImageDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }

    public class FeedTaggedStoreDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    /// <summary>
    /// Response du lieu cho Community Feed gom danh sach va metadata phan trang.
    /// </summary>
    public class CommunityFeedResponseDto
    {
        public List<CommunityFeedItemDto> Items { get; set; } = new();
        public string Tab { get; set; } = "newest";
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }
}
