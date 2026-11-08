

using System;
using CO2_CORE_DLL.IO;
using Revora.Game;
using Revora.Database;
using Revora.Network.GamePackets;
using Revora;
using Revora.Game.Attacking;
using System.Collections.Generic;

namespace Revora
{
    public partial class MyMath
    {
        public static Int32 GetDamageEntity2Monster(Entity Attacker, Entity Monster, uint AtkType, ref Attack Packet)
        {
            Double Damage = 0;

            switch (AtkType)
            {
                case Attack.Melee:
                    {                        
                        if (!Attacker.Transformed)
                        {
                            Damage = MyMath.Generate(Attacker.MinAttack, Attacker.MaxAttack);                            
                        }
                        else
                            Damage = MyMath.Generate(Attacker.TransformationMinAttack, Attacker.TransformationMaxAttack);

                        Revora.Game.Attacking.Calculate.Refinary(Attacker, Monster, ref Damage, ref Packet, false);                        
                        Damage += Attacker.getFan(false);                        
                        Damage = Attacker.AdjustAttack((int)Damage);                        

                        var bonus = Attacker.Gems[GemTypes.Dragon];
                        if (bonus > 0)
                        {
                            Damage += Revora.Game.Attacking.PvP.MathHelper.MulDiv((int)Damage, bonus, 100);                     
                        }

                        Damage = AdjustDamageEntity2Monster(Damage, Attacker, Monster);                        
                        Damage -= Monster.Defence;                              
                        break;
                    }
                case Attack.Magic:
                    {                        
                        Damage = Attacker.MagicAttack;
                        Damage = AdjustDamageEntity2Monster(Damage, Attacker, Monster);
                        Revora.Game.Attacking.Calculate.Refinary(Attacker, Monster, ref Damage, ref Packet, true);                        
                        Damage *= ((Double)(100 - Monster.MagicDefence) / 100);
                        Damage -= Monster.Block;
                        Damage *= 0.75;

                        Damage += Attacker.MagicDamageIncrease;
                        Damage -= Monster.MagicDamageDecrease;

                        var effect = Attacker.Gems[GemTypes.Phoenix];
                        Damage += Damage * effect / 100;
                        if (effect >= 180)
                            Damage += Damage / 20;
                        
                        break;
                    }
                case Attack.Ranged:
                    {                        
                        Damage = MyMath.Generate((int)Attacker.MinAttack, (int)Attacker.MaxAttack);
                        Revora.Game.Attacking.Calculate.Refinary(Attacker, Monster, ref Damage, ref Packet, false);
             
                        Damage += Attacker.getFan(false);
                        Damage = AdjustDamageEntity2Monster(Damage, Attacker, Monster);

                        var bonus = Attacker.Gems[GemTypes.Dragon];
                        if (bonus > 0)
                            Damage += Revora.Game.Attacking.PvP.MathHelper.MulDiv((int)Damage, bonus, 100);

                        break;
                    }
            }

            if (AtkType != Attack.Magic)
            {
                if (Attacker.Gems[GemTypes.Dragon] >= 210)
                    Damage += Damage * 50 / 100;
            }

            if (Attacker.BattlePower < Monster.Level)
                Damage *= 0.01;


            if (Damage < 1)
                Damage = 1;

            Damage = AdjustMinDamageEntity2Monster(Damage, Attacker, Monster);

            if (Monster.MonsterInfo.Boss || Monster.Boss == 1)
            {
                if (Attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Superman))
                    Damage *= 0.2;
                if (Attacker.ContainsFlag((ulong)FlagsUpdate.Flags.FatalStrike))
                    Damage = Damage / 5;
            }

                if (Attacker.EntityFlag == EntityFlag.Player)
            {
                if (Attacker.Owner.BlessTime > 0 && Kernel.Rate(10, 100))
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
            return (Int32)Math.Round(Damage, 0);
        }

