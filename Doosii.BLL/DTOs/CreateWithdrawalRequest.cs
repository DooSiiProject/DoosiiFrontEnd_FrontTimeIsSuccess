using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class CreateWithdrawalRequest
    {
        [Required(ErrorMessage = "Tên ngân hàng nhận tiền là bắt buộc.")]
        [MaxLength(100, ErrorMessage = "Tên ngân hàng tối đa 100 ký tự.")]
        public string BankName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số tài khoản ngân hàng là bắt buộc.")]
        [MaxLength(50, ErrorMessage = "Số tài khoản tối đa 50 ký tự.")]
        public string AccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên chủ tài khoản ngân hàng là bắt buộc.")]
        [MaxLength(100, ErrorMessage = "Tên chủ tài khoản tối đa 100 ký tự.")]
        public string AccountHolder { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số tiền rút là bắt buộc.")]
        [Range(50000, 100000000, ErrorMessage = "Số tiền rút tối thiểu là 50,000 VND và tối đa 100,000,000 VND.")]
        public decimal Amount { get; set; }
    }
}
