namespace Doosii.BLL.Interfaces
{
    public interface IProductLockService
    {
        Task<bool> LockProductAsync(int productId, int orderId);
        Task UnlockProductAsync(int productId, int? orderId = null);
        Task MarkProductAsSoldAsync(int productId, int? orderId = null);
    }
}
