using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class PowerArenaEvent
    {
        internal const uint Manager=500010;
        internal static readonly int[] Needs={30,45,60,35,45,60,90};
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        private static readonly ConcurrentDictionary<uint,DateTime> active=new ConcurrentDictionary<uint,DateTime>();
        private sealed class Death {internal GameState Player;internal ushort Map;internal DateTime Window;internal int Target;}
        private static readonly ConcurrentDictionary<uint,Death> deaths=new ConcurrentDictionary<uint,Death>();
        [ThreadStatic] private static HashSet<uint> transfers;
        private static bool enabled;
        private sealed class Run {
            internal DateTime Window,Cooldown;internal int Stage,Kills,Claimed;internal bool Entered,Cleared,Pending,Valuable;
        }
        internal static DateTime Window(DateTime utc) {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Power Arena clock requires UTC.");
            var local=TimeZoneInfo.ConvertTimeFromUtc(utc,Zone);
            if(local.Hour!=13 && local.Hour!=20)return DateTime.MinValue;
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Date.AddHours(local.Hour),DateTimeKind.Unspecified),Zone);
        }
        private static bool Arena(ushort map) {return map>=1730 && map<=1736;}
        private static bool Current(Run r,DateTime now) {return r!=null && r.Entered && r.Window==Window(now) && now<r.Window.AddHours(1);}
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            using(var q=Q.Command(c,t,"SELECT window_start,stage,kills,claimed,entered,cleared,cooldown,pending_death,valuable_reward FROM revora_power_runs WHERE uid=@uid FOR UPDATE","uid",(long)uid))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Window=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime,Stage=r.GetInt32(1),Kills=r.GetInt32(2),Claimed=r.GetInt32(3),Entered=r.GetBoolean(4),Cleared=r.GetBoolean(5),Cooldown=r.GetFieldValue<DateTimeOffset>(6).UtcDateTime,Pending=r.GetBoolean(7),Valuable=r.GetBoolean(8)}:null;
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,uint uid,Run r) {
            if(Q.Execute(c,t,@"UPDATE revora_power_runs SET window_start=@window,stage=@stage,kills=@kills,claimed=@claimed,entered=@entered,cleared=@cleared,cooldown=@cooldown,pending_death=@pending,valuable_reward=@valuable WHERE uid=@uid",
                "window",r.Window,"stage",r.Stage,"kills",r.Kills,"claimed",r.Claimed,"entered",r.Entered,"cleared",r.Cleared,"cooldown",r.Cooldown,"pending",r.Pending,"valuable",r.Valuable,"uid",(long)uid)!=1)throw new InvalidOperationException("Power Arena run was not saved.");
        }
        internal static void ConfigureGuardian(MonsterInformation m) {
            if(m.ID<2133 || m.ID>2139)return;
            uint floor=m.ID-2132;m.Level=(byte)(95+floor*5);m.Hitpoints=15000*floor;
            m.MinAttack=1125+300*(floor-1);m.MaxAttack=1504+400*(floor-1);m.Defence=(ushort)(700+100*floor);
        }
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_power_runs(
uid bigint PRIMARY KEY CHECK(uid>0),window_start timestamptz NOT NULL,stage integer NOT NULL CHECK(stage BETWEEN 1 AND 7),
kills integer NOT NULL CHECK(kills BETWEEN 0 AND 90),claimed integer NOT NULL CHECK(claimed BETWEEN 0 AND 127),entered boolean NOT NULL,cleared boolean NOT NULL,cooldown timestamptz NOT NULL,pending_death boolean NOT NULL DEFAULT false);
CREATE TABLE IF NOT EXISTS revora_power_claims(uid bigint NOT NULL,window_start timestamptz NOT NULL,stage integer NOT NULL,
choice varchar(16) NOT NULL,reward_uid bigint NOT NULL,PRIMARY KEY(uid,window_start,stage));
CREATE TABLE IF NOT EXISTS revora_power_totals(uid bigint PRIMARY KEY,badges integer NOT NULL CHECK(badges BETWEEN 0 AND 12));
ALTER TABLE revora_power_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_power_claims ADD COLUMN IF NOT EXISTS mount_uid bigint UNIQUE;");
                active.Clear();using(var q=Q.Command(c,null,"SELECT uid,window_start FROM revora_power_runs WHERE entered=true"))using(var r=q.ExecuteReader())while(r.Read())active[Convert.ToUInt32(r[0])]=r.GetFieldValue<DateTimeOffset>(1).UtcDateTime;
            }
            for(ushort map=1730;map<=1736;map++)if(!Constants.PKFreeMaps.Contains(map))Constants.PKFreeMaps.Add(map);
            PowerArenaPacks.Initialize();enabled=true;
        }
        internal static void PlaceNpcs() {
            var actors=new List<NpcSpawn>();using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT id,lookface,mapid,cellx,celly FROM npcs WHERE id=500010 OR id BETWEEN 3956 AND 3962"))
            using(var r=q.ExecuteReader())while(r.Read())actors.Add(new NpcSpawn{UID=Convert.ToUInt32(r[0]),Mesh=Convert.ToUInt16(r[1]),MapID=Convert.ToUInt16(r[2]),X=Convert.ToUInt16(r[3]),Y=Convert.ToUInt16(r[4]),Type=Enums.NpcType.Talker});}
            if(actors.Count!=8)throw new InvalidOperationException("Power Arena NPC catalog incomplete.");
            foreach(var npc in actors) {
                if(npc.UID==Manager){npc.MapID=1002;npc.X=449;npc.Y=293;npc.Name="ArenaManagerWang";}else npc.Name="Agate";
                Map map;if(!Kernel.Maps.TryGetValue(npc.MapID,out map))throw new InvalidOperationException("Power Arena floor missing.");
                foreach(var m in Kernel.Maps.Values.ToArray()){INpc old;if(m.Npcs.TryGetValue(npc.UID,out old))m.RemoveNpc(old);}
                ushort x=npc.X,y=npc.Y;if(!PotencyGuide.FindLanding(map,ref x,ref y))throw new InvalidOperationException("Power Arena NPC landing blocked.");
                npc.X=x;npc.Y=y;map.AddNpc(npc);
            }
        }
        internal static bool AllowTeleport(GameState p,ushort destination) {
            if(!enabled || p==null || p.Entity==null || transfers!=null && transfers.Contains(p.Entity.UID))return true;
            if(Arena(destination) || Arena(p.Entity.MapID) && active.ContainsKey(p.Entity.UID)) {
                p.Send("Speak to the Agate or Arena Manager Wang to leave or enter the arena.");return false;
            }
            return true;
        }
        private static void Travel(GameState p,int stage,bool heal=false) {
            ushort map=stage==0?(ushort)1002:(ushort)(1729+stage),x=stage==0?(ushort)449:(ushort)50,y=stage==0?(ushort)293:(ushort)50;
            if(!PotencyGuide.FindLanding(Kernel.Maps[map],ref x,ref y))throw new InvalidOperationException("Power Arena travel landing blocked.");
            if(transfers==null)transfers=new HashSet<uint>();transfers.Add(p.Entity.UID);
            try{p.Entity.Teleport(map,x,y);if(heal){p.Entity.BringToLife();p.Entity.Mana=p.Entity.MaxMana;}}finally{transfers.Remove(p.Entity.UID);}
        }
        private static string Join(GameState p) {
            var now=Clock();var window=Window(now);if(window==DateTime.MinValue)return "The arena opens at 13:00 and 20:00 Pacific, for one hour each.";
            if(p.Entity.OnDeath!=null || p.InQualifier() || p.InTeamQualifier())return "Finish your current contest first.";
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Power Arena character missing.");
                var r=Read(c,t,p.Entity.UID);
                if(r!=null && now<r.Cooldown)return "Rest for five minutes after being knocked out. Wang will admit you when you recover.";
                if(r==null) {
                    r=new Run{Window=window,Stage=1,Entered=true,Valuable=true,Cooldown=new DateTime(2000,1,1,0,0,0,DateTimeKind.Utc)};
                    Q.Execute(c,t,"INSERT INTO revora_power_runs(uid,window_start,stage,kills,claimed,entered,cleared,cooldown,valuable_reward) VALUES(@uid,@window,1,0,0,true,false,@cooldown,true)","uid",(long)p.Entity.UID,"window",window,"cooldown",r.Cooldown);
                }else {
                    if(r.Window!=window){r.Window=window;r.Claimed=0;r.Entered=false;r.Valuable=true;}
                    if(!r.Entered){r.Stage=1;r.Kills=0;r.Cleared=false;}r.Entered=true;r.Pending=false;Save(c,t,p.Entity.UID,r);
                }
                t.Commit();active[p.Entity.UID]=window;Travel(p,r.Stage);return "Defeat the guardians, then seek each floor's Agate. Each floor offers one gift this session.";
            }}
        }
        private static string Clear(GameState p,bool badge) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var r=Read(c,t,p.Entity.UID);if(!Current(r,Clock()) || r.Pending || p.Entity.MapID!=1729+r.Stage)return "This challenge has ended. Wang will welcome you at the next opening.";
                if(r.Cleared)return "This floor is cleared. You may continue your climb.";
                if(r.Kills<Needs[r.Stage-1])return "Defeat "+(Needs[r.Stage-1]-r.Kills)+" more guardians on this floor.";
                int mask=1<<(r.Stage-1);ConquerItem reward=null,mount=null;
                if((r.Claimed&mask)==0) {
                    bool finalMount=r.Stage==7 && r.Valuable;
                    if(p.Inventory.Count+(finalMount?2:1)>40)return "Make room for your floor reward"+(finalMount?" and Somersault Cloud.":".");
                    reward=Q.New(badge?PowerArenaPacks.Badge:722136);Q.Insert(c,t,p.Entity.UID,reward);
                    if(finalMount){mount=Q.New(200449,false);Q.Insert(c,t,p.Entity.UID,mount);}
                    if(Q.Execute(c,t,"INSERT INTO revora_power_claims(uid,window_start,stage,choice,reward_uid,mount_uid) VALUES(@uid,@window,@stage,@choice,@reward,@mount)",
                        "uid",(long)p.Entity.UID,"window",r.Window,"stage",r.Stage,"choice",badge?"badge":"exp-ball","reward",(long)reward.UID,"mount",mount==null?(object)DBNull.Value:(long)mount.UID)!=1)throw new InvalidOperationException("Power Arena claim was not saved.");
                    if(badge) {
                        int total;using(var q=Q.Command(c,t,@"INSERT INTO revora_power_totals(uid,badges) VALUES(@uid,1)
ON CONFLICT(uid) DO UPDATE SET badges=LEAST(12,revora_power_totals.badges+1) RETURNING badges","uid",(long)p.Entity.UID))total=Convert.ToInt32(q.ExecuteScalar());
                        if(total==12)ProgressionEvidence.Complete(c,t,p.Entity.UID,11014,"power-arena-v1","twelve-earned-badges",1);
                    }
                    r.Claimed|=mask;
                }
                if(r.Stage==7)ProgressionEvidence.Complete(c,t,p.Entity.UID,11013,"power-arena-v1",r.Window.ToString("yyyyMMddHH",CultureInfo.InvariantCulture),1);
                r.Cleared=true;Save(c,t,p.Entity.UID,r);t.Commit();if(reward!=null)Q.Install(p,new uint[0],mount==null?new[]{reward}:new[]{reward,mount});Q.Refresh(p);
                return r.Stage==7?(mount==null?"You have conquered the seventh floor. Wang awaits your return.":"The seventh floor is yours. Take this five-star Somersault Cloud as proof of your triumph."):"Your strength is proven. The next floor awaits.";
            }}
        }
        private static string Move(GameState p,int direction) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var r=Read(c,t,p.Entity.UID);if(!Current(r,Clock()) || r.Pending || p.Entity.MapID!=1729+r.Stage)return "This challenge has ended.";
                if(direction==1 && (!r.Cleared || r.Stage==7))return "Clear this floor with the Agate before advancing.";
                if(direction==-1 && r.Stage==1)return "You stand on the first floor already.";
                if(direction==0){r.Entered=false;r.Stage=1;}else r.Stage+=direction;
                r.Kills=0;r.Cleared=false;Save(c,t,p.Entity.UID,r);t.Commit();
                if(!r.Entered){DateTime ignored;active.TryRemove(p.Entity.UID,out ignored);}Travel(p,r.Entered?r.Stage:0);
                return direction==0?"Return whenever you are ready, while the arena is open.":"A new challenge awaits. Defeat this floor's guardians before advancing.";
            }}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!enabled || p==null || p.Entity==null || request==null)return false;
            uint npc=p.ActiveNpc;if(npc!=Manager && (npc<3956 || npc>3962))return false;
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot) {
                ushort map=npc==Manager?(ushort)1002:(ushort)(1730+npc-3956);if(!Q.Near(p,npc,map))return true;
                try {
                    var d=new Npcs(p);byte option=request.OptionID;
                    if(npc==Manager) {
                        if(option==1)d.Text(Join(p));
                        else if(!PowerArenaPacks.Menu(p,d,option)){d.Text("Conquer all seven floors for a five-star Somersault Cloud. The arena opens at 13:00 and 20:00 Pacific, for one hour. Older runs keep their promised gifts.");d.Option("Enter or resume the arena.",1);d.Option("Exchange Refinery Badges.",10);}
                    }else if(option==1 || option==2)d.Text(Clear(p,option==1));
                    else if(option==3)d.Text(Move(p,1));else if(option==4)d.Text(Move(p,-1));else if(option==5)d.Text(Move(p,0));
                    else {
                        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                            var r=Read(c,t,p.Entity.UID);t.Commit();
                            if(!Current(r,Clock()) || r.Stage!=map-1729)d.Text("This challenge has ended.");
                            else {d.Text("Floor "+r.Stage+": "+r.Kills+" of "+Needs[r.Stage-1]+" guardians defeated. Each floor grants one gift per session.");
                                d.Option("Clear: Refinery Badge.",1);d.Option("Clear: EXP Ball.",2);if(r.Stage<7)d.Option("Advance one floor.",3);if(r.Stage>1)d.Option("Withdraw one floor.",4);d.Option("Return to Wang.",5);}
                        }}
                    }
                    d.Option("Farewell.",255);d.Send();
                }catch(Exception ex){Q.Fail(p,"power-arena",ex);}
            }
            return true;
        }
        internal static void OnLogin(GameState p) {
            if(!enabled || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try {
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    var r=Read(c,t,p.Entity.UID);DateTime ignored;
                    if(Current(r,Clock())){
                        if(p.Entity.Dead && Arena(p.Entity.MapID) && !r.Pending){Penalize(r,Clock());Save(c,t,p.Entity.UID,r);}
                        t.Commit();if(r.Entered)active[p.Entity.UID]=r.Window;else active.TryRemove(p.Entity.UID,out ignored);
                        if(Arena(p.Entity.MapID) || r.Pending){Travel(p,r.Entered?r.Stage:0,p.Entity.Dead);CompleteTransfer(p,r.Window);}
                    }
                    else {if(r!=null){r.Entered=false;Save(c,t,p.Entity.UID,r);}t.Commit();active.TryRemove(p.Entity.UID,out ignored);if(Arena(p.Entity.MapID) || r!=null && r.Pending){Travel(p,0,true);if(r!=null)CompleteTransfer(p,r.Window);}}
                }}
            }catch(Exception ex){Q.Fail(p,"power-arena-login",ex);}
        }
        internal static void OnDeath(GameState p) {
            DateTime window;if(!enabled || !Q.Player(p,false) || !Arena(p.Entity.MapID) || !active.TryGetValue(p.Entity.UID,out window))return;
            // Persist the penalty before releasing the death callback. No victim item
            // lock is taken from the killer's stack; only transfer/revival is queued.
            try {using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var r=Read(c,t,p.Entity.UID);if(r==null || !r.Entered || r.Pending || r.Window!=window || p.Entity.MapID!=1729+r.Stage)return;
                ushort map=p.Entity.MapID;Penalize(r,Clock());Save(c,t,p.Entity.UID,r);t.Commit();
                if(!r.Entered){DateTime ignored;active.TryRemove(p.Entity.UID,out ignored);}
                deaths.TryAdd(p.Entity.UID,new Death{Player=p,Map=map,Window=window,Target=r.Entered?r.Stage:0});
            }}}catch(Exception ex){Q.Fail(p,"power-arena-death-save",ex);}
        }
        private static void Penalize(Run r,DateTime now) {
            if(r.Stage==7 || !Current(r,now)){r.Entered=false;r.Cooldown=now.AddMinutes(5);r.Stage=1;}
            else r.Stage=Math.Max(1,r.Stage-1);
            r.Kills=0;r.Cleared=false;r.Pending=true;
        }
        private static void CompleteTransfer(GameState p,DateTime window) {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,"UPDATE revora_power_runs SET pending_death=false WHERE uid=@uid AND window_start=@window","uid",(long)p.Entity.UID,"window",window);}
        }
        internal static void Tick() {
            PowerArenaPacks.ExpireOffers(Clock());
            foreach(var entry in deaths.ToArray()) {
                Death death;if(!deaths.TryRemove(entry.Key,out death) || !Q.Player(death.Player,false))continue;
                var p=death.Player;lock(p.ItemSyncRoot)try {
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                        var r=Read(c,t,p.Entity.UID);if(r==null || !r.Pending || r.Window!=death.Window || p.Entity.MapID!=death.Map || (r.Entered?r.Stage:0)!=death.Target)continue;
                        t.Commit();Travel(p,death.Target,true);CompleteTransfer(p,r.Window);
                    }}
                }catch(Exception ex){Q.Fail(p,"power-arena-death",ex);}
            }
            var now=Clock();foreach(var entry in active.ToArray())if(now>=entry.Value.AddHours(1)) {
                GameState p;if(Kernel.GamePool.TryGetValue(entry.Key,out p) && Q.Player(p,false))OnLogin(p);
                else {DateTime ignored;active.TryRemove(entry.Key,out ignored);}
            }
        }
        internal static bool TryDie(Entity mob,Entity killer) {
            if(!enabled || mob==null || mob.EntityFlag!=EntityFlag.Monster || mob.Companion || mob.MonsterInfo==null || !Arena(mob.MapID) || mob.MonsterInfo.ID!=(uint)(2133+mob.MapID-1730))return false;
            Map map;Entity owned;if(!Kernel.Maps.TryGetValue(mob.MapID,out map) || !map.Entities.TryGetValue(mob.UID,out owned) || !ReferenceEquals(owned,mob))return false;
            ushort area,x,y;DateTime at;bool died;
            bool durable=mob.MonsterInfo.Level>=120,reached=false;
            lock(mob){
                if(mob.Dead)return true;area=mob.MapID;x=mob.X;y=mob.Y;at=Clock();
                if(durable)WildMonsterVictory.DieWithProgress(mob,killer,(c,t)=>{int count=CreditSaved(c,t,killer,area,x,y,at);reached=count==Needs[area-1730];return count>0;});
                else mob.DieCore(killer);
                died=mob.Dead;
            }
            if(died){if(!durable)Credit(killer,area,x,y,at);else if(reached)killer.Owner.Send("The Agate awaits you. This floor's guardians have tested your strength.");}
            return true;
        }
        // Runs under the canonical killer's owner lock and the accepted life transaction.
        private static int CreditSaved(NpgsqlConnection c,NpgsqlTransaction t,Entity killer,ushort area,ushort x,ushort y,DateTime at) {
            if(killer==null || killer.EntityFlag!=EntityFlag.Player || killer.Owner==null || !ReferenceEquals(killer.Owner.Entity,killer))return 0;
            var p=killer.Owner;
            if(!Q.Player(p) || p.Entity.MapID!=area || !active.ContainsKey(p.Entity.UID) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>Constants.pScreenDistance)return 0;
            var r=Read(c,t,p.Entity.UID);
            if(!Current(r,at) || !Current(r,Clock()) || r.Pending || r.Stage!=area-1729 || r.Cleared || r.Kills>=Needs[r.Stage-1])return 0;
            r.Kills++;Save(c,t,p.Entity.UID,r);return r.Kills;
        }
        private static void Credit(Entity killer,ushort area,ushort x,ushort y,DateTime at) {
            if(killer==null || killer.EntityFlag!=EntityFlag.Player || killer.Owner==null || !ReferenceEquals(killer.Owner.Entity,killer))return;
            var p=killer.Owner;if(!Q.Player(p) || p.Entity.MapID!=area || !active.ContainsKey(p.Entity.UID) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>Constants.pScreenDistance)return;
            lock(p.ItemSyncRoot)try {
                if(!Q.Player(p))return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    var r=Read(c,t,p.Entity.UID);if(!Current(r,at) || !Current(r,Clock()) || r.Pending || r.Stage!=area-1729 || r.Cleared || r.Kills>=Needs[r.Stage-1])return;
                    r.Kills++;Save(c,t,p.Entity.UID,r);t.Commit();if(r.Kills==Needs[r.Stage-1])p.Send("The Agate awaits you. This floor's guardians have tested your strength.");
                }}
            }catch(Exception ex){Q.Fail(p,"power-arena-kill",ex);}
        }
    }
}
