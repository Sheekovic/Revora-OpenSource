using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class DisCityPacks
    {
        internal static Func<int,int> Roll=RandomIndex;
        private sealed class Recent {internal readonly Dictionary<uint,DateTime> Packs=new Dictionary<uint,DateTime>();}
        private static readonly ConditionalWeakTable<GameState,Recent> recent=new ConditionalWeakTable<GameState,Recent>();
        private static uint[][] materials;
        private static uint[] superiorLevel4;
        private static int RandomIndex(int count) {
            var bytes=new byte[4];uint n,limit=uint.MaxValue-uint.MaxValue%(uint)count;
            using(var rng=RandomNumberGenerator.Create())do{rng.GetBytes(bytes);n=BitConverter.ToUInt32(bytes,0);}while(n>=limit);
            return (int)(n%(uint)count);
        }
        internal static void Initialize() {
            superiorLevel4=Kernel.DatabaseRefinery.Values.Where(i=>i.Level==4 && !i.Untradable
                && ConquerItemInformation.BaseInformations.ContainsKey(i.Identifier)).Select(i=>i.Identifier).Distinct().OrderBy(i=>i).ToArray();
            materials=new[]{5,6}.Select(level=>Kernel.DatabaseRefinery.Values.Where(i=>i.Level==level && !i.Untradable
                && ConquerItemInformation.BaseInformations.ContainsKey(i.Identifier)).Select(i=>i.Identifier).Distinct().OrderBy(i=>i).ToArray()).ToArray();
            if(superiorLevel4.Length==0 || !ConquerItemInformation.BaseInformations.ContainsKey(720399) || !ConquerItemInformation.BaseInformations.ContainsKey(728919)
                || materials.Any(a=>a.Length==0) || !ConquerItemInformation.BaseInformations.ContainsKey(3008057)
                || !ConquerItemInformation.BaseInformations.ContainsKey(3008058))throw new InvalidOperationException("Dis City refinery reward catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_dis_refinery_packs(
id uuid PRIMARY KEY,uid bigint NOT NULL,pack_uid bigint NOT NULL,pack_type integer NOT NULL,before_stack integer NOT NULL,
result_uid bigint NOT NULL UNIQUE,result_type integer NOT NULL,opened_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);");}
        }
        private static int Draw(int count) {int value=Roll(count);if(value<0 || value>=count)throw new InvalidOperationException("Refinery reward draw is outside its pool.");return value;}
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || item.ID!=720399 && item.ID!=728919 && (item.ID<3008057 || item.ID>3008058))return false;
            if(materials==null || !Q.Player(p) || p.Booth!=null || p.Trade==null || p.Trade.InTrade)return true;
            lock(p.ItemSyncRoot)try {
                ConquerItem owned;
                if(!Q.Player(p) || p.Booth!=null || p.Trade==null || p.Trade.InTrade || !p.Inventory.TryGetItem(item.UID,out owned)
                    || !ReferenceEquals(item,owned) || item.Warehouse!=0 || item.Position!=0 || item.Lock!=0
                    || item.Suspicious || item.InWardrobe || item.Days!=0)return true;
                var now=RefineryProgress.Clock();var history=recent.GetValue(p,_=>new Recent());
                foreach(uint uid in history.Packs.Where(a=>a.Value<=now).Select(a=>a.Key).ToArray())history.Packs.Remove(uid);
                if(history.Packs.ContainsKey(item.UID))return true;
                if(item.StackSize>1 && p.Inventory.Count>=40){p.Send("Make one place in your bag for the refinery material.");return true;}
                var pool=(item.ID==720399 || item.ID==728919)?(Draw(100)<70?superiorLevel4:materials[0]):materials[item.ID-3008057];
                var result=Q.New(pool[Draw(pool.Length)],item.Bound);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Dis City pack owner missing.");
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Refinery pack inventory requires reload.");
                    string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0)!=1)
                        throw new InvalidOperationException("Dis City pack ownership changed.");
                    if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max)","item",(long)result.UID,"type",(int)result.ID,"uid",(long)p.Entity.UID,
                        "bound",result.Bound?1:0,"dur",(int)result.Durability,"max",(int)result.MaximDurability)!=1)
                        throw new InvalidOperationException("Refinery pack result was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_dis_refinery_packs(id,uid,pack_uid,pack_type,before_stack,result_uid,result_type)
VALUES(@id,@uid,@pack,@type,@stack,@result,@result_type)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"pack",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"result",(long)result.UID,"result_type",(int)result.ID)!=1)
                        throw new InvalidOperationException("Refinery pack receipt was not saved.");t.Commit();
                }}
                if(history.Packs.Count>=64)history.Packs.Remove(history.Packs.OrderBy(a=>a.Value).First().Key);
                history.Packs[item.UID]=now.AddSeconds(1);
                Q.Install(p,item.StackSize<=1?new[]{item.UID}:new uint[0],new[]{result});
                if(item.StackSize>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);}
            }catch(Exception ex){Q.Fail(p,"dis-city-refinery-pack",ex);}
            return true;
        }
    }
}
