using System;
using System.Linq;
using Revora.Interfaces;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class Spell : Writer, IPacket, ISkill
    {
        public static List<Soul_Level> SkillSoul_values = new List<Soul_Level>
        {
            Soul_Level.Default,
            Soul_Level.Level_One,
            Soul_Level.Level_Two,
            Soul_Level.Level_Three,
            Soul_Level.Level_Four
        };
        byte _Souls;
        public int FireCurseDamage { get; set; }
        #region ColdTime
        public Time32 ColdTime { get; set; }
        public bool IsSpellWithColdTime { get; set; }

        public int GetColdTime
        {
            get
            {
                return (ColdTime.AllMilliseconds() - Time32.Now.AllMilliseconds());
            }
        }
        #endregion
        public Soul_Level Souls
        {
            get { return (Soul_Level)_Souls; }
            set
            {
                _Souls = (byte)value;
                if (_Souls == 0)
                    _Souls = (byte)Soul_Level.Default;
                Buffer[24] = (byte)_Souls;
                Buffer[28] = Buffer[24];
            }
        }
        public enum Soul_Level : byte
        {
            Default = 1 << 0,
            LevelOne = 1 << 1,
            LevelTwo = 1 << 2,
            LevelThree = 1 << 3,
            LevelFour = 1 << 4,
            Level_One = Default | LevelOne,
            Level_Two = Level_One | LevelTwo,
            Level_Three = Level_Two | LevelThree,
            Level_Four = Level_Three | LevelFour,
        }
        public void AddFlag(Soul_Level flag)
        {
            Souls |= flag;
        }
        public bool ContainsFlag(Soul_Level flag)
        {
            Soul_Level aux = Souls;
            aux &= ~flag;
            return !(aux == Souls);
        }
        public void RemoveFlag(Soul_Level flag)
        {
            if (ContainsFlag(flag))
            {
                Souls &= ~flag;
            }
        }
        public byte[] Buffer { get; set; }
        public Spell(bool Create)
        {
            Buffer = new byte[40];
            WriteUInt16(32, 0, Buffer);
            WriteUInt16(1103, 2, Buffer);
            WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Buffer);
            Souls = Soul_Level.Default;
            _Available = false;
        }
        byte _Levelhu;
        public byte LevelHu
        {
            get { return _Levelhu; }
            set
            {
                _Levelhu = value;
                uint IntegerFlag = 0;
                if (_Levelhu >= 1)
                    IntegerFlag |= (uint)(1UL << 1);
                if (_Levelhu >= 2)
                    IntegerFlag |= (uint)(1UL << 4);
                if (_Levelhu >= 3)
                    IntegerFlag |= (uint)(1UL << 8);
                if (_Levelhu >= 4)
                    IntegerFlag |= (uint)(1UL << 16);
                WriteUInt32(IntegerFlag, 25, Buffer);
            }
        }
        public byte LevelHu2
        {
            get;
            set;
        }
        public byte Level
        {
            get { return (byte)BitConverter.ToUInt32(Buffer, 16); }
            set
            {
                if (Database.SpellTable.SpellInformations != null)
                    if (Database.SpellTable.SpellInformations.ContainsKey(ID))
                        if (!Database.SpellTable.SpellInformations[ID].ContainsKey(value))
                            value = Database.SpellTable.SpellInformations[ID].Keys.LastOrDefault();
                WriteUInt16(value, 16, Buffer);
            }
        }
        public uint Experience
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public ushort ID
        {
            get { return (ushort)BitConverter.ToUInt16(Buffer, 12); }
            set { WriteUInt16(value, 12, Buffer); }
        }
        private bool _Available;
        public bool Available
        {
            get
            {
                return _Available;
            }
            set
            {
                _Available = value;
            }
        }
        private byte _PreviousLevel;
        public byte PreviousLevel
        {
            get { return _PreviousLevel; }
            set { _PreviousLevel = value; }
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
            client.Send(this);
        }
        public static void Poroces(byte[] packet, Client.GameState client)
        {
            {
                Spell spell = new Spell(false);
                spell.Deserialize(packet);
                if (!client.Spells.ContainsKey(spell.ID)) return;
                switch (packet[18])
                {
                    case 5://Remove 
                        {
                            Spell.Soul_Level remove = Spell.SkillSoul_values[(int)packet[20]];
                            if (client.Spells[spell.ID].ContainsFlag(remove))
                            {
                                client.Spells[spell.ID].RemoveFlag(remove);
                                Data data = new Data(true);
                                data.UID = client.Entity.UID;
                                data.dwParam = client.Spells[spell.ID].ID;
                                data.ID = 109;
                                client.Send(data);
                                client.Send(new Spell(true)
                                {
                                    ID = client.Spells[spell.ID].ID,
                                    Level = client.Spells[spell.ID].Level,
                                    PreviousLevel = client.Spells[spell.ID].PreviousLevel,
                                    Experience = 0,
                                    Souls = client.Spells[spell.ID].Souls,
                                    Available = true
                                }.ToArray());
                                client.Spells[spell.ID].Send(client);
                                Database.SkillTable.SaveSpells(client);
                            }
                            break;
                        }
                    case 2://Flag
                        {
                            switch (packet[20])
                            {
                                case 1:
                                case 8: client.Spells[spell.ID].LevelHu2 = 1; break;
                                case 2:
                                case 16: client.Spells[spell.ID].LevelHu2 = 2; break;
                                case 3:
                                case 24: client.Spells[spell.ID].LevelHu2 = 3; break;
                                case 4:
                                case 32: client.Spells[spell.ID].LevelHu2 = 4; break;
                                default:
                                    {
                                        client.Spells[spell.ID].LevelHu2 = packet[20];
                                    }
                                    break;
                            }
                            break;
                        }
                }
                packet[18] = 3;
                client.Send(packet);
            }
        }
    }
}