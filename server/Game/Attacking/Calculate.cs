using System;
using Revora.Game.ConquerStructures;
using Revora.Network.GamePackets;
using System.Runtime.InteropServices;

namespace Revora.Game.Attacking
{
    public unsafe class Calculate
    {
        public static uint Rango(Entity attacker, Entity attacked, Database.SpellInformation spell, ref Attack Packet)
        {

            if (Packet != null)
            {
                Packet.Effect = Attack.AttackEffects.None;
            }

            #region HideGui
            if (attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Player)
            {
                Data Data = new Data(true);
                Data.UID = attacker.UID;
                Data.UID = attacked.UID;
                Data.ID = Data.HideGui;
                attacker.Owner.Send(Data);
                attacked.Owner.Send(Data);
            }
            #endregion
            if (attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Player)
            {
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex)) return 1;
                if (attacker.Transformed) return 0;
                if (attacker == null) return 0;
                if (attacked == null) return 0;
                #region Effect By Maxs

                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    if (Kernel.Rate(50))
                    {
                        GemEffect.Effect(attacker);
                    }
                }

                #endregion
                Durability(attacker, attacked, null);
                if (attacker.Transformed) return 0;
                Double attack = attacker.MinAttack + Kernel.Random.Next((int)attacker.MaxAttack - (int)attacker.MinAttack);
                #region Breakthrough
                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    //if (attacker.BattlePower < attacked.BattlePower)
                    //{
                    //    if (Kernel.Rate(((float)attacker.Breaktrough * 1.01) - (float)(attacked.Counteraction)))
                    //    {
                    //        attack = (Int32)attacker.MinAttack - 18000;
                    //        Packet.Effect |= Attack.AttackEffects.Penetration;
                    //    }
                    //}
                }
                #endregion
                #region Spell Bounes

                if (spell != null)
                {
                    if (spell.PowerPercent != 0)
                    {
                        attack = (long)(attack * spell.PowerPercent);
                    }
                }

                #endregion

              //  AutoRespone(attacker, attacked,ref attack);
                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    var bonus = attacker.Gems[1];
                    if (bonus > 0)
                    {
                        attack += MathHelper.MulDiv((int)attack, bonus, 100);
                    }
                }
                if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))
                    if (!attacker.Transformed)
                        attack = (long)(attack * 1.15);

                Double defense = Revora.Game.Features.WarriorStance.Defence(attacked,attacked.Defence,Time32.Now);
                Double damage = attack - defense;
                var targetLevel = attacked.Level;
                if (targetLevel < 22) damage = MathHelper.MulDiv((int)damage, 35, 100);
                else if (targetLevel < 50) damage = MathHelper.MulDiv((int)damage, 25, 100);
                else if (targetLevel < 90) damage = MathHelper.MulDiv((int)damage, 20, 100);
                else if (targetLevel < 110) damage = MathHelper.MulDiv((int)damage, 15, 100);
                else damage = MathHelper.MulDiv((int)damage, 12, 100);

                damage = CombatMath.Tortoise(attacked, (int)damage);
                damage = CombatMath.BattlePower(attacker, attacked, (int)damage, Packet, false);
                damage = CombatMath.Critical(attacker, attacked, damage, Packet, false);
                #region AzureShield
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
                {

                    if (damage > attacked.AzureShieldDefence)
                    {
                        damage -= (int)attacked.AzureShieldDefence;
                        CreateAzureDmg(attacked.AzureShieldDefence, attacker, attacked);
                        //attacked.RemoveFlag2((ulong)FlagsUpdate.Flags.AzureShield);
                    }
                    else
                    {
                        CreateAzureDmg((uint)damage, attacker, attacked);
                        attacked.AzureShieldDefence -= (ushort)damage;
                        attacked.AzureShieldPacket();
                        damage = 1;
                    }
                }
                #endregion
                #region IronShield
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.IronShield))
                {

                    if (damage > attacked.IronShieldDefence)
                    {
                        damage -= (int)attacked.IronShieldDefence;
                        CreateAzureDMG(attacked.IronShieldDefence, attacker, attacked);
                        attacked.RemoveFlag((ulong)FlagsUpdate.Flags.IronShield);
                    }
                    else
                    {
                        CreateAzureDMG((uint)damage, attacker, attacked);
                        attacked.IronShieldDefence -= (uint)damage;
                        attacked.IronShieldPacket();
                        damage = 1;
                    }
                }
                #endregion
                #region MagicShield
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.MagicShield))
                {
                    damage = (int)((double)damage *60 / 67);// Shield reduces damage
                }
                #endregion
                damage = AdjustRebirthDamage(attacked.Owner, attacker.Owner, (int)damage);
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    var reduce = Math.Max(0, Math.Min(100, (int)attacked.ItemBless));
                    if (reduce > 0)
                    {
                        damage -= damage * (int)reduce / 100;
                    }
                }
                if (attacked.EntityFlag == EntityFlag.Monster)
                    damage = damage * 3;
                //if (attacker.OnIntensify && Time32.Now >= attacker.IntensifyStamp.AddSeconds(4))
                //{
                //    damage = (uint)Math.Floor(damage * 1.1);
                //    attacker.OnIntensify = false;
                //    attacker.RemoveFlag((ulong)FlagsUpdate.Flags.Intensify);
                //}
                #region Block
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    if (CombatMath.Roll(attacked.Block / 100.0))
                    {
                        Packet.Effect |= Attack.AttackEffects.Block;
                        damage = (Int32)Math.Floor((float)damage / 2);
                    }
                }
                #endregion
                #region Reduction!
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
                {
                    if (damage > attacked.AzureShieldDefence)
                    {
                        damage -= attacked.AzureShieldDefence;
                        CreateAzureDmg(attacked.AzureShieldDefence, attacker, attacked);
                        attacked.RemoveFlag((ulong)FlagsUpdate.Flags.AzureShield);
                        attacked.AzuredShield = true;
                    }
                    else
                    {
                        CreateAzureDmg((uint)damage, attacker, attacked);
                        attacked.AzureShieldDefence -= (ushort)damage;
                        attacked.AzureShieldPacket();
                        damage = 1;
                    }
                }
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.IronShield))
                {

                    if (damage > attacked.IronShieldDefence)
                    {
                        damage -= (int)attacked.IronShieldDefence;
                        CreateAzureDMG(attacked.IronShieldDefence, attacker, attacked);
                        attacked.RemoveFlag((ulong)FlagsUpdate.Flags.IronShield);
                    }
                    else
                    {
                        CreateAzureDMG((uint)damage, attacker, attacked);
                        attacked.IronShieldDefence -= (uint)damage;
                        attacked.IronShieldPacket();
                        damage = 1;
                    }
                }
                #endregion
                if (damage < 0)
                    damage = 1;
                return Math.Max(1, (uint)damage);
            }
            else if (attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Monster)
            {
                int Damage = 0;
                Damage = MyMath.GetDamageEntity2Monster(attacker, attacked, spell.ID, spell.Level, ref Packet);
                CheckDamage(attacker, attacked, ref Damage, ref Packet, false);
                return (uint)Damage;
            }
            else if (attacker.EntityFlag == EntityFlag.Monster && attacked.EntityFlag == EntityFlag.Player)
            {
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex)) return 1;
                if (attacker.Transformed) return 0;
                if (attacker == null) return 0;
                if (attacked == null) return 0;
                #region GemEffect
                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    if (Kernel.Rate(50))
                    {
                        GemEffect.Effect(attacker);
                    }
                }
                #endregion
                Durability(attacker, attacked, null);
                if (attacker.Transformed) return 0;
                Double attack = attacker.MinAttack + Kernel.Random.Next((int)attacker.MaxAttack - (int)attacker.MinAttack);


                #region Spell Bounes
                if (spell != null)
                {
                    if (spell.PowerPercent != 0)
                    {
                        attack = (long)(attack * spell.PowerPercent);
                    }
                }
                #endregion
                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    var bonus = attacker.Gems[1];
                    if (bonus > 0)
                    {
                        attack += MathHelper.MulDiv((int)attack, bonus, 100);
                    }
                }
                if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))
                    if (!attacker.Transformed)
                        attack = (long)(attack * 1.15);
                Double defense = Revora.Game.Features.WarriorStance.Defence(attacked,attacked.Defence,Time32.Now);
                Double damage = attack - defense;
                var targetLevel = attacked.Level;
                if (targetLevel < 22) damage = MathHelper.MulDiv((int)damage, 35, 100);
                else if (targetLevel < 50) damage = MathHelper.MulDiv((int)damage, 25, 100);
                else if (targetLevel < 90) damage = MathHelper.MulDiv((int)damage, 20, 100);
                else if (targetLevel < 110) damage = MathHelper.MulDiv((int)damage, 15, 100);
                else damage = MathHelper.MulDiv((int)damage, 12, 100);
                damage = CombatMath.Critical(attacker, attacked, damage, Packet, false);

                damage = AdjustRebirthDamage(attacked.Owner, attacker.Owner, (int)damage);
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    var reduce = Math.Max(0, Math.Min(100, (int)attacked.ItemBless));
                    if (reduce > 0)
                    {
                        damage -= damage * (int)reduce / 100;
                    }
                }
                if (attacked.EntityFlag == EntityFlag.Monster)
                    damage = damage * 3;

                #region Block
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    if (CombatMath.Roll(attacked.Block / 100.0))
                    {
                        Packet.Effect |= Attack.AttackEffects.Block;
                        damage = (Int32)Math.Floor((float)damage / 2);
                    }
                }
                #endregion
                #region Reduction!
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
                {
                    if (damage > attacked.AzureShieldDefence)
                    {
                        damage -= attacked.AzureShieldDefence;
                        CreateAzureDmg(attacked.AzureShieldDefence, attacker, attacked);
                        attacked.RemoveFlag((ulong)FlagsUpdate.Flags.AzureShield);
                        attacked.AzuredShield = true;
                    }
                    else
                    {
                        CreateAzureDmg((uint)damage, attacker, attacked);
                        attacked.AzureShieldDefence -= (ushort)damage;
                        attacked.AzureShieldPacket();
                        damage = 1;
                    }
                }
                #region IronShield
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.IronShield))
                {

                    if (damage > attacked.IronShieldDefence)
                    {
                        damage -= (int)attacked.IronShieldDefence;
                        CreateAzureDMG(attacked.IronShieldDefence, attacker, attacked);
                        attacked.RemoveFlag((ulong)FlagsUpdate.Flags.IronShield);
                    }
                    else
                    {
                        CreateAzureDMG((uint)damage, attacker, attacked);
                        attacked.IronShieldDefence -= (uint)damage;
                        attacked.IronShieldPacket();
                        damage = 1;
                    }
                }
                #endregion

                #endregion
                if (damage < 0)
                    damage = 1;
                return Math.Max((uint)1, (uint)damage);
            }
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex)) return 1;
            if (attacker.Transformed) return 0;
            if (attacker == null) return 0;
            if (attacked == null) return 0;
            #region Effect
            if (attacker.EntityFlag == EntityFlag.Player)
            {
                if (Kernel.Rate(50))
                {
                    GemEffect.Effect(attacker);
                }
            }
            #endregion
            Durability(attacker, attacked, null);
            if (attacker.Transformed) return 0;
            double attacke = attacker.MinAttack + Kernel.Random.Next((int)attacker.MaxAttack - (int)attacker.MinAttack);


            #region Spell Bounes
            if (spell != null)
            {
                if (spell.PowerPercent != 0)
                {
                    attacke = (long)(attacke * spell.PowerPercent);
                }
            }
            #endregion
            if (attacker.EntityFlag == EntityFlag.Player)
            {
                var bonus = attacker.Gems[1];
                if (bonus > 0)
                {
                    attacke += MathHelper.MulDiv((int)attacke, bonus, 100);
                }
            }
            if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))
                if (!attacker.Transformed)
                    attacke = (long)(attacke * 1.15);
            double defensee = Revora.Game.Features.WarriorStance.Defence(attacked,attacked.Defence,Time32.Now);
            double damagee = attacke - defensee;
            var targetLevell = attacked.Level;
            if (targetLevell < 22) damagee = MathHelper.MulDiv((int)damagee, 35, 100);
            else if (targetLevell < 50) damagee = MathHelper.MulDiv((int)damagee, 25, 100);
            else if (targetLevell < 90) damagee = MathHelper.MulDiv((int)damagee, 20, 100);
            else if (targetLevell < 110) damagee = MathHelper.MulDiv((int)damagee, 15, 100);
            else damagee = MathHelper.MulDiv((int)damagee, 12, 100);
            //#region CriticalStrike
            //if (attacker.EntityFlag == EntityFlag.Player)
            //{
            //    if (Kernel.Rate(((float)attacker.CriticalStrike * 1.1) - ((float)attacked.Immunity)))
            //    {
            //        Packet.Effect |= Attack.AttackEffects.CriticalStrike;
            //        damagee = (uint)Math.Floor((float)damagee * 1.15);
            //    }
            //}
            //#endregion
            //if (attacked.EntityFlag == EntityFlag.Player)
            //{
            //    var reduce = Math.Max(0, Math.Min(100, (int)attacked.Gems[7]));
            //    if (reduce > 0)
            //    {
            //        damagee -= (damagee / 2) * reduce / 10;
            //    }
            //}
            damagee = AdjustRebirthDamage(attacked.Owner, attacker.Owner, (int)damagee);
            if (attacked.EntityFlag == EntityFlag.Player)
            {
                var reduce = Math.Max(0, Math.Min(100, (int)attacked.ItemBless));
                if (reduce > 0)
                {
                    damagee -= damagee * reduce / 100;
                }
            }
            if (attacked.EntityFlag == EntityFlag.Monster)
                damagee = damagee * 3;
            //if (attacker.OnIntensify && Time32.Now >= attacker.IntensifyStamp.AddSeconds(4))
            //{
            //    damagee = (uint)Math.Floor(damagee * 1.1);
            //    attacker.OnIntensify = false;
            //    attacker.RemoveFlag((ulong)FlagsUpdate.Flags.Intensify);
            //}
            #region Block
            if (attacked.EntityFlag == EntityFlag.Player)
            {
                if (CombatMath.Roll(attacked.Block / 100.0))
                {
                    Packet.Effect |= Attack.AttackEffects.Block;
                    damagee = (int)Math.Floor((float)damagee / 2);
                }
            }
            #endregion
            #region Reduction!
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
            {
                if (damagee > attacked.AzureShieldDefence)
                {
                    damagee -= attacked.AzureShieldDefence;
                    CreateAzureDmg(attacked.AzureShieldDefence, attacker, attacked);
                    attacked.RemoveFlag((ulong)FlagsUpdate.Flags.AzureShield);
                    attacked.AzuredShield = true;
                }
                else
                {
                    CreateAzureDmg((uint)damagee, attacker, attacked);
                    attacked.AzureShieldDefence -= (ushort)damagee;
                    attacked.AzureShieldPacket();
                    damagee = 1;
                }
            }
            #region IronShield
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.IronShield))
            {

                if (damagee > attacked.IronShieldDefence)
                {
                    damagee -= (int)attacked.IronShieldDefence;
                    CreateAzureDMG(attacked.IronShieldDefence, attacker, attacked);
                    attacked.RemoveFlag((ulong)FlagsUpdate.Flags.IronShield);
                }
                else
                {
                    CreateAzureDMG((uint)damagee, attacker, attacked);
                    attacked.IronShieldDefence -= (uint)damagee;
                    attacked.IronShieldPacket();
                    damagee = 1;
                }
            }
            #endregion
            #endregion
            if (damagee < 0)
                damagee = 1;
            return Math.Max(1, (uint)damagee);
        }
        public static uint Rango(Entity attacker, Entity attacked, ref Attack Packet)
        {
            if (Packet != null)
            {
                Packet.Effect = Attack.AttackEffects.None;
            }
            #region HideGui
            if (attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Player)
            {
                Data Data = new Data(true);
                Data.UID = attacker.UID;
                Data.UID = attacked.UID;
                Data.ID = Data.HideGui;
                attacker.Owner.Send(Data);
                attacked.Owner.Send(Data);
            }
            #endregion
            if (attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Player)
            {
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex)) return 1;
                if (attacker.Transformed) return 0;
                if (attacker == null) return 0;
                if (attacked == null) return 0;
                if (attacker.EntityFlag == EntityFlag.Player)
                {

                }
                Durability(attacker, attacked, null);
                if (attacker.Transformed)
                    return 0;
                Double attack = attacker.MinAttack + Kernel.Random.Next((int)attacker.MaxAttack - (int)attacker.MinAttack);


                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    var bonus = attacker.Gems[1];
                    if (bonus > 0)
                    {
                        attack += MathHelper.MulDiv((int)attack, bonus, 100);
                    }
                }
                if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))
                    if (!attacker.Transformed)
                        attack = (long)(attack * 1.30);
                Double defense = Revora.Game.Features.WarriorStance.Defence(attacked,attacked.Defence,Time32.Now);
                Double damage = attack - defense;
                if (attacked.EntityFlag == EntityFlag.Player)
                {

                }
                var targetLevel = attacked.Level;
                if (targetLevel < 22) damage = MathHelper.MulDiv((int)damage, 35, 100);
                else if (targetLevel < 50) damage = MathHelper.MulDiv((int)damage, 25, 100);
                else if (targetLevel < 90) damage = MathHelper.MulDiv((int)damage, 20, 100);
                else if (targetLevel < 110) damage = MathHelper.MulDiv((int)damage, 15, 100);
                else damage = MathHelper.MulDiv((int)damage, 12, 100);
                damage = CombatMath.BattlePower(attacker, attacked, (int)damage, Packet, false);
                damage = CombatMath.Critical(attacker, attacked, damage, Packet, false);
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    var reduce = Math.Max(0, Math.Min(100, (int)attacked.Gems[7]));
                    if (reduce > 0)
                    {
                        damage -= damage * reduce / 100;
                    }
                }
                damage = AdjustRebirthDamage(attacked.Owner, attacker.Owner, (int)damage);
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    var reduce = Math.Max(0, Math.Min(100, (int)attacked.ItemBless));
                    if (reduce > 0)
                    {
                        damage -= damage * (int)reduce / 100;
                    }
                }
                if (attacked.EntityFlag == EntityFlag.Monster)
                    damage = damage * 3;
                if (attacker.OnIntensify && Time32.Now >= attacker.IntensifyStamp.AddSeconds(4))
                {
                    damage *= 2;
                    attacker.OnIntensify = false;
                    attacker.RemoveFlag((ulong)FlagsUpdate.Flags.Intensify);
                }
                #region Block
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    if (Kernel.ChanceSuccess((float)attacked.Block / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.Block;
                        damage = (Int32)Math.Floor((float)damage / 2);
                    }
                }
                #endregion
                #region Reduction!
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
                {
                    if (damage > attacked.AzureShieldDefence)
                    {
                        damage -= attacked.AzureShieldDefence;
                        CreateAzureDmg(attacked.AzureShieldDefence, attacker, attacked);
                        attacked.RemoveFlag((ulong)FlagsUpdate.Flags.AzureShield);
                        attacked.AzuredShield = true;
                    }
                    else
                    {
                        CreateAzureDmg((uint)damage, attacker, attacked);
                        attacked.AzureShieldDefence -= (ushort)damage;
                        attacked.AzureShieldPacket();
                        damage = 1;
                    }
                }
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.IronShield))
                {

                    if (damage > attacked.IronShieldDefence)
                    {
                        damage -= (int)attacked.IronShieldDefence;
                        CreateAzureDMG(attacked.IronShieldDefence, attacker, attacked);
                        attacked.RemoveFlag((ulong)FlagsUpdate.Flags.IronShield);
                    }
                    else
                    {
                        CreateAzureDMG((uint)damage, attacker, attacked);
                        attacked.IronShieldDefence -= (uint)damage;
                        attacked.IronShieldPacket();
                        damage = 1;
                    }
                }
                #endregion
                if (damage < 0)
                    damage = 1;
                return Math.Max((uint)1, (uint)damage);
            }
            else if (attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Monster)
            {
                int Damage = 0;
                Damage = MyMath.GetDamageEntity2Monster(attacker, attacked, Attack.Ranged, ref Packet);
                CheckDamage(attacker, attacked, ref Damage, ref Packet, false);
                return (uint)Damage;
            }
            else if (attacker.EntityFlag == EntityFlag.Monster && attacked.EntityFlag == EntityFlag.Player)
            {
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex))
                    return 1;
                if (attacker.Transformed)
                    return 0;
                if (attacker == null)
                    return 0;
                if (attacked == null)
                    return 0;

                Durability(attacker, attacked, null);
                if (attacker.Transformed)
                    return 0;
                Double attack = attacker.MinAttack + Kernel.Random.Next((int)attacker.MaxAttack - (int)attacker.MinAttack);

                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    var bonus = attacker.Gems[1];
                    if (bonus > 0)
                    {
                        attack += MathHelper.MulDiv((int)attack, bonus, 100);
                    }
                }
                if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))
                    if (!attacker.Transformed)
                        attack = (long)(attack * 1.30);
                Double defense = Revora.Game.Features.WarriorStance.Defence(attacked,attacked.Defence,Time32.Now);
                Double damage = attack - defense;
                if (attacked.EntityFlag == EntityFlag.Player)
                {

                }
                var targetLevel = attacked.Level;
                if (targetLevel < 22) damage = MathHelper.MulDiv((int)damage, 35, 100);
                else if (targetLevel < 50) damage = MathHelper.MulDiv((int)damage, 25, 100);
                else if (targetLevel < 90) damage = MathHelper.MulDiv((int)damage, 20, 100);
                else if (targetLevel < 110) damage = MathHelper.MulDiv((int)damage, 15, 100);
                else damage = MathHelper.MulDiv((int)damage, 12, 100);
                damage = CombatMath.Critical(attacker, attacked, damage, Packet, false);
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    var reduce = Math.Max(0, Math.Min(100, (int)attacked.Gems[7]));
                    if (reduce > 0)
                    {
                        damage -= damage * reduce / 100;
                    }
                }
                damage = AdjustRebirthDamage(attacked.Owner, attacker.Owner, (int)damage);
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    var reduce = Math.Max(0, Math.Min(100, (int)attacked.ItemBless));
                    if (reduce > 0)
                    {
                        damage -= damage * (int)reduce / 100;
                    }
                }
                if (attacked.EntityFlag == EntityFlag.Monster)
                    damage = damage * 3;
                if (attacker.OnIntensify && Time32.Now >= attacker.IntensifyStamp.AddSeconds(4))
                {
                    damage *= 2;
                    attacker.OnIntensify = false;
                    attacker.RemoveFlag((ulong)FlagsUpdate.Flags.Intensify);
                }
                #region Block
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    if (Kernel.ChanceSuccess((float)attacked.Block / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.Block;
                        damage = (Int32)Math.Floor((float)damage / 2);
                    }
                }
                #endregion
                #region Reduction!
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
                {
                    if (damage > attacked.AzureShieldDefence)
                    {
                        damage -= attacked.AzureShieldDefence;
                        CreateAzureDmg(attacked.AzureShieldDefence, attacker, attacked);
                        attacked.RemoveFlag((ulong)FlagsUpdate.Flags.AzureShield);
                        attacked.AzuredShield = true;
                    }
                    else
                    {
                        CreateAzureDmg((uint)damage, attacker, attacked);
                        attacked.AzureShieldDefence -= (ushort)damage;
                        attacked.AzureShieldPacket();
                        damage = 1;
                    }
                }
                #endregion
                #region IronShield
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.IronShield))
                {

                    if (damage > attacked.IronShieldDefence)
                    {
                        damage -= (int)attacked.IronShieldDefence;
                        CreateAzureDMG(attacked.IronShieldDefence, attacker, attacked);
                        attacked.RemoveFlag((ulong)FlagsUpdate.Flags.IronShield);
                    }
                    else
                    {
                        CreateAzureDMG((uint)damage, attacker, attacked);
                        attacked.IronShieldDefence -= (uint)damage;
                        attacked.IronShieldPacket();
                        damage = 1;
                    }
                }
                #endregion
                if (damage < 0)
                    damage = 1;
                return Math.Max((uint)1, (uint)damage);
            }
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex)) return 1;
            if (attacker.Transformed) return 0;
            if (attacker == null) return 0;
            if (attacked == null) return 0;
            #region Effect By Maxs

            if (attacker.EntityFlag == EntityFlag.Player)
            {

            }

            #endregion
            Durability(attacker, attacked, null);
            if (attacker.Transformed)
                return 0;
            Double attacke = attacker.MinAttack + Kernel.Random.Next((int)attacker.MaxAttack - (int)attacker.MinAttack);

            if (attacker.EntityFlag == EntityFlag.Player)
            {
                var bonus = attacker.Gems[1];
                if (bonus > 0)
                {
                    attacke += MathHelper.MulDiv((int)attacke, bonus, 100);
                }
            }
            if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))
                if (!attacker.Transformed)
                    attacke = (long)(attacke * 1.30);
            Double defensee = Revora.Game.Features.WarriorStance.Defence(attacked,attacked.Defence,Time32.Now);
            Double damagee = attacke - defensee;
            if (attacked.EntityFlag == EntityFlag.Player)
            {

            }
            var targetLevell = attacked.Level;
            if (targetLevell < 22) damagee = MathHelper.MulDiv((int)damagee, 35, 100);
            else if (targetLevell < 50) damagee = MathHelper.MulDiv((int)damagee, 25, 100);
            else if (targetLevell < 90) damagee = MathHelper.MulDiv((int)damagee, 20, 100);
            else if (targetLevell < 110) damagee = MathHelper.MulDiv((int)damagee, 15, 100);
            else damagee = MathHelper.MulDiv((int)damagee, 12, 100);
            damagee = CombatMath.Critical(attacker, attacked, damagee, Packet, false);
            if (attacked.EntityFlag == EntityFlag.Player)
            {
                var reduce = Math.Max(0, Math.Min(100, (int)attacked.Gems[7]));
                if (reduce > 0)
                {
                    damagee -= (damagee / 2) * reduce / 10;
                }
            }
            damagee = AdjustRebirthDamage(attacked.Owner, attacker.Owner, (int)damagee);
            if (attacked.EntityFlag == EntityFlag.Player)
            {
                var reduce = Math.Max(0, Math.Min(100, (int)attacked.ItemBless));
                if (reduce > 0)
                {
                    damagee -= damagee * (int)reduce / 100;
                }
            }
            if (attacked.EntityFlag == EntityFlag.Monster)
                damagee = damagee * 3;
            if (attacker.OnIntensify && Time32.Now >= attacker.IntensifyStamp.AddSeconds(4))
            {
                damagee *= 2;
                attacker.OnIntensify = false;
                attacker.RemoveFlag((ulong)FlagsUpdate.Flags.Intensify);
            }
            #region Block
            if (attacked.EntityFlag == EntityFlag.Player)
            {
                if (Kernel.ChanceSuccess((float)attacked.Block / 100f))
                {
                    Packet.Effect |= Attack.AttackEffects.Block;
                    damagee = (Int32)Math.Floor((float)damagee / 2);
                }
            }
            #endregion
            #region Reduction!
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
            {
                if (damagee > attacked.AzureShieldDefence)
                {
                    damagee -= attacked.AzureShieldDefence;
                    CreateAzureDmg(attacked.AzureShieldDefence, attacker, attacked);
                    attacked.RemoveFlag((ulong)FlagsUpdate.Flags.AzureShield);
                    attacked.AzuredShield = true;
                }
                else
                {
                    CreateAzureDmg((uint)damagee, attacker, attacked);
                    attacked.AzureShieldDefence -= (ushort)damagee;
                    attacked.AzureShieldPacket();
                    damagee = 1;
                }
            }
            #region IronShield
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.IronShield))
            {

                if (damagee > attacked.IronShieldDefence)
                {
                    damagee -= (int)attacked.IronShieldDefence;
                    CreateAzureDMG(attacked.IronShieldDefence, attacker, attacked);
                    attacked.RemoveFlag((ulong)FlagsUpdate.Flags.IronShield);
                }
                else
                {
                    CreateAzureDMG((uint)damagee, attacker, attacked);
                    attacked.IronShieldDefence -= (uint)damagee;
                    attacked.IronShieldPacket();
                    damagee = 1;
                }
            }
            #endregion

            #endregion
            if (damagee < 0)
                damagee = 1;
            return Math.Max((uint)1, (uint)damagee);
        }
        public static uint Rango(Entity attacker, SobNpcSpawn attacked, ref Attack Packet)
        {
            long Damage = 0;
            Durability(attacker, null, null);
            if (attacker.Transformed) return 0;
            Damage = Kernel.Random.Next((int)Math.Min(attacker.MinAttack, attacker.MaxAttack),
                (int)Math.Max(attacker.MinAttack, attacker.MaxAttack) + 1);
            if (attacker.Gems[1] != 0)
            {
                Damage += (uint)Math.Floor(Damage * (double)(attacker.Gems[1] * 0.01));
            }
            if (attacker.OnIntensify && Time32.Now >= attacker.IntensifyStamp.AddSeconds(4))
            {
                Damage *= 2;
                attacker.OnIntensify = false;
                attacker.RemoveFlag((ulong)FlagsUpdate.Flags.Intensify);
            }

            if (Kernel.ChanceSuccess(5))
            {
                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    if (attacker.Owner.BlessTime > 0)
                    {
                        Damage *= 2;
                        _String str = new _String(true);
                        str.UID = attacker.UID;
                        str.TextsCount = 1;
                        str.Type = _String.Effect;
                        str.Texts.Add("LuckyGuy");
                        attacker.Owner.SendScreen(str, true);
                    }
                }
            }
            if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))
                if (!attacker.Transformed)
                    Damage = (long)(Damage * 1.30);
            Damage += attacker.PhysicalDamageIncrease;
            Damage += Damage;
            Damage += Damage / 4;
            if (Damage <= 0)
                Damage = 1;
            return (uint)(Damage /4);
        }

        public enum AttackType : byte
        {
            Magic = 0,
            Melee = 1,
            Ranged = 2,
        }
        public static uint CalculateDamage(Entity attacker, SobNpcSpawn attacked, ref Attack Packet, AttackType type, Database.SpellInformation spell = null)
        {
            uint Damage = 0;
            if (type == AttackType.Melee)
            {
                Damage = Melee(attacker, attacked, ref Packet);
            }
            else if (type == AttackType.Magic)
            {
                Damage = Magic(attacker, attacked, spell, ref Packet);
            }
            else if (type == AttackType.Ranged)
            {
                Damage = Ranged(attacker, attacked, ref Packet);
            }
            Damage = (uint)(Damage * 1.5);

            if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Intensify))
            {
                Damage = (uint)(Damage * attacker.SpiritFocusPercent);
                attacker.RemoveFlag((ulong)FlagsUpdate.Flags.Intensify);
            }
            return Damage;
        }
        public static uint CalculateDamage(Entity attacker, Entity attacked, ref Attack Packet, AttackType type, byte SkillSoul, Database.SpellInformation spell = null, ushort Spell = 0, ushort spelllevel = 0)
        {
            uint Damage = 0;

            if (type == AttackType.Melee)
            {

                if (spell == null)
                {
                    Damage = Melee(attacker, attacked, ref Packet);
                }

                else
                {
                    Damage = Melee(attacker, attacked, spell, ref Packet);
                }
            }
            else if (type == AttackType.Magic)
            {
                if (spell == null)
                {
                    Damage = Magic(attacker, attacked, spell, ref Packet);
                }
                else
                {
                    Damage = Magic(attacker, attacked, spell, ref Packet);
                }
            }
            else if (type == AttackType.Ranged)
            {
                if (spell == null)
                {
                    Damage = Ranged(attacker, attacked, ref Packet);
                }
                else
                {
                    Damage = Ranged(attacker, attacked, spell, ref Packet);
                }
            }

            if (attacker != null && attacked != null)
            {
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.DragonCyclone))
                    {
                        if (attacked.EntityFlag == EntityFlag.Monster)
                        {
                            if (attacked.MonsterInfo != null)
                            {
                                if (!attacked.MonsterInfo.Boss)
                                {
                                    Damage = Damage * 2;
                                }
                            }
                        }
                    }
                    if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.BladeFlurry))
                    {
                        if (attacked.EntityFlag == EntityFlag.Monster)
                        {
                            if (attacked.MonsterInfo != null)
                            {
                                if (!attacked.MonsterInfo.Boss)
                                {
                                    Damage = Damage * 5;
                                }
                            }
                        }
                    }
                }
            }
            #region DragonSwing
            if (Kernel.Rate(20))// base chance 
            {
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.DragonSwing))
                {
                    _String str = new _String(true);
                    str.UID = attacked.UID;
                    str.TextsCount = 1;
                    str.Type = (int)_String.Effect;
                    str.Texts.Add("poisonmiss");
                    if (attacked.Owner != null && attacked.EntityFlag == EntityFlag.Player)
                        attacked.Owner.SendScreen(str, true);
                    Damage = 0;
                }
            }
            #endregion
            if (attacker != null && attacked != null && attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Player)
            {
                Data Data = new Data(true);
                Data.UID = attacker.UID;
                Data.UID = attacked.UID;
                Data.ID = Data.CustomCommands.HideGUI;
                attacker.Owner.Send(Data);
                attacked.Owner.Send(Data);
            }
            if (spell != null && spell.CanKill && spell.ID != 12070 && spell.ID != 11610 && type != AttackType.Magic)
            {
                if (spell.PowerPercent != 0)
                {
                    Damage = (uint)(Damage * spell.PowerPercent);
                }
            }
            else
            {
                if (spell != null && spell.ID == 11610)
                {
                    if (attacked != null && attacked.EntityFlag == EntityFlag.Monster)
                    {
                        Damage = (uint)(Damage * spell.PowerPercent);
                    }
                    else
                    {
                        if (attacked != null && attacked.EntityFlag == EntityFlag.Player)
                        {
                            Damage = (uint)(Damage / spell.PowerPercent);
                        }
                    }
                }
            }
            if (attacked != null && attacked.EntityFlag == EntityFlag.Monster)
            {
                if (attacked.Name == "SkyJab")
                    Damage /= 4;
                if (attacked.Name == "CryptScorpion" || attacked.Name == "GreenNightDevil"
                    || attacked.Name == "AxeKnight" || attacked.Name == "LordofStorm")
                    Damage /= 4;
                if (attacked.Name == "SnowBanshee" || attacked.Name == "SnowBansheeSoul"
                    || attacked.Name == "NemesisTyrant" || attacked.Name == "Thundercloud" || attacked.Name == "CreepySpook"
                    || attacked.Name == "EvilXmasBeast" || attacked.Name == "AlienBeast"
                    || attacked.Name == "PurpleBanshee" || attacked.Name == "FuriousDevastato"
                    || attacked.Name == "AwakeDevastator" || attacked.Name == "EvilMonkMiseryA"
                    || attacked.Name == "GhostReaverAdv" || attacked.Name == "TeratoDragon"
                    || attacked.Name == "LavaBeast" || attacked.Name == "SwordMaster"
                    || attacked.Name == "ThirillingSpook" || attacked.Name == "GuildBeast"
                    /*|| attacked.Name == "WarriorofRage"*/
                    || attacked.Name == "ThunderDragon")
                    Damage *= 1;
                if (attacked.Name == "Pheasant")
                {
                    if (attacker.BattlePower >= 200)
                    {
                        Damage /= 10;
                    }
                }
                if (attacked.Name == "RedDevilL117")
                {
                    Damage *= 3;
                }
            }
            if (attacked != null && attacked.EntityFlag == EntityFlag.Player)
            {
                if (attacked.UID == 1087425)
                    return 1;

                if (attacker.Class < 100)
                {
                    if (spell != null && (spell.ID == 1001 || spell.ID == 1000))
                    {
                        if (attacked != null && attacked.EntityFlag == EntityFlag.Player)
                        {
                            Damage = (uint)(Damage / 4);
                        }
                    }
                    if (attacker.BattlePower >= 370 && attacker.BattlePower < 396 && attacked.BattlePower >= 400)
                    {
                        Damage = (uint)(Damage * 2);
                    }
                    if (attacker.BattlePower >= 340 && attacker.BattlePower < 370 && attacked.BattlePower >= 385)
                    {
                        Damage = (uint)(Damage * 2);
                    }
                    if (attacker.BattlePower >= 200 && attacker.BattlePower < 340 && attacked.BattlePower >= 350)
                    {
                        Damage = (uint)(Damage * 2.5);
                    }
                }
            }
            if (attacked != null && attacked.EntityFlag == EntityFlag.Player)
            {
                if (Damage > (attacked.Hitpoints + attacked.Hitpoints))
                {
                    Damage -= (uint)1.5;
                }
                else if (Damage > (attacked.Hitpoints + attacked.Hitpoints / 2))
                {
                    Damage -= (uint)1.5;
                }
            }
            return Damage;
        }
        public static void Immu(Entity Attacked)
        {
            if (Attacked.EntityFlag == EntityFlag.Player)
            {
                _String str = new _String(true);
                str.UID = Attacked.UID;
                str.TextsCount = 1;
                str.Type = _String.Effect;
                str.Texts.Add("bossimmunity");
                Attacked.Owner.SendScreen(str, true);
            }

        }
        #region BreakingTouch
        public static void CreateAzureDMG(uint dmg, Entity attacker, Entity attacked)
        {
            Network.GamePackets.Attack attac = new Attack(true);
            attac.Attacker = attacker.UID;
            attac.Attacked = attacked.UID;
            attac.X = attacked.X;
            attac.Y = attacked.Y;
            attac.AttackType = 55;
            attac.Damage = dmg;
            attacked.Owner.SendScreen(attac, true);
        }
        #endregion
        #region Attack by BattlePower
        public static int[] BattlePowerNoRebornFactors = { 10, 6, 6, 6, 6 };
        public static int[] BattlePowerWithRebornFactors = { 18, 18, 14, 27, 18 };
        public static int BattlePowerFactors(Entity Attacked)
        {
            if (Attacked.EntityFlag == EntityFlag.Player)
            {
                var Prof = Attacked.Class;
                int Factor = 0;
                if (Prof >= 10 && Prof <= 15)
                    Factor = 0; // Trojan                   
                else if (Prof >= 20 && Prof <= 25)
                    Factor = 1; // Warrior
                else if (Prof >= 40 && Prof <= 45)
                    Factor = 2; // Archer / Assassin
                else if (Prof >= 100 && Prof <= 101 || Prof >= 142 && Prof <= 145)
                    Factor = 3; // Fire Taoist
                else if (Prof >= 132 && Prof <= 135)
                    Factor = 4; // Water Taoist 
                else if (Prof >= 80 && Prof <= 85)
                    Factor = 5; // Dragon Warrior 
                else if (Prof >= 70 && Prof <= 75)
                    Factor = 6; // Pirate 
                else if (Prof >= 50 && Prof <= 55)
                    Factor = 7; //Ninja 
                else if (Prof >= 60 && Prof <= 65)
                    Factor = 8; // Monk 

                else
                    return 1;
                if (Attacked.Reborn != 0)
                    return BattlePowerWithRebornFactors[Factor];
                else
                    return BattlePowerNoRebornFactors[Factor];
            }
            else
                return 1;
        }

        public const int DefaultDefense2 = 10000;

        public static long BigMulDiv(long number, long numerator, long denominator)
        {
            return (number * numerator + denominator / 2) / denominator;
        }
        #endregion
        public static bool IsTargetDodged(Entity attacker, Entity attacked)
        {
            int nDodge = 0;
            int nHitRate;
            if (attacked.EntityFlag == EntityFlag.Player)
            {
                nDodge = 40;
            }
            if (!attacked.Transformed)
                nHitRate = Math.Min(100, Math.Max(0, 100 + (ushort)(attacker.frequency * 0.25) + attacker.Gems[2] + (int)attacker.HitRate - nDodge - (int)attacked.Dodge));
            else
                nHitRate = Math.Min(100, Math.Max(0, 100 + (int)attacker.TransformationHitRate - nDodge - (int)attacked.TransformationDodge));
            return false;
        }
        #region PvP system
        public static bool RateStatus(double value)
        {
            return (Kernel.Random.Next() % 100) < value;
        }
        public static int AdjustDefence(Entity attacked, int nDag)
        {
            if (!attacked.Transformed)
            {

                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.MagicShield))
                {
                    if (attacked.ShieldTime > 0)//if (attacked.ShieldTime > 0)
                        nDag = nDag - (ushort)(nDag * attacked.ShieldIncrease);                   
                    nDag = nDag - (ushort)(nDag * attacked.MagicShieldIncrease);// Shield reduces damage
                }
                nDag = Math.Max(0,nDag - (Revora.Game.Features.WarriorStance.Defence(attacked,attacked.Defence,Time32.Now) - attacked.Defence));
            }
            return nDag;
        }
        public static void Refinary(Entity attacker, Entity attacked, ref double Damage, ref Attack Packet, bool magic = false)
        {
            Damage = CombatMath.Critical(attacker, attacked, Damage, Packet, magic);
            if (attacked.EntityFlag == EntityFlag.Player && CombatMath.Roll(attacked.Block / 100.0))
            {
                Damage /= 2;
                if (Packet != null) Packet.Effect |= Attack.AttackEffects.Block;
            }
        }
        #endregion
        private static int AdjustAttack(Entity attacker, Entity attacked, int nAtk)
        {
            if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))
                if (!attacker.Transformed)
                    nAtk = (int)(nAtk * 1.10);
            return nAtk;
        }

        private static int AdjustMinDamageUser2Monster(int nDamage, Entity attacker, Entity attacked)
        {
            int nMinDamage = 1;
            nMinDamage += attacker.Level / 10;
            if (!(attacker.EntityFlag == EntityFlag.Player))
                return (nMinDamage >= nDamage) ? nMinDamage : nDamage;
            if (!attacker.Owner.Equipment.Free(ConquerItem.RightWeapon))
            {
                ConquerItem item = attacker.Owner.Equipment.TryGetItem(ConquerItem.RightWeapon);
                nMinDamage += (int)(item.ID % 10);
            }
            return (nMinDamage >= nDamage) ? nMinDamage : nDamage;
        }
        private static int AdjustMinDamageMonster2User(int nDamage, Entity attacker, Entity attacked)
        {
            int nMinDamage = 7;
            if (nDamage >= nMinDamage || attacked.Level <= 15)
                return nDamage;
            if (!(attacked.EntityFlag == EntityFlag.Player))
                return (nMinDamage >= nDamage) ? nMinDamage : nDamage;
            for (int i = 1; i < 12; i++)
            {
                switch (i)
                {
                    case 1:
                    case 2:
                    case 3:
                        {
                            if (!attacked.Owner.Equipment.Free((byte)i))
                            {
                                ConquerItem item = attacked.Owner.Equipment.TryGetItem((byte)i);
                                nMinDamage -= (int)(item.ID % 10) / 5;
                            }
                            break;
                        }
                }
            }
            nMinDamage = (1 >= nMinDamage) ? 1 : nMinDamage;
            return (nMinDamage >= nDamage) ? nMinDamage : nDamage;
        }

        private static int CalReduceDamageBattlePowerMelee(Entity attacker, Entity attacked, int damage)
        {
            int subbattlepower = attacked.BattlePower - attacker.BattlePower;
            if (subbattlepower == 0)
                return damage;
            int ReduceDamage = 100 - (20 + (subbattlepower * 10));
            if (subbattlepower < 5)
                return (int)((long)damage * ReduceDamage / 100);
            else if (subbattlepower >= 5)
                return (int)((long)damage * 30 / 100);
            return damage;
        }
        private static int CalReduceDamageBattlePowerMagic(Entity attacker, Entity attacked, int damage)
        {
            int subbattlepower = attacked.BattlePower - attacker.BattlePower;
            if (subbattlepower == 0)
                return damage;
            int ReduceDamage = 100 - (subbattlepower * 10);
            if (subbattlepower < 5)
                return (int)((long)damage * ReduceDamage / 100);
            else if (subbattlepower >= 5)
                return (int)((long)damage * 50 / 100);
            return damage;
        }
        /* Jheisson*/
        public static int DefaultDefense = 10000;
        //public static int DefeAire = 10000;
        //public static int DefeGuerrero = 10000;

        public static int GetDefense(Client.GameState attacked, Client.GameState attacker) // Profession defense
        {
            if (attacker == null) return DefaultDefense;
            if (attacked == null) return DefaultDefense;
            if (attacker.Entity.EntityFlag == EntityFlag.Player && attacked.Entity.EntityFlag == EntityFlag.Player)
            {
                if (attacked.Entity.Reborn == 0) return DefaultDefense;
                var defense2 = (attacked.Entity.Class % 10) >= 3 ? 7000 : DefaultDefense;

                if (attacked.Entity.Reborn < 2)
                {
                    return defense2;
                }
                var targetHero = attacked as Client.GameState;
                if (targetHero != null)
                {
                    if (attacker.Entity.Reborn < 2)
                    {
                        return targetHero.Entity.Reborn == 2 ? 5000 : 7000;
                    }
                }
                return defense2;
            }
            else
            {
                return DefaultDefense;
            }
        }
        public static int CalcMulDiv(int number, int numerator, int denominator)
        {
            return (number * numerator + denominator / 2) / denominator;
        }
        public static int AdjustRebirthDamage(Client.GameState target, Client.GameState attacker, int damage)
        {
            return CalcMulDiv(damage, GetDefense(target, attacker), DefaultDefense);
        }

        private static int CalcDamageMonster2User(int nAtk, int nDef, int nAtkLev, int nDefLev, int nDamage)
        {
            if (nAtkLev > 120)
                nAtkLev = 120;
            MONSTER_NAME_TYPE nNameType = GetNameType(nDefLev, nAtkLev);
            if (nNameType == MONSTER_NAME_TYPE.NAME_RED)
            {
                nDamage = (int)(((float)nAtk * 1.5F) - nDef);
            }
            else if (nNameType == MONSTER_NAME_TYPE.NAME_BLACK)
            {
                int nDeltaLev = nDefLev - nAtkLev;
                if (nDeltaLev >= -10 && nDeltaLev <= -5)
                    nAtk *= 2;
                else if (nDeltaLev >= -20 && nDeltaLev < -10)
                    nAtk = (int)((float)nAtk * 3.5F);
                else if (nDeltaLev < -20)
                    nAtk *= 5;
                nDamage = nAtk - nDef;
            }
            return (0 >= nDamage) ? 0 : nDamage;
        }
        private static int CalcDamageUser2Monster(int nAtk, int nDef, int nAtkLev, int nDefLev, int nDamage)
        {
            if (nAtkLev > 120)
                nAtkLev = 120;
            if (GetNameType(nAtkLev, nDefLev) != MONSTER_NAME_TYPE.NAME_GREEN)
                return (0 >= nDamage) ? 0 : nDamage;
            int nDeltaLev = nAtkLev - nDefLev;
            if (nDeltaLev >= 3 && nDeltaLev <= 5)
                nAtk = (int)((float)nAtk * 1.5F);
            else if (nDeltaLev > 5 && nDeltaLev <= 10)
                nAtk *= 2;
            else if (nDeltaLev > 10 && nDeltaLev <= 20)
                nAtk = (int)((float)nAtk * 2.5F);
            else if (nDeltaLev > 20)
                nAtk *= 3;
            return (0 >= nAtk - nDef) ? 0 : nAtk - nDef;
        }

        enum MONSTER_NAME_TYPE
        {
            NAME_GREEN,
            NAME_WHITE,
            NAME_RED,
            NAME_BLACK,
        };
        [DllImport("kernel32.dll")]
        static extern int MulDiv(int nNumber, int nNumerator, int nDenominator);
        private static MONSTER_NAME_TYPE GetNameType(int nAtkerLev, int nMonsterLev)
        {
            int nDeltaLev = nAtkerLev - nMonsterLev;
            if (nDeltaLev >= 3)
                return MONSTER_NAME_TYPE.NAME_GREEN;
            else if (nDeltaLev >= 0)
                return MONSTER_NAME_TYPE.NAME_WHITE;
            else if (nDeltaLev >= -5)
                return MONSTER_NAME_TYPE.NAME_RED;
            else return MONSTER_NAME_TYPE.NAME_BLACK;
        }

        public static uint Melee(Entity attacker, Entity attacked, ref Attack Packet)
        {
            Database.SpellInformation spell = null;
            return Melee(attacker, attacked, spell, ref Packet);
            #region CP wager
            //// Do not touch. Removing the CP wager does not affect anything.
            //long Damage = 0;
            //if (Constants.ZonaTroyanos.Contains(attacker.MapID))
            //{
            //    Damage = (int)attacked.Hitpoints + 1;
            //    return (uint)Damage;
            //}
            //if (Constants.ZonaNinjas.Contains(attacker.MapID))
            //{
            //    Damage = (int)attacked.Hitpoints + 1;
            //    return (uint)Damage;
            //}
            //if (Constants.ZonaMonjes.Contains(attacker.MapID))
            //{
            //    Damage = (int)attacked.Hitpoints + 1;
            //    return (uint)Damage;
            //}
            #endregion
        }
        public static uint Melee(Entity attacker, Entity attacked, Database.SpellInformation spell, ref Attack Packet)
        {

            #region EquipmentEffect
            if (attacked != null)
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    BlessEffect.Effect(attacked);
                }
            if (Kernel.RandGet(10) == 7)
                if (attacker.EntityFlag == EntityFlag.Player)
                    GemEffect.Effect(attacker);
            #endregion
            #region HideGui
            if (attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Player)
            {
                Data Data = new Data(true);
                Data.UID = attacker.UID;
                Data.UID = attacked.UID;
                Data.ID = Data.HideGui;
                attacker.Owner.Send(Data);
                attacked.Owner.Send(Data);
            }
            #endregion
            #region Return 1
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex)) return 1;

            if (attacked.Name == "GoldenOctopus") return 1;
            #endregion
            uint rushHit;
            if (Revora.Game.Features.DailyWorldBosses.TryHit(attacker, attacked, out rushHit)
                || Revora.Game.Features.ExpeditionEvent.TryHit(attacker, attacked, out rushHit)
                || Revora.Game.Features.BossRushSystem.TryRushHit(attacker, attacked, out rushHit))
                return rushHit;
            #region Player -> Player \ Monster
            int nMaxRand = 50;
            int nAtk = 0;
            Durability(attacker, attacked, null);
            if (!IsTargetDodged(attacker, attacked))
            {
                if (!attacker.Transformed)
                {
                    if (Kernel.RandGet(100) < nMaxRand)
                    {
                        nAtk = (int)(attacker.MaxAttack - Kernel.RandGet((int)(attacker.MaxAttack - attacker.MinAttack) / 2)) * 80 / 100;
                        nAtk += nAtk * attacker.Gems[1] / 100;
                    }
                    else
                    {
                        nAtk = (int)(attacker.MinAttack + Kernel.RandGet((int)(attacker.MaxAttack - attacker.MinAttack) / 2)) * 80 / 100;
                        nAtk += nAtk * attacker.Gems[1] / 100;
                    }
                }
                else
                {
                    if (Kernel.RandGet(100) < nMaxRand)
                        nAtk = (int)(attacker.TransformationMaxAttack - Kernel.RandGet((int)(attacker.TransformationMaxAttack - attacker.TransformationMinAttack) / 2)) * 80 / 100;
                    else nAtk = (int)(attacker.TransformationMinAttack + Kernel.RandGet((int)(attacker.TransformationMaxAttack - attacker.TransformationMinAttack) / 2)) * 80 / 100;
                }
                nAtk = AdjustAttack(attacker, attacked, nAtk);
                int nRawDef;
                if (!attacked.Transformed)
                {
                    nRawDef = attacked.Defence;
                }
                else
                {
                    nRawDef = attacked.TransformationDefence;
                }
                int nDef = AdjustDefence(attacker, attacked, nRawDef); // Superman attack (Jheisson)
                if (attacker.OnSuperman())
                {
                    nDef *= 1;
                }
                int Damage = nAtk - nDef;
                if (attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Monster)
                {
                    Damage = CalcDamageUser2Monster(nAtk, nDef, attacker.Level, attacked.Level, Damage);
                    Damage = MulDiv(Damage, 10000, 10000);
                    Damage = AdjustMinDamageUser2Monster(Damage, attacker, attacked);
                }
                else if (attacker.EntityFlag == EntityFlag.Monster && attacked.EntityFlag == EntityFlag.Player)
                {
                    Damage = CalcDamageMonster2User(nAtk, nDef, attacker.Level, attacked.Level, Damage);
                    Damage = MulDiv(Damage, (attacked.Reborn != 0 && attacked.Class % 10 >= 3) ? 7000 : 10000, 10000);
                    Damage = AdjustMinDamageMonster2User(Damage, attacker, attacked);
                }
                else
                {
                    if (attacked.EntityFlag == EntityFlag.Monster)
                        Damage = MulDiv(Damage, 10000, 10000);
                    else if (attacked.EntityFlag == EntityFlag.Player)
                        Damage = MulDiv(Damage, (attacked.Reborn != 0 && attacked.Class % 10 >= 3) ? 7000 : 10000, 10000);
                }
                Damage = CombatMath.Tortoise(attacked, Damage);
            Damage = CombatMath.Block(attacked, Damage, Packet);
            Damage = CombatMath.Critical(attacker, attacked, Damage, Packet, false);
                Damage += attacker.PhysicalDamageIncrease;
                Damage -= attacked.PhysicalDamageDecrease;

                #region Spell Bounes
                if (spell != null)
                {
                    if (spell.PowerPercent != 0)
                    {
                        Damage = (int)(Damage * spell.PowerPercent);
                    }
                }
                #endregion

                #region BattlePower damage

                //if (attacker.BattlePower >= 394 )
                //{
                //    if (attacked.BattlePower <= 393)
                //    {
                //        Damage = (int)((double)Damage * 1.31);
                //    }
                //}
                #endregion

                #region Nobility Damge
                //if (attacker.NobilityRank != attacked.NobilityRank)
                //{
                //    int addnobility_damage = 0;
                //    if ((byte)attacker.NobilityRank == 12)
                //        addnobility_damage = (Damage * ((byte)attacker.NobilityRank + 45)) / 100;
                //    else
                //        if ((byte)attacker.NobilityRank > 5)
                //            addnobility_damage = (Damage * ((byte)attacker.NobilityRank + 25)) / 100;
                //    if ((byte)attacked.NobilityRank > 5)
                //    {
                //        if ((addnobility_damage - (uint)((Damage * ((byte)attacked.NobilityRank + 8)) / 100)) > 0)
                //        {
                //            addnobility_damage -= (int)((Damage * ((byte)attacked.NobilityRank + 8)) / 100);
                //        }
                //        else
                //        {
                //            if (Damage > (uint)((Damage * ((byte)attacked.NobilityRank + 8)) / 100))
                //            {
                //                Damage -= (int)((Damage * ((byte)attacked.NobilityRank + 8)) / 100);
                //            }
                //            else
                //                Damage = 1;
                //        }
                //    }
                //    if (Damage != 1)
                //    {
                //        Damage += addnobility_damage;
                //    }
                //}
                #endregion
            // Nobility contributes through Battle Power; no second rank-pair damage multiplier.
                #region AzureShield
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
                {

                    if (Damage > attacked.AzureShieldDefence)
                    {
                        Damage -= (int)attacked.AzureShieldDefence;
                        CreateAzureDmg(attacked.AzureShieldDefence, attacker, attacked);
                        //attacked.RemoveFlag2((ulong)FlagsUpdate.Flags.AzureShield);
                    }
                    else
                    {
                        CreateAzureDmg((uint)Damage, attacker, attacked);
                        attacked.AzureShieldDefence -= (ushort)Damage;
                        attacked.AzureShieldPacket();
                        Damage = 1;
                    }
                }
                #endregion
                #region IronShield
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.IronShield))
                {

                    if (Damage > attacked.IronShieldDefence)
                    {
                        Damage -= (int)attacked.IronShieldDefence;
                        CreateAzureDMG(attacked.IronShieldDefence, attacker, attacked);
                        attacked.RemoveFlag((ulong)FlagsUpdate.Flags.IronShield);
                    }
                    else
                    {
                        CreateAzureDMG((uint)Damage, attacker, attacked);
                        attacked.IronShieldDefence -= (uint)Damage;
                        attacked.IronShieldPacket();
                        Damage = 1;
                    }
                }
                #endregion
                #region MagicShield
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.MagicShield))
                {
                    Damage = (int)((double)Damage * 55 / 60);// Shield reduces damage
                }
                #endregion

                #region DragonSwing
                if (Kernel.Rate(25))
                {
                    if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.DragonSwing))
                    {
                        _String str = new _String(true);
                        str.UID = attacked.UID;
                        str.TextsCount = 1;
                        str.Type = _String.Effect;
                        str.Texts.Add("poisonmiss");
                        if (attacked.EntityFlag == EntityFlag.Player)
                            attacked.Owner.SendScreen(str, true);
                        return 0;
                    }
                }
                #endregion
                #region Perfection
                ////if (attacker.Perfection != null && attacked != null)
                ////{
                ////    var canDO = attacked.Perfection == null || attacked.Owner == null
                ////        || attacked.Owner.Equipment == null ? true : attacker.Owner.Equipment.GetTPL() > attacked.Owner.Equipment.GetTPL();
                ////    if (canDO)
                ////    {
                //        if (attacker.ToxicFogPercent > 0 && Success(attacker.Perfection.ToxinEraserLevel))
                //        {
                //            attacker.ToxicFogPercent /= 2;
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.ToxinEraserLevel), true);
                //        }
                //        else if (Success(attacker.Perfection.LuckyStrike))
                //        {
                //            Damage *= 2;
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.LuckyStrike), true);
                //        }
                //// if (Success(attacker.Perfection.LightOfStamina))
                ////{
                ////    attacker.Stamina = 100;
                ////    attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.LightOfStamina), true);
                ////}
                //        else if (Success(attacker.Perfection.AbsoluteLuck))
                //        {
                //            Damage *= 2;
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.AbsoluteLuck), true);
                //        }
                //        else if (Success(attacker.Perfection.BloodSpawn))
                //        {
                //            attacker.Hitpoints = attacker.MaxHitpoints;
                //            attacker.Mana = attacker.MaxMana;
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.BloodSpawn), true);
                //        }
                //        else if (Success(attacker.Perfection.CalmWind))
                //        {
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.CalmWind), true);
                //        }
                ////else if (Success(attacker.Perfection.CoreStrike))
                ////{
                ////    Damage += ((int)(attacker.MagicDamageIncrease - attacked.Immunity) * 100);
                ////    attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.CoreStrike), true);
                ////}
                //        else if (attacked.Perfection != null && Success(attacked.Perfection.DivineGuard))
                //        {
                //            Damage -= ((int)(attacker.Defence * 5) / 100);
                //            attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.DivineGuard), true);
                //        }
                //        else if (Success(attacker.Perfection.DrainingTouch))
                //        {
                //            if (Success(80))
                //            {
                //                attacker.Hitpoints = attacker.MaxHitpoints;
                //                attacker.Mana = attacker.MaxMana;
                //                attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.DrainingTouch), true);
                //            }
                //        }
                //        else if (attacked.Perfection != null && Success(attacked.Perfection.FreeSoul))
                //        {
                //            if (attacked.ShackleTime > 0)
                //            {
                //                attacked.ShackleTime = 0;
                //                attacked.RemoveFlag2((ulong)FlagsUpdate.Flags.SoulShackle);
                //                attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.FreeSoul), true);
                //            }
                //        }
                //        else if (Success(attacker.Perfection.InvisableArrow))
                //        {
                //            Damage += ((int)(attacker.MagicAttack * 5) / 100);
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.InvisbleArrow), true);
                //        }
                //        else if (Success(attacker.Perfection.KillingFlash))
                //        {
                //            attacker.Experience += attacker.Level >= 140 ? 0 : ((uint)(attacker.Experience * 5) / 100);
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.KillingFlash), true);
                //        }
                //        else if (attacked.Perfection != null && Success(attacked.Perfection.MirrorOfSin))
                //        {
                //            attacked.Experience += attacked.Level >= 140 ? 0 : ((uint)(attacked.Experience * 5) / 100);
                //            attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.MirrorOfSin), true);
                //        }
                //        else if (Success(attacker.Perfection.ShieldBreak))
                //        {
                //            Damage += (int)(attacked.Block * 100);
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.ShiledBreak), true);
                //        }
                //        else if (attacked.Perfection != null && Success(attacked.Perfection.StraightLife))
                //        {
                //            if (attacked.Dead)
                //            {
                //                attacked.BringToLife();
                //                attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.ShiledBreak), true);
                //            }
                //        }
                //    }
                //}
                #endregion
                Damage = CombatMath.BattlePower(attacker, attacked, Damage, Packet, false);
                if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Oblivion))
                    if (attacked.EntityFlag == EntityFlag.Monster)
                        Damage *= 2;
                if (attacked.Action == Enums.ConquerAction.Sit)
                    if (attacked.EntityFlag == EntityFlag.Player)
                    {
                        if (attacked.Stamina >= 20)
                            attacked.Stamina -= 20;
                    }

                if (attacker.OnSuperman())
                    if (attacked.EntityFlag == EntityFlag.Monster)
                        if (!attacked.MonsterInfo.Boss)
                            Damage *= 2;
                if (attacker.OnSuperman())
                    if (attacked.EntityFlag == EntityFlag.Monster)
                        Damage *= 2;
                if (attacker.OnFatalStrike())
                    if (attacked.EntityFlag == EntityFlag.Monster)
                        if (!attacked.MonsterInfo.Boss)
                            Damage *= 5;
                        else Damage += (int)(Damage * .1);

                if (attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Player)
                {
                    Damage = AdjustRebirthDamage(attacked.Owner, attacker.Owner, Damage);
                }
                if (attacked != null)
                {
                    var reduces = Math.Max(0, Math.Min((int)100, (int)attacked.ItemBless));
                    if (reduces > 0)
                    {
                        Damage -= Damage * reduces / 100;
                    }
                }
                #region LuckyGuy [Flags.CastPray]
                //if (Kernel.Rate(5))
                //{
                //    if (attacker.EntityFlag == EntityFlag.Player)
                //    {
                //        if (attacker.Owner.BlessTime > 0)
                //        {
                //            Damage *= 2;
                //            _String str = new _String(true);
                //            str.UID = attacker.UID;
                //            str.TextsCount = 1;
                //            str.Type = _String.Effect;
                //            str.Texts.Add("LuckyGuy");
                //            attacker.Owner.SendScreen(str, true);
                //        }
                //    }
                //}
                if (Kernel.Rate(10))
                {
                    if (attacked.EntityFlag == EntityFlag.Player)
                    {
                        if (attacked.Owner.BlessTime > 0)
                        {
                            Damage = 1;
                            _String str = new _String(true);
                            str.UID = attacker.UID;
                            str.TextsCount = 1;
                            str.Type = _String.Effect;
                            str.Texts.Add("LuckyGuy");
                            attacked.Owner.SendScreen(str, true);
                        }
                    }
                }
                #endregion
               // AutoRespone(attacker, attacked, ref Damage);
                return (uint)((0 >= Damage) ? 1 : Damage);
            }
            return 0;
            #endregion

        }
        public static uint Melee(Entity attacker, SobNpcSpawn attacked, ref Attack Packet)
        {
            int Damage = 0;
            int nMaxRand = 50;
            int nAtk = 0;
            Boolean CritImmune = false;
            if (Kernel.RandGet(10) == 7)
                if (attacker.EntityFlag == EntityFlag.Player)
                    GemEffect.Effect(attacker);
            Durability(attacker, null, null);
            if (!attacker.Transformed)
            {
                if (Kernel.RandGet(100) < nMaxRand)
                {
                    nAtk = (int)(attacker.MaxAttack - Kernel.RandGet((int)(attacker.MaxAttack - attacker.MinAttack)) / 2) * 80 / 100;
                    nAtk += nAtk * attacker.Gems[1] / 100;
                }
                else
                {
                    nAtk = (int)(attacker.MinAttack + Kernel.RandGet((int)(attacker.MaxAttack - attacker.MinAttack)) / 2) * 80 / 100;
                    nAtk += nAtk * attacker.Gems[1] / 100;
                }
            }
            else
            {
                if (Kernel.RandGet(100) < nMaxRand)
                    nAtk = (int)(attacker.TransformationMaxAttack - Kernel.RandGet((int)(attacker.TransformationMaxAttack - attacker.TransformationMinAttack))) * 80 / 100;
                else nAtk = (int)(attacker.TransformationMinAttack + Kernel.RandGet((int)(attacker.TransformationMaxAttack - attacker.TransformationMinAttack))) * 80 / 100;
            }
            Damage = nAtk;
            if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))
                if (!attacker.Transformed)
                    Damage = (int)(Damage * 1.10);
            //Damage = (int)(Damage * attacker.StigmaIncrease);
            if (!CritImmune) Damage = CombatMath.Critical(attacker, null, Damage, Packet, false);
            Damage += attacker.PhysicalDamageIncrease;
            if (attacker.OnIntensify && Time32.Now >= attacker.IntensifyStamp.AddSeconds(4))
            {
                Damage *= 2;
                attacker.OnIntensify = false;
            }
            if (Kernel.Rate(5))
            {
                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    if (attacker.Owner.BlessTime > 0)
                    {
                        Damage *= 2;
                        _String str = new _String(true);
                        str.UID = attacker.UID;
                        str.TextsCount = 1;
                        str.Type = _String.Effect;
                        str.Texts.Add("LuckyGuy");
                        attacker.Owner.SendScreen(str, true);
                    }
                }
            }
            //if (attacker.EntityFlag == EntityFlag.Player)
            //{
            //    if (Kernel.Rate((float)attacker.Penetration / 100f))
            //    {
            //        Packet.Effect |= Attack.AttackEffects.Penetration;
            //        Damage = (Int32)Math.Floor((float)Damage * 1.1);
            //    }
            //}
            if (Damage <= 0)
                Damage = 1;
            if (attacker.MapID == 1707)
            {
                Damage = (int)attacked.Hitpoints + 1;
                return (uint)Damage;
            }
            return (uint)Damage;
        }
        public static uint Magic(Entity attacker, Entity attacked, Database.SpellInformation spell, ref Attack Packet, byte SpellLevelHu = 0)
        {
            if (spell == null) return 0;
            #region EquipmentEffect
            if (attacked.EntityFlag == EntityFlag.Player)
            {
                BlessEffect.Effect(attacked);
            }
            if (Kernel.RandGet(10) == 7)
                if (attacker.EntityFlag == EntityFlag.Player)
                    GemEffect.Effect(attacker);
            #endregion
            #region HideGui
            if (attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Player)
            {
                Data data1 = new Data(true);
                data1.UID = attacker.UID;
                data1.UID = attacked.UID;
                data1.ID = Data.HideGui;
                attacker.Owner.Send(data1);
                attacked.Owner.Send(data1);
            }
            #endregion
            #region MagicDefender
            if (Revora.Game.Features.MagicDefenderProtection.Blocks(attacker,attacked,Time32.Now)) return 0;
            #endregion
            #region Return 1
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex)) return 1;
            if (attacked.Name == "GoldenOctopus") return 1;

            #endregion
            uint rushHit;
            if (Revora.Game.Features.DailyWorldBosses.TryHit(attacker, attacked, out rushHit)
                || Revora.Game.Features.ExpeditionEvent.TryHit(attacker, attacked, out rushHit)
                || Revora.Game.Features.BossRushSystem.TryRushHit(attacker, attacked, out rushHit))
                return rushHit;
            #region Player -> Player \ Monster
            int nAtk = (int)Math.Round((double)((attacker.MagicAttack) + spell.Power));
            nAtk += (int)(nAtk * attacker.Gems[0] / 100);
            ushort nDef = attacked.MagicDefence;
            nAtk = (int)Math.Round(nAtk * 0.75);
            int Damage = CombatMath.MagicBase(attacker, attacked, (uint)spell.Power, Packet);
            if (attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Monster)
            {
                Damage = CalcDamageUser2Monster(nAtk, nDef, attacker.Level, attacked.Level, Damage);
                Damage = MulDiv(Damage, 10000, 10000);
                Damage = AdjustMinDamageUser2Monster(Damage, attacker, attacked);
            }
            else if (attacker.EntityFlag == EntityFlag.Monster && attacked.EntityFlag == EntityFlag.Player)
            {
                #region Resist
                if (attacker.Name == "SnowBanshee")
                {
                    if (Kernel.Rate((float)attacked.WaterResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WaterResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.WoodResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WoodResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                }
                if (attacker.Name == "SnowBansheeSoul" || attacker.Name == "PurpleBanshee")
                {
                    if (Kernel.Rate((float)attacked.WaterResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WaterResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.MetalResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.MetalResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.WoodResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WoodResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                }
                if (attacker.Name == "SwordMaster")
                {
                    if (Kernel.Rate((float)attacked.MetalResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.MetalResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.WoodResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WoodResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                }
                if (attacker.Name == "TeratoDragon")
                {
                    if (Kernel.Rate((float)attacked.FireResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.FireResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.WoodResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WoodResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                }
                if (attacker.Name == "ThrillingSpook" || attacker.Name == "ThrillingSpook2" || attacker.Name == "ThrillingSpook2")
                {
                    if (Kernel.Rate((float)attacked.EarthResistance / 100f))
                    {
                        Packet.Effect2 |= Attack.AttackEffects2.EarthResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.WoodResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WoodResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                }
                if (attacker.Name == "NemesisTyrant")
                {
                    if (Kernel.Rate((float)attacked.MetalResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.MetalResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.FireResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.FireResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.EarthResistance / 100f))
                    {
                        Packet.Effect2 |= Attack.AttackEffects2.EarthResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.WaterResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WaterResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.WoodResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WoodResist;
                        Damage = (Int32)Math.Floor((float)Damage * 1.1);
                    }
                }
                #endregion
                #region Stamina
                if (attacked.Action == Enums.ConquerAction.Sit)
                    if (attacked.EntityFlag == EntityFlag.Player)
                    {
                        if (attacked.Stamina >= 20)
                            attacked.Stamina -= 20;
                    }
                #endregion
                #region Spell Bounes
                if (spell != null)
                {
                    if (spell.PowerPercent != 0)
                    {
                        Damage = (int)(Damage * spell.PowerPercent);
                    }
                }
                #endregion
                #region Nobility Damge
                //if (attacker.NobilityRank != attacked.NobilityRank)
                //{
                //    int addnobility_damage = 0;
                //    if ((byte)attacker.NobilityRank == 12)
                //        addnobility_damage = (Damage * ((byte)attacker.NobilityRank + 45)) / 100;
                //    else
                //        if ((byte)attacker.NobilityRank > 5)
                //            addnobility_damage = (Damage * ((byte)attacker.NobilityRank + 25)) / 100;
                //    if ((byte)attacked.NobilityRank > 5)
                //    {
                //        if ((addnobility_damage - (uint)((Damage * ((byte)attacked.NobilityRank + 8)) / 100)) > 0)
                //        {
                //            addnobility_damage -= (int)((Damage * ((byte)attacked.NobilityRank + 8)) / 100);
                //        }
                //        else
                //        {
                //            if (Damage > (uint)((Damage * ((byte)attacked.NobilityRank + 8)) / 100))
                //            {
                //                Damage -= (int)((Damage * ((byte)attacked.NobilityRank + 8)) / 100);
                //            }
                //            else
                //                Damage = 1;
                //        }
                //    }
                //    if (Damage != 1)
                //    {
                //        Damage += addnobility_damage;
                //    }
                //}
                #endregion
            // Nobility contributes through Battle Power; no second rank-pair damage multiplier.
                #region AzureShield
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
                {
                    if (Damage > attacked.AzureShieldDefence)
                    {
                        Damage -= (int)attacked.AzureShieldDefence;
                        CreateAzureDmg(attacked.AzureShieldDefence, attacker, attacked);
                        //attacked.RemoveFlag2((ulong)FlagsUpdate.Flags.AzureShield);
                    }
                    else
                    {
                        CreateAzureDmg((uint)Damage, attacker, attacked);
                        attacked.AzureShieldDefence -= (ushort)Damage;
                        attacked.AzureShieldPacket();
                        Damage = 1;
                    }
                }
                #endregion
                #region IronShield
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.IronShield))
                {

                    if (Damage > attacked.IronShieldDefence)
                    {
                        Damage -= (int)attacked.IronShieldDefence;
                        CreateAzureDMG(attacked.IronShieldDefence, attacker, attacked);
                        attacked.RemoveFlag((ulong)FlagsUpdate.Flags.IronShield);
                    }
                    else
                    {
                        CreateAzureDMG((uint)Damage, attacker, attacked);
                        attacked.IronShieldDefence -= (uint)Damage;
                        attacked.IronShieldPacket();
                        Damage = 1;
                    }
                }
                #endregion
                #region Perfection
                //if (attacker.Perfection != null && attacked != null)
                //{
                //    var canDO = attacked.Perfection == null || attacked.Owner == null
                //        || attacked.Owner.Equipment == null ? true : attacker.Owner.Equipment.GetTPL() > attacked.Owner.Equipment.GetTPL();
                //    if (canDO)
                //    {
                //        if (attacker.ToxicFogPercent > 0 && Success(attacker.Perfection.ToxinEraserLevel))
                //        {
                //            attacker.ToxicFogPercent /= 2;
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.ToxinEraserLevel), true);
                //        }
                //        else if (Success(attacker.Perfection.LuckyStrike))
                //        {
                //            Damage *= 2;
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.LuckyStrike), true);
                //        }
                //        else if (Success(attacker.Perfection.LightOfStamina))
                //        {
                //            attacker.Stamina = 100;
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.LightOfStamina), true);
                //        }
                //        else if (Success(attacker.Perfection.AbsoluteLuck))
                //        {
                //            Damage *= 2;
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.AbsoluteLuck), true);
                //        }
                //        else if (Success(attacker.Perfection.BloodSpawn))
                //        {
                //            attacker.Hitpoints = attacker.MaxHitpoints;
                //            attacker.Mana = attacker.MaxMana;
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.BloodSpawn), true);
                //        }
                //        else if (Success(attacker.Perfection.CalmWind))
                //        {
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.CalmWind), true);
                //        }
                //        else if (Success(attacker.Perfection.CoreStrike))
                //        {
                //            Damage += ((int)(attacker.MagicDamageIncrease - attacked.Immunity) * 100);
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.CoreStrike), true);
                //        }
                //        else if (attacked.Perfection != null && Success(attacked.Perfection.DivineGuard))
                //        {
                //            Damage -= ((int)(attacker.Defence * 5) / 100);
                //            attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.DivineGuard), true);
                //        }
                //        else if (Success(attacker.Perfection.DrainingTouch))
                //        {
                //            if (Success(80))
                //            {
                //                attacker.Hitpoints = attacker.MaxHitpoints;
                //                attacker.Mana = attacker.MaxMana;
                //                attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.DrainingTouch), true);
                //            }
                //        }
                //        else if (attacked.Perfection != null && Success(attacked.Perfection.FreeSoul))
                //        {
                //            if (attacked.ShackleTime > 0)
                //            {
                //                attacked.ShackleTime = 0;
                //                attacked.RemoveFlag2((ulong)FlagsUpdate.Flags.SoulShackle);
                //                attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.FreeSoul), true);
                //            }
                //        }
                //        else if (Success(attacker.Perfection.InvisableArrow))
                //        {
                //            Damage += ((int)(attacker.MagicAttack * 5) / 100);
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.InvisbleArrow), true);
                //        }
                //        else if (Success(attacker.Perfection.KillingFlash))
                //        {
                //            attacker.Experience += attacker.Level >= 140 ? 0 : ((uint)(attacker.Experience * 5) / 100);
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.KillingFlash), true);
                //        }
                //        else if (attacked.Perfection != null && Success(attacked.Perfection.MirrorOfSin))
                //        {
                //            attacked.Experience += attacked.Level >= 140 ? 0 : ((uint)(attacked.Experience * 5) / 100);
                //            attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.MirrorOfSin), true);
                //        }
                //        else if (Success(attacker.Perfection.ShieldBreak))
                //        {
                //            Damage += (int)(attacked.Block * 100);
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.ShiledBreak), true);
                //        }
                //        else if (attacked.Perfection != null && Success(attacked.Perfection.StraightLife))
                //        {
                //            if (attacked.Dead)
                //            {
                //                attacked.BringToLife();
                //                attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.ShiledBreak), true);
                //            }
                //        }
                //    }
                //}
                #endregion

                #region DragonSwing [12200]
                if (Kernel.Rate(15))
                {
                    if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.DragonSwing))
                    {
                        _String str = new _String(true);
                        str.UID = attacked.UID;
                        str.TextsCount = 1;
                        str.Type = _String.Effect;
                        str.Texts.Add("poisonmiss");
                        if (attacked.EntityFlag == EntityFlag.Player)
                            attacked.Owner.SendScreen(str, true);
                        return 0;
                    }
                }
                #endregion
                Damage = CalcDamageMonster2User(nAtk, nDef, attacker.Level, attacked.Level, Damage);
                Damage = MulDiv(Damage, (attacked.Reborn != 0 && attacked.Class % 10 >= 3) ? 7000 : 10000, 10000);
                Damage = AdjustMinDamageMonster2User(Damage, attacker, attacked);
            }
            else
            {
                if (attacked.EntityFlag == EntityFlag.Monster)
                    Damage = MulDiv(Damage, 10000, 10000);
                else if (attacked.EntityFlag == EntityFlag.Player)
                    Damage = MulDiv(Damage, (attacked.Reborn != 0 && attacked.Class % 10 >= 3) ? 7000 : 10000, 10000);
            }
            #region LuckyGuy
            if (Kernel.Rate(5))
            {
                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    if (attacker.Owner.BlessTime > 0)
                    {
                        Damage *= 2;
                        _String str = new _String(true);
                        str.UID = attacker.UID;
                        str.TextsCount = 1;
                        str.Type = _String.Effect;
                        str.Texts.Add("LuckyGuy");
                        attacker.Owner.SendScreen(str, true);
                    }
                }
            }
            if (Kernel.Rate(5))
            {
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    if (attacked.Owner.BlessTime > 0)
                    {
                        Damage = 1;
                        _String str = new _String(true);
                        str.UID = attacker.UID;
                        str.TextsCount = 1;
                        str.Type = _String.Effect;
                        str.Texts.Add("LuckyGuy");
                        attacked.Owner.SendScreen(str, true);
                    }
                }
            }
            #endregion
            if (Damage <= 0)
                Damage = 1;

            Damage = CombatMath.Tortoise(attacked, Damage);
            Damage = CombatMath.Block(attacked, Damage, Packet);
            Damage = CombatMath.Critical(attacker, attacked, Damage, Packet, true);
            Damage += attacker.MagicDamageIncrease;

            if (Damage > attacked.MagicDamageDecrease)
                Damage -= attacked.MagicDamageDecrease;
            else
                Damage = 1;

            AutoRespone(attacker, attacked, ref Damage);
            Damage /= 4;
            if (SpellLevelHu != 0)
                Damage += (Damage * (SpellLevelHu * 15) / 100);
            Damage /= 3;
            Damage *= 3;
            return (uint)Damage;
            #endregion
        }
        public static uint Magic(Entity attacker, Entity attacked, ushort spellID, byte spellLevel, ref Attack Packet)
        {
            Database.SpellInformation spellInfo = Database.SpellTable.SpellInformations[spellID][spellLevel];
            return Magic(attacker, attacked, spellInfo, ref Packet);
        }
        public static uint Magic(Entity attacker, SobNpcSpawn attacked, Database.SpellInformation spell, ref Attack Packet)
        {
            if (Kernel.RandGet(10) == 7)
                if (attacker.EntityFlag == EntityFlag.Player)
                    GemEffect.Effect(attacker);
            bool CritImmune = false;
            if (!Kernel.Rate(spell.Percent)) return 0;
            if (spell != null)
                Durability(attacker, null, spell);
            if (attacker.Transformed) return 0;
            int Damage = (int)Math.Round((double)(attacker.MagicAttack + spell.Power));
            Damage += (ushort)attacker.MagicAttack;
            Damage += (int)(Damage * attacker.Gems[0] / 100);
            if (Kernel.Rate(5))
            {
                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    if (attacker.Owner.BlessTime > 0)
                    {
                        Damage *= 2;
                        _String str = new _String(true);
                        str.UID = attacker.UID;
                        str.TextsCount = 1;
                        str.Type = _String.Effect;
                        str.Texts.Add("LuckyGuy");
                        attacker.Owner.SendScreen(str, true);
                    }
                }
            }
            if (!CritImmune) Damage = CombatMath.Critical(attacker, null, Damage, Packet, true);
            //if (attacker.EntityFlag == EntityFlag.Player)
            //{
            //    if (Kernel.Rate((float)attacker.Penetration / 100f))
            //    {
            //        Packet.Effect |= Attack.AttackEffects.Penetration;
            //        Damage = (Int32)Math.Floor((float)Damage * 0.1);
            //    }
            //}
            Damage += attacker.MagicDamageIncrease;

            if (Damage <= 0)
                Damage = 1;
            return (uint)Damage;
        }
        public static uint Magico(Entity attacker, Entity attacked, Database.SpellInformation spell, ref Attack Packet, byte SpellLevelHu = 0)
        {
            if (spell == null) return 0;
            #region EquipmentEffect
            if (attacked.EntityFlag == EntityFlag.Player)
            {
                BlessEffect.Effect(attacked);
            }
            if (Kernel.RandGet(10) == 7)
                if (attacker.EntityFlag == EntityFlag.Player)
                    GemEffect.Effect(attacker);
            #endregion
            #region HideGui
            if (attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Player)
            {
                Data data1 = new Data(true);
                data1.UID = attacker.UID;
                data1.UID = attacked.UID;
                data1.ID = Data.HideGui;
                attacker.Owner.Send(data1);
                attacked.Owner.Send(data1);
            }
            #endregion
            #region MagicDefender
            if (Revora.Game.Features.MagicDefenderProtection.Blocks(attacker,attacked,Time32.Now)) return 0;
            #endregion
            #region Return 1
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex)) return 1;
            if (attacked.Name == "GoldenOctopus") return 1;

            #endregion
            uint rushHit;
            if (Revora.Game.Features.DailyWorldBosses.TryHit(attacker, attacked, out rushHit)
                || Revora.Game.Features.ExpeditionEvent.TryHit(attacker, attacked, out rushHit)
                || Revora.Game.Features.BossRushSystem.TryRushHit(attacker, attacked, out rushHit))
                return rushHit;
            #region Player -> Player \ Monster
            int nAtk = (int)Math.Round((double)((attacker.MagicAttack) + spell.Power));
            nAtk += (int)(nAtk * attacker.Gems[0] / 100);
            ushort nDef = attacked.MagicDefence;
            nAtk = (int)Math.Round(nAtk * 0.75);
            int damage = CombatMath.MagicBase(attacker, attacked, (uint)spell.Power, Packet);
            if (attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Monster)
            {
                damage = CalcDamageUser2Monster(nAtk, nDef, attacker.Level, attacked.Level, damage);
                damage = MulDiv(damage, 10000, 10000);
                damage = AdjustMinDamageUser2Monster(damage, attacker, attacked);
            }
            else if (attacker.EntityFlag == EntityFlag.Monster && attacked.EntityFlag == EntityFlag.Player)
            {
                #region Resist
                if (attacker.Name == "SnowBanshee")
                {
                    if (Kernel.Rate((float)attacked.WaterResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WaterResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.WoodResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WoodResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                }
                if (attacker.Name == "SnowBansheeSoul" || attacker.Name == "PurpleBanshee")
                {
                    if (Kernel.Rate((float)attacked.WaterResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WaterResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.MetalResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.MetalResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.WoodResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WoodResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                }
                if (attacker.Name == "SwordMaster")
                {
                    if (Kernel.Rate((float)attacked.MetalResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.MetalResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.WoodResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WoodResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                }
                if (attacker.Name == "TeratoDragon")
                {
                    if (Kernel.Rate((float)attacked.FireResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.FireResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.WoodResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WoodResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                }
                if (attacker.Name == "ThrillingSpook" || attacker.Name == "ThrillingSpook2" || attacker.Name == "ThrillingSpook2")
                {
                    if (Kernel.Rate((float)attacked.EarthResistance / 100f))
                    {
                        Packet.Effect2 |= Attack.AttackEffects2.EarthResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.WoodResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WoodResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                }
                if (attacker.Name == "NemesisTyrant")
                {
                    if (Kernel.Rate((float)attacked.MetalResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.MetalResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.FireResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.FireResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.EarthResistance / 100f))
                    {
                        Packet.Effect2 |= Attack.AttackEffects2.EarthResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.WaterResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WaterResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                    else if (Kernel.Rate((float)attacked.WoodResistance / 100f))
                    {
                        Packet.Effect |= Attack.AttackEffects.WoodResist;
                        damage = (Int32)Math.Floor((float)damage * 1.1);
                    }
                }
                #endregion
                #region Stamina
                if (attacked.Action == Enums.ConquerAction.Sit)
                    if (attacked.EntityFlag == EntityFlag.Player)
                    {
                        if (attacked.Stamina >= 20)
                            attacked.Stamina -= 20;
                    }
                #endregion
                #region Spell Bounes
                //if (spell != null)
                //{
                //    if (spell.PowerPercent != 0)
                //    {
                //        damage = (int)(damage * spell.PowerPercent);
                //    }
                //}
                #endregion


                #region AzureShield
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
                {
                    if (damage > attacked.AzureShieldDefence)
                    {
                        damage -= (int)attacked.AzureShieldDefence;
                        CreateAzureDmg(attacked.AzureShieldDefence, attacker, attacked);
                        //attacked.RemoveFlag2((ulong)FlagsUpdate.Flags.AzureShield);
                    }
                    else
                    {
                        CreateAzureDmg((uint)damage, attacker, attacked);
                        attacked.AzureShieldDefence -= (ushort)damage;
                        attacked.AzureShieldPacket();
                        damage = 1;
                    }
                }
                #endregion
                #region IronShield
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.IronShield))
                {

                    if (damage > attacked.IronShieldDefence)
                    {
                       damage -= (int)attacked.IronShieldDefence;
                        CreateAzureDMG(attacked.IronShieldDefence, attacker, attacked);
                        attacked.RemoveFlag((ulong)FlagsUpdate.Flags.IronShield);
                    }
                    else
                    {
                        CreateAzureDMG((uint)damage, attacker, attacked);
                        attacked.IronShieldDefence -= (uint)damage;
                        attacked.IronShieldPacket();
                        damage = 1;
                    }
                }
                #endregion
                #region Perfection
                //if (attacker.Perfection != null && attacked != null)
                //{
                //    var canDO = attacked.Perfection == null || attacked.Owner == null
                //        || attacked.Owner.Equipment == null ? true : attacker.Owner.Equipment.GetTPL() > attacked.Owner.Equipment.GetTPL();
                //    if (canDO)
                //    {
                //        if (attacker.ToxicFogPercent > 0 && Success(attacker.Perfection.ToxinEraserLevel))
                //        {
                //            attacker.ToxicFogPercent /= 2;
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.ToxinEraserLevel), true);
                //        }
                //        else if (Success(attacker.Perfection.LuckyStrike))
                //        {
                //            damage *= 2;
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.LuckyStrike), true);
                //        }
                //        else if (Success(attacker.Perfection.LightOfStamina))
                //        {
                //            attacker.Stamina = 100;
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.LightOfStamina), true);
                //        }
                //        else if (Success(attacker.Perfection.AbsoluteLuck))
                //        {
                //            damage *= 2;
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.AbsoluteLuck), true);
                //        }
                //        else if (Success(attacker.Perfection.BloodSpawn))
                //        {
                //            attacker.Hitpoints = attacker.MaxHitpoints;
                //            attacker.Mana = attacker.MaxMana;
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.BloodSpawn), true);
                //        }
                //        else if (Success(attacker.Perfection.CalmWind))
                //        {
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.CalmWind), true);
                //        }
                //        else if (Success(attacker.Perfection.CoreStrike))
                //        {
                //            damage += ((int)(attacker.MagicdamageIncrease - attacked.Immunity) * 100);
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.CoreStrike), true);
                //        }
                //        else if (attacked.Perfection != null && Success(attacked.Perfection.DivineGuard))
                //        {
                //            damage -= ((int)(attacker.Defence * 5) / 100);
                //            attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.DivineGuard), true);
                //        }
                //        else if (Success(attacker.Perfection.DrainingTouch))
                //        {
                //            if (Success(80))
                //            {
                //                attacker.Hitpoints = attacker.MaxHitpoints;
                //                attacker.Mana = attacker.MaxMana;
                //                attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.DrainingTouch), true);
                //            }
                //        }
                //        else if (attacked.Perfection != null && Success(attacked.Perfection.FreeSoul))
                //        {
                //            if (attacked.ShackleTime > 0)
                //            {
                //                attacked.ShackleTime = 0;
                //                attacked.RemoveFlag2((ulong)FlagsUpdate.Flags.SoulShackle);
                //                attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.FreeSoul), true);
                //            }
                //        }
                //        else if (Success(attacker.Perfection.InvisableArrow))
                //        {
                //            damage += ((int)(attacker.MagicAttack * 5) / 100);
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.InvisbleArrow), true);
                //        }
                //        else if (Success(attacker.Perfection.KillingFlash))
                //        {
                //            attacker.Experience += attacker.Level >= 140 ? 0 : ((uint)(attacker.Experience * 5) / 100);
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.KillingFlash), true);
                //        }
                //        else if (attacked.Perfection != null && Success(attacked.Perfection.MirrorOfSin))
                //        {
                //            attacked.Experience += attacked.Level >= 140 ? 0 : ((uint)(attacked.Experience * 5) / 100);
                //            attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.MirrorOfSin), true);
                //        }
                //        else if (Success(attacker.Perfection.ShieldBreak))
                //        {
                //            damage += (int)(attacked.Block * 100);
                //            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.ShiledBreak), true);
                //        }
                //        else if (attacked.Perfection != null && Success(attacked.Perfection.StraightLife))
                //        {
                //            if (attacked.Dead)
                //            {
                //                attacked.BringToLife();
                //                attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.ShiledBreak), true);
                //            }
                //        }
                //    }
                //}
                #endregion

                #region DragonSwing [12200]
                if (Kernel.Rate(15))
                {
                    if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.DragonSwing))
                    {
                        _String str = new _String(true);
                        str.UID = attacked.UID;
                        str.TextsCount = 1;
                        str.Type = _String.Effect;
                        str.Texts.Add("poisonmiss");
                        if (attacked.EntityFlag == EntityFlag.Player)
                            attacked.Owner.SendScreen(str, true);
                        return 0;
                    }
                }
                #endregion
                damage = CalcDamageMonster2User(nAtk, nDef, attacker.Level, attacked.Level, damage);
                damage = MulDiv(damage, (attacked.Reborn != 0 && attacked.Class % 10 >= 3) ? 7000 : 10000, 10000);
                damage = AdjustMinDamageMonster2User(damage, attacker, attacked);
            }
            else
            {
                if (attacked.EntityFlag == EntityFlag.Monster)
                    damage = MulDiv(damage, 10000, 10000);
                else if (attacked.EntityFlag == EntityFlag.Player)
                    damage = MulDiv(damage, (attacked.Reborn != 0 && attacked.Class % 10 >= 3) ? 7000 : 10000, 10000);
            }
            #region Fire attack attribute
            if (attacker.EntityFlag == EntityFlag.Player)
            {

            }
            if (attacked.BlockShieldCheck)
            {
                if (MyMath.Success(attacked.BlockShield))
                {
                    //     damage = damage / 1;
                    //   Packet.Effect |= Attack.AttackEffects.Block;
                }
            }
            if (attacked.EntityFlag == EntityFlag.Player)
            {
                if (CombatMath.Roll(attacked.Block / 100.0))
                {
                    if (attacked.Block > 0)
                    {
                        Packet.Effect |= Attack.AttackEffects.Block;
                        damage = (Int32)Math.Floor((float)damage / 2);
                    }
                }
            }
            #endregion
            #region LuckyGuy
            if (Kernel.Rate(5))
            {
                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    if (attacker.Owner.BlessTime > 0)
                    {
                        damage *= 2;
                        _String str = new _String(true);
                        str.UID = attacker.UID;
                        str.TextsCount = 1;
                        str.Type = _String.Effect;
                        str.Texts.Add("LuckyGuy");
                        attacker.Owner.SendScreen(str, true);
                    }
                }
            }
            if (Kernel.Rate(5))
            {
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    if (attacked.Owner.BlessTime > 0)
                    {
                        damage = 1;
                        _String str = new _String(true);
                        str.UID = attacker.UID;
                        str.TextsCount = 1;
                        str.Type = _String.Effect;
                        str.Texts.Add("LuckyGuy");
                        attacked.Owner.SendScreen(str, true);
                    }
                }
            }
            #endregion
            if (damage <= 0)
                damage = 1;

            damage = CombatMath.Tortoise(attacked, damage);
            damage = CombatMath.Critical(attacker, attacked, damage, Packet, true);
            damage += attacker.MagicDamageIncrease;

            if (damage > attacked.MagicDamageDecrease)
                damage -= attacked.MagicDamageDecrease;
            else
                damage = 1;

            AutoRespone(attacker, attacked, ref damage);
            damage /= 4;
            if (SpellLevelHu != 0)
                damage += (damage * (SpellLevelHu * 15) / 100);
            damage /= 3;
            damage *= 3;
            return (uint)damage;
            #endregion
        }

        #region Archers
        public static void CheckDamage(Entity attacker, Entity attacked, ref int Damage, ref Attack Packet, bool magic = false)
        {

            if (attacker.EntityFlag == EntityFlag.Player)
            {
                BlessEffect.Effect(attacker);
                GemEffect.Effect(attacker);
            }
            Calculate.AutoRespone(attacker, attacked, ref Damage);
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
            {

                if (Damage > attacked.AzureShieldDefence)
                {
                    Damage -= (int)attacked.AzureShieldDefence;
                    CreateAzureDMG(attacked.AzureShieldDefence, attacker, attacked);
                    //attacked.RemoveFlag2((ulong)FlagsUpdate.Flags.AzureShield);
                }
                else
                {
                    CreateAzureDMG((uint)Damage, attacker, attacked);
                    attacked.AzureShieldDefence -= (ushort)Damage;
                    attacked.AzureShieldPacket();
                    Damage = 1;
                }
            }
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.IronShield))
            {

                if (Damage > attacked.IronShieldDefence)
                {
                    Damage -= (int)attacked.IronShieldDefence;
                    CreateAzureDMG(attacked.IronShieldDefence, attacker, attacked);
                    attacked.RemoveFlag((ulong)FlagsUpdate.Flags.IronShield);
                }
                else
                {
                    CreateAzureDMG((uint)Damage, attacker, attacked);
                    attacked.IronShieldDefence -= (uint)Damage;
                    attacked.IronShieldPacket();
                    Damage = 1;
                }
            }
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex))
                Damage = 1;
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex)) Damage = 1;
        }
        //internal static uint Ranged2(Entity attacker, Entity attacked, ref Attack attack, byte p = 0)
        //{
        //    int Damage = 0;

        //    if (attacked.EntityFlag == EntityFlag.Player)
        //        Damage = GetDamageEntity2Entity(attacker, attacked, Attack.Ranged, ref attack);

        //    CheckDamage(attacker, attacked, ref Damage, ref attack, false);
        //    return (uint)Damage;
        //}
        public static bool RateStatus(byte value)
        {
            return (Kernel.Random.Next() % 100) < value;
        }
        public static Int32 GetDamageEntity2Entity(Entity Attacker, Entity Target, uint AtkType, ref Attack Packet)
        {
            Double Damage = 0;

            Double Reborn = 1.00;
            if (Target.Reborn == 1)
                Reborn -= 0.30; //30%
            else if (Target.Reborn >= 2)
                Reborn -= 0.50; //50%

            Double Dodge = 1.00;
            Dodge -= (Double)Target.Dodge / 1000;
            Dodge += (Double)Target.Weight / 100;

            switch (AtkType)
            {
                case Attack.Ranged:
                    {
                        Damage = MyMath.Generate(Attacker.MinAttack, Attacker.MaxAttack);
                        Game.Attacking.Calculate.Refinary1(Attacker, Target, ref Damage, ref Packet, false);
                        var bonus = Attacker.Gems[GemTypes.Dragon];
                        if (bonus > 0)
                            Damage += MathHelper.MulDiv((int)Damage, bonus, 100);

                        Damage = Attacker.AdjustAttack((int)Damage);


                        Damage -= Target.AdjustBowDefense(Target.Defence);
                        Damage *= Dodge;
                        break;
                    }
            }

            if (AtkType != Attack.Magic)
            {
                if (Attacker.Gems[GemTypes.Dragon] >= 210)
                    Damage += Damage * 50 / 100;
            }

            Damage *= Reborn;
            Damage *= Target.ItemBless;
            double torist = (double)(Target.Gems[GemTypes.Tortoise] / 100d);
            torist = (double)(1 - torist);
            torist = Math.Max(torist, 0.5);
            Damage *= torist;

            Double BattlePower = Attacker.BattlePower - Target.BattlePower;
            BattlePower = Math.Pow(2, BattlePower / 12.0);
            BattlePower = Math.Min(BattlePower, 100.0);
            bool bypass = false;
            if (!bypass)
                Damage *= BattlePower;

            Damage += Attacker.getFan(AtkType == Attack.Magic);
            if (Target.EntityFlag == EntityFlag.Player)
                Damage -= Target.getTower(AtkType == Attack.Magic);
            if (Damage < 1)
                Damage = 1;



            if (Attacker.EntityFlag == EntityFlag.Player)
            {
                if (Attacker.Owner.BlessTime > 0 && MyMath.Success(5))
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
                if (Target.Owner.BlessTime > 0 && MyMath.Success(5))
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
        public static void Refinary1(Entity attacker, Entity attacked, ref double Damage, ref Attack Packet, bool magic = false)
        {
            Damage = CombatMath.Critical(attacker, attacked, Damage, Packet, magic);
            if (attacked.EntityFlag == EntityFlag.Player && CombatMath.Roll(attacked.Block / 100.0))
            {
                Damage /= 2;
                if (Packet != null) Packet.Effect |= Attack.AttackEffects.Block;
            }
        }
        #endregion
        public class StatusConstants
        {
            public const int AdjustSet = -30000,
                             AdjustFull = -32768,
                             AdjustPercent = 30000;
        }
        public static uint Ranged(Entity attacker, Entity attacked, ref Attack Packet)
        {
            Database.SpellInformation spell = null;
            return Melee(attacker, attacked, spell, ref Packet);
        }
        public static uint Ranged(Entity attacker, Entity attacked, Database.SpellInformation spell, ref Attack Packet)
        {

            if (attacker.EntityFlag == EntityFlag.Player)
            {
                BlessEffect.Effect(attacker);
            }
            if (Kernel.RandGet(10) == 7)
                if (attacker.EntityFlag == EntityFlag.Player)
                    GemEffect.Effect(attacker);
            if (attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Player)
            {
                Data data = new Data(true);
                data.UID = attacker.UID;
                data.UID = attacked.UID;
                data.ID = Data.HideGui;
                attacker.Owner.Send(data);
                attacked.Owner.Send(data);
            }
            int Damage = 0;
            Durability(attacker, attacked, null);
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.ShurikenVortex)) return 1;
            if (attacked.Name == "GoldenOctopus") return 1;

            if (attacker.Transformed) return 0;
            #region Breakthrough
            //int rateBreakthrough = (attacker.Breaktrough - attacked.Counteraction) / 100;
            //if (attacker.BattlePower < attacked.BattlePower)
            //{
            //    if (Kernel.Rate(rateBreakthrough))
            //    {
            //        Damage = (int)attacker.MaxAttack + 1;
            //        attacker.Owner.Send(new Message("Smash grants a chance to exceed maximum attack when the foe has high BattlePower.", Message.TopLeft));
            //    }
            //}
            #endregion
            Damage = Kernel.Random.Next(Math.Min((int)attacker.MinAttack, (int)attacker.MaxAttack), Math.Max((int)attacker.MinAttack, (int)attacker.MaxAttack) + 1);

            if (attacker.OnSuperman())
                if (attacked.EntityFlag == EntityFlag.Monster)
                    if (!attacked.MonsterInfo.Boss)
                        Damage *= 2;
            if (attacker.OnSuperman())
                if (attacked.EntityFlag == EntityFlag.Monster)
                    Damage *= 2;
            if (attacker.OnFatalStrike())
                if (attacked.EntityFlag == EntityFlag.Monster)
                    if (!attacked.MonsterInfo.Boss)
                        Damage *= 5;
                    else Damage += (int)(Damage * .1);

            if (!attacked.Transformed)
                Damage -= Revora.Game.Features.WarriorStance.Defence(attacked,attacked.Defence,Time32.Now);
            else
                Damage -= attacked.TransformationDefence;
            Damage -= Damage * attacked.ItemBless / 100;
            byte dodge = attacked.Dodge;
            if (dodge > 100)
                dodge = 99;
            if (!attacked.Transformed)
                Damage -= Damage * dodge / 100;
            else
                Damage -= Damage * attacked.TransformationDodge / 100;

            if (attacker.OnIntensify && Time32.Now >= attacker.IntensifyStamp.AddSeconds(4))
            {
                Damage *= 2;
                attacker.OnIntensify = false;
            }
            if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))
                if (!attacker.Transformed)
                    Damage = (int)(Damage * 1.10);
            //Damage = (int)(Damage * attacker.StigmaIncrease);
            if (attacked.EntityFlag == EntityFlag.Monster)
            {
                if (attacked.MapID < 1351 || attacked.MapID > 1354)
                    Damage = (int)(Damage * (1 + (GetLevelBonus(attacker.Level, attacked.Level) * 0.08)));

                if (attacked.MapID < 1351 || attacked.MapID > 1354)
                    Damage = (int)(Damage * AttackMultiplier(attacker, attacked));
            }
            Damage = CombatMath.BattlePower(attacker, attacked, Damage, Packet, false);
            if (attacker.EntityFlag == EntityFlag.Player && attacked.EntityFlag == EntityFlag.Player)
            {
                Damage = AdjustRebirthDamage(attacked.Owner, attacker.Owner, Damage);
            }
            if (Kernel.Rate(5))
            {
                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    if (attacker.Owner.BlessTime > 0)
                    {
                        Damage *= 2;
                        _String str = new _String(true);
                        str.UID = attacker.UID;
                        str.TextsCount = 1;
                        str.Type = _String.Effect;
                        str.Texts.Add("LuckyGuy");
                        attacker.Owner.SendScreen(str, true);
                    }
                }
            }
            if (Kernel.Rate(5))
            {
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    if (attacked.Owner.BlessTime > 0)
                    {
                        Damage = 1;
                        _String str = new _String(true);
                        str.UID = attacker.UID;
                        str.TextsCount = 1;
                        str.Type = _String.Effect;
                        str.Texts.Add("LuckyGuy");
                        attacked.Owner.SendScreen(str, true);
                    }
                }
            }
            if (attacked.EntityFlag == EntityFlag.Monster)
            {
                if (Damage >= 700 * attacked.MaxHitpoints)
                    Damage = (int)(700 * attacked.MaxHitpoints);
            }
            if (attacked.EntityFlag == EntityFlag.Player)
            {
                if (CombatMath.Roll(attacked.Block / 100.0))
                {
                    if (attacked.Block > 0)
                    {
                        Packet.Effect |= Attack.AttackEffects.Block;
                        Damage = (Int32)Math.Floor((float)Damage / 2);
                    }
                }
            }
            if (spell != null && spell.PowerPercent != 0)
                Damage = (int)(Damage * spell.PowerPercent);

            Damage = CombatMath.Tortoise(attacked, Damage);
            Damage = CombatMath.Critical(attacker, attacked, Damage, Packet, false);
            Damage += attacker.PhysicalDamageIncrease;
            Damage -= attacked.PhysicalDamageDecrease;

            if (Damage <= 0)
                Damage = 1;
            #region Nobility Damge
            //if (attacker.NobilityRank != attacked.NobilityRank)
            //{
            //    int addnobility_damage = 0;
            //    if ((byte)attacker.NobilityRank == 12)
            //        addnobility_damage = (Damage * ((byte)attacker.NobilityRank + 45)) / 100;
            //    else
            //        if ((byte)attacker.NobilityRank > 5)
            //            addnobility_damage = (Damage * ((byte)attacker.NobilityRank + 25)) / 100;
            //    if ((byte)attacked.NobilityRank > 5)
            //    {
            //        if ((addnobility_damage - (uint)((Damage * ((byte)attacked.NobilityRank + 8)) / 100)) > 0)
            //        {
            //            addnobility_damage -= (int)((Damage * ((byte)attacked.NobilityRank + 8)) / 100);
            //        }
            //        else
            //        {
            //            if (Damage > (uint)((Damage * ((byte)attacked.NobilityRank + 8)) / 100))
            //            {
            //                Damage -= (int)((Damage * ((byte)attacked.NobilityRank + 8)) / 100);
            //            }
            //            else
            //                Damage = 1;
            //        }
            //    }
            //    if (Damage != 1)
            //    {
            //        Damage += addnobility_damage;
            //    }

            //}
            #endregion
            // Nobility contributes through Battle Power; no second rank-pair damage multiplier.
            #region AzureShield
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
            {

                if (Damage > attacked.AzureShieldDefence)
                {
                    Damage -= (int)attacked.AzureShieldDefence;
                    CreateAzureDmg(attacked.AzureShieldDefence, attacker, attacked);
                    //attacked.RemoveFlag2((ulong)FlagsUpdate.Flags.AzureShield);
                }
                else
                {
                    CreateAzureDmg((uint)Damage, attacker, attacked);
                    attacked.AzureShieldDefence -= (ushort)Damage;
                    attacked.AzureShieldPacket();
                    Damage = 1;
                }
            }
            #endregion
            #region IronShield
            if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.IronShield))
            {

                if (Damage > attacked.IronShieldDefence)
                {
                    Damage -= (int)attacked.IronShieldDefence;
                    CreateAzureDMG(attacked.IronShieldDefence, attacker, attacked);
                    attacked.RemoveFlag((ulong)FlagsUpdate.Flags.IronShield);
                }
                else
                {
                    CreateAzureDMG((uint)Damage, attacker, attacked);
                    attacked.IronShieldDefence -= (uint)Damage;
                    attacked.IronShieldPacket();
                    Damage = 1;
                }
            }
            #endregion
            #region DragonSwing [12200]
            if (Kernel.Rate(20))
            {
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.DragonSwing))
                {
                    _String str = new _String(true);
                    str.UID = attacked.UID;
                    str.TextsCount = 1;
                    str.Type = _String.Effect;
                    str.Texts.Add("poisonmiss");
                    if (attacked.EntityFlag == EntityFlag.Player)
                        attacked.Owner.SendScreen(str, true);
                    return 0;
                }
            }
            #endregion
            AutoRespone(attacker, attacked, ref Damage);
            return (uint)Damage;
        }
        public static uint Ranged(Entity attacker, SobNpcSpawn attacked, ref Attack Packet)
        {
            int Damage = 0;
            Boolean CritImmune = false;
            Durability(attacker, null, null);
            if (attacker.Transformed) return 0;
            Damage = Kernel.Random.Next(Math.Min((int)attacker.MinAttack, (int)attacker.MaxAttack), Math.Max((int)attacker.MinAttack, (int)attacker.MaxAttack) + 1);
            if (attacker.OnIntensify && Time32.Now >= attacker.IntensifyStamp.AddSeconds(4))
            {
                Damage *= 2;

                attacker.OnIntensify = false;
            }
            if (Kernel.Rate(5))
            {
                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    if (attacker.Owner.BlessTime > 0)
                    {
                        Damage *= 2;
                        _String str = new _String(true);
                        str.UID = attacker.UID;
                        str.TextsCount = 1;
                        str.Type = _String.Effect;
                        str.Texts.Add("LuckyGuy");
                        attacker.Owner.SendScreen(str, true);
                    }
                }
            }
            if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))
                if (!attacker.Transformed)
                    Damage = (int)(Damage * 1.10);
            //Damage = (int)(Damage * attacker.StigmaIncrease);
            if (!CritImmune) Damage = CombatMath.Critical(attacker, null, Damage, Packet, false);
            //if (attacker.EntityFlag == EntityFlag.Player)
            //{
            //    if (Kernel.Rate((float)attacker.Penetration / 100f))
            //    {
            //        Packet.Effect |= Attack.AttackEffects.Penetration;
            //        Damage = (Int32)Math.Floor((float)Damage * 1.1);
            //    }
            //}
            Damage += attacker.PhysicalDamageIncrease;
            if (Damage <= 0)
                Damage = 1;
            return (uint)Damage;
        }

        public static int BattlePowerCalculationMelee(int damage, int subbattlepower)
        {
            if (subbattlepower <= 0)
                return damage;
            int ReduceDamage = 100 - (20 + (subbattlepower * 10));
            if (subbattlepower < 5)
                return (int)((long)damage * ReduceDamage / 100);
            else if (subbattlepower >= 5)
                return (int)((long)damage * 30 / 100);
            return damage;
        }
        public static int BattlePowerCalculationMagic(int damage, int subbattlepower)
        {
            if (subbattlepower <= 0)
                return damage;
            int ReduceDamage = 100 - ((subbattlepower * 10));
            if (subbattlepower < 5)
                return (int)((long)damage * ReduceDamage / 100);
            else if (subbattlepower >= 5)
                return (int)((long)damage * 50 / 100);
            return damage;
        }

        public static int GetLevelBonus(int l1, int l2)
        {
            int num = l1 - l2;
            int bonus = 0;
            if (num >= 3)
            {
                num -= 3;
                bonus = 1 + (num / 5);
            }
            return bonus;
        }

        public static uint Percent(int target, float percent)
        {
            return (uint)(target * percent);
        }
        public static uint Percent(Entity attacked, float percent)
        {
            return (uint)(attacked.Hitpoints * percent);
        }
        public static uint Percent(SobNpcSpawn attacked, float percent)
        {
            return (uint)(attacked.Hitpoints * percent);
        }

        private static double AttackMultiplier(Entity attacker, Entity attacked)
        {
            if (attacked.Level > attacker.Level) return 1;
            return ((double)(attacker.Level - attacked.Level)) / 10 + 1;
        }
        // shield
        private static int AdjustDefence(Entity attacker, Entity attacked, int nDef)
        {
            if (!attacked.Transformed)
            {
                if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.MagicShield))
                {
                    if (attacked.ShieldTime > 0) //>
                        nDef = (ushort)(attacked.Defence * attacked.ShieldIncrease);
                    else
                        nDef = (ushort)(attacked.Defence * attacked.MagicShieldIncrease);
                }
            }
            return Revora.Game.Features.WarriorStance.Defence(attacked,nDef,Time32.Now);
        }

        public static void CreateAzureDmg(uint dmg, Entity attacker, Entity attacked)
        {
            Attack attac = new Attack(true);
            attac.Attacker = attacker.UID;
            attac.Attacked = attacked.UID;
            attac.X = attacked.X;
            attac.Y = attacked.Y;
            attac.AttackType = Attack.BlueDamage;
            attac.Damage = dmg;
            attacked.Owner.SendScreen(attac, true);
        }
        internal static Func<int,bool> ResponseChance;
        private static bool ResponseRoll(int chance){var roll=ResponseChance;return roll==null?Kernel.Rate(chance):roll(chance);}
        private static void AutoRespone(Entity attacker, Entity attacked, ref int Damage)
        {
            using(var guild=Revora.Game.Features.GuildPkBanner.PlayerCombatLease.Acquire(attacker,attacked)) {
            if(guild!=null && !guild.Allowed){Damage=0;return;}
            try
            {
                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    if (attacker.Owner.Spells.ContainsKey(11120))
                    {
                        var s = attacker.Owner.Spells[11120];
                        var spell = Database.SpellTable.SpellInformations[s.ID][s.Level];
                        if (spell != null)
                        {
                            if (Kernel.Rate(spell.Percent))
                            {
                                var ent = attacked as Entity;
                                if (!ent.IsBlackSpotted)
                                {
                                    ent.IsBlackSpotted = true;
                                    ent.BlackSpotStamp = Time32.Now;
                                    ent.BlackSpotStepSecs = spell.Duration;
                                    Kernel.BlackSpoted.TryAdd(ent.UID, ent);
                                    BlackSpotPacket bsp = new BlackSpotPacket();
                                    foreach (var h in Server.GamePool)
                                    {
                                        h.Send(bsp.ToArray(true, ent.UID));
                                    }
                                }
                            }
                        }
                    }
                }
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    if (attacked.Owner.Spells.ContainsKey(11130) && attacked.Owner.Entity.IsEagleEyeShooted)
                    {
                        var s = attacked.Owner.Spells[11130];
                        var spell = Database.SpellTable.SpellInformations[s.ID][s.Level];
                        if (spell != null)
                        {
                            if (Kernel.Rate(spell.Percent))
                            {
                                attacked.Owner.Entity.IsEagleEyeShooted = false;
                                SpellUse ssuse = new SpellUse(true);
                                ssuse.Attacker = attacked.UID;
                                ssuse.SpellID = spell.ID;
                                ssuse.SpellLevel = spell.Level;
                                ssuse.AddTarget(attacked.Owner.Entity.UID, new SpellUse.DamageClass().Damage = 11030, null);
                                if (attacked.EntityFlag == EntityFlag.Player)
                                {
                                    attacked.Owner.SendScreen(ssuse, true);
                                }
                            }
                        }

                    }
                    else if (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.RevengeTaill) && (attacked.ContainsFlag((ulong)FlagsUpdate.Flags.RevengeTaill) && attacked.ContainsFlag((ulong)FlagsUpdate.Flags.RevengeTaill) && (ResponseRoll(10))))
                    {
                        if (Damage <= 22000)
                        {
                            attacked.RemoveFlag((ulong)FlagsUpdate.Flags.RevengeTaill);
                            var spell = Database.SpellTable.GetSpell(13030, attacked.Owner);
                            if (spell != null)
                            {

                                uint damage = (uint)(Damage);
                                Attack attack = new Attack(true);
                                attack.Attacked = attacked.UID;
                                attack.Attacker = attacker.UID;
                                attack.AttackType = 57;
                                attack.AttackType = Network.GamePackets.Attack.Reflect;
                                attack.Damage = damage;
                                attack.ResponseDamage = damage;
                                attack.X = attacked.X;
                                attack.Y = attacked.Y;

                                if (attacker.Hitpoints <= damage)
                                {
                                    if (attacker.EntityFlag == EntityFlag.Player)
                                    {
                                        attacked.Owner.UpdateQualifier(attacked.Owner, attacker.Owner, attacker.Hitpoints);

                                        attacker.Owner.SendScreen(attack, true);
                                        attacked.AttackPacket = null;
                                    }
                                    else
                                    {
                                        attacker.MonsterInfo.SendScreen(attack);
                                    }
                                    attacker.Die(attacked);
                                }
                                else
                                {
                                    attacker.Hitpoints -= damage;
                                    if (attacker.EntityFlag == EntityFlag.Player)
                                    {
                                        attacked.Owner.UpdateQualifier(attacked.Owner, attacker.Owner, damage);
                                        attacker.Owner.SendScreen(attack, true);
                                    }
                                    else
                                    {
                                        attacker.MonsterInfo.SendScreen(attack);
                                    }
                                }
                                Damage = 0;
                            }
                        }
                    }
                    if (attacked.CounterKillSwitch && ResponseRoll(30) && !attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Fly) && Time32.Now > attacked.CounterKillStamp.AddSeconds(15))
                    {
                        attacked.CounterKillStamp = Time32.Now;
                        Network.GamePackets.Attack attack = new Network.GamePackets.Attack(true);
                        attack.Effect = Attack.AttackEffects.None;
                        uint damage = Melee(attacked, attacker, ref attack);
                        damage = damage / 3;
                        attack.Attacked = attacker.UID;
                        attack.Attacker = attacked.UID;
                        attack.AttackType = Network.GamePackets.Attack.Scapegoat;
                        attack.Damage = 0;
                        attack.ResponseDamage = damage;
                        attack.X = attacked.X;
                        attack.Y = attacked.Y;

                        if (attacker.Hitpoints <= damage)
                        {
                            if (attacker.EntityFlag == EntityFlag.Player)
                            {
                                attacked.Owner.UpdateQualifier(attacked.Owner, attacker.Owner, attacker.Hitpoints);

                                attacker.Owner.SendScreen(attack, true);
                                attacked.AttackPacket = null;
                            }
                            else
                            {
                                attacker.MonsterInfo.SendScreen(attack);
                            }
                            attacker.Die(attacked);
                        }
                        else
                        {
                            attacker.Hitpoints -= damage;
                            if (attacker.EntityFlag == EntityFlag.Player)
                            {
                                attacked.Owner.UpdateQualifier(attacked.Owner, attacker.Owner, damage);

                                attacker.Owner.SendScreen(attack, true);
                            }
                            else
                            {
                                attacker.MonsterInfo.SendScreen(attack);
                            }
                        }
                        Damage = 0;
                    }
                    else if (attacked.Owner.Spells.ContainsKey(3060) && ResponseRoll(10))
                    {
                        uint damage = (uint)(Damage / 10);
                        if (damage <= 1)
                            damage = 1;
                        if (damage > 10000) damage = 10000;
                        Network.GamePackets.Attack attack = new Network.GamePackets.Attack(true);
                        attack.Attacked = attacker.UID;
                        attack.Attacker = attacked.UID;
                        attack.AttackType = Network.GamePackets.Attack.Reflect;
                        attack.Damage = damage;
                        attack.ResponseDamage = damage;
                        attack.X = attacked.X;
                        attack.Y = attacked.Y;


                        if (attacker.Hitpoints <= damage)
                        {
                            if (attacker.EntityFlag == EntityFlag.Player)
                            {
                                attacked.Owner.UpdateQualifier(attacked.Owner, attacker.Owner, attacker.Hitpoints);

                                attacker.Owner.SendScreen(attack, true);
                                attacked.AttackPacket = null;
                            }
                            else
                            {
                                attacker.MonsterInfo.SendScreen(attack);
                            }
                            attacker.Die(attacked);
                        }
                        else
                        {
                            attacker.Hitpoints -= damage;
                            if (attacker.EntityFlag == EntityFlag.Player)
                            {
                                attacked.Owner.UpdateQualifier(attacked.Owner, attacker.Owner, damage);
                                attacker.Owner.SendScreen(attack, true);
                            }
                            else
                            {
                                attacker.MonsterInfo.SendScreen(attack);
                            }
                        }
                        Damage = 1;

                    }
                }
            }
            catch (Exception)
            {

            }
            }
        }
        private static bool Success(int percent)
        {
            if (percent == 0) return false;
            return Kernel.ChanceSuccess(Kernel.Random.Next(0, 30) + percent);
        }
        public static ulong CalculateExpBonus(ushort Level, ushort MonsterLevel, ulong Experience)
        {
            int leveldiff = (2 + Level - MonsterLevel);
            if (leveldiff < -5)
                Experience = (ulong)(Experience * 1.3);
            else if (leveldiff < -1)
                Experience = (ulong)(Experience * 1.2);
            else if (leveldiff == 4)
                Experience = (ulong)(Experience * 0.8);
            else if (leveldiff == 5)
                Experience = (ulong)(Experience * 0.3);
            else if (leveldiff > 5)
                Experience = (ulong)(Experience * 0.1);
            return Experience;
        }
        private static void Durability(Entity attacker, Entity attacked, Database.SpellInformation spell)
        {
            // World actors have synthetic equipment, with no persistent item ownership.
            if (attacker != null && attacker.Owner != null && attacker.Owner.Fake) return;
            if (spell != null)
                if (!spell.CanKill) return;
            if (attacker.EntityFlag == EntityFlag.Player)
                if (attacker.Owner.Map.ID == 1039) return;
            #region Attack
            if (attacker != null)
            {
                if (attacker.EntityFlag == EntityFlag.Player)
                {
                    for (byte i = 4; i <= 6; i++)
                    {
                        if (!attacker.Owner.Equipment.Free(i))
                        {
                            var item = attacker.Owner.Equipment.TryGetItem(i);
                            if (i == 5)
                            {
                                if (ItemHandler.IsArrow(item.ID))
                                {
                                    continue;
                                }
                            }
                            if (Kernel.Rate(20, 100))
                            {
                                if (item.Durability != 0)
                                {
                                    item.Durability--;
                                    Database.ConquerItemTable.UpdateDurabilityItem2(item, item.ID);
                                    Database.ConquerItemTable.UpdateDurabilityItem(item);
                                    item.Mode = Enums.ItemMode.Update;
                                    item.Send(attacker.Owner);
                                    item.Mode = Enums.ItemMode.Default;
                                }
                                else if (item.Durability == 0)
                                {
                                    if (item.ID == 1050002) return;
                                    _String str = new _String(true)
                                    {
                                        UID = attacker.UID,
                                        Type = 10,
                                        TextsCount = 1
                                    };
                                    str.Texts.Add("EquipBroken");
                                    attacker.Owner.Send(str.ToArray());
                                    //attacker.Owner.Send(new Message(Database.ConquerItemInformation.BaseInformations[item.ID].Name + " is completely damaged!", Message.TopLeft));
                                }
                            }
                        }
                        if (i == 6) break;
                    }
                    if (!attacker.Owner.Equipment.Free(10))
                    {
                        var item = attacker.Owner.Equipment.TryGetItem(10);
                        if (Kernel.Rate(20, 100))
                        {
                            if (item.Durability != 0)
                            {
                                item.Durability--;
                                Database.ConquerItemTable.UpdateDurabilityItem2(item, item.ID);
                                Database.ConquerItemTable.UpdateDurabilityItem(item);
                                item.Mode = Enums.ItemMode.Update;
                                item.Send(attacker.Owner);
                                item.Mode = Enums.ItemMode.Default;
                            }
                            else if (item.Durability == 0)
                            {
                                if (item.ID == 1050002) return;
                                _String str = new _String(true)
                                {
                                    UID = attacker.UID,
                                    Type = 10,
                                    TextsCount = 1
                                };
                                str.Texts.Add("EquipBroken");
                                attacker.Owner.Send(str.ToArray());
                                attacker.Owner.Send(new Message(Database.ConquerItemInformation.BaseInformations[item.ID].Name + " is completely damaged!", Message.TopLeft));
                            }
                        }
                    }
                }
            }
            #endregion
            #region Defence
            if (attacked != null)
            {
                if (attacked.EntityFlag == EntityFlag.Player)
                {
                    for (byte i = 1; i <= 8; i++)
                    {
                        if (i == 4 || i == 6 || i == 7) continue;
                        if (!attacked.Owner.Equipment.Free(i))
                        {
                            var item = attacked.Owner.Equipment.TryGetItem(i);
                            if (i == 5)
                            {
                                if (ItemHandler.IsArrow(item.ID))
                                {
                                    continue;
                                }
                            }
                            if (Kernel.Rate(30, 100))
                            {
                                if (item.Durability != 0)
                                {
                                    item.Durability--;
                                    Database.ConquerItemTable.UpdateDurabilityItem2(item, item.ID);
                                    Database.ConquerItemTable.UpdateDurabilityItem(item);
                                    item.Mode = Enums.ItemMode.Update;
                                    item.Send(attacked.Owner);
                                    item.Mode = Enums.ItemMode.Default;
                                }
                                else if (item.Durability == 0)
                                {
                                    _String str = new _String(true)
                                    {
                                        UID = attacked.UID,
                                        Type = 10,
                                        TextsCount = 1
                                    };
                                    str.Texts.Add("EquipBroken");
                                    attacked.Owner.Send(str.ToArray());
                                    attacked.Owner.Send(new Message(Database.ConquerItemInformation.BaseInformations[item.ID].Name + " is completely damaged!", Message.TopLeft));
                                }
                            }
                        }
                        if (i == 8) break;
                    }
                    if (!attacked.Owner.Equipment.Free(11) && Kernel.Rate(30, 100))
                    {
                        var item = attacked.Owner.Equipment.TryGetItem(11);
                        if (Kernel.Rate(30, 100))
                        {
                            if (item.Durability != 0)
                            {
                                item.Durability--;
                                Database.ConquerItemTable.UpdateDurabilityItem2(item, item.ID);
                                Database.ConquerItemTable.UpdateDurabilityItem(item);
                                item.Mode = Enums.ItemMode.Update;
                                item.Send(attacked.Owner);
                                item.Mode = Enums.ItemMode.Default;
                            }
                            else if (item.Durability == 0)
                            {
                                _String str = new _String(true)
                                {
                                    UID = attacked.UID,
                                    Type = 10,
                                    TextsCount = 1
                                };
                                str.Texts.Add("EquipBroken");
                                attacked.Owner.Send(str.ToArray());
                                attacked.Owner.Send(new Message(Database.ConquerItemInformation.BaseInformations[item.ID].Name + " is completely damaged!", Message.TopLeft));
                            }
                        }
                    }
                }
            }
            #endregion
        }



        internal static unsafe uint Ranged2(Entity attacker, Entity attacked, Database.SpellInformation spell, ref Attack attack)
        {
            int Damage = 0;

            if (attacked.EntityFlag == EntityFlag.Player)
                Damage = GetDamageEntity2Entity(attacker, attacked, Attack.Ranged, ref attack) / 80;
            CheckDamage(attacker, attacked, ref Damage, ref attack, false);
            return (uint)Damage;


        }
    }
    public class MathHelper
    {
        public static int BitFold32(int lower16, int higher16)
        {
            return (lower16) | (higher16 << 16);
        }
        public static void BitUnfold32(int bits32, out int lower16, out int upper16)
        {
            lower16 = (int)(bits32 & UInt16.MaxValue);
            upper16 = (int)(bits32 >> 16);
        }
        public static void BitUnfold64(ulong bits64, out int lower32, out int upper32)
        {
            lower32 = (int)(bits64 & UInt32.MaxValue);
            upper32 = (int)(bits64 >> 32);
        }
        public static int RoughDistance(int x1, int y1, int x2, int y2)
        {
            return Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));
        }
        public static int ConquerDirection(int x1, int y1, int x2, int y2)
        {
            double angle = Math.Atan2(y2 - y1, x2 - x1);
            angle -= Math.PI / 2;

            if (angle < 0) angle += 2 * Math.PI;

            angle *= 8 / (2 * Math.PI);
            return (int)angle;
        }
        public static int MulDiv(int number, int numerator, int denominator)
        {
            return (number * numerator + denominator / 2) / denominator;
        }
        public static bool OverflowAdd(ref int acc, int add)
        {
            if (int.MaxValue - acc < add)
                return true;
            acc = Math.Max(acc + add, 0);
            return false;
        }
        public static int AdjustDataEx(int data, int adjust, int maxData = 0)
        {
            if (adjust >= Revora.Game.Attacking.Calculate.StatusConstants.AdjustPercent)
                return MulDiv(data, adjust - Revora.Game.Attacking.Calculate.StatusConstants.AdjustPercent, 100);

            if (adjust <= Revora.Game.Attacking.Calculate.StatusConstants.AdjustSet)
                return -1 * adjust + Revora.Game.Attacking.Calculate.StatusConstants.AdjustSet;

            if (adjust == Revora.Game.Attacking.Calculate.StatusConstants.AdjustFull)
                return maxData;

            return data + adjust;
        }
    }
}
