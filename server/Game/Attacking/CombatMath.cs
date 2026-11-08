using System;
using Revora.Network.GamePackets;

namespace Revora.Game.Attacking
{
    internal static class CombatMath
    {
        internal static double CriticalChance(int strike, int immunity)
        {
            return Math.Max(0, Math.Min(100, ((long)strike - immunity) / 100.0));
        }

        internal static bool Roll(double chance)
        {
            return chance >= 100 || (chance > 0 && Kernel.ChanceSuccess(chance));
        }

        internal static int Critical(Entity attacker, Entity target, double damage, Attack packet, bool magic)
        {
            if (attacker.EntityFlag == EntityFlag.Player && Roll(CriticalChance(
                magic ? attacker.SkillCStrike : attacker.CriticalStrike, target == null ? 0 : target.Immunity)))
            {
                damage *= magic ? 2.0 : 1.5;
                if (packet != null) packet.Effect |= Attack.AttackEffects.CriticalStrike;
            }
            return (int)Math.Max(0, Math.Min(int.MaxValue, damage));
        }

        internal static int BattlePower(Entity attacker, Entity target, int damage, Attack packet, bool magic)
        {
            int gap = target.BattlePower - attacker.BattlePower;
            if (gap > 0 && attacker.EntityFlag == EntityFlag.Player && target.EntityFlag == EntityFlag.Player
                && Roll(Math.Max(0, Math.Min(100, ((int)attacker.Breaktrough - target.Counteraction) / 10.0))))
            {
                if (packet != null) packet.Effect |= Attack.AttackEffects.Penetration;
                gap = 0;
            }
            return magic ? Calculate.BattlePowerCalculationMagic(damage, gap) : Calculate.BattlePowerCalculationMelee(damage, gap);
        }

        internal static int Tortoise(Entity target, int damage)
        {
            if (target.EntityFlag != EntityFlag.Player) return damage;
            return (int)((long)Math.Max(0, damage) * (100 - Math.Min(100, (int)target.Gems[7])) / 100);
        }

        internal static int Block(Entity target, int damage, Attack packet)
        {
            if (target.EntityFlag == EntityFlag.Player && Roll(target.Block / 100.0))
            {
                if (packet != null) packet.Effect |= Attack.AttackEffects.Block;
                return damage / 2;
            }
            return damage;
        }

        internal static int MagicBase(Entity attacker, Entity target, uint spellPower, Attack packet)
        {
            double attack = ((double)attacker.MagicAttack + spellPower) * (100 + attacker.Gems[0]) / 100;
            double defense = target.Transformed ? target.TransformationMagicDefence : target.MagicDefence;
            double damage = Math.Max(0, attack - defense - target.BaseMagicDefence);
            damage *= (100 - Math.Min(100, (int)target.MagicDefencePercent)) / 100.0;
            return BattlePower(attacker, target, (int)Math.Min(int.MaxValue, damage), packet, true);
        }
    }
}
