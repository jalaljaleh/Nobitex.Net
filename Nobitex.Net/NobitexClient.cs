using Microsoft.Extensions.DependencyInjection;
using Nobitex.Net;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net
{
    /// <summary>
    /// Facade that aggregates the Nobitex typed clients and provides a simple entry point
    /// for standalone usage (console apps, tools, tests).
    /// </summary>
    /// <remarks>
    /// This class builds a private <see cref="ServiceProvider"/> to resolve the typed
    /// clients exposed by the Nobitex library. It is intended to be short-lived in small
    /// tools or used as a convenience wrapper. For long-running applications prefer
    /// registering the library directly into the application's DI container and injecting
    /// required clients instead of using this wrapper.
    /// </remarks>
    public class NobitexClient : IDisposable
    {
        /// <summary>
        /// Account related endpoints (user profile, wallets, cards, etc.).
        /// Resolved from the internal <see cref="ServiceProvider"/> during construction.
        /// </summary>
        public readonly Nobitex.Net.IAccountClient AccountClient;

        /// <summary>
        /// Market-related endpoints (market stats, tickers, margin markets).
        /// Resolved from the internal <see cref="ServiceProvider"/> during construction.
        /// </summary>
        public readonly Nobitex.Net.IMarketClient MarketClient;

        /// <summary>
        /// OrderBook related endpoints (order book snapshots / depth).
        /// Resolved from the internal <see cref="ServiceProvider"/> during construction.
        /// </summary>
        public readonly Nobitex.Net.IOrderBookClient OrderBookClient;

        /// <summary>
        /// Trade and order management endpoints (place orders, list trades, positions).
        /// Resolved from the internal <see cref="ServiceProvider"/> during construction.
        /// </summary>
        public readonly Nobitex.Net.ITradesClient TradesClient;

        /// <summary>
        /// Wallet related endpoints (balances, deposit/withdrawal operations).
        /// Resolved from the internal <see cref="ServiceProvider"/> during construction.
        /// </summary>
        public readonly Nobitex.Net.IWalletClient WalletClient;

        // Backing service provider built during construction. Disposed in Dispose().
        private readonly ServiceProvider? _serviceProvider;

        // Tracks whether Dispose() has been called to ensure idempotent disposal.
        private bool _disposed;

        /// <summary>
        /// Construct a new <see cref="NobitexClient"/> and register the required Nobitex services.
        /// </summary>
        /// <param name="token">API token used for authenticated endpoints. Required.</param>
        /// <exception cref="ArgumentException">Thrown when token is null/empty.</exception>
        /// <remarks>
        /// The constructor builds an internal DI container and resolves all typed clients immediately.
        /// This design simplifies usage for small apps but creates an internal ServiceProvider instance.
        /// If your app already has DI, prefer registering the library directly and injecting clients.
        /// </remarks>
        public NobitexClient(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token must be provided", nameof(token));

            // Create a fresh service collection. We configure the Nobitex client options here.
            var services = new ServiceCollection();

            // Register the Nobitex client services with sensible defaults.
            // Adjust BaseUrl/UserAgent/Timeout as needed for your environment.
            services.AddNobitexClient(opts =>
            {
                opts.Token = token;
                opts.BaseUrl = "https://apiv2.nobitex.ir";
                opts.UserAgent = "Nobitex.Net/1.0.0";
                opts.Timeout = TimeSpan.FromSeconds(20);
            });

            // Build the provider and capture it for later disposal.
            _serviceProvider = services.BuildServiceProvider();

            // Resolve required typed clients from the provider. These throws if registrations are missing,
            // which surfaces configuration issues early (during construction).
            AccountClient = _serviceProvider.GetRequiredService<Nobitex.Net.IAccountClient>();
            MarketClient = _serviceProvider.GetRequiredService<Nobitex.Net.IMarketClient>();
            OrderBookClient = _serviceProvider.GetRequiredService<Nobitex.Net.IOrderBookClient>();
            TradesClient = _serviceProvider.GetRequiredService<Nobitex.Net.ITradesClient>();
            WalletClient = _serviceProvider.GetRequiredService<Nobitex.Net.IWalletClient>();
        }

        /// <summary>
        /// Dispose the internal <see cref="ServiceProvider"/> and any resources it holds.
        /// Safe to call multiple times.
        /// </summary>
        /// <remarks>
        /// Because the class creates a private ServiceProvider, it is responsible for disposing it.
        /// If you move to application-level DI, remove this wrapper and let the host manage disposal.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed) return;

            _serviceProvider?.Dispose();
            _disposed = true;
        }
    }
}
