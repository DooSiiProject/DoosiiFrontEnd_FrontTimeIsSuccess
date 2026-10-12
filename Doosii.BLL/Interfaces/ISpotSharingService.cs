using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface ISpotSharingService
    {
        /// <summary>
        /// Tao moi bai Spot Sharing Post voi anh, upload len Cloudinary folder 'forum/spots'.
        /// Validate TaggedStoreId hoac (Latitude + Longitude).
        /// Rollback anh tren Cloudinary neu database ghi that bai.
        /// </summary>
        Task<CreateSpotPostResponse> CreateSpotPostAsync(int authorId, CreateSpotPostRequest request);

        /// <summary>
        /// Lay chi tiet bai Spot Sharing theo ID.
        /// Tra ve null neu bai khong ton tai hoac Status = HIDDEN.
        /// </summary>
        Task<SpotPostDetailDto?> GetSpotPostByIdAsync(int postId);

        /// <summary>
        /// Lay danh sach Spot Post theo storeId (phan trang, moi nhat truoc).
        /// Tra ve danh sach rong neu store chua co bai.
        /// Nem ServiceException(404) neu store khong ton tai.
        /// </summary>
        Task<SpotPostPagedResult> GetSpotPostsByStoreAsync(int storeId, int page, int pageSize);
    }
}
