using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Revora.Network.Sockets;

internal static class DisconnectTests
{
    sealed class Subscription : IDisposable
    {
        public int Calls;
        public void Dispose() { Interlocked.Increment(ref Calls); }
    }
    static void Require(bool value) { if(!value) throw new Exception("Socket lifecycle assertion failed."); }
    static void Close(Socket socket)
    {
        var wrapper=new ClientWrapper { Socket=socket,Alive=true };
        var subscriptions=new[]{new Subscription(),new Subscription(),new Subscription()};
        const BindingFlags fields=BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(ClientWrapper).GetField("TimerSubscriptions",fields).SetValue(wrapper,subscriptions);
        typeof(ClientWrapper).GetField("SendQueue",fields).SetValue(wrapper,new Queue<byte[]>());
        typeof(ClientWrapper).GetField("SendSyncRoot",fields).SetValue(wrapper,new object());
        Parallel.For(0,8,i=>wrapper.Disconnect());
        wrapper.Disconnect();
        Require(!wrapper.Alive);
        foreach(var subscription in subscriptions)Require(subscription.Calls==1);
        bool disposed=false;
        try { socket.Poll(0,SelectMode.SelectRead); } catch(ObjectDisposedException) { disposed=true; }
        Require(disposed);
    }
    static void Main()
    {
        // Regression invariant: the managed Socket alone owns the native handle.
        foreach(var method in typeof(ClientWrapper).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static))
            Require((method.Attributes&MethodAttributes.PinvokeImpl)==0);
        Close(new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp));
        var disposed=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
        disposed.Dispose();Close(disposed);
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        try {
            for(int i=0;i<64;i++) {
                using(var peer=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)) {
                    peer.ReceiveTimeout=2000;peer.Connect(listener.LocalEndpoint);
                    var accepted=listener.AcceptSocket();Close(accepted);
                    Require(peer.Receive(new byte[1])==0);
                }
                // A subsequent unrelated connection must remain usable.
                using(var peer=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)) {
                    peer.ReceiveTimeout=2000;peer.Connect(listener.LocalEndpoint);
                    using(var accepted=listener.AcceptSocket()) {
                        accepted.Send(new byte[]{37});var value=new byte[1];
                        Require(peer.Receive(value)==1 && value[0]==37);
                    }
                }
            }
        } finally { listener.Stop(); }
        Console.WriteLine("Socket disconnect: concurrent/repeated, unconnected/disposed, 64 loopback closures and surviving connections passed.");
    }
}
