namespace Doosii.BLL.DTOs
{
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
        public List<string> Errors { get; set; } = new();

        public static ApiResponse Ok(object data, string message = "Thanh cong.") =>
            new() { Success = true, Message = message, Data = data };

        public static ApiResponse Fail(string message, List<string>? errors = null) =>
            new() { Success = false, Message = message, Errors = errors ?? new() };
    }
}