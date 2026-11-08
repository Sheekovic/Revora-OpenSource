using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Ordinary use of the JustCreated 727026 starter chain through terminal 727050.
    // Not merged with WelcomePacks.
    internal static class StarterPacks
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action<string> Spoken;
        private static bool enabled;
        private sealed class Gift { internal uint ID; internal byte Plus; internal bool Bound; }
        private static bool Handles(uint id){return id>=727026 && id<=727050;}
        private const string StarterTypes="727026,727027,727028,727029,727030,727031,727032,727033,727034,727035,727036,727037,727038,727039,727040,727041,727042,727043,727044,727045,727046,727047,727048,727049,727050";
        private static void Tell(GameState p,string msg){p.Send(msg);var s=Spoken;if(s!=null)s(msg);}
        private static void Quirk(GameState p,uint id){if(id==727050)Tell(p,"You should reach Level 120 before you can open this pack");}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_starter_pack_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,
item_type integer NOT NULL CHECK(item_type IN("+StarterTypes+@")),
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,level integer NOT NULL,
cps bigint NOT NULL CHECK(cps>=0),silver bigint NOT NULL CHECK(silver>=0),rewards bigint[] NOT NULL,used_at timestamptz NOT NULL);");
                Q.Execute(c,t,"ALTER TABLE revora_starter_pack_uses DROP CONSTRAINT IF EXISTS revora_starter_pack_uses_item_type_check");
                Q.Execute(c,t,"ALTER TABLE revora_starter_pack_uses ADD CONSTRAINT revora_starter_pack_uses_item_type_check CHECK(item_type IN("+StarterTypes+"))");
                t.Commit();}
            }enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static byte MinLevel(uint id){return id==727044?(byte)95:id==727026?(byte)1:(byte)((id-727026)*5);}
        private static byte ToldLevel(uint id){return id==727044?(byte)90:MinLevel(id);}
        private static uint Cps(uint id){return id==727030?500u:0u;}
        private static ulong Silver(uint id){return id==727026?15000UL:id==727027?1500UL:id==727028?5000UL:0UL;}
        private static void Add(List<Gift> list,uint id,int n){for(int i=0;i<n;i++)list.Add(new Gift{ID=id,Plus=0,Bound=false});}
        private static List<Gift> Outputs(uint id,byte cls){
            var list=new List<Gift>();
            if(id==727026){Add(list,722136,10);Add(list,1001000,5);Add(list,727027,1);}
            else if(id==727027){Add(list,1001000,5);Add(list,727028,1);}
            else if(id==727028){Add(list,723790,3);Add(list,727029,1);Add(list,721623,1);Add(list,721624,1);}
            else if(id==727029){Add(list,723790,5);Add(list,1060020,3);Add(list,727030,1);}
            else if(id==727030)Add(list,727031,1);
            else if(id==727031){Add(list,203009,1);Add(list,727032,1);}
            else if(id==727032){Add(list,1200000,1);Add(list,727033,1);}
            else if(id==727033){Add(list,723017,1);Add(list,727034,1);}
            else if(id==727034){Add(list,723017,1);Add(list,727035,1);}
            else if(id==727035){Add(list,1200001,1);Add(list,727036,1);}
            else if(id==727036){Add(list,1200002,1);Add(list,727037,1);}
            else if(id==727037){Add(list,723017,1);Add(list,723584,1);Add(list,727038,1);}
            else if(id==727038){Add(list,723700,1);Add(list,752001,1);Add(list,727039,1);}
            else if(id==727039){Add(list,2100025,1);Add(list,727040,1);}
            else if(id==727040){Add(list,723700,2);Add(list,727041,1);}
            else if(id==727041){Add(list,1200000,1);Add(list,727042,1);Add(list,721672,1);Add(list,721673,1);}
            else if(id==727042){Add(list,723700,2);Add(list,727043,1);}
            else if(id==727043){Add(list,723017,2);Add(list,727044,1);}
            else if(id==727044){Add(list,723268,1);Add(list,723017,2);Add(list,727045,1);}
            else if(id==727045){Add(list,723268,1);Add(list,723017,2);Add(list,727046,1);}
            else if(id==727046){Add(list,751099,1);Add(list,727047,1);}
            else if(id==727047){Add(list,1200000,1);Add(list,723017,2);Add(list,727048,1);}
            else if(id==727048){Add(list,751099,2);Add(list,727049,1);}
            else if(id==727049){Add(list,cls<=75?700012u:700002u,2);Add(list,727050,1);}
            else {Add(list,193300,1);Add(list,751099,3);Add(list,721627,1);Add(list,721628,1);}
            return list;
        }
        private static ConquerItem Item(Gift g){
            ushort dur=1;
            if(ConquerItemInformation.BaseInformations.ContainsKey(g.ID))dur=ConquerItemInformation.BaseInformations[g.ID].Durability;
            return new ConquerItem(true){ID=g.ID,UID=ConquerItem.ItemUID.Next,Plus=g.Plus,Bound=g.Bound,StackSize=1,Durability=dur,MaximDurability=dur};
        }
        internal static bool TryHandle(GameState p,byte[] packet){
            ConquerItem item;
            if(p==null || p.Inventory==null || packet==null || packet.Length<12 || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet,8),out item) || !Handles(item.ID))return false;
            if(packet.Length<32 || packet.Length>128 || BitConverter.ToUInt16(packet,0)+8!=packet.Length || BitConverter.ToUInt16(packet,2)!=1009
                || BitConverter.ToUInt16(packet,20)!=4 || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal)return true;
            TryUse(item,p);return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(item==null || !Handles(item.ID))return false;if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try{
                ConquerItem owned;DateTime now=Clock();
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || item.Position!=0 || item.Warehouse!=0
                    || item.Lock!=0 || item.Suspicious || item.InWardrobe || (item.Days==0?item.TimeLeftInMinutes!=0:now.ToLocalTime()>=item.DayStamp.AddDays(item.Days)))return true;
                if(p.Entity.Level<MinLevel(item.ID)){Tell(p,"You should reach Level "+ToldLevel(item.ID).ToString(CultureInfo.InvariantCulture)+" before you can open this pack");Quirk(p,item.ID);return true;}
                var gifts=Outputs(item.ID,p.Entity.Class);int spaces=gifts.Count-(item.StackSize<=1?1:0);
                if(p.Inventory.Count+spaces>40){Tell(p,"Make room for this starter pack.");Quirk(p,item.ID);return true;}
                uint cps=Cps(item.ID);ulong silver=Silver(item.ID);
                long nextCp=(long)p.Entity.ConquerPoints+cps;
                if(cps!=0 && nextCp>uint.MaxValue){Tell(p,"Spend some CPs before opening this pack.");return true;}
                ulong nextSilver=p.Entity.Money+silver;
                if(silver!=0 && (nextSilver<silver || nextSilver>Entity.MaximumSilver)){Tell(p,"Store some silver before opening this pack.");return true;}
                var rewards=gifts.Select(Item).ToArray();
                if(item.Bound) foreach(var reward in rewards) reward.Bound=true;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid, class FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader()){
                        if(!r.Read())throw new InvalidOperationException("Starter pack owner missing.");
                        if(item.ID==727049 && Convert.ToInt32(r[1])!=(int)p.Entity.Class)throw new InvalidOperationException("Starter pack class changed.");
                    }
                    if(silver!=0)SilverBalance.AssertSaved(c,t,p);
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Starter pack inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Starter pack inventory changed.");
                    string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Starter pack ownership changed.");
                    foreach(var reward in rewards)
                        if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@plus,@dur,@max)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                            "bound",reward.Bound?1:0,"plus",(int)reward.Plus,"dur",(int)reward.Durability,"max",(int)reward.MaximDurability)!=1)
                            throw new InvalidOperationException("Starter pack item was not saved.");
                    if(cps!=0 && Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp WHERE uid=@uid AND COALESCE(conquerpoints,0)=@old",
                        "cp",nextCp,"uid",(long)p.Entity.UID,"old",(long)p.Entity.ConquerPoints)!=1)
                        throw new InvalidOperationException("Starter pack CP balance changed.");
                    if(silver!=0)SilverBalance.Persist(c,t,p,nextSilver);
                    if(Q.Execute(c,t,@"INSERT INTO revora_starter_pack_uses(id,uid,item_uid,item_type,before_stack,bound,level,cps,silver,rewards,used_at)
VALUES(@id,@uid,@item,@type,@stack,@bound,@level,@cps,@silver,@rewards,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"level",(int)p.Entity.Level,
                        "cps",(long)cps,"silver",(long)silver,"rewards",rewards.Select(r=>(long)r.UID).ToArray(),"now",now)!=1)
                        throw new InvalidOperationException("Starter pack receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Starter pack session changed.");t.Commit();
                }}
                Q.Install(p,item.StackSize<=1?new[]{item.UID}:new uint[0],rewards);
                if(item.StackSize>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                if(cps!=0)p.Entity.ApplyCommittedConquerPoints((uint)nextCp);
                if(silver!=0)SilverBalance.ApplyCommitted(p,nextSilver);
                Quirk(p,item.ID);
            }catch(Exception ex){Q.Fail(p,"starter-pack",ex);}return true;
        }
    }
}
