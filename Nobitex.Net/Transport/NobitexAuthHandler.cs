using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;

public class NobitexAuthHandler : DelegatingHandler
{
    private readonly NobitexOptions _opts;
    private readonly ILogger<NobitexAuthHandler> _logger;

    public NobitexAuthHandler(IOptions<NobitexOptions> opts, ILogger<NobitexAuthHandler> logger)
    {
        _opts = opts.Value;
        _logger = logger;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_opts.Token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", _opts.Token);
        }

        if (!string.IsNullOrWhiteSpace(_opts.UserAgent))
            request.Headers.UserAgent.ParseAdd(_opts.UserAgent);

        return base.SendAsync(request, cancellationToken);
    }
}
