using System;
namespace Nobitex.Net;
public class NobitexApiException : Exception
{
    public int? StatusCode { get; }
    public string? ApiCode { get; }
    public NobitexApiException(string message, int? statusCode = null, string? apiCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ApiCode = apiCode;
    }
}
