using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers
{
    [ApiController]
    [Route("api/products")]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        /// <summary>
        /// Cap nhat san pham. Chi owner. Khong chinh sua LOCKED/SOLD.
        /// </summary>
        [HttpPut("{productId:int}")]
        public async Task<IActionResult> UpdateProduct(int productId, [FromBody] UpdateProductRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Du lieu khong hop le.", GetModelErrors()));

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse.Fail("Token khong hop le."));

            try
            {
                var result = await _productService.UpdateProductAsync(userId.Value, productId, request);
                return Ok(ApiResponse.Ok(result, "Cap nhat san pham thanh cong."));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
            catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse.Fail(ex.Message)); }
            catch (InvalidOperationException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
            catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        }

        /// <summary>
        /// Xoa san pham. Chi owner. Khong xoa LOCKED/SOLD.
        /// </summary>
        [HttpDelete("{productId:int}")]
        public async Task<IActionResult> DeleteProduct(int productId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse.Fail("Token khong hop le."));

            try
            {
                await _productService.DeleteProductAsync(userId.Value, productId);
                return Ok(ApiResponse.Ok(new { }, "Xoa san pham thanh cong."));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
            catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse.Fail(ex.Message)); }
            catch (InvalidOperationException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out int id)) return null;
            return id;
        }

        private List<string> GetModelErrors() =>
            ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
    }
}