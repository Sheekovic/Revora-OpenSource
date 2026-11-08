/*
namespace Revora
{
    using System.Net.Sockets;
    using Sockets;

    public sealed class CrossClient : AsynchronousClientSocket
    {
        public static uint ServerID = 38;
        public Passport Passport { get; private set; }
        public Client.GameState Client;
        public CrossClient(Client.GameState client)
            : base("Cross Client", "TQClient", AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            OnClientConnect = Connect;
            OnClientReceive = Receive;
            OnClientDisconnect = DisconnectEvent;
            Client = client;
        }
        public void Connect(AsynchronousState state)
        {
            Passport = new Passport(this, this, null);
            state.Client = Passport;
        }
        ~CrossClient()
        {
            Dispose();
        }
        public void Receive(AsynchronousState state)
        {
            Passport passport = state.Client as Passport;
            if (passport.Packet != null)
            {
               Packets2.HandelPacket(this.Client, passport.Packet);
            }
        }
        public void DisconnectEvent(object state)
        {
            Client.Pipe = false;
            Client.Player.Invisable = false;
            Client.Equipment.UpdateEntityPacket();
            Client.LoadItemStats();
            Client.Player.Teleport(Client.Player.MapID, Client.Player.X, Client.Player.Y);
            Passport.Disconnect();
        }
        public new void Send(byte[] packet)
        {
            Passport.Send(packet);
        }
    }
}
*/
