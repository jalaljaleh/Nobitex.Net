namespace Nobitex.Net;
public record WalletAddress(string Currency, string Address, bool IsConfirmed);
public record WalletBalance(string Currency, decimal Available, decimal Locked);
