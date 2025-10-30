using System.Collections.Generic;

namespace Nobitex.Net;
public record BankCard(string Number, string Bank, string Owner, bool Confirmed, string Status);
public record Profile(
    string FirstName,
    string LastName,
    string Email,
    string Username,
    string? Mobile,
    IReadOnlyList<BankCard> BankCards,
    IDictionary<string, bool> Verifications,
    string? WebsocketAuthParam
);
