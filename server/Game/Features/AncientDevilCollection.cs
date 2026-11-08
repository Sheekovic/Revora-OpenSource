using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Npgsql;
using Revora.Client;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class AncientDevilCollection
    {
        private static bool enabled;
        internal static void Initialize() {
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_ancient_collections(
 id uuid PRIMARY KEY,uid bigint NOT NULL UNIQUE REFERENCES entities(uid),created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE IF NOT EXISTS revora_ancient_collection_items(
 collection uuid NOT NULL REFERENCES revora_ancient_collections(id),item_uid bigint NOT NULL,item_type integer NOT NULL,
 origin_trial uuid NOT NULL REFERENCES revora_ancient_trials(id),PRIMARY KEY(collection,item_uid),
 UNIQUE(collection,item_type),UNIQUE(collection,origin_trial));");t.Commit();}}
            enabled=true;
        }
        // Called inside an earning/exchange transaction, after saved ownership
        // changes, while its canonical owner lock is held. Runtime items may not
        // yet be installed; the durable inventory is authoritative at this seam.
        internal static bool Record(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            if(!enabled)return false;
            if(t==null || t.Connection!=c || !Q.Player(p,false) || !ReferenceEquals(p.Entity.Owner,p)
                || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Ancient collection requires its transaction owner.");
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_ancient_collections WHERE uid=@uid)","uid",(long)p.Entity.UID))
                if(Equals(q.ExecuteScalar(),true))return false;
            var rows=new List<Tuple<uint,uint,Guid>>();
            using(var q=Q.Command(c,t,@"SELECT i.uid,i.id,r.id FROM items i JOIN revora_ancient_trials r ON r.amulet_uid=i.uid AND r.amulet=i.id
WHERE i.entityid=@uid AND i.position=0 AND i.warehouse=0 AND i.bound=0 AND i.stacksize=1 AND i.locked=0
AND COALESCE(i.suspicious,0)=0 AND COALESCE(i.inwardrobe,0)=0 AND COALESCE(NULLIF(i.days,''),'0')='0'
AND r.state='exchanged' ORDER BY i.id,i.uid FOR UPDATE OF i","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()) {
                uint type=checked((uint)Convert.ToInt64(r[1]));if(AncientDevilRules.AmuletName(type)!=null)
                    rows.Add(Tuple.Create(checked((uint)Convert.ToInt64(r[0])),type,r.GetGuid(2)));
            }
            var selected=rows.GroupBy(row=>row.Item2).Select(g=>g.First()).Take(9).ToArray();
            if(selected.Length!=9)return false;
            Guid id=Guid.NewGuid();
            if(Q.Execute(c,t,"INSERT INTO revora_ancient_collections(id,uid) VALUES(@id,@uid)","id",id,"uid",(long)p.Entity.UID)!=1)
                throw new InvalidOperationException("Ancient collection receipt was not saved.");
            foreach(var row in selected)if(Q.Execute(c,t,@"INSERT INTO revora_ancient_collection_items(collection,item_uid,item_type,origin_trial)
VALUES(@id,@item,@type,@origin)","id",id,"item",(long)row.Item1,"type",(int)row.Item2,"origin",row.Item3)!=1)
                throw new InvalidOperationException("Ancient collection provenance was not saved.");
            return ProgressionEvidence.Complete(c,t,p.Entity.UID,11020,"ancient-collection-v1",id.ToString("N"),1);
        }
    }
}
