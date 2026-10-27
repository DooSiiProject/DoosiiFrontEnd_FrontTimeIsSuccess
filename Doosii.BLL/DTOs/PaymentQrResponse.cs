namespace Doosii.BLL.DTOs
{
    public class PaymentQrResponse
    {
        public bool IsSandbox { get; set; }
        public string QrCodeData { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public DateTime ServerTime { get; set; } = DateTime.UtcNow;
        public string Message { get; set; } = "";
        public int OrderId { get; set; }
        public long OrderCode { get; set; }
        public decimal Amount { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
        public string QrCodeUrl { get; set; } = string.Empty;
        public string TransferContent { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string BankBin { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
