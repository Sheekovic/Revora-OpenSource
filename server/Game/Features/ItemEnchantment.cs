using System;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class ItemEnchantment
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int,int> Draw=(minimum,exclusiveMaximum)=>Kernel.Random.Next(minimum,exclusiveMaximum);
        internal static bool Bounds(uint type,out int minimum,out int maximum) {
            minimum=maximum=0;
            if(type<700001 || type>700123 || !Enum.IsDefined(typeof(Enums.Gem),(byte)(type%1000)))return false;
            int grade=(int)(type%10),family=(int)(type%1000)/10;
            if(grade==1){minimum=1;maximum=59;return true;} // Retains the existing normal-gem policy.
            if(grade!=2 && grade!=3 || family>7)return false;
            if(grade==2) {
                minimum=family==1?100:family==3?80:family==0 || family==5 || family==6?60:40;
                maximum=minimum+(family==1?59:49);
            } else {
                minimum=family==1?200:family==0 || family==3 || family==7?170:family==5 || family==6?140:family==2?90:70;
                maximum=family==1?255:minimum+(family==4?49:59);
            }
            return true;
        }
        internal static void Enchant(GameState p,ItemUsage request) {
            if(!Ready(p) || request==null)return;
            lock(p.ItemSyncRoot)try {
                ConquerItem item,gem;int minimum,maximum;
                if(!p.Inventory.TryGetItem(request.UID,out item) || !p.Inventory.TryGetItem(request.dwParam,out gem)
                    || !Bounds(gem.ID,out minimum,out maximum))return;
                int roll=Draw(minimum,maximum+1);
                if(roll<minimum || roll>maximum)throw new InvalidOperationException("Enchantment roll outside range.");
                Apply(p,item,gem,(byte)roll);
            }catch(Exception ex){Q.Fail(p,"enchantment-roll",ex);}
        }
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_item_enchantments(
id uuid PRIMARY KEY,uid bigint NOT NULL,target_uid bigint NOT NULL,gem_uid bigint NOT NULL,gem_type integer NOT NULL,
before_stack integer NOT NULL,after_stack integer NOT NULL CHECK(after_stack>=0),
before_hp integer NOT NULL CHECK(before_hp BETWEEN 0 AND 255),roll_hp integer NOT NULL CHECK(roll_hp BETWEEN 0 AND 255),
after_hp integer NOT NULL CHECK(after_hp BETWEEN before_hp AND 255),created_at timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS revora_enchantment_recent ON revora_item_enchantments(uid,target_uid,gem_uid,created_at DESC);");}
        }
        private static bool Ready(GameState p){return Q.Player(p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static bool Usable(ConquerItem i){return i!=null && i.Position==0 && i.Warehouse==0 && i.Lock==0
            && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0;}
        private static bool Saved(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem i) {
            using(var q=Q.Command(c,t,@"SELECT uid FROM items WHERE uid=@item AND entityid=@owner AND id=@type
AND position=0 AND warehouse=0 AND stacksize=@stack AND bound=@bound AND COALESCE(enchant,0)=@hp
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0' FOR UPDATE",
                "item",(long)i.UID,"owner",(long)p.Entity.UID,"type",(int)i.ID,"stack",(int)i.StackSize,"bound",i.Bound?1:0,"hp",(int)i.Enchant))
                return q.ExecuteScalar()!=null;
        }
        // Validate the supplied outcome again before any paid mutation.
        internal static void Apply(GameState p,ConquerItem item,ConquerItem gem,byte roll) {
            if(!Ready(p))return;
            lock(p.ItemSyncRoot)try {
                int minimum,maximum;
                if(gem==null || !Bounds(gem.ID,out minimum,out maximum) || roll<minimum || roll>maximum)return;
                ConquerItem owned,material;
                if(!Ready(p) || !Usable(item) || !Usable(gem) || item.UID==gem.UID || item.StackSize>1
                    || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item)
                    || !p.Inventory.TryGetItem(gem.UID,out material) || !ReferenceEquals(material,gem)
                    || !ConquerItemInformation.BaseInformations.ContainsKey(item.ID) || !ConquerItemInformation.BaseInformations.ContainsKey(gem.ID)
                    || gem.ID<700001 || gem.ID>700123 || gem.ID%10<1 || gem.ID%10>3
                    || !Enum.IsDefined(typeof(Enums.Gem),(Enums.Gem)(gem.ID%1000)) || item.Enchant==255) return;
                int slot=(int)ItemHandler.GetPositionFromID(item.ID);
                if(!(slot>=1 && slot<=6 || slot==8) || gem.Bound && !item.Bound)return;
                byte before=item.Enchant,after=Math.Max(before,roll);ushort stack=gem.StackSize;
                int remaining=stack>1?stack-1:0;DateTime now=Clock();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_item_enchantments WHERE uid=@uid
AND target_uid=@target AND gem_uid=@gem AND created_at>@now-interval '1 second')",
                        "uid",(long)p.Entity.UID,"target",(long)item.UID,"gem",(long)gem.UID,"now",now))
                        if(Equals(q.ExecuteScalar(),true))return;
                    if(!Saved(c,t,p,item) || !Saved(c,t,p,gem))throw new InvalidOperationException("Enchanting item preimage changed.");
                    string consume=remaining==0?"DELETE FROM items WHERE uid=@item AND entityid=@uid AND stacksize=@stack"
                        :"UPDATE items SET stacksize=stacksize-1 WHERE uid=@item AND entityid=@uid AND stacksize=@stack";
                    if(Q.Execute(c,t,consume,"item",(long)gem.UID,"uid",(long)p.Entity.UID,"stack",(int)stack)!=1)
                        throw new InvalidOperationException("Enchanting material was not consumed.");
                    if(Q.Execute(c,t,"UPDATE items SET enchant=@after WHERE uid=@item AND entityid=@uid AND COALESCE(enchant,0)=@before",
                        "after",(int)after,"item",(long)item.UID,"uid",(long)p.Entity.UID,"before",(int)before)!=1)
                        throw new InvalidOperationException("Enchanting result was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_item_enchantments(id,uid,target_uid,gem_uid,gem_type,before_stack,after_stack,before_hp,roll_hp,after_hp,created_at)
VALUES(@id,@uid,@target,@gem,@type,@stack,@remaining,@before,@roll,@after,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,
                        "target",(long)item.UID,"gem",(long)gem.UID,"type",(int)gem.ID,"stack",(int)stack,"remaining",remaining,
                        "before",(int)before,"roll",(int)roll,"after",(int)after,"now",now)!=1)
                        throw new InvalidOperationException("Enchanting receipt was not saved.");
                    if(!Ready(p) || !Usable(item) || !Usable(gem) || item.Enchant!=before || gem.StackSize!=stack)
                        throw new InvalidOperationException("Enchanting context changed.");
                    t.Commit();
                }}
                if(remaining==0)Q.Install(p,new[]{gem.UID},new ConquerItem[0]);
                else {gem.StackSize=(ushort)remaining;gem.Mode=Enums.ItemMode.Update;gem.Send(p);}
                item.Enchant=after;item.Mode=Enums.ItemMode.Update;item.Send(p);p.Send(Constants.Enchant(before,roll));
            }catch(Exception ex){Q.Fail(p,"item-enchantment",ex);}
        }
    }
}
