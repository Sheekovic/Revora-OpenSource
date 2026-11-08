using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DefenseTalismanStore
    {
        internal static bool BeginVisit(GameState p,DefenseTalismanScenes.Scene scene,Func<bool> context){
            Ready(p,context);
            if(!DefenseTalismanScenes.Registered(scene,p) || Clock()>=scene.Until)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);
                bool dungeon=scene.Map.BaseID>=1785 && scene.Map.BaseID<=1787;
                int expected=dungeon?8:scene.Map.BaseID==1782?1:scene.Map.BaseID==1783?7:scene.Map.BaseID==1784?4:0;
                if(expected==0 || run.Stage!=expected || expected==4 && run.DoorStep!=3)return false;
                if(dungeon && (run.DungeonFloor!=scene.Map.BaseID-1784 || !Materials(c,t,p,721786).Any(id=>Material(c,t,p,id,721786))))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_tower_visits(uid,visit,map,active,until_at) VALUES(@uid,@visit,@map,true,@until) ON CONFLICT DO NOTHING",
                    "uid",(long)p.Entity.UID,"visit",scene.Visit,"map",(int)scene.Map.ID,"until",scene.Until)!=1)return false;
                Ready(p,()=>context() && DefenseTalismanScenes.Registered(scene,p) && Clock()<scene.Until);t.Commit();return true;
            }}
        }
        internal static int HouseKills(GameState p,Guid visit){
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT COUNT(*) FROM revora_tower_house_kills WHERE uid=@uid AND visit=@visit","uid",(long)p.Entity.UID,"visit",visit))return Convert.ToInt32(q.ExecuteScalar());}
        }
        internal static bool HouseKill(GameState p,Entity body,Guid attempt,Func<bool> context){
            Ready(p,context);var scene=DefenseTalismanScenes.Find(p);
            if(scene==null || !scene.Entered || (scene.Map.BaseID!=1782 && scene.Map.BaseID!=1783))return false;
            int kind=scene.Map.BaseID==1782?1:5;
            if(!DefenseTalismanEncounters.Owns(p,body,attempt,kind) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=(kind==1?1:7))return false;
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_tower_visits WHERE uid=@uid AND visit=@visit AND active AND map=@map AND until_at>@now)",
                    "uid",(long)p.Entity.UID,"visit",scene.Visit,"map",(int)scene.Map.ID,"now",Clock()))if(!Equals(q.ExecuteScalar(),true))return false;
                using(var q=Q.Command(c,t,"SELECT COUNT(*) FROM revora_tower_house_kills WHERE uid=@uid AND visit=@visit","uid",(long)p.Entity.UID,"visit",scene.Visit))if(Convert.ToInt32(q.ExecuteScalar())>=5)return false;
                if(Q.Execute(c,t,"INSERT INTO revora_tower_house_kills(attempt,uid,visit,body) VALUES(@attempt,@uid,@visit,@body) ON CONFLICT DO NOTHING",
                    "attempt",attempt,"uid",(long)p.Entity.UID,"visit",scene.Visit,"body",(long)body.UID)!=1)return false;
                Ready(p,()=>context() && DefenseTalismanEncounters.Owns(p,body,attempt,kind));t.Commit();return true;
            }}
        }
        internal static bool HouseReport(GameState p,Func<bool> context){
            Ready(p,context);var scene=DefenseTalismanScenes.Find(p);
            if(scene==null || !scene.Entered || !DefenseTalismanScenes.Registered(scene,p) || p.Entity.MapID!=scene.Map.ID || Clock()>=scene.Until)return false;
            ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=1)return false;
                using(var q=Q.Command(c,t,"SELECT COUNT(*) FROM revora_tower_house_kills WHERE uid=@uid AND visit=@visit","uid",(long)p.Entity.UID,"visit",scene.Visit))if(Convert.ToInt32(q.ExecuteScalar())!=5)return false;
                SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;
                SilverBalance.Persist(c,t,p,silver);Q.Execute(c,t,"UPDATE revora_defense_talisman SET stage=2 WHERE uid=@uid AND stage=1","uid",(long)p.Entity.UID);
                Ready(p,()=>context() && DefenseTalismanScenes.Registered(scene,p) && p.Entity.MapID==scene.Map.ID && Clock()<scene.Until);t.Commit();
            }}SilverBalance.ApplyCommitted(p,silver);scene.Completed=true;return true;
        }
        internal static bool FishingReferral(GameState p,Func<bool> context){
            Ready(p,context);if(p.Entity.MapID!=1015)return false;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=2)return false;
                SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;
                SilverBalance.Persist(c,t,p,silver);Q.Execute(c,t,"UPDATE revora_defense_talisman SET stage=3 WHERE uid=@uid AND stage=2","uid",(long)p.Entity.UID);
                Ready(p,()=>context() && p.Entity.MapID==1015);t.Commit();
            }}SilverBalance.ApplyCommitted(p,silver);return true;
        }
    }
}
