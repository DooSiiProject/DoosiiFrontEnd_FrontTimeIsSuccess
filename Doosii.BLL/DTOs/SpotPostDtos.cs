using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    /// <summary>
    /// Request tao bai Spot Sharing Post. Nhan qua multipart/form-data.
    /// Bat buoc phai co: TaggedStoreId hoac (Latitude + Longitude).
    /// </summary>
    public class CreateSpotPostRequest
    {
        /// <summary>
        /// ID cua Store muon tag. Co the null neu dung toa do thu cong.
        /// </summary>
        public int? TaggedStoreId { get; set; }

        /// <summary>
        /// Vi do, tu -90 den 90. Co the null neu dung TaggedStoreId.
        /// </summary>
        public decimal? Latitude { get; set; }

        /// <summary>
        /// Kinh do, tu -180 den 180. Co the null neu dung TaggedStoreId.
        /// </summary>
        public decimal? Longitude { get; set; }

        /// <summary>
        /// Ten dia diem tu nhap. Khong bat buoc, toi da 200 ky tu.
        /// </summary>
        [MaxLength(200)]
        public string? LocationName { get; set; }

        /// <summary>
        /// Noi dung bai dang. Bat buoc, toi da 2000 ky tu.
        /// </summary>
        [Required(ErrorMessage = "INVALID_CONTENT")]
        [MaxLength(2000, ErrorMessage = "INVALID_CONTENT")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Danh sach anh (1-5 file). Bat buoc.
        /// </summary>
        public List<IFormFile> Images { get; set; } = new();
    }

    /// <summary>
    /// Query parameters cho GET /api/stores/{storeId}/spot-posts
    /// </summary>
    public class SpotPostQueryParams
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
