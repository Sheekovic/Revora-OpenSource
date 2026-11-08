using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Game.ConquerStructures.Society;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class GuildPkBanner
    {
        internal const uint Actor=706074,Health=1000000;
        private sealed class Battle {
            internal Guid Run;internal Map Map;internal SobNpcSpawn Body;internal DateTime Start,End;
            internal readonly object Gate=new object();internal bool Spawned,Pending,Retired;
            internal GuildPkBannerTable.Snapshot Saved;
        }
        private sealed class Defeat {internal uint Killer;internal Guid KillerAdmission;internal int Number,KillerPower,VictimPower;internal DateTime At;}
        private sealed class Entry {internal Guid Run,Admission;internal Guild Guild;internal Guild.Member Member;internal int Deaths;internal bool Participated,Downed;internal DateTime Ready;internal Defeat Pending;}
        private static readonly ConcurrentDictionary<ushort,Battle> battles=new ConcurrentDictionary<ushort,Battle>();
        private static readonly ConditionalWeakTable<GameState,Entry> entries=new ConditionalWeakTable<GameState,Entry>();
        private static DateTime Now {get{return GuildContestCoordinator.Clock();}}
        internal static void Register(Guid run,Map map,DateTime start,DateTime end) {
            var source=GuildWar.Pole;
            if(source==null || source.Mesh==0 || map.Npcs.ContainsKey(Actor))throw new InvalidOperationException("Guild PK banner profile is unavailable.");
            GuildPkBannerTable.Ensure(run);
            var battle=new Battle{Run=run,Map=map,Start=start,End=end,Body=new SobNpcSpawn{UID=Actor,MapID=map.ID,
                Mesh=source.Mesh,Type=source.Type,Sort=source.Sort,Name="Guild Banner",ShowName=true,MaxHitpoints=Health,Hitpoints=Health}};
            if(!battles.TryAdd(map.ID,battle))throw new InvalidOperationException("Guild PK banner map is already owned.");
        }
        internal static void Admit(GameState p,Guid run,Guid admission) {
            Battle battle;if(p.Map==null || !battles.TryGetValue(p.Map.ID,out battle) || battle.Run!=run || !ReferenceEquals(p.Map,battle.Map))return;
            if(!SessionInstall.Holds(p.Entity.UID) || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Guild PK participant requires session ownership.");
            entries.Remove(p);entries.Add(p,new Entry{Run=run,Admission=admission,Guild=p.Guild,Member=p.AsMember});
        }
        internal static bool Managed(Entity attacker,SobNpcSpawn target) {
            return target!=null && (target.UID==Actor || battles.ContainsKey(target.MapID)) || attacker!=null && battles.ContainsKey(attacker.MapID);
        }
        private static bool Participant(Battle battle,Entity body,out Entry entry,bool downed=false) {
            entry=null;var p=body==null?null:body.Owner;Guild guild;Guild.Member member;Map map;
            return !battle.Retired && !battle.Pending && battle.Spawned && battle.Saved!=null && battle.Saved.State=="active" && Now>=battle.Start && Now<battle.End
                && Q.Player(p,!downed) && ReferenceEquals(p.Entity,body) && body.EntityFlag==EntityFlag.Player && !body.Companion
                && ReferenceEquals(p.Map,battle.Map) && body.MapID==battle.Map.ID && Kernel.Maps.TryGetValue(body.MapID,out map) && ReferenceEquals(map,battle.Map)
                && entries.TryGetValue(p,out entry) && entry.Run==battle.Run && entry.Pending==null && entry.Deaths<20 && (downed?entry.Downed && body.Dead && Now<entry.Ready:!entry.Downed && Now>=entry.Ready) && p.Guild!=null && ReferenceEquals(p.Guild,entry.Guild) && ReferenceEquals(p.AsMember,entry.Member)
                && body.GuildID==entry.Guild.ID && Kernel.Guilds.TryGetValue(body.GuildID,out guild) && ReferenceEquals(guild,entry.Guild)
                && guild.Members.TryGetValue(body.UID,out member) && ReferenceEquals(member,entry.Member) && member.GuildID==guild.ID
                && (guild.ID==battle.Saved.First || guild.ID==battle.Saved.Second);
        }
        private static bool Fighter(Battle battle,Entity body,SobNpcSpawn target,out Entry entry) {
            Revora.Interfaces.INpc actual;
            return Participant(battle,body,out entry) && ReferenceEquals(target,battle.Body)
                && battle.Map.Npcs.TryGetValue(Actor,out actual) && ReferenceEquals(actual,target) && target.Hitpoints>0
                && entry.Guild.ID!=battle.Saved.Keeper && Kernel.GetDistance(body.X,body.Y,target.X,target.Y)<=18;
        }
        internal static void Revoke(GameState p,Guid run) {
            Entry entry;if(entries.TryGetValue(p,out entry) && entry.Run==run)entries.Remove(p);
        }
        private static Battle Find(Entity a,Entity b) {
            Battle battle;
            if(a!=null && battles.TryGetValue(a.MapID,out battle))return battle;
            return b!=null && battles.TryGetValue(b.MapID,out battle)?battle:null;
        }
        internal static bool InBattle(Entity body){return body!=null && battles.ContainsKey(body.MapID);}
        internal static bool TryDefeat(Entity victim,Entity killer) {
            if(!InBattle(victim))return false;
            using(var lease=PlayerCombatLease.Acquire(killer,victim)) {
                if(lease==null || !lease.Allowed)return true;
                Battle battle=Find(killer,victim);Entry entry,other;
                if(!entries.TryGetValue(victim.Owner,out entry) || !entries.TryGetValue(killer.Owner,out other))return true;
                entry.Pending=new Defeat{Killer=killer.UID,KillerAdmission=other.Admission,Number=entry.Deaths+1,KillerPower=killer.BattlePower,VictimPower=victim.BattlePower,At=Now};
                ResolveDefeat(battle,victim.Owner,entry);
            }
            return true;
        }
        private static void ResolveDefeat(Battle battle,GameState p,Entry entry) {
            Entry current;
            if(!SessionInstall.Holds(p.Entity.UID) || !Monitor.IsEntered(p.ItemSyncRoot) || !Q.Player(p,false)
                || !entries.TryGetValue(p,out current) || !ReferenceEquals(current,entry))return;
            var pending=entry.Pending;if(pending==null)return;
            try {
                if(!GuildPkDefeatTable.Record(battle.Run,p.Entity.UID,pending.Killer,entry.Admission,pending.KillerAdmission,pending.Number,pending.KillerPower,pending.VictimPower,pending.At)) {
                    entry.Pending=null;return;
                }
                // Keep this request pending until every runtime projection succeeds.
                // Repeating Record resolves the same receipt, never another death.
                ushort x,y;
                if(pending.Number<20) {
                    if(!GuildPkAdmission.Camp(battle.Run,battle.Map,entry.Guild.ID,out x,out y))throw new InvalidOperationException("Guild PK return camp is unavailable.");
                    p.Entity.Teleport(battle.Map.BaseID,battle.Map.ID,x,y);
                    if(!ReferenceEquals(p.Map,battle.Map) || p.Entity.X!=x || p.Entity.Y!=y)throw new InvalidOperationException("Guild PK camp return failed.");
                }
                p.Entity.AttackPacket=null;p.Entity.ToxicFogLeft=0;p.Entity.ClearFlags();
                entry.Deaths=pending.Number;entry.Ready=pending.At.AddSeconds(5);
                if(pending.Number==20){p.Entity.BringToLife();GuildPkAdmission.Leave(p,battle.Run);}
                else {
                    entry.Downed=true;p.Entity.DeathStamp=Time32.Now;p.Entity.Hitpoints=0;
                    p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Dead,StatusFlagsBigVector32.PermanentFlag,true);
                    p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Ghost,StatusFlagsBigVector32.PermanentFlag,true);
                }
                entry.Pending=null;
                p.Send("Defeats: "+entry.Deaths+"/20. "+(entry.Deaths==20?"Your battle is over.":"Recover at your guild's camp."));
            }catch(Exception ex){Console.WriteLine("[guild-pk] defeat recovery error="+ex.GetType().Name);}
        }
        private static void RecoverDefeats(Battle battle) {
            foreach(var p in Kernel.GamePool.Values.Where(p=>p!=null && p.Entity!=null).Take(4096)) {
                Entry entry;if(!entries.TryGetValue(p,out entry) || entry.Run!=battle.Run || entry.Pending==null && !entry.Downed || !Q.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))continue;
                bool held=false;
                try {if(held=Monitor.TryEnter(p.ItemSyncRoot)) {
                    ResolveDefeat(battle,p,entry);
                    if(entry.Pending==null && entry.Downed && GuildPkDefeatTable.Restore(battle.Run,p.Entity.UID,entry.Deaths,0,Guid.Empty,Now)) {
                        p.Entity.BringToLife();entry.Downed=false;
                    }
                }}
                finally{if(held)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}
            }
        }
        private static void RecordParticipation(Battle battle) {
            var players=Kernel.GamePool.Values.Where(p=>Q.Player(p) && ReferenceEquals(p.Map,battle.Map)).ToArray();
            foreach(var p in players) {
                Entry entry;if(!Participant(battle,p.Entity,out entry) || entry.Participated)continue;
                var other=players.FirstOrDefault(q=>{Entry opponent;return q!=p && q.Entity.GuildID!=p.Entity.GuildID && Participant(battle,q.Entity,out opponent);});
                if(other==null)continue;
                using(var lease=PlayerCombatLease.Acquire(p.Entity,other.Entity,true)) {
                    if(lease==null || !lease.Allowed)continue;Entry opponent;
                    if(entries.TryGetValue(other,out opponent))entry.Participated=GuildPkSchedule.Participate(battle.Run,p.Entity.UID,entry.Admission,other.Entity.UID,opponent.Admission,Now);
                }
            }
        }
        private static bool Shelter(Battle battle,Entity body) {
            // Revora's explicit radius: three tiles around one's captured banner.
            // Protected players cannot fire out of the sanctuary either.
            return battle.Saved.Keeper==body.GuildID && Kernel.GetDistance(body.X,body.Y,battle.Body.X,battle.Body.Y)<=3;
        }
        private static bool Opponents(Battle battle,Entity a,Entity b) {
            Entry first,second;
            return a!=b && Participant(battle,a,out first) && Participant(battle,b,out second)
                && first.Guild.ID!=second.Guild.ID && !Shelter(battle,a) && !Shelter(battle,b);
        }
        internal static bool BlockPlayerAttack(Entity a,Entity b) {
            var battle=Find(a,b);if(battle==null)return false;
            if(!Monitor.TryEnter(battle.Gate))return true;
            try{return !Opponents(battle,a,b);}finally{Monitor.Exit(battle.Gate);}
        }
        internal sealed class PlayerCombatLease:IDisposable {
            private readonly System.Collections.Generic.List<object> gates=new System.Collections.Generic.List<object>();
            private readonly System.Collections.Generic.List<uint> sessions=new System.Collections.Generic.List<uint>();
            internal bool Allowed;
            private bool Own(object gate){if(gate==null || !Monitor.TryEnter(gate))return false;gates.Add(gate);return true;}
            internal static PlayerCombatLease Acquire(Entity a,Entity b,bool participation=false,bool revival=false,bool support=false) {
                var battle=Find(a,b);if(battle==null)return null;var lease=new PlayerCombatLease();
                try {
                    if(a==null || b==null || a==b && !support || a.Owner==null || b.Owner==null)return lease;
                    foreach(var body in new[]{a,b}.Distinct().OrderBy(body=>body.UID)) {
                        if(!SessionInstall.TryEnter(body.UID))return lease;lease.sessions.Add(body.UID);
                    }
                    foreach(var body in new[]{a,b}.Distinct().OrderBy(body=>body.UID))if(!lease.Own(body.Owner.ItemSyncRoot))return lease;
                    if(!lease.Own(battle.Gate))return lease;
                    Entry first,second;
                    lease.Allowed=revival ? Participant(battle,a,out first) && Participant(battle,b,out second,true) && first.Guild.ID==second.Guild.ID : support ? Participant(battle,a,out first) && Participant(battle,b,out second) && first.Guild.ID==second.Guild.ID : participation ? Participant(battle,a,out first) && Participant(battle,b,out second) && first.Guild.ID!=second.Guild.ID : Opponents(battle,a,b);return lease;
                }catch{lease.Dispose();throw;}
            }
            public void Dispose(){
                for(int i=gates.Count-1;i>=0;i--)Monitor.Exit(gates[i]);gates.Clear();
                for(int i=sessions.Count-1;i>=0;i--)SessionInstall.Exit(sessions[i]);sessions.Clear();
            }
        }
        internal static bool CommitRevival(Entity caster,Entity target) {
            Battle battle=Find(caster,target);Entry a,b;
            if(battle==null || !Monitor.IsEntered(battle.Gate) || !Participant(battle,caster,out a) || !Participant(battle,target,out b,true) || a.Guild.ID!=b.Guild.ID)return false;
            return GuildPkDefeatTable.Restore(battle.Run,target.UID,b.Deaths,caster.UID,a.Admission,Now);
        }
        internal static void Returning(GameState p,Guid run) {
            Entry entry;if(entries.TryGetValue(p,out entry) && entry.Run==run && entry.Downed){p.Entity.BringToLife();entry.Downed=false;}
        }
        internal static void Revived(Entity target) {
            Entry entry;if(target!=null && target.Owner!=null && !target.Dead && entries.TryGetValue(target.Owner,out entry))entry.Downed=false;
        }
        internal static bool CanAttack(Entity attacker,SobNpcSpawn target) {
            Battle battle;if(target==null || !battles.TryGetValue(target.MapID,out battle) || !Monitor.TryEnter(battle.Gate))return false;
            try {Entry entry;return Fighter(battle,attacker,target,out entry);}finally{Monitor.Exit(battle.Gate);}
        }
        private static void Broadcast(Battle battle,Revora.Interfaces.IPacket packet) {
            foreach(var p in Kernel.GamePool.Values)if(Q.Player(p,false) && ReferenceEquals(p.Map,battle.Map)) {
                if(ReferenceEquals(packet,battle.Body)) {
                    if(Kernel.GetDistance(p.Entity.X,p.Entity.Y,battle.Body.X,battle.Body.Y)<=16)battle.Body.SendSpawn(p,false);
                }else {p.Screen.Remove(battle.Body);p.Send(packet);}
            }
        }
        private static void Place(Battle battle,uint keeper) {
            ushort anchorX=50,anchorY=60;
            if(keeper!=0) {
                if(!GuildPkAdmission.Camp(battle.Run,battle.Map,keeper,out anchorX,out anchorY))throw new InvalidOperationException("Guild PK banner camp is unavailable.");
                anchorY=checked((ushort)(anchorY+5));
            }
            ushort x=0,y=0;
            for(int radius=0;radius<=6 && x==0;radius++)for(int dx=-radius;dx<=radius && x==0;dx++)for(int dy=-radius;dy<=radius;dy++) {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;int nx=anchorX+dx,ny=anchorY+dy;
                if(nx<2 || ny<2 || nx>=battle.Map.Floor.Bounds.Width-2 || ny>=battle.Map.Floor.Bounds.Height-2 || !battle.Map.Floor[nx,ny,MapObjectType.Player]
                    || battle.Map.Npcs.Values.Any(n=>n.UID!=Actor && Kernel.GetDistance(n.X,n.Y,(ushort)nx,(ushort)ny)<3))continue;
                x=(ushort)nx;y=(ushort)ny;break;
            }
            if(x==0)throw new InvalidOperationException("Guild PK banner landing is unavailable.");
            if(battle.Spawned){Broadcast(battle,new Data(true){UID=Actor,ID=Data.RemoveEntity});battle.Map.RemoveNpc(battle.Body);}
            battle.Body.X=x;battle.Body.Y=y;battle.Body.Hitpoints=Health;battle.Body.Name=keeper==0?"Guild Banner":"Captured Banner";
            battle.Map.AddNpc(battle.Body);battle.Spawned=true;Broadcast(battle,battle.Body);
        }
        private static void Sync(Battle battle) {
            var state=GuildPkBannerTable.Pulse(battle.Run,Now);
            if(Now>=battle.Start && Now<battle.End && state.State=="active"
                && (!battle.Spawned || battle.Saved==null || state.Capture!=battle.Saved.Capture || state.Keeper!=battle.Saved.Keeper))Place(battle,state.Keeper);
            if(battle.Spawned && battle.Body.Hitpoints!=state.Hitpoints){battle.Body.Hitpoints=state.Hitpoints;Broadcast(battle,battle.Body);}
            battle.Saved=state;battle.Pending=false;
        }
        internal static void Pulse(Map map) {
            Battle battle;if(!battles.TryGetValue(map.ID,out battle) || !ReferenceEquals(map,battle.Map) || !Monitor.TryEnter(battle.Gate))return;
            try {if(!battle.Retired && Now>=battle.Start){
                Sync(battle);RecoverDefeats(battle);RecordParticipation(battle);
                bool pending=Kernel.GamePool.Values.Any(p=>{Entry entry;return p!=null && entries.TryGetValue(p,out entry) && entry.Run==battle.Run && entry.Pending!=null;});
                if(!pending)battle.Saved=GuildPkBannerTable.Settle(battle.Run,Now);
            }}
            catch(Exception ex){battle.Pending=true;Console.WriteLine("[guild-pk] banner pulse error="+ex.GetType().Name);}
            finally{Monitor.Exit(battle.Gate);}
        }
        internal static bool Hit(Entity attacker,SobNpcSpawn target,Attack packet,uint damage) {
            if(!Managed(attacker,target))return false;
            Battle battle;var p=attacker==null?null:attacker.Owner;
            if(target==null || !battles.TryGetValue(target.MapID,out battle) || !Q.Player(p) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            bool item=false,gate=false;
            try {
                if(!(item=Monitor.TryEnter(p.ItemSyncRoot)) || !(gate=Monitor.TryEnter(battle.Gate)))return true;
                Entry entry;if(packet==null || damage==0 || !Fighter(battle,attacker,target,out entry))return true;
                uint accepted=Math.Min(damage,target.Hitpoints);
                if(!GuildPkBannerTable.Strike(battle.Run,checked(battle.Saved.HitNumber+1),attacker.UID,entry.Guild.ID,entry.Admission,accepted,target.Hitpoints,Now))return true;
                battle.Pending=true;packet.Damage=accepted;
                if(packet.AttackType!=Attack.Magic)p.SendScreen(packet,true);
                Sync(battle);
                attacker.AttackPacket=packet;attacker.AttackStamp=Time32.Now;
            }catch(Exception ex) {
                battle.Pending=true;Console.WriteLine("[guild-pk] banner hit error="+ex.GetType().Name);
                try{Sync(battle);}catch(Exception recovery){Console.WriteLine("[guild-pk] banner recovery error="+recovery.GetType().Name);}
            }finally{if(gate)Monitor.Exit(battle.Gate);if(item)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}
            return true;
        }
        internal static bool Retire(Map map) {
            Battle battle;if(!battles.TryGetValue(map.ID,out battle) || !ReferenceEquals(map,battle.Map))return true;
            if(!Monitor.TryEnter(battle.Gate))return false;
            try {
                battle.Retired=true;if(battle.Spawned){Broadcast(battle,new Data(true){UID=Actor,ID=Data.RemoveEntity});map.RemoveNpc(battle.Body);}
                Battle removed;battles.TryRemove(map.ID,out removed);return true;
            }finally{Monitor.Exit(battle.Gate);}
        }
    }
}
