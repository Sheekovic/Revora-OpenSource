using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Game.ConquerStructures;

namespace Revora.Network.GamePackets
{
    public class ChiPowers : Writer, Interfaces.IPacket
    {
        public const byte
            SpawnWindow = 0,
            Update = 1;

        byte[] Buffer;

        public ChiPowers(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[8 + 94];
                Writer.WriteUInt16(94, 0, Buffer);
                Writer.WriteUInt16(2534, 2, Buffer);
            }
        }

        public ushort Type
        {
            get { return BitConverter.ToUInt16(Buffer, 4); }
            set { WriteUInt16(value, 4, Buffer); }
        }

        public uint UID
        {
            get { return BitConverter.ToUInt32(Buffer, 6); }
            set { WriteUInt32(value, 6, Buffer); }
        }

        public uint Points
        {
            get { return BitConverter.ToUInt32(Buffer, 10); }
            set { WriteUInt32(value, 10, Buffer); }
        }

        public uint InfoLength
        {
            get { return BitConverter.ToUInt32(Buffer, 18); }
            set { WriteUInt32(value, 18, Buffer); }
        }

        public ChiPowers AppendInfo(ICollection<ChiPowerStructure> infos)
        {
            foreach (var info in infos)
                AppendInfo(info);
            return this;
        }
        public ChiPowers AppendInfo(ChiPowerStructure info)
        {
            InfoLength++;
            int offset = (int)(22 + (InfoLength - 1) * 17);

            Buffer[offset] = (byte)info.Power; offset++;
            foreach (var attribute in info.Attributes)
            {
                WriteInt32(attribute, offset, Buffer);
                offset += 4;
            }
            return this;
        }

        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }

        public byte[] ToArray()
        {
            return Buffer;
        }

        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }

        public ChiPowers Query(Client.GameState client, bool update = true)
        {
            UID = client.Entity.UID;
            if (!update) Type = SpawnWindow;
            else Type = Update;
            Points = client.ChiPoints;
            AppendInfo(client.ChiPowers);
            return this;
        }
    }
}