using System;
using System.Linq;
using System.Security.Cryptography;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class GemComposition
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static readonly uint[] families={700000,700010,700020,700030,700040,700050,700060,700070,700100,700120};
        internal static void Initialize()
        {
            foreach(uint family in families)for(uint quality=1;quality<=3;quality++)
                if(!ConquerItemInformation.BaseInformations.ContainsKey(family+quality))throw new InvalidOperationException("Gem catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_gem_compositions(
id uuid PRIMARY KEY,uid bigint NOT NULL,request_hash varchar(64) NOT NULL,input_type integer NOT NULL,
output_type integer NOT NULL CHECK(output_type=input_type+1),bound boolean NOT NULL,cost bigint NOT NULL CHECK(cost IN (10000,800000,1000000)),
silver_before bigint NOT NULL CHECK(silver_before>=cost),silver_after bigint NOT NULL CHECK(silver_after=silver_before-cost),
item_uid bigint NOT NULL UNIQUE,created_at timestamptz NOT NULL DEFAULT now(),UNIQUE(uid,request_hash));
CREATE INDEX IF NOT EXISTS revora_gem_recent ON revora_gem_compositions(uid,input_type,bound,created_at DESC);
CREATE TABLE IF NOT EXISTS revora_gem_materials(
composition uuid NOT NULL REFERENCES revora_gem_compositions(id),item_uid bigint NOT NULL,
PRIMARY KEY(composition,item_uid));");}
        }
        private static bool Ready(GameState p){return Q.Player(p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static bool Material(ConquerItem i,uint type,bool bound)
        {return i.ID==type && i.Bound==bound && i.Position==0 && i.Warehouse==0 && i.StackSize<=1
            && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0;}
        internal static void Handle(GameState p,byte[] frame)
        {
            if(frame==null || frame.Length<30 || !Ready(p))return;
            ushort action=BitConverter.ToUInt16(frame,20);
            if(action!=39 && action!=51)return;
            if(frame.Length!=(action==39?103:131) || BitConverter.ToUInt16(frame,0)!=frame.Length-8
                || BitConverter.ToUInt16(frame,2)!=1009 || BitConverter.ToUInt64(frame,frame.Length-8)!=PacketHandler.ClientSeal)return;
            // The mixed recipe's outcome odds and UI response are not established yet.
            // Fail closed globally; never fall through to the unsafe legacy grant.
            if(action==51){p.Send("The Tortoise gem service is not available yet.");return;}
            uint type=BitConverter.ToUInt32(frame,8),binding=BitConverter.ToUInt32(frame,12);
            if(binding>1 || type==700071 || BitConverter.ToUInt32(frame,26)!=0 || !families.Any(f=>type==f+1 || type==f+2))return;
            ulong cost=type%10==1?10000UL:type==700072?1000000UL:800000UL;
            string fingerprint;using(var hash=SHA256.Create())fingerprint=System.BitConverter.ToString(hash.ComputeHash(frame,0,frame.Length-8)).Replace("-","");
            bool bound=binding==1;
            lock(p.ItemSyncRoot)try {
                if(!Ready(p))return;
                ulong before=p.Entity.Money;
                if(before<cost){p.Send("You do not have enough silver to compose this gem.");return;}
                var parts=p.Inventory.Objects.Where(i=>Material(i,type,bound)).OrderBy(i=>i.UID).Take(15).ToArray();
                if(parts.Length!=15){p.Send("You need fifteen matching gems of the same binding.");return;}
                if(p.Inventory.Count>40)return;
                var reward=Q.New(type+1,bound);Guid id=Guid.NewGuid();DateTime now=Clock();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_gem_compositions WHERE uid=@uid AND
(request_hash=@hash OR input_type=@type AND bound=@bound AND created_at>@now-interval '1 second'))",
                        "uid",(long)p.Entity.UID,"hash",fingerprint,"type",(int)type,"bound",bound,"now",now))
                        if(Equals(q.ExecuteScalar(),true))return;
                    var saved=new System.Collections.Generic.List<uint>();
                    using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(saved.Count>=40)throw new InvalidOperationException("Gem bag exceeds capacity.");saved.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Gem inventory changed.");
                    SilverBalance.Persist(c,t,p,before-cost);
                    foreach(var item in parts)
                        if(Q.Execute(c,t,@"DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND bound=@bound AND stacksize=@stack
AND position=0 AND warehouse=0 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')='0'","item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)type,"bound",bound?1:0,"stack",(int)item.StackSize)!=1)
                            throw new InvalidOperationException("Gem material was not consumed.");
                    Q.Insert(c,t,p.Entity.UID,reward);
                    if(Q.Execute(c,t,@"INSERT INTO revora_gem_compositions(id,uid,request_hash,input_type,output_type,bound,cost,silver_before,silver_after,item_uid,created_at)
VALUES(@id,@uid,@hash,@input,@output,@bound,@cost,@before,@after,@item,@now)","id",id,"uid",(long)p.Entity.UID,"hash",fingerprint,"input",(int)type,"output",(int)reward.ID,
                        "bound",bound,"cost",(long)cost,"before",checked((long)before),"after",checked((long)(before-cost)),"item",(long)reward.UID,"now",now)!=1)
                        throw new InvalidOperationException("Gem composition was not recorded.");
                    foreach(var item in parts)
                        if(Q.Execute(c,t,"INSERT INTO revora_gem_materials(composition,item_uid) VALUES(@id,@item)","id",id,"item",(long)item.UID)!=1)
                            throw new InvalidOperationException("Gem material receipt was not saved.");
                    if(!Ready(p) || p.Entity.Money!=before || !parts.All(i=>p.Inventory.Objects.Any(j=>ReferenceEquals(i,j)) && Material(i,type,bound)))
                        throw new InvalidOperationException("Gem customer changed.");
                    t.Commit();
                }}
                SilverBalance.ApplyCommitted(p,before-cost);
                Q.Install(p,parts.Select(i=>i.UID),new[]{reward});
                // Preserve the existing success parameter pending native UI acceptance.
                var response=new ItemUsage(true){UID=type,Action=ItemUsage.Mode.GemCompose,dwParam=1};p.Send(response);
            }catch(Exception ex){Q.Fail(p,"gem-composition",ex);}
        }
    }
}
