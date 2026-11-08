using System;
using System.Linq;
using Revora.Client;
using System.Threading;
using System.Diagnostics;
using Revora.Interfaces;
using System.Threading.Generic;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using System.Collections.Concurrent;

namespace Revora.Game
{
    public unsafe class Screen
    {
        private static TimerRule<GameState> MonsterBuffers, Guards, AliveMonsters, Items, FloorSpells;
        public static void CreateTimerFactories()
        {
            MonsterBuffers = new TimerRule<GameState>(monsterBuffersCallback, 500);
            Guards = new TimerRule<GameState>(guardsCallback, 700);
            AliveMonsters = new TimerRule<GameState>(aliveMonstersCallback, 500);
            Items = new TimerRule<GameState>(itemsCallback, 1000);
            FloorSpells = new TimerRule<GameState>(FloorSpellsCallBack, 100);
        }
        private static void FloorSpellsCallBack(GameState client, int time)
        {
            if (!client.Socket.Alive)
            {
                client.Screen.DisposeTimers();
                return;
            }
            if (client.Entity == null) return;
            if (client.Map == null) return;
            Time32 Now = new Time32(time);
            if (client.Entity.FloorSpells.Count != 0)
            {
                foreach (var ID in client.Entity.FloorSpells)
                {
                    switch (ID.Key)
                    {

                        #region AuroraLotus Skill
                        case 12370://AuroraLouts:
                            {
                                var spellclient = ID.Value;
                                Queue<Game.Attacking.FloorSpell> RemoveSpells = new Queue<Game.Attacking.FloorSpell>();
                                foreach (var spell in spellclient.Spells)
                                {
                                    if (spellclient.CheckInvocke(Now, spell))
                                    {
                                        RemoveSpells.Enqueue(spell);
                                        spellclient.CreateMsgSpell(100);
                                        var attack = new Attack(true);
                                        attack.Attacker = client.Entity.UID;
                                        attack.AttackType = Attack.Melee;
                                        foreach (Interfaces.IMapObject _obj in client.Screen.Objects)
                                        {
                                            if (_obj == null) continue;
                                            if (_obj.MapObjType == MapObjectType.Player)
                                            {
                                                var attacked = _obj as Entity;
                                                if (Kernel.GetDistance(spell.FloorPacket.X, spell.FloorPacket.Y, attacked.X, attacked.Y) <= 15)
                                                {
                                                    if (!Revora.Game.Features.PlayerRevival.HandleLotus(client.Entity, attacked, spell.FloorPacket.MapID))
                                                        attacked.BringToLifeLotus();

                                                }
                                            }
                                        }
                                        spellclient.SendView(client);

                                        spell.FloorPacket.Type = FloorItem.RemoveEffect;
                                        client.SendScreen(spell.FloorPacket, true);
                                    }
                                }
                                while (RemoveSpells.Count > 0)
                                    spellclient.RemoveItem(RemoveSpells.Dequeue());
                                if (spellclient.Spells.Count == 0)
                                {
                                    Game.Attacking.FloorSpell.ClientFloorSpells FloorSpell;
                                    client.Entity.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                }
                                break;
                            }
                        #endregion

                        #region FlameLotus Skill
                        case 12380://FlameLouts:
                            {
                                var spellclient = ID.Value;
                                Queue<Game.Attacking.FloorSpell> RemoveSpells = new Queue<Game.Attacking.FloorSpell>();
                                foreach (var spell in spellclient.Spells)
                                {
                                    if (spellclient.CheckInvocke(Now, spell))
                                    {
                                        RemoveSpells.Enqueue(spell);
                                        spellclient.CreateMsgSpell(100);
                                        var attack = new Attack(true);
                                        attack.Attacker = client.Entity.UID;
                                        attack.AttackType = Attack.Melee;
                                        foreach (Interfaces.IMapObject _obj in client.Screen.Objects)
                                        {
                                            if (_obj == null) continue;
                                            if (_obj.MapObjType == MapObjectType.Monster)
                                            {
                                                var attacked = _obj as Entity;
                                                if (Kernel.GetDistance(spell.FloorPacket.X, spell.FloorPacket.Y, attacked.X, attacked.Y) <= 15)
                                                {
                                                    if (!Game.Attacking.Handle.CanAttack(client.Entity, attacked, spell.DBSkill, false)) continue;
                                                    attack.Effect = Attack.AttackEffects.None;
                                                    uint damage = Game.Attacking.Calculate.Magic(client.Entity, attacked, spell.DBSkill, ref attack);
                                                    attack.Damage = damage / 2;
                                                    #region Monster and Boss Attack
                                                    /*   if (attacked.EntityFlag == EntityFlag.Boss)
                                                    {
                                                        damage = 1;
                                                    }*/
                                                    if (attacked.EntityFlag == EntityFlag.Monster)
                                                    {
                                                        damage = (damage * 3);
                                                    }
                                                    #endregion
                                                    spellclient.SpellPacket.Effect = attack.Effect;
                                                    Game.Attacking.Handle.ReceiveAttack(client.Entity, attacked, attack, ref damage, spell.DBSkill);
                                                    spellclient.SpellPacket.AddTarget(attacked.UID, damage, attack);
                                                }
                                            }
                                            if (_obj.MapObjType == MapObjectType.Player)
                                            {
                                                var attacked = _obj as Entity;
                                                if (Kernel.GetDistance(spell.FloorPacket.X, spell.FloorPacket.Y, attacked.X, attacked.Y) <= 15)
                                                {
                                                    if (!Game.Attacking.Handle.CanAttack(client.Entity, attacked, spell.DBSkill, false)) continue;
                                                    attack.Effect = Attack.AttackEffects.None;
                                                    uint damage = Game.Attacking.Calculate.Magic(client.Entity, attacked,spell.DBSkill, ref attack);
                                                    attack.Damage = damage / 2;
                                                    spellclient.SpellPacket.Effect = attack.Effect;
                                                    Game.Attacking.Handle.ReceiveAttack(client.Entity, attacked, attack, ref damage, spell.DBSkill);
                                                    spellclient.SpellPacket.AddTarget(attacked.UID, damage, attack);
                                                }
                                            }
                                        }
                                        spellclient.SendView(client);

                                        spell.FloorPacket.Type = FloorItem.RemoveEffect;
                                        client.SendScreen(spell.FloorPacket, true);
                                    }
                                }
                                while (RemoveSpells.Count > 0)
                                    spellclient.RemoveItem(RemoveSpells.Dequeue());
                                if (spellclient.Spells.Count == 0)
                                {
                                    Game.Attacking.FloorSpell.ClientFloorSpells FloorSpell;
                                    client.Entity.FloorSpells.TryRemove(spellclient.DBSkill.ID, out FloorSpell);
                                }
                                break;
                            }
                        #endregion

                    }
                }
            }
            foreach (var Cloud in client.Map.Clouds.Values)
            {
                if (Cloud == null) continue;
                if (Kernel.GetDistance(Cloud.X, Cloud.Y, client.Entity.X, client.Entity.Y) > 16) continue;
                if (DateTime.Now > Cloud.EndTime || Cloud.Owner == null)
                {
                    Cloud.DisposeCloud(client);
                    return;
                }
                #region ThunderCloud
                if (!Cloud.Dead && Time32.Now > Cloud.LastCloudAttack.AddSeconds(1))
                {
                    var spell = Database.SpellTable.GetSpell(12970, client);
                    SpellUse suse = new SpellUse(true);
                    ////suse.Attacker = client.Player.Cloud.UID;
                    suse.SpellID = 13190;
                    suse.SpellLevel = 0;
                    suse.X = client.Entity.Cloud.X;
                    suse.Y = client.Entity.Cloud.Y;
                    Entity attacked = null;
                    foreach (Interfaces.IMapObject _obj in client.Screen.Objects)
                    {
                        if (_obj == null) continue;
                        if (_obj.MapObjType == MapObjectType.Monster )
                        {
                            Cloud.LastCloudAttack = Time32.Now;
                            attacked = _obj as Entity;
                            if (Kernel.GetDistance(Cloud.X, Cloud.Y, attacked.X, attacked.Y) <= 15)
                            {
                                Attack attack = new Attack(true);
                                if (Game.Attacking.Handle.CanAttack(Cloud.Owner.Entity, attacked, spell, attack.AttackType == Attack.Melee))
                                {
                                    attack.Effect = Attack.AttackEffects.None;
                                    uint damage = Game.Attacking.Calculate.Melee(Cloud.Owner.Entity, attacked, spell, ref attack);
                                    if (Cloud.ThunderBolt)
                                        damage += Game.Attacking.Calculate.Melee(Cloud.Owner.Entity, attacked, Database.SpellTable.GetSpell(12970, client), ref attack);
                                    suse.Effect = attack.Effect;
                                    Game.Attacking.Handle.ReceiveAttack(Cloud.Owner.Entity, attacked, attack, ref damage, spell);
                                    suse.AddTarget(attacked.UID, damage, attack);
                                    break;
                                }
                            }
                        }
                        if (_obj == null) continue;
                        if (_obj.MapObjType == MapObjectType.Player)
                        {
                            Cloud.LastCloudAttack = Time32.Now;
                            attacked = _obj as Entity;
                            if (Kernel.GetDistance(Cloud.X, Cloud.Y, attacked.X, attacked.Y) <= 15)
                            {
                                Attack attack = new Attack(true);
                                if (Game.Attacking.Handle.CanAttack(Cloud.Owner.Entity, attacked, spell, attack.AttackType == Attack.Melee))
                                {
                                    attack.Effect = Attack.AttackEffects.None;
                                    uint damage = Game.Attacking.Calculate.Melee(Cloud.Owner.Entity, attacked, spell, ref attack)/5;
                                    damage = (uint)(damage * 1.3);
                                    if (Cloud.ThunderBolt)
                                        damage += Game.Attacking.Calculate.Melee(Cloud.Owner.Entity, attacked, Database.SpellTable.GetSpell(12970, client), ref attack)/5;                                    
                                    suse.Effect = attack.Effect;
                                    Game.Attacking.Handle.ReceiveAttack(Cloud.Owner.Entity, attacked, attack, ref damage, spell);
                                    suse.AddTarget(attacked.UID, damage, attack);
                                    break;
                                }
                            }
                        }
                    }
                    client.SendScreen(suse, true);
                }
                #endregion
            }
        }
        private static void monsterBuffersCallback(GameState client, int time)
        {
            if (!client.Socket.Alive)
            {
                client.Screen.DisposeTimers(); return;
            }
            if (client.Entity == null) return;
            if (client.Map == null) return;
            if (client.Map.FreezeMonsters) return;
            #region Stamina
            if (client.Entity.StaminaStamp.Next(500, time: time))
            {
                if (client.Vigor < client.MaxVigor)
                {
                    ushort amount = (ushort)(3 + (client.Entity.Action == Game.Enums.ConquerAction.Sit ? 2 : 0));
                    if (client.Vigor + amount > client.MaxVigor)
                    {
                        amount = client.MaxVigor;
                        client.Vigor = amount;
                    }
                    else
                    {
                        client.Vigor += amount;
                    }
                    Vigor vigor = new Vigor(true);
                    vigor.Amount = client.Vigor;
                    vigor.Send(client);
                }
                if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.FrostArrows))
                    return;
                if (!client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride) && !client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Fly) || client.Equipment.TryGetItem(18) != null)
                {
                    int limit = 0;
                    if (client.Entity.HeavenBlessing > 0)
                        limit = 60;
                    if (client.Entity.Stamina != 100 + limit)
                    {
                        if (client.Entity.Action == Enums.ConquerAction.Sit)
                        {
                            if (client.Entity.Stamina <= 90 + limit)
                            {
                                client.Entity.Stamina += 10;
                            }
                            else
                            {
                                if (client.Entity.Stamina != 100 + limit)
                                    client.Entity.Stamina = (byte)(100 + limit);
                            }
                        }
                        else
                        {
                            if (client.Entity.Stamina <= 95 + limit)
                            {
                                client.Entity.Stamina += 5;
                            }
                            else
                            {
                                if (client.Entity.Stamina != 100 + limit)
                                    client.Entity.Stamina = (byte)(100 + limit);
                            }
                        }
                    }
                    client.Entity.StaminaStamp = new Time32(time);
                }
            }
            #endregion
            #region Water/Fire Taoist Epic Energy
            if (Database.DataHolder.IsTaoist(client.Entity.Class))
            {
                var Date = DateTime.Now;
                if (client.Entity.LastGetEnergy.AddSeconds(1) <= Date)
                {
                    if (client.Entity.EpicTaoist())
                    {
                        if (client.Entity.Class >= 130 && client.Entity.Class <= 135)
                        {
                            if (220 > client.Entity.Energy)
                            {
                                client.Entity.Energy += 4;
                            }
                        }
                        else
                        {
                            if (client.Entity.Energy < 330)
                            {
                                client.Entity.Energy += 7;
                            }
                        }
                        client.Entity.LastGetEnergy = Date;
                    }
                    else
                    {
                        if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.AuroraLotus))
                        {
                            client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.AuroraLotus);
                        }
                        if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.FlameLotus))
                        {
                            client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.FlameLotus);
                        }
                        if (client.Entity.Energy > 0)
                        {
                            client.Entity.Energy = 0;
                        }
                    }
                }
            }
            #endregion
            #region Unbound AutoCPs System
            if (Time32.Now > client.CpsStamp.AddSeconds(3))
            {
                if (client.Entity.MapID == 19989)
                {
                    if (client.Entity.Hitpoints != 0)
                    {
                        if (client.Entity.BoundCps <= 2000000000)
                        {
                            client.Entity.BoundCps += 2;
                            client.CpsStamp = Time32.Now;
                            client.Send(new Message("You have obtained 2 CPs for staying here!", System.Drawing.Color.Red, Message.TopLeft));
                        }
                        else
                        {
                            client.Send(new Message("You already have 2,000,000,000 CPs and cannot obtain more!", System.Drawing.Color.Red, Message.TopLeft));
                        }
                    }
                }
            }
            #endregion
            if (client.Map.ID == 8880 || client.Map.ID == 8881)
            {
                SafeDictionary<uint, PokerTable> ToRem = new SafeDictionary<uint, PokerTable>(40);
                foreach (PokerTable T in PokerTables.Values)
                {
                    if (client.Map.ID == T.Map)
                        if (Kernel.GetDistance(T.X, T.Y, client.Entity.X, client.Entity.Y) > Constants.nScreenDistance)
                            ToRem.Add(T.Id, T);
                }
                foreach (PokerTable T in ToRem.Values)
                    if (PokerTables.ContainsKey(T.Id))
                        PokerTables.Remove(T.Id);
            }
            foreach (IMapObject obj in client.Screen.Objects)
            {
                if (obj != null)
                {
                    if (obj.MapObjType == MapObjectType.Monster)
                    {
                        Entity monster = obj as Entity;
                        if (monster == null) continue;

                        if (monster.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma))
                        {
                            if (monster.StigmaStamp.AddSeconds(monster.StigmaTime).Next(time: time) || monster.Dead)
                            {
                                monster.StigmaTime = 0;
                                monster.StigmaIncrease = 0;
                                monster.RemoveFlag((ulong)FlagsUpdate.Flags.Stigma);
                            }
                        }
                        if (monster.ContainsFlag((ulong)FlagsUpdate.Flags.Dodge))
                        {
                            if (monster.DodgeStamp.AddSeconds(monster.DodgeTime).Next(time: time) || monster.Dead)
                            {
                                monster.DodgeTime = 0;
                                monster.DodgeIncrease = 0;
                                monster.RemoveFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.Dodge);
                            }
                        }
                        if (monster.ContainsFlag((ulong)FlagsUpdate.Flags.Invisibility))
                        {
                            if (monster.InvisibilityStamp.AddSeconds(monster.InvisibilityTime).Next(time: time) || monster.Dead)
                            {
                                monster.RemoveFlag((ulong)FlagsUpdate.Flags.Invisibility);
                            }
                        }
                        if (monster.ContainsFlag((ulong)FlagsUpdate.Flags.StarOfAccuracy))
                        {
                            if (monster.StarOfAccuracyTime != 0)
                            {
                                if (monster.StarOfAccuracyStamp.AddSeconds(monster.StarOfAccuracyTime).Next(time: time) || monster.Dead)
                                {
                                    monster.RemoveFlag((ulong)FlagsUpdate.Flags.StarOfAccuracy);
                                }
                            }
                            else
                            {
                                if (monster.AccuracyStamp.AddSeconds(monster.AccuracyTime).Next(time: time) || monster.Dead)
                                {
                                    monster.RemoveFlag((ulong)FlagsUpdate.Flags.StarOfAccuracy);
                                }
                            }
                        }
                        if (monster.ContainsFlag((ulong)FlagsUpdate.Flags.MagicShield))
                        {
                            if (monster.MagicShieldTime != 0)
                            {
                                if (monster.MagicShieldStamp.AddSeconds(monster.MagicShieldTime).Next(time: time) || monster.Dead)
                                {
                                    monster.MagicShieldIncrease = 0;
                                    monster.MagicShieldTime = 0;
                                    monster.RemoveFlag((ulong)FlagsUpdate.Flags.MagicShield);
                                }
                            }
                            else
                            {
                                if (monster.ShieldStamp.AddSeconds(monster.ShieldTime).Next(time: time) || monster.Dead)
                                {
                                    monster.ShieldIncrease = 0;
                                    monster.ShieldTime = 0;
                                    monster.RemoveFlag((ulong)FlagsUpdate.Flags.MagicShield);
                                }
                            }
                        }
                        if (monster.Dead || monster.Killed)
                        {
                            if (!monster.ContainsFlag((ulong)FlagsUpdate.Flags.Ghost) || monster.Killed)
                            {
                                monster.Killed = false;
                                monster.MonsterInfo.InSight = 0;
                                monster.AddFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.Ghost,StatusFlagsBigVector32.PermanentFlag,false);
                                monster.AddFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.Dead, StatusFlagsBigVector32.PermanentFlag, false);
                                monster.AddFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.FadeAway, StatusFlagsBigVector32.PermanentFlag, false);
                                Network.GamePackets.Attack attack = new Network.GamePackets.Attack(true);
                                attack.Attacker = monster.Killer.UID;
                                attack.Attacked = monster.UID;
                                attack.AttackType = Network.GamePackets.Attack.Kill;
                                attack.X = monster.X;
                                attack.Y = monster.Y;
                                client.Map.Floor[monster.X, monster.Y, MapObjectType.Monster, monster] = true;
                                attack.KOCount = Features.MonsterKoCounter.Snapshot(monster);
                                if (monster.Killer.EntityFlag == EntityFlag.Player)
                                {
                                    monster.MonsterInfo.ExcludeFromSend = monster.Killer.UID;
                                    monster.Killer.Owner.Send(attack);
                                }
                                monster.MonsterInfo.SendScreen(attack);
                                monster.MonsterInfo.ExcludeFromSend = 0;
                            }
                            if (monster.DeathStamp.AddSeconds(4).Next(time: time))
                            {
                                Data data = new Data(true);
                                data.UID = monster.UID;
                                data.ID = Network.GamePackets.Data.RemoveEntity;
                                monster.MonsterInfo.SendScreen(data);
                            }
                        }
                    }
                }
            }
        }
        private static void guardsCallback(GameState client, int time)
        {
            if (!client.Socket.Alive)
            {
                client.Screen.DisposeTimers(); return;
            }
            if (client.Entity == null) return;
            if (client.Map == null) return;
            if (client.Map.FreezeMonsters) return;
            Time32 Now = new Time32(time);
            foreach (IMapObject obj in client.Screen.Objects)
            {
                if (obj != null)
                {
                    if (obj.MapObjType == MapObjectType.Monster)
                    {
                        Entity monster = obj as Entity;
                        if (!monster.Companion && !monster.Dead && !monster.Killed && monster.MonsterInfo.Guard && !monster.MonsterInfo.Boss && monster.Name != "NemesisTyrant" && monster.Name != "SwordMaster" && monster.Name != "ThrillingSpook" && monster.Name != "LavaBeast" && monster.Name != "SnowBanshee" && monster.Name != "PurpleBanshee" && monster.Name != "SnowBansheeSoul" && monster.Name != "TeratoDragon" && Now >= monster.MonsterInfo.LastMove.AddMilliseconds(monster.MonsterInfo.MinimumSpeed))
                        {
                            if (Now >= monster.MonsterInfo.LastMove.AddMilliseconds(monster.MonsterInfo.MinimumSpeed))
                            {
                                if (monster.MonsterInfo.InSight == 0)
                                {
                                    if (monster.X != monster.MonsterInfo.BoundX || monster.Y != monster.MonsterInfo.BoundY)
                                    {
                                        monster.X = monster.MonsterInfo.BoundX;
                                        monster.Y = monster.MonsterInfo.BoundY;
                                        TwoMovements jump = new TwoMovements();
                                        jump.X = monster.MonsterInfo.BoundX;
                                        jump.Y = monster.MonsterInfo.BoundY;
                                        jump.EntityCount = 1;
                                        jump.FirstEntity = monster.UID;
                                        jump.MovementType = TwoMovements.Jump;
                                        client.SendScreen(jump, true);
                                    }
                                    if (Revora.Game.Features.CityGuardSystem.IsGuardTarget(client, monster))
                                        monster.MonsterInfo.InSight = client.Entity.UID;
                                }
                                else
                                {
                                    if (Revora.Game.Features.CityGuardSystem.IsGuardTarget(client, monster))
                                    {
                                        if (monster.MonsterInfo.InSight == client.Entity.UID)
                                        {
                                            if (!client.Entity.Dead)
                                            {
                                                if (Now >= monster.MonsterInfo.LastMove.AddMilliseconds(monster.MonsterInfo.AttackSpeed))
                                                {
                                                    short distance = Kernel.GetDistance(monster.X, monster.Y, client.Entity.X, client.Entity.Y);

                                                    if (distance <= monster.MonsterInfo.AttackRange)
                                                    {
                                                        monster.MonsterInfo.LastMove = Time32.Now;
                                                        new Game.Attacking.Handle(null, monster, client.Entity);
                                                    }
                                                    else
                                                    {
                                                        if (distance <= monster.MonsterInfo.ViewRange)
                                                        {
                                                            short dx = (short)(client.Entity.X - monster.X);
                                                            short dy = (short)(client.Entity.Y - monster.Y);
                                                            ushort nx = monster.X, ny = monster.Y;
                                                            if (Math.Abs(dx) >= Math.Abs(dy))
                                                                nx = (ushort)(monster.X + Math.Sign(dx));
                                                            else
                                                                ny = (ushort)(monster.Y + Math.Sign(dy));
                                                            TwoMovements jump = new TwoMovements();
                                                            jump.X = nx;
                                                            jump.Y = ny;
                                                            monster.X = nx;
                                                            monster.Y = ny;
                                                            jump.EntityCount = 1;
                                                            jump.FirstEntity = monster.UID;
                                                            jump.MovementType = TwoMovements.Jump;
                                                            client.SendScreen(jump, true);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (monster.MonsterInfo.InSight == client.Entity.UID)
                                        {
                                            monster.MonsterInfo.InSight = 0;
                                        }
                                        Revora.Game.Features.CityGuardSystem.NoteHidden(client, true);
                                    }
                                }

                                foreach (IMapObject obj2 in client.Screen.Objects)
                                {
                                    if (obj2 == null) continue;
                                    if (obj2.MapObjType == MapObjectType.Monster)
                                    {
                                        Entity monster2 = obj2 as Entity;

                                        if (monster2 == null) continue;
                                        if (monster2.Dead) continue;

                                        if (Now >= monster.MonsterInfo.LastMove.AddMilliseconds(monster.MonsterInfo.AttackSpeed))
                                        {
                                            if (!monster2.MonsterInfo.Guard && (!monster2.Companion || monster2.Owner.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.FlashingName)))
                                            {
                                                short distance = Kernel.GetDistance(monster.X, monster.Y, monster2.X, monster2.Y);

                                                if (distance <= monster.MonsterInfo.AttackRange)
                                                {
                                                    monster.MonsterInfo.LastMove = Time32.Now;
                                                    new Game.Attacking.Handle(null, monster, monster2);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        internal static void DriveMonsterAt(Entity monster, Entity prey, Time32 Now)
        {
            if (monster == null || monster.MonsterInfo == null || prey == null)
            {
                if (monster != null && monster.MonsterInfo != null)
                    monster.MonsterInfo.InSight = 0;
                return;
            }
            if (prey.Dead)
            {
                monster.MonsterInfo.InSight = 0;
                return;
            }
            short distance = Kernel.GetDistance(monster.X, monster.Y, prey.X, prey.Y);
            if (distance > Constants.pScreenDistance)
            {
                monster.MonsterInfo.InSight = 0;
                return;
            }
            if (distance <= monster.MonsterInfo.AttackRange)
            {
                if (Now >= monster.MonsterInfo.LastMove.AddMilliseconds(monster.MonsterInfo.AttackSpeed))
                {
                    monster.MonsterInfo.LastMove = Time32.Now;
                    new Game.Attacking.Handle(null, monster, prey);
                }
                return;
            }
            if (distance > monster.MonsterInfo.ViewRange)
            {
                monster.MonsterInfo.InSight = 0;
                return;
            }
            int delay = monster.MonsterInfo.RunSpeed > 0 ? monster.MonsterInfo.RunSpeed : monster.MonsterInfo.MoveSpeed;
            if (Now < monster.MonsterInfo.LastMove.AddMilliseconds(delay))
                return;
            monster.MonsterInfo.LastMove = Time32.Now;
            Enums.ConquerAngle facing = Kernel.GetAngle(monster.X, monster.Y, prey.X, prey.Y);
            if (!monster.Move(facing))
            {
                facing = (Enums.ConquerAngle)Kernel.Random.Next(7);
                if (!monster.Move(facing))
                    return;
            }
            monster.Facing = facing;
            GroundMovement move = new GroundMovement(true);
            move.Direction = facing;
            move.UID = monster.UID;
            move.GroundMovementType = GroundMovement.Run;
            monster.MonsterInfo.SendScreen(move);
        }

        private static void aliveMonstersCallback(GameState client, int time)
        {
            if (!client.Socket.Alive)
            {
                client.Screen.DisposeTimers();
                return;
            }
            if (client.Entity == null) return;
            if (client.Map == null) return;
            if (client.Map.FreezeMonsters) return;
            Time32 Now = new Time32(time);
            foreach (IMapObject obj in client.Screen.Objects)
            {
                if (obj != null)
                {
                    if (obj.MapObjType == MapObjectType.Monster)
                    {
                        Entity monster = obj as Entity;
                        if (monster == null) continue;
                        if (monster.MonsterInfo.Guard || monster.Companion || monster.Dead) continue;
                        if (Revora.Game.Features.LivingWorldSystem.IsAdventurer(monster)) continue;
                        if (Revora.Game.Features.SnowBansheeEncounter.IsEncounter(monster)) continue;
                        if (Revora.Game.Features.PurpleBansheeEncounter.IsEncounter(monster)) continue;
                        if (Revora.Game.Features.DesertExpeditionEncounters.DriveOwned(client,monster,Now)) continue;
                        if (Revora.Game.Features.DefenseTalismanEncounters.DriveOwned(client,monster,Now)) continue;
                        if (Revora.Game.Features.AttackTalismanEncounters.DriveOwned(client,monster,Now)) continue;
                        if (Revora.Game.Features.TempestWingsEncounters.DriveOwned(client,monster,Now)) continue;
                        if (monster.MonsterInfo.Reviver)
                        {
                            if (client.Entity.Dead && Now > client.Entity.DeathStamp.AddSeconds(5))
                            {
                                client.Entity.BringToLife();
                                SpellUse use = new SpellUse(true);
                                use.Attacker = monster.UID;
                                use.X = client.Entity.X;
                                use.Y = client.Entity.Y;
                                use.SpellID = 1100;
                                use.AddTarget(client.Entity.UID, 0, null);
                                Kernel.SendWorldMessage(use, Server.GamePool, monster.MapID);
                            }
                            return;
                        }
                        short distance = Kernel.GetDistance(monster.X, monster.Y, client.Entity.X, client.Entity.Y);
                        if (distance > Constants.pScreenDistance)
                        {
                            client.Screen.Remove(obj);
                            continue;
                        }
                        // Wave Defense owns movement/attack timing, including unattended posts.
                        if (Revora.Game.Features.WaveDefenseSystem.InEvent(monster)) continue;
                        if (Revora.Game.Features.HeartEncounter.IsEncounter(monster) && Revora.Game.Features.HeartEncounter.BlockAttack(monster, client.Entity)) continue;
                        if (Revora.Game.Features.PlanetEncounter.IsEncounter(monster) && Revora.Game.Features.PlanetEncounter.BlockAttack(monster, client.Entity)) continue;
                        // Quest steles are stationary; private evil AI belongs only
                        // to the currently registered Warrior's scene.
                        if (Revora.Game.Features.WarriorEpicCombat.IsEncounter(monster) &&
                            (monster.MonsterInfo.ID == Revora.Game.Features.WarriorEpicCombat.SteleFamily ||
                             Revora.Game.Features.WarriorEpicCombat.BlockAttack(monster, client.Entity))) continue;
                        if (monster.MonsterInfo.InSight != 0 && monster.MonsterInfo.InSight != client.Entity.UID)
                        {
                            if (monster.MonsterInfo.InSight > 1000000)
                            {
                                GameState cl;
                                if (Kernel.GamePool.TryGetValue(monster.MonsterInfo.InSight, out cl)
                                    && cl != null && cl.Entity != null && !cl.Entity.Dead)
                                {
                                    short dst = Kernel.GetDistance(monster.X, monster.Y, cl.Entity.X, cl.Entity.Y);
                                    if (dst > Constants.pScreenDistance)
                                        monster.MonsterInfo.InSight = 0;
                                }
                                else
                                    monster.MonsterInfo.InSight = 0;
                            }
                            else
                            {
                                Entity companion = client.Map.Companions[monster.MonsterInfo.InSight];
                                if (companion != null)
                                {
                                    short dst = Kernel.GetDistance(monster.X, monster.Y, companion.X, companion.Y);
                                    if (dst > Constants.pScreenDistance)
                                        monster.MonsterInfo.InSight = 0;
                                }
                                else
                                    monster.MonsterInfo.InSight = 0;
                            }
                        }
                        if (Now >= monster.MonsterInfo.LastMove.AddMilliseconds(monster.MonsterInfo.MinimumSpeed))
                        {
                            if (distance <= Constants.pScreenDistance)
                            {
                                #region Companions
                                if (client.Pet != null)
                                {
                                    if (client.Pet.Pets != null)
                                    {
                                        foreach (var pet in client.Pet.Pets.Values)
                                        {
                                            if (pet != null)
                                            {
                                                #region Pets
                                                if (pet.Entity.Companion && !pet.Entity.Dead)
                                                {
                                                    short distance2 = Kernel.GetDistance(monster.X, monster.Y, pet.Entity.X, pet.Entity.Y);
                                                    if (distance > distance2 || client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Invisibility) || client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Fly))
                                                    {
                                                        if (monster.MonsterInfo.InSight == 0)
                                                        {
                                                            monster.MonsterInfo.InSight = pet.Entity.UID;
                                                        }
                                                        else
                                                        {
                                                            if (monster.MonsterInfo.InSight == pet.Entity.UID)
                                                            {
                                                                if (distance2 > Constants.pScreenDistance)
                                                                {
                                                                    monster.MonsterInfo.InSight = 0;
                                                                }
                                                                else
                                                                {
                                                                    if (distance2 <= monster.MonsterInfo.AttackRange)
                                                                    {
                                                                        if (Now >= monster.MonsterInfo.LastMove.AddMilliseconds(monster.MonsterInfo.AttackSpeed))
                                                                        {
                                                                            monster.MonsterInfo.LastMove = Time32.Now;
                                                                            new Game.Attacking.Handle(null, monster, pet.Entity);
                                                                            if (Time32.Now >= monster.MonsterInfo.Lastpop.AddSeconds(30))
                                                                            {
                                                                                monster.MonsterInfo.Lastpop = Time32.Now;

                                                                                continue;
                                                                            }
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        if (distance2 > monster.MonsterInfo.ViewRange / 2)
                                                                        {
                                                                            if (distance2 < Constants.pScreenDistance)
                                                                            {
                                                                                if (Now >= monster.MonsterInfo.LastMove.AddMilliseconds(monster.MonsterInfo.RunSpeed))
                                                                                {
                                                                                    monster.MonsterInfo.LastMove = Time32.Now;

                                                                                    Enums.ConquerAngle facing = Kernel.GetAngle(monster.X, monster.Y, pet.Entity.X, pet.Entity.Y);
                                                                                    if (!monster.Move(facing))
                                                                                    {
                                                                                        facing = (Enums.ConquerAngle)Kernel.Random.Next(7);
                                                                                        if (monster.Move(facing))
                                                                                        {
                                                                                            monster.Facing = facing;
                                                                                            GroundMovement move = new GroundMovement(true);
                                                                                            move.Direction = facing;
                                                                                            move.UID = monster.UID;
                                                                                            move.GroundMovementType = GroundMovement.Run;
                                                                                            monster.MonsterInfo.SendScreen(move);
                                                                                            continue;
                                                                                        }
                                                                                    }
                                                                                    else
                                                                                    {
                                                                                        monster.Facing = facing;
                                                                                        GroundMovement move = new GroundMovement(true);
                                                                                        move.Direction = facing;
                                                                                        move.UID = monster.UID;
                                                                                        move.GroundMovementType = GroundMovement.Run;
                                                                                        monster.MonsterInfo.SendScreen(move);
                                                                                        continue;
                                                                                    }
                                                                                }
                                                                            }
                                                                            else
                                                                            {
                                                                                monster.MonsterInfo.InSight = 0;
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            if (Now >= monster.MonsterInfo.LastMove.AddMilliseconds(monster.MonsterInfo.MoveSpeed))
                                                                            {
                                                                                monster.MonsterInfo.LastMove = Time32.Now;
                                                                                Enums.ConquerAngle facing = Kernel.GetAngle(monster.X, monster.Y, pet.Entity.X, pet.Entity.Y);
                                                                                if (!monster.Move(facing))
                                                                                {
                                                                                    facing = (Enums.ConquerAngle)Kernel.Random.Next(7);
                                                                                    if (monster.Move(facing))
                                                                                    {
                                                                                        monster.Facing = facing;
                                                                                        GroundMovement move = new GroundMovement(true);
                                                                                        move.Direction = facing;
                                                                                        move.UID = monster.UID;
                                                                                        monster.MonsterInfo.SendScreen(move);
                                                                                        continue;
                                                                                    }
                                                                                }
                                                                                else
                                                                                {
                                                                                    monster.Facing = facing;
                                                                                    GroundMovement move = new GroundMovement(true);
                                                                                    move.Direction = facing;
                                                                                    move.UID = monster.UID;
                                                                                    monster.MonsterInfo.SendScreen(move);
                                                                                    continue;
                                                                                }
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                                #endregion
                                            }

                                        }
                                    }
                                }
                                #endregion
                                #region Player
                                if (monster.MonsterInfo.InSight == 0)
                                {
                                    bool playerOk = distance <= monster.MonsterInfo.ViewRange
                                        && !client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Invisibility)
                                        && (monster.MonsterInfo.SpellID != 0 || !client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Fly));
                                    if (playerOk)
                                        monster.MonsterInfo.InSight = client.Entity.UID;
                                    else
                                    {
                                        GameState world = Revora.Game.Features.CityGuardSystem.NearestWorldActor(monster, monster.MonsterInfo.ViewRange);
                                        if (world != null && world.Entity != null)
                                            monster.MonsterInfo.InSight = world.Entity.UID;
                                    }
                                }
                                else if (monster.MonsterInfo.InSight != client.Entity.UID)
                                {
                                    GameState prey;
                                    if (Kernel.GamePool.TryGetValue(monster.MonsterInfo.InSight, out prey)
                                        && prey != null && prey.Entity != null && !prey.Entity.Dead)
                                        DriveMonsterAt(monster, prey.Entity, Now);
                                    else
                                        monster.MonsterInfo.InSight = 0;
                                }
                                else
                                {
                                    if (monster.MonsterInfo.InSight == client.Entity.UID)
                                    {
                                        if (monster.MonsterInfo.SpellID == 0 && client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Fly))
                                        {
                                            monster.MonsterInfo.InSight = 0;
                                            return;
                                        }
                                        DriveMonsterAt(monster, client.Entity, Now);
                                    }
                                }
                                #endregion
                            }
                        }
                    }
                    else if (obj.MapObjType == MapObjectType.Item)
                    {
                        FloorItem item = obj as FloorItem;
                        if (item == null) continue;

                        if (item.Type == FloorItem.Effect)
                        {
                            #region RageOfWar
                            if (item.ItemID == FloorItem.RageOfWarTrap)
                            {
                                if (item.Owner == client)
                                {
                                    if (item.OnFloor.AddSeconds(2).Next(time: time))
                                    {
                                        var spell = Database.SpellTable.GetSpell(12930, client);
                                        if (spell == null) return;
                                        var attack = new Attack(true);
                                        attack.Attacker = client.Entity.UID;
                                        attack.AttackType = Attack.Melee;

                                        foreach (var obj1 in client.Screen.Objects)
                                        {
                                            if (obj1 == null)
                                                continue;
                                            var attacked = obj1 as Entity;
                                            if (attacked == null) continue;
                                            if (Kernel.GetDistance(obj1.X, obj1.Y, obj.X, obj.Y) <= 3)
                                            {
                                                if (Attacking.Handle.CanAttack(client.Entity, attacked, spell, attack.AttackType == Attack.Melee))
                                                {
                                                    Attacking.Handle.RageOfWarTrap(item);

                                                }

                                            }
                                        }
                                    }
                                }
                            }
                            #endregion
                            if (item.ItemID == FloorItem.DaggerStorm || item.ItemID == FloorItem.FuryofEgg || item.ItemID == FloorItem.ShacklingIce || item.ItemID == 31)
                            {
                                if (item.Owner == client)
                                {
                                    if (Time32.Now > item.UseTime.AddSeconds(1))
                                    {
                                        item.UseTime = Time32.Now;
                                        var spell = Database.SpellTable.GetSpell(11600, client);
                                        var attack = new Attack(true);
                                        attack.Attacker = item.Owner.Entity.UID;
                                        attack.AttackType = Attack.Melee;
                                        foreach (var obj1 in client.Screen.Objects)
                                        {
                                            if (Kernel.GetDistance(obj1.X, obj1.Y, obj.X, obj.Y) <= 3)
                                            {
                                                if (obj1.MapObjType == MapObjectType.Monster || obj1.MapObjType == MapObjectType.Player)
                                                {
                                                    var attacked = obj1 as Entity;
                                                    if (Attacking.Handle.CanAttack(client.Entity, attacked, spell, false))
                                                    {
                                                        uint damage = Game.Attacking.Calculate.Melee(client.Entity, attacked, spell, ref attack);
                                                        damage = (uint)((damage) / 12);
                                                        attack.Damage = damage;
                                                        attack.Attacked = attacked.UID;
                                                        attack.X = attacked.X;
                                                        attack.Y = attacked.Y;
                                                        Attacking.Handle.ReceiveAttack(client.Entity, attacked, attack, ref damage, spell);
                                                    }
                                                }
                                                else if (obj1.MapObjType == MapObjectType.SobNpc)
                                                {
                                                    var attacked = obj1 as SobNpcSpawn;
                                                    if (Attacking.Handle.CanAttack(client.Entity, attacked, spell))
                                                    {
                                                        uint damage = Game.Attacking.Calculate.Melee(client.Entity, attacked, ref attack);
                                                        damage = (uint)(damage * spell.PowerPercent);
                                                        attack.Damage = damage;
                                                        attack.Attacked = attacked.UID;
                                                        attack.X = attacked.X;
                                                        attack.Y = attacked.Y;
                                                        Attacking.Handle.ReceiveAttack(client.Entity, attacked, attack, damage, spell);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            
                            if (item.ItemID == FloorItem.FlameLotus)
                            {
                                if (item.Owner == client)
                                {
                                    if (item.OnFloor.AddSeconds(8).Next(time: time))
                                    {
                                        item.UseTime = Time32.Now;
                                        var spell = Database.SpellTable.GetSpell(12380, client);

                                        var attack = new Attack(true);
                                        attack.Attacker = item.Owner.Entity.UID;
                                        attack.AttackType = Attack.Melee;

                                        foreach (var obj1 in client.Screen.Objects)
                                        {
                                            if (Kernel.GetDistance(obj1.X, obj1.Y, obj.X, obj.Y) <= 3)
                                            {
                                                if (obj1.MapObjType == MapObjectType.Player)
                                                {
                                                    var attacked = obj1 as Entity;
                                                    if (Attacking.Handle.CanAttack(client.Entity, attacked, spell, false))
                                                    {
                                                        uint damage = Game.Attacking.Calculate.Melee(client.Entity, attacked,spell, ref attack) / 2;
                                                        damage = (damage * (uint)1.2);
                                                        attack.Damage = damage;
                                                        attack.Attacked = attacked.UID;
                                                        attack.X = attacked.X;
                                                        attack.Y = attacked.Y;

                                                        Attacking.Handle.ReceiveAttack(client.Entity, attacked, attack, ref damage, spell);
                                                    }
                                                }
                                                if (obj1.MapObjType == MapObjectType.Monster)
                                                {
                                                    var attacked = obj1 as Entity;
                                                    if (Attacking.Handle.CanAttack(client.Entity, attacked, spell, false))
                                                    {
                                                        uint damage = Game.Attacking.Calculate.Melee(client.Entity, attacked, spell,ref attack);
                                                        damage = (damage * (uint)1.2);
                                                        attack.Damage = damage;
                                                        attack.Attacked = attacked.UID;
                                                        attack.X = attacked.X;
                                                        attack.Y = attacked.Y;

                                                        Attacking.Handle.ReceiveAttack(client.Entity, attacked, attack, ref damage, spell);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if (item.ItemID == FloorItem.ShadowOfChaser)
                            {
                                if (item.Owner == client)
                                {
                                    var spell = Database.SpellTable.GetSpell(13090, client);
                                    var attack = new Attack(true);
                                    attack.Attacker = item.Owner.Entity.UID;
                                    attack.AttackType = Attack.Melee;
                                    foreach (var obj1 in client.Screen.Objects)
                                    {
                                        if (Kernel.GetDistance(obj1.X, obj1.Y, obj.X, obj.Y) <= spell.Range)
                                        {
                                            if (obj1.MapObjType == MapObjectType.Monster || obj1.MapObjType == MapObjectType.Player)
                                            {
                                                var attacked = obj1 as Entity;
                                                if (Attacking.Handle.CanAttack(client.Entity, attacked, spell, false))
                                                {
                                                    uint damage = Attacking.Calculate.Melee(client.Entity, attacked, spell, ref attack);
                                                    damage = (uint)(damage * 0.6);
                                                    attack.Damage = damage;
                                                    attack.Attacked = attacked.UID;
                                                    attack.X = attacked.X;
                                                    attack.Y = attacked.Y;
                                                    Attacking.Handle.ReceiveAttack(client.Entity, attacked, attack, ref damage, spell);
                                                }
                                            }
                                            else if (obj1.MapObjType == MapObjectType.SobNpc)
                                            {
                                                var attacked = obj1 as SobNpcSpawn;
                                                if (Attacking.Handle.CanAttack(client.Entity, attacked, spell))
                                                {
                                                    uint damage = Game.Attacking.Calculate.Melee(client.Entity, attacked, ref attack);
                                                    damage = (uint)(damage * 0.3);
                                                    attack.Damage = damage;
                                                    attack.Attacked = attacked.UID;
                                                    attack.X = attacked.X;
                                                    attack.Y = attacked.Y;
                                                    Attacking.Handle.ReceiveAttack(client.Entity, attacked, attack, damage, spell);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        private static void itemsCallback(GameState client, int time)
        {
            if (!client.Socket.Alive)
            {
                client.Screen.DisposeTimers(); return;
            }
            if (client.Entity == null) return;
            if (client.Map == null) return;
            if (client.Map.FreezeMonsters) return;
            Time32 Now = new Time32(time);
            int goldCollected = 0;
            foreach (IMapObject obj in client.Screen.Objects)
            {
                if (obj != null)
                {
                    if (obj.MapObjType == MapObjectType.Item)
                    {
                        FloorItem item = obj as FloorItem;
                        if (item == null) continue;
                        if (goldCollected < 8 && client.Entity.InAutoHunt
                            && Features.GoldPickup.TryCollect(client, item, true))
                        {
                            goldCollected++;
                            continue;
                        }
                        if (item.Type == FloorItem.Effect)
                        {
                            if (item.ItemID == FloorItem.RageOfWarTrap)
                            {
                                if (item.OnFloor.AddSeconds(2).Next(time: time))
                                {
                                    item.Type = Network.GamePackets.FloorItem.RemoveEffect;
                                    client.Map.RemoveFloorItem(item);
                                    client.RemoveScreenSpawn(item, true);
                                }
                            }
                            if (item.ItemID == FloorItem.DaggerStorm || item.ItemID == FloorItem.FuryofEgg || item.ItemID == FloorItem.ShacklingIce || item.ItemID == 31)
                            {
                                if (item.OnFloor.AddSeconds(4).Next(time: time))
                                {
                                    item.Type = FloorItem.RemoveEffect;
                                    client.Map.RemoveFloorItem(item);
                                    client.RemoveScreenSpawn(item, true);
                                }
                            }
                            if (item.ItemID == FloorItem.FlameLotus ||
                          item.ItemID == FloorItem.AuroraLotus)
                            {
                                if (item.OnFloor.AddSeconds(8).Next(time: time))
                                {
                                    IEnumerable<Client.GameState> array = null;
                                    Database.SpellInformation Spell = null;
                                    if (item.ItemID == FloorItem.FlameLotus)
                                    {
                                        Spell = Database.SpellTable.GetSpell(12380, 6);
                                        if (item.Owner.Team != null)
                                        {
                                            array = Kernel.GamePool.Values.Where
                                            (x =>
                                            Kernel.GetDistance(x.Entity.X, x.Entity.Y, item.X, item.Y) <= Spell.Range &&
                                            x.Entity.Dead == true &&
                                            item.Owner.Team.IsTeammate(x.Entity.UID)
                                            );
                                        }
                                        else if (item.Owner.Guild != null)
                                        {
                                            array = Kernel.GamePool.Values.Where
                                            (x =>
                                            Kernel.GetDistance(x.Entity.X, x.Entity.Y, item.X, item.Y) <= Spell.Range &&
                                            x.Entity.Dead == true && x.Entity.GuildID == item.Owner.Entity.GuildID
                                            );
                                        }
                                        else if (item.Owner.Entity.GetClan != null)
                                        {
                                            array = Kernel.GamePool.Values.Where
                                            (x =>
                                            Kernel.GetDistance(x.Entity.X, x.Entity.Y, item.X, item.Y) <= Spell.Range &&
                                            x.Entity.Dead == true && x.Entity.ClanId == item.Owner.Entity.ClanId
                                            );
                                        }
                                        else
                                        {
                                            array = Kernel.GamePool.Values.Where
                                            (x =>
                                            Kernel.GetDistance(x.Entity.X, x.Entity.Y, item.X, item.Y) <= Spell.Range &&
                                            x.Entity.Dead == true
                                            );
                                        }
                                    }

                                    if (item.ItemID == FloorItem.AuroraLotus)
                                    {
                                        Spell = Database.SpellTable.GetSpell(12370, 6);
                                        if (item.Owner.Team != null)
                                        {
                                            array = Kernel.GamePool.Values.Where
                                            (x =>
                                            Kernel.GetDistance(x.Entity.X, x.Entity.Y, item.X, item.Y) <= Spell.Range &&
                                            x.Entity.Dead == true &&
                                            item.Owner.Team.IsTeammate(x.Entity.UID)
                                            );
                                        }
                                        else if (item.Owner.Guild != null)
                                        {
                                            array = Kernel.GamePool.Values.Where
                                            (x =>
                                            Kernel.GetDistance(x.Entity.X, x.Entity.Y, item.X, item.Y) <= Spell.Range &&
                                            x.Entity.Dead == true && x.Entity.GuildID == item.Owner.Entity.GuildID
                                            );
                                        }
                                        else if (item.Owner.Entity.GetClan != null)
                                        {
                                            array = Kernel.GamePool.Values.Where
                                            (x =>
                                            Kernel.GetDistance(x.Entity.X, x.Entity.Y, item.X, item.Y) <= Spell.Range &&
                                            x.Entity.Dead == true && x.Entity.ClanId == item.Owner.Entity.ClanId
                                            );
                                        }
                                        else
                                        {
                                            array = Kernel.GamePool.Values.Where
                                            (x =>
                                            Kernel.GetDistance(x.Entity.X, x.Entity.Y, item.X, item.Y) <= Spell.Range &&
                                            x.Entity.Dead == true
                                            );
                                        }
                                    }
                                    foreach (GameState pClient in array)
                                    {
                                        Database.SpellInformation spell = null;
                                        if (pClient == null) return;
                                        if (pClient.Entity == null) return;
                                        if (pClient.Entity.UID != item.Owner.Entity.UID)
                                        {
                                            if (Spell.ID == 12370)
                                            {
                                                if (Revora.Game.Features.PlayerRevival.HandleLotus(item.Owner.Entity, pClient.Entity, item.MapID)) continue;
                                                SpellUse suse = new SpellUse(true);
                                                suse.Attacker = item.Owner.Entity.UID;
                                                suse.SpellID = 1100;
                                                suse.SpellLevel = 0;
                                                suse.X = pClient.Entity.X;
                                                suse.Y = pClient.Entity.Y;
                                                suse.AddTarget(pClient.Entity.UID, 0, null);
                                                pClient.Entity.Action =
                                                Revora.Game.Enums.ConquerAction.None;
                                                pClient.ReviveStamp = Time32.Now;
                                                pClient.Attackable = false;
                                                pClient.Entity.TransformationID = 0;
                                                pClient.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Dead);
                                                pClient.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ghost);
                                                pClient.Entity.Hitpoints = pClient.Entity.MaxHitpoints;
                                                pClient.Entity.Ressurect();
                                                pClient.SendScreen(suse, true);
                                                pClient.Entity.AddFlag((ulong)FlagsUpdate.Flags.Stigma, 25, true);
                                                pClient.Entity.AddFlag((ulong)FlagsUpdate.Flags.MagicShield,25,true);
                                                pClient.Entity.AddFlag((ulong)FlagsUpdate.Flags.StarOfAccuracy,25,true);
                                                pClient.Entity.StigmaStamp = Time32.Now;
                                                pClient.Entity.StarOfAccuracyTime = 25;
                                                pClient.Entity.StigmaTime = 25;
                                                pClient.Entity.MagicShieldTime = 25;
                                                pClient.Entity.StigmaIncrease = 15;
                                                pClient.Entity.MagicShieldIncrease = 15;
                                                pClient.Entity.ShieldTime = 0;
                                                pClient.Entity.ShieldTime = 0;
                                                pClient.Entity.AccuracyTime = 0;
                                                pClient.Entity.AccuracyStamp = Time32.Now;
                                                pClient.Entity.StarOfAccuracyStamp = Time32.Now;
                                                pClient.Entity.ShieldStamp = Time32.Now;
                                                pClient.Entity.MagicShieldStamp = Time32.Now;
                                                if (pClient.Entity.EntityFlag == EntityFlag.Player)
                                                    pClient.Entity.Owner.Send(Constants.Shield(spell.PowerPercent, spell.Duration));
                                                    pClient.Entity.Owner.Send(Constants.Accuracy(spell.Duration));
                                                pClient.Entity.Owner.Send(Constants.Stigma(spell.PowerPercent, spell.Duration));
                                            }
                                        }
                                    }
                                    item.Type = Network.GamePackets.FloorItem.RemoveEffect;
                                    client.Map.RemoveFloorItem(item);
                                    client.RemoveScreenSpawn(item, true);
                                }
                            }
                            else
                                if (item.ItemID == FloorItem.AuroraLotus || item.ItemID == FloorItem.FlameLotus) return;
                            if (item.ItemID == 1397)
                            {
                                if (item.OnFloor.AddSeconds(3).Next(time: time))
                                {
                                    Attacking.Handle.InfroEcho(item);
                                    item.Type = FloorItem.RemoveEffect;
                                    client.Map.RemoveFloorItem(item);
                                    client.RemoveScreenSpawn(item, true);
                                }
                            }
                            if (item.ItemID == FloorItem.ShadowOfChaser)
                            {
                                if (item.OnFloor.AddMilliseconds(900).Next(time: time))
                                {
                                    item.Type = FloorItem.RemoveEffect;
                                    client.Map.RemoveFloorItem(item);
                                    client.RemoveScreenSpawn(item, true);
                                }
                            }
                            if (item.ItemID == FloorItem.InfernalEcho)
                            {
                                if (item.OnFloor.AddSeconds(3).Next(time: time))
                                {
                                    Attacking.Handle.InfroEcho(item);
                                    item.Type = FloorItem.RemoveEffect;
                                    client.Map.RemoveFloorItem(item);
                                    client.RemoveScreenSpawn(item, true);
                                }
                            }
                            if (item.ItemID == FloorItem.ActOfTarget)
                            {
                                if (item.OnFloor.AddSeconds(3).Next(time: time))
                                {
                                    Attacking.Handle.InfroEcho(item);
                                    item.Type = FloorItem.RemoveEffect;
                                    client.Map.RemoveFloorItem(item);
                                    client.RemoveScreenSpawn(item, true);
                                }
                            }
                            if (item.ItemID == FloorItem.ActOfTarget2)
                            {
                                if (item.OnFloor.AddSeconds(3).Next(time: time))
                                {
                                    Attacking.Handle.InfroEcho(item);
                                    item.Type = FloorItem.RemoveEffect;
                                    client.Map.RemoveFloorItem(item);
                                    client.RemoveScreenSpawn(item, true);
                                }
                            }
                            
                            if (item.ItemID == FloorItem.TwilightDance)
                            {
                                if (item.OnFloor.AddMilliseconds(500).Next(time: time))
                                {
                                    item.Type = FloorItem.RemoveEffect;
                                    client.Map.RemoveFloorItem(item);
                                    client.RemoveScreenSpawn(item, true);
                                }
                            }
                            if (item.ItemID == FloorItem.TideTrap)
                            {
                                if (item.OnFloor.AddMilliseconds(1500).Next(time: time))
                                {
                                    Attacking.Handle.TideTrap(item);
                                    item.Type = FloorItem.RemoveEffect;
                                    client.Map.RemoveFloorItem(item);
                                    client.RemoveScreenSpawn(item, true);
                                }
                            }
                            if (item.ItemID == FloorItem.SeaBurial)
                            {
                                if (item.OnFloor.AddMilliseconds(2500).Next(time: time))
                                {
                                    Attacking.Handle.SeaBurial(item);
                                }
                            }
                            else
                            {
                                if (item.Expired(time))
                                {
                                    item.Type = FloorItem.Remove;
                                    foreach (IMapObject _obj in client.Screen.Objects)
                                        if (_obj != null)
                                            if (_obj.MapObjType == MapObjectType.Player)
                                                (_obj as Entity).Owner.Send(item);
                                    client.Map.RemoveFloorItem(item);
                                    client.Screen.Remove(item);
                                }
                            }
                        }
                    }
                }
            }
        }
        private IDisposable[] TimerSubscriptions;
        private object DisposalSyncRoot;
        public ConcurrentDictionary<uint, Game.Statue> Statue = new ConcurrentDictionary<uint, Statue>();
        private Interfaces.IMapObject[] _objects;
        private static ConcurrentDictionary<uint, PokerTable> PokerTables;
        public Interfaces.IMapObject[] Objects
        {
            get { return _objects; }
        }
        private ConcurrentDictionary<uint, Interfaces.IMapObject> _objectDictionary;
        public Client.GameState Owner;
        public Screen(Client.GameState client)
        {
            Owner = client;
            _objects = new Interfaces.IMapObject[0];
            PokerTables = new ConcurrentDictionary<uint, PokerTable>();
            _objectDictionary = new ConcurrentDictionary<uint, IMapObject>();
            TimerSubscriptions = new IDisposable[] 
            {
                MonsterBuffers.Add(client),
                Guards.Add(client),
                AliveMonsters.Add(client),
                Items.Add(client),
                FloorSpells.Add(client)
            };
            DisposalSyncRoot = new object();
        }
        ~Screen()
        {
            DisposeTimers();
            Clear();
            _objects = null;
            _objectDictionary = null;
            Owner = null;
        }
        public void DisposeTimers()
        {
            if (DisposalSyncRoot == null) return;
            lock (DisposalSyncRoot)
            {
                if (TimerSubscriptions == null) return;
                for (int i = 0; i < TimerSubscriptions.Length; i++)
                {
                    if (TimerSubscriptions[i] != null)
                    {
                        TimerSubscriptions[i].Dispose();
                        TimerSubscriptions[i] = null;
                    }
                }
            }
        }
        private void updateBase()
        {
            _objects = _objectDictionary.Values.ToArray();
        }
        public bool Add(Interfaces.IMapObject _object)
        {
            if (_object == null) return false;
            if (_objectDictionary.ContainsKey(_object.UID))
                if (_objectDictionary[_object.UID] == null)
                    _objectDictionary.Remove(_object.UID);
            if (!_objectDictionary.ContainsKey(_object.UID))
            {
                if (Kernel.GetDistance(_object.X, _object.Y, Owner.Entity.X, Owner.Entity.Y) <= Constants.pScreenDistance)
                {
                    _objectDictionary[_object.UID] = _object;
                    updateBase();
                    return true;
                }
            }
            return false;
        }
        public bool Remove(Interfaces.IMapObject _object)
        {
            if (_object == null) return false;
            if (_objectDictionary.Remove(_object.UID))
            {
                updateBase();
                if (_object.MapObjType == MapObjectType.Item)
                {
                    FloorItem item = _object as FloorItem;
                    if (item.Type >= FloorItem.Effect)
                    {
                        item.Type = FloorItem.RemoveEffect;
                        Owner.Send(item);
                    }
                    else
                    {
                        item.Type = FloorItem.Remove;
                        Owner.Send(item);
                        item.Type = FloorItem.Drop;
                    }
                }
                else if (_object.MapObjType == MapObjectType.Player)
                {
                    Owner.Send(new Data(true)
                    {
                        UID = _object.UID,
                        ID = Network.GamePackets.Data.RemoveEntity
                    });
                }
                else if (_object.MapObjType == MapObjectType.StaticEntity)
                {
                    Owner.Send(new Data(true)
                    {
                        UID = _object.UID,
                        ID = Network.GamePackets.Data.RemoveEntity
                    });
                }
                return true;
            }
            return false;
        }
        public bool TryGetValue(uint uid, out Entity entity)
        {
            entity = null;
            Interfaces.IMapObject imo = null;
            if (_objectDictionary.TryGetValue(uid, out imo))
            {
                if (imo == null)
                {
                    _objectDictionary.Remove(uid);
                    updateBase();
                    return false;
                }
                if (imo is Entity)
                {
                    entity = imo as Entity;
                    return true;
                }
            }
            return false;
        }
        public bool GetRaceObject(Func<IMapObject, bool> predicate, out StaticEntity entity)
        {
            entity = null;
            foreach (var obj in Objects)
                if (obj is StaticEntity)
                    if (predicate(obj))
                        entity = obj as StaticEntity;
            return entity != null;
        }
        public bool TryGetSob(uint uid, out SobNpcSpawn sob)
        {
            sob = null;
            Interfaces.IMapObject imo = null;
            if (_objectDictionary.TryGetValue(uid, out imo))
            {
                if (imo == null)
                {
                    _objectDictionary.Remove(uid);
                    updateBase();
                    return false;
                }
                if (imo is SobNpcSpawn)
                {
                    sob = imo as SobNpcSpawn;
                    return true;
                }
            }
            return false;
        }
        public bool TryGetFloorItem(uint uid, out FloorItem item)
        {
            item = null;
            Interfaces.IMapObject imo = null;
            if (_objectDictionary.TryGetValue(uid, out imo))
            {
                if (imo == null)
                {
                    _objectDictionary.Remove(uid);
                    updateBase();
                    return false;
                }
                if (imo is FloorItem)
                {
                    item = imo as FloorItem;
                    return true;
                }
            }
            return false;
        }
        public IEnumerable<T> Select<T>(MapObjectType type) where T : class
        {
            foreach (var obj in Objects)
                if (obj != null)
                    if (obj.MapObjType == type)
                        yield return obj as T;
        }
        public IEnumerable<T> Where<T>(Func<IMapObject, bool> predicate) where T : class
        {
            foreach (var obj in Objects)
                if (obj != null)
                    if (predicate(obj))
                        yield return obj as T;
        }
        public IEnumerable<T> SelectWhere<T>(MapObjectType type, Func<T, bool> predicate) where T : class
        {
            foreach (var obj in Objects)
                if (obj != null)
                    if (obj.MapObjType == type)
                        if (predicate(obj as T))
                            yield return obj as T;
        }
        public bool Contains(Interfaces.IMapObject _object)
        {
            if (_object == null) return false;
            return _objectDictionary.ContainsKey(_object.UID);
        }
        public bool Contains(uint uid)
        {
            return _objectDictionary.ContainsKey(uid);
        }
        public void FullWipe()
        {
            foreach (IMapObject Base in Objects)
            {
                if (Base == null) continue;
                Data data = new Data(true);
                data.UID = Base.UID;
                data.ID = Data.RemoveEntity;
                Owner.Send(data);
                if (Base.MapObjType == Game.MapObjectType.Player)
                {
                    GameState pPlayer = Base.Owner as GameState;
                    pPlayer.Screen.Remove(Owner.Entity);
                }
            }
            Clear();
        }
        public void Clear()
        {
            _objectDictionary.Clear();
            _objects = new IMapObject[0];
        }
        public void Reload(Interfaces.IPacket spawnWith)
        {
            CleanUp(spawnWith);
            if (Owner.Entity.MapID == 1002)
            {
                foreach (var statue in Game.Statue.Statues.Values)
                {
                    if (statue > Owner)
                    {
                        Statue.TryAdd(statue.UID, statue);
                    }
                    else if (statue < Owner)
                    {
                        Game.Statue astatue;
                        Statue.TryRemove(statue.UID, out astatue);
                    }
                }
            }
            else
            {
                if (Statue.Count > 0)
                    Statue.Clear();
            }
            try
            {
                foreach (GameState pClient in Kernel.GamePool.Values)
                {
                    if (pClient == null || pClient.Entity == null) continue;
                    if (Owner == null || Owner.Entity == null) return;
                    if (pClient.Entity.UID == Owner.Entity.UID) continue;
                    if (pClient.Entity.MapID != Owner.Entity.MapID) continue;
                    {
                            short dist = Kernel.GetDistance(pClient.Entity.X, pClient.Entity.Y, Owner.Entity.X, Owner.Entity.Y);
                            if (dist <= Constants.pScreenDistance && !Contains(pClient.Entity))
                            {
                                if (pClient.Fake)
                                {
                                    try
                                    {
                                        pClient.Entity.SendSpawn(Owner);
                                    }
                                    catch
                                    {
                                    }
                                    continue;
                                }
                                if (pClient.Guild != null)
                                    pClient.Guild.SendName(Owner);
                                if (Owner.Guild != null)
                                    Owner.Guild.SendName(pClient);
                                if (pClient.Entity.InteractionInProgress && pClient.Entity.InteractionWith != Owner.Entity.UID && pClient.Entity.InteractionSet)
                                {
                                    if (pClient.Entity.Body == 2005 || pClient.Entity.Body == 1006)
                                    {
                                        if (pClient.Entity.InteractionX == pClient.Entity.X && pClient.Entity.Y == pClient.Entity.InteractionY)
                                        {
                                            Attack atak = new Attack(true);
                                            atak.Attacker = pClient.Entity.UID;
                                            atak.Attacked = pClient.Entity.InteractionWith;
                                            atak.X = pClient.Entity.X;
                                            atak.Y = pClient.Entity.Y;
                                            atak.AttackType = 49;
                                            atak.Damage = pClient.Entity.InteractionType;
                                            Owner.Send(atak);
                                        }
                                    }
                                    else
                                    {
                                        if (Kernel.GamePool.ContainsKey(pClient.Entity.InteractionWith))
                                        {
                                            Client.GameState Cs = Kernel.GamePool[pClient.Entity.InteractionWith] as Client.GameState;
                                            if (Cs.Entity.X == pClient.Entity.InteractionX && pClient.Entity.Y == pClient.Entity.InteractionY)
                                            {
                                                Attack atak = new Attack(true);
                                                atak.Attacker = pClient.Entity.UID;
                                                atak.Attacked = pClient.Entity.InteractionWith;
                                                atak.X = pClient.Entity.X;
                                                atak.Y = pClient.Entity.Y;
                                                atak.AttackType = 49;
                                                atak.Damage = pClient.Entity.InteractionType;
                                                Owner.Send(atak);
                                            }
                                        }
                                    }
                                }
                                if (pClient.Pet != null && pClient.Pet.Pets != null && pClient.Pet.Pets.Count > 0)
                                {
                                    foreach (var pet in pClient.Pet.Pets.Values)
                                    {
                                        if (pet == null) continue;
                                        if (pet.Entity == null) continue;
                                        if (Kernel.GetDistance(pet.Entity.X, pet.Entity.Y, Owner.Entity.X, Owner.Entity.Y) <= 18 && !Contains(pet.Entity.UID))
                                        {
                                            if (!pet.Entity.Dead)
                                                pet.Entity.SendSpawn(Owner);
                                        }
                                    }
                                }
                                if (pClient.Map.BaseID == 700)
                                {
                                    if (Owner.InQualifier())
                                    {
                                        if (pClient.InQualifier())
                                        {
                                            Owner.Entity.SendSpawn(pClient);
                                            pClient.Entity.SendSpawn(Owner);
                                            if (pClient.Guild != null)
                                                Owner.Entity.SendSpawn(pClient, false);
                                            if (Owner.Guild != null)
                                                pClient.Entity.SendSpawn(Owner, false);
                                            if (spawnWith != null)
                                                pClient.Send(spawnWith);
                                        }
                                        else
                                        {
                                            Owner.Entity.SendSpawn(pClient);
                                            if (pClient.Guild != null)
                                                Owner.Entity.SendSpawn(pClient, false);
                                            Add(pClient.Entity);
                                            if (spawnWith != null)
                                                pClient.Send(spawnWith);
                                        }
                                    }
                                    else
                                    {
                                        if (pClient.InQualifier())
                                        {
                                            pClient.Entity.SendSpawn(Owner);
                                            if (Owner.Guild != null)
                                                pClient.Entity.SendSpawn(Owner, false);
                                            pClient.Screen.Add(Owner.Entity);
                                            if (spawnWith != null)
                                                Owner.Send(spawnWith);
                                        }
                                        else
                                        {
                                            Owner.Entity.SendSpawn(pClient);
                                            pClient.Entity.SendSpawn(Owner);
                                            if (pClient.Guild != null)
                                                Owner.Entity.SendSpawn(pClient, false);
                                            if (Owner.Guild != null)
                                                pClient.Entity.SendSpawn(Owner, false);
                                            if (spawnWith != null)
                                                pClient.Send(spawnWith);
                                        }
                                    }
                                }
                                else
                                {
                                    Owner.Entity.SendSpawn(pClient);
                                    pClient.Entity.SendSpawn(Owner);
                                    if (pClient.Guild != null)
                                        Owner.Entity.SendSpawn(pClient, false);
                                    if (Owner.Guild != null)
                                        pClient.Entity.SendSpawn(Owner, false);
                                    if (spawnWith != null)
                                        pClient.Send(spawnWith);
                                }
                            }
                    }
                }
                var Map = Owner.Map;
                #region Npcs
                foreach (Interfaces.INpc npc in Map.Npcs.Values)
                {
                    if (npc == null) continue;
                    if (Kernel.GetDistance(npc.X, npc.Y, Owner.Entity.X, Owner.Entity.Y) > 16) continue;
                    if (Contains(npc.UID)) continue;
                    npc.SendSpawn(Owner, false);
                }
                #endregion
                #region ThunderCloud
                foreach (var Cloud in Map.Clouds.Values)
                {
                    if (Cloud == null) continue;
                    if (Kernel.GetDistance(Cloud.X, Cloud.Y, Owner.Entity.X, Owner.Entity.Y) > 16) continue;
                    Cloud.SendSpawn(Owner, false);
                }
                #endregion
                #region Items + map effects
                foreach (var item in Map.FloorItems.Values)
                {
                    if (item == null) continue;
                    if (Kernel.GetDistance(item.X, item.Y, Owner.Entity.X, Owner.Entity.Y) > 16) continue;
                    if (Contains(item.UID)) continue;
                    if (item.Type == FloorItem.Effect)
                    {
                        #region Water/Fire Taoist
                        if (item.ItemID == FloorItem.FlameLotus || item.ItemID == FloorItem.AuroraLotus)
                        {
                            if (item.OnFloor.AddSeconds(8).Next(time: Time32.Now.AllMilliseconds()))
                            {
                                item.Type = Network.GamePackets.FloorItem.RemoveEffect;
                                foreach (Interfaces.IMapObject _obj in Objects)
                                    if (_obj != null)
                                        if (_obj.MapObjType == MapObjectType.Player)
                                            (_obj as Entity).Owner.Send(item);
                                Map.RemoveFloorItem(item);
                            }
                            else
                                item.SendSpawn(Owner, false);
                        }
                        else
                        #endregion
                                #region TwilightDance
                                if (item.ItemID == FloorItem.TwilightDance)
                                {
                                    if (item.OnFloor.AddSeconds(1).Next(time: Time32.Now.AllMilliseconds()))
                                    {
                                        item.Type = Network.GamePackets.FloorItem.RemoveEffect;
                                        foreach (Interfaces.IMapObject _obj in Objects)
                                            if (_obj != null)
                                                if (_obj.MapObjType == MapObjectType.Player)
                                                    (_obj as Entity).Owner.Send(item);
                                        Map.RemoveFloorItem(item);
                                    }
                                    else
                                        item.SendSpawn(Owner, false);
                                }
                                else
                                #endregion
                                    if (item.ItemID == FloorItem.DaggerStorm || item.ItemID == FloorItem.FuryofEgg || item.ItemID == FloorItem.ShacklingIce || item.ItemID == 31)
                                    {
                                        if (item.OnFloor.AddSeconds(4).Next(time: Time32.Now.AllMilliseconds()))
                                        {
                                            item.Type = FloorItem.RemoveEffect;
                                            foreach (Interfaces.IMapObject _obj in Objects)
                                                if (_obj != null)
                                                    if (_obj.MapObjType == MapObjectType.Player)
                                                        (_obj as Entity).Owner.Send(item);
                                            Map.RemoveFloorItem(item);
                                        }
                                        else
                                            item.SendSpawn(Owner, false);
                                    }
                                    else
                                        item.SendSpawn(Owner, false);
                    }
                    else
                    {
                        if (item.Expired(Time32.Now.AllMilliseconds()) || item.PickedUpAlready)
                        {
                            item.Type = Network.GamePackets.FloorItem.Remove;
                            Map.RemoveFloorItem(item);
                        }
                    }
                    item.SendSpawn(Owner);
                }
                #endregion
                foreach (Game.Entity monster in Map.Entities.Values)
                {
                    if (monster == null) continue;
                    if (Kernel.GetDistance(monster.X, monster.Y, Owner.Entity.X, Owner.Entity.Y) <= 16 && !Contains(monster.UID))
                    {
                        if (!monster.Dead)
                        {
                            monster.SendSpawn(Owner, false);
                            if (monster.MaxHitpoints > 65535)
                            {
                                uint shown = monster.Hitpoints;
                                if (monster.Boss > 0)
                                {
                                    uint key = monster.MaxHitpoints / 10000;
                                    if (key != 0)
                                    {
                                        shown = monster.Hitpoints / key;
                                        if (shown < 1 && monster.Hitpoints > 0)
                                            shown = 1;
                                    }
                                }
                                FlagsUpdate upd = new FlagsUpdate(true)
                                {
                                    UID = monster.UID
                                };
                                upd.Append((byte)FlagsUpdate.DataType.Hitpoints, shown);
                                Owner.Send(upd);
                            }
                        }
                    }
                }
                #region RaceItems
                if (Owner.Map.StaticEntities.Count != 0)
                {
                    foreach (var item in Owner.Map.StaticEntities.Values)
                    {
                        if (item == null) continue;
                        if (!item.Viable) continue;
                        if (Kernel.GetDistance(item.X, item.Y, Owner.Entity.X, Owner.Entity.Y) > 16) continue;
                        if (Contains(item.UID)) continue;
                        item.SendSpawn(Owner);
                    }
                }
                #endregion
                foreach (Game.Entity monster in Map.Companions.Values)
                {
                    if (monster == null) continue;
                    if (Kernel.GetDistance(monster.X, monster.Y, Owner.Entity.X, Owner.Entity.Y) <= 18 && !Contains(monster.UID))
                    {
                        if (!monster.Dead)
                        {
                            monster.SendSpawn(Owner);
                        }
                    }
                }
                if (Owner.Map.ID == 8880 || Owner.Map.ID == 8881)
                {
                    foreach (Game.PokerTable T in Kernel.PokerTables.Values)
                    {
                        if (T.Map == Owner.Map.ID)
                        {
                            if (Kernel.GetDistance(T.X, T.Y, Owner.Entity.X, Owner.Entity.Y) <= Constants.nScreenDistance && !PokerTables.ContainsKey(T.Id))
                            {
                                Owner.Send(Game.PokerPackets.PokerTable(T));
                                PokerTables.Add(T.Id, T);
                            }
                        }
                    }
                }
                if (Owner.Map.ID == Roulette.Database.Roulettes.RouletteTable.MapID)
                {
                    foreach (var R in Roulette.Database.Roulettes.RoulettesPoll.Values)
                    {
                        if (Kernel.GetDistance(R.SpawnPacket.X, R.SpawnPacket.Y, Owner.Entity.X, Owner.Entity.Y) <= Constants.nScreenDistance && !PokerTables.ContainsKey(R.SpawnPacket.UID))
                        {
                            Owner.Send(R.SpawnPacket);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
        }
        public void CleanUp(Interfaces.IPacket spawnWith)
        {
            bool remove;
            try
            {
                foreach (IMapObject Base in Objects)
                {
                    if (Base == null) continue;
                    remove = false;
                    if (Base.MapObjType == MapObjectType.Monster)
                    {
                        if ((Base as Entity).Dead)
                        {
                            if (Time32.Now > (Base as Entity).DeathStamp.AddSeconds(8))
                                remove = true;
                            else remove = false;
                        }
                        if (Kernel.GetDistance(Owner.Entity.X, Owner.Entity.Y, Base.X, Base.Y) >= Constants.remScreenDistance)
                            remove = true;
                        if (remove)
                        {
                            if ((Base as Entity).MonsterInfo.InSight == Owner.Entity.UID)
                                (Base as Entity).MonsterInfo.InSight = 0;
                        }
                    }
                    else if (Base.MapObjType == MapObjectType.Player)
                    {
                        if (remove = (Kernel.GetDistance(Owner.Entity.X, Owner.Entity.Y, Base.X, Base.Y) >= Constants.pScreenDistance))
                        {
                            GameState pPlayer = Base.Owner as GameState;
                            if (pPlayer != null && pPlayer.Screen != null)
                                pPlayer.Screen.Remove(Owner.Entity);
                        }
                    }
                    else if (Base.MapObjType == MapObjectType.Item)
                    {
                        remove = (Kernel.GetDistance(Owner.Entity.X, Owner.Entity.Y, Base.X, Base.Y) >= 22);
                    }
                    else
                    {
                        remove = (Kernel.GetDistance(Owner.Entity.X, Owner.Entity.Y, Base.X, Base.Y) >= Constants.remScreenDistance);
                    }
                    if (Base.MapID != Owner.Map.ID)
                        remove = true;
                    if (remove)
                    {
                        Remove(Base);
                    }
                }
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
        }
    }
}
