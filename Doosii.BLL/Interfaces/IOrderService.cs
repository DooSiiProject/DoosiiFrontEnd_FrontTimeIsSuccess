using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IOrderService
    {
        Task<OrderQuoteResponse> QuoteAsync(int buyerId, int productId);
        Task<OrderQuoteResponse> QuotePassCodAsync(int buyerId, int postId);
        Task<OrderResponse> CreatePassCodAsync(int buyerId, CreatePassCodRequest request);
        Task<OrderResponse> CreateEscrowOrderAsync(int buyerId, CreateEscrowOrderRequest request);
        Task<OrderResponse> GetOrderByIdAsync(int orderId, int userId);
        Task<List<OrderResponse>> GetMyPurchasesAsync(int buyerId);
        Task<List<OrderResponse>> GetMySalesAsync(int sellerId);
        Task<OrderResponse> ShipOrderAsync(int orderId, int sellerId, ShipOrderRequest request);
        Task<OrderResponse> ConfirmOrderReceivedAsync(int orderId, int buyerId, ConfirmOrderRequest request);
        Task<int> CancelExpiredOrdersAsync();
        Task<OrderResponse> CancelAsync(int orderId, int buyerId, ConfirmOrderRequest request);
        Task<OrderResponse> CollectCodAsync(int orderId, int sellerId, ConfirmOrderRequest request);
        Task<int> AutoCompleteDeliveredOrdersAsync();
        Task<DisputeResponse> CreateDisputeAsync(int orderId, int buyerId, CreateDisputeRequest request);
        Task<DisputeResponse?> GetOrderDisputeAsync(int orderId, int userId);
    }
}
