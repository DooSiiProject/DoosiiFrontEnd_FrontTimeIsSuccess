namespace Doosii.BLL.Interfaces
{
    public interface IProductLockService
    {
        Task<bool> LockProductAsync(int productId, int orderId);
        Task UnlockProductAsync(int productId);
        Task MarkProductAsSoldAsync(int productId);
    }
}
