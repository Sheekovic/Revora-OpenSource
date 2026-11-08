// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;

namespace Revora.Network.GamePackets
{
    public unsafe class ItemLock : Writer, Interfaces.IPacket
    {
        public const uint
        RequestLock = 0,
        RequestUnlock = 1,
        UnlockDate = 2;

        byte[] Buffer;
        public ItemLock(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[24];
                WriteUInt16(16, 0, Buffer);
                WriteUInt16(2048, 2, Buffer);
            }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public uint ID
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public uint dwParam
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
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
        public static void Process(byte[] packet, Client.GameState client)
        {
            if (client.Action != 2) return;
            ItemLock itemlock = new ItemLock(false);
            itemlock.Deserialize(packet);
            switch (itemlock.ID)
            {
                case ItemLock.RequestLock:
                    {
                        LockItem(itemlock, client);
                        break;
                    }
                case ItemLock.RequestUnlock:
                    {
                        UnlockItem(itemlock, client);
                        break;
                    }
            }
        }
        static void LockItem(ItemLock itemLock, Client.GameState client)
        {
            ConquerItem item = null;
            if (client.Inventory.TryGetItem(itemLock.UID, out item))
            {
                itemLock.ID = 1;
                item.Lock = 1;
                item.Mode = Game.Enums.ItemMode.Update;
                client.Send(itemLock);
                item.Send(client);
                Database.ConquerItemTable.UpdateLock(item);
            }
            else if (client.Equipment.TryGetItem(itemLock.UID) != null)
            {
                item = client.Equipment.TryGetItem(itemLock.UID);
                itemLock.ID = 1;
                item.Lock = 1;
                item.Mode = Game.Enums.ItemMode.Update;
                client.Send(itemLock);
                item.Send(client);
                Database.ConquerItemTable.UpdateLock(item);
            }
        }
        static void UnlockItem(ItemLock itemLock, Client.GameState client)
        {
            ConquerItem item = null;
            if (client.Inventory.TryGetItem(itemLock.UID, out item))
            {
                if (item.Lock == 1)
                {
                    item.Lock = 2;
                    item.UnlockEnd = DateTime.Now.AddDays(5);
                    item.Mode = Game.Enums.ItemMode.Update;
                    item.Send(client);
                    Database.ConquerItemTable.UpdateLock(item);
                }
                else client.Send(new Message("Can't unlock an item that is in progress of unlocking.", System.Drawing.Color.FloralWhite, Message.TopLeft));
            }
        }
    }
}