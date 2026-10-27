using Doosii.BLL.DTOs;
using Microsoft.AspNetCore.Http;

namespace Doosii.BLL.Interfaces;

public interface ICommunityPostService
{
    Task<CommunityPostDto> CreateAsync(int actorId, CommunityPostWriteRequest request, IReadOnlyList<IFormFile> files);
    Task<CommunityPostDto> UpdateAsync(int postId, int actorId, CommunityPostWriteRequest request, IReadOnlyList<IFormFile> files);
    Task<CommunityPostDto> DetailAsync(int postId, int? viewerId, bool moderation = false);
    Task<CommunityPage<CommunityPostDto>> FeedAsync(CommunityPostQuery query, int? viewerId);
    Task<CommunityPostDto> VisibilityAsync(int postId, int actorId, CommunityVisibilityRequest request);
    Task<CommunityPostDto> SaleAsync(int postId, int actorId, CommunitySaleRequest request);
}
public interface ICommunityMediaStorage
{
    Task<CommunityUploadedMedia> UploadAsync(IFormFile file, string mediaType, string publicId);
    Task DeleteAsync(string publicId, string mediaType);
}
