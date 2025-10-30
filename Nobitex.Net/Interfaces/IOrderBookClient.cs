
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public interface IOrderBookClient
{
    Task<OrderBook?> GetOrderBookAsync(string symbol, CancellationToken ct = default);
    Task<IDictionary<string, OrderBook>> GetAllAsync(CancellationToken ct = default);
}
