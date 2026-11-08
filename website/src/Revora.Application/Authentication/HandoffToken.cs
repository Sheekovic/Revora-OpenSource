namespace Revora.Application.Authentication;

public sealed class HandoffToken
{
    private readonly byte[] value;

    public HandoffToken(ReadOnlySpan<byte> value)
    {
        if (value.Length != 32)
        {
            throw new ArgumentException("A handoff token must contain exactly 32 bytes.", nameof(value));
        }

        this.value = value.ToArray();
    }

    public ReadOnlyMemory<byte> Value => value;

    public override string ToString() => "[REDACTED HANDOFF TOKEN]";
}
