using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class TravelProgress
    {
        internal enum Arrival { Walk=1, Jump=2, Teleport=3, Login=4 }
        private sealed class Seen { internal readonly HashSet<int> Ids=new HashSet<int>(); }
        private static readonly ConditionalWeakTable<GameState,Seen> sessions=new ConditionalWeakTable<GameState,Seen>();
        private static bool enabled;
        // Fixed world aliases have their own logical 1110 field-1 identity.
        // Do not extend this to arbitrary instances that reuse these floors.
        private static bool WorldIdentity(Map map) {
            if(map.ID==1217)return map.BaseID==1075;
            if(map.ID==1219)return map.BaseID==1212;
            return map.ID==map.BaseID;
        }
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection) {c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_travel_visits(
uid bigint NOT NULL CHECK(uid>0),achievement_id integer NOT NULL,map_id integer NOT NULL CHECK(map_id BETWEEN 1 AND 65535),
x integer NOT NULL CHECK(x BETWEEN 0 AND 65535),y integer NOT NULL CHECK(y BETWEEN 0 AND 65535),
arrival integer NOT NULL CHECK(arrival BETWEEN 1 AND 4),visited_at timestamptz NOT NULL DEFAULT now(),PRIMARY KEY(uid,achievement_id));");}
            enabled=true;
        }
        // Call only after the server accepts this exact destination. Keep the
        // accepted coordinates: a subsequent move need not remain in the region.
        internal static void Arrived(GameState p,Map map,ushort x,ushort y,Arrival arrival) {
            DesertTreasureQuest.Arrived(p,map,x,y,arrival);
            FamilyFeudQuest.Arrived(p,map,x,y,arrival);
            SteedRaceCourse.Arrived(p,map,x,y,arrival);
            MarketErrandSystem.Arrived(p,map,x,y);
            if(!enabled || !Q.Player(p) || map==null || arrival<Arrival.Walk || arrival>Arrival.Login)return;
            lock(p.ItemSyncRoot) try {
                Map owned;
                if(!Q.Player(p) || !Kernel.Maps.TryGetValue(map.ID,out owned) || !ReferenceEquals(owned,map)
                    || !WorldIdentity(map) || map.Floor==null || !map.Floor[x,y,MapObjectType.Player,null])return;
                var state=sessions.GetValue(p,_=>new Seen());
                // The installed world uses terrain identity 1762 for icecrypt-lev1;
                // the reviewed achievement catalog names that same floor 1926.
                // WorldIdentity above excludes instances merely reusing its floor.
                ushort regionMap=map.ID==1762?(ushort)1926:map.ID;
                var candidates=TravelRegions.At(regionMap,x,y).Where(id=>!state.Ids.Contains(id)).ToArray();
                if(candidates.Length==0)return;
                using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Travel owner missing.");
                    if(!Q.Player(p))return;
                    foreach(int id in candidates) {
                        Q.Execute(c,t,@"INSERT INTO revora_travel_visits(uid,achievement_id,map_id,x,y,arrival)
VALUES(@uid,@id,@map,@x,@y,@arrival) ON CONFLICT(uid,achievement_id) DO NOTHING", "uid",(long)p.Entity.UID,
                            "id",id,"map",(int)map.ID,"x",(int)x,"y",(int)y,"arrival",(int)arrival);
                        using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_travel_visits WHERE uid=@uid AND achievement_id=@id)","uid",(long)p.Entity.UID,"id",id))
                            if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Travel visit was not saved.");
                        Complete(c,t,p.Entity.UID,id);
                        using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id)","uid",(long)p.Entity.UID,"id",id))
                            if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Travel achievement was not saved.");
                    }
                    t.Commit();
                }}
                Q.Refresh(p);
                foreach(int id in candidates)state.Ids.Add(id);
            }catch(Exception ex){Q.Fail(p,"travel-arrival",ex);}
        }
        private static void Complete(NpgsqlConnection c,NpgsqlTransaction t,uint uid,int id) {
            string key=id.ToString(CultureInfo.InvariantCulture);
            switch(id) {
                case 10612: ProgressionEvidence.Complete(c,t,uid,10612,"travel-region-v1",key,1);break;
                case 10613: ProgressionEvidence.Complete(c,t,uid,10613,"travel-region-v1",key,1);break;
                case 10614: ProgressionEvidence.Complete(c,t,uid,10614,"travel-region-v1",key,1);break;
                case 10617: ProgressionEvidence.Complete(c,t,uid,10617,"travel-region-v1",key,1);break;
                case 10618: ProgressionEvidence.Complete(c,t,uid,10618,"travel-region-v1",key,1);break;
                case 10619: ProgressionEvidence.Complete(c,t,uid,10619,"travel-region-v1",key,1);break;
                case 10620: ProgressionEvidence.Complete(c,t,uid,10620,"travel-region-v1",key,1);break;
                case 10621: ProgressionEvidence.Complete(c,t,uid,10621,"travel-region-v1",key,1);break;
                case 10622: ProgressionEvidence.Complete(c,t,uid,10622,"travel-region-v1",key,1);break;
                case 10625: ProgressionEvidence.Complete(c,t,uid,10625,"travel-region-v1",key,1);break;
                case 10626: ProgressionEvidence.Complete(c,t,uid,10626,"travel-region-v1",key,1);break;
                case 10629: ProgressionEvidence.Complete(c,t,uid,10629,"travel-region-v1",key,1);break;
                case 10631: ProgressionEvidence.Complete(c,t,uid,10631,"travel-region-v1",key,1);break;
                case 10701: ProgressionEvidence.Complete(c,t,uid,10701,"travel-region-v1",key,1);break;
                case 10702: ProgressionEvidence.Complete(c,t,uid,10702,"travel-region-v1",key,1);break;
                case 10703: ProgressionEvidence.Complete(c,t,uid,10703,"travel-region-v1",key,1);break;
                case 10708: ProgressionEvidence.Complete(c,t,uid,10708,"travel-region-v1",key,1);break;
                case 10709: ProgressionEvidence.Complete(c,t,uid,10709,"travel-region-v1",key,1);break;
                case 10710: ProgressionEvidence.Complete(c,t,uid,10710,"travel-region-v1",key,1);break;
                case 10711: ProgressionEvidence.Complete(c,t,uid,10711,"travel-region-v1",key,1);break;
                case 10712: ProgressionEvidence.Complete(c,t,uid,10712,"travel-region-v1",key,1);break;
                case 10713: ProgressionEvidence.Complete(c,t,uid,10713,"travel-region-v1",key,1);break;
                case 10714: ProgressionEvidence.Complete(c,t,uid,10714,"travel-region-v1",key,1);break;
                case 10715: ProgressionEvidence.Complete(c,t,uid,10715,"travel-region-v1",key,1);break;
                case 10716: ProgressionEvidence.Complete(c,t,uid,10716,"travel-region-v1",key,1);break;
                case 10717: ProgressionEvidence.Complete(c,t,uid,10717,"travel-region-v1",key,1);break;
                case 10718: ProgressionEvidence.Complete(c,t,uid,10718,"travel-region-v1",key,1);break;
                case 10719: ProgressionEvidence.Complete(c,t,uid,10719,"travel-region-v1",key,1);break;
                case 10720: ProgressionEvidence.Complete(c,t,uid,10720,"travel-region-v1",key,1);break;
                case 10721: ProgressionEvidence.Complete(c,t,uid,10721,"travel-region-v1",key,1);break;
                case 10723: ProgressionEvidence.Complete(c,t,uid,10723,"travel-region-v1",key,1);break;
                case 10724: ProgressionEvidence.Complete(c,t,uid,10724,"travel-region-v1",key,1);break;
                case 10725: ProgressionEvidence.Complete(c,t,uid,10725,"travel-region-v1",key,1);break;
                case 10726: ProgressionEvidence.Complete(c,t,uid,10726,"travel-region-v1",key,1);break;
                case 10727: ProgressionEvidence.Complete(c,t,uid,10727,"travel-region-v1",key,1);break;
                case 10801: ProgressionEvidence.Complete(c,t,uid,10801,"travel-region-v1",key,1);break;
                case 10802: ProgressionEvidence.Complete(c,t,uid,10802,"travel-region-v1",key,1);break;
                case 10803: ProgressionEvidence.Complete(c,t,uid,10803,"travel-region-v1",key,1);break;
                case 10804: ProgressionEvidence.Complete(c,t,uid,10804,"travel-region-v1",key,1);break;
                case 10805: ProgressionEvidence.Complete(c,t,uid,10805,"travel-region-v1",key,1);break;
                case 10806: ProgressionEvidence.Complete(c,t,uid,10806,"travel-region-v1",key,1);break;
                case 10807: ProgressionEvidence.Complete(c,t,uid,10807,"travel-region-v1",key,1);break;
                case 10808: ProgressionEvidence.Complete(c,t,uid,10808,"travel-region-v1",key,1);break;
                case 10809: ProgressionEvidence.Complete(c,t,uid,10809,"travel-region-v1",key,1);break;
                case 10810: ProgressionEvidence.Complete(c,t,uid,10810,"travel-region-v1",key,1);break;
                case 10812: ProgressionEvidence.Complete(c,t,uid,10812,"travel-region-v1",key,1);break;
                default: throw new InvalidOperationException("Unreviewed travel achievement.");
            }
        }
    }
}
