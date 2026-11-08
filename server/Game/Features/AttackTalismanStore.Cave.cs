using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using Scenes=Revora.Game.Features.AttackTalismanScenes;
namespace Revora.Game.Features
{
    internal static partial class AttackTalismanStore
    {
        internal const uint CavePrice=10000;
        internal static bool BeginVisit(GameState p,Scenes.Scene scene,Func<bool> context){
            Ready(p,context);if(scene!=null && scene.Map.BaseID==Scenes.GardenBaseID)return BeginGardenVisit(p,scene,context);
            if(scene!=null && scene.Map.BaseID==Scenes.LakeBaseID)return BeginLakeVisit(p,scene,context);
            if(!Scenes.Registered(scene,p) || Clock()>=scene.Until || !Near(p,1000,490,618))return false;ulong silver=p.Entity.Money;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=5 || scene.Map.BaseID!=1025 || !Materials(c,t,p,710218).Any(id=>Material(c,t,p,id,710218)) || !Materials(c,t,p,721798).Any(id=>Material(c,t,p,id,721798)))return false;
                if(!run.CavePaid){if(p.Entity.Money<CavePrice)return false;SilverBalance.AssertSaved(c,t,p);silver=p.Entity.Money-CavePrice;SilverBalance.Persist(c,t,p,silver);
                    if(Q.Execute(c,t,"UPDATE revora_attack_talisman SET cave_paid=true WHERE uid=@uid AND stage=5 AND NOT cave_paid","uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Fan cave permit changed.");}
                if(Q.Execute(c,t,"INSERT INTO revora_fan_visits(uid,visit,map,active,until_at) VALUES(@uid,@visit,@map,true,@until) ON CONFLICT DO NOTHING",
                    "uid",(long)p.Entity.UID,"visit",scene.Visit,"map",(int)scene.Map.ID,"until",scene.Until)!=1)return false;
                Ready(p,()=>context() && Scenes.Registered(scene,p) && Clock()<scene.Until && Near(p,1000,490,618));t.Commit();
            }}SilverBalance.ApplyCommitted(p,silver);return true;
        }
        private static bool InCave(GameState p){var s=Scenes.Find(p);return s!=null && s.Entered && !s.Completed && Scenes.Registered(s,p) && Clock()<s.Until && p.Entity.MapID==s.Map.ID;}
        internal static int CaveKind(GameState p){if(!Eligible(p) || !InCave(p))return -1;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();var run=Read(c,null,p);if(run.Stage!=5 || !run.CavePaid)return -1;
            for(int kind=0;kind<2;kind++)using(var q=Q.Command(c,null,"SELECT EXISTS(SELECT 1 FROM revora_fan_cave_kills WHERE uid=@uid AND kind=@kind)","uid",(long)p.Entity.UID,"kind",kind))
                if(!Equals(q.ExecuteScalar(),true))return kind==0 || Materials(c,null,p,721803).Any(id=>Material(c,null,p,id,721803))?kind:-1;
            return -1;
        }}
        internal static bool CaveKill(GameState p,Entity body,Guid attempt,int kind,Func<bool> context){
            Ready(p,context);if(kind<0 || kind>1 || !InCave(p) || !AttackTalismanEncounters.Owns(p,body,attempt,kind) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity))return false;ConquerItem item;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);uint type=kind==0?721803u:721806u;if(run.Stage!=5 || !run.CavePaid || p.Inventory.Count>=40 || Materials(c,t,p,type).Count!=0)return false;
                if(kind==1 && !Materials(c,t,p,721803).Any(id=>Material(c,t,p,id,721803)))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_fan_cave_kills(uid,kind,attempt,body) VALUES(@uid,@kind,@attempt,@body) ON CONFLICT DO NOTHING","uid",(long)p.Entity.UID,"kind",kind,"attempt",attempt,"body",(long)body.UID)!=1)return false;
                item=Q.New(type,true);Track(c,t,p,item);Ready(p,()=>context() && InCave(p) && AttackTalismanEncounters.Owns(p,body,attempt,kind));t.Commit();
            }}Q.Install(p,new uint[0],new[]{item});return true;
        }
        internal static bool UseKettle(GameState p,Func<bool> context){
            Ready(p,context);if(!InCave(p))return false;uint kettle;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=5)return false;kettle=Materials(c,t,p,721806).FirstOrDefault(id=>Material(c,t,p,id,721806));if(kettle==0)return false;
                Consume(c,t,p,kettle,721806);Ready(p,()=>context() && InCave(p));t.Commit();
            }}Q.Install(p,new[]{kettle},new ConquerItem[0]);Scenes.Find(p).Completed=true;return true;
        }
    }
}
