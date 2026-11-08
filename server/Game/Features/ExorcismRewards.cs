using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class ExorcismRewards
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int> Roll=RandomIndex;
        // Revora weights: uniform slots; the legacy double RadiantSword slot is retained.
        private static readonly uint[] standard={350001,350002,350004,350005,350006,350007,350008,350009,350010,350011,350012,350014,350015,350016,350017,350018,350019,350020,360001,360002,360003,360004,360005,360006,360007,360008,360009,360010,360011,360012,360013,360014,360015,360016,360017,360018,360019,360020,360021,360022,360023,360024,360025,360026,360027,360028,360029,360030,360031,360032,360033,360034,360035,360036,360037,360038,360039,360040,370001,370002,370003,370004,370005,370011,370012,370013,370014,370015,380001,380002,380003,380004,380005,380011,380012,380013,380014,380015,820052,820053,800014,800015,800016,800512,800613,800253};
        private static readonly uint[] golden={822053,822055,800014,800015,800014,800016,800512,800613,800415,800513,800017,820052,350006,350007,350008,820053,820057,823055,824017,821031,821032,820071,820072,800722,800804,800809,823056,800254,800253,800421,800110,800320,350001,350002,350004,350005,820056,350009,350010,350011,350012,350013,370001,370002,370003};
        private static bool enabled;
        private sealed class Offer { internal uint Token,Standard,Golden;internal int Small,Large;internal DateTime End; }
        private sealed class Recent {internal readonly Dictionary<uint,DateTime> Packs=new Dictionary<uint,DateTime>();}
        private sealed class Take {internal ConquerItem Item;internal int Count;internal bool Removed {get{return Count>=Math.Max(1,(int)Item.StackSize);}}}
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        private static readonly ConditionalWeakTable<GameState,Recent> recent=new ConditionalWeakTable<GameState,Recent>();
        internal static void Initialize() {
            foreach(uint id in standard.Concat(golden).Concat(new[]{711211u,720948u,729594u,Config.DailyQuestChiToken})) {
                var item=ConquerItemInformation.BaseInformations[id];
                if(item==null || item.TimeTillRemovInMinutes!=0 && item.TimeTillRemovInMinutes!=10080)
                    throw new InvalidOperationException("Exorcism reward catalog is missing or its lifetime changed.");
            }
            if(standard.Any(id=>ConquerItemInformation.BaseInformations[id].PurificationLevel!=0 && (ConquerItemInformation.BaseInformations[id].PurificationLevel<3 || ConquerItemInformation.BaseInformations[id].PurificationLevel>4))
                || golden.Any(id=>ConquerItemInformation.BaseInformations[id].PurificationLevel!=0 && (ConquerItemInformation.BaseInformations[id].PurificationLevel<3 || ConquerItemInformation.BaseInformations[id].PurificationLevel>6)))
                throw new InvalidOperationException("Exorcism soul grades changed.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_exorcism_exchanges(
id uuid PRIMARY KEY,uid bigint NOT NULL,token_type integer NOT NULL,token_count integer NOT NULL CHECK(token_count>0),
debited_ids bigint[] NOT NULL,debited_counts integer[] NOT NULL,result_uid bigint NOT NULL UNIQUE,result_type integer NOT NULL,exchanged_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_exorcism_openings(
id uuid PRIMARY KEY,uid bigint NOT NULL,pack_uid bigint NOT NULL,pack_type integer NOT NULL,before_stack integer NOT NULL,
result_uids bigint[] NOT NULL,result_types integer[] NOT NULL,bound_cp integer NOT NULL CHECK(bound_cp IN(0,50)),opened_at timestamptz NOT NULL);");}enabled=true;
        }
        private static int RandomIndex(int count) {
            var bytes=new byte[4];uint n,limit=uint.MaxValue-uint.MaxValue%(uint)count;
            using(var rng=RandomNumberGenerator.Create())do{rng.GetBytes(bytes);n=BitConverter.ToUInt32(bytes,0);}while(n>=limit);
            return (int)(n%(uint)count);
        }
        private static int Draw(int count){int n=Roll(count);if(n<0 || n>=count)throw new InvalidOperationException("Exorcism draw is outside its pool.");return n;}
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static bool Near(GameState p){return Ready(p) && Q.Near(p,12333,1036) && p.Map.BaseID==1036;}
        private static bool Usable(ConquerItem i){return i!=null && i.Position==0 && i.Warehouse==0 && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0;}
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                if(q.ExecuteScalar()==null)throw new InvalidOperationException("Exorcism owner missing.");
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Exorcism bag exceeds capacity.");ids.Add(Convert.ToUInt32(r[0]));}
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Exorcism bag requires reload.");
        }
        private static void Debit(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Take take) {
            var i=take.Item;string sql=take.Removed?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-@count";
            if(Q.Execute(c,t,sql+@" WHERE uid=@item AND id=@type AND entityid=@uid AND position=0 AND warehouse=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'", "item",(long)i.UID,"type",(int)i.ID,"uid",(long)p.Entity.UID,
                "stack",(int)i.StackSize,"bound",i.Bound?1:0,"count",take.Count)!=1)throw new InvalidOperationException("Exorcism item ownership changed.");
        }
        private static ConquerItem Reward(uint id,bool bound,DateTime now) {
            var item=Q.New(id,bound);if(ConquerItemInformation.BaseInformations[id].TimeTillRemovInMinutes==10080){item.Days=7;item.DayStamp=now.ToLocalTime();item.TimeLeftInMinutes=604800;}return item;
        }
        private static void Insert(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item) {
            if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability,days,daystamp)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max,@days,@stamp)","item",(long)item.UID,"type",(int)item.ID,"uid",(long)p.Entity.UID,
                "bound",item.Bound?1:0,"dur",(int)item.Durability,"max",(int)item.MaximDurability,"days",item.Days.ToString(System.Globalization.CultureInfo.InvariantCulture),"stamp",item.DayStamp.ToBinary())!=1)
                throw new InvalidOperationException("Exorcism reward was not saved.");
        }
        private static void Install(GameState p,List<Take> takes,ConquerItem[] rewards) {
            // Capture removal decisions before changing stack sizes.
            var remaining=takes.Where(i=>!i.Removed).ToArray();Q.Install(p,takes.Where(i=>i.Removed).Select(i=>i.Item.UID),rewards);
            foreach(var take in remaining){take.Item.StackSize-=(ushort)take.Count;take.Item.Mode=Enums.ItemMode.Update;take.Item.Send(p);take.Item.Mode=Enums.ItemMode.Default;}
        }
        private static string Exchange(GameState p,bool gold,DateTime now) {
            Offer offer;if(!offers.TryGetValue(p,out offer))return "Let us review the packs first.";offers.Remove(p);
            if(now>=offer.End || offer.Token!=Config.DailyExorcismToken || offer.Standard!=Config.DailyExorcismPack || offer.Golden!=Config.DailyExorcismGoldPack
                || offer.Small!=Config.DailyExorcismPackTokens || offer.Large!=Config.DailyExorcismGoldPackTokens)return "Let us review the packs again.";
            int count=gold?offer.Large:offer.Small,remaining=count;var takes=new List<Take>();
            foreach(var item in p.Inventory.Objects.Where(i=>i.ID==offer.Token && Usable(i)).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID)) {
                int amount=Math.Min(remaining,Math.Max(1,(int)item.StackSize));takes.Add(new Take{Item=item,Count=amount});remaining-=amount;if(remaining==0)break;
            }
            if(remaining!=0)return "Bring me "+count+" Exorcism Tokens.";
            if(p.Inventory.Count-takes.Count(i=>i.Removed)>=40)return "Make one place in your bag for the pack.";
            var reward=Reward(gold?offer.Golden:offer.Standard,takes.Any(i=>i.Item.Bound),now);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);foreach(var take in takes.OrderBy(i=>i.Item.UID))Debit(c,t,p,take);Insert(c,t,p,reward);
                if(Q.Execute(c,t,@"INSERT INTO revora_exorcism_exchanges(id,uid,token_type,token_count,debited_ids,debited_counts,result_uid,result_type,exchanged_at)
VALUES(@id,@uid,@type,@count,@ids,@counts,@result,@result_type,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"type",(int)offer.Token,"count",count,
                    "ids",takes.Select(i=>(long)i.Item.UID).ToArray(),"counts",takes.Select(i=>i.Count).ToArray(),"result",(long)reward.UID,"result_type",(int)reward.ID,"now",now)!=1)
                    throw new InvalidOperationException("Exorcism exchange receipt was not saved.");
                if(!Near(p))throw new InvalidOperationException("Exorcism exchange session changed.");t.Commit();
            }}Install(p,takes,new[]{reward});return "The pack is yours. May it serve you well.";
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || request==null || p.ActiveNpc!=12333)return false;
            byte choice=request.OptionID;if(choice!=6 && choice!=8 && choice!=9){offers.Remove(p);return false;}
            if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try {
                if(!Near(p) || request.NpcID!=0 && request.NpcID!=12333)return true;
                var d=new Npcs(p);d.Avatar(67);DateTime now=Clock();
                if(choice==6) {
                    offers.Remove(p);
                    if(Config.DailyExorcismToken!=711211 || Config.DailyExorcismPack!=720948 || Config.DailyExorcismGoldPack!=729594 || Config.DailyExorcismPackTokens==0 || Config.DailyExorcismGoldPackTokens==0)
                        throw new InvalidOperationException("Exorcism exchange configuration is invalid.");
                    offers.Add(p,new Offer{Token=Config.DailyExorcismToken,Standard=Config.DailyExorcismPack,Golden=Config.DailyExorcismGoldPack,
                        Small=Config.DailyExorcismPackTokens,Large=Config.DailyExorcismGoldPackTokens,End=now.AddMinutes(2)});
                    d.Text("Bring me "+Config.DailyExorcismPackTokens+" tokens for an Exorcism Pack, or "+Config.DailyExorcismGoldPackTokens+" for a Golden Pack. The golden seal also holds 50 bound CPs.");
                    d.Option("Exorcism Pack.",8);d.Option("Golden Exorcism Pack.",9);
                }else {d.Text(Exchange(p,choice==9,now));d.Option("Show me the packs.",6);}
                d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){offers.Remove(p);Q.Fail(p,"exorcism-exchange",ex);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || item.ID!=720948 && item.ID!=729594)return false;
            if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try {
                ConquerItem owned;if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || !Usable(item))return true;
                DateTime now=Clock();var history=recent.GetValue(p,_=>new Recent());
                foreach(uint uid in history.Packs.Where(a=>a.Value<=now).Select(a=>a.Key).ToArray())history.Packs.Remove(uid);
                if(history.Packs.ContainsKey(item.UID))return true;
                bool gold=item.ID==729594;uint cp=gold?50u:0,before=p.Entity.BoundCps;
                // Reserve the complete possible bundle before drawing, so a full bag cannot reroll away the Chi prize.
                if(p.Inventory.Count-(item.StackSize<=1?1:0)+(gold?2:1)>40){p.Send("Make room for the pack's gifts.");return true;}
                if((ulong)before+cp>uint.MaxValue){p.Send("Spend some bound CPs before opening this pack.");return true;}
                uint balance=before+cp;var takes=new List<Take>{new Take{Item=item,Count=1}};ConquerItem[] rewards;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);Debit(c,t,p,takes[0]);var pool=gold?golden:standard;
                    var gifts=new List<ConquerItem>{Reward(pool[Draw(pool.Length)],item.Bound,now)};
                    if(gold && Draw(100)<20)gifts.Add(Reward(Config.DailyQuestChiToken,item.Bound,now));rewards=gifts.ToArray();
                    foreach(var gift in rewards)Insert(c,t,p,gift);
                    if(gold && Q.Execute(c,t,"UPDATE entities SET boundcps=@next WHERE uid=@uid AND COALESCE(boundcps,0)=@before",
                        "next",(long)balance,"uid",(long)p.Entity.UID,"before",(long)before)!=1)throw new InvalidOperationException("Exorcism bound CP balance changed.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_exorcism_openings(id,uid,pack_uid,pack_type,before_stack,result_uids,result_types,bound_cp,opened_at)
VALUES(@id,@uid,@pack,@type,@stack,@results,@types,@cp,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"pack",(long)item.UID,"type",(int)item.ID,
                        "stack",(int)item.StackSize,"results",rewards.Select(i=>(long)i.UID).ToArray(),"types",rewards.Select(i=>(int)i.ID).ToArray(),"cp",(int)cp,"now",now)!=1)
                        throw new InvalidOperationException("Exorcism opening receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Exorcism pack session changed.");t.Commit();
                }}
                if(history.Packs.Count>=64)history.Packs.Remove(history.Packs.OrderBy(a=>a.Value).First().Key);history.Packs[item.UID]=now.AddSeconds(1);
                Install(p,takes,rewards);if(gold)p.Entity.ApplyCommittedBoundCps(balance);
            }catch(Exception ex){Q.Fail(p,"exorcism-pack",ex);}return true;
        }
    }
}
