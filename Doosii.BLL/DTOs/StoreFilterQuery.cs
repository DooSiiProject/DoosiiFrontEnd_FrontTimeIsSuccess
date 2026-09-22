namespace Doosii.BLL.DTOs
{
    public class StoreFilterQuery
    {
        public string? Style { get; set; }
        public string? StoreType { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public decimal? MinRating { get; set; }
        public string? SortBy { get; set; }
        public double? Lat { get; set; }
        public double? Lng { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
