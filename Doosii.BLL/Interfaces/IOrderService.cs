using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IOrderService
    {
        Task<OrderResponse> CreateEscrowOrderAsync(int buyerId, CreateEscrowOrderRequest request);
        Task<OrderResponse> GetOrderByIdAsync(int orderId, int userId);
        Task<List<OrderResponse>> GetMyPurchasesAsync(int buyerId);
        Task<List<OrderResponse>> GetMySalesAsync(int sellerId);
    }
}
