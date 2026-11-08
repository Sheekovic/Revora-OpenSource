using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Revora.Networking.Framing;

namespace Revora.IntegrationTests;

internal static class Program
{
    private static async Task Main()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(1);

        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            using var client = new TcpClient(AddressFamily.InterNetwork);
            var accept = listener.AcceptTcpClientAsync(timeout.Token).AsTask();
            await client.ConnectAsync(endpoint.Address, endpoint.Port, timeout.Token);
            using var server = await accept;

            var first = MakeFrame(1, 2, 3);
            var second = MakeFrame(4, 5);
            var clientStream = client.GetStream();
            await clientStream.WriteAsync(first.AsMemory(0, 1), timeout.Token);
            await clientStream.WriteAsync(first.AsMemory(1), timeout.Token);
            await clientStream.WriteAsync(second, timeout.Token);

            var accumulator = new LittleEndianUInt16FrameAccumulator(64);
            var received = new List<byte[]>();
            var readBuffer = new byte[64];
            var serverStream = server.GetStream();
            while (received.Count < 2)
            {
                var count = await serverStream.ReadAsync(readBuffer, timeout.Token);
                received.AddRange(accumulator.Append(readBuffer.AsSpan(0, count)));
            }

            AssertSequence(first, received[0]);
            AssertSequence(second, received[1]);
            Console.WriteLine("Revora.IntegrationTests: loopback fragmentation/coalescing passed");
        }
        finally
        {
            listener.Stop();
        }
    }

    private static byte[] MakeFrame(params byte[] body)
    {
        var frame = new byte[sizeof(ushort) + body.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)frame.Length));
        body.CopyTo(frame, sizeof(ushort));
        return frame;
    }

    private static void AssertSequence(byte[] expected, byte[] actual)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
        {
            throw new InvalidOperationException("Byte sequences differ.");
        }
    }
}
