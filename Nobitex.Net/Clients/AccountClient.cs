using Microsoft.Extensions.Options;



using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public class AccountClient : IAccountClient
{
    private readonly IHttpTransport _transport;
    private readonly Nobitex.Net.NobitexOptions _opts;

    public AccountClient(IHttpTransport transport, IOptions<Nobitex.Net.NobitexOptions> opts)
    {
        _transport = transport;
        _opts = opts.Value;
    }

    public Task<Profile?> GetProfileAsync(CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_opts.BaseUrl), "/v1/user/profile"));
        return _transport.SendAsync<Profile>(req, ct)!;
    }
}
