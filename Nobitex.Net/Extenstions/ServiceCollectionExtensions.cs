using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System;

namespace Nobitex.Net;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNobitexClient(this IServiceCollection services, Action<NobitexOptions>? configure = null)
    {
        var opts = new NobitexOptions();
        configure?.Invoke(opts);
        services.AddSingleton(Options.Create(opts));

        services.AddSingleton<RetryPolicies>();
        services.AddTransient<NobitexAuthHandler>();
        services.AddTransient<RateLimitHandler>();

        // configure named HttpClient used by HttpTransport
        services.AddHttpClient("nobitex", client =>
        {
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = opts.Timeout;
            if (!string.IsNullOrWhiteSpace(opts.UserAgent))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(opts.UserAgent);
            }
        })
        .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        })
        .AddHttpMessageHandler<NobitexAuthHandler>()
        .AddHttpMessageHandler<RateLimitHandler>();

        services.AddSingleton<IHttpTransport, HttpTransport>();
        services.AddSingleton<IMarketClient, MarketClient>();
        services.AddSingleton<IOrderBookClient, OrderBookClient>();
        services.AddSingleton<ITradesClient, TradesClient>();
        services.AddSingleton<IAccountClient, AccountClient>();
        services.AddSingleton<IWalletClient, WalletClient>();

        // optional metrics implementation can be registered by consumer
        services.AddSingleton<INobitexMetrics, NullMetrics>();

        return services;
    }

    private class NullMetrics : INobitexMetrics
    {
        public void RequestFinished(string path, string method, int statusCode, TimeSpan duration) { }
        public void RequestStarted(string path, string method) { }
        public void Retry(string path, string method, int attempt) { }
    }
}
