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
using E=Revora.Game.Features.TeratoEncounter;

namespace Revora.Game.Features
{
    internal static class TeratoRewards
    {
        internal static Func<int,int> Roll=RandomIndex;
        private static readonly uint[] Accessories={350001,360001,370001,380001};
        private static uint[] souls,valuableSouls;
        private static readonly object Gate=new object();
        private sealed class Origin {
            internal Guid Run;internal int Slot;internal uint Owner,Type,ItemUid,FloorUid;internal ushort X,Y;internal Map Map;
            internal DateTime Until,Protected;internal bool Bound;internal ConquerItem Item;internal FloorItem Drop;
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
        private static uint Pick(uint[] pool) {int index=Roll(pool.Length);if(index<0 || index>=pool.Length)throw new InvalidOperationException("Terato loot draw outside pool.");return pool[index];}
        internal static void Initialize() {
            souls=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID>=800000 && i.ID<900000 && i.PurificationLevel>=1 && i.PurificationLevel<=6)
                .Select(i=>i.ID).OrderBy(i=>i).ToArray();
            valuableSouls=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID>=800000 && i.ID<900000 && i.PurificationLevel==7)
                .Select(i=>i.ID).OrderBy(i=>i).ToArray();
            if(valuableSouls.Length==0 || souls.Length==0 || !ConquerItemInformation.BaseInformations.ContainsKey(720399) || Accessories.Any(id=>!ConquerItemInformation.BaseInformations.ContainsKey(id)
                || ConquerItemInformation.BaseInformations[id].TimeTillRemovInMinutes!=10080))throw new InvalidOperationException("Terato reward catalog changed.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_terato_study(
run uuid NOT NULL REFERENCES revora_terato_runs(id),uid bigint NOT NULL,points integer NOT NULL CHECK(points=50),paid_at timestamptz,PRIMARY KEY(run,uid));
CREATE TABLE IF NOT EXISTS revora_terato_loot(
run uuid NOT NULL REFERENCES revora_terato_runs(id),slot integer NOT NULL CHECK(slot BETWEEN 0 AND 2),item_type integer NOT NULL,
owner bigint NOT NULL,expires_at timestamptz NOT NULL,protected_until timestamptz NOT NULL,collector bigint,item_uid bigint UNIQUE,
PRIMARY KEY(run,slot),CHECK((collector IS NULL AND item_uid IS NULL) OR (collector>0 AND item_uid>0)));
ALTER TABLE revora_terato_loot ADD COLUMN IF NOT EXISTS bound boolean NOT NULL DEFAULT false;
CREATE INDEX IF NOT EXISTS revora_terato_pending_study ON revora_terato_study(uid) WHERE paid_at IS NULL;
CREATE INDEX IF NOT EXISTS revora_terato_pending_loot ON revora_terato_loot(expires_at) WHERE collector IS NULL;");}initialized=true;
        }
        internal static void Record(NpgsqlConnection c,NpgsqlTransaction t,Guid run,uint killer,uint[] party) {
            foreach(uint uid in party.Distinct())if(Q.Execute(c,t,"INSERT INTO revora_terato_study(run,uid,points) VALUES(@run,@uid,50)","run",run,"uid",(long)uid)!=1)
                throw new InvalidOperationException("Terato team reward was not recorded.");
            bool valuable;using(var q=Q.Command(c,t,"SELECT valuable_reward FROM revora_terato_runs WHERE id=@run","run",run)) {
                object contract=q.ExecuteScalar();if(!(contract is bool))throw new InvalidOperationException("Terato reward contract missing.");valuable=(bool)contract;
            }
            var items=new[]{720399u,Pick(Accessories),Pick(valuable?valuableSouls:souls)};var now=E.Clock();
            for(int slot=0;slot<items.Length;slot++)if(Q.Execute(c,t,@"INSERT INTO revora_terato_loot(run,slot,item_type,owner,expires_at,protected_until,bound)
VALUES(@run,@slot,@type,@owner,@until,@protected,@bound)","bound",valuable && slot==2,"run",run,"slot",slot,"type",(int)items[slot],"owner",(long)killer,
                "until",now.AddMinutes(2),"protected",now.AddSeconds(Constants.FloorItemAvailableAfter))!=1)throw new InvalidOperationException("Terato loot was not recorded.");
        }
        internal static bool Managed(FloorItem drop) {Origin o;return drop!=null && origins.TryGetValue(drop,out o);}
        internal static bool Expired(FloorItem drop) {Origin o;return origins.TryGetValue(drop,out o) && E.Clock()>=o.Until;}
        private static void Remove(Origin o) {lock(o.Drop){o.Drop.Type=FloorItem.Remove;o.Map.RemoveFloorItem(o.Drop);
            foreach(var p in Kernel.GamePool.Values)if(Q.Player(p,false) && p.Entity.MapID==o.Map.ID)p.RemoveScreenSpawn(o.Drop,true);}}
        internal static void ClearGround() {lock(Gate){foreach(var o in ground.Values.ToArray())Remove(o);ground.Clear();nextPoll=DateTime.MinValue;}}
        private static void Restore() {
            lock(Gate) {
                foreach(var pair in ground.ToArray()) {var o=pair.Value;FloorItem current;
                    if(E.Clock()>=o.Until || o.Drop.PickedUpAlready || !o.Map.FloorItems.TryGetValue(o.FloorUid,out current) || !ReferenceEquals(current,o.Drop)) {Remove(o);ground.Remove(pair.Key);}}
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT l.run,l.slot,l.item_type,l.owner,l.expires_at,l.protected_until,r.map,r.x,r.y,l.bound
FROM revora_terato_loot l JOIN revora_terato_runs r ON r.id=l.run WHERE l.collector IS NULL AND l.expires_at>@now ORDER BY l.expires_at LIMIT 256","now",E.Clock()))
                using(var r=q.ExecuteReader())while(r.Read()) {
                    Guid run=r.GetGuid(0);int slot=r.GetInt32(1);string key=run.ToString("N")+"/"+slot;if(ground.ContainsKey(key))continue;
                    Map map;ushort mapId=checked((ushort)r.GetInt32(6));if(!Kernel.Maps.TryGetValue(mapId,out map) || map.BaseID!=mapId)continue;
                    ushort x=checked((ushort)r.GetInt32(7)),y=checked((ushort)r.GetInt32(8));if(!map.SelectCoordonates(ref x,ref y))continue;
                    uint type=checked((uint)r.GetInt32(2)),owner=checked((uint)r.GetInt64(3));var item=Q.New(type,r.GetBoolean(9));GameState player;Kernel.GamePool.TryGetValue(owner,out player);
                    var drop=new FloorItem(true){UID=FloorItem.FloorUID.Next,MapID=mapId,X=x,Y=y,Item=item,ItemID=type,Owner=player,OwnerUID=owner,
                        Type=FloorItem.Drop,MapObjType=MapObjectType.Item,ValueType=FloorItem.FloorValueType.Item,OnFloor=Time32.Now,ItemColor=item.Color};
                    while(map.FloorItems.ContainsKey(drop.UID) || map.Npcs.ContainsKey(drop.UID))drop.UID=FloorItem.FloorUID.Next;
                    var o=new Origin{Run=run,Slot=slot,Type=type,Owner=owner,ItemUid=item.UID,FloorUid=drop.UID,X=x,Y=y,Map=map,Item=item,Drop=drop,Bound=item.Bound,
                        Until=r.GetFieldValue<DateTimeOffset>(4).UtcDateTime,Protected=r.GetFieldValue<DateTimeOffset>(5).UtcDateTime};
                    origins.Add(drop,o);map.AddFloorItem(drop);ground.Add(key,o);
                    foreach(var p in Kernel.GamePool.Values)if(Q.Player(p,false) && p.Entity.MapID==mapId && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<Constants.nScreenDistance)drop.SendSpawn(p,false);
                }}
            }
        }
        internal static bool Pickup(GameState p,FloorItem drop) {
            Origin o;if(drop==null || !origins.TryGetValue(drop,out o))return false;if(!Q.Player(p))return true;
            lock(p.ItemSyncRoot)lock(drop)try {
                FloorItem current;if(!Q.Player(p) || p.Trade==null || p.Trade.InTrade || p.Booth!=null || !ReferenceEquals(p.Map,o.Map) || p.Entity.X!=o.X || p.Entity.Y!=o.Y
                    || drop.PickedUpAlready || drop.Type!=FloorItem.Drop || E.Clock()>=o.Until || !o.Map.FloorItems.TryGetValue(o.FloorUid,out current) || !ReferenceEquals(current,drop))return true;
                if(p.Entity.UID!=o.Owner && E.Clock()<o.Protected && !(p.Team!=null && p.Team.PickupItems && p.Team.IsTeammate(o.Owner))){p.Send(Constants.FloorItemNotAvailable);return true;}
                var item=o.Item;
                if(drop.UID!=o.FloorUid || drop.ItemID!=o.Type || !ReferenceEquals(drop.Item,item) || drop.MapID!=o.Map.ID || drop.OwnerUID!=o.Owner || drop.X!=o.X || drop.Y!=o.Y
                    || item.UID!=o.ItemUid || item.ID!=o.Type || item.Bound!=o.Bound || item.Plus!=0 || item.Position!=0 || item.Warehouse!=0 || item.StackSize!=1
                    || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0)throw new InvalidOperationException("Terato loot provenance changed.");
                if(p.Inventory.Count>=40){p.Send(Constants.FullInventory);return true;}
                byte days=Accessories.Contains(item.ID)?(byte)7:(byte)0;DateTime stamp=days==0?default(DateTime):E.Clock().ToLocalTime();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Terato loot inventory requires reload.");
                    if(Q.Execute(c,t,"UPDATE revora_terato_loot SET collector=@uid,item_uid=@item WHERE run=@run AND slot=@slot AND item_type=@type AND bound=@bound AND collector IS NULL AND expires_at>@now",
                        "uid",(long)p.Entity.UID,"item",(long)item.UID,"run",o.Run,"slot",o.Slot,"type",(int)item.ID,"bound",o.Bound,"now",E.Clock())!=1)return true;
                    if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability,days,daystamp)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max,@days,@stamp)","bound",o.Bound?1:0,"item",(long)item.UID,"type",(int)item.ID,"uid",(long)p.Entity.UID,
                        "dur",(int)item.Durability,"max",(int)item.MaximDurability,"days",days.ToString(System.Globalization.CultureInfo.InvariantCulture),"stamp",stamp.ToBinary())!=1)
                        throw new InvalidOperationException("Terato loot item was not saved.");
                    if(o.Slot==2 && (souls.Contains(item.ID) || valuableSouls.Contains(item.ID))) {
                        ProgressionEvidence.Complete(c,t,p.Entity.UID,10932,"dragon-slayer-v1",o.Run.ToString("N"),1);
                        using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10932)","uid",(long)p.Entity.UID))
                            if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Dragon Slayer achievement was not saved.");
                    }
                    t.Commit();
                }}
                drop.PickedUpAlready=true;Remove(o);item.Days=days;item.DayStamp=stamp;if(days!=0)item.TimeLeftInMinutes=7*24*60*60;
                Q.Install(p,new uint[0],new[]{item});if(o.Slot==2)Q.Refresh(p);p.Send(Constants.PickupItem(ConquerItemInformation.BaseInformations[item.ID].Name));
            }catch(Exception ex){Q.Fail(p,"terato-loot-pickup",ex);}return true;
        }
        internal static void PayStudy(GameState p) {
            if(!Q.Player(p,false) || p.Entity.SubClasses==null || !Monitor.TryEnter(p.ItemSyncRoot))return;
            try {
                if(!Q.Player(p,false))return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    int before=p.Entity.SubClasses.StudyPoints;using(var q=Q.Command(c,t,"SELECT studypoints FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(Convert.ToInt32(q.ExecuteScalar())!=before)throw new InvalidOperationException("Terato Study Point balance requires reload.");
                    var runs=new List<Guid>();using(var q=Q.Command(c,t,"SELECT run FROM revora_terato_study WHERE uid=@uid AND paid_at IS NULL ORDER BY run LIMIT 64 FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read() && before+(runs.Count+1)*50<=ushort.MaxValue)runs.Add(r.GetGuid(0));
                    if(runs.Count==0)return;int after=before+runs.Count*50;
                    foreach(var run in runs)if(Q.Execute(c,t,"UPDATE revora_terato_study SET paid_at=@now WHERE run=@run AND uid=@uid AND paid_at IS NULL",
                        "now",E.Clock(),"run",run,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Terato Study Point claim changed.");
                    if(Q.Execute(c,t,"UPDATE entities SET studypoints=@after WHERE uid=@uid AND studypoints=@before","after",after,"uid",(long)p.Entity.UID,"before",before)!=1)
                        throw new InvalidOperationException("Terato Study Point reward was not saved.");
                    StudyRewardEvidence.Record(c,t,p,runs.Count*50,"terato:"+runs[0].ToString("N"));t.Commit();
                    p.Entity.SubClasses.StudyPoints=(ushort)after;Q.Refresh(p);p.Send(new SubClassShowFull(0){ID=8,Study=(ushort)after,StudyReceive=(ushort)(runs.Count*50)}.ToArray());
                    p.Send("Your dragon hunts earned "+(runs.Count*50)+" Study Points.");
                }}
            }catch(Exception ex){Q.Fail(p,"terato-study",ex);}finally{Monitor.Exit(p.ItemSyncRoot);}
        }
        internal static void Tick(bool force=false) {
            if(!initialized)return;
            if(!force && E.Clock()<nextPoll)return;nextPoll=E.Clock().AddSeconds(5);
            try{Restore();var pending=new List<uint>();
                var online=Kernel.GamePool.Values.Where(p=>Q.Player(p,false)).Select(p=>p.Entity.UID).Distinct().OrderBy(uid=>uid).ToArray();
                var batch=online.Where(uid=>uid>lastStudyOwner).Concat(online.Where(uid=>uid<=lastStudyOwner)).Take(256).Select(uid=>(long)uid).ToArray();
                if(batch.Length==0)return;lastStudyOwner=(uint)batch[batch.Length-1];
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT DISTINCT uid FROM revora_terato_study WHERE paid_at IS NULL AND uid=ANY(@online)","online",batch))
                using(var r=q.ExecuteReader())while(r.Read())pending.Add(checked((uint)r.GetInt64(0)));}
                foreach(uint uid in pending){GameState p;if(Kernel.GamePool.TryGetValue(uid,out p))PayStudy(p);}
            }catch(Exception ex){Console.WriteLine("[terato] reward-recovery error="+ex.GetType().Name);}
        }
    }
}
