using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class GuildPkAdmission
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
                throw new InvalidOperationException("Guild PK transfer requires a locked session owner.");
        }
        private sealed class Binding {internal Guid Run;internal uint First,Second;internal ushort FirstX,FirstY,SecondX,SecondY;}
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Map,Binding> bindings=new System.Runtime.CompilerServices.ConditionalWeakTable<Map,Binding>();
        private static readonly object bindingGate=new object();
        private static bool Destination(Guid run,Map map) {
            Map actual;Binding binding;
            return map!=null && map.ID!=map.BaseID && Kernel.Maps.TryGetValue(map.ID,out actual) && ReferenceEquals(map,actual)
                && bindings.TryGetValue(map,out binding) && binding.Run==run;
        }
        private static bool FindCamp(Map map,int anchor,out ushort x,out ushort y) {
            for(int radius=0;radius<=8;radius++)for(int dx=-radius;dx<=radius;dx++)for(int dy=-radius;dy<=radius;dy++) {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;int nx=anchor+dx,ny=50+dy;
                if(nx<2 || ny<2 || nx>=map.Floor.Bounds.Width-2 || ny>=map.Floor.Bounds.Height-2 || !map.Floor[nx,ny,MapObjectType.Player]
                    || map.Npcs.Values.Any(n=>Kernel.GetDistance(n.X,n.Y,(ushort)nx,(ushort)ny)<4))continue;
                x=(ushort)nx;y=(ushort)ny;return true;
            }
            x=y=0;return false;
        }
        internal static bool Camp(Guid run,Map map,uint guild,out ushort x,out ushort y) {
            x=y=0;Binding binding;if(!Destination(run,map) || !bindings.TryGetValue(map,out binding))return false;
            if(guild==binding.First){x=binding.FirstX;y=binding.FirstY;return true;}
            if(guild==binding.Second){x=binding.SecondX;y=binding.SecondY;return true;}
            return false;
        }
        internal static bool BindMap(Guid run,Map map) {
            Map actual;if(run==Guid.Empty || map==null || map.BaseID!=700 || map.ID==map.BaseID || !Kernel.Maps.TryGetValue(map.ID,out actual) || !ReferenceEquals(map,actual))return false;
            lock(bindingGate) {
                Binding existing;if(bindings.TryGetValue(map,out existing))return existing.Run==run;
                var binding=new Binding{Run=run};
                if(!FindCamp(map,30,out binding.FirstX,out binding.FirstY) || !FindCamp(map,70,out binding.SecondX,out binding.SecondY)
                    || Kernel.GetDistance(binding.FirstX,binding.FirstY,binding.SecondX,binding.SecondY)<20)return false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                    using(var q=Q.Command(c,t,"SELECT first_guild,second_guild FROM revora_guild_pk_matches WHERE id=@run AND state='scheduled' AND @now>=starts_at-interval '5 minutes' AND @now<starts_at FOR UPDATE","run",run,"now",Clock()))using(var r=q.ExecuteReader()) {
                        if(!r.Read())return false;binding.First=checked((uint)r.GetInt64(0));binding.Second=checked((uint)r.GetInt64(1));
                    }
                    if(Q.Execute(c,t,"INSERT INTO revora_guild_pk_maps(run,map) VALUES(@run,@map) ON CONFLICT DO NOTHING","run",run,"map",(long)map.ID)!=1)return false;
                    t.Commit();
                }}
                bindings.Add(map,binding);return true;
            }
        }
        private static bool Eligible(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p,Guid run,Map map,DateTime now,out Guid admission) {
            admission=Guid.Empty;
            using(var q=Q.Command(c,t,@"SELECT m.id FROM revora_guild_pk_matches m
JOIN revora_guild_pk_weeks w ON w.contest=m.contest JOIN revora_guild_pk_maps f ON f.run=m.id
JOIN revora_guild_contest_owners o ON o.run=m.contest AND o.guild_id=@guild AND o.eligible
WHERE m.id=@run AND m.state='scheduled' AND @guild IN(m.first_guild,m.second_guild)
AND ((m.round<4 AND w.phase='groups') OR (m.round=4 AND w.phase='finals'))
AND @now>=m.starts_at-interval '5 minutes' AND @now<m.starts_at AND f.map=@map FOR UPDATE OF m",
                "run",run,"guild",(long)p.Guild.ID,"map",(long)map.ID,"now",now))if(q.ExecuteScalar()==null)return false;
            SkillTable.LockOwner(c,t,p);
            if(!WayOfHeroesClaims.SevenDayGuildMembership(c,t,p.Entity.UID,p.Guild.ID,now))return false;
            using(var q=Q.Command(c,t,"SELECT admission FROM revora_guild_admissions WHERE uid=@uid AND guild_id=@guild FOR SHARE","uid",(long)p.Entity.UID,"guild",(long)p.Guild.ID)) {
                var value=q.ExecuteScalar();if(value==null)return false;admission=(Guid)value;
            }
            return true;
        }
        internal static bool Reserve(GameState p,Guid run,Map destination) {
            Owner(p);
            if(!GuildOwner(p) || !Destination(run,destination) || p.Map==null || p.Map.ID!=1002 || p.Map.BaseID!=1002
                || p.Trade==null || p.Trade.InTrade || p.Booth!=null)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");Guid admission;var now=Clock();
                if(!Eligible(c,t,p,run,destination,now,out admission))return false;
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid AND mapid=1002","uid",(long)p.Entity.UID))if(q.ExecuteScalar()==null)return false;
                int n=Q.Execute(c,t,@"INSERT INTO revora_guild_pk_transfers(run,uid,map,guild_id,admission,return_x,return_y,state,reserved_at)
VALUES(@run,@uid,@map,@guild,@admission,@x,@y,'reserved',@now) ON CONFLICT DO NOTHING",
                    "run",run,"uid",(long)p.Entity.UID,"map",(long)destination.ID,"guild",(long)p.Guild.ID,"admission",admission,
                    "x",(int)p.Entity.X,"y",(int)p.Entity.Y,"now",now);
                t.Commit();return n==1;
            }}
        }
        internal static bool Confirm(GameState p,Guid run,Map destination) {
            Owner(p);
            if(!GuildOwner(p) || !Destination(run,destination) || !ReferenceEquals(p.Map,destination) || p.Entity.MapID!=destination.ID)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");Guid admission;var now=Clock();
                if(!Eligible(c,t,p,run,destination,now,out admission))return false;
                int n=Q.Execute(c,t,@"INSERT INTO revora_guild_pk_entries(run,uid,guild_id,admission,map,entered_at)
SELECT f.run,f.uid,f.guild_id,f.admission,f.map,@now FROM revora_guild_pk_transfers f
JOIN entities e ON e.uid=f.uid AND e.guildid=f.guild_id AND e.mapid=f.map
WHERE f.run=@run AND f.uid=@uid AND f.state='reserved' AND f.map=@map AND f.guild_id=@guild
AND f.admission=@admission AND f.reserved_at<=@now",
                    "run",run,"uid",(long)p.Entity.UID,"map",(long)destination.ID,"guild",(long)p.Guild.ID,"admission",admission,"now",now);
                if(n!=1)return false;
                if(Q.Execute(c,t,"UPDATE revora_guild_pk_transfers SET state='entered' WHERE run=@run AND uid=@uid AND state='reserved'","run",run,"uid",(long)p.Entity.UID)!=1)
                    throw new InvalidOperationException("Guild PK arrival ownership changed.");
                t.Commit();GuildPkBanner.Admit(p,run,admission);return true;
            }}
        }
        internal static bool Enter(GameState p,Guid run,Map destination) {
            if(!Q.Player(p) || !SessionInstall.TryEnter(p.Entity.UID))return false;bool held=false,reserved=false;
            try {
                if(!(held=Monitor.TryEnter(p.ItemSyncRoot)))return false;
                if(!(reserved=Reserve(p,run,destination)))return false;
                ushort x,y;
                if(!Camp(run,destination,p.Guild.ID,out x,out y))throw new InvalidOperationException("Guild PK camp ownership changed.");
                p.Entity.Teleport(destination.BaseID,destination.ID,x,y);
                if(Confirm(p,run,destination))return true;
                Leave(p,run);return false;
            }catch(Exception ex){
                if(reserved)try{Leave(p,run);}catch(Exception recovery){Q.Fail(p,"guild-pk-return",recovery);}
                Q.Fail(p,"guild-pk-entry",ex);return false;
            }finally {if(held)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}
        }
        // Caller owns the session/item locks, including on failed arrival.
        internal static void Leave(GameState p,Guid run) {
            Owner(p,false);int map,x,y;
            using(var c=DataHolder.MySqlConnection){c.Open();
                using(var q=Q.Command(c,null,"SELECT map,return_x,return_y FROM revora_guild_pk_transfers WHERE run=@run AND uid=@uid AND state<>'returned'","run",run,"uid",(long)p.Entity.UID))using(var r=q.ExecuteReader()) {
                    if(!r.Read())return;map=checked((int)r.GetInt64(0));x=r.GetInt32(1);y=r.GetInt32(2);
                }
                if(p.Entity.MapID==map)GuildPkBanner.Returning(p,run);
                if(p.Entity.MapID==map)p.Entity.Teleport(1002,checked((ushort)x),checked((ushort)y));
                if(p.Entity.MapID==map)throw new InvalidOperationException("Guild PK return failed.");
                using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                    using(var q=Q.Command(c,t,"SELECT id FROM revora_guild_pk_matches WHERE id=@run FOR UPDATE","run",run))q.ExecuteScalar();
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid AND mapid<>@map","uid",(long)p.Entity.UID,"map",map))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Guild PK return is not saved.");
                    Q.Execute(c,t,"UPDATE revora_guild_pk_entries SET active=false WHERE run=@run AND uid=@uid;UPDATE revora_guild_pk_transfers SET state='returned' WHERE run=@run AND uid=@uid",
                        "run",run,"uid",(long)p.Entity.UID);t.Commit();GuildPkBanner.Revoke(p,run);
                }
            }
        }
        internal static void Recover() {
            if(Server.AuthServer!=null || Server.GameServer!=null || Kernel.GamePool.Count!=0)throw new InvalidOperationException("Guild PK transfer recovery requires cold startup.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                var rows=new List<Tuple<Guid,long,long,int,int>>();
                using(var q=Q.Command(c,t,"SELECT run,uid,map,return_x,return_y FROM revora_guild_pk_transfers WHERE state<>'returned' ORDER BY uid LIMIT 4097 FOR UPDATE"))using(var r=q.ExecuteReader())
                    while(r.Read())rows.Add(Tuple.Create(r.GetGuid(0),r.GetInt64(1),r.GetInt64(2),r.GetInt32(3),r.GetInt32(4)));
                if(rows.Count>4096)throw new InvalidOperationException("Guild PK recovery exceeds its batch limit.");
                foreach(var row in rows) {
                    Q.Execute(c,t,"UPDATE entities SET mapid=1002,x=@x,y=@y,hitpoints=GREATEST(COALESCE(hitpoints,1),1) WHERE uid=@uid AND mapid=@map","uid",row.Item2,"map",row.Item3,"x",row.Item4,"y",row.Item5);
                    Q.Execute(c,t,"UPDATE revora_guild_pk_entries SET active=false WHERE run=@run AND uid=@uid;UPDATE revora_guild_pk_transfers SET state='returned' WHERE run=@run AND uid=@uid",
                        "run",row.Item1,"uid",row.Item2);
                }
                Q.Execute(c,t,"UPDATE revora_guild_pk_matches SET state='cancelled' WHERE state IN('scheduled','active') AND id IN(SELECT run FROM revora_guild_pk_maps)");
                t.Commit();
            }}
        }
        internal static bool ReturnOffline(Guid run,uint uid) {
            if(!SessionInstall.TryEnter(uid))return false;
            try {
                GameState online;if(Kernel.GamePool.TryGetValue(uid,out online))return false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                    using(var q=Q.Command(c,t,"SELECT id FROM revora_guild_pk_matches WHERE id=@run AND state IN('complete','cancelled') FOR UPDATE","run",run))if(q.ExecuteScalar()==null)return false;
                    Q.Execute(c,t,@"UPDATE entities e SET mapid=1002,x=f.return_x,y=f.return_y,hitpoints=GREATEST(COALESCE(e.hitpoints,1),1) FROM revora_guild_pk_transfers f
WHERE f.run=@run AND f.uid=@uid AND e.uid=f.uid AND e.mapid=f.map AND f.state<>'returned';
UPDATE revora_guild_pk_entries SET active=false WHERE run=@run AND uid=@uid;
UPDATE revora_guild_pk_transfers SET state='returned' WHERE run=@run AND uid=@uid;",
                        "run",run,"uid",(long)uid);t.Commit();return true;
                }}
            }finally {SessionInstall.Exit(uid);}
        }
    }
}
