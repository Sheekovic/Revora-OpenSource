using System;
using Revora.Client;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class ArenaBotStore
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint,byte> identities=new System.Collections.Concurrent.ConcurrentDictionary<uint,byte>();
        internal static bool IsBotUid(uint uid){return identities.ContainsKey(uid);}
        internal const uint FirstUid = 1300001;
        internal const int Capacity = 9;
        internal static bool Reserved(uint uid) { return uid >= FirstUid && uid < FirstUid + Capacity; }
        internal static readonly string[] Names = { "[AI] Kael", "[AI] Arin", "[AI] Veyra", "[AI] Ryden", "[AI] Nyra", "[AI] Soren", "[AI] Zarek", "[AI] Lyra", "[AI] Toren" };
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_arena_bots(
uid bigint PRIMARY KEY CHECK(uid BETWEEN 1300001 AND 1300009),
name varchar(16) NOT NULL UNIQUE,created_at timestamptz NOT NULL);");
                using(var q=Q.Command(c,null,"SELECT uid FROM revora_arena_bots"))using(var r=q.ExecuteReader())
                    while(r.Read())identities.TryAdd(checked((uint)r.GetInt64(0)),0);
            }
        }
        // No account, password, player inventory or currency balance is cloned.
        // The existing result ledger requires durable identities and two statistic mirrors.
        internal static void Prepare(uint uid, GameState source, byte profession)
        {
            if (!Reserved(uid) || !Q.Player(source) || !System.Threading.Monitor.IsEntered(source.ItemSyncRoot))
                throw new InvalidOperationException("Arena bot snapshot requires its player owner.");
            string name = Names[uid - FirstUid];
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    using(var q=Q.Command(c,t,"SELECT pg_advisory_xact_lock(@uid)","uid",(long)uid))q.ExecuteNonQuery();
                    bool registered;
                    using(var q=Q.Command(c,t,"SELECT name FROM revora_arena_bots WHERE uid=@uid","uid",(long)uid))
                    {
                        var value=q.ExecuteScalar();registered=value!=null;
                        if(registered && !Equals(value,name))throw new InvalidOperationException("Arena bot roster identity changed.");
                    }
                    if(!registered)
                    {
                        using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM entities WHERE uid=@uid) OR EXISTS(SELECT 1 FROM arena WHERE entityid=@uid) OR EXISTS(SELECT 1 FROM teamarena WHERE entityid=@uid)","uid",(long)uid))
                            if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Reserved Arena identity is already occupied.");
                        Q.Execute(c,t,@"INSERT INTO entities(uid,name,owner,level,class,viplevel,onlinepoints,claimedteampk,claimedsteampk,firstcredit,claimedelitepk,cursedtime,swordsoul,bansheespirit,claimedexp,vipleveldays)
VALUES(@uid,@name,'',@level,@class,0,0,0,0,0,0,0,0,0,0,0)","uid",(long)uid,"name",name,"level",(int)source.Entity.Level,"class",(int)profession);
                        Q.Execute(c,t,"INSERT INTO arena(entityid,entityname,arenapoints) VALUES(@uid,@name,4000)","uid",(long)uid,"name",name);
                        Q.Execute(c,t,"INSERT INTO teamarena(entityid,entityname,arenapoints) VALUES(@uid,@name,4000)","uid",(long)uid,"name",name);
                        Q.Execute(c,t,"INSERT INTO revora_arena_bots(uid,name,created_at) VALUES(@uid,@name,@now)","uid",(long)uid,"name",name,"now",DateTime.UtcNow);
                    }
                    if(ArenaMatchLedger.Pending(c,t,uid) || TeamArenaResultStatistics.Pending(c,t,uid))throw new InvalidOperationException("Arena bot has unsettled results.");
                    if(Q.Execute(c,t,"UPDATE entities SET level=@level,class=@class WHERE uid=@uid AND name=@name AND owner='' AND viplevel=0",
                        "level",(int)source.Entity.Level,"class",(int)profession,"uid",(long)uid,"name",name)!=1)
                        throw new InvalidOperationException("Arena bot durable ownership changed.");
                    Q.Execute(c,t,"UPDATE arena SET level=@level,class=@class,model=@model WHERE entityid=@uid","level",(int)source.Entity.Level,"class",(int)profession,"model",371006L,"uid",(long)uid);
                    Q.Execute(c,t,"UPDATE teamarena SET level=@level,class=@class,model=@model WHERE entityid=@uid","level",(int)source.Entity.Level,"class",(int)profession,"model",371006L,"uid",(long)uid);
                    t.Commit();identities.TryAdd(uid,0);
                }
            }
        }
    }
}
