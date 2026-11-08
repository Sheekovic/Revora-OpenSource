using System;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DesertTreasureStore
    {
        internal static Func<int,int> BidRoll=max=>Kernel.Random.Next(max);
        // Revora odds: 30%, 60%, 100%. Refused offers cost nothing and may be
        // retried after five seconds. The successful price and journal payout
        // commit with the portrait and stage, preventing duplicate charges.
        internal static bool Purchase(GameState p,int price,Func<bool> context){
            Ready(p,context);if(price!=30000 && price!=60000 && price!=99999)return false;
            if(p.Entity.MapID!=1001 || Kernel.GetDistance(p.Entity.X,p.Entity.Y,310,631)>3)return false;
            DateTime now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Desert Treasure clock must be UTC.");
            ConquerItem portrait=null;ulong silver=p.Entity.Money;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=3 || p.Inventory.Count>=40 || silver<(ulong)price)return false;
                SilverBalance.AssertSaved(c,t,p);
                using(var q=Q.Command(c,t,"SELECT MAX(attempted_at) FROM revora_desert_treasure_bids WHERE uid=@uid AND life=@life","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)){
                    object last=q.ExecuteScalar();if(last!=null && last!=DBNull.Value && now<((DateTime)last).ToUniversalTime().AddSeconds(5))return false;
                }
                int roll=BidRoll(100);if(roll<0 || roll>=100)throw new InvalidOperationException("Desert Treasure bid roll outside range.");
                bool won=roll<(price==99999?100:price==60000?60:30);
                if(won){portrait=Q.New(721878,true);Track(c,t,p,portrait);silver=checked(silver-(ulong)price+100);
                    SilverBalance.Persist(c,t,p,silver);Save(c,t,p,3,4);}
                if(Q.Execute(c,t,"INSERT INTO revora_desert_treasure_bids(id,uid,life,price,won,item_uid,attempted_at) VALUES(@id,@uid,@life,@price,@won,NULLIF(@item,0),@now)",
                    "id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"price",price,"won",won,"item",portrait==null?0L:(long)portrait.UID,"now",now)!=1)
                    throw new InvalidOperationException("Desert Treasure bid receipt missing.");
                Ready(p,()=>context() && p.Entity.MapID==1001 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,310,631)<=3);t.Commit();
            }}
            if(portrait==null)return false;
            Q.Install(p,new uint[0],new[]{portrait});SilverBalance.ApplyCommitted(p,silver);return true;
        }
    }
}
