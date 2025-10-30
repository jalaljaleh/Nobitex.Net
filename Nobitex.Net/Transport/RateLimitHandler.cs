using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;

/// <summary>
/// Simple per-endpoint token-bucket rate limiter as an HttpMessageHandler.
/// Configure limits by path prefix. This is lightweight and in-memory.
/// For distributed scenarios use a remote limiter (Redis, etc).
/// </summary>
public class RateLimitHandler : DelegatingHandler
{
    private readonly ILogger<RateLimitHandler> _logger;
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();

    public RateLimitHandler(ILogger<RateLimitHandler> logger)
    {
        _logger = logger;
    }

    public void ConfigureBucket(string pathPrefix, int maxTokens, TimeSpan refillPeriod)
    {
        _buckets[pathPrefix] = new TokenBucket(maxTokens, refillPeriod);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // match the most specific path prefix
        var path = request.RequestUri?.AbsolutePath ?? "/";
        foreach (var kv in _buckets)
        {
            if (path.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
            {
                var bucket = kv.Value;
                var ok = await bucket.ConsumeAsync(cancellationToken);
                if (!ok)
                {
                    _logger.LogWarning("Rate limit exhausted for {Path}", kv.Key);
                    // instead of waiting indefinitely, return 429-like behavior by throwing
                    var resp = new HttpResponseMessage((System.Net.HttpStatusCode)429)
                    {
                        RequestMessage = request,
                        ReasonPhrase = "Rate limit exceeded (client-side)"
                    };
                    return resp;
                }
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private class TokenBucket
    {
        private readonly SemaphoreSlim _sem = new SemaphoreSlim(0);
        private volatile int _tokens;
        private readonly int _max;
        private readonly TimeSpan _refillPeriod;
        private DateTime _lastRefill;

        public TokenBucket(int maxTokens, TimeSpan refillPeriod)
        {
            _max = maxTokens;
            _refillPeriod = refillPeriod;
            _tokens = maxTokens;
            _lastRefill = DateTime.UtcNow;
        }

        public Task<bool> ConsumeAsync(CancellationToken ct)
        {
            lock (this)
            {
                RefillIfNeeded();
                if (_tokens > 0)
                {
                    _tokens--;
                    return Task.FromResult(true);
                }
            }
            return Task.FromResult(false);
        }

        private void RefillIfNeeded()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastRefill) >= _refillPeriod)
            {
                _tokens = _max;
                _lastRefill = now;
            }
        }
    }
}
