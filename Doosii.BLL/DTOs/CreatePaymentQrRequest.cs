using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class CreatePaymentQrRequest
    {
        [Required(ErrorMessage = "Mã đơn hàng là bắt buộc.")]
        public int OrderId { get; set; }
    }
}
