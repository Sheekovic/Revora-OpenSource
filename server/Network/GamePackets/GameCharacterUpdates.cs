// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class GameCharacterUpdates : Writer, Interfaces.IPacket
    {
        public const uint 
        Activated = 1 << 8,
        Deactivated = 1;
        public const uint
        Accelerated = 52,
        Decelerated = 53,
        Flustered = 54,
        Sprint = 55,
        DivineShield = 57,
        Stun = 58,
        Freeze = 59,
        Dizzy = 60,
        AzureShield = 93,
        SoulShacle = 111;

        byte[] Buffer;
        const byte minBufferSize = 16;
        public GameCharacterUpdates(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[minBufferSize + 8];
                WriteUInt16(minBufferSize, 0, Buffer);
                WriteUInt16(2075, 2, Buffer);
            }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public uint UpdateCount
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set
            {
                byte[] buffer = new byte[minBufferSize + 8 + 16 * value];
                int count = buffer.Length;
                if (count > Buffer.Length)
                    count = Buffer.Length;
                System.Buffer.BlockCopy(Buffer, 0, buffer, 0, count);
                WriteUInt16((ushort)(minBufferSize + 16 * value), 0, buffer);
                Buffer = buffer;
                WriteUInt32(value, 8, Buffer);
            }
        }
        public GameCharacterUpdates Add(uint updateID, uint shownamount, uint time, uint amount = 0)
        {
            UpdateCount = UpdateCount + 1;
            ushort offset = (ushort)(12 + (UpdateCount - 1) * 20);
            WriteUInt32(updateID, offset, Buffer);
            WriteUInt32(Activated, offset + 4, Buffer);
            WriteUInt32(shownamount, offset + 8, Buffer);
            WriteUInt32(time, offset + 12, Buffer);
            WriteUInt32(amount, offset + 16, Buffer);
            return this;
        }
        public GameCharacterUpdates Remove(uint updateID)
        {
            UpdateCount = UpdateCount + 1;
            ushort offset = (ushort)(12 + (UpdateCount - 1) * 20);
            WriteUInt32(updateID, offset, Buffer);
            WriteUInt32(Deactivated, offset + 4, Buffer);
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
    }
}