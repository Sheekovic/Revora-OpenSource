using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // MillionaireLee NPC 11275 options 1/2/3/4/5/7 pack exact UID-ordered payments
    // into one Plus-0 pack. Any bound unit in that payment binds the pack.
    // Item Plus is not a gate. Option 6 is menu text only and has no handler.
    internal static class MillionaireStonePacking
    {
        internal const uint Clerk=11275;
        internal const ushort MapId=1002;
        internal const uint Stone1=730001,Pack1=723712,Stone2=730002,Pack2=729022,Stone3=730003,Pack3=729023;
        internal const uint Meteor=1088001,MeteorPack=720027,Ball=1088000,BallPack=720028,ExpBall=723700,ExpPack=720757;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action<string> Spoken;
        internal static Action<GameState> BeforeInstall;
        private static bool enabled;
        internal static bool Enabled{get{return enabled;}}
        private sealed class Take{internal ConquerItem Item;internal int Amount,Units;internal ushort Before;internal bool Bound;}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_millionaire_stone_packs(
id uuid PRIMARY KEY,uid bigint NOT NULL,option smallint NOT NULL CHECK(option IN (1,2,3,4,5,7)),
stone_type integer NOT NULL,pack_uid bigint NOT NULL,pack_type integer NOT NULL,
bound boolean NOT NULL,source_uids bigint[] NOT NULL,source_amounts integer[] NOT NULL,
used_at timestamptz NOT NULL);
ALTER TABLE revora_millionaire_stone_packs DROP CONSTRAINT IF EXISTS revora_millionaire_stone_packs_option_check;
ALTER TABLE revora_millionaire_stone_packs ADD CONSTRAINT revora_millionaire_stone_packs_option_check CHECK(option IN (1,2,3,4,5,7));");}
            foreach(uint id in new uint[]{Stone1,Pack1,Stone2,Pack2,Stone3,Pack3,Meteor,MeteorPack,Ball,BallPack,ExpBall,ExpPack})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))
                    throw new InvalidOperationException("Millionaire stone packing catalog incomplete.");
            enabled=true;
        }
        private static bool Option(byte option,out uint stone,out uint pack,out int need,out string poor){
            stone=0;pack=0;need=0;poor=null;
            if(option==1){stone=Meteor;pack=MeteorPack;need=10;poor="Poor man! I can't help you.";return true;}
            if(option==2){stone=Ball;pack=BallPack;need=10;poor="Poor man! I can't help you.";return true;}
            if(option==7){stone=ExpBall;pack=ExpPack;need=10;poor="Poor man! I can't help you, you don't have 10 Exp Balls.";return true;}
            if(option==3){stone=Stone1;pack=Pack1;need=5;poor="Poor man! I can't help you, you don't have 5 stones +1.";return true;}
            if(option==4){stone=Stone2;pack=Pack2;need=10;poor="Poor man! I can't help you, you don't have 10 stones +2.";return true;}
            if(option==5){stone=Stone3;pack=Pack3;need=10;poor="Poor man! I can't help you, you don't have 10 stones +3.";return true;}
            return false;
        }
        private static int Units(ConquerItem item){return item.StackSize<=1?1:item.StackSize;}
        private static bool Eligible(ConquerItem item,uint stone,DateTime now){
            if(item==null || item.ID!=stone || item.Position!=0 || item.Warehouse!=0 || item.Lock!=0 || item.Suspicious || item.InWardrobe)return false;
            return item.Days==0?item.TimeLeftInMinutes==0:now.ToLocalTime()<item.DayStamp.AddDays(item.Days);
        }
        private static void Say(GameState p,string text){
            var dialog=new Npcs(p);dialog.Text(text);dialog.Option("Sorry!",255);dialog.Avatar(7);dialog.Send();
            var s=Spoken;if(s!=null)s(text);
        }
        private static void Full(GameState p){p.Send(Constants.FullInventory);var s=Spoken;if(s!=null)s("Your inventory is Full.");}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            uint stone,pack;int need;string poor;
            if(p==null || request==null || p.ActiveNpc!=Clerk || !Option(request.OptionID,out stone,out pack,out need,out poor))return false;
            if(request.NpcID!=Clerk || !enabled || !Q.Near(p,Clerk,MapId))return true;
            TryPack(p,request.OptionID,stone,pack,need,poor);return true;
        }
        private static void TryPack(GameState p,byte option,uint stone,uint pack,int need,string poor){
            lock(p.ItemSyncRoot)try{
                DateTime now=Clock();
                if(!enabled || !Q.Near(p,Clerk,MapId))return;
                var takes=new List<Take>();int have=0;bool bound=false;
                foreach(var item in p.Inventory.Objects.OrderBy(i=>i.UID)){
                    if(!Eligible(item,stone,now))continue;
                    int units=Units(item);int left=need-have;if(left<=0)break;
                    int take=units<left?units:left;
                    takes.Add(new Take{Item=item,Amount=take,Units=units,Before=item.StackSize,Bound=item.Bound});
                    if(item.Bound)bound=true;have+=take;
                }
                if(have<need){Say(p,poor);return;}
                int released=0;foreach(var take in takes)if(take.Amount==take.Units)released++;
                if(p.Inventory.Count+(1-released)>40){Full(p);return;}
                ushort dur=1;
                if(ConquerItemInformation.BaseInformations.ContainsKey(pack))dur=ConquerItemInformation.BaseInformations[pack].Durability;
                var reward=new ConquerItem(true){ID=pack,UID=ConquerItem.ItemUID.Next,Plus=0,Bound=bound,StackSize=1,Durability=dur,MaximDurability=dur};
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Millionaire stone packing owner missing.");
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Millionaire stone packing inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Millionaire stone packing inventory changed.");
                    foreach(var take in takes){
                        bool deplete=take.Amount==take.Units;
                        string sql=deplete?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-@take";
                        if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                            "take",take.Amount,"item",(long)take.Item.UID,"uid",(long)p.Entity.UID,"type",(int)take.Item.ID,"stack",(int)take.Before,"bound",take.Bound?1:0,
                            "days",take.Item.Days.ToString(CultureInfo.InvariantCulture),"permanent",take.Item.Days==0,"stamp",take.Item.DayStamp.ToBinary())!=1)
                            throw new InvalidOperationException("Millionaire stone packing source changed.");
                    }
                    if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,0,@dur,@max)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                        "bound",reward.Bound?1:0,"dur",(int)reward.Durability,"max",(int)reward.MaximDurability)!=1)
                        throw new InvalidOperationException("Millionaire stone pack was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_millionaire_stone_packs(id,uid,option,stone_type,pack_uid,pack_type,bound,source_uids,source_amounts,used_at)
VALUES(@id,@uid,@option,@stone,@pack,@type,@bound,@sources,@amounts,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"option",(short)option,
                        "stone",(int)stone,"pack",(long)reward.UID,"type",(int)reward.ID,"bound",bound,
                        "sources",takes.Select(x=>(long)x.Item.UID).ToArray(),"amounts",takes.Select(x=>x.Amount).ToArray(),"now",now)!=1)
                        throw new InvalidOperationException("Millionaire stone packing receipt was not saved.");
                    if(!Q.Near(p,Clerk,MapId))throw new InvalidOperationException("Millionaire stone packing session changed.");
                    t.Commit();
                }}
                var before=BeforeInstall;if(before!=null)before(p);
                var removed=new List<uint>();foreach(var take in takes)if(take.Amount==take.Units)removed.Add(take.Item.UID);
                Q.Install(p,removed,new[]{reward});
                foreach(var take in takes)if(take.Amount!=take.Units){
                    take.Item.StackSize=(ushort)(take.Item.StackSize-take.Amount);
                    take.Item.Mode=Enums.ItemMode.Update;take.Item.Send(p);take.Item.Mode=Enums.ItemMode.Default;
                }
            }catch(Exception ex){Q.Fail(p,"millionaire-stone-packing",ex);}
        }
    }
}
