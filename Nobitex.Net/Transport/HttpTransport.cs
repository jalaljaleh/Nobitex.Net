using Microsoft.Extensions.Logging;


using Polly;
using Polly.Wrap;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Nobitex.Net;

public class HttpTransport : IHttpTransport
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<HttpTransport> _logger;
    private readonly AsyncPolicyWrap<HttpResponseMessage> _policyWrap;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly NobitexOptions _opts;

    public HttpTransport(IHttpClientFactory httpFactory, RetryPolicies policies, ILogger<HttpTransport> logger, Microsoft.Extensions.Options.IOptions<NobitexOptions> opts)
    {
        _httpFactory = httpFactory;
        _logger = logger;
        _policyWrap = policies.PolicyWrap;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };
        _jsonOptions.Converters.Add(new OrderbookLevelConverter());

        _opts = opts.Value;
    }

    public async Task<T?> SendAsync<T>(HttpRequestMessage req, CancellationToken ct = default) where T : class
    {
        var client = _httpFactory.CreateClient("nobitex");
        HttpResponseMessage res = null!;
        try
        {
            res = await _policyWrap.ExecuteAsync(ct => client.SendAsync(req, ct), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transport error for {Method} {Uri}", req.Method, req.RequestUri);
            throw;
        }

        if (!res.IsSuccessStatusCode)
        {
            if (res.StatusCode == (HttpStatusCode)429)
            {
                var retryAfter = res.Headers.RetryAfter?.Delta?.TotalSeconds ?? -1;
                _logger.LogWarning("Received 429. Retry-After: {RetryAfter}s", retryAfter);
            }
            var content = await res.Content.ReadAsStringAsync(ct);
            _logger.LogDebug("Non-success response: {Status} {Content}", res.StatusCode, content);
            res.EnsureSuccessStatusCode();
        }

        var stream = await res.Content.ReadAsStreamAsync(ct);
        var obj = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, ct);
        return obj;
    }
}
