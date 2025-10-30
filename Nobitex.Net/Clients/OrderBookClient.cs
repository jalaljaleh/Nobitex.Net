using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;



using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public class OrderBookClient : IOrderBookClient
{
    private readonly IHttpTransport _transport;
    private readonly Nobitex.Net.NobitexOptions _opts;
    private readonly ILogger<OrderBookClient> _logger;

    public OrderBookClient(IHttpTransport transport, IOptions<Nobitex.Net.NobitexOptions> opts, ILogger<OrderBookClient> logger)
    {
        _transport = transport;
        _opts = opts.Value;
        _logger = logger;
    }

    public Task<IDictionary<string, OrderBook>> GetAllAsync(CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), "/v3/orderbook/all"));
        return _transport.SendAsync<IDictionary<string, OrderBook>>(req, ct)!;
    }

    public Task<OrderBook?> GetOrderBookAsync(string symbol, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentNullException(nameof(symbol));
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), $"/v3/orderbook/{Uri.EscapeDataString(symbol)}"));
        return _transport.SendAsync<OrderBook>(req, ct)!;
    }
}
