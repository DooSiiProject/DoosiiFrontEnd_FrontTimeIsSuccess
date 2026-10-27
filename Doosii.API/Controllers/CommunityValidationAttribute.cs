using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Doosii.BLL.DTOs;

namespace Doosii.API.Controllers;

// Runs before ApiController's automatic invalid-model filter, preserving the API envelope.
[AttributeUsage(AttributeTargets.Class)]
public sealed class CommunityValidationAttribute : ActionFilterAttribute
{
    public CommunityValidationAttribute() { Order = -3000; }
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState.Values.SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Du lieu khong hop le." : e.ErrorMessage).ToList();
            context.Result = new BadRequestObjectResult(ApiResponse.Fail("Du lieu khong hop le.", errors));
        }
    }
}
