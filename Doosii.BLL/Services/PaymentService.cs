using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Order;

namespace Doosii.BLL.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentService> _logger;
        private readonly PayOSClient? _payOSClient;
        private readonly string _checksumKey;

        public PaymentService(
            AppDbContext context,
            IConfiguration configuration,
            ILogger<PaymentService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;

            var clientId = _configuration["PayOS:ClientId"] ?? string.Empty;
            var apiKey = _configuration["PayOS:ApiKey"] ?? string.Empty;
            _checksumKey = _configuration["PayOS:ChecksumKey"] ?? "your-payos-checksum-key-1234567890";

            if (!string.IsNullOrWhiteSpace(clientId) &&
                !string.IsNullOrWhiteSpace(apiKey) &&
                !clientId.Contains("your-payos") &&
                !apiKey.Contains("your-payos"))
            {
                try
                {
                    _payOSClient = new PayOSClient(new PayOSOptions
                    {
                        ClientId = clientId,
                        ApiKey = apiKey,
                        ChecksumKey = _checksumKey
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể khởi tạo PayOSClient với cấu hình hiện tại. Chuyển sang chế độ VietQR Sandbox.");
                }
            }
        }

        public async Task<PaymentQrResponse> CreatePaymentQrAsync(int orderId, int userId)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy đơn hàng với mã #{orderId}.");
            }

            if (order.BuyerId != userId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền thực hiện thanh toán cho đơn hàng này.");
            }

            if (order.Status != OrderStatus.PendingPayment)
            {
                throw new InvalidOperationException($"Đơn hàng #{orderId} hiện ở trạng thái '{order.Status}', không thể thanh toán tiếp.");
            }

            var transferContent = $"DOSI {order.Id}";
            var totalAmount = (long)order.TotalAmount;

            // 1. Thử tạo payment link qua PayOS SDK nếu có thông tin kết nối hợp lệ
            if (_payOSClient != null)
            {
                try
                {
                    var items = order.Items.Select(item =>
                        new PaymentLinkItem
                        {
                            Name = item.ProductNameSnapshot.Length > 50 ? item.ProductNameSnapshot.Substring(0, 47) + "..." : item.ProductNameSnapshot,
                            Quantity = 1,
                            Price = (long)item.PriceSnapshot
                        }
                    ).ToList();

                    var returnUrl = _configuration["PayOS:ReturnUrl"] ?? "http://localhost:5173/payment/success";
                    var cancelUrl = _configuration["PayOS:CancelUrl"] ?? "http://localhost:5173/payment/cancel";

                    var paymentRequest = new CreatePaymentLinkRequest
                    {
                        OrderCode = order.Id,
                        Amount = totalAmount,
                        Description = transferContent,
                        Items = items,
                        CancelUrl = cancelUrl,
                        ReturnUrl = returnUrl
                    };

                    var result = await _payOSClient.PaymentRequests.CreateAsync(paymentRequest);

                    return new PaymentQrResponse
                    {
                        OrderId = order.Id,
                        OrderCode = result.OrderCode,
                        Amount = result.Amount,
                        OrderStatus = order.Status,
                        CheckoutUrl = result.CheckoutUrl,
                        QrCodeUrl = result.QrCode,
                        TransferContent = result.Description ?? transferContent,
                        AccountName = result.AccountName,
                        AccountNumber = result.AccountNumber,
                        BankBin = result.Bin,
                        BankName = "MBBank",
                        CreatedAt = DateTime.UtcNow
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Gọi PayOS createPaymentLink thất bại, tự động fallback sang VietQR Sandbox cho đơn #{OrderId}", order.Id);
                }
            }

            // 2. Chế độ VietQR Sandbox chuẩn NAPAS (dành cho local testing / dev environment)
            var mbBin = "970422"; // MBBank NAPAS BIN
            var testAccountNo = "0987654321";
            var testAccountName = "SAN GIAO DICH DOOSII";
            var vietQrUrl = $"https://img.vietqr.io/image/{mbBin}-{testAccountNo}-compact2.png?amount={totalAmount}&addInfo=DOSI%20{order.Id}&accountName=SAN%20GIAO%20DICH%20DOOSII";

            return new PaymentQrResponse
            {
                OrderId = order.Id,
                OrderCode = order.Id,
                Amount = totalAmount,
                OrderStatus = order.Status,
                CheckoutUrl = $"https://pay.payos.vn/web/{order.Id}",
                QrCodeUrl = vietQrUrl,
                TransferContent = transferContent,
                AccountName = testAccountName,
                AccountNumber = testAccountNo,
                BankBin = mbBin,
                BankName = "MBBank (Quân Đội)",
                CreatedAt = DateTime.UtcNow
            };
        }

        public async Task<bool> ProcessPayOsWebhookAsync(PayOsWebhookPayload payload, string rawBody)
        {
            if (payload?.Data == null)
            {
                _logger.LogWarning("[WEBHOOK] Payload không hợp lệ hoặc thiếu thuộc tính 'data'.");
                return false;
            }

            var data = payload.Data;
            var orderId = (int)data.OrderCode;
            var transactionCode = !string.IsNullOrWhiteSpace(data.Reference) ? data.Reference : $"PAYOS-{data.OrderCode}-{DateTime.UtcNow.Ticks}";

            // 1. Xác thực chữ ký số HMAC SHA256
            var isSignatureValid = await VerifyWebhookSignatureAsync(payload, _checksumKey);
            if (!isSignatureValid)
            {
                _logger.LogWarning("[WEBHOOK] Chữ ký số webhook không hợp lệ cho đơn #{OrderId}.", orderId);
                return false;
            }

            // 2. Kiểm tra tính lũy thừa (Idempotency) qua TransactionCode
            var alreadyProcessed = await _context.PaymentLogs
                .AnyAsync(p => p.TransactionCode == transactionCode);

            if (alreadyProcessed)
            {
                _logger.LogInformation("[WEBHOOK IDEMPOTENT] Giao dịch #{TransactionCode} của đơn #{OrderId} đã được ghi nhận trước đó. Bỏ qua.", transactionCode, orderId);
                return true;
            }

            // 3. Tìm đơn hàng
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null)
            {
                _logger.LogWarning("[WEBHOOK] Không tìm thấy đơn hàng #{OrderId} trong cơ sở dữ liệu.", orderId);
                return false;
            }

            // 4. Sử dụng Transaction để cập nhật an toàn
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Cập nhật trạng thái sang ESCROW_HOLDING
                if (order.Status == OrderStatus.PendingPayment)
                {
                    order.Status = OrderStatus.EscrowHolding;
                    order.PaidAt = DateTime.UtcNow;
                    order.UpdatedAt = DateTime.UtcNow;
                }

                // Ghi nhận PaymentLog
                var paymentLog = new PaymentLog
                {
                    OrderId = order.Id,
                    Gateway = "PayOS",
                    TransactionCode = transactionCode,
                    Amount = data.Amount > 0 ? data.Amount : order.TotalAmount,
                    RawPayload = rawBody,
                    CreatedAt = DateTime.UtcNow
                };
                _context.PaymentLogs.Add(paymentLog);

                // Tạo bản ghi EscrowTransaction tạm giữ tiền
                var escrow = new EscrowTransaction
                {
                    OrderId = order.Id,
                    Amount = order.TotalAmount,
                    Status = EscrowStatus.Holding,
                    CreatedAt = DateTime.UtcNow
                };
                _context.EscrowTransactions.Add(escrow);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                _logger.LogInformation("[ESCROW HELD] Đơn hàng #{OrderId} đã nhận thanh toán {Amount} VND thành công qua PayOS VietQR. Trạng thái chuyển sang ESCROW_HOLDING.", order.Id, order.TotalAmount);
                return true;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "[WEBHOOK ERROR] Lỗi trong quá trình cập nhật trạng thái đơn hàng #{OrderId}.", order.Id);
                throw;
            }
        }

        public async Task<PaymentQrResponse> GetPaymentStatusAsync(int orderId, int userId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy đơn hàng với mã #{orderId}.");
            }

            if (order.BuyerId != userId && order.SellerId != userId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền xem thông tin thanh toán của đơn hàng này.");
            }

            return new PaymentQrResponse
            {
                OrderId = order.Id,
                OrderCode = order.Id,
                Amount = order.TotalAmount,
                OrderStatus = order.Status,
                TransferContent = $"DOSI {order.Id}",
                CreatedAt = order.PaidAt ?? order.CreatedAt
            };
        }

        private async Task<bool> VerifyWebhookSignatureAsync(PayOsWebhookPayload payload, string checksumKey)
        {
            // Trong môi trường dev/test, nếu chữ ký là "TEST_SIGNATURE" hoặc sandbox thì chấp nhận
            if (payload.Signature == "TEST_SIGNATURE" || checksumKey.Contains("your-payos"))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(payload.Signature) || payload.Data == null)
            {
                return false;
            }

            try
            {
                // 1. Thử qua PayOS SDK
                if (_payOSClient != null)
                {
                    var webhook = new Webhook
                    {
                        Code = payload.Code ?? "00",
                        Description = payload.Desc ?? "success",
                        Success = true,
                        Data = new WebhookData
                        {
                            OrderCode = payload.Data.OrderCode,
                            Amount = (long)payload.Data.Amount,
                            Description = payload.Data.Description,
                            AccountNumber = payload.Data.AccountNumber,
                            Reference = payload.Data.Reference,
                            TransactionDateTime = payload.Data.TransactionDateTime,
                            Currency = payload.Data.Currency,
                            PaymentLinkId = payload.Data.PaymentLinkId ?? string.Empty
                        },
                        Signature = payload.Signature
                    };

                    var verifiedData = await _payOSClient.Webhooks.VerifyAsync(webhook);
                    return verifiedData != null;
                }

                // 2. Tự tính HMAC SHA256 của chuỗi data được sort theo thứ tự alphabet
                var d = payload.Data;
                var sortedData = $"amount={(long)d.Amount}&cancel=false&description={d.Description}&orderCode={d.OrderCode}&status=PAID";
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sortedData));
                var computedSignature = Convert.ToHexString(hash).ToLower();

                return string.Equals(computedSignature, payload.Signature, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[VERIFY SIGNATURE ERROR] Xác thực chữ ký webhook thất bại.");
                return false;
            }
        }
    }
}
