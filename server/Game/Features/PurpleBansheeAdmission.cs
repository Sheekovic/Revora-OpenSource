using System;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Exact item/day/house transaction. PurpleBansheeEncounter owns confirmation,
    // combat authorization, rewards and interruption recovery.
    internal static class PurpleBansheeAdmission
    {
        private static bool initialized;
        internal const uint Soul=3004259;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal sealed class Run {
            internal Guid Id;internal uint Owner,Item;internal ushort Map,Terrain,X,Y,BeforeStack;
            internal int HouseLevel;internal bool Bound;internal uint[] Party;internal string Day,State;
        }
        internal static void Initialize() {
            if(!ConquerItemInformation.BaseInformations.ContainsKey(Soul))throw new InvalidOperationException("Banshee Spirit catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_purple_banshee_runs(
id uuid PRIMARY KEY,uid bigint NOT NULL,day varchar(8) NOT NULL,map integer NOT NULL CHECK(map BETWEEN 60000 AND 65534),
terrain integer NOT NULL,house_level integer NOT NULL CHECK(house_level BETWEEN 1 AND 5),x integer NOT NULL,y integer NOT NULL,
item_uid bigint NOT NULL,before_stack integer NOT NULL CHECK(before_stack BETWEEN 0 AND 65535),bound boolean NOT NULL,
party bigint[] NOT NULL CHECK(cardinality(party) BETWEEN 1 AND 5),state varchar(12) NOT NULL CHECK(state IN('active','failed','victory')),
started_at timestamptz NOT NULL,ended_at timestamptz,UNIQUE(uid,day));
ALTER TABLE revora_purple_banshee_runs ADD COLUMN IF NOT EXISTS killer bigint;
CREATE UNIQUE INDEX IF NOT EXISTS revora_purple_banshee_active ON revora_purple_banshee_runs(uid) WHERE state='active';");}
            initialized=true;
        }
        internal static bool HasActive(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,uint uid) {
            if(!initialized)return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_purple_banshee_runs WHERE uid=@uid AND state='active')","uid",(long)uid))return Equals(q.ExecuteScalar(),true);
        }
        internal static bool Ready(GameState p,Map map) {
            Map registered;MapsTable.MapInformation info;
            return map!=null && Q.Player(p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0
                && Housing.OwnsLocation(p) && ReferenceEquals(p.Map,map) && map.ID==p.Entity.MapID
                && Kernel.Maps.TryGetValue(map.ID,out registered) && ReferenceEquals(registered,map)
                && MapsTable.MapInformations.TryGetValue(map.ID,out info) && info.ID==map.ID && info.Owner==p.Entity.UID
                && info.BaseID==map.BaseID;
        }
        internal static bool Material(GameState p,ConquerItem item) {
            ConquerItem current;
            return item!=null && item.ID==Soul && item.Position==0 && item.Warehouse==0 && item.Lock==0 && !item.Suspicious
                && !item.InWardrobe && item.Days==0 && item.TimeLeftInMinutes==0 && p.Inventory.TryGetItem(item.UID,out current) && ReferenceEquals(current,item);
        }
        private static uint[] Party(GameState p) {
            var members=p.Team==null?new[]{p}:p.Team.Teammates.Concat(new[]{p}).ToArray();GameState member;
            var others=members.Where(m=>!ReferenceEquals(m,p) && Q.Player(m) && ReferenceEquals(m.Map,p.Map)
                && m.Entity.MapID==p.Entity.MapID && ReferenceEquals(m.Team,p.Team)
                && p.Team.TryGetMember(m.Entity.UID,out member) && ReferenceEquals(member,m))
                .Select(m=>m.Entity.UID).Distinct().OrderBy(id=>id).Take(4);
            return others.Concat(new[]{p.Entity.UID}).OrderBy(id=>id).ToArray();
        }
        internal static bool Cell(Map map,ushort x,ushort y) {
            return x<map.Floor.Bounds.Width && y<map.Floor.Bounds.Height && map.Floor[x,y,MapObjectType.Monster]
                && map.Npcs.Values.All(n=>Kernel.GetDistance(x,y,n.X,n.Y)>=3)
                && map.Entities.Values.All(e=>Kernel.GetDistance(x,y,e.X,e.Y)>=3)
                && Kernel.GamePool.Values.All(p=>p==null || p.Entity==null || p.Entity.MapID!=map.ID || Kernel.GetDistance(x,y,p.Entity.X,p.Entity.Y)>=3);
        }
        internal static Run Admit(GameState p,ConquerItem item,Map map,ushort x,ushort y,Func<bool> context) {
            if(p==null || p.ItemSyncRoot==null || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Purple Banshee admission requires its owner lock.");
            if(map==null || context==null || !Ready(p,map) || !Material(p,item) || !Cell(map,x,y) || !context())return null;
            var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Purple Banshee clock requires UTC.");
            var run=new Run{Id=Guid.NewGuid(),Owner=p.Entity.UID,Item=item.UID,Map=map.ID,Terrain=map.BaseID,X=x,Y=y,
                BeforeStack=item.StackSize,Bound=item.Bound,Party=Party(p),Day=MagnoliaQuest.Day(now),State="active"};
            int after=Math.Max(1,(int)run.BeforeStack)-1;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                SkillTable.LockOwner(c,t,p);
                if(SwordMasterAdmission.HasActive(c,t,p.Entity.UID))return null;
                run.HouseLevel=Housing.Level(c,t,p.Entity.UID);
                using(var q=Q.Command(c,t,@"SELECT h.map,h.houselevel,m.map_id FROM house h JOIN revora_house_maps m ON m.uid=h.id
WHERE h.id=@uid AND h.owner=@uid FOR UPDATE OF h,m","uid",(long)p.Entity.UID))
                using(var r=q.ExecuteReader()) {
                    if(!r.Read() || Convert.ToInt32(r[0])!=run.Terrain || Convert.ToInt32(r[1])!=run.HouseLevel || Convert.ToInt32(r[2])!=run.Map || r.Read())
                        throw new InvalidOperationException("Purple Banshee house ownership changed.");
                }
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_purple_banshee_runs WHERE uid=@uid AND (day>=@day OR state='active'))",
                    "uid",(long)p.Entity.UID,"day",run.Day))if(Equals(q.ExecuteScalar(),true))return null;
                using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                    if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Banshee Spirit inventory requires reload.");
                if(Q.Execute(c,t,@"INSERT INTO revora_purple_banshee_runs(id,uid,day,map,terrain,house_level,x,y,item_uid,before_stack,bound,party,state,started_at)
VALUES(@id,@uid,@day,@map,@terrain,@level,@x,@y,@item,@stack,@bound,@party,'active',@now)",
                    "id",run.Id,"uid",(long)run.Owner,"day",run.Day,"map",(int)run.Map,"terrain",(int)run.Terrain,"level",run.HouseLevel,
                    "x",(int)x,"y",(int)y,"item",(long)run.Item,"stack",(int)run.BeforeStack,"bound",run.Bound,
                    "party",run.Party.Select(id=>(long)id).ToArray(),"now",now)!=1)throw new InvalidOperationException("Purple Banshee attempt was not saved.");
                string sql=after==0?"DELETE FROM items":"UPDATE items SET stacksize=@after";
                if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=3004259 AND stacksize=@stack AND bound=@bound
AND position=0 AND warehouse=0 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                    "after",after,"item",(long)run.Item,"uid",(long)run.Owner,"stack",(int)run.BeforeStack,"bound",run.Bound?1:0)!=1)
                    throw new InvalidOperationException("Banshee Spirit ownership changed.");
                if(!Ready(p,map) || !Material(p,item) || item.StackSize!=run.BeforeStack || item.Bound!=run.Bound
                    || !Cell(map,x,y) || !Party(p).SequenceEqual(run.Party) || !context())throw new InvalidOperationException("Purple Banshee admission context changed.");
                var committedAt=Clock();
                if(committedAt.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Purple Banshee clock requires UTC.");
                if(MagnoliaQuest.Day(committedAt)!=run.Day)return null;
                t.Commit();
            }}
            try {
                if(after==0)Q.Install(p,new[]{run.Item},new ConquerItem[0]);
                else {item.StackSize=(ushort)after;item.Mode=Enums.ItemMode.Update;item.Send(p);}
            }catch(Exception ex){Q.Fail(p,"purple-banshee-admission",ex);throw;}
            return run;
        }
        // Trusted lifecycle participant; exact owner and run are mandatory.
        internal static bool Fail(uint uid,Guid run) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                int changed=Q.Execute(c,t,"UPDATE revora_purple_banshee_runs SET state='failed',ended_at=@now WHERE id=@run AND uid=@uid AND state='active'",
                    "now",Clock(),"run",run,"uid",(long)uid);
                if(changed!=1)return false;t.Commit();return true;
            }}
        }
    }
}
