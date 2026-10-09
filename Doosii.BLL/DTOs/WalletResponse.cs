namespace Doosii.BLL.DTOs
{
    public class WalletResponse
    {
        /// <summary>
        /// Số dư khả dụng hiện tại có thể rút về tài khoản ngân hàng
        /// </summary>
        public decimal AvailableBalance { get; set; }

        /// <summary>
        /// Số dư đang được tạm giữ trong quỹ Escrow (các đơn hàng đang giao hoặc đang chờ gửi)
        /// </summary>
        public decimal EscrowHoldingBalance { get; set; }

        /// <summary>
        /// Tổng tiền đang chờ admin duyệt rút
        /// </summary>
        public decimal PendingWithdrawalAmount { get; set; }

        /// <summary>
        /// Tổng doanh thu tích lũy từ các đơn hàng đã hoàn tất
        /// </summary>
        public decimal TotalRevenue { get; set; }

        /// <summary>
        /// Lịch sử biến động số dư (Sổ cái giao dịch)
        /// </summary>
        public List<WalletTransactionDto> Transactions { get; set; } = new List<WalletTransactionDto>();
    }

    public class WalletTransactionDto
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Loại giao dịch: "ORDER_PAYOUT" (Cộng tiền bán hàng) | "WITHDRAWAL" (Rút tiền về ngân hàng)
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Số tiền giao dịch (Dương nếu nhận tiền, Âm nếu rút tiền)
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Mô tả chi tiết giao dịch
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Trạng thái: "COMPLETED", "PENDING", "APPROVED", "REJECTED"
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Mã tham chiếu liên quan (Mã đơn hàng hoặc Mã yêu cầu rút tiền)
        /// </summary>
        public string? ReferenceId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
