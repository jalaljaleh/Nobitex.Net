using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Wrap;
using System;
using System.Net;
using System.Net.Http;

namespace Nobitex.Net;
public sealed class RetryPolicies
{
    public AsyncPolicyWrap<HttpResponseMessage> PolicyWrap { get; }

    public RetryPolicies()
    {
        var jitter = Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromMilliseconds(200), retryCount: 4);

        var transient = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => (int)r.StatusCode >= 500 || r.StatusCode == (HttpStatusCode)429)
            .WaitAndRetryAsync(jitter, onRetry: (res, ts, retry, ctx) =>
            {
                // optionally log via injected logger in transport
            });

        var circuit = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => (int)r.StatusCode >= 500)
            .CircuitBreakerAsync(6, TimeSpan.FromSeconds(30));

        PolicyWrap = Policy.WrapAsync(transient, circuit);
    }
}
