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

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Catalog([FromQuery] ProductCatalogQuery query, CancellationToken cancellationToken)
        {
            var errors = query.Validate();
            if (errors.Count > 0) return BadRequest(ApiResponse.Fail("Bộ lọc sản phẩm không hợp lệ.", errors));
            return Ok(ApiResponse.Ok(await _productService.GetCatalogAsync(query, cancellationToken), "Lấy danh sách sản phẩm thành công."));
        }
        [HttpGet("mine")]
        public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 12, [FromQuery] string? state = null)
        {
            if (page < 1 || pageSize < 1 || pageSize > 24 || (state != null && !new[] { "AVAILABLE", "HIDDEN", "LOCKED", "SOLD" }.Contains(state))) return BadRequest(ApiResponse.Fail("Bộ lọc hoặc phân trang không hợp lệ."));
            var id = GetCurrentUserId(); if (id == null) return Unauthorized(ApiResponse.Fail("Token không hợp lệ."));
            return Ok(ApiResponse.Ok(await _productService.GetMyProductsAsync(id.Value, page, pageSize, state)));
        }
        [HttpGet("{productId:int}/manage")]
        public async Task<IActionResult> Manage(int productId)
        {
            var id = GetCurrentUserId(); if (id == null) return Unauthorized(ApiResponse.Fail("Token không hợp lệ."));
            try { return Ok(ApiResponse.Ok(await _productService.GetManagedProductAsync(id.Value, productId))); }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
            catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse.Fail(ex.Message)); }
        }
        [HttpPatch("{productId:int}/publication")]
        public async Task<IActionResult> Publication(int productId, [FromBody] ProductPublicationRequest request)
        {
            var id = GetCurrentUserId(); if (id == null) return Unauthorized(ApiResponse.Fail("Token không hợp lệ."));
            try { return Ok(ApiResponse.Ok(await _productService.SetPublicationAsync(id.Value, productId, request))); }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
            catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse.Fail(ex.Message)); }
            catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
        }

        [HttpGet("categories")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategories()
        {
            var result = await _productService.GetCategoriesAsync();
            return Ok(ApiResponse.Ok(result, "Lấy danh mục sản phẩm thành công."));
        }

        /// <summary>
        /// Xem chi tiet mot san pham (public). Khong can dang nhap.
        /// Product AVAILABLE / LOCKED / SOLD deu xem duoc, tra dung status.
        /// Tra 404 neu product khong ton tai hoac store da inactive.
        /// </summary>
        [HttpGet("{productId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProductDetail(int productId)
        {
            try
            {
                var result = await _productService.GetProductDetailAsync(productId);
                return Ok(ApiResponse.Ok(result, "Product details retrieved successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                var errorCode = ex.Message switch
                {
                    "PRODUCT_NOT_FOUND" => "PRODUCT_NOT_FOUND",
                    "STORE_NOT_ACTIVE"  => "STORE_NOT_ACTIVE",
                    _                   => "NOT_FOUND"
                };
                var userMsg = ex.Message switch
                {
                    "PRODUCT_NOT_FOUND" => "Product not found",
                    "STORE_NOT_ACTIVE"  => "Store is no longer active",
                    _                   => ex.Message
                };
                return NotFound(ApiResponse.Fail(userMsg, new List<string> { errorCode }));
            }
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
            catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
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
            catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
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

