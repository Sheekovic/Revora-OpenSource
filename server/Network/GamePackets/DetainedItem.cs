// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using Revora.Game;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class DetainedItem : Writer, Interfaces.IPacket 
    { 
        public const ushort DetainPage = 0, ClaimPage = 1; 
        public byte[] Buffer; 
        private ConquerItem item; 
        public DetainedItem(bool Create) 
        { 
            if (Create) 
            { 
                Buffer = new byte[176 + 8]; 
                WriteUInt16(176, 0, Buffer); 
                WriteUInt16(1034, 2, Buffer); 
            } 
        } 
        public uint UID 
        { 
            get { return BitConverter.ToUInt32(Buffer, 4); } 
            set { WriteUInt32(value, 4, Buffer); } 
        } 
        public uint ItemUID 
        { 
            get { return BitConverter.ToUInt32(Buffer, 8); } 
            set { WriteUInt32(value, 8, Buffer); } 
        } 
        public uint ItemID 
        { 
            get { return BitConverter.ToUInt32(Buffer, 12); } 
            set { WriteUInt32(value, 12, Buffer); } 
        } 
        public ushort Durability 
        { 
            get { return BitConverter.ToUInt16(Buffer, 16); } 
            set { WriteUInt16(value, 16, Buffer); } 
        } 
        public ushort MaximDurability 
        { 
            get { return BitConverter.ToUInt16(Buffer, 18); } 
            set { WriteUInt16(value, 18, Buffer); } 
        } 
        public byte Page 
        { 
            get { return Buffer[20]; } 
            set { Buffer[20] = value; } 
        } 
        public uint SocketProgress 
        { 
            get { return BitConverter.ToUInt32(Buffer, 24); } 
            set { WriteUInt32(value, 24, Buffer); } 
        } 
        public Enums.Gem SocketOne 
        { 
            get { return (Enums.Gem)Buffer[28]; } 
            set { Buffer[28] = (byte)value; } 
        } 
        public Enums.Gem SocketTwo 
        { 
            get { return (Enums.Gem)Buffer[29]; } 
            set { Buffer[29] = (byte)value; } 
        } 
        public Enums.ItemEffect Effect 
        { 
            get { return (Enums.ItemEffect)BitConverter.ToUInt16(Buffer, 30); } 
            set { WriteUInt16((ushort)value, 30, Buffer); } 
        } 
        public byte Plus 
        { 
            get { return Buffer[37]; } 
            set { Buffer[37] = value; } 
        } 
        public byte Bless 
        { 
            get { return Buffer[38]; } 
            set { Buffer[38] = value; } 
        } 
        public bool Bound 
        { 
            get { return Buffer[39] == 0 ? false : true; } 
            set { Buffer[39] = (byte)(value ? 1 : 0); } 
        } 
        public byte Enchant 
        { 
            get { return Buffer[40]; } 
            set { Buffer[40] = value; } 
        } 
        public bool Suspicious 
        { 
            get { return Buffer[40] == 0 ? false : true; } 
            set { Buffer[40] = (byte)(value ? 1 : 0); } 
        } 
        public byte Lock 
        { 
            get { return Buffer[47]; } 
            set { Buffer[47] = value; } 
        } 
        public Game.Enums.Color ItemColor 
        { 
            get { return (Game.Enums.Color)BitConverter.ToUInt32(Buffer, 52); } 
            set { WriteUInt32((uint)value, 52, Buffer); } 
        } 
        public uint OwnerUID 
        { 
            get { return BitConverter.ToUInt32(Buffer, 120); } 
            set { WriteUInt32(value, 120, Buffer); } 
        } 
        public string OwnerName 
        { 
            get 
            { 
                return Encoding.Default.GetString(Buffer, 124, 16).Split('\0')[0]; 
            } 
            set 
            { 
                if (value.Length > 16) 
                    value = value.Remove(16); 
                for (int count = 0; count < value.Length; count++) 
                    Buffer[124 + count] = (byte)(value[count]); 
            } 
        } 
        public uint GainerUID 
        { 
            get { return BitConverter.ToUInt32(Buffer, 140); } 
            set { WriteUInt32(value, 140, Buffer); } 
        } 
        public string GainerName 
        { 
            get 
            { 
                return Encoding.Default.GetString(Buffer, 144, 16).Split('\0')[0]; 
            } 
            set 
            { 
                if (value.Length > 16) 
                    value = value.Remove(16); 
                for (int count = 0; count < value.Length; count++) 
                    Buffer[144 + count] = (byte)(value[count]); 
            } 
        } 
        public uint Date2 
        { 
            get { return BitConverter.ToUInt32(Buffer, 160); } 
            set { WriteUInt32(value, 160, Buffer); } 
        } 
        public uint ConquerPointsCost 
        { 
            get { return BitConverter.ToUInt32(Buffer, 168); } 
            set { WriteUInt32(value, 168, Buffer); } 
        } 
        public uint DaysLeft 
        { 
            get { return (BitConverter.ToUInt32(Buffer, 170)); } 
            set { WriteUInt32(value, 170, Buffer); } 
        } 
        public DateTime Date 
        { 
            get; 
            set; 
        }
        public ItemAdding.Refinery_ ExtraEffect
        {
            get;
            set;
        } 
        public ItemAdding.Purification_ Purification
        { 
            get; 
            set; 
        } 
        public void Send(Client.GameState client) 
        { 
            client.Send(Buffer);             
        }         
        public ConquerItem Item 
        { 
            get { return item; } 
            set  
            { 
                item = value; 
                ItemUID = item.UID; 
                ItemID = item.ID; 
                ItemColor = item.Color; 
                Durability = item.Durability; 
                MaximDurability = item.MaximDurability; 
                SocketOne = item.SocketOne; 
                SocketTwo = item.SocketTwo; 
                Effect = item.Effect; 
                Plus = item.Plus; 
                Bless = item.Bless; 
                Enchant = item.Enchant; 
                SocketProgress = item.SocketProgress; 
                Bound = item.Bound; 
                Lock = item.Lock; 
                Purification = item.Purification; 
                ExtraEffect = item.ExtraEffect; 
            } 
        }
        public void MakeItReadyToClaim()
        { 
            ItemID = 0; 
            ItemColor = 0; 
            Durability = 0; 
            MaximDurability = 0; 
            SocketOne = Enums.Gem.NoSocket; 
            SocketTwo = Enums.Gem.NoSocket; 
            Effect = Enums.ItemEffect.None; 
            Plus = 0; 
            Bless = 0; 
            Enchant = 0; 
            SocketProgress = 0; 
            Bound = false; 
            Lock = 0; 
            WriteUInt32(ConquerPointsCost, 164, Buffer); 
            Page = 2; 
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