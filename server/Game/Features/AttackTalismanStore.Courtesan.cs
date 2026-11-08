using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class AttackTalismanStore
    {
        internal static readonly ushort[,] CourtesanSites={{733,776},{803,884},{797,513},{703,653}};
        internal static Func<int,int> CourtesanRoll=max=>Kernel.Random.Next(max);
        private static bool CourtesanGems(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p){return LakeGems(c,t,p) && Materials(c,t,p,710216).Any(id=>Material(c,t,p,id,710216));}
        internal static int CourtesanKind(GameState p){
            if(!Eligible(p) || p.Entity.MapID!=1015)return -1;
            lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();var run=Read(c,null,p);if(run.Stage!=9 || run.CourtesanSite<0 || run.CourtesanSite>3 || !CourtesanGems(c,null,p))return -1;
                using(var q=Q.Command(c,null,"SELECT EXISTS(SELECT 1 FROM revora_fan_courtesan_kills WHERE uid=@uid)","uid",(long)p.Entity.UID))return Equals(q.ExecuteScalar(),true)?-1:4;
            }
        }
        internal static bool CourtesanKill(GameState p,Entity body,Guid attempt,Func<bool> context){
            Ready(p,context);if(p.Entity.MapID!=1015 || !AttackTalismanEncounters.Owns(p,body,attempt,4) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity))return false;ConquerItem scepter;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=9 || run.CourtesanSite<0 || run.CourtesanSite>3 || p.Inventory.Count>=40 || !CourtesanGems(c,t,p) || Materials(c,t,p,710217).Count!=0)return false;
                if(Q.Execute(c,t,"INSERT INTO revora_fan_courtesan_kills(uid,attempt,body,site) VALUES(@uid,@attempt,@body,@site) ON CONFLICT DO NOTHING","uid",(long)p.Entity.UID,"attempt",attempt,"body",(long)body.UID,"site",run.CourtesanSite)!=1)return false;
                scepter=Q.New(710217,true);Track(c,t,p,scepter);Ready(p,()=>context() && AttackTalismanEncounters.Owns(p,body,attempt,4));t.Commit();
            }}Q.Install(p,new uint[0],new[]{scepter});return true;
        }
    }
}
