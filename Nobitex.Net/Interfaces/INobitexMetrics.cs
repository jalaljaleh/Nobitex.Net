using System;

namespace Nobitex.Net;
public interface INobitexMetrics
{
    void RequestStarted(string path, string method);
    void RequestFinished(string path, string method, int statusCode, TimeSpan duration);
    void Retry(string path, string method, int attempt);
}
