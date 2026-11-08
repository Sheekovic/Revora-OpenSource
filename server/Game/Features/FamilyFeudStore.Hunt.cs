using System;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class FamilyFeudStore
    {
        internal static Func<int,int> Roll=max=>Kernel.Random.Next(max);
        private static readonly ConditionalWeakTable<Entity,object> deaths=new ConditionalWeakTable<Entity,object>();
        internal static void Revived(Entity body){if(body!=null && !body.Dead)lock(body)deaths.Remove(body);}
        private static bool Death(GameState p,Entity body){return body!=null && body.MapID==1015 && DailySoulBall.WildernessDeath(body,p.Entity)
            && ReferenceEquals(body.Killer,p.Entity) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)<=Constants.pScreenDistance;}
        private static int Kills(NpgsqlConnection c,NpgsqlTransaction t,GameState p,int stage){
            using(var q=Q.Command(c,t,"SELECT kills FROM revora_family_feud_kills WHERE uid=@uid AND life=@life AND stage=@stage FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"stage",stage)){
                object value=q.ExecuteScalar();return value==null?0:Convert.ToInt32(value);
            }
        }
        internal static int KillCount(GameState p){if(!Q.Player(p,false))return 0;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();var run=Read(c,null,p);return Kills(c,null,p,run.Stage);}}
        internal static bool Hunt(GameState p,Entity body,Func<bool> context){
            Ready(p,context);if(body==null)return false;lock(body){object used;
                if(!Death(p,body) || deaths.TryGetValue(body,out used))return false;
                deaths.Add(body,new object());ConquerItem added=null;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);uint family=body.MonsterInfo.ID;
                    if(run.Stage==2 && family==55 || run.Stage==4 && family==84){
                        int cap=run.Stage==2?50:10,before=Kills(c,t,p,run.Stage);if(before>=cap)return false;
                        if(Q.Execute(c,t,@"INSERT INTO revora_family_feud_kills(uid,life,stage,kills) VALUES(@uid,@life,@stage,@next)
ON CONFLICT(uid,life,stage) DO UPDATE SET kills=@next WHERE revora_family_feud_kills.kills=@before",
                            "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"stage",run.Stage,"next",before+1,"before",before)!=1)throw new InvalidOperationException("Family Feud kill counter changed.");
                    }else if(run.Stage==3 && family==79){
                        if(p.Inventory.Count>=40 || Materials(c,t,p,710563).Count>0)return false;
                        int roll=Roll(100);if(roll<0 || roll>=100)throw new InvalidOperationException("Family Feud roll outside range.");
                        // Revora probability: original documentation does not publish odds.
                        if(roll>=50)return false;added=Q.New(710563,true);Track(c,t,p,added);
                    }else return false;
                    Ready(p,()=>context() && Death(p,body));t.Commit();
                }}if(added!=null)Q.Install(p,new uint[0],new[]{added});return true;
            }
        }
        internal static bool Recover(GameState p,Func<bool> context){
            Ready(p,context);ConquerItem added;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage<2 || run.Stage>7 || p.Inventory.Count>=40)return false;
                uint type=710560u+(uint)run.Stage,old=0;
                using(var q=Q.Command(c,t,@"SELECT item_uid FROM revora_family_feud_materials WHERE uid=@uid AND life=@life AND type=@type AND state=0
AND NOT EXISTS(SELECT 1 FROM items WHERE uid=item_uid) ORDER BY item_uid LIMIT 1 FOR UPDATE",
                    "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type)){
                    object value=q.ExecuteScalar();if(value!=null)old=checked((uint)Convert.ToInt64(value));
                }if(old==0)return false;
                added=Q.New(type,true);Track(c,t,p,added);
                if(Q.Execute(c,t,"UPDATE revora_family_feud_materials SET state=2 WHERE item_uid=@old AND state=0","old",(long)old)!=1)throw new InvalidOperationException("Family Feud lost token changed.");
                if(Q.Execute(c,t,"INSERT INTO revora_family_feud_recoveries(old_uid,new_uid,uid,life,type) VALUES(@old,@new,@uid,@life,@type)",
                    "old",(long)old,"new",(long)added.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type)!=1)throw new InvalidOperationException("Family Feud recovery receipt missing.");
                Ready(p,context);t.Commit();
            }}Q.Install(p,new uint[0],new[]{added});return true;
        }
    }
}
