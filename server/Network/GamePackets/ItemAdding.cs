using System;

namespace Revora.Network.GamePackets
{
    public unsafe class ItemAdding : Writer, Interfaces.IPacket
    {
        public struct Purification_
        {
            public bool Available;
            public uint ItemUID;
            public uint PurificationItemID;
            public uint PurificationLevel;
            public uint PurificationDuration;
            public DateTime AddedOn;
        }
        public struct Refinery_
        {
            public bool Available;
            public uint ItemUID;
            public uint EffectID;
            public uint EffectLevel;
            public uint EffectPercent;
            public uint EffectDuration;
            public DateTime AddedOn;
        }
        public const uint
        ExtraEffect = 2,
        PurificationAdding = 6;
        public const uint
        PermanentRefinery = 3,
        StabilizationEffectRefined = 4,
        PurificationEffect = 5,
        ExpireTime = 7,
        StabilizationEffect = 9,
        Warehause = 10;

        byte[] Buffer;
        const byte minBufferSize = 8;
        public ItemAdding(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[minBufferSize + 8];
                WriteUInt16(minBufferSize, 0, Buffer);
                WriteUInt16(2077, 2, Buffer);
            }
        }
        public uint Type
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }
        public uint UpdateCount
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set
            {
                byte[] buffer = new byte[minBufferSize + 8 + 32 * value];
                int count = buffer.Length;
                if (count > Buffer.Length)
                    count = Buffer.Length;
                System.Buffer.BlockCopy(Buffer, 0, buffer, 0, count);
                WriteUInt16((ushort)(minBufferSize + 32 * value), 0, buffer);
                Buffer = buffer;
                WriteUInt32(value, 4, Buffer);
            }
        }
        public bool Append(Refinery_ effect)
        {
            UpdateCount = UpdateCount + 1;
            ushort offset = (ushort)(8 + (UpdateCount - 1) * 32);
            WriteUInt32(effect.ItemUID, offset, Buffer);
            if (effect.EffectDuration == 0)
                WriteUInt32(PermanentRefinery, offset + 4, Buffer);
            else
                WriteUInt32(ExtraEffect, offset + 4, Buffer);
            if (effect.EffectID >= 724440 && effect.EffectID <= 724444 || effect.EffectID == 3004140) effect.EffectID = 301;
            WriteUInt32(effect.EffectID, offset + 8, Buffer);
            WriteUInt32(effect.EffectLevel, offset + 12, Buffer);
            WriteUInt32(effect.EffectPercent, offset + 16, Buffer);
            if (effect.EffectPercent != 0)
            {
                TimeSpan span1 = new TimeSpan(effect.AddedOn.AddSeconds(effect.EffectDuration).Ticks);
                TimeSpan span2 = new TimeSpan(DateTime.Now.Ticks);
                int secondsleft = (int)(span1.TotalSeconds - span2.TotalSeconds);
                if (secondsleft <= 0 && effect.EffectDuration != 0)
                {
                    effect.Available = false;
                    UpdateCount -= 1;
                    return false;
                }
                WriteUInt32((uint)secondsleft, offset + 24, Buffer);
            }
            return true;
        }
        public bool Append2(Refinery_ effect)
        {
            this.UpdateCount++;
            ushort offset = (ushort)(8 + ((this.UpdateCount - 1) * 32));
            Writer.WriteUInt32(effect.ItemUID, offset, this.Buffer);
            Writer.WriteUInt32(Type, offset + 4, this.Buffer);
            Writer.WriteUInt32(effect.EffectID, offset + 8, this.Buffer);
            Writer.WriteUInt32(effect.EffectLevel, offset + 12, this.Buffer);
            Writer.WriteUInt32(effect.EffectPercent, offset + 16, this.Buffer);
            if (effect.EffectPercent != 0)
            {
                TimeSpan span = new TimeSpan(effect.AddedOn.AddSeconds((double)effect.EffectDuration).Ticks);
                TimeSpan span2 = new TimeSpan(DateTime.Now.Ticks);
                int num2 = (int)(span.TotalSeconds - span2.TotalSeconds);
                if (num2 <= 0 && effect.EffectDuration != 0)
                {
                    effect.Available = false;
                    this.UpdateCount--;
                    return false;
                }
                Writer.WriteUInt32((uint)num2, offset + 24, this.Buffer);
            }
            return true;
        }
        public bool Append(Purification_ purify)
        {
            UpdateCount = UpdateCount + 1;
            ushort offset = (ushort)(8 + (UpdateCount - 1) * 32);
            WriteUInt32(purify.ItemUID, offset, Buffer);
            WriteUInt32(PurificationAdding, offset + 4, Buffer);
            WriteUInt32(purify.PurificationItemID, offset + 8, Buffer);
            WriteUInt32(purify.PurificationLevel, offset + 12, Buffer);
            if (purify.PurificationDuration != 0)
            {
                TimeSpan span1 = new TimeSpan(purify.AddedOn.AddSeconds(purify.PurificationDuration).Ticks);
                TimeSpan span2 = new TimeSpan(DateTime.Now.Ticks);
                int secondsleft = (int)(span1.TotalSeconds - span2.TotalSeconds);
                if (secondsleft <= 0)
                {
                    purify.Available = false;
                    UpdateCount = UpdateCount - 1;
                    return false;
                }
                WriteUInt32((uint)secondsleft, offset + 24, Buffer);
            }
            return true;
        }
        public bool Append2(Purification_ purify)
        {
            this.UpdateCount++;
            ushort offset = (ushort)(8 + ((this.UpdateCount - 1) * 32));
            Writer.WriteUInt32(purify.ItemUID, offset, this.Buffer);
            Writer.WriteUInt32(Type, offset + 4, this.Buffer);
            Writer.WriteUInt32(purify.PurificationItemID, offset + 8, this.Buffer);
            Writer.WriteUInt32(purify.PurificationLevel, offset + 12, this.Buffer);
            if (purify.PurificationDuration != 0)
            {
                TimeSpan span = new TimeSpan(purify.AddedOn.AddSeconds((double)purify.PurificationDuration).Ticks);
                TimeSpan span2 = new TimeSpan(DateTime.Now.Ticks);
                int num2 = (int)(span.TotalSeconds - span2.TotalSeconds);
                if (num2 <= 0)
                {
                    purify.Available = false;
                    this.UpdateCount--;
                    return false;
                }
                Writer.WriteUInt32((uint)num2, offset + 24, this.Buffer);
            }
            return true;
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
    }
}