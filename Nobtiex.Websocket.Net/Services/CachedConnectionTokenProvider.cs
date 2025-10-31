using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nobtiex.Websocket.Net
{
    using Microsoft.Extensions.Logging;
    using Nobitex.Websocket.Net.Abstractions;
    using Nobtiex.Websocket.Net.Utilities;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class CachedConnectionTokenProvider : IConnectionTokenProvider
    {
        private readonly IConnectionTokenProvider _inner;
        private readonly TimeSpan _refreshMargin;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ILogger<CachedConnectionTokenProvider>? _logger;
        private string? _cached;
        private DateTimeOffset? _expiry;

        public CachedConnectionTokenProvider(IConnectionTokenProvider inner, TimeSpan? refreshMargin = null, ILogger<CachedConnectionTokenProvider>? logger = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _refreshMargin = refreshMargin ?? TimeSpan.FromSeconds(60);
            _logger = logger;
        }

        public async Task<string> GetConnectionTokenAsync(CancellationToken cancellationToken = default)
        {
            if (!JwtHelper.ShouldRefresh(_expiry, _refreshMargin) && !string.IsNullOrEmpty(_cached))
            {
                return _cached!;
            }

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!JwtHelper.ShouldRefresh(_expiry, _refreshMargin) && !string.IsNullOrEmpty(_cached))
                {
                    return _cached!;
                }

                var token = await _inner.GetConnectionTokenAsync(cancellationToken).ConfigureAwait(false);
                var expiry = JwtHelper.ParseExpiry(token);
                _cached = token;
                _expiry = expiry;

                _logger?.LogInformation("Fetched WS token; expiry: {expiry}", expiry?.ToString() ?? "unknown");
                return token;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
