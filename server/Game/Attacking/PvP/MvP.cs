
using System;
using CO2_CORE_DLL.IO;
using COServer.Game;
using COServer;
using COServer.Network.GamePackets;
using COServer.Database;
using COServer.Game.Attacking;

namespace COServer
{
    public partial class MyMath
    {
        public static Int32 GetDamageMonster2Entity(Entity Monster, Entity Target, uint AtkType)
        {
            Double Damage = 0;

            Double Reborn = 1.00;
            if (Target.Reborn == 1)
                Reborn -= 0.30; //30%
            else if (Target.Reborn >= 2)
                Reborn -= 0.50; //50%

            switch (AtkType)
            {
                case Attack.Melee:
                    {
                        Damage = MyMath.Generate((int)Monster.MinAttack, (int)Monster.MaxAttack);
                        Damage = AdjustDamageMonster2Entity(Damage, Monster, Target);
                        
                        Damage -= Target.Defence;
                        if (Target.EntityFlag == EntityFlag.Player)
                            Damage -= Target.getTower(false);
                        break;
                    }
                case Attack.Magic:
                    {
                        Damage = Monster.MagicAttack;
                        Damage = AdjustDamageMonster2Entity(Damage, Monster, Target);

                        Damage *= ((Double)(100 - Target.MagicDefence) / 100);
                        Damage -= Target.Block;
                        Damage *= 0.75;
                        if (Target.EntityFlag == EntityFlag.Player)
                            Damage -= Target.getTower(true);
                        break;
                    }
            }

            Damage *= Reborn;
            Damage *= Target.ItemBless;
            double torist = (double)(Target.Gems[GemTypes.Tortoise] / 100d);
            torist = Math.Min(0.5, (double)torist);
            torist = (double)(1 - torist);
            if (Target.Class / 10 == 13 || Target.Class / 10 == 14) 
            Damage *= torist;

            if (Damage < 1)
                Damage = 1;

            Damage = AdjustMinDamageMonster2Entity(Damage, Monster, Target);

            if (Damage < 1)
                Damage = 1;

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

        public static Int32 GetDamageMonster2Entity(Entity Monster, Entity Target, ushort MagicType, Byte MagicLevel)
        {
            var Info = SpellTable.GetSpell(MagicType, MagicLevel);

            Double Damage = 0;

            Double Reborn = 1.00;
            if (Target.Reborn == 1)
                Reborn -= 0.30; //30%
            else if (Target.Reborn >= 2)
                Reborn -= 0.50; //50%

            if (Info.ID == 1115 || (Info.WeaponSubtype.Count >= 0 && !Info.WeaponSubtype.Contains(500)))
            {
                Damage = MyMath.Generate((int)Monster.MinAttack, (int)Monster.MaxAttack);
                if (Info.Power > 30000)
                    Damage *= (Double)(Info.Power - 30000) / 100;
                else
                    Damage += Info.Power;

                Damage = AdjustDamageMonster2Entity(Damage, Monster, Target);

                Damage -= Target.Defence;
            }
            else if (Info.WeaponSubtype.Contains(500))
            {
                Damage = MyMath.Generate((int)Monster.MinAttack, (int)Monster.MaxAttack);
                if (Info.Power > 30000)
                    Damage *= (Double)(Info.Power - 30000) / 100;
                else
                    Damage += Info.Power;
                Damage = AdjustDamageMonster2Entity(Damage, Monster, Target);
            }
            else
            {
                Damage = Monster.MagicAttack;
                if (Info.Power > 30000)
                    Damage *= (Double)(Info.Power - 30000) / 100;
                else
                    Damage += Info.Power;
                Damage = AdjustDamageMonster2Entity(Damage, Monster, Target);

                Damage *= ((Double)(100 - Target.MagicDefence) / 100);
                Damage -= Target.Block;
                Damage *= 0.75;
            }

            Damage *= Reborn;
            double bless = Target.ItemBless;
            Damage *= bless;
            double torist = (double)(Target.Gems[GemTypes.Tortoise] / 100d);
            torist = Math.Min(0.5, (double)torist);
            torist = (double)(1 - torist);
            if (Target.Class / 10 == 13 || Target.Class / 10 == 14) 
            Damage *= torist;


            Damage += Monster.MagicDamageIncrease;
            Damage -= Target.MagicDamageDecrease;

            if (Damage < 1)
                Damage = 1;

            Damage = AdjustMinDamageMonster2Entity(Damage, Monster, Target);



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
                    // World.BroadcastRoomMsg(Target, Network.MsgName.Create(Target.UniqId, "LuckyGuy", Network.MsgName.Action.RoleEffect), true);
                }
            }
            return (Int32)Math.Round(Damage, 0);
        }

        private static Int32 AdjustMinDamageMonster2Entity(Double Damage, Entity Monster, Entity Target)
        {
            Int32 MinDmg = 7;
            if (Damage >= MinDmg || Target.Level <= 15)
                return (Int32)Damage;

            MinDmg += (Int32)(Monster.Level / 10);

            if (Target.EntityFlag == EntityFlag.Player)
            {
                var Item = Target.Owner.Equipment.TryGetItem(3);
                if (Item != null)
                    MinDmg -= ((int)Item.ID % 10);


                if (Item != null && (Item.ID % 10) == 0)
                    MinDmg = 1;
            }
            MinDmg = Math.Max(1, MinDmg);

            return Math.Max(MinDmg, (Int32)Damage);
        }

        private static Int32 AdjustDamageMonster2Entity(Double Damage, Entity Monster, Entity Target)
        {
            Byte Level = 120;
            if (Monster.Level < 120)
                Level = (Byte)Monster.Level;

            if (Monster.IsRed(Target))
                Damage *= 1.5;
            else if (Monster.IsBlack(Target))
            {
                Int32 DeltaLvl = Target.Level - Level;
                if (DeltaLvl >= -10 && DeltaLvl <= -5)
                    Damage *= 2.0;
                else if (DeltaLvl >= -20 && DeltaLvl < -10)
                    Damage *= 3.5;
                else if (DeltaLvl < -20)
                    Damage *= 5.0;
            }

            return Math.Max(0, (Int32)Damage);
        }
    }
}
