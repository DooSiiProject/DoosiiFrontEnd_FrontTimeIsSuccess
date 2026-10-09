using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class ImageUploadRequest
    {
        /// <summary>
        /// Thu muc con de upload anh vao. Phai nam trong whitelist:
        /// "products", "stores", "reviews", "forum" hoac de trong (root folder).
        /// </summary>
        [MaxLength(50)]
        public string? Folder { get; set; } = "products";
    }
}
