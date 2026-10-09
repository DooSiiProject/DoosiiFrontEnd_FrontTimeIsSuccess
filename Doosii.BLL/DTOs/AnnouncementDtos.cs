using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class CreateAnnouncementRequest
    {
        [Required(ErrorMessage = "Mã cửa hàng là bắt buộc.")]
        public int StoreId { get; set; }

        [Required(ErrorMessage = "Tiêu đề buổi khui kiện là bắt buộc.")]
        [MaxLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự.")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "Đường dẫn ảnh banner tối đa 500 ký tự.")]
        public string? BannerUrl { get; set; }

        [Required(ErrorMessage = "Thời gian diễn ra buổi khui kiện là bắt buộc.")]
        public DateTime EventTime { get; set; }

        [MaxLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
        public string? Description { get; set; }

        /// <summary>
        /// Phương thức thanh toán gói phát sóng: "WALLET" (Trừ từ số dư ví) | "PAYOS" (Tạo link VietQR thanh toán)
        /// </summary>
        [Required(ErrorMessage = "Phương thức thanh toán là bắt buộc.")]
        [RegularExpression("^(WALLET|PAYOS)$", ErrorMessage = "Phương thức thanh toán phải là WALLET hoặc PAYOS.")]
        public string PaymentMethod { get; set; } = "WALLET";
    }

    public class AnnouncementResponse
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string StoreAddress { get; set; } = string.Empty;
        public int SellerId { get; set; }
        public string SellerName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? BannerUrl { get; set; }
        public DateTime EventTime { get; set; }
        public string? Description { get; set; }
        public decimal BroadcastFee { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Thông tin thanh toán nếu thanh toán qua cổng PayOS
        public string? PaymentUrl { get; set; }
        public string? QrCodeUrl { get; set; }
    }
}