        public static Int32 GetDamageEntity2Monster(Entity Attacker, Entity Monster, ushort MagicType, Byte MagicLevel, ref Attack Packet)
        {
            var Info = SpellTable.GetSpell(MagicType, MagicLevel);

            Double Damage = 0;
            if (Monster.Name == "GoldenOctopus") return 1;
            List<ushort> spells = new List<ushort>()
            {
                11190,
                10315
            };
            if (Info.ID == 1115 || spells.Contains(Info.ID) || (Info.WeaponSubtype.Count > 0 && !Info.WeaponSubtype.Contains(500) && !Info.WeaponSubtype.Contains(613)))
            {
                Damage = MyMath.Generate((int)Attacker.MinAttack, (int)Attacker.MaxAttack);
                Revora.Game.Attacking.Calculate.Refinary(Attacker, Monster, ref Damage, ref Packet, false);
             
                if (Info.Power > 30000)
                    Damage *= (Double)(Info.Power - 30000) / 100;
                else
                    Damage += Info.Power;
                Damage += Attacker.getFan(false);

                var bonus = Attacker.Gems[GemTypes.Dragon];
                if (bonus > 0)
                    Damage += Revora.Game.Attacking.PvP.MathHelper.MulDiv((int)Damage, bonus, 100);

                Damage = AdjustDamageEntity2Monster(Damage, Attacker, Monster);

                Damage -= Monster.Defence;

                Damage = Attacker.AdjustAttack((int)Damage);


                if (Attacker.Gems[GemTypes.Dragon] >= 210)
                    Damage += Damage * 50 / 100;
            }
            else if (Info.WeaponSubtype.Contains(500) || Info.WeaponSubtype.Contains(613))
            {
                Damage = MyMath.Generate((int)Attacker.MinAttack, (int)Attacker.MaxAttack);
                Damage += Attacker.getFan(false);

                if (Info.Power > 30000)
                    Damage *= (Double)(Info.Power - 30000) / 100;
                else
                    Damage += Info.Power;

                Damage = AdjustDamageEntity2Monster(Damage, Attacker, Monster);

                Damage = Attacker.AdjustAttack((int)Damage);

                var bonus = Attacker.Gems[GemTypes.Dragon];
                if (bonus > 0)
                    Damage += Revora.Game.Attacking.PvP.MathHelper.MulDiv((int)Damage, bonus, 100);



                if (Attacker.Gems[GemTypes.Dragon] >= 210)
                    Damage += Damage * 50 / 100;
            }
            else
            {
                Damage = Attacker.MagicAttack;
                if (Info.Power > 30000)
                    Damage *= (Double)(Info.Power - 30000) / 100;
                else
                    Damage += Info.Power;
                Damage += Attacker.getFan(true);
                var effect = Attacker.Gems[GemTypes.Phoenix];
                Damage += Damage * effect / 100;
                Damage = AdjustDamageEntity2Monster(Damage, Attacker, Monster);

                Damage *= ((Double)(100 - Monster.MagicDefence) / 100);
                Damage -= Monster.Block;
    


                if (effect >= 180)
                    Damage += Damage / 20;
            }

            if (Attacker.BattlePower < Monster.Level)
                Damage *= 0.01;

            if (Damage < 1)
                Damage = 1;

            Damage = AdjustMinDamageEntity2Monster(Damage, Attacker, Monster);



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
            return (Int32)Math.Round(Damage, 0);
        }

        private static Int32 AdjustMinDamageEntity2Monster(Double Damage, Entity Attacker, Entity Monster)
        {
            Int32 MinDmg = 1;
            MinDmg += (Int32)(Attacker.Level / 10);

            var Item = Attacker.Owner.Equipment.TryGetItem(4);
            if (Item != null)
                MinDmg += (int)Item.ID % 10;
            
            return Math.Max(MinDmg, (Int32)Damage);
        }

        private static Int32 AdjustDamageEntity2Monster(Double Damage, Entity Attacker, Entity Monster)
        {
            if (!Monster.IsGreen(Attacker))
                return Math.Max(0, (Int32)Damage);

            Int32 DeltaLvl = Attacker.Level - Monster.Level;            
            if (DeltaLvl >= 3 && DeltaLvl <= 5)
                Damage *= 1.5;
            else if (DeltaLvl > 5 && DeltaLvl <= 10)
                Damage *= 2;
            else if (DeltaLvl > 10 && DeltaLvl <= 20)
                Damage *= 2.5;
            else if (DeltaLvl > 20)
                Damage *= 3;
            else
                Damage *= 1;

            return Math.Max(0, (Int32)Damage);
        }
    }
}
