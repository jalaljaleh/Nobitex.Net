using System.Text.Json;
using System.Text.Json.Nodes;
using Nobitex.Net.Internal;
using Nobitex.Net.Models;
using Nobitex.Net.Serialization;

namespace Nobitex.Net.Apis;

/// <summary>
/// Margin (leverage) trading endpoints: fee rates, delegation limits, positions and margin orders.
/// </summary>
public sealed class MarginApi
{
    private readonly NobitexClient _client;

    internal MarginApi(NobitexClient client) => _client = client;

    /// <summary>Returns the leverage fee rates available to the user.</summary>
    public Task<MarginFeeRates> GetFeeRatesAsync(CancellationToken cancellationToken = default)
        => _client.SendAsync<MarginFeeRates>(
            HttpMethod.Get, "/margin/fee-rates", null, null,
            RateLimitGroups.Default, authenticated: true, cancellationToken);

    /// <summary>Returns the maximum delegation (borrowing) limit of the user.</summary>
    public Task<DelegationLimit> GetDelegationLimitAsync(
        string srcCurrency, string dstCurrency, CancellationToken cancellationToken = default)
        => _client.SendAsync<DelegationLimit>(
            HttpMethod.Get, "/margin/delegation-limit",
            QueryBuilder.Build(new { srcCurrency, dstCurrency }), null,
            RateLimitGroups.Default, authenticated: true, cancellationToken);

    /// <summary>Returns the open margin positions.</summary>
    /// <param name="status">Optional status filter, e.g. <c>open</c>.</param>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    /// <param name="cancellationToken"></param>
    public async Task<IReadOnlyList<MarginPosition>> GetPositionsAsync(
        string? status = null, int? page = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get, "/positions/list",
            QueryBuilder.Build(new { status, page, pageSize }), null,
            RateLimitGroups.Default, authenticated: true, cancellationToken).ConfigureAwait(false);

        return node["positions"]?.Deserialize<List<MarginPosition>>(NobitexJson.Options) ?? new List<MarginPosition>();
    }

    /// <summary>Returns the number of currently active positions.</summary>
    public async Task<int> GetActivePositionCountAsync(CancellationToken cancellationToken = default)
    {
        var node = await _client.SendNodeAsync(
            HttpMethod.Get, "/positions/active-count", null, null,
            RateLimitGroups.Default, authenticated: true, cancellationToken).ConfigureAwait(false);

        return node["count"]?.GetValue<int>() ?? 0;
    }

    /// <summary>Returns a single position by id.</summary>
    public Task<MarginPosition> GetPositionAsync(long positionId, CancellationToken cancellationToken = default)
        => _client.SendAsync<MarginPosition>(
            HttpMethod.Get, $"/positions/{positionId}/status", null, null,
            RateLimitGroups.Default, authenticated: true, cancellationToken);

    /// <summary>
    /// Places a margin order.
    /// Endpoint: <c>POST /margin/orders/add</c>. Counts towards the shared 300 requests / 10 minutes limit.
    /// </summary>
    /// <param name="request">Order definition. <see cref="PlaceOrderRequest.IsMargin"/> must be <c>true</c>.</param>
    /// <param name="cancellationToken"></param>
    public Task<Order> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.IsMargin = true;
        request.Validate();

        return _client.SendAsync<Order>(
            HttpMethod.Post, "/margin/orders/add", null,
            new Dictionary<string, object?>
            {
                ["type"] = NobitexSymbol.ToApiString(request.Side),
                ["execution"] = NobitexSymbol.ToApiString(request.Execution),
                ["srcCurrency"] = request.SrcCurrency,
                ["dstCurrency"] = request.DstCurrency,
                ["amount"] = request.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["price"] = request.Price?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["leverage"] = request.Leverage,
                ["side"] = request.PositionSide,
                ["clientOrderId"] = request.ClientOrderId,
            },
            RateLimitGroups.PlaceOrder, authenticated: true, cancellationToken);
    }

    /// <summary>Closes (or partially closes) an open position.</summary>
    public Task<MarginPosition> ClosePositionAsync(
        long positionId, decimal? amount = null, CancellationToken cancellationToken = default)
        => _client.SendAsync<MarginPosition>(
            HttpMethod.Post, $"/positions/{positionId}/close", null,
            new Dictionary<string, object?> { ["amount"] = amount?.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            RateLimitGroups.PlaceOrder, authenticated: true, cancellationToken);

    /// <summary>Changes the collateral of an open position.</summary>
    public Task<MarginPosition> EditCollateralAsync(
        long positionId, decimal collateral, CancellationToken cancellationToken = default)
        => _client.SendAsync<MarginPosition>(
            HttpMethod.Post, $"/positions/{positionId}/edit-collateral", null,
            new Dictionary<string, object?> { ["collateral"] = collateral.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            RateLimitGroups.PlaceOrder, authenticated: true, cancellationToken);
}