using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;
using R = Revora.Game.Features.AbyssTrialRules;
using S = Revora.Game.Features.AbyssTrialStore;
using M = Revora.Game.Features.AbyssTrialMaterials;
using J = Revora.Game.Features.AbyssTrialJournal;

namespace Revora.Game.Features
{
    internal static class AbyssTrialWorld
    {
        internal const uint FamilyOffset = 9920000;
        internal const int Capacity = 32;
        // Exact names and health shared with the privately staged 6711 profiles.
        // Distinct names preserve the original Cup/loose-monster presentation.
        internal static readonly string[] HudNames = {"Hill Spirit","Swift Devil","Vigor Banshee","Cleansing Devil",
            "Cursed Andrew","Cursed Peter","Cursed Philip","Cursed Timothy","Cursed Daphne","Cursed Victoria","Cursed Wayne","Cursed Theodore",
            "Sealed Satan","Beast Satan","Fury Satan"};
        internal static readonly uint[] HudHealth = {50000,60000,60460,65535,65535,65535,65535,65535,65535,65535,65535,65535,100000,150000,200000};
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        internal static Func<int, int> Roll = max => Kernel.Random.Next(max);
        private static bool enabled;
        private static readonly object gate = new object();
        internal sealed class Actor {
            internal S.Encounter Encounter; internal GameState Player; internal Map Map; internal Entity Body;
            internal DateTime Until; internal bool Released;
        }
        private sealed class Life { internal readonly Guid Id = Guid.NewGuid(); internal bool Credited; }
        private static readonly ConditionalWeakTable<Entity, Life> lives = new ConditionalWeakTable<Entity, Life>();
        internal static readonly ConcurrentDictionary<uint, Actor> Actors = new ConcurrentDictionary<uint, Actor>();
        internal static void Initialize() {
            Map map;
            if (!Kernel.Maps.TryGetValue(R.Map, out map) || map.ID != R.Map || map.BaseID != R.Map || map.Floor == null)
                throw new InvalidOperationException("Abyss world is missing.");
            using (var sha = SHA256.Create()) using (var file = File.OpenRead(Path.Combine(Constants.DMapsPath, "maps", "1700.map")))
                if (System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "") != "8026193EC8C8CB7F1934CCF7C2E713931A0C65269C8D7A0B67209019BED17D1B")
                    throw new InvalidOperationException("Abyss terrain differs from the reviewed floor.");
            for (uint family = 3600; family <= 3645; family++) if (!MonsterInformation.MonsterInformations.ContainsKey(family))
                throw new InvalidOperationException("Abyss creature catalog is missing.");
            foreach (uint family in Enumerable.Range(3632, 15).Select(n => (uint)n)) Profile(family);
            J.Initialize();
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_abyss_completions(
run uuid PRIMARY KEY REFERENCES revora_abyss_trials(id),uid bigint NOT NULL UNIQUE,
encounter uuid NOT NULL UNIQUE REFERENCES revora_abyss_encounters(id),completed_at timestamptz NOT NULL DEFAULT now());"); }
            enabled = true;
            foreach (var body in map.Entities.Values) Normalize(body);
        }
        // These are Revora combat values, not a claim of original CO balance.
        internal static MonsterInformation Profile(uint family) {
            if (family < 3632 || family > 3646) throw new ArgumentException("Unknown Abyss encounter profile.");
            uint template = family == 3646 ? 3647u : family;
            MonsterInformation original;
            if (!MonsterInformation.MonsterInformations.TryGetValue(template, out original)) throw new InvalidOperationException("Abyss model template missing.");
            ushort[] meshes = {153,909,951,168,153,153,168,151,132,328,129,154,166,200,103};
            if (original.Mesh != meshes[family - 3632]) throw new InvalidOperationException("Abyss model template changed.");
            var info = original.Copy(); info.ID = FamilyOffset + family; info.Guard = info.Reviver = false; info.Boss = true;
            info.Level = 120; info.SpellID = 0; info.AttackType = 1; info.ViewRange = 18; info.AttackRange = 3;
            info.AttackSpeed = 1800; info.MoveSpeed = info.RunSpeed = 600; info.RespawnTime = int.MaxValue / 2;
            info.OwnItemID = info.OwnItemRate = 0;
            if (family == 3633) { info.Hitpoints = 60000; info.MinAttack = 2000; info.MaxAttack = 4000; info.Defence = 200; }
            if (family >= 3644) {
                int form = (int)(family - 3644);
                info.Hitpoints = new uint[] {100000,150000,200000}[form]; info.MinAttack = (uint)(4500 + form * 1000);
                info.MaxAttack = info.MinAttack + 2000; info.Defence = (ushort)(2000 + form * 250);
            }
            info.Name=HudNames[family-3632];
            if (info.Hitpoints != HudHealth[family-3632] || info.MinAttack > info.MaxAttack) throw new InvalidOperationException("Abyss combat profile differs from its reviewed client HUD.");
            return info;
        }
        internal static void Normalize(Entity body) {
            if (!enabled || body == null || body.EntityFlag != EntityFlag.Monster || body.Companion || body.MapID != R.Map
                || body.MonsterInfo == null || body.MonsterInfo.ID != 3606 || body.Dead) return;
            var info = body.MonsterInfo.Copy(); info.Owner = body; info.Level = 120; info.Hitpoints = 50000;
            info.MinAttack = 1400; info.MaxAttack = 1728; info.Defence = 0; info.Guard = info.Reviver = info.Boss = false;
            body.MonsterInfo = info; body.Level = info.Level; body.MinAttack = info.MinAttack; body.MaxAttack = body.MagicAttack = info.MaxAttack;
            body.Defence = info.Defence; body.MaxHitpoints = body.Hitpoints = info.Hitpoints;
        }
        internal static bool IsEncounter(Entity body) {
            return body != null && body.EntityFlag == EntityFlag.Monster && body.MonsterInfo != null
                && body.MonsterInfo.ID >= FamilyOffset + 3632 && body.MonsterInfo.ID <= FamilyOffset + 3646;
        }
        private static bool OwnFighter(GameState p, Entity fighter) {
            if (!Q.Player(p) || !R.Eligible(p.Entity.Reborn,p.Entity.Level) || p.Trade==null || p.Trade.InTrade || p.Booth!=null
                || fighter == null || fighter.Dead || p.Entity.MapID != R.Map || fighter.MapID != R.Map || p.Map.BaseID != R.Map) return false;
            if (ReferenceEquals(fighter, p.Entity)) return true;
            Entity registered;
            return fighter.Companion && ReferenceEquals(fighter.Owner, p) && ReferenceEquals(p.Companion, fighter)
                && p.Map.Companions.TryGetValue(fighter.UID, out registered) && ReferenceEquals(registered, fighter);
        }
        private static bool Current(Actor a) {
            Entity registered; Map map;
            return !a.Released && Clock() < a.Until && Q.Player(a.Player, false) && R.Eligible(a.Player.Entity.Reborn,a.Player.Entity.Level) && a.Player.Entity.MapID == R.Map
                && Kernel.Maps.TryGetValue(R.Map, out map) && ReferenceEquals(map, a.Map) && ReferenceEquals(a.Player.Map, a.Map)
                && a.Map.Entities.TryGetValue(a.Body.UID, out registered) && ReferenceEquals(registered, a.Body)
                && ReferenceEquals(a.Body.MonsterInfo.Owner, a.Body) && !a.Body.Dead;
        }
        internal static bool BlockAttack(Entity attacker, Entity target) {
            Entity body = IsEncounter(attacker) ? attacker : IsEncounter(target) ? target : null;
            if (body == null) return false;
            Actor a;
            return !enabled || !Actors.TryGetValue(body.UID, out a) || !ReferenceEquals(a.Body, body) || !Current(a)
                || !OwnFighter(a.Player, ReferenceEquals(body, attacker) ? target : attacker);
        }
        private static bool Landing(Map map, ushort x, ushort y, out ushort rx, out ushort ry) {
            for (int radius = 3; radius <= 12; radius++) for (int dx = -radius; dx <= radius; dx++) for (int dy = -radius; dy <= radius; dy++) {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue; int tx = x + dx, ty = y + dy;
                if (tx < 0 || ty < 0 || tx > ushort.MaxValue || ty > ushort.MaxValue || !map.Floor[tx,ty,MapObjectType.Monster]
                    || map.Npcs.Values.Any(n => Kernel.GetDistance(n.X,n.Y,(ushort)tx,(ushort)ty) < 3)) continue;
                rx = (ushort)tx; ry = (ushort)ty; return true;
            }
            rx = ry = 0; return false;
        }
        internal static Actor Summon(GameState p, int version, uint family, M.Plan plan) {
            return SummonOffered(p,version,family,plan,null);
        }
        internal static bool InPublicWorld(Entity body) {
            Map map;
            return enabled && body!=null && body.EntityFlag==EntityFlag.Player && body.MapID==R.Map
                && Q.Player(body.Owner,false) && ReferenceEquals(body.Owner.Entity,body)
                && Kernel.Maps.TryGetValue(R.Map,out map) && ReferenceEquals(body.Owner.Map,map) && map.ID==R.Map && map.BaseID==R.Map;
        }
        internal static bool SuppressCrime(Entity attacker,Entity target) {
            return InPublicWorld(attacker) && InPublicWorld(target);
        }
        internal static bool ProtectDeathItem(Entity owner,uint item) {
            return item>=722721 && item<=722731 && InPublicWorld(owner);
        }
        internal static Actor SummonOffered(GameState p, int version, uint family, M.Plan plan, Func<bool> valid) {
            if (!enabled || !Q.Player(p) || p.Entity.MapID != R.Map || !Monitor.IsEntered(p.ItemSyncRoot)) throw new InvalidOperationException("Abyss summoner is not ready.");
            lock (gate) {
                var map=p.Map; uint npc=p.ActiveNpc; DateTime until;
                var old = Actors.Values.FirstOrDefault(existing => existing.Player.Entity.UID == p.Entity.UID);
                if (old != null && !Current(old)) { Release(old); old = null; }
                if (old == null && Actors.Count >= Capacity) return null;
                ushort x, y;
                if (old != null) { x = old.Body.X; y = old.Body.Y; }
                else { if (!Landing(p.Map, p.Entity.X, p.Entity.Y, out x, out y)) return null; }
                S.Encounter encounter;
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    encounter = S.Summon(c,t,p,version,family,plan); until=Clock().AddMinutes(30);
                    if (!ReferenceEquals(p.Map,map) || !Q.Near(p,npc,R.Map) || valid!=null && !valid()) throw new InvalidOperationException("Abyss summoner moved or offering expired during payment.");
                    S.Require(c,t,p); t.Commit();
                }}
                if (encounter.Created) M.Apply(p,plan,null);
                if (!Q.Player(p) || p.Entity.MapID!=R.Map || !ReferenceEquals(p.Map,map)) return null;
                if (old != null) {
                    if (old.Encounter.Id != encounter.Id) throw new InvalidOperationException("Abyss actor and saved offering differ.");
                    return old;
                }
                var info = Profile(family); info.BoundX = x; info.BoundY = y; info.BoundCX = info.BoundCY = 1; info.InSight = p.Entity.UID;
                var body = new Entity(EntityFlag.Monster,false) { UID=map.EntityUIDCounter.Next,MapID=R.Map,MonsterInfo=info,MapObjType=MapObjectType.Monster,
                    X=x,Y=y,Name=info.Name,Body=info.Mesh,Level=info.Level,MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,MagicAttack=info.MaxAttack,Defence=info.Defence };
                while (map.Entities.ContainsKey(body.UID) || map.Companions.ContainsKey(body.UID) || map.Npcs.ContainsKey(body.UID) || Actors.ContainsKey(body.UID)) body.UID=map.EntityUIDCounter.Next;
                info.Owner=body; body.MaxHitpoints=body.Hitpoints=info.Hitpoints; body.ApplyMonsterBossHud(); body.SendUpdates=true;
                var a = new Actor { Encounter=encounter,Player=p,Map=map,Body=body,Until=until };
                if (!Actors.TryAdd(body.UID,a)) throw new InvalidOperationException("Abyss actor identity collision.");
                try {
                    a.Map.AddEntity2(body);
                    foreach (var viewer in Kernel.GamePool.Values) if (Q.Player(viewer,false) && viewer.Entity.MapID==R.Map
                        && Kernel.GetDistance(viewer.Entity.X,viewer.Entity.Y,x,y)<Constants.nScreenDistance) body.SendSpawn(viewer,false);
                } catch { Release(a); throw; }
                return a;
            }
        }
        internal static void Release(Actor a) {
            lock (gate) {
                if (a.Released) return; a.Released=true; Entity current;
                if (a.Map.Entities.TryGetValue(a.Body.UID,out current) && ReferenceEquals(current,a.Body)) {
                    a.Body.MonsterInfo.SendScreen(new Data(true){UID=a.Body.UID,ID=Data.RemoveEntity}); a.Map.RemoveEntity(a.Body);
                }
                Actor ignored; Actors.TryRemove(a.Body.UID,out ignored);
            }
        }
        internal static void Tick() {
            if (!enabled) return;
            lock (gate) foreach (var a in Actors.Values.ToArray()) {
                if (!Current(a)) Release(a); else a.Body.MonsterInfo.InSight=a.Player.Entity.UID;
            }
        }
        internal static bool ClaimOne(GameState p) {
            if (!enabled || !Q.Player(p) || !Monitor.IsEntered(p.ItemSyncRoot)) return false;
            ConquerItem item;
            using (var c=DataHolder.MySqlConnection) { c.Open(); using(var t=c.BeginTransaction()) { item=S.Claim(c,t,p); t.Commit(); }}
            if (item==null) return false; M.Apply(p,null,item); return true;
        }
        internal static bool TryDie(Entity body, Entity killer) {
            if (!IsEncounter(body)) return false; Actor a;
            if (!enabled || !Actors.TryGetValue(body.UID,out a) || !ReferenceEquals(a.Body,body) || !OwnFighter(a.Player,killer)
                || !Monitor.TryEnter(a.Player.ItemSyncRoot)) return true;
            var p=a.Player;
            try {
                lock (gate) {
                    if (!Current(a) || !OwnFighter(p,killer) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance) return true;
                    using (var c=DataHolder.MySqlConnection) { c.Open(); using(var t=c.BeginTransaction()) {
                        if (!S.Defeat(c,t,p,a.Encounter.Id,a.Encounter.Family)) return true;
                        var r=S.Require(c,t,p);
                        EquipmentAchievements.RecordUnarmedVictory(c,t,p,body,killer,"abyss:"+a.Encounter.Id.ToString("N"));
                        if (r.Progress.Step==R.Stage.Complete) {
                            if (Q.Execute(c,t,"INSERT INTO revora_abyss_completions(run,uid,encounter) VALUES(@run,@uid,@encounter)",
                                "run",r.Id,"uid",(long)p.Entity.UID,"encounter",a.Encounter.Id)!=1) throw new InvalidOperationException("Abyss completion was not saved.");
                            ProgressionEvidence.Complete(c,t,p.Entity.UID,11031,"abyss-trial-v1",r.Id.ToString("N"),1);
                            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11031)","uid",(long)p.Entity.UID))
                                if (!Equals(q.ExecuteScalar(),true)) throw new InvalidOperationException("Abyss achievement was not saved.");
                        }
                        if (!Current(a) || !OwnFighter(p,killer) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance)
                            throw new InvalidOperationException("Abyss combat context changed.");
                        t.Commit();
                    }}
                    Release(a);
                }
                body.DieCore(p.Entity); ClaimOne(p); J.Refresh(p); Q.Refresh(p);
                p.Send(a.Encounter.Family==3646?"Satan's last form is broken. Return to Sereth in Twin City.":"The spirit is laid to rest. Your trial has been recorded.");
            } catch(Exception ex) { Q.Fail(p,"abyss-encounter",ex); }
            finally { Monitor.Exit(p.ItemSyncRoot); }
            return true;
        }
        internal static bool SuppressLegacy(Entity body, Entity killer) {
            return enabled && body!=null && body.MapID==R.Map && killer!=null && killer.Owner!=null
                && (J.Selected(killer.Owner) || killer.Owner.Entity.PersistenceRequiresReload);
        }
        private static bool Wild(Entity body, Entity killer) {
            if (!enabled || body==null || !body.Dead || body.Companion || body.EntityFlag!=EntityFlag.Monster || body.MonsterInfo==null
                || body.MapID!=R.Map || !ReferenceEquals(body.MonsterInfo.Owner,body) || body.MonsterInfo.Guard || body.MonsterInfo.Reviver
                || R.GrievanceWeight(body.MonsterInfo.ID)==0 || killer==null || !OwnFighter(killer.Owner,killer) || !J.Selected(killer.Owner)) return false;
            Entity current;
            return killer.Owner.Map.Entities.TryGetValue(body.UID,out current) && ReferenceEquals(current,body)
                && Kernel.GetDistance(killer.Owner.Entity.X,killer.Owner.Entity.Y,body.X,body.Y)<=Constants.pScreenDistance;
        }
        internal static void Revived(Entity body) { if (enabled && body!=null && !body.Dead) lock(body) lives.Remove(body); }
        internal static void OnWildDeath(Entity body, Entity killer) {
            if (!Wild(body,killer)) return; var p=killer.Owner;
            lock(p.ItemSyncRoot) lock(body) try {
                if (!Wild(body,killer)) return; var life=lives.GetValue(body,_=>new Life()); if(life.Credited)return;
                using(var c=DataHolder.MySqlConnection) { c.Open(); using(var t=c.BeginTransaction()) {
                    S.WildDeath(c,t,p,life.Id,body.MonsterInfo.ID,Roll(100));
                    if (!Wild(body,killer)) throw new InvalidOperationException("Abyss wild combat context changed."); t.Commit();
                }}
                life.Credited=true; ClaimOne(p); J.Refresh(p,false); J.SendDetails(p);
            } catch(Exception ex) { Q.Fail(p,"abyss-wild-hunt",ex); }
        }
    }
}
