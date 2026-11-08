using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using Scenes=Revora.Game.Features.TempestWingsScenes;
namespace Revora.Game.Features
{
    internal static partial class TempestWingsStore
    {
        internal static void InitializeEncounter(){
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"ALTER TABLE revora_tempest_wings ADD COLUMN IF NOT EXISTS feather_uid bigint NOT NULL DEFAULT 0;
CREATE TABLE IF NOT EXISTS revora_tempest_kills(uid bigint PRIMARY KEY REFERENCES revora_tempest_permits(uid),
attempt uuid NOT NULL UNIQUE,body bigint NOT NULL,visit uuid NOT NULL REFERENCES revora_tempest_visits(visit));
CREATE TABLE IF NOT EXISTS revora_tempest_feathers(item_uid bigint PRIMARY KEY,uid bigint NOT NULL REFERENCES revora_tempest_kills(uid),
previous_uid bigint NOT NULL DEFAULT 0);");}
        }
        internal static bool BeginVisit(GameState p,Scenes.Scene scene,Func<bool> context){
            Ready(p,context);if(!Scenes.Registered(scene,p) || Clock()>=scene.Until || !Near(p,1011,212,196))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=2)return false;
                using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_tempest_permits r WHERE r.uid=@uid AND r.scrolls=5
AND (SELECT SUM(spent) FROM revora_tempest_payments p WHERE p.uid=r.uid)=5)","uid",(long)p.Entity.UID))if(!Equals(q.ExecuteScalar(),true))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_tempest_visits(uid,visit,map,active,until_at) VALUES(@uid,@visit,@map,true,@until) ON CONFLICT DO NOTHING",
                    "uid",(long)p.Entity.UID,"visit",scene.Visit,"map",(int)scene.Map.ID,"until",scene.Until)!=1)return false;
                Ready(p,()=>context() && Scenes.Registered(scene,p) && Clock()<scene.Until && Near(p,1011,212,196));t.Commit();return true;
            }}
        }
        internal static bool InScene(GameState p){var s=Scenes.Find(p);return s!=null && s.Entered && !s.Completed && Scenes.Registered(s,p) && Clock()<s.Until && p.Entity.MapID==s.Map.ID && ReferenceEquals(p.Map,s.Map);}
        internal static bool RocKill(GameState p,Entity body,Guid attempt,Func<bool> context){
            Ready(p,context);if(!InScene(p) || !TempestWingsEncounters.Owns(p,body,attempt) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity))return false;
            var scene=Scenes.Find(p);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=2)return false;
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_tempest_visits WHERE uid=@uid AND visit=@visit AND map=@map AND active AND until_at>@now)",
                    "uid",(long)p.Entity.UID,"visit",scene.Visit,"map",(int)scene.Map.ID,"now",Clock()))if(!Equals(q.ExecuteScalar(),true))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_tempest_kills(uid,attempt,body,visit) VALUES(@uid,@attempt,@body,@visit) ON CONFLICT DO NOTHING",
                    "uid",(long)p.Entity.UID,"attempt",attempt,"body",(long)body.UID,"visit",scene.Visit)!=1)return false;
                if(Q.Execute(c,t,"UPDATE revora_tempest_wings SET stage=3 WHERE uid=@uid AND stage=2","uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Tempest kill stage changed.");
                Ready(p,()=>context() && InScene(p) && TempestWingsEncounters.Owns(p,body,attempt));t.Commit();return true;
            }}
        }
        // The durable kill remains claimable even if the bag is full at death.
        internal static bool Feather(GameState p,Func<bool> context){
            Ready(p,context);if(!InScene(p) && !Near(p,1011,212,196))return false;ConquerItem item;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=3 || p.Inventory.Count>=40)return false;
                uint old;
                using(var q=Q.Command(c,t,"SELECT feather_uid FROM revora_tempest_wings WHERE uid=@uid","uid",(long)p.Entity.UID))old=checked((uint)Convert.ToInt64(q.ExecuteScalar()));
                if(old!=0)using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@item)","item",(long)old))if(Equals(q.ExecuteScalar(),true))return false;
                item=Q.New(3006392,true);Q.Insert(c,t,p.Entity.UID,item);
                if(Q.Execute(c,t,"INSERT INTO revora_tempest_feathers(item_uid,uid,previous_uid) VALUES(@item,@uid,@old)","item",(long)item.UID,"uid",(long)p.Entity.UID,"old",(long)old)!=1)throw new InvalidOperationException("Tempest feather receipt missing.");
                if(Q.Execute(c,t,"UPDATE revora_tempest_wings SET feather_uid=@item WHERE uid=@uid AND stage=3 AND feather_uid=@old","item",(long)item.UID,"uid",(long)p.Entity.UID,"old",(long)old)!=1)throw new InvalidOperationException("Tempest feather changed.");
                Ready(p,()=>context() && (InScene(p) || Near(p,1011,212,196)));t.Commit();
            }}Q.Install(p,new uint[0],new[]{item});return true;
        }
        internal static bool Claim(GameState p,Func<bool> context){
            Ready(p,context);if(!Near(p,1011,212,196))return false;uint feather;ConquerItem[] rewards;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=3 || p.Inventory.Count>39)return false;
                using(var q=Q.Command(c,t,"SELECT feather_uid FROM revora_tempest_wings WHERE uid=@uid","uid",(long)p.Entity.UID))feather=checked((uint)Convert.ToInt64(q.ExecuteScalar()));
                ConquerItem item;
                if(!p.Inventory.TryGetItem(feather,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,feather,3006392))return false;
                using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_tempest_feathers f JOIN revora_tempest_kills k ON k.uid=f.uid
JOIN items i ON i.uid=f.item_uid WHERE f.item_uid=@item AND f.uid=@uid AND i.locked=0 AND COALESCE(i.suspicious,0)=0
AND COALESCE(i.inwardrobe,0)=0 AND COALESCE(NULLIF(i.days,''),'0')='0')","item",(long)feather,"uid",(long)p.Entity.UID))if(!Equals(q.ExecuteScalar(),true))return false;
                ConquerItemBaseInformation soul;if(!ConquerItemInformation.BaseInformations.TryGetValue(run.Soul,out soul) || run.Soul<800000 || run.Soul>=900000 || soul.PurificationLevel!=7)throw new InvalidOperationException("Tempest soul changed.");
                Q.Consume(c,t,p,feather,3006392);rewards=new[]{Q.New(204008,true),Q.New(run.Soul,true)};
                foreach(var reward in rewards)Q.Insert(c,t,p.Entity.UID,reward);
                if(Q.Execute(c,t,"UPDATE revora_tempest_wings SET stage=4,rewards=@rewards WHERE uid=@uid AND stage=3","uid",(long)p.Entity.UID,"rewards",rewards.Select(i=>(long)i.UID).ToArray())!=1)throw new InvalidOperationException("Tempest reward receipt changed.");
                Ready(p,()=>context() && Near(p,1011,212,196));ProgressionEvidence.RecordEquipmentQuestAggregate(c,t,p.Entity.UID);t.Commit();
            }}Q.Install(p,new[]{feather},rewards);return true;
        }
    }
}
