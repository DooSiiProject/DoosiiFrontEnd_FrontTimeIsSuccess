using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class ShipOrderRequest
    {
        [Required(ErrorMessage = "Tên đơn vị vận chuyển là bắt buộc.")]
        [MaxLength(100, ErrorMessage = "Tên đơn vị vận chuyển tối đa 100 ký tự.")]
        public string ShippingCarrier { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mã vận đơn là bắt buộc.")]
        [MaxLength(100, ErrorMessage = "Mã vận đơn tối đa 100 ký tự.")]
        public string TrackingCode { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "Đường dẫn ảnh kiện hàng tối đa 500 ký tự.")]
        public string? PackageImageUrl { get; set; }
    }
}
