namespace Revora.Domain.Accounts;

public readonly record struct AccountId
{
    public AccountId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Account identifier cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }
}
