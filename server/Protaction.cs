using C_Socket;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AntiHack
{
    public class Socket
    {
        public void init(ushort port = 6319, string ip = "127.0.0.1")
        {
            if(port==0)throw new ArgumentOutOfRangeException("port");
            SocketEvents sockEvents = new SocketEvents();
            sockEvents.OnConnection = new ConnectionEvent(Handle_Connection);
            sockEvents.OnDisconnection = new ConnectionEvent(Handle_Disconnection);
            sockEvents.OnReceive = new BufferEvent(Handle_Receive);
            Server server = new Server(sockEvents);
            server.Start(ip, port);
            Console.WriteLine("Protection start on port " + port);
            System.Threading.Thread t = new System.Threading.Thread(() =>
            {
                while (true)
                {
                    if (clients.Count > 0)
                    {
                        foreach (var sClient in clients)
                        {
                            sClient.Send(ASCIIEncoding.Default.GetBytes("FILE:ini/MagicEffect.ini:3d8b9b5a1a0fbd4cd22a2b594869c0afa4327564" + "<eof>"));

                            sClient.Send(ASCIIEncoding.Default.GetBytes("FILE:ini/MagicType.dat:811589835cb818375eb024283348d6df51899917" + "<eof>"));

                            sClient.Send(ASCIIEncoding.Default.GetBytes("FILE:Env_DX8/Conquer.exe:8d733130a6f15a3331e9cda7a6d31025bffd887f" + "<eof>"));

                        }
                    }
                    System.Threading.Thread.Sleep(15000);
                }
            });
            t.Start();
        }
        public static List<Client> clients = new List<Client>();
        private bool Handle_Receive(Client sClient, int Size, byte[] Packet)
        {
            string mess = Encoding.Default.GetString(Packet);
            var data = mess.Split(':');
            if (data.Length < 2)
            {
                sClient.Disconnect();
            }
            switch (data[0].ToUpper())
            {
                case "OK":
                    {
                        var uid = uint.Parse(data[1]);
                        if (uid == 0)
                            return true;
                        if (Revora.Kernel.GamePool.ContainsKey(uid))
                        {
                            var client = Revora.Kernel.GamePool[uid];
                            client.LastClientOK = DateTime.Now;
                            sClient.Send(ASCIIEncoding.Default.GetBytes("OK:ON" + "<eof>"));
                        }
                        else
                        {
                            sClient.Send(ASCIIEncoding.Default.GetBytes("OK:OFF" + "<eof>"));
                        }
                        break;
                    }
                case "FILE":
                    {
                        var Name = data[1];
                        sClient.HackerPlayer = true;
                        sClient.HackName = " using File " + Name;
                        Console.WriteLine("Found a FILE :" + sClient.HackName);
                        sClient.Send(ASCIIEncoding.Default.GetBytes("KILL:OFF" + "<eof>"));
                        break;
                    }
                case "PROG":
                    {
                        var Name = data[1];
                        if (Name == "officeclicktorun.exe") return false;
                        else
                        {
                            sClient.HackerPlayer = true;
                            sClient.HackName = " using Program " + Name;
                            //Console.WriteLine("found a PROG :" + sClient.HackName);
                        }
                        break;
                    }
                case "TIT":
                    {
                        sClient.HackerPlayer = true;
                        var Name = data[1];
                        sClient.HackName = " using Program " + Name;
                        //Console.WriteLine("found a TIT :" + sClient.HackName);
                        break;
                    }
                case "SP":
                    {
                        sClient.HackerPlayer = true;
                        sClient.HackName = " using Speed Hack";
                        //Console.WriteLine("found a SP :" + sClient.HackName);
                        sClient.Send(ASCIIEncoding.Default.GetBytes("KILL:OFF" + "<eof>"));
                        break;
                    }
                case "CL":
                    {
                        sClient.HackerPlayer = true;
                        sClient.HackName = " using Clicker";
                        //Console.WriteLine("found a clicker :" + sClient.HackName);
                        sClient.Send(ASCIIEncoding.Default.GetBytes("KILL:OFF" + "<eof>"));
                        break;
                    }
            }
            return true;
        }

        private bool Handle_Disconnection(Client sClient)
        {
            if (clients.Contains(sClient))
            {
                clients.Remove(sClient);
            }
            return true;
        }
        private bool Handle_Connection(Client sClient)
        {
            if (!clients.Contains(sClient))
            {
                clients.Add(sClient);
            }
            sClient.Send(ASCIIEncoding.Default.GetBytes("FILE:ini/MagicEffect.ini:3d8b9b5a1a0fbd4cd22a2b594869c0afa4327564" + "<eof>"));

            sClient.Send(ASCIIEncoding.Default.GetBytes("FILE:ini/MagicType.dat:811589835cb818375eb024283348d6df51899917" + "<eof>"));

            sClient.Send(ASCIIEncoding.Default.GetBytes("FILE:Env_DX8/Conquer.exe:8d733130a6f15a3331e9cda7a6d31025bffd887f" + "<eof>"));

            sClient.Send(ASCIIEncoding.Default.GetBytes("PROG:aimbot~cheat~speed~hack~tasker~artmoney~auto~macro~evolution~knup~medo_o~windowstools~magictype~magiceffect~supway~F8E0A3885481E91333212D7B41304769~666A5EC4B9BCBF382DA87E4904353F14~mihwak~C3188882E6BF739D02DA3CB9C9755AED" + "<eof>"));

            sClient.Send(ASCIIEncoding.Default.GetBytes("TIT:aimbot~cheat~speed~hack~tasker~artmoney~auto~macro~evolution~knup~medo_o~windowstools~magictype~magiceffect~supway~F8E0A3885481E91333212D7B41304769~666A5EC4B9BCBF382DA87E4904353F14~mihwak~C3188882E6BF739D02DA3CB9C9755AED" + "<eof>"));
            return true;
        }
    }
}
namespace C_Socket
{
    // State object for receiving data from remote device.
    public class Server
    {
        /// <summary>
        /// The socket associated with the server.
        /// </summary>
        private Socket serverSocket;

