namespace Nobitex.Websocket.Net.Extensions
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Nobitex.Websocket.Net.Abstractions;
    using Nobitex.Websocket.Net.Services;
    using Nobtiex.Websocket.Net;
    using System;

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddNobitexWebsocket(this IServiceCollection services, Action<NobitexWebsocketOptions>? configure = null)
        {
            var options = new NobitexWebsocketOptions();
            configure?.Invoke(options);

            if (string.IsNullOrWhiteSpace(options.ApiToken))
            {
                throw new ArgumentException("ApiToken must be provided when registering Nobitex websocket services.");
            }

            services.AddSingleton(options);

            // Register a named HttpClient used by NobitexConnectionTokenProvider
            services.AddHttpClient(nameof(NobitexConnectionTokenProvider), client =>
            {
                client.BaseAddress = options.ApiBaseAddress;
            });

            // Register the raw HTTP-backed provider (factory)
            services.AddSingleton<NobitexConnectionTokenProvider>(sp =>
            {
                var factory = sp.GetRequiredService<System.Net.Http.IHttpClientFactory>();
                var http = factory.CreateClient(nameof(NobitexConnectionTokenProvider));
                var logger = sp.GetService<ILogger<NobitexConnectionTokenProvider>>();
                return new NobitexConnectionTokenProvider(http, options.ApiToken, logger);
            });

            // Wrap with cached provider and register as IConnectionTokenProvider
            services.AddSingleton<IConnectionTokenProvider>(sp =>
            {
                var raw = sp.GetRequiredService<NobitexConnectionTokenProvider>();
                var logger = sp.GetService<ILogger<CachedConnectionTokenProvider>>();
                return new CachedConnectionTokenProvider(raw, TimeSpan.FromSeconds(60), logger);
            });

            // Register client with options injected
            services.AddSingleton<WebsocketCentrifugoClient>(sp =>
            {
                var tokenProvider = sp.GetRequiredService<IConnectionTokenProvider>();
                var logger = sp.GetService<ILogger<WebsocketCentrifugoClient>>();
                var opts = sp.GetRequiredService<NobitexWebsocketOptions>();
                return new WebsocketCentrifugoClient(tokenProvider, opts, logger);
            });

            services.AddSingleton<IWebsocketCentrifugoClient>(sp => sp.GetRequiredService<WebsocketCentrifugoClient>());



            return services;
        }
    }
}
