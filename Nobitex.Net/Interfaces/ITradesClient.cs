
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public interface ITradesClient
{
    Task<IReadOnlyList<Trade>> GetTradesAsync(string symbol, CancellationToken ct = default);
}