        /// <summary>
        /// The socket events associated with the server.
        /// </summary>
        private SocketEvents socketEvents;

        /// <summary>
        /// Creates a new socket server.
        /// </summary>
        /// <param name="socketEvents">The events associated with the server. Put null for nothing.</param>
        public Server(SocketEvents socketEvents)
        {
            if (socketEvents == null)
                this.socketEvents = new SocketEvents();
            else
                this.socketEvents = socketEvents;

            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        /// <param name="ip">The IP of the server.</param>
        /// <param name="port">The port of the server.</param>
        public void Start(string ip, int port)
        {
            serverSocket.Bind(new IPEndPoint(IPAddress.Parse(ip), port));
            serverSocket.Listen(500);
            Accept();
        }

        /// <summary>
        /// Starts to accept connections.
        /// </summary>
        private void Accept()
        {
            serverSocket.BeginAccept(new AsyncCallback(Accept_Callback), null);
        }

        /// <summary>
        /// The callback from Accept()
        /// </summary>
        /// <param name="asyncResult">The async result.</param>
        private void Accept_Callback(IAsyncResult asyncResult)
        {
            try
            {
                Socket accepted = serverSocket.EndAccept(asyncResult);
                if (accepted.Connected)
                {
                    Client sClient = new Client(accepted, socketEvents);
                    if (socketEvents.OnConnection.Invoke(sClient))
                    {
                        sClient.Alive = true;
                        sClient.Receive();
                    }
                }
            }
            catch { }
            Accept();
        }
    }
    public class Client
    {
        /// <summary>
        /// The socket associated with the client.
        /// </summary>
        private Socket clientSocket;
        /// <summary>
        /// The socket events associated with the server.
        /// </summary>
        private SocketEvents socketEvents;
        /// <summary>
        /// The buffer holding all bytes received.
        /// </summary>
        private byte[] dataHolder;
        /// <summary>
        /// An object which is the owner of the client.
        /// </summary>
        public bool Alive { get; set; }
        private object send_lock;
        public Client(Socket acceptedsocket, SocketEvents socketEvents)
        {
            clientSocket = acceptedsocket;
            this.socketEvents = socketEvents;
            System.Threading.Interlocked.CompareExchange(ref send_lock, new object(), null);
        }
        internal void Receive()
        {
            try
            {
                if (!Connected)
                {
                    Disconnect("Not connected.");
                    return;
                }

                dataHolder = new byte[65535];
                clientSocket.BeginReceive(dataHolder, 0, dataHolder.Length, SocketFlags.None, new AsyncCallback(Receive_Callback), null);
            }
            catch (Exception e)
            {
                Disconnect("Failed beginreceive. Exception: " + e.ToString());
            }
        }
        private void Receive_Callback(IAsyncResult asyncResult)
        {
            try
            {
                if (!clientSocket.Connected)
                {
                    Disconnect("Not connected.");
                    return;
                }
                SocketError err;
                int rSize = clientSocket.EndReceive(asyncResult, out err);

                if (err != SocketError.Success)
                {
                    Disconnect("Failed receive. (99% regular DC) Socket Error: " + err.ToString());
                    return;
                }
                if (rSize < 4)
                {
                    Disconnect("Invalid Packet Header. (99% regular DC) Size: " + rSize);
                    return;
                }
                byte[] rBuffer = new byte[rSize];
                Buffer.BlockCopy(dataHolder, 0, rBuffer, 0, rSize);
                if (!alreadyDisconnected)
                {
                    socketEvents.OnReceive.Invoke(this, rSize, rBuffer);
                }
            }
            catch (Exception e)
            {
                Disconnect(e.ToString());
            }
            Receive();
        }
        public bool Connected
        {
            get
            {
                if (clientSocket == null)
                    return false;
                return clientSocket.Connected;
            }
        }
        public void Disconnect(string reason)
        {
            if (alreadyDisconnected)
                return;
            alreadyDisconnected = true;
            dcReason = reason;
            Console.WriteLine(reason);
            try
            {
                clientSocket.Disconnect(false);
                clientSocket.Shutdown(SocketShutdown.Both);
            }
            catch { }
            socketEvents.OnDisconnection.Invoke(this);
        }
        public void Disconnect()
        {
            if (alreadyDisconnected)
                return;
            alreadyDisconnected = true;
            try
            {
                clientSocket.Disconnect(false);
                clientSocket.Shutdown(SocketShutdown.Both);
            }
            catch { }
            socketEvents.OnDisconnection.Invoke(this);
        }
        private bool alreadyDisconnected = false;
        /// <summary>
        /// Gets a boolean defining whether the client is already disconnected or not.
        /// </summary>
        /// <summary>
        /// The disconnect reason.
        /// </summary>
        private string dcReason;
        /// <summary>
        /// Gets the disconnect reason.
        /// </summary>
        public bool HackerPlayer { get; internal set; }
        public string HackName { get; internal set; }
        public void Send(byte[] Buffer)
        {
            if (Buffer.Length > 65535)
            {
                Disconnect("Too big packet...");
                return;
            }
            lock (send_lock)
            {
                try
                {
                    if (clientSocket.Connected)
                    {
                        clientSocket.Send(Buffer);
                    }
                    else
                        Disconnect("Not Connected.");
                }
                catch (SocketException se)
                {
                    Disconnect(string.Format("Failed to send packet... Error: {0}", se.SocketErrorCode.ToString()));
                }
            }
        }
        /// <summary>
        /// The callback from Send().
        /// </summary>
        /// <param name="asyncResult">The async result.</param>
    }
    public delegate bool ConnectionEvent(Client sClient);
    /// <summary>
    /// A delegate associated with socket-buffer events.
    /// </summary>
    public delegate bool BufferEvent(Client sClient, int Size, byte[] Packet);
    /// <summary>
    /// Events called through sockets.
    /// </summary>
    public class SocketEvents
    {
        /// <summary>
        /// An event raised when a client is connected.
        /// </summary>
        public ConnectionEvent OnConnection = new ConnectionEvent(empty_conn);
        /// <summary>
        /// Empty method for the connection events.
        /// </summary>
        /// <param name="sClient">The socket client.</param>
        /// <returns>Returns true always.</returns>
        static bool empty_conn(Client sClient) { return true; }
        /// <summary>
        /// An event raised when a client is disconnecting.
        /// </summary>
        public ConnectionEvent OnDisconnection = new ConnectionEvent(empty_conn);
        /// <summary>
        /// An event raised when a client has send data to the server.
        /// </summary>
        public BufferEvent OnReceive = new BufferEvent(empty_buff);
        /// <summary>
        /// Empty method for the buffer events.
        /// </summary>
        /// <param name="sClient">The socket client.</param>
        /// <param name="packet">The data packet.</param>
        /// <returns>Returns true always.</returns>
        static bool empty_buff(Client sClient, int Size, byte[] packet) { return true; }
    }
}
