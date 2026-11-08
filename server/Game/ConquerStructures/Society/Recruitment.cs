using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revora.Game.ConquerStructures.Society
{
   public class Recruitment
    {
        public Recruitment()
        {
            NotAllowFlag = 0;
        }
        public enum Mode
        {
            Requirements, Recruit
        }
        public class Flags
        {
            public const int
                NoneBlock = 0,
                Trojan = 1,
                Warrior = 2,
                Taoist = 4,
                Archas = 8,
                Ninja = 16,
                Monk = 32,
                Pirate = 64;
        }

        public bool AutoJoin = true;
        public string Buletin = "Nothing";
        public int NotAllowFlag;
        public byte Level = 0;
        public byte Reborn = 0;
        public byte Grade = 0;
        public uint Donations;

        public bool ContainFlag(int val)
        {
            return (NotAllowFlag & val) == val;
        }
        public void AddFlag(int val)
        {
            if (!ContainFlag(val))
                NotAllowFlag |= val;
        }
        public void Remove(int val)
        {
            if (ContainFlag(val))
                NotAllowFlag &= ~val;
        }
        public void SetFlag(int m_flag, Mode mod)
        {
            switch (mod)
            {
                case Mode.Requirements:
                    {
                        if (m_flag == 0) NotAllowFlag = Flags.NoneBlock;
                        if (m_flag >= 127)
                            AddFlag(Flags.Trojan | Flags.Warrior | Flags.Taoist | Flags.Archas | Flags.Ninja | Flags.Monk | Flags.Pirate);

                        int n_flag = 127 - m_flag;
                        AddFlag(n_flag);
                        break;
                    }
                case Mode.Recruit:
                    {
                        if (m_flag == 0) NotAllowFlag = Flags.NoneBlock;
                        AddFlag(m_flag);
                        break;
                    }
            }
        }
        public bool Compare(Game.Entity player, Mode mod)
        {
            if (player.Level < Level)
                return false;
            if (player.Reborn < Reborn && Reborn != 0)
                return false;
            if ((player.Class >= 40 && player.Class >= 45) && ContainFlag(Flags.Archas))
                return false;
            if ((player.Class >= 130 && player.Class >= 145) && ContainFlag(Flags.Taoist))
                return false;
            if ((player.Class >= 20 && player.Class >= 25) && ContainFlag(Flags.Warrior))
                return false;
            if ((player.Class >= 10 && player.Class >= 15) && ContainFlag(Flags.Trojan))
                return false;
            if ((player.Class >= 70 && player.Class >= 75) && ContainFlag(Flags.Pirate))
                return false;
            if ((player.Class >= 60 && player.Class >= 65) && ContainFlag(Flags.Monk))
                return false;
            if ((player.Class >= 50 && player.Class >= 55) && ContainFlag(Flags.Ninja))
                return false;
            if (mod == Mode.Recruit)
            {
                if (Grade == 0) return true;
                if (player.Mesh != Grade)
                    return false;
            }

            return true;
        }
        public override string ToString()
        {
            StringBuilder build = new StringBuilder();
            build.Append(NotAllowFlag + "^" + Level + "^" + Reborn + "^" + Grade + "^" + Donations + "^"
                + (byte)(AutoJoin ? 1 : 0) + "^" + Buletin + "^0" + "^0");
            return build.ToString();
        }
        public void Load(string line)
        {
            string[] data = line.Split('^');
            NotAllowFlag = int.Parse(data[0]);
            Level = byte.Parse(data[1]);
            Reborn = byte.Parse(data[2]);
            Grade = byte.Parse(data[3]);
            Donations = uint.Parse(data[4]);
            AutoJoin = byte.Parse(data[5]) == 1;
            Buletin = data[6];
        }

    }
}
