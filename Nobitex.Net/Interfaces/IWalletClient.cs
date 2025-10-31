
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public interface IWalletClient
{
    Task<WalletBalanceResponse?> GetBalanceAsync(string currency, CancellationToken ct = default);
    Task<WalletTransactionsListResponse?> GetLatestWalletTransactionsAsync(int walletId, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<WalletTransactionsHistoryResponse?> GetWalletTransactionsHistoryAsync(string currency, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<WalletDepositsListResponse?> GetWalletDepositsAsync(int walletId = 0, int page = 1, int pageSize = 50, DateTime? from = null, DateTime? to = null, CancellationToken ct = default);

}
