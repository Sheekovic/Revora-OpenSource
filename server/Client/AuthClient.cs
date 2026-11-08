using System;
using Revora.Network.Sockets;
using Revora.Network.Cryptography;
using Revora.Network;

namespace Revora.Client
{
    public unsafe class AuthClient
    {
        private ClientWrapper _socket;
        public Network.AuthPackets.Authentication Info;
        public Database.AccountTable Account;
        public AuthCryptography Cryptographer;
        public int PasswordSeed;
        public ConcurrentPacketQueue Queue;
        public Network.AuthPackets.Srp6711Exchange Srp;
        public AuthClient(ClientWrapper socket)
        {
            Queue = new ConcurrentPacketQueue(0);
            _socket = socket;
        }
        public void Send(byte[] buffer)
        {
            byte[] _buffer = new byte[buffer.Length];
            Buffer.BlockCopy(buffer, 0, _buffer, 0, buffer.Length);
            Cryptographer.Encrypt(_buffer);
            _socket.Send(_buffer);
        }
        public void Send(Interfaces.IPacket buffer)
        {
            Send(buffer.ToArray());
        }
        public static uint nextID = 0;
    }
}