using System;
using Revora.Game;
using Revora.Client;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
   public class WindWalker
    {
        public class ThunderCloud
        {
            public static Counter ThunderCloudCounter = new Counter(700000);
            byte[] Buffer;
            public ushort X = 0, Y = 0;
            public uint UID = 0;
            public uint OwnerID;
            public GameState Owner;
            public DateTime EndTime;
            public bool Dead = false;
            public bool ThunderBolt = false;
            public Time32 LastCloudAttack;
            public ThunderCloud(GameState client, ushort X, ushort Y)
            {
                Owner = client;
                Owner.Entity.Cloud = this;
                EndTime = DateTime.Now.AddSeconds(24);
                this.X = X;
                this.Y = Y;
                OwnerID = client.Entity.UID;
                UID = ThunderCloudCounter.Next;
                string _Name = "Thundercloud";
                byte[] Packet_2035 = new byte[68 + 8];
                Writer.WriteUInt16((ushort)(Packet_2035.Length - 8), 0, Packet_2035);
                Writer.WriteUInt16(2035, 2, Packet_2035);
                Writer.WriteUInt32(UID, 4, Packet_2035);
                Writer.WriteUInt32(4264, 8, Packet_2035);
                Packet_2035[12] = 3;
                Writer.WriteUInt32(980, 6, Packet_2035);//mesh
                Packet_2035[24] = 14;
                Writer.WriteUInt32(22348141, 26, Packet_2035);
                Writer.WriteString(_Name, 30, Packet_2035);
                Buffer = new byte[8 + SpawnEntity.NameClan + _Name.Length + 36];
                Writer.WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
                Writer.WriteUInt16(10014, 2, Buffer);
                Writer.WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Buffer);
                Writer.WriteUInt32(980, 8, Buffer);//Mesh
                Writer.WriteUInt32(UID, 12, Buffer);//Uid
                var seconds = System.Math.Abs((EndTime - DateTime.Now).TotalSeconds);
                Writer.WriteUInt32((uint)((seconds * 10000) / 24), 111, Buffer);//Time
                Writer.WriteUInt32(140, 117, Buffer);//Level
                Writer.WriteUInt32(X, 119, Buffer);
                Writer.WriteUInt32(Y, 121, Buffer);
                Writer.WriteUInt32(5, 125, Buffer);
                Writer.WriteUInt32(client.Entity.UID, 274, Buffer);//OwnerUID
                Buffer[278] = 1;
                Buffer[220] = 1;
                Writer.WriteUInt16(768, 270, Buffer);
                Writer.WriteStringList(new List<string>() { _Name, "", "", "", "", "", "" }, SpawnEntity.NameClan, Buffer);
                Network.GamePackets.Data d = new Data(true);
                d.UID = UID;
                d.ID = 134;
                d.Thundercloud = 5;
                //d.Data24_Uint = 22348141;
                client.Send(Packet_2035);
                client.Send(Buffer);
                client.Send(d);
                client.Map.AddCloud(this);
            }
            public void DisposeCloud(GameState c)
            {
                if (c.Map.Clouds.ContainsKey(UID))
                    c.Map.Clouds.Remove(UID);
                Data d = new Data(true);
                d.UID = UID;
                d.ID = 135;
                c.SendScreen(d, true);
                ThunderBolt = false;
                if (Owner != null)
                    Owner.Entity.ThunderBolt = false;
                Dead = true;
            }
            public void SendSpawn(GameState Owner, bool p)
            {

                var seconds = System.Math.Abs((EndTime - DateTime.Now).TotalSeconds);
                if (seconds > 24) seconds = 0;
                Writer.WriteUInt32((uint)((seconds * 10000) / 24), 111, Buffer);//Time
                Owner.Send(Buffer);
            }
        }
        public enum CharacterType : byte
        {
            Chaser = 7,
            Stomper = 15
        }
        public static void SwitchToRanged(GameState client)
        {
            Data data = new Data(true);
            data.ID = 205;
            data.UID = client.Entity.UID;
            data.TimeStamp2 = (uint)Time32.timeGetTime().GetHashCode();
            client.Send(data);
        }
        public static void SendGreenEffect(GameState client)
        {
            _String str = new _String(true);
            str.UID = client.Entity.UID;
            str.TextsCount = 1;
            str.Type = _String.Effect;
            str.Texts.Add("TSM_SXJ_HPhf");
            client.SendScreen(str.ToArray(), true);
        }
        public static void JusticeChainEquipment(GameState client)
        {
            if (client.Equipment.Objects[3] != null && client.Equipment.Objects[4] != null)
            {
                if ((uint)(client.Equipment.Objects[3].ID / 1000) == 626 && (uint)(client.Equipment.Objects[4].ID / 1000) == 626)
                {
                    if (!client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.JusticeChant))
                        client.Entity.AddFlag((ulong)FlagsUpdate.Flags.JusticeChant,StatusFlagsBigVector32.PermanentFlag,false);
                }
                else if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.JusticeChant))
                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.JusticeChant);
            }
            else if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.JusticeChant))
                client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.JusticeChant);
        }

        internal static void SendRevengeTail(Game.Entity attacker, Game.Entity attacked, ref int damage)
        {
            uint dmg = (uint)damage;
            var spell = Database.SpellTable.GetSpell(13030, attacker.Owner);
            if (damage < spell.Power)
            {
                SpellUse suse = new SpellUse(true);
                suse.Attacker = attacker.UID;
                suse.Attacker1 = attacked.UID;
                suse.SpellID = spell.ID;
                suse.SpellLevel = spell.Level;
                
                var attack = new Attack(true);
                attack.Attacked = attacker.UID;
                attack.Attacker = attacked.UID;
                attack.AttackType = Network.GamePackets.Attack.Melee;
                attack.Damage = 0;
                attack.MonsterSpellID = 1;
                attack.ResponseDamage = (uint)damage;
                attack.X = attacked.X;
                attack.Y = attacked.Y;
                suse.AddTarget(attacked.UID, dmg, attack);
                attacker.SendScreen(attack);
                attacker.SendScreen(suse);
                if (attacker.Hitpoints <= damage)
                    attacker.Die(attacked);
                else attacker.Hitpoints -= dmg;
                attacked.RemoveFlag((ulong)FlagsUpdate.Flags.RevengeTaill);
            }

        }
    }
}


