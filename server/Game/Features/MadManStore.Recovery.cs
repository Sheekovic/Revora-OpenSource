using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using Scenes=Revora.Game.Features.MadManScenes;
namespace Revora.Game.Features
{
    internal static partial class MadManStore
    {
        // After evacuation and scene release. Stored/restricted powders must be retrieved;
        // never delete another container's item or refund before its attempt is retired.
        internal static bool RecoverTrial(GameState p,Guid attempt,Func<bool> context){
            Ready(p,context);if(Scenes.HasOwner(p.Entity.UID))return false;var removed=new List<uint>();var added=new List<ConquerItem>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);var trial=ReadTrial(c,t,p,attempt);if(run.Stage!=6 || trial==null || trial.State!=0 || p.Entity.MapID==trial.Map)return false;
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_mad_visits WHERE uid=@uid AND active)","uid",(long)p.Entity.UID))if(Equals(q.ExecuteScalar(),true))return false;
                var powders=new List<Run>();using(var q=Q.Command(c,t,"SELECT colour,item_uid FROM revora_mad_powders WHERE attempt=@attempt ORDER BY colour FOR UPDATE","attempt",attempt))using(var r=q.ExecuteReader())while(r.Read())powders.Add(new Run{Type=729983u+(uint)r.GetInt16(0),Item=checked((uint)r.GetInt64(1))});
                foreach(var powder in powders){object exists;using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@uid FOR UPDATE","uid",(long)powder.Item))exists=q.ExecuteScalar();
                    if(exists==null)continue;if(!Material(c,t,p,powder,powder.Type))return false;removed.Add(powder.Item);
                }
                if(p.Inventory.Count-removed.Count+2>40)return false;
                var toxins=Toxins(c,t,p).Where(x=>x.Type==729980 || x.Type==729981).ToArray();if(toxins.Length!=2 || toxins.Any(x=>x.State!=2))throw new InvalidOperationException("Mad interrupted toxins changed.");
                foreach(var id in removed){var powder=powders.Single(x=>x.Item==id);Q.Consume(c,t,p,id,powder.Type);}
                foreach(var toxin in toxins){
                    using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@uid FOR UPDATE","uid",(long)toxin.Item))if(q.ExecuteScalar()!=null)throw new InvalidOperationException("Mad spent toxin unexpectedly exists.");
                    var item=Q.New(toxin.Type,true);Q.Insert(c,t,p.Entity.UID,item);added.Add(item);
                    if(Q.Execute(c,t,"UPDATE revora_mad_toxins SET item_uid=@new,state=0 WHERE uid=@uid AND life=@life AND type=@type AND item_uid=@old AND state=2","new",(long)item.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)toxin.Type,"old",(long)toxin.Item)!=1)throw new InvalidOperationException("Mad toxin refund changed.");
                    if(Q.Execute(c,t,"INSERT INTO revora_mad_recoveries(uid,life,old_uid,new_uid,type) VALUES(@uid,@life,@old,@new,@type)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",(long)toxin.Item,"new",(long)item.UID,"type",(long)toxin.Type)!=1)throw new InvalidOperationException("Mad toxin refund receipt failed.");
                }
                if(Q.Execute(c,t,"UPDATE revora_mad_trials SET state=2 WHERE id=@id AND state=0","id",attempt)!=1)throw new InvalidOperationException("Mad trial retirement changed.");
                Q.Execute(c,t,"UPDATE revora_mad_mixes SET applied=true WHERE uid=@uid AND life=@life AND outcome=7 AND NOT applied","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn);
                run.Stage=5;run.Item=run.Type=0;Save(c,t,p,run,6);Ready(p,()=>context() && !Scenes.HasOwner(p.Entity.UID));t.Commit();
            }}Q.Install(p,removed,added);return true;
        }
    }
}
