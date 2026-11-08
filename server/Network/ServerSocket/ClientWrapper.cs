using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;

namespace Revora.Network.Sockets
{
    public class ClientWrapper
    {
        public int BufferSize;
        public byte[] Buffer;
        public Socket Socket;
        public object Connector;
        public ServerSocket Server;
        public string IP;
        public string MAC;
        public bool Alive;
        public bool OverrideTiming;
        private IDisposable[] TimerSubscriptions;
        private Queue<byte[]> SendQueue;
        private object SendSyncRoot;

        public Action<byte[], int, ClientWrapper> Callback;
        public void Create(Socket socket, ServerSocket server, Action<byte[], int, ClientWrapper> callBack)
        {
            Callback = callBack;
            BufferSize = 2047;
            Socket = socket;
            Server = server;
            Buffer = new byte[BufferSize];
            LastReceive = Time32.Now;
            OverrideTiming = false;
            SendQueue = new Queue<byte[]>();
            SendSyncRoot = new object();
            TimerSubscriptions = new[]
            {
                Thread.Subscribe<ClientWrapper>(Revora.Server.Thread.ConnectionReview, this, Thread.GenericThreadPool),
                Thread.Subscribe<ClientWrapper>(Revora.Server.Thread.ConnectionReceive, this, Thread.ReceivePool),
                Thread.Subscribe<ClientWrapper>(Revora.Server.Thread.ConnectionSend, this, Thread.SendPool)
            };
        }

        /// <summary>
        /// To be called only from a syncrhonized block of code
        /// </summary>
        /// <param name="data"></param>
        public void Send(byte[] data)
        {
#if DIRECTSEND
            lock (SendSyncRoot)
                Socket.Send(data);
#else
            lock (SendSyncRoot)
                SendQueue.Enqueue(data);
#endif
        }

        public Time32 LastReceive;
        public Time32 LastReceiveCall;

        public void Disconnect()
        {
            lock (Socket)
            {
                int K = 1000;
                while (SendQueue.Count > 0 && Alive && (K--) > 0)
                    System.Threading.Thread.Sleep(1);
                if (!Alive) return;
                Alive = false;

                for (int i = 0; i < TimerSubscriptions.Length; i++)
                    TimerSubscriptions[i].Dispose();

                // Socket owns its native handle. Closing it separately can leave
                // Dispose acting on a handle already reused by another connection.
                try { Socket.Shutdown(SocketShutdown.Both); }
                catch (SocketException) { } // Already disconnected or never connected.
                catch (ObjectDisposedException) { } // Another owner already disposed it.
                finally { Socket.Dispose(); }
            }
        }

        public static void TryReview(ClientWrapper wrapper)
        {
            if (wrapper.Alive)
            {
                if (wrapper.OverrideTiming)
                {
                    if (Time32.Now > wrapper.LastReceive.AddMilliseconds(180000))
                        wrapper.Server.InvokeDisconnect(wrapper);
                }
                else
                {
                    if (Time32.Now < wrapper.LastReceiveCall.AddMilliseconds(2000))
                        if (Time32.Now > wrapper.LastReceive.AddMilliseconds(60000))
                            wrapper.Server.InvokeDisconnect(wrapper);
                }
            }
        }

        private bool isValid()
        {
            if (!Alive)
            {
                for (int i = 0; i < TimerSubscriptions.Length; i++)
                    TimerSubscriptions[i].Dispose();
                return false;
            }
            return true;
        }

        private void doReceive(int available)
        {
            LastReceive = Time32.Now;
            try
            {
                if (available > Buffer.Length) available = Buffer.Length;
                int size = Socket.Receive(Buffer, available, SocketFlags.None);
                if (size != 0)
                {
                    if (Callback != null)
                        Callback(Buffer, size, this);
                }
                else
                {
                    Server.InvokeDisconnect(this);
                }
            }
            catch (SocketException)
            {
                Server.InvokeDisconnect(this);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        public static void TryReceive(ClientWrapper wrapper)
        {
            wrapper.LastReceiveCall = Time32.Now;
            if (!wrapper.isValid()) return;
            try
            {
                bool poll = wrapper.Socket.Poll(0, SelectMode.SelectRead);
                int available = wrapper.Socket.Available;
                if (available > 0)
                    wrapper.doReceive(available);
                else if (poll)
                    wrapper.Server.InvokeDisconnect(wrapper);
            }
            catch (SocketException)
            {
                wrapper.Server.InvokeDisconnect(wrapper);
            }
        }

        private bool TryDequeueSend(out byte[] buffer)
        {
            buffer = null;
            lock (SendSyncRoot)
                if (SendQueue.Count != 0)
                    buffer = SendQueue.Dequeue();
            return buffer != null;
        }
        public static void TrySend(ClientWrapper wrapper)
        {
            if (!wrapper.isValid()) return;
            byte[] buffer;

            while (wrapper.TryDequeueSend(out buffer))
            {
                try
                {
                    wrapper.Socket.Send(buffer);
                    //wrapper.Socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, endSend, wrapper);
                }
                catch
                {
                    wrapper.Server.InvokeDisconnect(wrapper);
                }
            }
        }

        private static void endSend(IAsyncResult ar)
        {
            var wrapper = ar.AsyncState as ClientWrapper;
            try
            {
                wrapper.Socket.EndSend(ar);
            }
            catch
            {
                wrapper.Server.InvokeDisconnect(wrapper);
            }
        }
    }
}
