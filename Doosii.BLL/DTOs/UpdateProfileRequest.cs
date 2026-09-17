using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class UpdateProfileRequest
    {
        [Required(ErrorMessage = "Ho va ten la bat buoc.")]
        [MaxLength(100, ErrorMessage = "Ho va ten khong duoc vuot qua 100 ky tu.")]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "AvatarUrl khong duoc vuot qua 500 ky tu.")]
        public string? AvatarUrl { get; set; }

        [MaxLength(500, ErrorMessage = "Dia chi giao hang khong duoc vuot qua 500 ky tu.")]
        public string? DeliveryAddress { get; set; }
    }
}
