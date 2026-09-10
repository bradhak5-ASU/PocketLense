namespace PocketLense.Infrastructure.Services;

public class AppException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
