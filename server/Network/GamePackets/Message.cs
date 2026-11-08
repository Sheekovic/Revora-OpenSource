using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Revora.Network.GamePackets
{
    public unsafe class Message : Interfaces.IPacket
    {
        [ProtoContract]
        public class CMsgTalk
        {
            [ProtoMember(1, IsRequired = true)]
            public int Color;
            [ProtoMember(2, IsRequired = true)]
            public uint ChatType;
            [ProtoMember(3, IsRequired = true)]
            public uint MessageUID1;
            [ProtoMember(4, IsRequired = true)]
            public uint unkown;
            [ProtoMember(5, IsRequired = true)]
            public uint unkown1;
            [ProtoMember(6, IsRequired = false)]
            public uint Mesh;
            [ProtoMember(7, IsRequired = true)]
            public uint OfficelRank;
            [ProtoMember(8, IsRequired = true)]
            public uint _ISMyServer;
            [ProtoMember(9, IsRequired = true)]
            public uint unkown4;
            [ProtoMember(10, IsRequired = true)]
            public List<byte[]> Chat = new List<byte[]>();//4String
        }
        public uint Mesh;
        public string _To;
        public Color Color;
        public string _From;
        public uint ChatType;
        public string __Message;

        public const uint
        Talk = 2000,
        Whisper = 2001,
        Team = 2003,
        Guild = 2004,
        Clan = 2006,
        System = 2007,
        Friend = 2009,
        Center = 2011,
        TopLeft = 2012,
        Service = 2014,
        Tip = 2015,
        Unknown = 2016,
        World = 2021,
        Qualifier = 2022,
        PopUP = 2100,
        Dialog = 2101,
        Website = 2105,
        FirstRightCorner = 2108,
        ContinueRightCorner = 2109,
        SystemWhisper = 2110,
        GuildAnnouncement = 2111,
        Agate = 2115,
        ArenaQualifier = 2022,
        ChiSystem = 2024,
        JiangHu = 2026,
        HawkMessage = 2104,
        Middle = 2115,
        //CrossServer = 2402,
        BroadcastMessage = 2500,
        Monster = 2600,
        SlideFromRight = 100000,
        SlideFromRightRedVib = 1000000,
        WhiteVibrate = 10000000;

        public Message(string _Message, uint _ChatType)
        {
            this.Mesh = 0;
            this.__Message = _Message;
            this._To = "ALL";
            this._From = "SYSTEM";
            this.Color = Color.Red;
            this.ChatType = _ChatType;
        }
        public Message(string _Message, Color _Color, uint _ChatType)
        {
            this.Mesh = 0;
            this.__Message = _Message;
            this._To = "ALL";
            this._From = "SYSTEM";
            this.Color = _Color;
            this.ChatType = _ChatType;
        }
        public Message(string _Message, string __To, Color _Color, uint _ChatType)
        {
            this.Mesh = 0;
            this.__Message = _Message;
            this._To = __To;
            this._From = "SYSTEM";
            this.Color = _Color;
            this.ChatType = _ChatType;
        }
        public Message(string _Message, string __To, string __From, Color _Color, uint _ChatType)
        {
            this.Mesh = 0;
            this.__Message = _Message;
            this._To = __To;
            this._From = __From;
            this.Color = _Color;
            this.ChatType = _ChatType;
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            if (client.Action != 2) return;
            Message message = new Message();
            message.Deserialize(packet);
            if (message.__Message.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries).Length > 0)
                message.__Message = message.__Message.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries)[0];
            Chat.Chats(message, client);
        }
        public Message()
        {
            this.Mesh = 0;
        }
        public uint MessageUID1 = 0;
        public uint MessageUID2 = 0;
        public CMsgTalk Buffer;

        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer.ConvertToProto<CMsgTalk>();
            this.Color = Color.FromArgb(Buffer.Color);
            this.ChatType = Buffer.ChatType;
            this.MessageUID1 = Buffer.MessageUID1;
            this.Mesh = Buffer.Mesh;
            this._From = Encoding.Default.GetString(Buffer.Chat[0]);
            this._To = Encoding.Default.GetString(Buffer.Chat[1]);
            this.__Message = Encoding.Default.GetString(Buffer.Chat[3]);

        }

        public static byte[] EncodeNewRole()
        {
            return Encode6711Talk(uint.MaxValue, Dialog, "SYSTEM", "ALLUSERS", "NEW_ROLE");
        }
        static byte[] Encode6711ControlTalk(uint channel, string text)
        {
            if (text == null) text = "";
            List<byte> body = new List<byte>(80);
            WriteTalkVarint(body, 1, 0xFFFFA500);
            WriteTalkVarint(body, 2, channel);
            WriteTalkVarint(body, 3, 0);
            WriteTalkVarint(body, 4, 0);
            WriteTalkVarint(body, 5, 0);
            WriteTalkVarint(body, 7, 0);
            WriteTalkVarint(body, 8, 0);
            WriteTalkVarint(body, 9, 0);
            string[] parts = new string[] { "SYSTEM", "ALLUSERS", "", text };
            for (int i = 0; i < parts.Length; i++)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(parts[i]);
                body.Add(82);
                WriteTalkRaw(body, (uint)bytes.Length);
                for (int n = 0; n < bytes.Length; n++)
                    body.Add(bytes[n]);
            }
            int declared = 4 + body.Count;
            byte[] packet = new byte[declared + 8];
            packet[0] = (byte)declared;
            packet[1] = (byte)(declared >> 8);
            packet[2] = 0xEC;
            packet[3] = 0x03;
            body.CopyTo(packet, 4);
            return packet;
        }
        static void WriteTalkVarint(List<byte> body, int field, uint value)
        {
            WriteTalkRaw(body, (uint)(field << 3));
            WriteTalkRaw(body, value);
        }
        static void WriteTalkRaw(List<byte> body, uint value)
        {
            while (value >= 128)
            {
                body.Add((byte)(value | 128));
                value >>= 7;
            }
            body.Add((byte)value);
        }
        static byte[] Encode6711Talk(uint color, uint channel, string from, string to, string text)
        {
            if (from == null) from = "SYSTEM";
            if (to == null) to = "ALLUSERS";
            if (text == null) text = "";
            List<byte> body = new List<byte>(96);
            WriteTalkVarint(body, 1, color);
            WriteTalkVarint(body, 2, channel);
            WriteTalkVarint(body, 3, 0);
            WriteTalkVarint(body, 4, 0);
            WriteTalkVarint(body, 5, 0);
            WriteTalkVarint(body, 6, 0);
            string[] parts = new string[] { from, to, "", text, "", "", "" };
            for (int i = 0; i < parts.Length; i++)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(parts[i]);
                body.Add(82);
                WriteTalkRaw(body, (uint)bytes.Length);
                for (int n = 0; n < bytes.Length; n++)
                    body.Add(bytes[n]);
            }
            int declared = 4 + body.Count;
            byte[] packet = new byte[declared + 8];
            packet[0] = (byte)declared;
            packet[1] = (byte)(declared >> 8);
            packet[2] = 0xEC;
            packet[3] = 0x03;
            body.CopyTo(packet, 4);
            return packet;
        }
        public byte[] ToArray()
        {
            if (this.ChatType == Dialog || this.ChatType == PopUP)
                return Encode6711ControlTalk(this.ChatType, this.__Message);
            return Encode6711Talk((uint)this.Color.ToArgb(), this.ChatType,
                this._From, this._To, this.__Message);
        }
        public void Send(Client.GameState client)
        {
            client.Send(ToArray());
        }
        public class MessageBoard
        {
            private const Int32 TITLE_SIZE = 44;
            private const Int32 LIST_SIZE = 10;
            private static List<MessageInfo> TradeBoard = new List<MessageInfo>();
            private static List<MessageInfo> FriendBoard = new List<MessageInfo>();
            private static List<MessageInfo> TeamBoard = new List<MessageInfo>();
            private static List<MessageInfo> SynBoard = new List<MessageInfo>();
            private static List<MessageInfo> OtherBoard = new List<MessageInfo>();
            private static List<MessageInfo> SystemBoard = new List<MessageInfo>();
            public struct MessageInfo
            {
                public String Author;
                public String Words;
                public String Date;
            };
            public static void Add(String Author, String Words, ushort Channel)
            {
                MessageInfo Info = new MessageInfo();
                Info.Author = Author;
                Info.Words = Words;
                Info.Date = DateTime.Now.ToString("yyyyMMddHHmmss");
                switch (Channel)
                {
                    case 2201:
                        TradeBoard.Add(Info);
                        break;
                    case 2202:
                        FriendBoard.Add(Info);
                        break;
                    case 2203:
                        TeamBoard.Add(Info);
                        break;
                    case 2204:
                        SynBoard.Add(Info);
                        break;
                    case 2205:
                        OtherBoard.Add(Info);
                        break;
                    case 2206:
                        SystemBoard.Add(Info);
                        break;
                }
            }
            public static void Delete(MessageInfo Message, ushort Channel)
            {
                switch (Channel)
                {
                    case 2201:
                        if (TradeBoard.Contains(Message))
                            TradeBoard.Remove(Message);
                        break;
                    case 2202:
                        if (FriendBoard.Contains(Message))
                            FriendBoard.Remove(Message);
                        break;
                    case 2203:
                        if (TeamBoard.Contains(Message))
                            TeamBoard.Remove(Message);
                        break;
                    case 2204:
                        if (SynBoard.Contains(Message))
                            SynBoard.Remove(Message);
                        break;
                    case 2205:
                        if (OtherBoard.Contains(Message))
                            OtherBoard.Remove(Message);
                        break;
                    case 2206:
                        if (SystemBoard.Contains(Message))
                            SystemBoard.Remove(Message);
                        break;
                }
            }
            public static String[] GetList(UInt16 Index, ushort Channel)
            {
                MessageInfo[] Board = null;
                switch (Channel)
                {
                    case 2201:
                        Board = TradeBoard.ToArray();
                        break;
                    case 2202:
                        Board = FriendBoard.ToArray();
                        break;
                    case 2203:
                        Board = TeamBoard.ToArray();
                        break;
                    case 2204:
                        Board = SynBoard.ToArray();
                        break;
                    case 2205:
                        Board = OtherBoard.ToArray();
                        break;
                    case 2206:
                        Board = SystemBoard.ToArray();
                        break;
                    default: return null;
                }
                if (Board.Length == 0) return null;
                if ((Index / 8 * LIST_SIZE) > Board.Length) return null;
                String[] List = null;
                Int32 Start = (Board.Length - ((Index / 8 * LIST_SIZE) + 1));
                if (Start < LIST_SIZE)
                    List = new String[(Start + 1) * 3];
                else
                    List = new String[LIST_SIZE * 3];
                Int32 End = (Start - (List.Length / 3));
                Int32 x = 0;
                for (Int32 i = Start; i > End; i--)
                {
                    List[x + 0] = Board[i].Author;
                    if (Board[i].Words.Length > TITLE_SIZE)
                        List[x + 1] = Board[i].Words.Remove(TITLE_SIZE, Board[i].Words.Length - TITLE_SIZE);
                    else
                        List[x + 1] = Board[i].Words;
                    List[x + 2] = Board[i].Date;
                    x += 3;
                }
                return List;
            }
            public static String GetWords(String Author, ushort Channel)
            {
                MessageInfo[] Board = null;
                switch (Channel)
                {
                    case 2201:
                        Board = TradeBoard.ToArray();
                        break;
                    case 2202:
                        Board = FriendBoard.ToArray();
                        break;
                    case 2203:
                        Board = TeamBoard.ToArray();
                        break;
                    case 2204:
                        Board = SynBoard.ToArray();
                        break;
                    case 2205:
                        Board = OtherBoard.ToArray();
                        break;
                    case 2206:
                        Board = SystemBoard.ToArray();
                        break;
                    default: return "";
                }

                foreach (MessageInfo Info in Board)
                {
                    if (Info.Author == Author)
                        return Info.Words;
                }
                return "";
            }
            public static MessageInfo GetMsgInfoByAuthor(String Author, ushort Channel)
            {
                MessageInfo[] Board = null;
                switch (Channel)
                { // Reparado Por Pezzi Tomas
                    case 2201:
                        Board = TradeBoard.ToArray();
                        break;
                    case 2202:
                        Board = FriendBoard.ToArray();
                        break;
                    case 2203:
                        Board = TeamBoard.ToArray();
                        break;
                    case 2204:
                        Board = SynBoard.ToArray();
                        break;
                    case 2205:
                        Board = OtherBoard.ToArray();
                        break;
                    case 2206:
                        Board = SystemBoard.ToArray();
                        break;
                    default: return new MessageInfo();
                }

                foreach (MessageInfo Info in Board)
                {
                    if (Info.Author == Author)
                        return Info;
                }
                return new MessageInfo();
            }
        }
    }
}