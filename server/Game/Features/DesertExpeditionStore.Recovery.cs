using System;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DesertExpeditionStore
    {
        internal static bool Recover(GameState p,uint type,Func<bool> context){
            Ready(p,context);ConquerItem added;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(p.Inventory.Count>=40)return false;
                bool allowed=run.Stage==2 && (type==721920 || type==710259)
                    || run.Stage==3 && (type==721921 || type==710261)
                    || run.Stage==4 && type==710260;
                if(!allowed)return false;uint old=0;
                using(var q=Q.Command(c,t,@"SELECT item_uid FROM revora_desert_expedition_materials WHERE uid=@uid AND life=@life AND type=@type AND state=0
AND NOT EXISTS(SELECT 1 FROM items WHERE uid=item_uid) ORDER BY item_uid LIMIT 1 FOR UPDATE",
                    "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type)){
                    object value=q.ExecuteScalar();if(value!=null)old=checked((uint)Convert.ToInt64(value));
                }if(old==0)return false;
                added=Q.New(type,true);Track(c,t,p,added);
                if(Q.Execute(c,t,"UPDATE revora_desert_expedition_materials SET state=2 WHERE item_uid=@old AND state=0","old",(long)old)!=1)throw new InvalidOperationException("Desert Expedition lost token changed.");
                if(Q.Execute(c,t,"INSERT INTO revora_desert_expedition_recoveries(old_uid,new_uid,uid,life,type) VALUES(@old,@new,@uid,@life,@type)",
                    "old",(long)old,"new",(long)added.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type)!=1)throw new InvalidOperationException("Desert Expedition recovery receipt missing.");
                Ready(p,context);t.Commit();
            }}Q.Install(p,new uint[0],new[]{added});return true;
        }
    }
}
