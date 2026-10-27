using Doosii.BLL.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Doosii.API.Controllers;

/// <summary>
/// B1/B2 replaces the empty community schema. Old services must not query retired tables.
/// Remove this gate as each controller is ported to unified Posts in B3/B4.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class LegacyCommunityUnavailableAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        context.Result = new ObjectResult(ApiResponse.Fail(
            "Tinh nang cong dong dang duoc nang cap. Vui long thu lai sau.",
            new List<string> { "COMMUNITY_UPGRADE_PENDING" })) { StatusCode = 503 };
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
