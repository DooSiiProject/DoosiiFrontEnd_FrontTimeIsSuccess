using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class CreateEscrowOrderRequest
    {
        [Required(ErrorMessage = "Mã người bán là bắt buộc.")]
        public int SellerId { get; set; }

        [Required(ErrorMessage = "Mã sản phẩm là bắt buộc.")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Tên sản phẩm là bắt buộc.")]
        [MaxLength(256, ErrorMessage = "Tên sản phẩm tối đa 256 ký tự.")]
        public string ProductName { get; set; } = string.Empty;

        [Range(1000, 1000000000, ErrorMessage = "Giá sản phẩm phải từ 1,000 VND đến 1,000,000,000 VND.")]
        public decimal ProductPrice { get; set; }

        [MaxLength(500, ErrorMessage = "Đường dẫn ảnh thu nhỏ tối đa 500 ký tự.")]
        public string? ProductThumbnail { get; set; }

        [Range(0, 10000000, ErrorMessage = "Phí vận chuyển phải từ 0 đến 10,000,000 VND.")]
        public decimal ShippingFee { get; set; } = 0;

        [Required(ErrorMessage = "Tên người nhận là bắt buộc.")]
        [MaxLength(256, ErrorMessage = "Tên người nhận tối đa 256 ký tự.")]
        public string ReceiverName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số điện thoại người nhận là bắt buộc.")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [MaxLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
        public string ReceiverPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Địa chỉ nhận hàng là bắt buộc.")]
        [MaxLength(500, ErrorMessage = "Địa chỉ nhận hàng tối đa 500 ký tự.")]
        public string ShippingAddress { get; set; } = string.Empty;
    }
}
