using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers
{
    [ApiController]
    [Route("api/stores")]
    public class StoresController : ControllerBase
    {
        private readonly IStoreService _storeService;
        private readonly IProductService _productService;

        public StoresController(IStoreService storeService, IProductService productService)
        {
            _storeService = storeService;
            _productService = productService;
        }

        /// <summary>
        /// Loc danh sach cac cua hang (public).
        /// Ho tro loc theo style, price, rating, sap xep theo distance, rating, newest.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetStores([FromQuery] StoreFilterQuery query)
        {
            // 1. Validate pagination
            if (query.Page < 1 || query.PageSize < 1)
            {
                return BadRequest(ApiResponse.Fail("Invalid pagination parameters", new List<string> { "INVALID_PAGINATION" }));
            }

            // 2. Validate coordinates neu co truyen
            if (query.Lat.HasValue || query.Lng.HasValue)
            {
                if (!query.Lat.HasValue || !query.Lng.HasValue ||
                    query.Lat.Value < -90 || query.Lat.Value > 90 ||
                    query.Lng.Value < -180 || query.Lng.Value > 180)
                {
                    return BadRequest(ApiResponse.Fail("Invalid coordinates", new List<string> { "INVALID_COORDINATES" }));
                }
            }

            // 3. Validate sort by distance yeu cau toa do
            var isSortByDistance = string.Equals(query.SortBy, "distance", StringComparison.OrdinalIgnoreCase);
            if (isSortByDistance && (!query.Lat.HasValue || !query.Lng.HasValue))
            {
                return BadRequest(ApiResponse.Fail("Coordinates are required when sorting by distance", new List<string> { "MISSING_COORDINATES_FOR_DISTANCE_SORT" }));
            }

            // 4. Validate price range
            if ((query.MinPrice.HasValue && query.MinPrice < 0) ||
                (query.MaxPrice.HasValue && query.MaxPrice < 0) ||
                (query.MinPrice.HasValue && query.MaxPrice.HasValue && query.MinPrice > query.MaxPrice))
            {
                return BadRequest(ApiResponse.Fail("Invalid price range", new List<string> { "INVALID_PRICE_RANGE" }));
            }

            // 5. Validate rating
            if (query.MinRating.HasValue && (query.MinRating < 0 || query.MinRating > 5))
            {
                return BadRequest(ApiResponse.Fail("Invalid rating filter", new List<string> { "INVALID_RATING" }));
            }

            var result = await _storeService.GetStoresAsync(query);
            return Ok(ApiResponse.Ok(result, "Stores retrieved successfully"));
        }

        /// <summary>
        /// Tao cua hang moi. Chi Seller da KYC APPROVED moi duoc tao.
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateStore([FromBody] CreateStoreRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Du lieu khong hop le.", GetModelErrors()));

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse.Fail("Token khong hop le."));

            try
            {
                var result = await _storeService.CreateStoreAsync(userId.Value, request);
                return StatusCode(201, ApiResponse.Ok(result, "Tao cua hang thanh cong."));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
            catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse.Fail(ex.Message)); }
            catch (InvalidOperationException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        }

        /// <summary>
        /// Cap nhat thong tin cua hang. Chi owner moi duoc cap nhat.
        /// </summary>
        [HttpPut("{storeId:int}")]
        [Authorize]
        public async Task<IActionResult> UpdateStore(int storeId, [FromBody] UpdateStoreRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Du lieu khong hop le.", GetModelErrors()));

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse.Fail("Token khong hop le."));

            try
            {
                var result = await _storeService.UpdateStoreAsync(userId.Value, storeId, request);
                return Ok(ApiResponse.Ok(result, "Cap nhat cua hang thanh cong."));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
            catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse.Fail(ex.Message)); }
        }

        /// <summary>
        /// Lay thong tin cua hang (public).
        /// </summary>
        [HttpGet("{storeId:int}")]
        public async Task<IActionResult> GetStore(int storeId)
        {
            try
            {
                var result = await _storeService.GetStoreAsync(storeId);
                return Ok(ApiResponse.Ok(result, "Lay thong tin cua hang thanh cong."));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        }

        /// <summary>
        /// Lay danh sach san pham cua cua hang (public, chi AVAILABLE, co phan trang).
        /// </summary>
        [HttpGet("{storeId:int}/products")]
        public async Task<IActionResult> GetStoreProducts(int storeId, [FromQuery] int page = 1, [FromQuery] int pageSize = 12)
        {
            if (page < 1) return BadRequest(ApiResponse.Fail("page phai lon hon hoac bang 1."));
            if (pageSize < 1 || pageSize > 24) return BadRequest(ApiResponse.Fail("pageSize phai tu 1 den 24."));

            try
            {
                var result = await _productService.GetStoreProductsAsync(storeId, page, pageSize);
                return Ok(ApiResponse.Ok(result, "Lay danh sach san pham thanh cong."));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        }

        /// <summary>
        /// Them san pham vao cua hang. Chi owner Seller KYC APPROVED.
        /// </summary>
        [HttpPost("{storeId:int}/products")]
        [Authorize]
        public async Task<IActionResult> CreateProduct(int storeId, [FromBody] CreateProductRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Du lieu khong hop le.", GetModelErrors()));

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(ApiResponse.Fail("Token khong hop le."));

            try
            {
                var result = await _productService.CreateProductAsync(userId.Value, storeId, request);
                return StatusCode(201, ApiResponse.Ok(result, "Them san pham thanh cong."));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
            catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse.Fail(ex.Message)); }
            catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
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
