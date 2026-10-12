using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Doosii.BLL.DTOs
{
    /// <summary>
    /// Request tao moi bai PassPost. Nhan qua multipart/form-data.
    /// </summary>
    public class CreatePassPostRequest
    {
        [Required(ErrorMessage = "INVALID_TITLE")]
        [MaxLength(150, ErrorMessage = "INVALID_TITLE")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "INVALID_DESCRIPTION")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "INVALID_PRICE")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "INVALID_SIZE")]
        [MaxLength(50)]
        public string Size { get; set; } = string.Empty;

        [Range(0, 100, ErrorMessage = "INVALID_CONDITION")]
        public int ConditionPercent { get; set; } = 100;

        public bool AllowEscrow { get; set; } = true;

        public string? StyleTags { get; set; }

        /// <summary>
        /// Danh sach anh (1-5 file). Bat buoc.
        /// </summary>
        public List<IFormFile> Images { get; set; } = new();
    }
}
