using Revora.Client;
using Revora.Database;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revora.Network.GamePackets
{
    public class CMsgItemRefineOpt
    {
        private byte[] myPacket;
        public CMsgItemRefineOpt(byte[] incoming)
        {
            myPacket = incoming;
        }
        public bool Read()
        {
            try
            {
                var dePacket = new byte[myPacket.Length - 8];

                for (int i = 0; i < dePacket.Length; i++)
                {
                    dePacket[i] = myPacket[i];
                }
                Data = Serializer.DeserializeWithLengthPrefix<ItemRefineOperationProto>(new System.IO.MemoryStream(dePacket), PrefixStyle.Fixed32);
                return true;
            }
            catch
            {
                return false;
            }
        }
     /*   public bool Read()
        {
            try
            {
                var dePacket = new byte[myPacket.Length - 8];

                for (int i = 0; i < dePacket.Length; i++)
                {
                    dePacket[i] = myPacket[i];
                }
                Data = Serializer.DeserializeWithLengthPrefix<ItemRefineOperationProto>(new System.IO.MemoryStream(dePacket), PrefixStyle.Fixed32);
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
          */
        public void Handle(GameState client)
        {
            if (Data == null || !Game.Features.QuestInventory.Player(client)) return;
            lock (client.ItemSyncRoot) HandleCore(client);
        }
        private void HandleCore(GameState client)
        {
            if (!Game.Features.QuestInventory.Player(client)) return;
            switch (Data.Operation)
            {
                case ItemRefineOperationProto.Action.CpBoost:
                    Game.Features.PerfectionTempering.Boost(client, Data.ItemUID, Data.ItemArray);
                    break;
                case ItemRefineOperationProto.Action.Quicken:
                    Game.Features.PerfectionTempering.Quicken(client, Data.ItemUID);
                    break;
                case ItemRefineOperationProto.Action.TransferOwnership:
                    Game.Features.PerfectionTempering.Transfer(client, Data.ItemUID);
                    break;
                case ItemRefineOperationProto.Action.Exchange:
                    Game.Features.PerfectionTempering.Exchange(client, Data.ItemUID, Data.ItemArray);
                    break;
                case ItemRefineOperationProto.Action.Signature:
                    Game.Features.PerfectionTempering.Sign(client, Data.ItemUID, Data.Str);
                    break;
                case ItemRefineOperationProto.Action.Temper:
                    Game.Features.PerfectionTempering.Apply(client, Data.ItemUID, Data.ItemArray);
                    break;
            }
        }

        public uint GetPlusStonePoints(uint value)
        {
            if (value == 3009000) return 10;
            if (value == 3009001) return 100;
            if (value == 3009002) return 1000;
            if (value == 3009003) return 10000;
            if (value == 730001 || value == 1) return 1;
            if (value == 730002 || value == 2) return 1;
            if (value == 730003 || value == 3) return 1;
            if (value == 730004 || value == 4) return 1;
            if (value == 730005 || value == 5) return 1;
            if (value == 730006 || value == 6) return 1;
            if (value == 730007 || value == 7) return 1;
            if (value == 730008 || value == 8) return 2;
            if (value == 730009 || value == 9) return 1;
            // if (value == 9) return 87480;
            //if (value == 10) return 90180;
            //if (value == 11) return 95680;
            //if (value == 12) return 104680;
            return 0;
        }
        public double GetPerfectionProgress(uint value)
        {
            return (value * 1.25);
        }
        public uint GetPerfectionProgress(ConquerItem item)
        {
            if (item.PerfectionLevel == 0) return 30;
            if (item.PerfectionLevel == 1) return 60;
            if (item.PerfectionLevel == 2) return 100;
            if (item.PerfectionLevel == 3) return 200;
            if (item.PerfectionLevel == 4) return 350;
            if (item.PerfectionLevel == 5) return 600;
            if (item.PerfectionLevel == 6) return 1000;
            if (item.PerfectionLevel == 7) return 1500;
            if (item.PerfectionLevel == 8) return 2300;
            if (item.PerfectionLevel == 9) return 3500;
            if (item.PerfectionLevel == 10) return 5000;
            if (item.PerfectionLevel == 11) return 6500;
            if (item.PerfectionLevel == 12) return 8000;
            if (item.PerfectionLevel == 13) return 9500;
            if (item.PerfectionLevel == 14) return 11000;
            return 12000;
        }
        public static void SendMyPrefictionLevel(GameState client)
        {
            uint AllStarLevel = 0;
            foreach (var item in client.Equipment.Objects)
            {
                if (item == null)
                    continue;
                if (item.Position > 19)
                    continue;

                AllStarLevel += item.PerfectionLevel;
            }
            client.Send(new Revora.Systems_OverPower.PerfectionSystem.CMsgUserTotalRefineLev_3252() { EntityID = client.Entity.UID, type = 0, StarLeve = AllStarLevel }.ToConquerPacket(3252));
        }
        public ItemRefineOperationProto Data;
    }
    [ProtoContract]
    public class ItemRefineOperationProto
    {
        public enum Action
        {
            Temper,
            TransferOwnership,
            Signature,
            CpBoost,
            Exchange,
            Quicken
        }
        [ProtoMember(1, IsRequired = true)]
        public Action Operation;
        [ProtoMember(2, IsRequired = true)]
        public uint ItemUID;
        [ProtoMember(3, IsRequired = true)]
        public string Str;
        [ProtoMember(4, IsRequired = true)]
        public uint[] ItemArray;
    }
}
