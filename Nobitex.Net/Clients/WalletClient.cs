using Microsoft.Extensions.Options;



using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public class WalletClient : IWalletClient
{
    private readonly IHttpTransport _transport;
    private readonly Nobitex.Net.NobitexOptions _opts;

    public WalletClient(IHttpTransport transport, IOptions<Nobitex.Net.NobitexOptions> opts)
    {
        _transport = transport;
        _opts = opts.Value;
    }

    public Task<IReadOnlyList<WalletBalance>?> GetBalancesAsync(CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), "/v1/wallet/balances"));
        return _transport.SendAsync<IReadOnlyList<WalletBalance>>(req, ct);
    }

    public Task<IReadOnlyList<WalletAddress>?> GetAddressesAsync(CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), "/v1/wallet/addresses"));
        return _transport.SendAsync<IReadOnlyList<WalletAddress>>(req, ct);
    }
}
