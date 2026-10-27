using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;
using Doosii.BLL.Exceptions;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Order;

namespace Doosii.BLL.Services;
public class PaymentService : IPaymentService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentService> _logger;
    private readonly PayOSClient? _payOSClient;
    private readonly string _checksumKey;
    private static bool Configured(string? value) => !string.IsNullOrWhiteSpace(value) && !value.Contains("your-payos", StringComparison.OrdinalIgnoreCase);
    public PaymentService(AppDbContext context, IConfiguration configuration, ILogger<PaymentService> logger, HttpClient? gatewayHttpClient = null)
    {
        _context = context; _configuration = configuration; _logger = logger;
        _checksumKey = configuration["PayOS:ChecksumKey"] ?? "";
        var client = configuration["PayOS:ClientId"]; var key = configuration["PayOS:ApiKey"];
        if (Configured(client) && Configured(key) && Configured(_checksumKey))
        {
            var options = new PayOSOptions { ClientId = client!, ApiKey = key!, ChecksumKey = _checksumKey, TimeoutMs = 10000, MaxRetries = 0 };
            if (gatewayHttpClient != null) options.HttpClient = gatewayHttpClient;
            _payOSClient = new PayOSClient(options);
        }
    }
    public async Task<PaymentQrResponse> CreatePaymentQrAsync(int orderId, int userId)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        var order = await OrderLifecycle.LockAsync(_context, orderId);
        if (order.BuyerId != userId || order.Buyer.Role == "Banned") throw new UnauthorizedAccessException("Chỉ người mua của đơn được tạo thanh toán.");
        if (await OrderLifecycle.ExpireAsync(_context, order)) { await tx.CommitAsync(); throw new InvalidOperationException("Đơn đã hết thời gian thanh toán."); }
        if (order.Status != OrderStatus.PendingPayment) throw new InvalidOperationException("Đơn không còn chờ thanh toán.");
        if (_payOSClient == null)
        {
            await tx.CommitAsync();
            return new PaymentQrResponse { OrderId = order.Id, OrderCode = order.Id, Amount = order.TotalAmount, OrderStatus = order.Status,
                IsSandbox = true, QrCodeData = $"DOOSII DEMO ORDER {order.Id} AMOUNT {order.TotalAmount.ToString(CultureInfo.InvariantCulture)}",
                ExpiresAt = OrderLifecycle.ExpiresAt(order), Message = "Mã minh họa, không dùng để chuyển khoản. Máy chủ chưa cấu hình PayOS." };
        }
        if (order.PaymentDetailsJson != null)
        {
            var cached = JsonSerializer.Deserialize<PaymentQrResponse>(order.PaymentDetailsJson)!;
            cached.OrderStatus = order.Status; cached.ServerTime = DateTime.UtcNow; await tx.CommitAsync(); return cached;
        }
        if (order.TotalAmount != decimal.Truncate(order.TotalAmount) || order.TotalAmount > long.MaxValue) throw new InvalidOperationException("Thanh toán yêu cầu số tiền VND nguyên.");
        CreatePaymentLinkResponse result;
        try
        {
            var request = new CreatePaymentLinkRequest { OrderCode = order.Id, Amount = (long)order.TotalAmount, Description = $"DOSI {order.Id}",
                ReturnUrl = _configuration["PayOS:ReturnUrl"] ?? "http://localhost:5173/payment/success",
                CancelUrl = _configuration["PayOS:CancelUrl"] ?? "http://localhost:5173/payment/cancel",
                ExpiredAt = (int)new DateTimeOffset(OrderLifecycle.ExpiresAt(order)).ToUnixTimeSeconds() };
            try { result = await _payOSClient.PaymentRequests.CreateAsync(request); }
            catch (Exception createError)
            {
                _logger.LogInformation(createError, "Checking existing PayOS link after create failure for order {OrderId}", order.Id);
                // A previous request may have reached PayOS before the connection was interrupted.
                var link = await _payOSClient.PaymentRequests.GetAsync((long)order.Id);
                if (link.OrderCode != order.Id || link.Amount != order.TotalAmount || link.Status != PaymentLinkStatus.Pending) throw;
                // Reuse the provider's hosted page when recovering; never invent bank/QR information.
                result = new CreatePaymentLinkResponse { OrderCode = link.OrderCode, Amount = link.Amount, PaymentLinkId = link.Id,
                    CheckoutUrl = "https://pay.payos.vn/web/" + link.Id, Description = $"DOSI {order.Id}" };
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "PayOS unavailable for order {OrderId}", order.Id); throw new ServiceException(503, "Không tạo được thanh toán. Vui lòng thử lại sau.", "PAYMENT_UNAVAILABLE"); }
        if (OrderLifecycle.ExpiresAt(order) <= DateTime.UtcNow) { await OrderLifecycle.ExpireAsync(_context, order); await tx.CommitAsync(); throw new InvalidOperationException("Đơn đã hết hạn trong lúc tạo thanh toán."); }
        if (result.OrderCode != order.Id || result.Amount != order.TotalAmount || string.IsNullOrWhiteSpace(result.PaymentLinkId)) throw new ServiceException(502, "Thông tin thanh toán từ cổng không khớp đơn hàng.", "PAYMENT_MISMATCH");
        var response = new PaymentQrResponse { OrderId = order.Id, OrderCode = result.OrderCode, Amount = result.Amount, OrderStatus = order.Status,
            CheckoutUrl = result.CheckoutUrl ?? "", QrCodeData = result.QrCode ?? "", TransferContent = result.Description ?? $"DOSI {order.Id}",
            AccountName = result.AccountName ?? "", AccountNumber = result.AccountNumber ?? "", BankBin = result.Bin ?? "", BankName = "",
            ExpiresAt = OrderLifecycle.ExpiresAt(order) };
        order.PaymentLinkId = result.PaymentLinkId; order.PaymentDetailsJson = JsonSerializer.Serialize(response);
        await _context.SaveChangesAsync(); await tx.CommitAsync(); return response;
    }
    public async Task<bool> ProcessPayOsWebhookAsync(PayOsWebhookPayload payload, string rawBody)
    {
        if (!VerifySignature(payload.Signature, rawBody) || payload.Data == null || !payload.Success || payload.Code != "00" || payload.Data.Code != "00") return false;
        var data = payload.Data;
        if (data.OrderCode <= 0 || data.OrderCode > int.MaxValue || data.Currency != "VND" || string.IsNullOrWhiteSpace(data.Reference) || data.Reference.Length > 100 || data.Amount <= 0) return false;
        await using var tx = await _context.Database.BeginTransactionAsync();
        Order order;
        try { order = await OrderLifecycle.LockAsync(_context, (int)data.OrderCode); } catch (KeyNotFoundException) { _logger.LogWarning("Verified payment event for unknown order {OrderId}", data.OrderCode); return true; }
        var previous = await _context.PaymentLogs.AsNoTracking().FirstOrDefaultAsync(p => p.Gateway == "PayOS" && p.TransactionCode == data.Reference);
        if (previous != null) return previous.OrderId == order.Id && previous.Amount == data.Amount;
        if (string.IsNullOrEmpty(order.PaymentLinkId) || order.PaymentLinkId != data.PaymentLinkId) return false;
        await OrderLifecycle.ExpireAsync(_context, order);
        var applied = order.Status == OrderStatus.PendingPayment && data.Amount == order.TotalAmount;
        var payment = order.PaymentDetailsJson == null ? null : JsonSerializer.Deserialize<PaymentQrResponse>(order.PaymentDetailsJson);
        if (payment == null || payment.IsSandbox || (!string.IsNullOrEmpty(payment.AccountNumber) && payment.AccountNumber != data.AccountNumber)) return false;
        foreach (var item in order.Items)
        {
            var product = await _context.Products.FromSqlInterpolated($"SELECT * FROM [store].[Products] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {item.ProductId}").SingleOrDefaultAsync();
            if (product?.Status != "LOCKED" || product.ReservedOrderId != order.Id) applied = false;
        }
        if (applied)
        {
            order.Status = OrderStatus.EscrowHolding; order.PaidAt = DateTime.UtcNow; order.UpdatedAt = DateTime.UtcNow;
            _context.EscrowTransactions.Add(new EscrowTransaction { OrderId = order.Id, Amount = order.TotalAmount, Status = EscrowStatus.Holding });
        }
        _context.PaymentLogs.Add(new PaymentLog { OrderId = order.Id, Gateway = "PayOS", TransactionCode = data.Reference, Amount = data.Amount,
            RawPayload = rawBody, ProcessingStatus = applied ? "APPLIED" : "REVIEW_REQUIRED" });
        await _context.SaveChangesAsync(); await tx.CommitAsync();
        if (!applied) _logger.LogWarning("Payment for order {OrderId} requires reconciliation; order was not credited", order.Id);
        return true;
    }
    public async Task<PaymentQrResponse> GetPaymentStatusAsync(int orderId, int userId)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        var order = await OrderLifecycle.LockAsync(_context, orderId);
        if (order.BuyerId != userId && order.SellerId != userId) throw new UnauthorizedAccessException("Bạn không có quyền xem thanh toán của đơn.");
        await OrderLifecycle.ExpireAsync(_context, order); await tx.CommitAsync();
        return new PaymentQrResponse { OrderId = order.Id, Amount = order.TotalAmount, OrderStatus = order.Status, ExpiresAt = OrderLifecycle.ExpiresAt(order) };
    }
    private bool VerifySignature(string? signature, string rawBody)
    {
        if (!Configured(_checksumKey) || string.IsNullOrEmpty(signature)) return false;
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var data = document.RootElement.GetProperty("data");
            var fields = data.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => p.Name + "=" + Value(p.Value));
            var signed = Encoding.UTF8.GetBytes(string.Join("&", fields));
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_checksumKey), signed);
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature));
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or KeyNotFoundException) { return false; }
    }
    private static string Value(JsonElement value) => value.ValueKind switch {
        JsonValueKind.Null => "", JsonValueKind.String => value.GetString() is "null" or "undefined" ? "" : value.GetString()!,
        JsonValueKind.Number => value.GetDecimal().ToString("G29", CultureInfo.InvariantCulture),
        JsonValueKind.True => "true", JsonValueKind.False => "false", _ => throw new InvalidOperationException("Unsupported signature field")
    };
}
