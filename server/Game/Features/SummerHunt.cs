using System;
using System.Collections.Concurrent;
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
    internal static class SummerHunt
    {
        internal const uint Npc=900000063;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        private static readonly ConcurrentDictionary<uint,string> active=new ConcurrentDictionary<uint,string>();
        private static readonly string[] Targets={"Birdmen on Bird Island","Tomb Bats in Mystic Castle","Bull Monsters in Mystic Castle","Red Devils in Mystic Castle"};
        private static readonly string[] Bonus={"Release the Souls","Demon Exterminators","a Power Arena floor"};
        private static bool enabled;
        private static DateTime nextCleanup;
        internal static string Day(DateTime utc) {return Local(utc).ToString("yyyyMMdd",CultureInfo.InvariantCulture);}
        private static DateTime Local(DateTime utc) {if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Summer timestamps require UTC.");return TimeZoneInfo.ConvertTimeFromUtc(utc,Zone);}
        internal static DateTime Midnight(DateTime utc,int days=0) {return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(Local(utc).Date.AddDays(days),DateTimeKind.Unspecified),Zone);}
        private sealed class Progress {internal string Day;internal int Route,Kills,Bonus;internal bool Claimed,BonusClaimed,Valuable;internal DateTime Accepted,Start,End;}
        private static Progress ProgressRow(NpgsqlDataReader r) {return new Progress{Day=r.GetString(0),Route=r.GetInt32(1),Kills=r.GetInt32(2),Bonus=r.GetInt32(3),Claimed=r.GetBoolean(4),BonusClaimed=r.GetBoolean(5),Accepted=r.GetFieldValue<DateTimeOffset>(6).UtcDateTime,Start=r.GetFieldValue<DateTimeOffset>(7).UtcDateTime,End=r.GetFieldValue<DateTimeOffset>(8).UtcDateTime,Valuable=r.GetBoolean(9)};}
        private static Progress Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid,string day) {
            const string sql=@"SELECT day,route,kills,bonus_source,claimed,bonus_claimed,accepted_at,day_start,day_end,valuable_reward
FROM revora_summer_daily WHERE uid=@uid AND day=@day FOR UPDATE";
            using(var q=Q.Command(c,t,sql,"uid",(long)uid,"day",day))using(var r=q.ExecuteReader())return r.Read()?ProgressRow(r):null;
        }
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_summer_seasons(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),starts_at timestamptz NOT NULL,ends_at timestamptz NOT NULL,
rule_version integer NOT NULL CHECK(rule_version=1),UNIQUE(uid,starts_at),CHECK(ends_at>starts_at));
CREATE INDEX IF NOT EXISTS revora_summer_seasons_owner ON revora_summer_seasons(uid,starts_at DESC);
CREATE TABLE IF NOT EXISTS revora_summer_daily(
uid bigint NOT NULL CHECK(uid>0),day varchar(8) NOT NULL,season uuid NOT NULL REFERENCES revora_summer_seasons(id),
route integer NOT NULL CHECK(route BETWEEN 0 AND 3),kills integer NOT NULL DEFAULT 0 CHECK(kills BETWEEN 0 AND 120),
bonus_source integer NOT NULL CHECK(bonus_source BETWEEN 0 AND 2),accepted_at timestamptz NOT NULL,
day_start timestamptz NOT NULL,day_end timestamptz NOT NULL,claimed boolean NOT NULL DEFAULT false,bonus_claimed boolean NOT NULL DEFAULT false,
reward_ids bigint[] NOT NULL DEFAULT '{}',bonus_ids bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,day));
ALTER TABLE revora_summer_daily ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;");}
            SummerCrafting.Initialize();active.Clear();enabled=true;nextCleanup=DateTime.MinValue;
        }
        internal static void PlaceNpc() {
            var map=Kernel.Maps[(ushort)1002];if(Kernel.Maps.Values.Any(m=>m.Npcs!=null && m.Npcs.ContainsKey(Npc)))throw new InvalidOperationException("Summer artisan identity collision.");
            ushort mesh;using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT lookface FROM npcs WHERE id=10062")) {
                var value=q.ExecuteScalar();if(value==null)throw new InvalidOperationException("Summer artisan appearance missing.");mesh=Convert.ToUInt16(value);
            }}
            ushort x=460,y=281;bool found=false;
            for(int radius=0;radius<=8 && !found;radius++)for(int dx=-radius;dx<=radius && !found;dx++)for(int dy=-radius;dy<=radius && !found;dy++) {
                int tx=460+dx,ty=281+dy;if(!map.Floor[tx,ty,MapObjectType.Player] || map.Npcs.Values.Any(n=>n.X==tx && n.Y==ty))continue;
                x=(ushort)tx;y=(ushort)ty;found=true;
            }
            if(!found)throw new InvalidOperationException("Summer artisan landing unavailable.");CityGuardSystem.SpawnTalker(Npc,1002,x,y,mesh,"Liora");
        }
        internal static void OnLogin(GameState p) {
            if(!enabled || !Q.Player(p,false))return;
            try{using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {var r=Read(c,t,p.Entity.UID,Day(Clock()));t.Commit();
                if(r!=null && r.Kills<120)active[p.Entity.UID]=r.Day;else {string ignored;active.TryRemove(p.Entity.UID,out ignored);}
            }}}catch(Exception ex){Q.Fail(p,"summer-login",ex);}
        }
        private static string Status(Progress r) {return r==null?"Ask me for today's hunt. Your first hunt begins a sixty-day summer journey.":r.Claimed?"Your hunt materials are safe. Today's optional task is "+Bonus[r.Bonus]+".":"Today's hunt: "+r.Kills+"/120 "+Targets[r.Route]+". Optional task: "+Bonus[r.Bonus]+".";}
        private static string Accept(GameState p) {
            if(p.Entity.Level<120)return "Return at level 120, when the far hunting grounds are within your reach.";
            var now=Clock();string day=Day(now);using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Summer character missing.");
                var old=Read(c,t,p.Entity.UID,day);if(old!=null){t.Commit();if(old.Kills<120)active[p.Entity.UID]=day;return Status(old);}
                Guid season=Guid.Empty;DateTime end=DateTime.MinValue;
                using(var q=Q.Command(c,t,"SELECT id,ends_at FROM revora_summer_seasons WHERE uid=@uid ORDER BY starts_at DESC LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID))
                using(var r=q.ExecuteReader())if(r.Read()){season=r.GetGuid(0);end=r.GetFieldValue<DateTimeOffset>(1).UtcDateTime;}
                if(season==Guid.Empty || now>=end) {
                    season=Guid.NewGuid();end=Midnight(now,60);Q.Execute(c,t,"INSERT INTO revora_summer_seasons(id,uid,starts_at,ends_at,rule_version) VALUES(@id,@uid,@start,@end,1)","id",season,"uid",(long)p.Entity.UID,"start",Midnight(now),"end",end);
                }
                int sequence=(int)(Local(now).Date-new DateTime(2026,1,1)).TotalDays,route=(sequence%4+4)%4,bonus=(sequence%3+3)%3;
                Q.Execute(c,t,@"INSERT INTO revora_summer_daily(uid,day,season,route,bonus_source,accepted_at,day_start,day_end,valuable_reward)
VALUES(@uid,@day,@season,@route,@bonus,@now,@start,@end,true)","uid",(long)p.Entity.UID,"day",day,"season",season,"route",route,"bonus",bonus,"now",now,"start",Midnight(now),"end",Midnight(now,1));
                t.Commit();active[p.Entity.UID]=day;return "Hunt 120 "+Targets[route]+". Your summer journey runs until "+Local(end).ToString("MMM d",CultureInfo.InvariantCulture)+" Pacific.";
            }}
        }
        // Called only from the existing owned-monster, single-death hunt wrapper.
        // It writes a counter, taking no other player's inventory lock.
        internal static void Credit(Entity killer,ushort map,ushort x,ushort y,uint type,DateTime occurred) {
            if(!enabled || killer==null || killer.EntityFlag!=EntityFlag.Player || killer.Owner==null || !ReferenceEquals(killer.Owner.Entity,killer))return;
            var p=killer.Owner;string accepted;
            if(!Q.Player(p) || p.Entity.Level<120 || p.Entity.MapID!=map || Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>Constants.pScreenDistance
                || !active.TryGetValue(p.Entity.UID,out accepted) || accepted!=Day(occurred))return;
            try{using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var r=Read(c,t,p.Entity.UID,accepted);if(r==null || r.Kills>=120 || r.Accepted>occurred || occurred<r.Start || occurred>=r.End)return;
                uint[] types={18,20,57,58};ushort expectedMap=r.Route==0?(ushort)1015:(ushort)1001;
                if(map!=expectedMap || type!=types[r.Route] && !(r.Route==3 && type==83))return;
                if(Q.Execute(c,t,"UPDATE revora_summer_daily SET kills=kills+1 WHERE uid=@uid AND day=@day AND kills<120","uid",(long)p.Entity.UID,"day",accepted)!=1)return;
                t.Commit();if(r.Kills==119){
                    // A delayed final kill must not remove a newly accepted day.
                    ((System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint,string>>)active)
                        .Remove(new System.Collections.Generic.KeyValuePair<uint,string>(p.Entity.UID,accepted));
                    p.Send("Your summer hunt is complete. Liora has your materials.");
                }
            }}}catch(Exception ex){Q.Fail(p,"summer-hunt",ex);}
        }
        internal static bool CreditSaved(NpgsqlConnection c,NpgsqlTransaction t,Entity killer,ushort map,ushort x,ushort y,uint type,DateTime occurred,out Action refresh) {
            refresh=null;
            if(!enabled || killer==null || killer.EntityFlag!=EntityFlag.Player || killer.Owner==null || !ReferenceEquals(killer.Owner.Entity,killer))return false;
            var p=killer.Owner;string accepted;
            if(!Q.Player(p) || p.Entity.Level<120 || p.Entity.MapID!=map || Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>Constants.pScreenDistance
                || !active.TryGetValue(p.Entity.UID,out accepted) || accepted!=Day(occurred))return false;
            var r=Read(c,t,p.Entity.UID,accepted);if(r==null || r.Kills>=120 || r.Accepted>occurred || occurred<r.Start || occurred>=r.End)return false;
            uint[] types={18,20,57,58};ushort expectedMap=r.Route==0?(ushort)1015:(ushort)1001;
            if(map!=expectedMap || type!=types[r.Route] && !(r.Route==3 && type==83))return false;
            if(Q.Execute(c,t,"UPDATE revora_summer_daily SET kills=kills+1 WHERE uid=@uid AND day=@day AND kills<120","uid",(long)p.Entity.UID,"day",accepted)!=1)
                throw new InvalidOperationException("Summer kill was not saved.");
            if(r.Kills==119)refresh=()=>FinishCredit(p,accepted);
            return true;
        }
        private static void FinishCredit(GameState p,string accepted) {
            try {
                if(((System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint,string>>)active)
                    .Remove(new System.Collections.Generic.KeyValuePair<uint,string>(p.Entity.UID,accepted)))
                    p.Send("Your summer hunt is complete. Liora has your materials.");
            }catch(Exception ex){Q.Fail(p,"summer-hunt-refresh",ex);}
        }
        internal static void RefreshCredit(Entity killer,DateTime occurred) {
            if(!enabled || killer==null || !Q.Player(killer.Owner) || !ReferenceEquals(killer.Owner.Entity,killer))return;
            var p=killer.Owner;string accepted;
            if(!active.TryGetValue(killer.UID,out accepted) || accepted!=Day(occurred))return;
            try{using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var r=Read(c,t,p.Entity.UID,accepted);t.Commit();if(r==null || r.Kills!=120)return;
                FinishCredit(p,accepted);
            }}}catch(Exception ex){Q.Fail(p,"summer-hunt-refresh",ex);}
        }
        private static string Claim(GameState p,bool bonus) {
            string sql=bonus?@"SELECT d.day,d.route,d.kills,d.bonus_source,d.claimed,d.bonus_claimed,d.accepted_at,d.day_start,d.day_end,d.valuable_reward
FROM revora_summer_daily d WHERE d.uid=@uid AND d.kills=120 AND NOT d.bonus_claimed AND (
(d.bonus_source=0 AND EXISTS(SELECT 1 FROM revora_soul_runs s WHERE s.uid=d.uid AND s.day=d.day AND s.state='complete')) OR
(d.bonus_source=1 AND EXISTS(SELECT 1 FROM revora_demon_runs s WHERE s.uid=d.uid AND s.day=d.day AND s.state='complete')) OR
(d.bonus_source=2 AND EXISTS(SELECT 1 FROM revora_power_claims s WHERE s.uid=d.uid AND s.window_start>=d.day_start AND s.window_start<d.day_end)))
ORDER BY d.day LIMIT 1 FOR UPDATE OF d":@"SELECT day,route,kills,bonus_source,claimed,bonus_claimed,accepted_at,day_start,day_end,valuable_reward
FROM revora_summer_daily WHERE uid=@uid AND kills=120 AND NOT claimed ORDER BY day LIMIT 1 FOR UPDATE";
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Progress progress;using(var q=Q.Command(c,t,sql,"uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())progress=r.Read()?ProgressRow(r):null;
                if(progress==null)return bonus?"Complete a summer hunt and its optional task before asking for the extra fragments.":"No finished summer hunt is waiting for its materials.";
                var changes=SummerMaterials.Give(p,bonus?new[]{2,1,0,0}:new[]{6,3,1,1});if(changes==null)return "Make room for the material bundles, then return to me.";
                var rune=!bonus && progress.Valuable?Q.New(4030101,false):null;
                if(rune!=null && p.Inventory.Count+changes.Count(a=>a.Created)+1>40)return "Make room for your fragments and Tortoise Breaker rune.";
                SummerMaterials.Write(c,t,p,changes);
                if(rune!=null)Q.Insert(c,t,p.Entity.UID,rune);
                string update=bonus?"UPDATE revora_summer_daily SET bonus_claimed=true,bonus_ids=@items WHERE uid=@uid AND day=@day":"UPDATE revora_summer_daily SET claimed=true,reward_ids=@items WHERE uid=@uid AND day=@day";
                var rewardIds=changes.Select(a=>(long)a.Item.UID).Concat(rune==null?new long[0]:new[]{(long)rune.UID}).ToArray();
                if(Q.Execute(c,t,update,"items",rewardIds,"uid",(long)p.Entity.UID,"day",progress.Day)!=1)throw new InvalidOperationException("Summer reward receipt was not saved.");
                t.Commit();SummerMaterials.Install(p,changes);if(rune!=null)Q.Install(p,new uint[0],new[]{rune});return bonus?"Two Glow fragments and one Siren fragment are yours.":(rune==null?"Six Glow, three Siren, one Rainbow fragment and one unbind ingredient. Well hunted.":"Your fragments and unbound Tortoise Breaker rune are yours. Well hunted.");
            }}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!enabled || p==null || p.Entity==null || request==null || p.ActiveNpc!=Npc)return false;
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot) {
                if(!Q.Near(p,Npc,1002))return true;
                try {
                    var d=new Npcs(p);byte option=request.OptionID;
                    if(option==1)d.Text(Accept(p));
                    else if(option==2){using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){var r=Read(c,t,p.Entity.UID,Day(Clock()));t.Commit();d.Text(Status(r));}}}
                    else if(option==3 || option==4)d.Text(Claim(p,option==4));
                    else if(option==6)d.Text("Hunt 120 monsters each Pacific day for fragments and an unbound Tortoise Breaker rune. Your journey lasts sixty days. Older contracts keep their promised rewards.");
                    else if(!SummerCrafting.Menu(p,option,d)) {
                        d.Text("I keep a little summer in every feather. Bring me proof of your travels, and I will shape wings for you.");
                        d.Option("Take today's hunt (fragments and rune).",1);d.Option("My hunt progress.",2);d.Option("Claim hunt materials.",3);d.Option("Claim the optional bonus.",4);
                        d.Option("Craft wings or tools.",10);d.Option("My unopened wing packages.",20);d.Option("Explain the journey.",6);
                    }
                    d.Option("Farewell.",255);d.Send();
                }catch(Exception ex){Q.Fail(p,"summer-artisan",ex);}
            }
            return true;
        }
        internal static void Tick() {
            if(!enabled || Clock()<nextCleanup)return;nextCleanup=Clock().AddSeconds(30);SummerCrafting.Cleanup();string day=Day(Clock());
            foreach(var entry in active.ToArray()) {
                GameState p;
                if(!Kernel.GamePool.TryGetValue(entry.Key,out p) || p.ItemSyncRoot==null) {
                    ((System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint,string>>)active).Remove(entry);continue;
                }
                if(entry.Value==day && Q.Player(p,false))continue;
                lock(p.ItemSyncRoot) {
                    // Midnight or a replacement admission may occur after the
                    // scan's clock read. Acceptance uses this same owner lock.
                    GameState currentPlayer;string currentDay;
                    if(!Kernel.GamePool.TryGetValue(entry.Key,out currentPlayer) || !ReferenceEquals(p,currentPlayer))continue;
                    if(!active.TryGetValue(entry.Key,out currentDay) || currentDay!=entry.Value)continue;
                    if(currentDay!=Day(Clock()) || !Q.Player(p,false))
                        ((System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint,string>>)active).Remove(entry);
                }
            }
        }
    }
}
