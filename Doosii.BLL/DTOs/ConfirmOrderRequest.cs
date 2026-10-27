using System.ComponentModel.DataAnnotations;
namespace Doosii.BLL.DTOs;
public class ConfirmOrderRequest { [Required] public string Version { get; set; } = ""; }
