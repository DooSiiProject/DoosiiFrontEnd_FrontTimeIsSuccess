namespace Doosii.BLL.DTOs
{
    public class StoreLocationDto
    {
        public int Id { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Address { get; set; }
    }

    public class StoreDto
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? OpeningHours { get; set; }
        public bool IsActive { get; set; }
        public decimal RatingAverage { get; set; }
        public int RatingCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<StoreLocationDto> Locations { get; set; } = new();
    }
}