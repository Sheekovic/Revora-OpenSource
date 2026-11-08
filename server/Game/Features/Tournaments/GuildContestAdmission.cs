using System;
using System.Collections.Generic;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class GuildContestAdmission
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static bool GuildOwner(GameState p) {
            Revora.Game.ConquerStructures.Society.Guild guild;
            Revora.Game.ConquerStructures.Society.Guild.Member member;
            return p.Guild!=null && p.AsMember!=null && p.Entity.GuildID==p.Guild.ID
                && Kernel.Guilds.TryGetValue(p.Guild.ID,out guild) && ReferenceEquals(guild,p.Guild)
                && guild.Members.TryGetValue(p.Entity.UID,out member) && ReferenceEquals(member,p.AsMember);
        }
        private static void Owner(GameState p,bool alive=true) {
            if(!Q.Player(p,alive) || !SessionInstall.Holds(p.Entity.UID) || !Monitor.IsEntered(p.ItemSyncRoot))
                throw new InvalidOperationException("Guild contest transfer requires a locked session owner.");
        }
        internal static bool Reserve(GameState p,Guid run,Map destination) {
            Owner(p);Map current;
            if(run==Guid.Empty || !GuildOwner(p) || p.Entity.Level==0
                || p.Map==null || p.Map.ID!=1002 || p.Map.BaseID!=1002 || p.Trade==null || p.Trade.InTrade || p.Booth!=null
                || destination==null || destination.ID==destination.BaseID || !Kernel.Maps.TryGetValue(destination.ID,out current) || !ReferenceEquals(current,destination))return false;
            int band=GuildPkRules.ContestBand(p.Entity.Level);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                using(var q=Q.Command(c,t,@"SELECT r.id FROM revora_guild_contests r JOIN revora_guild_contest_maps m ON m.run=r.id
WHERE r.id=@run AND r.state='open' AND @now>=r.starts_at-interval '5 minutes' AND @now<r.starts_at AND m.band=@band AND m.map=@map FOR UPDATE OF r",
                    "run",run,"now",Clock(),"band",band,"map",(long)destination.ID))if(q.ExecuteScalar()==null)return false;
                SkillTable.LockOwner(c,t,p);
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid AND guildid=@guild AND guildrank>0 AND mapid=1002 AND level=@level",
                    "uid",(long)p.Entity.UID,"guild",(long)p.Guild.ID,"level",(int)p.Entity.Level))if(q.ExecuteScalar()==null)return false;
                int n=Q.Execute(c,t,@"INSERT INTO revora_guild_contest_transfers(run,uid,band,map,guild_id,return_map,return_x,return_y,state,reserved_at)
VALUES(@run,@uid,@band,@map,@guild,1002,@x,@y,'reserved',@now) ON CONFLICT DO NOTHING",
                    "run",run,"uid",(long)p.Entity.UID,"band",band,"map",(long)destination.ID,"guild",(long)p.Guild.ID,"x",(int)p.Entity.X,"y",(int)p.Entity.Y,"now",Clock());
                t.Commit();return n==1;
            }}
        }
        internal static bool Confirm(GameState p,Guid run,Map destination) {
            Owner(p);Map current;
            if(destination==null || !GuildOwner(p) || p.Entity.Level==0 || !ReferenceEquals(p.Map,destination) || p.Entity.MapID!=destination.ID
                || !Kernel.Maps.TryGetValue(destination.ID,out current) || !ReferenceEquals(current,destination))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                using(var q=Q.Command(c,t,"SELECT id FROM revora_guild_contests WHERE id=@run AND state='open' AND @now<starts_at FOR UPDATE","run",run,"now",Clock()))if(q.ExecuteScalar()==null)return false;
                SkillTable.LockOwner(c,t,p);
                if(!GuildContestTable.BindGuild(c,t,run,p.Guild.ID))return false;
                int n=Q.Execute(c,t,@"INSERT INTO revora_guild_contest_entries(run,uid,guild_id,band,map,entered_at)
SELECT f.run,f.uid,f.guild_id,f.band,f.map,@now FROM revora_guild_contest_transfers f
JOIN entities e ON e.uid=f.uid AND e.guildid=f.guild_id AND e.guildrank>0 AND e.mapid=f.map
WHERE f.run=@run AND f.uid=@uid AND f.state='reserved' AND f.map=@map AND f.reserved_at<=@now
AND f.guild_id=@guild AND f.band=@band AND e.level=@level",
                    "run",run,"uid",(long)p.Entity.UID,"map",(long)destination.ID,"now",Clock(),
                    "guild",(long)p.Guild.ID,"band",GuildPkRules.ContestBand(p.Entity.Level),"level",(int)p.Entity.Level);
                if(n!=1)return false;
                if(Q.Execute(c,t,"UPDATE revora_guild_contest_transfers SET state='entered' WHERE run=@run AND uid=@uid AND state='reserved'","run",run,"uid",(long)p.Entity.UID)!=1)
                    throw new InvalidOperationException("Guild contest arrival ownership changed.");
                t.Commit();return true;
            }}
        }
        internal static bool Enter(GameState p,Guid run,Map destination) {
            if(!Q.Player(p) || !SessionInstall.TryEnter(p.Entity.UID))return false;bool held=false,reserved=false;
            try {
                if(!(held=Monitor.TryEnter(p.ItemSyncRoot)))return false;
                if(!(reserved=Reserve(p,run,destination)))return false;
                p.Entity.Teleport(destination.BaseID,destination.ID,40,40);
                if(Confirm(p,run,destination))return true;
                Leave(p,run);return false;
            }catch(Exception ex){
                if(reserved)try{Leave(p,run);}catch(Exception recovery){Q.Fail(p,"guild-contest-return",recovery);}
                Q.Fail(p,"guild-contest-entry",ex);return false;
            }finally {if(held)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}
        }
        // Caller owns the session/item locks, including on failed arrival.
        internal static void Leave(GameState p,Guid run) {
            Owner(p,false);int map,x,y;
            using(var c=DataHolder.MySqlConnection){c.Open();
                using(var q=Q.Command(c,null,"SELECT map,return_x,return_y FROM revora_guild_contest_transfers WHERE run=@run AND uid=@uid AND state<>'returned'","run",run,"uid",(long)p.Entity.UID))using(var r=q.ExecuteReader()) {
                    if(!r.Read())return;map=checked((int)r.GetInt64(0));x=r.GetInt32(1);y=r.GetInt32(2);
                }
                if(p.Entity.MapID==map)p.Entity.Teleport(1002,checked((ushort)x),checked((ushort)y));
                if(p.Entity.MapID==map)throw new InvalidOperationException("Guild contest return failed.");
                using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                    using(var q=Q.Command(c,t,"SELECT id FROM revora_guild_contests WHERE id=@run FOR UPDATE","run",run))q.ExecuteScalar();
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid AND mapid<>@map","uid",(long)p.Entity.UID,"map",map))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Guild contest return is not saved.");
                    Q.Execute(c,t,"UPDATE revora_guild_contest_entries SET active=false WHERE run=@run AND uid=@uid;UPDATE revora_guild_contest_transfers SET state='returned' WHERE run=@run AND uid=@uid",
                        "run",run,"uid",(long)p.Entity.UID);t.Commit();
                }
            }
        }
        internal static void Recover() {
            if(Server.AuthServer!=null || Server.GameServer!=null || Kernel.GamePool.Count!=0)throw new InvalidOperationException("Guild contest transfer recovery requires cold startup.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                var rows=new List<Tuple<Guid,long,long,int,int>>();
                using(var q=Q.Command(c,t,"SELECT run,uid,map,return_x,return_y FROM revora_guild_contest_transfers WHERE state<>'returned' ORDER BY uid LIMIT 4097 FOR UPDATE"))using(var r=q.ExecuteReader())
                    while(r.Read())rows.Add(Tuple.Create(r.GetGuid(0),r.GetInt64(1),r.GetInt64(2),r.GetInt32(3),r.GetInt32(4)));
                if(rows.Count>4096)throw new InvalidOperationException("Guild contest recovery exceeds its batch limit.");
                foreach(var row in rows) {
                    Q.Execute(c,t,"UPDATE entities SET mapid=1002,x=@x,y=@y WHERE uid=@uid AND mapid=@map","uid",row.Item2,"map",row.Item3,"x",row.Item4,"y",row.Item5);
                    Q.Execute(c,t,"UPDATE revora_guild_contest_entries SET active=false WHERE run=@run AND uid=@uid;UPDATE revora_guild_contest_transfers SET state='returned' WHERE run=@run AND uid=@uid",
                        "run",row.Item1,"uid",row.Item2);
                }
                t.Commit();
            }}
        }
        internal static bool ReturnOffline(Guid run,uint uid) {
            if(!SessionInstall.TryEnter(uid))return false;
            try {
                GameState online;if(Kernel.GamePool.TryGetValue(uid,out online))return false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                    using(var q=Q.Command(c,t,"SELECT id FROM revora_guild_contests WHERE id=@run AND state IN('frozen','cancelled') FOR UPDATE","run",run))if(q.ExecuteScalar()==null)return false;
                    Q.Execute(c,t,@"UPDATE entities e SET mapid=1002,x=f.return_x,y=f.return_y FROM revora_guild_contest_transfers f
WHERE f.run=@run AND f.uid=@uid AND e.uid=f.uid AND e.mapid=f.map AND f.state<>'returned';
UPDATE revora_guild_contest_entries SET active=false WHERE run=@run AND uid=@uid;
UPDATE revora_guild_contest_transfers SET state='returned' WHERE run=@run AND uid=@uid;",
                        "run",run,"uid",(long)uid);t.Commit();return true;
                }}
            }finally {SessionInstall.Exit(uid);}
        }
    }
}
