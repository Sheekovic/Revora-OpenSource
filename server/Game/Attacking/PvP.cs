
using System;
using CO2_CORE_DLL.IO;
using Revora.Game;
using Revora.Network.GamePackets;
using Revora;
using Revora.Database;
using Revora.Game.Attacking;
using System.Collections.Generic;

namespace Revora
{
    public partial class MyMath
    {
        public static Int32 GetDamageEntity2Entity(Entity Attacker, Entity Attacked, uint AtkType, ref Attack Packet)
        {
            Double Damage = 0;

            Double Reborn = 1.00;
            if (Attacked.Reborn == 1)
                Reborn -= 0.30; //30%
            else if (Attacked.Reborn >= 2)
                Reborn -= 0.50; //50%

            Double Dodge = 1.00;
            Dodge -= (Double)Attacked.Dodge / 1000;
            Dodge += (Double)Attacked.Weight / 100;

            switch (AtkType)
            {
                case Attack.Melee:
                    {                        
                        if (!Attacker.Transformed)
                            Damage += MyMath.Generate(Attacker.MinAttack, Attacker.MaxAttack);
                        else
                            Damage = MyMath.Generate(Attacker.TransformationMinAttack, Attacker.TransformationMaxAttack);                        
                        Revora.Game.Attacking.Calculate.Refinary(Attacker, Attacked, ref Damage, ref Packet, false);                        
                        Damage += Attacker.getFan(false);
                        Damage += Attacker.getFanWings(false);
                        var bonus = Attacker.Gems[GemTypes.Dragon];
                        if (bonus > 0)
                            Damage += Revora.Game.Attacking.PvP.MathHelper.MulDiv((int)Damage, bonus, 100);
                        Damage = Attacker.AdjustAttack((int)Damage);                        
                        if (Attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Superman))
                            Damage *= 0.2; //PvP Reduction!
                 
                        Damage = Calculate.AdjustDefence(Attacked, (int)Damage);

                        if (Damage >= Attacked.getTower(false))
                            Damage -= Attacked.getTower(false);
                        if (Damage >= Attacked.getTowerWings(false))
                            Damage -= Attacked.getTowerWings(false);
                        else
                            Damage = 1;                                       
                        if(Attacker.Assassin() || Attacker.Class >= 40 && Attacker.Class <= 45)
                            Damage -= Damage * 70 / 100;
                        Damage -= Attacked.AdjustBowDefense(Attacked.Defence);
                        break;
                    }
                case Attack.Magic:
                    {
                        Damage = Attacker.MagicAttack;                       
                        Revora.Game.Attacking.Calculate.Refinary(Attacker, Attacked, ref Damage, ref Packet, true);                        
                        Damage -= (int)Attacked.MagicDefence;                        
                        Damage -= Attacked.Block;                        
                        Damage += Attacker.MagicDamageIncrease;                        
                        Damage -= Attacked.MagicDamageDecrease;                                              
                        var effect = Attacker.Gems[GemTypes.Phoenix];
                        Damage += Damage * effect / 100;
                        #region Taoist damage adjustment
                        Damage *= 0.3;
                        #endregion                        
                        break;
                    }
                case Attack.Ranged:
                    {
                        Damage = MyMath.Generate(Attacker.MinAttack, Attacker.MaxAttack);
                        Revora.Game.Attacking.Calculate.Refinary(Attacker, Attacked, ref Damage, ref Packet, false);
                        Damage += Attacker.getFan(false);
                        var bonus = Attacker.Gems[GemTypes.Dragon];
                        if (bonus > 0)
                            Damage += Revora.Game.Attacking.PvP.MathHelper.MulDiv((int)Damage, bonus, 100);
                        Damage = Attacker.AdjustAttack((int)Damage);
                        if (Attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Superman))
                            Damage *= 0.2; //PvP Reduction!             
                        Damage -= Attacked.getTower(false);
                        Damage *= Dodge;
                        Damage *= 0.10;                        
                        break;
                    }
            }
            Damage *= Reborn;
            Damage *= Attacked.ItemBless;
            #region Added new attack system
            #region TortoisenGem
            double torist = (double)(Attacked.Gems[GemTypes.Tortoise] / 100d);
            if (torist >= 0.30)
                Damage = Damage - (Damage *= 0.30);
            else
                Damage = Damage - (Damage *= torist);
            #endregion
            #region BattlePower adjustment 
            if (Attacked.BattlePower > Attacker.BattlePower)
            {
                Damage -= Damage * 35 / 100;
            }
            #endregion            
            #region Bless luck: double damage or zero damage
            if (Attacker.EntityFlag == EntityFlag.Player && Attacker.Owner.BlessTime > 0)
            {
                if (Kernel.Rate(0.2))
                {
                    Damage *= 2;
                    _String str = new _String(true);
                    str.UID = Attacker.UID;
                    str.TextsCount = 1;
                    str.Type = _String.Effect;
                    str.Texts.Add("LuckyGuy");
                    Attacker.Owner.SendScreen(str, true);
                }
                else if (Kernel.Rate(0.2))
                {
                    Damage = 1;
                    _String str = new _String(true);
                    str.UID = Attacked.UID;
                    str.TextsCount = 1;
                    str.Type = _String.Effect;
                    str.Texts.Add("LuckyGuy");
                    Attacked.Owner.SendScreen(str, true);
                }
            }
            #endregion                   
            #endregion
            return (Int32)Math.Round(Damage, 0);
        }

        public static Int32 GetDamageEntity2Entity(Entity Attacker, Entity Target, ushort MagicType, Byte MagicLevel, ref Attack Packet)
        {
            var Info = SpellTable.GetSpell(MagicType, MagicLevel);


            Double Damage = 0;

            Double Reborn = 1.00;
            if (Target.Reborn == 1)
                Reborn -= 0.30; //30%
            else if (Target.Reborn >= 2)
                Reborn -= 0.50; //50%

            Double Dodge = 1.00;
            Dodge -= (Double)Target.Dodge / 1000;
            Dodge += (Double)Target.Weight / 100;

            List<ushort> spells = new List<ushort>()
            {
                11190,
                10315
            };
            if ((Info.Target == SpellTarget.AroundCoordonates || Info.ID == 1115 || spells.Contains(Info.ID) || (Info.WeaponSubtype.Count > 0)) && !Info.WeaponSubtype.Contains(500) && !Info.WeaponSubtype.Contains(613))
            {
                Damage = MyMath.Generate(Attacker.MinAttack, Attacker.MaxAttack);
                Revora.Game.Attacking.Calculate.Refinary(Attacker, Target,ref Damage, ref Packet, false);
                Damage += Attacker.getFan(false);                
                double bonus = Attacker.Gems[GemTypes.Dragon];
                if (bonus > 0)
                    Damage += Revora.Game.Attacking.PvP.MathHelper.MulDiv((int)Damage, (int)bonus, 100);
                Damage = Attacker.AdjustAttack((int)Damage);

                if (Info.Power > 30000)
                    Damage *= (Double)(Info.Power - 30000) / 100;
                else
                    Damage += Info.Power;





                if (Attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Superman))
                    Damage *= 0.2; //PvP Reduction!

                Damage -= Target.AdjustDefense(Target.Defence);
                Damage -= Target.getTower(false);

                if (Attacker.Gems[GemTypes.Dragon] >= 210)
                    Damage += Damage * 50 / 100;


            }
            else if (Info.WeaponSubtype.Contains(500) || Info.WeaponSubtype.Contains(613))
            {
                Damage = MyMath.Generate(Attacker.MinAttack, Attacker.MaxAttack);
                Revora.Game.Attacking.Calculate.Refinary(Attacker, Target, ref Damage, ref Packet, false);
             
                Damage += Attacker.getFan(false);
                if (Info.Power > 30000)
                    Damage *= (Double)(Info.Power - 30000) / 100;
                else
                    Damage += Info.Power;

                if (Attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Superman))
                    Damage *= 0.2; //PvP Reduction!



                Damage *= Dodge;
                Damage *= 0.10;


                Damage -= Target.getTower(false);

                Damage = Attacker.AdjustAttack((int)Damage);
                var bonus = Attacker.Gems[GemTypes.Dragon];
                if (bonus > 0)
                    Damage += Revora.Game.Attacking.PvP.MathHelper.MulDiv((int)Damage, bonus, 100);

                if (Attacker.Gems[GemTypes.Dragon] >= 210)
                    Damage += Damage * 50 / 100;
                //Damage *= 0.12;
            }
            else
            {
                Damage = Attacker.MagicAttack;
                Revora.Game.Attacking.Calculate.Refinary(Attacker, Target, ref Damage, ref Packet, true);
             
                Damage += Attacker.getFan(true);

                var effect = Attacker.Gems[GemTypes.Phoenix];
                Damage += Damage * effect / 100;

                if (Info.Power > 30000)
                    Damage *= (Double)(Info.Power - 30000) / 100;
                else
                    Damage += Info.Power;
                Damage -= Target.MagicDefence;
                Damage -= Target.Block;
                Damage -= Target.getTower(true);

                if (effect >= 180)
                    Damage += Damage / 20;
            }


            Damage *= Reborn;
            Damage *= Target.ItemBless;
            double torist = (double)(Target.Gems[GemTypes.Tortoise] / 100d);
            torist = Math.Min(0.4, (double)torist);
            torist = (double)(1 - torist);
            if (Attacker.Class / 10 == 13 || Attacker.Class / 10 == 14) 
            Damage *= torist;

            Double BattlePower = Attacker.BattlePower - Target.BattlePower;
            BattlePower = Math.Pow(6.0, BattlePower / 12.0);
            BattlePower = Math.Min(BattlePower, 100.0);
            Damage *= BattlePower;

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
            if (Target.EntityFlag == EntityFlag.Player)
            {
                if (Target.Owner.BlessTime > 0 && MyMath.Success(10))
                {
                    Damage = 1;
                    _String str = new _String(true);
                    str.UID = Target.UID;
                    str.TextsCount = 1;
                    str.Type = _String.Effect;
                    str.Texts.Add("LuckyGuy");
                    Target.Owner.SendScreen(str, true);
                }
            }
            return (Int32)Math.Round(Damage, 0);
        }
    }
}
