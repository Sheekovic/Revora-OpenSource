using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using E=Revora.Game.Features.SnowBansheeStore;

namespace Revora.Game.Features
{
    internal static class SnowBansheeRewards
    {
        internal static Func<int,int> Roll=RandomIndex;
        private static readonly uint[] Accessories={350001,360001,370001,380001};
        private static uint[] souls,p7;
        private static readonly object Gate=new object();
        private sealed class Origin {
            internal Guid Run;internal bool Bound;internal int Slot;internal uint Owner,Type,ItemUid,FloorUid;internal ushort X,Y;internal Map Map;
            internal DateTime Until,Protected;internal ConquerItem Item;internal FloorItem Drop;
            internal string Key {get{return Run.ToString("N")+"/"+Slot;}}
        }
        private static readonly Dictionary<string,Origin> ground=new Dictionary<string,Origin>();
        private static readonly ConditionalWeakTable<FloorItem,Origin> origins=new ConditionalWeakTable<FloorItem,Origin>();
        private static DateTime nextPoll;
        private static uint lastStudyOwner;
        private static bool initialized;
        private static int RandomIndex(int count) {
            var bytes=new byte[4];uint n,limit=uint.MaxValue-uint.MaxValue%(uint)count;
            using(var rng=RandomNumberGenerator.Create())do{rng.GetBytes(bytes);n=BitConverter.ToUInt32(bytes,0);}while(n>=limit);return (int)(n%(uint)count);
        }
        private static uint Pick(uint[] pool) {int index=Roll(pool.Length);if(index<0 || index>=pool.Length)throw new InvalidOperationException("Snow Banshee loot draw outside pool.");return pool[index];}
        internal static void Initialize() {
            souls=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID>=800000 && i.ID<900000 && i.PurificationLevel>=4 && i.PurificationLevel<=7)
                .Select(i=>i.ID).OrderBy(i=>i).ToArray();
            p7=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID>=800000 && i.ID<900000 && i.PurificationLevel==7).Select(i=>i.ID).OrderBy(i=>i).ToArray();
            if(souls.Length==0 || p7.Length==0 || !ConquerItemInformation.BaseInformations.ContainsKey(1088000) || !ConquerItemInformation.BaseInformations.ContainsKey(730002) || Accessories.Any(id=>!ConquerItemInformation.BaseInformations.ContainsKey(id)
                || ConquerItemInformation.BaseInformations[id].TimeTillRemovInMinutes!=10080))throw new InvalidOperationException("Snow Banshee reward catalog changed.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_snow_banshee_study(
run uuid NOT NULL REFERENCES revora_snow_banshee_runs(id),uid bigint NOT NULL,points integer NOT NULL CHECK(points=100),paid_at timestamptz,PRIMARY KEY(run,uid));
CREATE TABLE IF NOT EXISTS revora_snow_banshee_loot(
run uuid NOT NULL REFERENCES revora_snow_banshee_runs(id),slot integer NOT NULL CHECK(slot BETWEEN 0 AND 2),item_type integer NOT NULL,
owner bigint NOT NULL,bound boolean NOT NULL,expires_at timestamptz NOT NULL,protected_until timestamptz NOT NULL,collector bigint,item_uid bigint UNIQUE,
PRIMARY KEY(run,slot),CHECK((collector IS NULL AND item_uid IS NULL) OR (collector>0 AND item_uid>0)));
CREATE INDEX IF NOT EXISTS revora_snow_banshee_pending_study ON revora_snow_banshee_study(uid) WHERE paid_at IS NULL;
CREATE INDEX IF NOT EXISTS revora_snow_banshee_pending_loot ON revora_snow_banshee_loot(expires_at) WHERE collector IS NULL;");}initialized=true;
        }
        // The combat coordinator must mark the exact victory in this same transaction.
        // This participant does not authorize or manufacture a victory.
        internal static void Record(NpgsqlConnection c,NpgsqlTransaction t,Guid run,GameState killer,uint[] party) {
            if(!initialized || t==null || t.Connection!=c || !Q.Player(killer) || !Monitor.IsEntered(killer.ItemSyncRoot) || !World(killer.Map) || killer.Entity.MapID!=E.MapId
                || party==null || party.Length<1 || party.Length>5 || party.Distinct().Count()!=party.Length || !party.Contains(killer.Entity.UID))
                throw new InvalidOperationException("Snow Banshee rewards require an owned victory transaction.");
            using(var q=Q.Command(c,t,"SELECT party FROM revora_snow_banshee_runs WHERE id=@run AND state='victory' AND ended_at IS NOT NULL AND killer=@killer FOR UPDATE",
                "run",run,"killer",(long)killer.Entity.UID)) {
                var frozen=q.ExecuteScalar() as long[];
                if(frozen==null || !party.OrderBy(uid=>uid).Select(uid=>(long)uid).SequenceEqual(frozen.OrderBy(uid=>uid)))throw new InvalidOperationException("Snow Banshee victory party differs.");
            }
            foreach(uint uid in party)if(Q.Execute(c,t,"INSERT INTO revora_snow_banshee_study(run,uid,points) VALUES(@run,@uid,100)","run",run,"uid",(long)uid)!=1)
                throw new InvalidOperationException("Snow Banshee team reward was not recorded.");
            var items=new[]{Draw(),Draw(),Pick(p7)};var now=E.Clock();
            for(int slot=0;slot<items.Length;slot++)if(Q.Execute(c,t,@"INSERT INTO revora_snow_banshee_loot(run,slot,item_type,owner,bound,expires_at,protected_until)
VALUES(@run,@slot,@type,@owner,@bound,@until,@protected)","run",run,"slot",slot,"type",(int)items[slot],"owner",(long)killer.Entity.UID,"bound",p7.Contains(items[slot]),
                "until",now.AddMinutes(2),"protected",now.AddSeconds(Constants.FloorItemAvailableAfter))!=1)throw new InvalidOperationException("Snow Banshee loot was not recorded.");
        }
        private static uint Draw() {
            int n=Roll(100);if(n<0 || n>=100)throw new InvalidOperationException("Snow Banshee loot draw outside range.");
            return n<10?1088000u:n<60?Pick(souls):n<70?Pick(Accessories):730002u;
        }
        private static bool World(Map map) {return E.Site(map,E.SpawnX,E.SpawnY);}
        internal static bool Managed(FloorItem drop) {Origin o;return drop!=null && origins.TryGetValue(drop,out o);}
        internal static bool Expired(FloorItem drop) {Origin o;return origins.TryGetValue(drop,out o) && E.Clock()>=o.Until;}
        private static void Remove(Origin o) {lock(o.Drop){o.Drop.Type=FloorItem.Remove;o.Map.RemoveFloorItem(o.Drop);
            foreach(var p in Kernel.GamePool.Values)if(Q.Player(p,false) && p.Entity.MapID==o.Map.ID)p.RemoveScreenSpawn(o.Drop,true);}}
        internal static void ClearGround() {lock(Gate){foreach(var o in ground.Values.ToArray())Remove(o);ground.Clear();nextPoll=DateTime.MinValue;}}
        private static void Restore() {
            lock(Gate) {
                foreach(var pair in ground.ToArray()) {var o=pair.Value;FloorItem current;
                    if(!World(o.Map) || E.Clock()>=o.Until || o.Drop.PickedUpAlready || !o.Map.FloorItems.TryGetValue(o.FloorUid,out current) || !ReferenceEquals(current,o.Drop)) {Remove(o);ground.Remove(pair.Key);}}
                Map map;if(!Kernel.Maps.TryGetValue(E.MapId,out map) || !World(map))return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT l.run,l.slot,l.item_type,l.owner,l.expires_at,l.protected_until,r.x,r.y,l.bound
FROM revora_snow_banshee_loot l JOIN revora_snow_banshee_runs r ON r.id=l.run
WHERE r.state='victory' AND l.collector IS NULL AND l.expires_at>@now
AND NOT (replace(l.run::text,'-','') || '/' || l.slot::text=ANY(@shown))
ORDER BY l.expires_at,l.run,l.slot LIMIT 256","now",E.Clock(),"shown",ground.Keys.ToArray()))
                using(var r=q.ExecuteReader())while(r.Read()) {
                    Guid run=r.GetGuid(0);int slot=r.GetInt32(1);string key=run.ToString("N")+"/"+slot;if(ground.ContainsKey(key))continue;
                    ushort mapId=E.MapId,x=checked((ushort)r.GetInt32(6)),y=checked((ushort)r.GetInt32(7));
                    if(!E.Site(map,x,y) || !map.SelectCoordonates(ref x,ref y) || !E.Site(map,x,y))continue;
                    uint type=checked((uint)r.GetInt32(2)),owner=checked((uint)r.GetInt64(3));bool bound=r.GetBoolean(8);
                    if(bound!=p7.Contains(type) || (slot==2 && !p7.Contains(type)) ||
                        (slot!=2 && type!=1088000 && type!=730002 && !souls.Contains(type) && !Accessories.Contains(type)))
                        throw new InvalidOperationException("Snow Banshee saved loot catalog differs.");
                    var item=Q.New(type,bound);item.Plus=type==730002?(byte)2:(byte)0;GameState player;Kernel.GamePool.TryGetValue(owner,out player);
                    var drop=new FloorItem(true){UID=FloorItem.FloorUID.Next,MapID=mapId,X=x,Y=y,Item=item,ItemID=type,Owner=player,OwnerUID=owner,
                        Type=FloorItem.Drop,MapObjType=MapObjectType.Item,ValueType=FloorItem.FloorValueType.Item,OnFloor=Time32.Now,ItemColor=item.Color};
                    while(map.FloorItems.ContainsKey(drop.UID) || map.Npcs.ContainsKey(drop.UID))drop.UID=FloorItem.FloorUID.Next;
                    var o=new Origin{Run=run,Slot=slot,Bound=bound,Type=type,Owner=owner,ItemUid=item.UID,FloorUid=drop.UID,X=x,Y=y,Map=map,Item=item,Drop=drop,
                        Until=r.GetFieldValue<DateTimeOffset>(4).UtcDateTime,Protected=r.GetFieldValue<DateTimeOffset>(5).UtcDateTime};
                    origins.Add(drop,o);map.AddFloorItem(drop);ground.Add(key,o);
                    foreach(var p in Kernel.GamePool.Values)if(Q.Player(p,false) && p.Entity.MapID==mapId && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<Constants.nScreenDistance)drop.SendSpawn(p,false);
                }}
            }
        }
        internal static bool Pickup(GameState p,FloorItem drop) {
            Origin o;if(drop==null || !origins.TryGetValue(drop,out o))return false;if(!Q.Player(p))return true;
            lock(p.ItemSyncRoot)lock(drop)try {
                FloorItem current;if(!Q.Player(p) || !World(o.Map) || p.Trade==null || p.Trade.InTrade || p.Booth!=null || !ReferenceEquals(p.Map,o.Map) || p.Entity.X!=o.X || p.Entity.Y!=o.Y
                    || drop.PickedUpAlready || drop.Type!=FloorItem.Drop || E.Clock()>=o.Until || !o.Map.FloorItems.TryGetValue(o.FloorUid,out current) || !ReferenceEquals(current,drop))return true;
                if(p.Entity.UID!=o.Owner && E.Clock()<o.Protected && !(p.Team!=null && p.Team.PickupItems && p.Team.IsTeammate(o.Owner))){p.Send(Constants.FloorItemNotAvailable);return true;}
                var item=o.Item;
                if(drop.UID!=o.FloorUid || drop.ItemID!=o.Type || !ReferenceEquals(drop.Item,item) || drop.MapID!=o.Map.ID || drop.OwnerUID!=o.Owner || drop.X!=o.X || drop.Y!=o.Y
                    || item.UID!=o.ItemUid || item.ID!=o.Type || item.Bound!=o.Bound || item.Plus!=(o.Type==730002?2:0) || item.Position!=0 || item.Warehouse!=0 || item.StackSize!=1
                    || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0)throw new InvalidOperationException("Snow Banshee loot provenance changed.");
                if(p.Inventory.Count>=40){p.Send(Constants.FullInventory);return true;}
                byte days=Accessories.Contains(item.ID)?(byte)7:(byte)0;DateTime stamp=days==0?default(DateTime):E.Clock().ToLocalTime();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Snow Banshee loot inventory requires reload.");
                    if(Q.Execute(c,t,"UPDATE revora_snow_banshee_loot SET collector=@uid,item_uid=@item WHERE run=@run AND slot=@slot AND item_type=@type AND bound=@bound AND owner=@owner AND collector IS NULL AND expires_at>@now",
                        "uid",(long)p.Entity.UID,"item",(long)item.UID,"run",o.Run,"slot",o.Slot,"type",(int)item.ID,"bound",o.Bound,"owner",(long)o.Owner,"now",E.Clock())!=1)return true;
                    if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability,plus,days,daystamp)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max,@plus,@days,@stamp)","item",(long)item.UID,"type",(int)item.ID,"uid",(long)p.Entity.UID,
                        "plus",(int)item.Plus,"bound",o.Bound?1:0,"dur",(int)item.Durability,"max",(int)item.MaximDurability,"days",days.ToString(System.Globalization.CultureInfo.InvariantCulture),"stamp",stamp.ToBinary())!=1)
                        throw new InvalidOperationException("Snow Banshee loot item was not saved.");
                    if(item.ID==1088000)
                        ProgressionEvidence.Complete(c,t,p.Entity.UID,10523,"snow-banshee-loot",o.Run.ToString("N")+"-"+o.Slot,1);
                    t.Commit();
                }}
                drop.PickedUpAlready=true;Remove(o);item.Days=days;item.DayStamp=stamp;if(days!=0)item.TimeLeftInMinutes=7*24*60*60;
                Q.Install(p,new uint[0],new[]{item});if(o.Bound || item.ID==1088000)Q.Refresh(p);p.Send(Constants.PickupItem(ConquerItemInformation.BaseInformations[item.ID].Name));
            }catch(Exception ex){Q.Fail(p,"snow_banshee-loot-pickup",ex);}return true;
        }
        internal static void PayStudy(GameState p) {
            if(!Q.Player(p,false) || p.Entity.SubClasses==null || !Monitor.TryEnter(p.ItemSyncRoot))return;
            try {
                if(!Q.Player(p,false))return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    int before=p.Entity.SubClasses.StudyPoints;using(var q=Q.Command(c,t,"SELECT studypoints FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(Convert.ToInt32(q.ExecuteScalar())!=before)throw new InvalidOperationException("Snow Banshee Study Point balance requires reload.");
                    var runs=new List<Guid>();using(var q=Q.Command(c,t,"SELECT run FROM revora_snow_banshee_study WHERE uid=@uid AND paid_at IS NULL ORDER BY run LIMIT 64 FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read() && before+(runs.Count+1)*100<=ushort.MaxValue)runs.Add(r.GetGuid(0));
                    if(runs.Count==0)return;int after=before+runs.Count*100;
                    foreach(var run in runs)if(Q.Execute(c,t,"UPDATE revora_snow_banshee_study SET paid_at=@now WHERE run=@run AND uid=@uid AND paid_at IS NULL",
                        "now",E.Clock(),"run",run,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Snow Banshee Study Point claim changed.");
                    if(Q.Execute(c,t,"UPDATE entities SET studypoints=@after WHERE uid=@uid AND studypoints=@before","after",after,"uid",(long)p.Entity.UID,"before",before)!=1)
                        throw new InvalidOperationException("Snow Banshee Study Point reward was not saved.");
                    StudyRewardEvidence.Record(c,t,p,runs.Count*100,"snow_banshee:"+runs[0].ToString("N"));t.Commit();
                    p.Entity.SubClasses.StudyPoints=(ushort)after;Q.Refresh(p);p.Send(new SubClassShowFull(0){ID=8,Study=(ushort)after,StudyReceive=(ushort)(runs.Count*100)}.ToArray());
                    p.Send("Your Snow Banshee victories earned "+(runs.Count*100)+" Study Points.");
                }}
            }catch(Exception ex){Q.Fail(p,"snow_banshee-study",ex);}finally{Monitor.Exit(p.ItemSyncRoot);}
        }
        internal static void Tick(bool force=false) {
            if(!initialized)return;
            if(!force && E.Clock()<nextPoll)return;nextPoll=E.Clock().AddSeconds(5);
            try{Restore();var pending=new List<uint>();
                var online=Kernel.GamePool.Values.Where(p=>Q.Player(p,false)).Select(p=>p.Entity.UID).Distinct().OrderBy(uid=>uid).ToArray();
                var batch=online.Where(uid=>uid>lastStudyOwner).Concat(online.Where(uid=>uid<=lastStudyOwner)).Take(256).Select(uid=>(long)uid).ToArray();
                if(batch.Length==0)return;lastStudyOwner=(uint)batch[batch.Length-1];
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT DISTINCT uid FROM revora_snow_banshee_study WHERE paid_at IS NULL AND uid=ANY(@online)","online",batch))
                using(var r=q.ExecuteReader())while(r.Read())pending.Add(checked((uint)r.GetInt64(0)));}
                foreach(uint uid in pending){GameState p;if(Kernel.GamePool.TryGetValue(uid,out p))PayStudy(p);}
            }catch(Exception ex){Console.WriteLine("[snow_banshee] reward-recovery error="+ex.GetType().Name);}
        }
    }
}
