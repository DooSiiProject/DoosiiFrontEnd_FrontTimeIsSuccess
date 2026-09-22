using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers
{
    [ApiController]
    [Route("api/map")]
    public class MapController : ControllerBase
    {
        private readonly IStoreService _storeService;

        public MapController(IStoreService storeService)
        {
            _storeService = storeService;
        }

        /// <summary>
        /// Tim kiem cac cua hang gan nhat theo toa do va ban kinh (Haversine).
        /// </summary>
        [HttpGet("nearby-stores")]
        public async Task<IActionResult> GetNearbyStores(
            [FromQuery] double? lat,
            [FromQuery] double? lng,
            [FromQuery] int? radiusKm = 5,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            // 1. Validate toa do
            if (!lat.HasValue || !lng.HasValue || lat.Value < -90 || lat.Value > 90 || lng.Value < -180 || lng.Value > 180)
            {
                return BadRequest(ApiResponse.Fail("Invalid coordinates", new List<string> { "INVALID_COORDINATES" }));
            }

            // 2. Validate ban kinh (chi chap nhan 1, 3, 5, 10)
            var allowedRadius = new[] { 1, 3, 5, 10 };
            var effectiveRadius = radiusKm.GetValueOrDefault(5);
            if (radiusKm.HasValue && !allowedRadius.Contains(radiusKm.Value))
            {
                return BadRequest(ApiResponse.Fail("Invalid radius. Allowed values: 1, 3, 5, 10.", new List<string> { "INVALID_RADIUS" }));
            }

            // 3. Validate phan trang
            if (page < 1 || pageSize < 1)
            {
                return BadRequest(ApiResponse.Fail("Invalid pagination parameters", new List<string> { "INVALID_PAGINATION" }));
            }

            var query = new NearbyStoreQuery
            {
                Lat = lat.Value,
                Lng = lng.Value,
                RadiusKm = effectiveRadius,
                Page = page,
                PageSize = pageSize
            };

            var result = await _storeService.GetNearbyStoresAsync(query);
            return Ok(ApiResponse.Ok(result, "Nearby stores retrieved successfully"));
        }
    }
}
