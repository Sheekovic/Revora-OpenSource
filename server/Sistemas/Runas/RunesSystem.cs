using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Attacking;
using Revora.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Revora.Network.GamePackets
{
    public class RuneEXp
    {
        public ushort RuneType, Level;
        public uint Exp;
    }
    public class RunesSystem
    {
        public static List<RuneEXp> AllRunesExp = new List<RuneEXp>();
        public static void LoadRunesEXP()
        {
            AllRunesExp.Clear();
            string[] baseText = File.ReadAllLines(Constants.rune_levexp);
            foreach (var Line in baseText)
            {
                string[] data = Line.Split(new string[] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
                RuneEXp it = new RuneEXp();
                it.RuneType = ushort.Parse(data[1]);
                it.Level = ushort.Parse(data[2]);
                it.Exp = uint.Parse(data[3]);
                AllRunesExp.Add(it);
            }
        }
        public static List<ushort> RunesSpells = new List<ushort>()
        {
            14190,14260,14270,14350,14280,14380,14290,14410,14160,14300,14250,14440,14310,14470,14320,14500,14220,14330,14340,14530
        };
        public static void RemoveRunesSkills(Entity Entity)
        {
            Entity.IronShieldTime = 0;
            //Entity.RevengeTailChange = 0;
            Entity.FineRainTime = 0;
        }
        internal static void ClearChampionEffects(Entity body)
        {
            if(body.ContainsFlag((ulong)FlagsUpdate.Flags.Infinity))body.RemoveFlag((ulong)FlagsUpdate.Flags.Fly);
            foreach(var flag in new[]{FlagsUpdate.Flags.Rampage,FlagsUpdate.Flags.Infinity,FlagsUpdate.Flags.Absolution,
                FlagsUpdate.Flags.IronShield,FlagsUpdate.Flags.Slayer,FlagsUpdate.Flags.Sacrifice,FlagsUpdate.Flags.FrostArrows})
                body.RemoveFlag((ulong)flag);
            RemoveRunesSkills(body);ResetRunesValues(body);
            body.InfinityDamage=0;body.IronShieldDefence=0;body.IronShieldLevel=0;
            body.FineRainHitPoints=0;body.SacrificePercent=0;
        }
        public static void ResetRunesValues(Entity Entity)
        {
            Entity.DrainingTouch = 0;
           Entity.BloodSpawn = 0;
            Entity.Healer = 0;
            Entity.NoMercyDamage = 0;
            Entity.FreeSoul = 0;
            Entity.XPKiller = 0;
            Entity.Grabber = 0;
            Entity.MPMaster = 0;
            Entity.BossKiller = 0;
            Entity.XPBooster = 0;
            Entity.TortoiseBreaker = 0;
            Entity.WitcheryDamage = 0;
            Entity.ImmunityRate = 0;
        }
        public static void HandleYellowRunes(Entity Entity)
        {
            if (Entity.Owner != null && Entity.Owner.ChampionGroup != null) { ResetRunesValues(Entity); return; }
            #region TortoiseBreaker
            if (Entity.Owner.Spells.ContainsKey(40301))
            {
                var Level = Entity.Owner.Spells[40301].Level;
                switch (Level)
                {
                    case 1:
                        {
                            Entity.TortoiseBreaker = 164000;
                            break;
                        }
                    case 2:
                        {
                            Entity.TortoiseBreaker = 163500;
                            break;
                        }
                    case 3:
                        {
                            Entity.TortoiseBreaker = 163000;
                            break;
                        }
                    case 4:
                        {
                            Entity.TortoiseBreaker = 162500;
                            break;
                        }
                    case 5:
                        {
                            Entity.TortoiseBreaker = 162000;
                            break;
                        }
                    case 6:
                        {
                            Entity.TortoiseBreaker = 161500;
                            break;
                        }
                    case 7:
                        {
                            Entity.TortoiseBreaker = 161000;
                            break;
                        }
                    case 8:
                        {
                            Entity.TortoiseBreaker = 160500;
                            break;
                        }
                    case 9:
                        {
                            Entity.TortoiseBreaker = 160000;
                            break;
                        }
                }
            }
            #endregion
            #region DrainingTouchBooster
            if (Entity.Owner.Spells.ContainsKey(40302))
            {
                var Level = Entity.Owner.Spells[40302].Level;
              
                switch (Level)
                {
                    case 1:
                        {
                            Entity.DrainingTouch += 1;
                            break;
                        }
                    case 2:
                        {
                            Entity.DrainingTouch += 2;
                            break;
                        }
                    case 3:
                        {
                            Entity.DrainingTouch += 3;
                            break;
                        }
                    case 4:
                        {
                            Entity.DrainingTouch += 4;
                            break;
                        }
                    case 5:
                        {
                            Entity.DrainingTouch += 5;
                            break;
                        }
                    case 6:
                        {
                            Entity.DrainingTouch += 6;
                            break;
                        }
                    case 7:
                        {
                            Entity.DrainingTouch += 7;
                            break;
                        }
                    case 8:
                        {
                            Entity.DrainingTouch += 8;
                            break;
                        }
                    case 9:
                        {
                            Entity.DrainingTouch += 9;
                            break;
                        }
                }
            }
            #endregion
            #region BloodSpawnBooster
            if (Entity.Owner.Spells.ContainsKey(40303))
            {
                var Level = Entity.Owner.Spells[40303].Level;
                switch (Level)
                {
                    case 1:
                        {
                            Entity.BloodSpawn += 1;
                            break;
                        }
                    case 2:
                        {
                            Entity.BloodSpawn += 2;
                            break;
                        }
                    case 3:
                        {
                            Entity.BloodSpawn += 3;
                            break;
                        }
                    case 4:
                        {
                            Entity.BloodSpawn += 4;
                            break;
                        }
                    case 5:
                        {
                            Entity.BloodSpawn += 5;
                            break;
                        }
                    case 6:
                        {
                            Entity.BloodSpawn += 6;
                            break;
                        }
                    case 7:
                        {
                            Entity.BloodSpawn += 7;
                            break;
                        }
                    case 8:
                        {
                            Entity.BloodSpawn += 8;
                            break;
                        }
                    case 9:
                        {
                            Entity.BloodSpawn += 9;
                            break;
                        }
                }
            }
            #endregion
            #region Healer
            if (Entity.Owner.Spells.ContainsKey(40304))
            {
                Entity.HealerStamp = Time32.Now;
                var Level = Entity.Owner.Spells[40304].Level;
                switch (Level)
                {
                    case 1:
                        {
                            Entity.Healer = 1000;
                            break;
                        }
                    case 2:
                        {
                            Entity.Healer = 3000;
                            break;
                        }
                    case 3:
                        {
                            Entity.Healer = 5000;
                            break;
                        }
                    case 4:
                        {
                            Entity.Healer = 7000;
                            break;
                        }
                    case 5:
                        {
                            Entity.Healer = 9000;
                            break;
                        }
                    case 6:
                        {
                            Entity.Healer = 11000;
                            break;
                        }
                    case 7:
                        {
                            Entity.Healer = 14000;
                            break;
                        }
                    case 8:
                        {
                            Entity.Healer = 17000;
                            break;
                        }
                    case 9:
                        {
                            Entity.Healer = 20000;
                            break;
                        }
                }
            }
            #endregion
            #region Sturdiness
            if (Entity.Owner.Spells.ContainsKey(40305))
            {
                var Level = Entity.Owner.Spells[40305].Level;
                switch (Level)
                {
                    case 1:
                        {
                            Entity.ImmunityRate += 10;
                            break;
                        }
                    case 2:
                        {
                            Entity.ImmunityRate += 15;
                            break;
                        }
                    case 3:
                        {
                            Entity.ImmunityRate += 20;
                            break;
                        }
                    case 4:
                        {
                            Entity.ImmunityRate += 25;
                            break;
                        }
                    case 5:
                        {
                            Entity.ImmunityRate += 30;
                            break;
                        }
                    case 6:
                        {
                            Entity.ImmunityRate += 35;
                            break;
                        }
                    case 7:
                        {
                            Entity.ImmunityRate += 40;
                            break;
                        }
                    case 8:
                        {
                            Entity.ImmunityRate += 45;
                            break;
                        }
                    case 9:
                        {
                            Entity.ImmunityRate += 55;
                            break;
                        }
                }
            }
            #endregion
            #region XPKiller
            if (Entity.Owner.Spells.ContainsKey(40306))
            {
                var Level = Entity.Owner.Spells[40306].Level;
                switch (Level)
                {
                    case 1:
                        {
                            Entity.XPKiller = 1;
                            break;
                        }
                    case 2:
                        {
                            Entity.XPKiller = 2;
                            break;
                        }
                    case 3:
                        {
                            Entity.XPKiller = 3;
                            break;
                        }
                    case 4:
                        {
                            Entity.XPKiller = 4;
                            break;
                        }
                    case 5:
                        {
                            Entity.XPKiller = 5;
                            break;
                        }
                    case 6:
                        {
                            Entity.XPKiller = 6;
                            break;
                        }
                    case 7:
                        {
                            Entity.XPKiller = 7;
                            break;
                        }
                    case 8:
                        {
                            Entity.XPKiller = 8;
                            break;
                        }
                    case 9:
                        {
                            Entity.XPKiller = 9;
                            break;
                        }
                }
            }
            #endregion
            #region Grabber
            if (Entity.Owner.Spells.ContainsKey(40307))
            {
                var Level = Entity.Owner.Spells[40307].Level;
                switch (Level)
                {
                    case 1:
                        {
                            Entity.Grabber += 5;
                            break;
                        }
                    case 2:
                        {
                            Entity.Grabber += 10;
                            break;
                        }
                    case 3:
                        {
                            Entity.Grabber += 15;
                            break;
                        }
                    case 4:
                        {
                            Entity.Grabber += 20;
                            break;
                        }
                    case 5:
                        {
                            Entity.Grabber += 25;
                            break;
                        }
                    case 6:
                        {
                            Entity.Grabber += 30;
                            break;
                        }
                    case 7:
                        {
                            Entity.Grabber += 35;
                            break;
                        }
                    case 8:
                        {
                            Entity.Grabber += 40;
                            break;
                        }
                    case 9:
                        {
                            Entity.Grabber += 45;
                            break;
                        }
                }
            }
            #endregion
            #region MPMaster
            if (Entity.Owner.Spells.ContainsKey(40308))
            {
                var Level = Entity.Owner.Spells[40308].Level;
                switch (Level)
                {
                    case 1:
                        {
                            Entity.MPMaster = 10;
                            break;
                        }
                    case 2:
                        {
                            Entity.MPMaster = 15;
                            break;
                        }
                    case 3:
                        {
                            Entity.MPMaster = 25;
                            break;
                        }
                    case 4:
                        {
                            Entity.MPMaster = 40;
                            break;
                        }
                    case 5:
                        {
                            Entity.MPMaster = 50;
                            break;
                        }
                    case 6:
                        {
                            Entity.MPMaster = 60;
                            break;
                        }
                    case 7:
                        {
                            Entity.MPMaster = 75;
                            break;
                        }
                    case 8:
                        {
                            Entity.MPMaster = 85;
                            break;
                        }
                    case 9:
                        {
                            Entity.MPMaster = 90;
                            break;
                        }
                }
            }
            #endregion
            #region BossKiller
            if (Entity.Owner.Spells.ContainsKey(40309))
            {
                var Level = Entity.Owner.Spells[40309].Level;
                switch (Level)
                {
                    case 1:
                        {
                            Entity.BossKiller = 220;
                            break;
                        }
                    case 2:
                        {
                            Entity.BossKiller = 230;
                            break;
                        }
                    case 3:
                        {
                            Entity.BossKiller = 240;
                            break;
                        }
                    case 4:
                        {
                            Entity.BossKiller = 250;
                            break;
                        }
                    case 5:
                        {
                            Entity.BossKiller = 260;
                            break;
                        }
                    case 6:
                        {
                            Entity.BossKiller = 270;
                            break;
                        }
                    case 7:
                        {
                            Entity.BossKiller = 280;
                            break;
                        }
                    case 8:
                        {
                            Entity.BossKiller = 290;
                            break;
                        }
                    case 9:
                        {
                            Entity.BossKiller = 300;
                            break;
                        }
                }
            }
            #endregion
            #region NoMercy
            if (Entity.Owner.Spells.ContainsKey(40310))
            {
                var Level = Entity.Owner.Spells[40310].Level;
                switch (Level)
                {
                    case 1:
                        {
                            Entity.NoMercyDamage = 5000;
                            break;
                        }
                    case 2:
                        {
                            Entity.NoMercyDamage = 5500;
                            break;
                        }
                    case 3:
                        {
                            Entity.NoMercyDamage = 6000;
                            break;
                        }
                    case 4:
                        {
                            Entity.NoMercyDamage = 6500;
                            break;
                        }
                    case 5:
                        {
                            Entity.NoMercyDamage = 7000;
                            break;
                        }
                    case 6:
                        {
                            Entity.NoMercyDamage = 7500;
                            break;
                        }
                    case 7:
                        {
                            Entity.NoMercyDamage = 8000;
                            break;
                        }
                    case 8:
                        {
                            Entity.NoMercyDamage = 9000;
                            break;
                        }
                    case 9:
                        {
                            Entity.NoMercyDamage = 10000;
                            break;
                        }
                }
            }
            #endregion
            #region XPBooster
            if (Entity.Owner.Spells.ContainsKey(40311))
            {
                Entity.XPBoosterTimes = 0;
                var Level = Entity.Owner.Spells[40311].Level;
                switch (Level)
                {
                    case 1:
                        {
                            Entity.XPBooster = 1;
                            break;
                        }
                    case 2:
                        {
                            Entity.XPBooster = 2;
                            break;
                        }
                    case 3:
                        {
                            Entity.XPBooster = 3;
                            break;
                        }
                    case 4:
                        {
                            Entity.XPBooster = 4;
                            break;
                        }
                    case 5:
                        {
                            Entity.XPBooster = 5;
                            break;
                        }
                    case 6:
                        {
                            Entity.XPBooster = 6;
                            break;
                        }
                    case 7:
                        {
                            Entity.XPBooster = 7;
                            break;
                        }
                    case 8:
                        {
                            Entity.XPBooster = 8;
                            break;
                        }
                    case 9:
                        {
                            Entity.XPBooster = 9;
                            break;
                        }
                }
            }
            #endregion
            #region FreeSoulBooster
            if (Entity.Owner.Spells.ContainsKey(40312))
            {
                var Level = Entity.Owner.Spells[40312].Level;
                switch (Level)
                {
                    case 1:
                        {
                            Entity.FreeSoul += 1;
                            break;
                        }
                    case 2:
                        {
                            Entity.FreeSoul += 2;
                            break;
                        }
                    case 3:
                        {
                            Entity.FreeSoul += 3;
                            break;
                        }
                    case 4:
                        {
                            Entity.FreeSoul += 4;
                            break;
                        }
                    case 5:
                        {
                            Entity.FreeSoul += 5;
                            break;
                        }
                    case 6:
                        {
                            Entity.FreeSoul += 6;
                            break;
                        }
                    case 7:
                        {
                            Entity.FreeSoul += 7;
                            break;
                        }
                    case 8:
                        {
                            Entity.FreeSoul += 8;
                            break;
                        }
                    case 9:
                        {
                            Entity.FreeSoul += 9;
                            break;
                        }
                }
            }
            #endregion
            #region Witchery
            if (Entity.Owner.Spells.ContainsKey(40313))
            {
                var Level = Entity.Owner.Spells[40313].Level;
                switch (Level)
                {
                    case 1:
                        {
                            Entity.WitcheryDamage = 2000;
                            break;
                        }
                    case 2:
                        {
                            Entity.WitcheryDamage = 2300;
                            break;
                        }
                    case 3:
                        {
                            Entity.WitcheryDamage = 2600;
                            break;
                        }
                    case 4:
                        {
                            Entity.WitcheryDamage = 3000;
                            break;
                        }
                    case 5:
                        {
                            Entity.WitcheryDamage = 3500;
                            break;
                        }
                    case 6:
                        {
                            Entity.WitcheryDamage = 4000;
                            break;
                        }
                    case 7:
                        {
                            Entity.WitcheryDamage = 4500;
                            break;
                        }
                    case 8:
                        {
                            Entity.WitcheryDamage = 5000;
                            break;
                        }
                    case 9:
                        {
                            Entity.WitcheryDamage = 6000;
                            break;
                        }
                }
            }
            #endregion
        }
        internal static bool FineRainPulse(Entity body,Map original,Time32 life,Action action) {
            if(body==null || body.Owner==null || action==null || !Revora.Game.Features.SessionInstall.TryEnter(body.UID))return false;
            try {
                using(var guild=Revora.Game.Features.GuildPkBanner.PlayerCombatLease.Acquire(body,body,false,false,true))
                using(var health=Revora.Game.Features.HealthMutationLease.Acquire(body,null)) {
                    if(guild!=null && !guild.Allowed || !health.Allowed || !Revora.Game.Features.QuestInventory.Player(body.Owner)
                        || !ReferenceEquals(body.Owner.Entity,body) || !ReferenceEquals(body.Owner.Map,original)
                        || body.DeathStamp.Value!=life.Value || body.Owner.ChampionGroup!=null)return false;
                    action();return true;
                }
            }finally{Revora.Game.Features.SessionInstall.Exit(body.UID);}
        }
        private static bool FineRainTarget(Entity caster,Entity target,SpellInformation spell,SpellUse packet,Attack attack,bool changeMaximum) {
            if(target==null || target.Owner==null || caster==null || spell==null)return false;
            using(var guild=Revora.Game.Features.GuildPkBanner.PlayerCombatLease.Acquire(caster,target,false,false,true))
            using(var health=Revora.Game.Features.HealthMutationLease.Acquire(caster,target)) {
                if(guild!=null && !guild.Allowed || !health.Allowed || !Revora.Game.Features.QuestInventory.Player(target.Owner)
                    || !ReferenceEquals(target.Owner.Entity,target) || target.Owner.ChampionGroup!=null || target.MapID!=caster.MapID
                    || !ReferenceEquals(target.Owner.Map,caster.Owner.Map) || Kernel.GetDistance(caster.X,caster.Y,target.X,target.Y)>spell.Distance)return false;
                if(changeMaximum)target.MaxHitpoints=spell.Power;
                target.Hitpoints=target.MaxHitpoints;target.FineRainTime=(uint)spell.Damage1;target.FineRainStamp=Time32.Now;
                packet.AddTarget(target.UID,0,attack);return true;
            }
        }
        public static void HandleRedBlueRunesSkills(Attack attack, Entity attacker, Entity attacked, SpellInformation spell, ushort X, ushort Y)
        {
            var owner=attacker==null?null:attacker.Owner;
            if(owner==null || owner.ItemSyncRoot==null || !System.Threading.Monitor.TryEnter(owner.ItemSyncRoot))return;
            try {
                using(var guild=Revora.Game.Features.GuildPkBanner.PlayerCombatLease.Acquire(attacker,attacker,false,false,true))
                using(var health=Revora.Game.Features.HealthMutationLease.Acquire(attacker,attacked)) {
                    if(guild!=null && !guild.Allowed || !health.Allowed)return;
                    HandleRedBlueRunesSkillsOwned(attack,attacker,attacked,spell,X,Y);
                }
            } finally {System.Threading.Monitor.Exit(owner.ItemSyncRoot);}
        }
        private static void HandleRedBlueRunesSkillsOwned(Attack attack, Entity attacker, Entity attacked, SpellInformation spell, ushort X, ushort Y)
        {
            if (attacker != null && attacker.Owner != null && attacker.Owner.ChampionGroup != null
                || attacked != null && attacked.Owner != null && attacked.Owner.ChampionGroup != null) return;
            switch (spell.ID)
            {
                #region Trojan
                #region Rampage
                case 14190:
                    {
                        if (Handle.CanUseSpell(spell, attacker.Owner))
                        {
                            SpellUse suse = new SpellUse(true);
                            suse.Attacker = attacker.UID;
                            suse.SpellID = spell.ID;
                            suse.SpellLevel = spell.Level;
                            suse.X = X;
                            suse.Y = Y;

                            attacker.RampageStamp = Time32.Now;
                            attacker.AddSpellFlag(FlagsUpdate.Flags.Rampage, spell.Duration, true);

                            int MaxStamina = (byte)(attacker.HeavenBlessing > 0 ? 150 : 100);
                            attacker.Stamina = (byte)Math.Min(200, MaxStamina);
                            attacker.Hitpoints = attacker.MaxHitpoints;

                            FlagsUpdate upgrade2 = new FlagsUpdate(true);
                            upgrade2.UID = attacker.UID;
                            upgrade2.Append(102, 214, (uint)spell.Duration, 0, 0);
                            attacker.Owner.Send(upgrade2.ToArray());

                            attacker.Owner.SendScreen(suse, true);
                        }
                        break;
                    }
                #endregion
                #endregion
                #region Warrior
                #region Pitching
                case 14270:
                    {
                        if (Handle.CanUseSpell(spell, attacker.Owner))
                        {
                            Handle.PrepareSpell(spell, attacker.Owner);
                            SpellUse suse = new SpellUse(true);
                            suse.Attacker = attacker.UID;
                            suse.SpellID = spell.ID;
                            suse.SpellLevel = spell.Level;
                            suse.X = attacker.X;
                            suse.Y = attacker.Y;
                            if (attacked.EntityFlag == EntityFlag.Player && attacked.OnKOSpell())
                            {
                                if (Handle.CanAttack(attacker, attacked, spell, false))
                                {
                                    int rate = attacked.BattlePower - attacker.BattlePower;
                                    if (rate <= 0 || Kernel.Rate(100 - (Math.Min(rate, 5) * 5)))
                                    {
                                        attacked.RemoveXP(0);
                                    }
                                }
                            }
                            attacker.Owner.SendScreen(suse, true);
                        }
                        break;
                    }
                #endregion
                #region AllRound
                case 14350:
                    {
                        if (attacker != null)
                        {
                            ConquerItem Shield;
                            if (!attacker.Owner.AlternateEquipment)
                            {
                                if (attacker.Owner.Equipment.TryGetEquip(ConquerItem.LeftWeapon, out Shield))
                                {
                                    if (ItemHandler.IsShield(Shield.ID))
                                    {
                                        attacker.MinAttack += (uint)spell.Damage1;
                                        attacker.MaxAttack += (uint)spell.Damage1;
                                        attacker.BaseDefence += (ushort)spell.Damage2;
                                        attacker.MagicDefence += (ushort)spell.Damage3;
                                    }
                                }
                            }
                            else
                            {
                                if (attacker.Owner.Equipment.TryGetEquip(ConquerItem.AlternateLeftWeapon, out Shield))
                                {
                                    if (ItemHandler.IsShield(Shield.ID))
                                    {
                                        attacker.MinAttack += (uint)spell.Damage1;
                                        attacker.MaxAttack += (uint)spell.Damage1;
                                        attacker.BaseDefence += (ushort)spell.Damage2;
                                        attacker.MagicDefence += (ushort)spell.Damage3;
                                    }
                                }
                            }
                        }
                        break;
                    }
                #endregion
                #endregion
                #region Archer
                #region FrostArrows
                case 14280:
                    {
                        if (Handle.CanUseSpell(spell, attacker.Owner))
                        {
                            Handle.PrepareSpell(spell, attacker.Owner);
                            SpellUse suse = new SpellUse(true);
                            suse.Attacker = attacker.UID;
                            suse.SpellID = spell.ID;
                            suse.SpellLevel = spell.Level;
                            suse.X = X;
                            suse.Y = Y;
                            foreach (IMapObject _obj in attacker.Owner.Screen.Objects)
                            {
                                if (_obj.MapObjType == MapObjectType.Player)
                                {
                                    attacked = _obj as Entity;
                                    if (Kernel.GetDistance(X, Y, attacked.X, attacked.Y) <= spell.Range)
                                    {
                                        if (Handle.CanAttack(attacker, attacked, spell, attack.AttackType == Attack.Melee))
                                        {
                                            int potDifference = attacker.BattlePower - attacked.BattlePower;
                                            int rate = (byte)spell.Percent + potDifference;
                                            
                                            if (Kernel.Rate(rate))
                                            {
                                                attacked.AddSpellFlag(FlagsUpdate.Flags.FrostArrows, spell.Duration, true);
                                                suse.Targets.Add(attacked.UID, (uint)spell.Duration);
                                            }
                                        }
                                    }
                                }
                            }
                            attacker.Owner.SendScreen(suse, true);
                        }
                        break;
                    }
                #endregion
                #region Infinity
                case 14380:
                    {
                        if (Handle.CanUseSpell(spell, attacker.Owner))
                        {
                            Handle.PrepareSpell(spell, attacker.Owner);
                            SpellUse suse = new SpellUse(true);
                            suse.Attacker = attacker.UID;
                            suse.SpellID = spell.ID;
                            suse.SpellLevel = spell.Level;
                            suse.X = X;
                            suse.Y = Y;
                            attacker.InfinityDamage = spell.Damage1;
                            attacker.RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
                            attacker.AddSpellFlag(FlagsUpdate.Flags.Fly, spell.Duration, true);
                            attacker.AddSpellFlag(FlagsUpdate.Flags.Infinity, spell.Duration, true);

                            FlagsUpdate upgrade = new FlagsUpdate(true);
                            upgrade.UID = attacker.UID;
                            upgrade.Append(103, 215, (uint)spell.Duration, 0, 0);
                            attacker.Owner.Send(upgrade.ToArray());


                            suse.AddTarget(attacker.UID, 0, null);
                            attacker.Owner.SendScreen(suse, true);
                        }
                        break;
                    }
                #endregion
                #endregion
                #region Ninja
                #region Absolution
                case 14410:
                    {
                        if (Handle.CanUseSpell(spell, attacker.Owner))
                        {
                            SpellUse suse = new SpellUse(true);
                            suse.Attacker = attacker.UID;
                            suse.SpellID = spell.ID;
                            suse.SpellLevel = spell.Level;
                            suse.X = X;
                            suse.Y = Y;
                            //Revive
                            attacker.ClearFlags();
                            attacker.Action = Enums.ConquerAction.None;
                            attacker.Owner.ReviveStamp = Time32.Now;
                            attacker.Owner.Attackable = false;
                            attacker.TransformationID = 0;
                            attacker.AutoRev = 0;
                            attacker.RemoveFlag((ulong)FlagsUpdate.Flags.Dead);
                            attacker.RemoveFlag((ulong)FlagsUpdate.Flags.Ghost);
                            attacker.AddSpellFlag(FlagsUpdate.Flags.Absolution, spell.Duration, true);
                            attacker.Hitpoints = attacker.MaxHitpoints;
                            attacker.Ressurect();
                            //respawn time
                            FlagsUpdate upgrade2 = new FlagsUpdate(true);
                            upgrade2.UID = attacker.UID;
                            upgrade2.Append(106, 216, (uint)spell.Duration, 0, 0);
                            attacker.Owner.Send(upgrade2.ToArray());

                            attacker.Owner.SendScreen(suse, true);
                        }
                        break;
                    }
                #endregion
                #endregion
                #region Monk
                #region Acalanatha
                case 14300:
                    {
                        if (attacker != null)
                            attacker.ImmunityRate += (uint)spell.Power;
                        break;
                    }
                #endregion
                #region IronShield
                case 14160:
                    {
                        if (Handle.CanUseSpell(spell, attacker.Owner))
                        {
                            Handle.PrepareSpell(spell, attacker.Owner);
                            SpellUse suse = new SpellUse(true);
                            suse.Attacker = attacker.UID;
                            suse.SpellID = spell.ID;
                            suse.SpellLevel = spell.Level;
                            suse.X = attacker.X;
                            suse.Y = attacker.Y;
                            attacker.IronShieldDefence = (uint)spell.Damage4;
                            attacker.IronShieldLevel = spell.Level;
                            attacker.AddSpellFlag(FlagsUpdate.Flags.IronShield, spell.Duration, true);
                            attacker.IronShieldPacket();
                            attacker.Owner.SendScreen(suse, true);
                        }
                        break;
                    }
                #endregion
                #endregion
                #region Pirate
                #region Slayer
                case (ushort)Enums.Skill.Slayer:
                    {
                        if (Handle.CanUseSpell(spell, attacker.Owner))
                        {
                            SpellUse suse = new SpellUse(true);
                            suse.Attacker = attacker.UID;
                            suse.SpellID = spell.ID;
                            suse.SpellLevel = spell.Level;
                            suse.X = X;
                            suse.Y = Y;
                            //Spell attributes
                            attacker.AddSpellFlag(FlagsUpdate.Flags.Slayer, spell.Duration, true);
                            attacker.Owner.SendScreen(suse, true);
                        }
                        break;
                    }
                #endregion
                #endregion
                #region Leelong
                #region CounterPunch
                case (ushort)Enums.Skill.CounterPunch:
                    {
                        if (Kernel.GetDistance(attacked.X, attacked.Y, attacker.X, attacker.Y) <= 1)
                        {
                            if (Kernel.Rate(spell.Percent))
                            {
                                if (Handle.CanUseSpell(spell, attacker.Owner))
                                {
                                    Handle.PrepareSpell(spell, attacker.Owner);
                                    InLineAlgorithm ila = new InLineAlgorithm(attacker.X, attacked.X, attacker.Y, attacked.Y, 2, InLineAlgorithm.Algorithm.DDA);
                                    SpellUse suse = new SpellUse(true);
                                    suse.Attacker = attacker.UID;
                                    suse.SpellID = spell.ID;
                                    suse.SpellLevel = attacker.Owner.Spells[spell.ID].Level;
                                    suse.X = X;
                                    suse.Y = Y;
                                    var attacke = new Attack(true);
                                    attacke.Attacker = suse.Attacker = attacker.UID;
                                    attacke.SpellID = spell.ID;
                                    attacke.X = X;
                                    attacke.Y = Y;
                                    attacke.AttackType = Attack.Magic;
                                    foreach (IMapObject _obj in attacker.Owner.Screen.Objects)
                                    {
                                        if (_obj == null) continue;
                                        if (_obj.MapObjType == MapObjectType.Monster || _obj.MapObjType == MapObjectType.Player)
                                        {
                                            attacked = _obj as Entity;
                                            if (ila.InLine(attacked.X, attacked.Y))
                                            {
                                                if (!Handle.CanAttack(attacker, attacked, spell, false)) continue;
                                                attacke.Effect = Attack.AttackEffects.None;
                                                uint damage = Calculate.Melee(attacker,attacked,spell,ref attack);
                                                suse.Effect = attacke.Effect;
                                                Handle.ReceiveAttack(attacker, attacked, attacke, ref damage, spell);
                                                suse.AddTarget(attacked.UID, damage, attacke);
                                            }
                                        }
                                    }
                                    attacker.Owner.SendScreen(suse, true);
                                }
                            }
                        }
                        break;
                    }
                #endregion
                #endregion
                #region WaterTaoist
                #region Sacrifice - Not finished
                case 14320:
                    {
                        if (Handle.CanUseSpell(spell, attacker.Owner))
                        {
                            Handle.PrepareSpell(spell, attacker.Owner);
                            SpellUse suse = new SpellUse(true);
                            suse.Attacker = attacker.UID;
                            suse.SpellID = spell.ID;
                            suse.SpellLevel = spell.Level;
                            suse.X = attacker.X;
                            suse.Y = attacker.Y;
                            attacker.Owner.SendScreen(suse, true);
                            if (attacker.ContainsFlag((ulong)FlagsUpdate.Flags.Sacrifice))
                            {
                                attacker.RemoveFlag((ulong)FlagsUpdate.Flags.Sacrifice);
                                attacker.Owner.LoadItemStats();
                            }
                            else
                            {
                                attacker.AddSpellFlag(FlagsUpdate.Flags.Sacrifice, StatusFlagsBigVector32.PermanentFlag, false);
                                attacker.TrueDmg = (int)(attacker.MaxHitpoints * (ulong)attacker.SacrificePercent / 100);
                                attacker.Hitpoints = (attacker.MaxHitpoints - (uint)attacker.TrueDmg);
                            }
                        }
                        break;
                    }
                #endregion
                #region FineRain
                /*case 14500:
                    {
                        if (Handle.CanUseSpell(spell, attacker.Owner))
                        {
                            Handle.PrepareSpell(spell, attacker.Owner);
                            SpellUse suse = new SpellUse(true);
                            suse.Attacker = attacker.UID;
                            suse.SpellID = spell.ID;
                            suse.SpellLevel = spell.Level;
                            suse.X = attacker.X;
                            suse.Y = attacker.Y;

                            attacker.FineRainTime = (uint)spell.Damage1;
                            attacker.FineRainStamp = Time32.Now;
                            attacker.FineRainHitPoints = spell.Power;
                            attacker.Hitpoints = (ushort)(attacker.MaxHitpoints = attacker.FineRainHitPoints);

                            if (attacker.Owner.Team != null && attacker.Owner.Team.IsTeammate(attacked.UID))
                            {
                                if (Kernel.GetDistance(attacker.X, attacker.Y, X, Y) <= spell.Range)
                                {
                                    foreach (IMapObject _obj in attacker.Owner.Screen.Objects)
                                    {
                                        if (_obj == null) continue;
                                        if (_obj.MapObjType == MapObjectType.Player)
                                        {
                                            attacked = _obj as Entity;
                                            if (Kernel.GetDistance(attacker.X, attacker.Y, attacked.X, attacked.Y) <= spell.Distance)
                                            {
                                                if (!attacked.Dead)
                                                {
                                                    attacked.FineRainTime = (uint)spell.Damage1;
                                                    attacked.FineRainStamp = Time32.Now;
                                                    attacked.FineRainHitPoints = spell.Power;
                                                    attacked.Hitpoints = (ushort)(attacked.MaxHitpoints = attacked.FineRainHitPoints);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            attacker.Owner.SendScreen(suse, true);
                        }
                        break;
                    }*/
                #endregion
                #region FineRain
                case 14500:
                    {
                        if (Handle.CanUseSpell(spell, attacker.Owner))
                        {
                            Handle.PrepareSpell(spell, attacker.Owner);
                            SpellUse suse = new SpellUse(true);
                            suse.Attacker = attacker.UID;
                            suse.SpellID = spell.ID;
                            suse.SpellLevel = spell.Level;
                            suse.X = attacker.X;
                            suse.Y = attacker.Y;
                            FlagsUpdate up = new FlagsUpdate(true);
                            up.UID = attacker.UID;
                            up.Append(4294967295, 0);
                            up.Send(attacker.Owner);
                            FlagsUpdate up2 = new FlagsUpdate(true);
                            up2.UID = attacker.UID;
                            up2.Buffer.Values.Add(new FlagsUpdate.CMsgFlags() { Type = 25, Flags = new ulong[] { 0, 0, 0, 0, 0, 0, 1048576 } });
                            up2.Buffer.Values.Add(new FlagsUpdate.CMsgFlags() { Type = 102, Amount = (uint)spell.Power, value5 = (uint)spell.Power, ICOID = 212, Time = (ulong)spell.Damage1, value = 0 });
                            up2.Send(attacker.Owner);
                            var originalMap=attacker.Owner.Map;var originalLife=attacker.DeathStamp;
                            System.Threading.Tasks.Task.Factory.StartNew(() =>
                            {
                                for (int i = 0; i < 10; i++)
                                {
                                    if(!FineRainPulse(attacker,originalMap,originalLife,()=>{
                                        FlagsUpdate up3 = new FlagsUpdate(true);up3.UID = attacker.UID;
                                        up3.Buffer.Values.Add(new FlagsUpdate.CMsgFlags() { Type = 4294967295 });
                                        up3.Buffer.Values.Add(new FlagsUpdate.CMsgFlags() { value = (uint)Math.Max(0L,(long)spell.Power-5000L*i) });
                                        up3.Send(attacker.Owner);
                                    }))return;
                                    System.Threading.Thread.Sleep(1000);
                                }
                                FineRainPulse(attacker,originalMap,originalLife,()=>{
                                    attacker.Hitpoints=attacker.MaxHitpoints;
                                    FlagsUpdate up4=new FlagsUpdate(true);up4.UID=attacker.UID;
                                    up4.Buffer.Values.Add(new FlagsUpdate.CMsgFlags() { Type = 25, Flags = new ulong[] { 0, 0, 0, 0, 0, 0, 2097152 } });
                                    up4.Buffer.Values.Add(new FlagsUpdate.CMsgFlags() { Type = 78, Time = (ulong)spell.Damage1, ICOID = 172, Amount = 200 });
                                    up4.Send(attacker.Owner);
                                });
                            });
                            if(attacker.Owner.Team!=null) {
                                foreach(GameState teammate in attacker.Owner.Team.Teammates)
                                    FineRainTarget(attacker,teammate==null?null:teammate.Entity,spell,suse,attack,false);
                            }else FineRainTarget(attacker,attacked,spell,suse,attack,true);
                            attacker.AttackPacket=null;attacker.Owner.SendScreen(suse,true);
                        }
                        break;
                    }
                    #endregion
                    #endregion
            }
        }
        public static void HandlePacket1009(GameState client, byte[] packet)
        {
            if (client == null || client.ItemSyncRoot == null || client.Entity == null || client.Entity.PersistenceRequiresReload
                || packet == null || packet.Length < 38 || BitConverter.ToUInt16(packet, 0) != packet.Length - 8
                || BitConverter.ToUInt16(packet, 2) != 1009) return;
            lock (client.ItemSyncRoot) HandleRunePacket(client, packet);
        }
        private static void HandleRunePacket(GameState client, byte[] packet)
        {
            var itemuid = BitConverter.ToUInt32(packet, 8);
            var Postion = BitConverter.ToUInt32(packet, 12);
            switch (packet[20])
            {
                case 66://TrojionPassiveAndWater
                    {
                        //ConquerItem item;
                        //if (client.RunesItem.TryGetValue(itemuid, out item))//inreuns
                        //{
                        //    if (item.ID == 4010701)
                        //    {
                        //        client.Entity.SacrificePercent = (int)Postion;
                        //        // Postion is the value of gui selected
                        //        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Sacrifice);
                        //    }
                        //    GamePackets.CMsgVerifyCheck vc = new GamePackets.CMsgVerifyCheck(true)//this is the last version packet will not work in this patch 
                        //    {
                        //        ItemID = item.ID,
                        //        Postion = (byte)item.Position,
                        //        UID = item.RuneSkill,
                        //        Value = (byte)Postion,
                        //        unkown = item.UID
                        //    };
                        //    //vc.Buffer[32] = 3;
                        //    //vc.Buffer[58] = 1;
                        //    vc.Send(client);
                        //}
                        break;
                    }
                case 63:
                    RuneProgress.Compose(client, packet);
                    break;
                case 57:
                case 58:
                case 61:
                    Revora.Game.Features.RuneLoadout.Process(client, packet);
                    break;
            }
        }
        public static uint MulDiv(uint number, uint numerator, uint denominator)
        {
            return (number * numerator) / denominator;
        }
    }
}
