using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IPassPostService
    {
        /// <summary>
        /// Tao moi bai PassPost voi anh, upload len Cloudinary.
        /// Rollback anh tren Cloudinary neu database ghi that bai.
        /// </summary>
        Task<CreatePassPostResponse> CreatePassPostAsync(int authorId, CreatePassPostRequest request);

        /// <summary>
        /// Lay chi tiet mot bai PassPost theo ID.
        /// Tra ve null neu post khong ton tai hoac co status HIDDEN.
        /// </summary>
        Task<PassPostDetailDto?> GetPassPostByIdAsync(int postId);
    }
}
