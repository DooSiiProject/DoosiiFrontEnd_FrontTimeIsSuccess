using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IPostReportService
    {
        /// <summary>
        /// Nguoi dung gui bao cao vi pham cho mot bai viet (PassPost hoac SpotSharingPost).
        /// </summary>
        Task<PostReportResponseDto> CreateReportAsync(int postId, int reporterId, CreatePostReportRequest request);

        /// <summary>
        /// Lay danh sach moderation queue cho Admin voi filter status, reason va phan trang.
        /// </summary>
        Task<PagedResult<PostReportResponseDto>> GetReportsQueueAsync(PostReportQuery query);

        /// <summary>
        /// Admin xu ly bao cao (DISMISS, HIDE_POST, DELETE_POST, BAN_AUTHOR).
        /// Su dung database transaction.
        /// </summary>
        Task<PostReportResponseDto> ResolveReportAsync(int reportId, int adminId, ResolvePostReportRequest request);
    }
}
