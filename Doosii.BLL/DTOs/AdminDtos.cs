using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class ReviewKycRequest
    {
        [Required(ErrorMessage = "Quyết định phê duyệt là bắt buộc.")]
        public bool IsApproved { get; set; }

        [MaxLength(1000, ErrorMessage = "Lý do từ chối tối đa 1000 ký tự.")]
        public string? RejectionReason { get; set; }
    }

    public class ArbitrateDisputeRequest
    {
        /// <summary>
        /// Phán quyết phân xử: "REFUND_BUYER" (Hoàn tiền cho người mua) | "RELEASE_SELLER" (Giải ngân cho người bán)
        /// </summary>
        [Required(ErrorMessage = "Phán quyết phân xử là bắt buộc.")]
        [RegularExpression("^(REFUND_BUYER|RELEASE_SELLER)$", ErrorMessage = "Phán quyết phải là 'REFUND_BUYER' hoặc 'RELEASE_SELLER'.")]
        public string Verdict { get; set; } = string.Empty;

        [Required(ErrorMessage = "Giải trình phán quyết của Admin là bắt buộc.")]
        [MaxLength(1000, ErrorMessage = "Giải trình tối đa 1000 ký tự.")]
        public string AdminVerdict { get; set; } = string.Empty;
    }

    public class ProcessWithdrawalRequest
    {
        [Required(ErrorMessage = "Quyết định phê duyệt rút tiền là bắt buộc.")]
        public bool IsApproved { get; set; }

        [MaxLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
        public string? AdminNote { get; set; }
    }

    public class AdminAnalyticsResponse
    {
        /// <summary>
        /// Tổng giá trị hàng hóa giao dịch (Gross Merchandise Value)
        /// </summary>
        public decimal TotalGmv { get; set; }

        /// <summary>
        /// Doanh thu tích lũy từ phí ký quỹ sàn (2.5%)
        /// </summary>
        public decimal PlatformEscrowRevenue { get; set; }

        /// <summary>
        /// Tổng số đơn hàng trên hệ thống
        /// </summary>
        public int TotalOrders { get; set; }

        /// <summary>
        /// Số đơn hàng hoàn tất thành công
        /// </summary>
        public int CompletedOrders { get; set; }

        /// <summary>
        /// Số đơn hàng phát sinh khiếu nại
        /// </summary>
        public int DisputedOrders { get; set; }

        /// <summary>
        /// Số lượng người bán đã được duyệt KYC
        /// </summary>
        public int TotalVerifiedSellers { get; set; }

        /// <summary>
        /// Tổng số người dùng khách hàng
        /// </summary>
        public int TotalCustomers { get; set; }
    }
}
