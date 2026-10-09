using Microsoft.AspNetCore.Http;
using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface ICloudinaryService
    {
        /// <summary>
        /// Upload mot anh len Cloudinary.
        /// </summary>
        /// <param name="file">File tu IFormFile.</param>
        /// <param name="subFolder">Thu muc con ben trong folder chinh (vd: "products", "stores", "reviews").</param>
        /// <returns>Thong tin anh da upload.</returns>
        Task<ImageUploadResult> UploadImageAsync(IFormFile file, string subFolder = "");

        /// <summary>
        /// Upload nhieu anh len Cloudinary (1-5 file).
        /// </summary>
        /// <param name="files">Danh sach file tu IFormFile.</param>
        /// <param name="subFolder">Thu muc con ben trong folder chinh.</param>
        /// <returns>Danh sach thong tin anh da upload.</returns>
        Task<List<ImageUploadResult>> UploadImagesAsync(IList<IFormFile> files, string subFolder = "");

        /// <summary>
        /// Xoa mot anh tren Cloudinary theo publicId.
        /// </summary>
        /// <param name="publicId">Public ID tra ve luc upload.</param>
        Task DeleteImageAsync(string publicId);

        /// <summary>
        /// Kiem tra Cloudinary da duoc cau hinh hay chua.
        /// </summary>
        bool IsConfigured { get; }
    }
}
