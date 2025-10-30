
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public interface IWalletClient
{
    Task<IReadOnlyList<WalletBalance>?> GetBalancesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WalletAddress>?> GetAddressesAsync(CancellationToken ct = default);
}
