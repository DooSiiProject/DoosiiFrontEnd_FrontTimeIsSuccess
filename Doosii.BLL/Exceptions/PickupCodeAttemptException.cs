namespace Doosii.BLL.Exceptions;

public sealed class PickupCodeAttemptException(int status, string message, string code, string version, int retryAfterSeconds = 0)
    : ServiceException(status, message, code)
{
    public string Version { get; } = version;
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
