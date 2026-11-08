
using System;
using CO2_CORE_DLL.IO;
using COServer.Network.GamePackets;
using COServer.Game;
using COServer.Database;
using COServer.Game.Attacking;
using System.Collections.Generic;

namespace COServer
{
    public partial class MyMath
    {
        public static Int32 GetDamageEntity2Environment(Entity Attacker, SobNpcSpawn sobnpc, uint AtkType)
        {
            Double Damage = 0;

            switch (AtkType)
            {
                case Attack.Melee:
                    {
                        Damage = MyMath.Generate(Attacker.MinAttack, Attacker.MaxAttack);
                        Damage += Attacker.getFan(false);
                   
                        break;
                    }
                case Attack.Magic:
                    {
                        Damage = Attacker.MagicAttack;
                        Damage += Attacker.getFan(true);
               
                        Damage *= 0.75;
                        break;
                    }
                case Attack.Ranged:
                    {
                        Damage = MyMath.Generate(Attacker.MinAttack, Attacker.MaxAttack);
                        Damage += Attacker.getFan(false);
                        break;
                    }
            }

            Damage = Attacker.AdjustAttack((int)Damage);
            if (Damage < 1)
                Damage = 1;
            if (Attacker.EntityFlag == EntityFlag.Player)
            {
                if (Attacker.Owner.BlessTime > 0 && MyMath.Success(10))
                {
                    Damage *= 2;
                    _String str = new _String(true);
                    str.UID = Attacker.UID;
                    str.TextsCount = 1;
                    str.Type = _String.Effect;
                    str.Texts.Add("LuckyGuy");
                    Attacker.Owner.SendScreen(str, true);

                }
            }
            Damage *= 10;
            return (Int32)Math.Round(Damage, 0);
        }

        public static Int32 GetDamageEntity2Environment(Entity Attacker, SobNpcSpawn sobnpc, ushort MagicType, Byte MagicLevel)
        {
            var Info = SpellTable.GetSpell(MagicType, MagicLevel);

            Double Damage = 0;

            List<ushort> spells = new List<ushort>()
            {
                11190,
                10315
            };
            if (Info.ID == 1115 || spells.Contains(Info.ID) || (Info.WeaponSubtype.Count > 0 && !Info.WeaponSubtype.Contains(500) && !Info.WeaponSubtype.Contains(613)))
            {
                Damage = MyMath.Generate(Attacker.MinAttack, Attacker.MaxAttack);
                Damage += Attacker.getFan(false);
                if (Info.Power > 30000)
                    Damage *= (Double)(Info.Power - 30000) / 100;
                else
                    Damage += Info.Power;

            }
            else if (Info.WeaponSubtype.Contains(500) || Info.WeaponSubtype.Contains(613))
            {
                Damage = MyMath.Generate(Attacker.MinAttack, Attacker.MaxAttack);
                Damage += Attacker.getFan(false);
                if (Info.Power > 30000)
                    Damage *= (Double)(Info.Power - 30000) / 100;
                else
                    Damage += Info.Power;
            }
            else
            {
                Damage = Attacker.MagicAttack;
                Damage += Attacker.getFan(true);
                if (Info.Power > 30000)
                    Damage *= (Double)(Info.Power - 30000) / 100;
                else
                    Damage += Info.Power;
            }

            if (Damage < 1)
                Damage = 1;
            if (Attacker.EntityFlag == EntityFlag.Player)
            {
                if (Attacker.Owner.BlessTime > 0 && MyMath.Success(10))
                {
                    Damage *= 2;
                    _String str = new _String(true);
                    str.UID = Attacker.UID;
                    str.TextsCount = 1;
                    str.Type = _String.Effect;
                    str.Texts.Add("LuckyGuy");
                    Attacker.Owner.SendScreen(str, true);

                }
            }
            Damage *= 10;
            return (Int32)Math.Round(Damage, 0);
        }
    }
}
