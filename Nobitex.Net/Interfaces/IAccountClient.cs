using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net
{
    /// <summary>
    /// Typed contract for user/account related endpoints of the Nobitex API.
    /// Implementations should be safe to call concurrently.
    /// </summary>
    public interface IAccountClient
    {
        /// <summary>
        /// Get current user's profile.
        /// Endpoint: GET /users/profile
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Deserialized <see cref="NobitexProfileResponse"/> or null.</returns>
        Task<NobitexProfileResponse?> GetProfileAsync(CancellationToken ct = default);

        /// <summary>
        /// Get current user limitations (withdraw/deposit/trade limits and enabled features).
        /// Endpoint: POST /users/limitations
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Deserialized <see cref="AccountLimitationsResponse"/> or null.</returns>
        Task<AccountLimitationsResponse?> GetUserLimitationsAsync(CancellationToken ct = default);

        /// <summary>
        /// Add a new bank account for the user.
        /// Endpoint: POST /users/accounts-add
        /// Body: { number, shaba, bank }
        /// </summary>
        /// <param name="request">Bank account payload (number, shaba, bank).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Deserialized <see cref="AccountAddResponse"/> or null.</returns>
        Task<AccountAddResponse?> AddBankAccountAsync(AddBankAccountRequest request, CancellationToken ct = default);

        /// <summary>
        /// Add a new bank card for the user.
        /// Endpoint: POST /users/cards-add
        /// Body: { number, bank }
        /// </summary>
        /// <param name="request">Card payload (number, bank).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Deserialized <see cref="CardAddResponse"/> or null.</returns>
        Task<CardAddResponse?> AddBankCardAsync(AddBankCardRequest request, CancellationToken ct = default);

        /// <summary>
        /// Generate a blockchain deposit address for a currency or wallet.
        /// Endpoint: POST /users/wallets/generate-address
        /// Body:
        /// - currency (string) preferred; if provided replaces wallet
        /// - wallet (string/int) legacy; required only if currency is not provided
        /// - network (string) optional (e.g., "BSC")
        /// </summary>
        /// <param name="request">Request containing currency or wallet and optional network.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Deserialized <see cref="GenerateAddressResponse"/> or null.</returns>
        Task<GenerateAddressResponse?> GenerateAddressAsync(GenerateAddressRequest request, CancellationToken ct = default);

        /// <summary>
        /// Get user's delegation limit for margin positions for a given currency.
        /// Endpoint: GET /margin/delegation-limit?currency=...
        /// </summary>
        /// <param name="currency">Currency code (required).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Deserialized <see cref="DelegationLimitResponse"/> or null.</returns>
        Task<DelegationLimitResponse?> GetMarginDelegationLimitAsync(string currency, CancellationToken ct = default);
    }
}
