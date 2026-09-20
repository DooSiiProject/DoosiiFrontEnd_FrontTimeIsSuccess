using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class UpdateProductRequest
    {
        [Required(ErrorMessage = "CategoryId la bat buoc.")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Tieu de san pham la bat buoc.")]
        [MaxLength(300, ErrorMessage = "Tieu de khong qua 300 ky tu.")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Gia san pham la bat buoc.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Gia phai lon hon 0.")]
        public decimal Price { get; set; }

        [MaxLength(50)]
        public string? Size { get; set; }

        [Range(0, 100, ErrorMessage = "ConditionPercent phai tu 0 den 100.")]
        public int ConditionPercent { get; set; } = 100;

        [MaxLength(500)]
        public string? StyleTags { get; set; }

        [Required(ErrorMessage = "Danh sach anh la bat buoc.")]
        [MinLength(1, ErrorMessage = "Can it nhat 1 anh.")]
        [MaxLength(5, ErrorMessage = "Toi da 5 anh.")]
        public List<string> ImageUrls { get; set; } = new();
    }
}