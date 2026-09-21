using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class GoogleLoginRequest
    {
        [Required(ErrorMessage = "IdToken không được để trống.")]
        public string IdToken { get; set; } = string.Empty;
    }
}
