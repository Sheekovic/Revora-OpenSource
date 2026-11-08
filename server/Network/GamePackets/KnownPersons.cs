// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;

namespace Revora.Network.GamePackets
{
    public unsafe class KnownPersons : Writer, Interfaces.IPacket
    {
        public const byte
        FriendApply = 10,
        FriendAccept = 11,
        FriendOnline = 12,
        FriendOffline = 13,
        RemovePerson = 14,
        GetInfo = 15,
        EnemyOnline = 16,
        EnemyOffline = 17,
        RemoveEnemy = 18,
        AddEnemy = 19;

        byte[] Buffer;
        public KnownPersons(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[80];
                WriteUInt16(72, 0, Buffer);
                WriteUInt16(1019, 2, Buffer);
            }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public byte Type
        {
            get { return Buffer[8]; }
            set { Buffer[8] = value; }
        }
        public bool Online
        {
            get { return Buffer[9] == 1; }
            set { Buffer[9] = value == true ? (byte)1 : (byte)0; }
        }
        public uint Level
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }
        public Game.ConquerStructures.NobilityRank NobilityRank
        {
            get { return (Game.ConquerStructures.NobilityRank)Buffer[12]; }
            set { Buffer[12] = (byte)value; }
        }
        public bool IsBoy
        {
            get { return Buffer[16] == 1; }
            set { Buffer[16] = value == true ? (byte)1 : (byte)2; }
        }
        public string Name
        {
            get { return System.Text.Encoding.Default.GetString(Buffer, 20, 16); }
            set
            {
                Array.Clear(Buffer, 20, 16);
                char[] chars = (value ?? "").ToCharArray(); int usedChars, usedBytes; bool complete;
                System.Text.Encoding.Default.GetEncoder().Convert(chars, 0, chars.Length, Buffer, 20, 16, true,
                    out usedChars, out usedBytes, out complete);
            }
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
            if (!Game.Features.Friendships.Frame(packet) || !Game.Features.QuestInventory.Player(client, false)) return;
            if (packet[8] != RemoveEnemy) { Game.Features.Friendships.Process(packet, client); return; }
            lock (client.ItemSyncRoot)
            {
                if (!Game.Features.QuestInventory.Player(client, false) || client.Enemy == null) return;
                var person = new KnownPersons(false); person.Deserialize(packet); RemoveEnemys(person, client);
            }
        }
        static void RemoveEnemys(KnownPersons knownperson, Client.GameState client)
        {
            if (client.Enemy.ContainsKey(knownperson.UID))
            {
                Game.ConquerStructures.Society.Enemy enemy = client.Enemy[knownperson.UID];

                client.Enemy.Remove(enemy.ID);
                client.Send(new KnownPersons(true)
                {
                    UID = enemy.ID,
                    Type = KnownPersons.RemovePerson,
                    Name = "",
                    Online = false
                });
                Database.KnownPersons.RemoveEnemy(client, enemy.ID);
            }
        }
        public static void AddEnemys(Client.GameState client, Client.GameState enemy)
        {
            if (!client.Enemy.ContainsKey(enemy.Entity.UID))
            {
                client.Enemy.Add(enemy.Entity.UID, new Game.ConquerStructures.Society.Enemy()
                {
                    ID = enemy.Entity.UID,
                    Name = enemy.Entity.Name
                });
                client.Send(new KnownPersons(true)
                {
                    UID = enemy.Entity.UID,
                    Type = KnownPersons.AddEnemy,
                    Name = enemy.Entity.Name,
                    NobilityRank = client.Entity.NobilityRank,
                    IsBoy = Game.Features.Flowers.IsBoy(client.Entity.Body),
                    Online = true
                });
                Database.KnownPersons.AddEnemy(client, client.Enemy[enemy.Entity.UID]);
            }
        }
    }
}