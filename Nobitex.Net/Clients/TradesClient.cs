using Microsoft.Extensions.Options;



using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public class TradesClient : ITradesClient
{
    private readonly IHttpTransport _transport;
    private readonly Nobitex.Net.NobitexOptions _opts;

    public TradesClient(IHttpTransport transport, IOptions<Nobitex.Net.NobitexOptions> opts)
    {
        _transport = transport;
        _opts = opts.Value;
    }

    public Task<IReadOnlyList<Trade>> GetTradesAsync(string symbol, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentNullException(nameof(symbol));
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), $"/v2/trade/history/{Uri.EscapeDataString(symbol)}"));
        return _transport.SendAsync<IReadOnlyList<Trade>>(req, ct)!;
    }
}
