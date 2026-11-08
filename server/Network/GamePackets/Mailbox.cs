using System;
using System.IO;
using System.Linq;
using System.Text;
using Revora.Game;
using Revora.Client;
using Revora.Network;
using Revora.Database;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public class Mailbox : Writer, Interfaces.IPacket
    {
        byte[] Packet;
        ushort Offset = 17;//17
        public Mailbox()
        {
            Packet = new byte[20];
            Ushort((ushort)(Packet.Length - 8), 0, Packet);
            Ushort(1046, 2, Packet);
        }
        public uint Count
        {
            get { return BitConverter.ReadUint(Packet, 5); }
            set { WriteUint(value, 5, Packet); }
        }
        public uint Page
        {
            get { return BitConverter.ReadUint(Packet, 9); }
            set { WriteUint(value, 9, Packet); }
        }
        public uint Unknown
        {
            get { return BitConverter.ReadUint(Packet, 13); }
            set { WriteUint(value, 13, Packet); }
        }
        public class PrizeInfo
        {
            public void WriteItem(BinaryWriter writer)
            {
                writer.Write(ID);
                writer.Write(Time);
                writer.Write(Sender);
                writer.Write(Subject);
                writer.Write(Message);
                writer.Write(goldprize);
                writer.Write(cpsprize);
                writer.Write(Attackment);
                if (ItemPrize != null)
                    writer.Write(ItemPrize.UID);
                else
                    writer.Write(ItemReference);
            }
            public PrizeInfo ReadItem(BinaryReader reader)
            {
                ID = reader.ReadUInt32();//4
                Time = reader.ReadUInt32();//8
                Sender = reader.ReadString();//10
                Subject = reader.ReadString();//12
                Message = reader.ReadString();//14
                goldprize = reader.ReadUInt32();//18
                cpsprize = reader.ReadUInt32();//22  
                Attackment = reader.ReadUInt32();//22
                ItemReference = reader.ReadUInt32();
                ItemPrize = ItemReference == 0 ? null : Database.ConquerItemTable.LoadItem(ItemReference);
                if (ItemPrize != null && ItemPrize.ID == 0) ItemPrize = null;
                return this;
            }
            internal uint ItemReference;
            public ConquerItem ItemPrize;
            public string Subject, Sender, Message;
            public uint ID, Time, cpsprize, Attackment, goldprize;
        }
        Random R = new Random();
        List<PrizeInfo> List = new List<PrizeInfo>();
        public void Check(GameState client, uint page)
        {
            List<Mailbox.PrizeInfo> prizes = client.Prizes.Values.ToList();
            List.Clear();
            Offset = 17;
            Packet = new byte[24];
            Ushort(16, 0, Packet);
            Ushort(1046, 2, Packet);
            Page = page;
            Unknown = (uint)prizes.Count;
            if (page >= prizes.Count) return;
            for (int i = (int)page; i < Math.Min((int)page + 7, prizes.Count); i++)
            {
                if (i < prizes.Count)
                {
                    List.Add(prizes[i]);
                }
            }
            if (List.Count > 0)
            {
                Packet = new byte[24 + 134 * List.Count];
                Ushort((ushort)(Packet.Length - 8), 0, Packet);
                Ushort(1046, 2, Packet);
                Count = (uint)List.Count;
                Page = page;
                Unknown = (uint)prizes.Count;
                for (int i = 0; i < List.Count; i++)
                    Append(List[i], client);
            }
        }
        public void Append(PrizeInfo prize, GameState C)
        {
            Uint(prize.ID, Offset, Packet);//uid 
            Offset += 4;
            WriteString(prize.Sender, Offset, Packet);//sender
            Offset += 32;
            WriteString(prize.Subject, Offset, Packet);//Subject
            Offset += 64;
            Uint(prize.goldprize, Offset, Packet);//attachment
            Offset += 8;
            Uint(prize.cpsprize, Offset, Packet);//attachment
            Offset += 12;
            Uint(prize.Time, Offset, Packet);//Time
            Offset += 8;
            if (prize.ItemPrize != null)
            {
                Uint(prize.ItemPrize.UID, Offset, Packet);//image  
                prize.ItemPrize.Mode = Enums.ItemMode.ItemInbox;//Enums.ItemMode.Inbox;
                prize.ItemPrize.Send(C);
            }
            Offset += 4;
            Uint(prize.Attackment, Offset, Packet);//Attackment
            Offset += 4;
        }
        public static void SendMailbox(GameState client, bool On)
        {
            byte[] test = new byte[20];
            WriteUshort((ushort)(test.Length - 8), 0, test);
            WriteUshort(1047, 2, test);
            if (On)
                Byte(3, 4, test);
            else
                Byte(2, 4, test);
            client.Send(test);
        }
        public static void AddPrize(GameState client, string Sender = "Slayer ", string Subject = "Inbox Test", string Message = "Message...", uint money = 5000000, uint cps = 50, uint Time = 600, uint attachment = 0, ConquerItem item = null)
        {
            if (!Revora.Game.Features.QuestInventory.Player(client, false) || !Revora.Game.Features.MailRewards.Ready(client)) return;
            lock (client.ItemSyncRoot)
            {
                if (client.Prizes.Count >= 255) throw new InvalidOperationException("Mailbox capacity reached.");
                int id = 0;
                do
                {
                    id = Kernel.Random.Next();
                }
                while (id == 0 || client.Prizes.ContainsKey((uint)id));
                PrizeInfo prize = new PrizeInfo();
                prize.ID = (uint)id;
                prize.Sender = Sender;
                prize.Subject = Subject;
                prize.Message = Message;
                prize.goldprize = money;
                prize.cpsprize = cps;
                prize.Time = Time;
                prize.ItemPrize = item;
                prize.Attackment = attachment;
                client.Prizes.Add(prize.ID, prize);
                MailboxTable.Save(client);
                if (!client.Entity.PersistenceRequiresReload && client.Prizes.Count > 0)
                    SendMailbox(client, true);
            }
        }
        public byte[] ToArray()
        {
            return Packet;
        }
        public void Send(GameState client)
        {
            client.Send(ToArray());
        }
        public void Deserialize(byte[] Data)
        {
            Packet = Data;
        }
        public static void Process(byte[] packet, GameState client)
        {
            Revora.Game.Features.MailRewards.Process(client, packet);
        }
    }
}
