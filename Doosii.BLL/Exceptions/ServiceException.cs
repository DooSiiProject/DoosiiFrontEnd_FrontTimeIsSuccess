namespace Doosii.BLL.Exceptions
{
    public class ServiceException : Exception
    {
        public int StatusCode { get; }
        public string ErrorCode { get; }

        public ServiceException(int statusCode, string message, string errorCode) : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }
    }
}
