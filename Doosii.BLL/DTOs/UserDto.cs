namespace Doosii.BLL.DTOs
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsGoogle { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
