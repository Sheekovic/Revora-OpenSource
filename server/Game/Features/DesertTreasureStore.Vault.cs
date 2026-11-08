using System;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DesertTreasureStore
    {
        internal static Func<int,int> VaultRoll=max=>Kernel.Random.Next(max);
        private static DateTime UtcNow(){var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Desert Treasure clock must be UTC.");return now;}
        private static bool AtVault(GameState p,int distance){return p.Entity.MapID==1001 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,91,283)<=distance;}
        internal static bool RevealVault(GameState p,uint vial,Func<bool> context){
            Ready(p,context);if(!AtVault(p,3))return false;DateTime now=UtcNow();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=2 || !Material(c,t,p,vial,721873))return false;
                if(Q.Execute(c,t,@"INSERT INTO revora_desert_treasure_vault(uid,life,until_at,stepped) VALUES(@uid,@life,@until,false)
ON CONFLICT(uid,life) DO UPDATE SET until_at=@until,stepped=false WHERE revora_desert_treasure_vault.until_at<@now",
                    "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"until",now.AddSeconds(60),"now",now)!=1)return false;
                Ready(p,()=>context() && AtVault(p,3));t.Commit();return true;
            }}
        }
        // Caller must be the accepted walk/jump hook; login/teleport cannot supply credit.
        internal static bool StepVault(GameState p,Func<bool> context){
            Ready(p,context);if(!AtVault(p,0))return false;DateTime now=UtcNow();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=2)return false;
                if(Q.Execute(c,t,"UPDATE revora_desert_treasure_vault SET stepped=true WHERE uid=@uid AND life=@life AND NOT stepped AND until_at>=@now AND until_at<=@latest","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"now",now,"latest",now.AddSeconds(60))!=1)return false;
                Ready(p,()=>context() && AtVault(p,0));t.Commit();return true;
            }}
        }
        internal static bool OpenVault(GameState p,Func<bool> context){
            Ready(p,context);if(!AtVault(p,0))return false;DateTime now=UtcNow();uint key;ConquerItem box=null;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=2 || Materials(c,t,p,721875).Count>0 || Materials(c,t,p,721870).Count>0)return false;
                key=Materials(c,t,p,721876).FirstOrDefault(id=>Material(c,t,p,id,721876));if(key==0)return false;
                if(Q.Execute(c,t,"DELETE FROM revora_desert_treasure_vault WHERE uid=@uid AND life=@life AND stepped AND until_at>=@now AND until_at<=@latest","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"now",now,"latest",now.AddSeconds(60))!=1)return false;
                int roll=VaultRoll(100);if(roll<0 || roll>=100)throw new InvalidOperationException("Desert Treasure vault roll outside range.");
                Consume(c,t,p,key,721876);
                if(roll<50){box=Q.New(721875,true);Track(c,t,p,box);} // Revora odds; failed attempt consumes its key.
                Ready(p,()=>context() && AtVault(p,0));t.Commit();
            }}Q.Install(p,new[]{key},box==null?new ConquerItem[0]:new[]{box});return box!=null;
        }
        internal static bool OpenScripture(GameState p,uint box,Func<bool> context){
            Ready(p,context);ConquerItem scripture;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=2 || !Material(c,t,p,box,721875) || Materials(c,t,p,721870).Count>0)return false;
                Consume(c,t,p,box,721875);scripture=Q.New(721870,true);Track(c,t,p,scripture);Ready(p,context);t.Commit();
            }}Q.Install(p,new[]{box},new[]{scripture});return true;
        }
    }
}
