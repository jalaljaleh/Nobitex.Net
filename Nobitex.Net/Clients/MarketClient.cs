using Microsoft.Extensions.Options;



using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public class MarketClient : IMarketClient
{
    private readonly IHttpTransport _transport;
    private readonly Nobitex.Net.NobitexOptions _opts;

    public MarketClient(IHttpTransport transport, IOptions<Nobitex.Net.NobitexOptions> opts)
    {
        _transport = transport;
        _opts = opts.Value;
    }

    public Task<MarketStats> GetStatsAsync(string? srcCurrency = null, string? dstCurrency = null, CancellationToken ct = default)
    {
        var url = "/market/stats";
        var q = new List<string>();
        if (!string.IsNullOrEmpty(srcCurrency)) q.Add($"srcCurrency={Uri.EscapeDataString(srcCurrency)}");
        if (!string.IsNullOrEmpty(dstCurrency)) q.Add($"dstCurrency={Uri.EscapeDataString(dstCurrency)}");
        if (q.Count > 0) url += "?" + string.Join("&", q);
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), url));
        return _transport.SendAsync<MarketStats>(req, ct)!;
    }
}
