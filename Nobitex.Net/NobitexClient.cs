using Microsoft.Extensions.DependencyInjection;
using Nobitex.Net;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net
{
    public class NobitexClient : IDisposable
    {
        public Nobitex.Net.AccountClient AccountClient { get; private set; }
        public Nobitex.Net.MarketClient MarketClient { get; private set; }
        public Nobitex.Net.OrderBookClient OrderBookClient { get; private set; }
        public Nobitex.Net.TradesClient TradesClient { get; private set; }
        public Nobitex.Net.WalletClient WalletClient { get; private set; }

        private readonly string _token;
        private ServiceProvider? _serviceProvider;
        private bool _initialized;
        private bool _disposed;

        public NobitexClient(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token must be provided", nameof(token));

            _token = token;
        }

        /// <summary>
        /// Initializes the underlying HTTP clients and DI container.
        /// Must be called once before accessing client properties.
        /// Safe to call multiple times; subsequent calls are no-ops.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_initialized) return;

            var services = new ServiceCollection();

            // Configure the Nobitex clients — adjust AddNobitexClient signature to your library
            services.AddNobitexClient(opts =>
            {
                opts.BaseUrl = "https://apiv2.nobitex.ir";
                opts.Token = _token;
                opts.UserAgent = "Nobitex.Net/ClientSample/1.0";
                opts.Timeout = TimeSpan.FromSeconds(20);
            });

            // If the library exposes typed clients via interfaces, prefer those to concrete types
            _serviceProvider = services.BuildServiceProvider();

            // Resolve and assign readonly clients
            AccountClient = _serviceProvider.GetRequiredService<Nobitex.Net.AccountClient>();
            MarketClient = _serviceProvider.GetRequiredService<Nobitex.Net.MarketClient>();
            OrderBookClient = _serviceProvider.GetRequiredService<Nobitex.Net.OrderBookClient>();
            TradesClient = _serviceProvider.GetRequiredService<Nobitex.Net.TradesClient>();
            WalletClient = _serviceProvider.GetRequiredService<Nobitex.Net.WalletClient>();

            // small async yield in case underlying registrations perform startup work
            await Task.Yield();

            _initialized = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NobitexClient));
        }

        /// <summary>
        /// Dispose the built service provider and its resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _serviceProvider?.Dispose();
            _disposed = true;
        }
    }
}
