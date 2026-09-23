namespace Doosii.BLL.DTOs
{
    public class NearbyStoreQuery
    {
        public double? Lat { get; set; }
        public double? Lng { get; set; }
        public int? RadiusKm { get; set; } = 5;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
