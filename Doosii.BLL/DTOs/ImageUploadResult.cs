namespace Doosii.BLL.DTOs
{
    /// <summary>
    /// Ket qua upload mot anh len Cloudinary.
    /// Khong chua ApiKey, ApiSecret hay thong tin nhay cam.
    /// </summary>
    public class ImageUploadResult
    {
        public string Url { get; set; } = string.Empty;
        public string SecureUrl { get; set; } = string.Empty;
        public string PublicId { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public string Format { get; set; } = string.Empty;
        public long Bytes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
