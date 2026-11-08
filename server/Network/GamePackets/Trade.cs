using System;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class Trade : Writer, Interfaces.IPacket
    {
        public const uint
        Request = 1,
        Close = 2,
        ShowTable = 3,
        HideTable = 5,
        AddItem = 6,
        SetMoney = 7,
        ShowMoney = 8,
        Accept = 10,
        RemoveItem = 11,
        ShowConquerPoints = 12,
        SetConquerPoints = 13;

        byte[] Buffer;

        public Trade(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[20];
                WriteUInt16(12, 0, Buffer);
                WriteUInt16(1056, 2, Buffer);
            }
        }
        //Agrega el Codigo
        public ulong dwParam2
        {
            get { return BitConverter.ToUInt64(Buffer, 4); }
            set { Writer.WriteUInt64(value, 4, Buffer); }
        }
        public uint dwParam
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public uint Type
        {
            get { return BitConverter.ToUInt16(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }

        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            if(packet==null || packet.Length<16 || packet.Length>24 || client==null || client.Entity==null || client.Entity.PokerTable>0)return;
            int declared=BitConverter.ToUInt16(packet,0);
            if(declared<12 || declared!=packet.Length && declared!=packet.Length-8 || BitConverter.ToUInt16(packet,2)!=1056)return;
            var trade=new Trade(false);trade.Deserialize(packet);Game.Features.AtomicTrade.Process(trade,client);
        }
        internal static void Dispatch(Trade trade,Client.GameState client)
        {
            switch(trade.Type) {
                case Request:RequestTrade(trade,client);break;
                case Close:CloseTrade(trade,client);break;
                case AddItem:AddTradeItem(trade,client);break;
                case SetMoney:SetTradeMoney(trade,client);break;
                case SetConquerPoints:SetTradeConquerPoints(trade,client);break;
                case Accept:Game.Features.AtomicTrade.AcceptLocked(trade,client);break;
            }
        }
        static void RequestTrade(Trade trade, Client.GameState client)
        {
            Client.GameState _client = null;
            if (Kernel.GamePool.TryGetValue(trade.dwParam, out _client))
            {
                if (_client.Trade.InTrade || client.Trade.InTrade || client.Entity.UID == trade.dwParam || Kernel.GetDistance(client.Entity.X, client.Entity.Y, _client.Entity.X, _client.Entity.Y) > Constants.pScreenDistance)
                {
                    client.Send(Constants.TradeInProgress);
                    return;
                }
                client.Trade.TraderUID = _client.Entity.UID;
                if (_client.Trade.TraderUID == client.Entity.UID)
                {
                    _client.Trade.InTrade = client.Trade.InTrade = true;
                    trade.Type = Trade.ShowTable;
                    client.Send(trade);
                    trade.dwParam = client.Entity.UID;
                    _client.Send(trade);
                }
                else
                {
                    client.Send(Constants.TradeRequest);
                    trade.dwParam = client.Entity.UID;
                    RequestInformation request = new RequestInformation();
                    request.Requester = client.Entity.UID;
                    request.Receiver = _client.Entity.UID;
                    request.Level = client.Entity.Level;
                    request.BattlePower = (uint)client.Entity.BattlePower;
                    if (client.Entity.Spouse == _client.Entity.Name)//Spouse
                    {
                        request.Spouse = RequestInformation.Request;
                    }
                    if (client.Friends != null && client.Friends.ContainsKey(_client.Entity.UID))//Friends
                    {
                        request.Friends = RequestInformation.Request;
                    }
                    if (client.Partners != null && client.Partners.ContainsKey(_client.Entity.UID))//TradePartner
                    {
                        request.TradePartner = RequestInformation.Request;
                    }
                    if (client.Apprentices != null && client.Apprentices.ContainsKey(_client.Entity.UID))//Mentor
                    {
                        request.Mentor = RequestInformation.Request;
                    }
                    if (client.Mentor != null)//Apprentice
                    {
                        if (client.Mentor.ID == _client.Entity.UID)
                            request.Apprentice = RequestInformation.Request;
                    }
                    if (_client.Mentor != null)
                        if (_client.Mentor.ID == client.Entity.UID)
                            request.Apprentice = RequestInformation.Request;
                    if (client.Team != null && _client.Team != null)//Team
                    {
                        if (client.Team.Contain(_client.Entity.UID))
                            if (_client.Team.Contain(client.Entity.UID))
                                request.Team = RequestInformation.Request;
                    }
                    if (client.Entity.GuildID == _client.Entity.GuildID)//Guild
                    {
                        request.GuildRank = RequestInformation.Request;
                    }
                    if (_client.Enemy != null && _client.Enemy.ContainsKey(client.Entity.UID))//Enemy
                    {
                        request.Enemy = RequestInformation.Request;
                    }
                    _client.Send(request);
                    _client.Send(trade);
                    _client.Send(request);
                }
            }
        }
        static void CloseTrade(Trade trade, Client.GameState client)
        {
            Client.GameState _client = null;
            if (Kernel.GamePool.TryGetValue(client.Trade.TraderUID, out _client))
            {
                _client.Trade = new Game.ConquerStructures.Trade();
                client.Trade = new Game.ConquerStructures.Trade();
                trade.Type = Trade.HideTable;
                trade.dwParam = _client.Entity.UID;
                client.Send(trade);
                trade.dwParam = client.Entity.UID;
                _client.Send(trade);
            }
        }
        static void SetTradeMoney(Trade trade, Client.GameState client)
        {
            Client.GameState _client = null;
            if (Kernel.GamePool.TryGetValue(client.Trade.TraderUID, out _client))
            {
                if ((ulong)client.Trade.Money == 0)
                {
                    if ((ulong)client.Entity.Money >= (ulong)trade.dwParam2)
                    {
                        client.Trade.Money = (ulong)trade.dwParam2;
                        trade.Type = Trade.ShowMoney;
                        _client.Send(trade);
                    }
                    else
                        CloseTrade(trade, client);
                }
            }
        }
        static void SetTradeConquerPoints(Trade trade, Client.GameState client)
        {
            Client.GameState _client = null;
            if (Kernel.GamePool.TryGetValue(client.Trade.TraderUID, out _client))
            {
                if (client.Trade.ConquerPoints == 0)
                {
                    if (client.Entity.ConquerPoints >= trade.dwParam)
                    {
                        client.Trade.ConquerPoints = trade.dwParam;
                        trade.Type = Trade.ShowConquerPoints;
                        _client.Send(trade);
                    }
                    else
                        CloseTrade(trade, client);
                }
            }
        }
        static void AddTradeItem(Trade trade, Client.GameState client)
        {
            ConquerItem item = null;
            if (client.Inventory.TryGetItem(trade.dwParam, out item))
            {
                Client.GameState _client = null;
                if (Kernel.GamePool.TryGetValue(client.Trade.TraderUID, out _client))
                {
                    Database.ConquerItemInformation infos = new Database.ConquerItemInformation(item.ID, 0);
                    try
                    {
                        if (!Game.Features.AtomicTrade.Tradable(client, _client, item))
                        {
                            trade.Type = Trade.RemoveItem;
                            client.Send(trade);
                            return;
                        }
                    }
                    catch
                    {
                        if (_client == null)
                            Console.WriteLine("null in _client");
                        if (client == null)
                            Console.WriteLine("null in client");
                        if (infos == null)
                            Console.WriteLine("null in infos");
                        if (infos.BaseInformation == null)
                            Console.WriteLine("null in infos.BaseInformation");
                        return;
                    }
                    if (_client.Inventory.Count + client.Trade.Items.Count >= 40 || client.Trade.Items.Count == 20)
                    {
                        trade.Type = Trade.RemoveItem;
                        client.Send(trade);
                        client.Send(Constants.TradeInventoryFull);
                        return;
                    }
                    client.Trade.Items.Add(item);
                    item.Mode = Game.Enums.ItemMode.Trade;
                    item.Send(_client);
                }
            }
        }
    }
}