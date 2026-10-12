using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface ICommunityFeedService
    {
        /// <summary>
        /// Lay danh sach Community Feed tong hop theo tab:
        /// - newest: tong hop ca PassPost va SpotSharingPost moi nhat.
        /// - near-you: loc bai theo toa do va ban kinh Haversine.
        /// - by-style: loc PassPost theo StyleTags khong phan biet hoa thuong.
        /// </summary>
        Task<CommunityFeedResponseDto> GetFeedAsync(CommunityFeedQuery query);
    }
}
