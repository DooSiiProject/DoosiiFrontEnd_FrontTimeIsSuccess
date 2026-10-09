using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IAnnouncementService
    {
        Task<AnnouncementResponse> CreateAnnouncementAsync(int sellerId, CreateAnnouncementRequest request);
        Task<List<AnnouncementResponse>> GetSellerAnnouncementsAsync(int sellerId);
        Task<List<AnnouncementResponse>> GetUpcomingAnnouncementsAsync();
    }
}
