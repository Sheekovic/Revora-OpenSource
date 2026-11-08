using System.Buffers.Binary;

namespace Revora.Networking.Framing;

/// <summary>
/// Accumulates a TCP byte stream whose first little-endian UInt16 is the total frame
/// length. A configurable trailing-byte count supports transports where a declared
/// packet length excludes a fixed footer. It does not decrypt or interpret contents.
/// </summary>
public sealed class LittleEndianUInt16FrameAccumulator
{
    public const int LengthFieldSize = sizeof(ushort);

    private readonly int _maximumFrameSize;
    private readonly int _trailingByteCount;
    private byte[] _buffer;
    private int _count;

    public LittleEndianUInt16FrameAccumulator(
        int maximumFrameSize,
        int trailingByteCount = 0,
        int initialCapacity = 0)
    {
        if (maximumFrameSize < LengthFieldSize)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFrameSize));
        }

        if (trailingByteCount < 0 || trailingByteCount > maximumFrameSize - LengthFieldSize)
        {
            throw new ArgumentOutOfRangeException(nameof(trailingByteCount));
        }

        initialCapacity = initialCapacity == 0 ? Math.Min(256, maximumFrameSize) : initialCapacity;
        if (initialCapacity < LengthFieldSize || initialCapacity > maximumFrameSize)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        }

        _maximumFrameSize = maximumFrameSize;
        _trailingByteCount = trailingByteCount;
        _buffer = new byte[initialCapacity];
    }

    public int BufferedByteCount => _count;

    public IReadOnlyList<byte[]> Append(ReadOnlySpan<byte> bytes)
    {
        EnsureCapacityFor(bytes.Length);
        bytes.CopyTo(_buffer.AsSpan(_count));
        _count += bytes.Length;

        var frames = new List<byte[]>();
        var consumed = 0;

        while (_count - consumed >= LengthFieldSize)
        {
            var declaredLength = BinaryPrimitives.ReadUInt16LittleEndian(
                _buffer.AsSpan(consumed, LengthFieldSize));

            var frameLength = ValidateAndGetFrameLength(declaredLength);

            if (_count - consumed < frameLength)
            {
                break;
            }

            frames.Add(_buffer.AsSpan(consumed, frameLength).ToArray());
            consumed += frameLength;
        }

        if (consumed > 0)
        {
            _buffer.AsSpan(consumed, _count - consumed).CopyTo(_buffer);
            _count -= consumed;
        }

        return frames;
    }

    public void Reset()
    {
        _buffer.AsSpan(0, _count).Clear();
        _count = 0;
    }

    private int ValidateAndGetFrameLength(int declaredLength)
    {
        if (declaredLength < LengthFieldSize)
        {
            throw new FrameProtocolException(
                $"Declared length {declaredLength} is smaller than the {LengthFieldSize}-byte length field.");
        }

        var frameLength = checked(declaredLength + _trailingByteCount);
        if (frameLength > _maximumFrameSize)
        {
            throw new FrameProtocolException(
                $"Frame length {frameLength} exceeds the configured maximum {_maximumFrameSize}.");
        }

        return frameLength;
    }

    private void EnsureCapacityFor(int appendedLength)
    {
        if (appendedLength < 0 || _count > _maximumFrameSize - appendedLength)
        {
            throw new FrameProtocolException(
                $"Buffered data would exceed the configured maximum {_maximumFrameSize}.");
        }

        var required = _count + appendedLength;
        if (required <= _buffer.Length)
        {
            return;
        }

        var grown = Math.Min(_maximumFrameSize, Math.Max(required, _buffer.Length * 2));
        Array.Resize(ref _buffer, grown);
    }
}
