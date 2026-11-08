using Revora.Domain.Accounts;

namespace Revora.Application.Authentication;

public interface IHandoffTokenStore
{
    ValueTask<HandoffToken> IssueAsync(
        AccountId accountId,
        string realm,
        TimeSpan lifetime,
        CancellationToken cancellationToken);

    ValueTask<HandoffTicket?> ConsumeAsync(
        HandoffToken token,
        string realm,
        CancellationToken cancellationToken);
}
