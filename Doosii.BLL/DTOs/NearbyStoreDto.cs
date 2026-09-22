namespace Doosii.BLL.DTOs
{
    public class NearbyStoreDto
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? DistanceKm { get; set; }
        public decimal RatingAverage { get; set; }
        public int RatingCount { get; set; }
        public string? OpeningHours { get; set; }
        public string? GoogleMapsDirectionUrl { get; set; }
    }
}
