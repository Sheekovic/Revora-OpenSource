using Revora.Domain.Accounts;

namespace Revora.Application.Authentication;

public sealed record HandoffTicket(AccountId AccountId, string Realm, DateTimeOffset ExpiresAt);
