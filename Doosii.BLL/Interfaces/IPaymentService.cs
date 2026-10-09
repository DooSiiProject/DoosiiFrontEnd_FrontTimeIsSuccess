using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IPaymentService
    {
        Task<PaymentQrResponse> CreatePaymentQrAsync(int orderId, int userId);
        Task<bool> ProcessPayOsWebhookAsync(PayOsWebhookPayload payload, string rawBody);
        Task<PaymentQrResponse> GetPaymentStatusAsync(int orderId, int userId);
    }
}
