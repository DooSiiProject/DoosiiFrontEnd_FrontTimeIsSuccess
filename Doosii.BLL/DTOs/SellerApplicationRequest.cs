using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class SellerApplicationRequest
    {
        [Required(ErrorMessage = "Ten cua hang la bat buoc.")]
        [MaxLength(200, ErrorMessage = "Ten cua hang khong duoc vuot qua 200 ky tu.")]
        public string StoreName { get; set; } = string.Empty;

        [Required(ErrorMessage = "So dien thoai la bat buoc.")]
        [MaxLength(20, ErrorMessage = "So dien thoai khong duoc vuot qua 20 ky tu.")]
        [Phone(ErrorMessage = "So dien thoai khong hop le.")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Dia chi la bat buoc.")]
        [MaxLength(500, ErrorMessage = "Dia chi khong duoc vuot qua 500 ky tu.")]
        public string Address { get; set; } = string.Empty;

        [Range(-90.0, 90.0, ErrorMessage = "Latitude phai nam trong khoang -90 den 90.")]
        public double? Latitude { get; set; }

        [Range(-180.0, 180.0, ErrorMessage = "Longitude phai nam trong khoang -180 den 180.")]
        public double? Longitude { get; set; }

        [Required(ErrorMessage = "Anh giay phep kinh doanh la bat buoc.")]
        [MaxLength(1000)]
        public string LicenseImageUrl { get; set; } = string.Empty;

        [Required(ErrorMessage = "Anh mat tien cua hang la bat buoc.")]
        [MaxLength(1000)]
        public string FrontFacadeUrl { get; set; } = string.Empty;

        [Required(ErrorMessage = "Anh CCCD mat truoc la bat buoc.")]
        [MaxLength(1000)]
        public string IdCardFrontUrl { get; set; } = string.Empty;

        [Required(ErrorMessage = "Anh CCCD mat sau la bat buoc.")]
        [MaxLength(1000)]
        public string IdCardBackUrl { get; set; } = string.Empty;
    }
}
