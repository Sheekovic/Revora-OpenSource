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
        private static bool InLake(GameState p){var s=Scenes.Find(p);return s!=null && s.Entered && !s.Completed && s.Map.BaseID==Scenes.LakeBaseID && Scenes.Registered(s,p) && Clock()<s.Until && p.Entity.MapID==s.Map.ID;}
        private static bool LakeGems(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p){return Materials(c,t,p,710218).Any(id=>Material(c,t,p,id,710218)) && Materials(c,t,p,710219).Any(id=>Material(c,t,p,id,710219));}
        private static void LakeStep(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p,int before,int after){
            if(Q.Execute(c,t,"UPDATE revora_attack_talisman SET lake_step=@after WHERE uid=@uid AND stage=8 AND lake_step=@before","uid",(long)p.Entity.UID,"before",before,"after",after)!=1)throw new InvalidOperationException("Fan lake checkpoint changed.");
        }
        private static bool BeginLakeVisit(GameState p,Scenes.Scene scene,Func<bool> context){
            if(!Scenes.Registered(scene,p) || Clock()>=scene.Until || !Near(p,1011,471,844))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=8 || run.LakeStep>=3 || !LakeGems(c,t,p) || run.LakeStep==0 && !Materials(c,t,p,721799).Any(id=>Material(c,t,p,id,721799)))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_fan_visits(uid,visit,map,active,until_at,base_map) VALUES(@uid,@visit,@map,true,@until,1011) ON CONFLICT DO NOTHING","uid",(long)p.Entity.UID,"visit",scene.Visit,"map",(int)scene.Map.ID,"until",scene.Until)!=1)return false;
                Ready(p,()=>context() && Scenes.Registered(scene,p) && Clock()<scene.Until && Near(p,1011,471,844));t.Commit();return true;
            }}
        }
        internal static bool BurnIncense(GameState p,uint used,Func<bool> context){
            Ready(p,context);if(!InLake(p) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,475,844)>3)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=8 || run.LakeStep!=0 || !LakeGems(c,t,p) || !Material(c,t,p,used,721799))return false;
                Consume(c,t,p,used,721799);LakeStep(c,t,p,0,1);Ready(p,()=>context() && InLake(p) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,475,844)<=3);t.Commit();
            }}Q.Install(p,new[]{used},new ConquerItem[0]);Scenes.RevealHerbalist(Scenes.Find(p));return true;
        }
        private static bool AtHerbalist(GameState p){return InLake(p) && p.Map.Npcs.ContainsKey(Scenes.Herbalist) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,475,840)<=3;}
        internal static bool SpeakHerbalist(GameState p,Func<bool> context){
            Ready(p,context);if(!AtHerbalist(p))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=8 || run.LakeStep!=1 || !LakeGems(c,t,p))return false;
                LakeStep(c,t,p,1,2);Ready(p,()=>context() && AtHerbalist(p));t.Commit();return true;
            }}
        }
        internal static int LakeKind(GameState p){if(!Eligible(p) || !InLake(p))return -1;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();var run=Read(c,null,p);return run.Stage==8 && run.LakeStep==2 && LakeGems(c,null,p)?3:-1;}}
        internal static bool LakeKill(GameState p,Entity body,Guid attempt,Func<bool> context){
            Ready(p,context);if(!InLake(p) || !AttackTalismanEncounters.Owns(p,body,attempt,3) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity))return false;ConquerItem eye;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=8 || run.LakeStep!=2 || p.Inventory.Count>=40 || !LakeGems(c,t,p) || Materials(c,t,p,710216).Count!=0)return false;
                if(Q.Execute(c,t,"INSERT INTO revora_fan_lake_kills(uid,attempt,body) VALUES(@uid,@attempt,@body) ON CONFLICT DO NOTHING","uid",(long)p.Entity.UID,"attempt",attempt,"body",(long)body.UID)!=1)return false;
                eye=Q.New(710216,true);Track(c,t,p,eye);LakeStep(c,t,p,2,3);Ready(p,()=>context() && InLake(p) && AttackTalismanEncounters.Owns(p,body,attempt,3));t.Commit();
            }}Q.Install(p,new uint[0],new[]{eye});Scenes.Find(p).Completed=true;return true;
        }
        internal static bool ReportLake(GameState p,Func<bool> context){
            Ready(p,context);if(!Near(p,1011,788,456))return false;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=8 || run.LakeStep!=3 || !LakeGems(c,t,p) || !Materials(c,t,p,710216).Any(id=>Material(c,t,p,id,710216)))return false;
                SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;
                int site=CourtesanRoll(4);if(site<0 || site>3)throw new InvalidOperationException("Fan Courtesan site roll invalid.");
                if(Q.Execute(c,t,"UPDATE revora_attack_talisman SET courtesan_site=@site WHERE uid=@uid AND stage=8","site",site,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Fan Courtesan referral changed.");
                SilverBalance.Persist(c,t,p,silver);Save(c,t,p,8,9);Ready(p,()=>context() && Near(p,1011,788,456));t.Commit();
            }}SilverBalance.ApplyCommitted(p,silver);return true;
        }
    }
}
