
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public interface IMarketClient
{
    Task<MarketStats> GetStatsAsync(string? srcCurrency = null, string? dstCurrency = null, CancellationToken ct = default);
}
