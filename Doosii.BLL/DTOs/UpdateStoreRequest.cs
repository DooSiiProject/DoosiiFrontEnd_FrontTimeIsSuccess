using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class UpdateStoreRequest
    {
        [Required(ErrorMessage = "Ten cua hang la bat buoc.")]
        [MaxLength(200, ErrorMessage = "Ten cua hang khong duoc qua 200 ky tu.")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        [MaxLength(500)]
        public string? OpeningHours { get; set; }

        [Range(-90.0, 90.0, ErrorMessage = "Latitude phai tu -90 den 90.")]
        public double? Latitude { get; set; }

        [Range(-180.0, 180.0, ErrorMessage = "Longitude phai tu -180 den 180.")]
        public double? Longitude { get; set; }
    }
}