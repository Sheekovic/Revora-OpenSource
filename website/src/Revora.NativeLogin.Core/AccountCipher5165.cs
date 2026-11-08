namespace Revora.NativeLogin.Core;

/// <summary>Patch-5165 Account transport cipher, independently implemented from the exact client disassembly.</summary>
public sealed class AccountCipher5165
{
    private readonly byte[] first = new byte[256];
    private readonly byte[] second = new byte[256];
    private ushort inboundCounter;
    private ushort outboundCounter;

    public AccountCipher5165()
    {
        byte a = 0x9d;
        byte b = 0x62;
        for (int index = 0; index < 256; index++)
        {
            first[index] = a;
            second[index] = b;
            a = unchecked((byte)(((0x0f + unchecked((byte)(a * 0xfa))) * a) + 0x13));
            b = unchecked((byte)(((0x79 - unchecked((byte)(b * 0x5c))) * b) + 0x6d));
        }
    }

    public void DecryptClient(ReadOnlySpan<byte> ciphertext, Span<byte> plaintext) =>
        TransformServer(ciphertext, plaintext, ref inboundCounter);

    public void EncryptServer(ReadOnlySpan<byte> plaintext, Span<byte> ciphertext) =>
        TransformServer(plaintext, ciphertext, ref outboundCounter);

    internal void EncryptClientForTest(ReadOnlySpan<byte> plaintext, Span<byte> ciphertext) =>
        TransformClient(plaintext, ciphertext, ref inboundCounter);

    internal void DecryptServerForTest(ReadOnlySpan<byte> ciphertext, Span<byte> plaintext) =>
        TransformClient(ciphertext, plaintext, ref outboundCounter);

    private void TransformServer(ReadOnlySpan<byte> source, Span<byte> destination, ref ushort counter)
    {
        if (destination.Length < source.Length) throw new ArgumentException("Destination is too small.", nameof(destination));
        for (int index = 0; index < source.Length; index++)
        {
            byte value = unchecked((byte)(source[index] ^ 0xab));
            value = unchecked((byte)((value << 4) | (value >> 4)));
            destination[index] = unchecked((byte)(value ^ first[counter & 0xff] ^ second[counter >> 8]));
            counter++;
        }
    }

    private void TransformClient(ReadOnlySpan<byte> source, Span<byte> destination, ref ushort counter)
    {
        if (destination.Length < source.Length) throw new ArgumentException("Destination is too small.", nameof(destination));
        for (int index = 0; index < source.Length; index++)
        {
            byte value = unchecked((byte)(source[index] ^ first[counter & 0xff] ^ second[counter >> 8]));
            value = unchecked((byte)((value << 4) | (value >> 4)));
            destination[index] = unchecked((byte)(value ^ 0xab));
            counter++;
        }
    }
}
