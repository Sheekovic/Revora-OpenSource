namespace Revora.Networking.Framing;

public sealed class FrameProtocolException : Exception
{
    public FrameProtocolException(string message)
        : base(message)
    {
    }
}

