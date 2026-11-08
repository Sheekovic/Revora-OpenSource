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
        private static int AmbushKills(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p){using(var q=Q.Command(c,t,"SELECT COUNT(*) FROM revora_fan_ambush_kills WHERE uid=@uid","uid",(long)p.Entity.UID))return Convert.ToInt32(q.ExecuteScalar());}
        internal static int AmbushKind(GameState p){if(!Eligible(p) || p.Entity.MapID!=1011)return -1;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p).Stage==7 && AmbushKills(c,null,p)<5
            && Materials(c,null,p,721801).Any(id=>Material(c,null,p,id,721801)) && Materials(c,null,p,710218).Any(id=>Material(c,null,p,id,710218))?2:-1;}}
        internal static bool AmbushKill(GameState p,Entity body,Guid attempt,Func<bool> context){
            Ready(p,context);if(p.Entity.MapID!=1011 || !AttackTalismanEncounters.Owns(p,body,attempt,2) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);int count=AmbushKills(c,t,p);if(Read(c,t,p).Stage!=7 || count>=5 || !Materials(c,t,p,721801).Any(id=>Material(c,t,p,id,721801)) || !Materials(c,t,p,710218).Any(id=>Material(c,t,p,id,710218)))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_fan_ambush_kills(uid,slot,attempt,body) VALUES(@uid,@slot,@attempt,@body) ON CONFLICT DO NOTHING","uid",(long)p.Entity.UID,"slot",count,"attempt",attempt,"body",(long)body.UID)!=1)return false;
                Ready(p,()=>context() && AttackTalismanEncounters.Owns(p,body,attempt,2));t.Commit();return true;
            }}
        }
        internal static bool ClaimIncense(GameState p,Func<bool> context){
            Ready(p,context);if(!Near(p,1011,788,456))return false;uint poem;ConquerItem[] added;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=7 || AmbushKills(c,t,p)!=5 || p.Inventory.Count>39 || !Materials(c,t,p,710218).Any(id=>Material(c,t,p,id,710218)))return false;
                poem=Materials(c,t,p,721801).FirstOrDefault(id=>Material(c,t,p,id,721801));if(poem==0)return false;
                SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;
                Consume(c,t,p,poem,721801);added=new[]{Q.New(721799,true),Q.New(710219,true)};foreach(var item in added)Track(c,t,p,item);
                SilverBalance.Persist(c,t,p,silver);Save(c,t,p,7,8);Ready(p,()=>context() && Near(p,1011,788,456));t.Commit();
            }}Q.Install(p,new[]{poem},added);SilverBalance.ApplyCommitted(p,silver);return true;
        }
    }
}
