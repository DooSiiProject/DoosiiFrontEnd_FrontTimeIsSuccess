using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Doosii.BLL.DTOs
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class UpdateStoreRequest
    {
        [Required(ErrorMessage = "Ten cua hang la bat buoc.")]
        [MaxLength(200, ErrorMessage = "Ten cua hang khong duoc qua 200 ky tu.")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(20)]
        [RegularExpression(@"\+?[0-9 ()-]{7,20}", ErrorMessage = "So dien thoai khong hop le.")]
        public string? Phone { get; set; }

        [MaxLength(500)]
        public string? OpeningHours { get; set; }

    }
}