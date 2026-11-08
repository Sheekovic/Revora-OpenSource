using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using Wire=Revora.Network.GamePackets.Lottery;

namespace Revora.Game.Features
{
    internal static class LotteryDraws
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int> Roll=RandomIndex;
        private static LotteryTable.LotteryItem[] pool;
        private sealed class View {internal Guid Id;internal int Step;internal DateTime Until;}
        private static readonly ConditionalWeakTable<GameState,View> views=new ConditionalWeakTable<GameState,View>();
        private sealed class Draw {
            internal Guid Id;internal uint Box,Type,Item;internal byte Plus,Sockets;internal ushort Durability;
            internal bool Bound;internal int Step;
        }
        private sealed class Debit {internal ConquerItem Item;internal int Units;}
        private static int RandomIndex(int max) {
            if(max<=0)throw new ArgumentOutOfRangeException("max");
            using(var rng=RandomNumberGenerator.Create()){var bytes=new byte[4];ulong limit=(1UL<<32)/(uint)max*(uint)max;
                for(int i=0;i<128;i++){rng.GetBytes(bytes);uint value=BitConverter.ToUInt32(bytes,0);if(value<limit)return (int)(value%(uint)max);}}
            throw new InvalidOperationException("Lottery random source exhausted.");
        }
        private static int Next(int max){int value=Roll(max);if(value<0 || value>=max)throw new InvalidOperationException("Invalid lottery draw.");return value;}
        internal static void Initialize() {
            var entries=LotteryTable.LotteryItems.ToArray();
            ValidatePool(entries);
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_lottery_draws(
id uuid PRIMARY KEY,uid bigint NOT NULL,box integer NOT NULL CHECK(box BETWEEN 925 AND 929),prize bigint NOT NULL,
plus integer NOT NULL CHECK(plus BETWEEN 0 AND 12),sockets integer NOT NULL CHECK(sockets BETWEEN 0 AND 2),
durability integer NOT NULL CHECK(durability BETWEEN 0 AND 65535),bound boolean NOT NULL,step integer NOT NULL CHECK(step BETWEEN 0 AND 2),
claimed boolean NOT NULL DEFAULT false,item_uid bigint NOT NULL DEFAULT 0,created_at timestamptz NOT NULL,
CHECK((NOT claimed AND item_uid=0) OR (claimed AND item_uid>0)));
CREATE UNIQUE INDEX IF NOT EXISTS revora_lottery_pending_owner ON revora_lottery_draws(uid) WHERE NOT claimed;
CREATE TABLE IF NOT EXISTS revora_lottery_steps(draw_id uuid NOT NULL REFERENCES revora_lottery_draws(id),step integer NOT NULL CHECK(step BETWEEN 0 AND 2),
source_uids bigint[] NOT NULL,source_stacks integer[] NOT NULL,source_units integer[] NOT NULL,prize bigint NOT NULL,
plus integer NOT NULL,sockets integer NOT NULL,bound boolean NOT NULL,created_at timestamptz NOT NULL,PRIMARY KEY(draw_id,step));");}
            pool=entries;
        }
        private static void ValidatePool(LotteryTable.LotteryItem[] entries) {
            if(entries.Length==0 || entries.Length>10000 || entries.Any(x=>x.Rank<=0 || x.Rank>=35 || x.Chance<=0 || x.Plus>12 || x.Sockets>2
                || !ConquerItemInformation.BaseInformations.ContainsKey(x.ID)))throw new InvalidOperationException("Lottery prize catalog requires review.");
        }
        private static bool Ready(GameState p,uint box) {return pool!=null && box>=925 && box<=929 && Q.Near(p,box,700);}
        private static bool Usable(ConquerItem item,DateTime now,uint type) {return item.ID==type && item.Position==0 && item.Warehouse==0 && item.Lock==0
            && !item.Suspicious && !item.InWardrobe && (item.Days==0?item.TimeLeftInMinutes==0:now.ToLocalTime()<item.DayStamp.AddDays(item.Days));}
        private static List<Debit> Payment(GameState p,int units,DateTime now,uint type) {
            var result=new List<Debit>();
            foreach(var item in p.Inventory.Objects.Where(i=>Usable(i,now,type)).OrderBy(i=>i.UID)) {
                int take=Math.Min(units,Math.Max(1,(int)item.StackSize));result.Add(new Debit{Item=item,Units=take});units-=take;if(units==0)return result;
            }
            return null;
        }
        private static LotteryTable.LotteryItem Pick() {
            // Preserve the two legacy acceptance tests; cap retries before any payment.
            for(int i=0;i<4096;i++){var item=pool[Next(pool.Length)];
                if(item.Rank>Next(item.Chance) && item.Rank>Next(35-item.Rank))return item;}
            throw new InvalidOperationException("Lottery selection exhausted without payment.");
        }
        private static void LockOwner(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                if(q.ExecuteScalar()==null)throw new InvalidOperationException("Lottery owner missing.");
        }
        private static Draw Pending(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            using(var q=Q.Command(c,t,"SELECT id,box,prize,plus,sockets,durability,bound,step FROM revora_lottery_draws WHERE uid=@uid AND NOT claimed FOR UPDATE","uid",(long)uid))
            using(var r=q.ExecuteReader()){if(!r.Read())return null;return new Draw{Id=r.GetGuid(0),Box=checked((uint)Convert.ToInt64(r[1])),Type=checked((uint)Convert.ToInt64(r[2])),
                Plus=checked((byte)Convert.ToInt32(r[3])),Sockets=checked((byte)Convert.ToInt32(r[4])),Durability=checked((ushort)Convert.ToInt32(r[5])),Bound=r.GetBoolean(6),Step=r.GetInt32(7)};}
        }
        private static void CheckBag(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Lottery bag exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Lottery inventory changed.");
        }
        private static void Consume(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Debit debit) {
            var item=debit.Item;int left=Math.Max(1,(int)item.StackSize)-debit.Units;
            string sql=left==0?"DELETE FROM items":"UPDATE items SET stacksize=@left";
            if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                "left",left,"type",(int)item.ID,"item",(long)item.UID,"uid",(long)p.Entity.UID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                throw new InvalidOperationException("Lottery ticket ownership changed.");
        }
        private static void ProjectPayment(GameState p,List<Debit> payment) {
            Q.Install(p,payment.Where(d=>Math.Max(1,(int)d.Item.StackSize)==d.Units).Select(d=>d.Item.UID),new ConquerItem[0]);
            foreach(var d in payment.Where(d=>Math.Max(1,(int)d.Item.StackSize)>d.Units)) {
                d.Item.StackSize=(ushort)(d.Item.StackSize-d.Units);d.Item.Mode=Enums.ItemMode.Update;d.Item.Send(p);d.Item.Mode=Enums.ItemMode.Default;
            }
        }
        private static ConquerItem Prize(Draw draw) {return new ConquerItem(true){UID=draw.Item,ID=draw.Type,StackSize=1,Bound=draw.Bound,Plus=draw.Plus,
            SocketOne=draw.Sockets>0?Enums.Gem.EmptySocket:Enums.Gem.NoSocket,SocketTwo=draw.Sockets>1?Enums.Gem.EmptySocket:Enums.Gem.NoSocket,
            Color=Enums.Color.Blue,Durability=draw.Durability,MaximDurability=draw.Durability};}
        private static void Show(GameState p,Draw draw,View view) {
            view.Id=draw.Id;view.Step=draw.Step;var item=Prize(draw);
            new Wire{Type=Wire.LotteryTypes.ShowGUI,Prize=item.ID,Plus=item.Plus,Color=item.Color,SocketOne=(byte)item.SocketOne,
                SocketTwo=(byte)item.SocketTwo,AddJadeChances=(byte)draw.Step}.Send(p);
        }
        internal static void Open(GameState p,uint box,bool reroll=false) {
            if(!Ready(p,box))return;
            lock(p.ItemSyncRoot)try {
                if(!Ready(p,box))return;var view=views.GetValue(p,_=>new View());DateTime now=Clock();Draw draw;
                List<Debit> payment=null;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    LockOwner(c,t,p);draw=Pending(c,t,p.Entity.UID);
                    if(draw!=null && draw.Box!=box){p.Send("Return to the box holding your pending prize.");return;}
                    if(draw!=null && !reroll){t.Commit();Show(p,draw,view);return;}
                    if(now<view.Until || (reroll && (draw==null || draw.Box!=box || draw.Step>=2 || view.Id!=draw.Id || view.Step!=draw.Step)))return;
                    payment=Payment(p,reroll?1:3,now,711504);
                    if(payment==null){p.Send(reroll?"You need one Small Lottery Ticket to try again.":"You need three Small Lottery Tickets to open this box.");return;}
                    CheckBag(c,t,p);var selected=Pick();
                    if(draw==null)draw=new Draw{Id=Guid.NewGuid(),Box=box,Step=0};else draw.Step++;
                    draw.Type=selected.ID;draw.Plus=selected.Plus;draw.Sockets=selected.Sockets;draw.Durability=ConquerItemInformation.BaseInformations[draw.Type].Durability;
                    draw.Bound=draw.Bound || payment.Any(d=>d.Item.Bound);
                    foreach(var d in payment)Consume(c,t,p,d);
                    string sql=reroll?"UPDATE revora_lottery_draws SET prize=@prize,plus=@plus,sockets=@sockets,durability=@dur,bound=@bound,step=@step WHERE id=@id AND uid=@uid AND NOT claimed AND step=@previous"
                        :"INSERT INTO revora_lottery_draws(id,uid,box,prize,plus,sockets,durability,bound,step,created_at) VALUES(@id,@uid,@box,@prize,@plus,@sockets,@dur,@bound,@step,@now)";
                    if(Q.Execute(c,t,sql,"id",draw.Id,"uid",(long)p.Entity.UID,"box",(int)box,"prize",(long)draw.Type,"plus",(int)draw.Plus,"sockets",(int)draw.Sockets,
                        "dur",(int)draw.Durability,"bound",draw.Bound,"step",draw.Step,"previous",draw.Step-1,"now",now)!=1)throw new InvalidOperationException("Lottery result was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_lottery_steps(draw_id,step,source_uids,source_stacks,source_units,prize,plus,sockets,bound,created_at)
VALUES(@id,@step,@sources,@stacks,@units,@prize,@plus,@sockets,@bound,@now)","id",draw.Id,"step",draw.Step,
                        "sources",payment.Select(d=>(long)d.Item.UID).ToArray(),"stacks",payment.Select(d=>(int)d.Item.StackSize).ToArray(),"units",payment.Select(d=>d.Units).ToArray(),
                        "prize",(long)draw.Type,"plus",(int)draw.Plus,"sockets",(int)draw.Sockets,"bound",draw.Bound,"now",now)!=1)throw new InvalidOperationException("Lottery payment receipt was not saved.");
                    if(!Ready(p,box))throw new InvalidOperationException("Lottery session changed.");t.Commit();
                }}
                view.Until=now.AddSeconds(1);ProjectPayment(p,payment);Show(p,draw,view);
            }catch(Exception ex){Q.Fail(p,"lottery-draw",ex);}
        }
        internal static void Claim(GameState p) {
            uint box=p==null?0:p.ActiveNpc;if(!Ready(p,box))return;
            lock(p.ItemSyncRoot)try {
                if(!Ready(p,box))return;var view=views.GetValue(p,_=>new View());ConquerItem item;bool achievement;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    LockOwner(c,t,p);var draw=Pending(c,t,p.Entity.UID);
                    if(draw==null || draw.Box!=box || view.Id!=draw.Id || view.Step!=draw.Step)return;
                    if(p.Inventory.Count>=40){p.Send("Make room for your lottery prize.");return;}CheckBag(c,t,p);
                    draw.Item=ConquerItem.ItemUID.Next;item=Prize(draw);
                    if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability,plus,socketone,sockettwo,color)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@dur,@plus,@one,@two,@color)","item",(long)item.UID,"type",(int)item.ID,"uid",(long)p.Entity.UID,"bound",item.Bound?1:0,
                        "dur",(int)item.Durability,"plus",(int)item.Plus,"one",(int)item.SocketOne,"two",(int)item.SocketTwo,"color",(int)item.Color)!=1)throw new InvalidOperationException("Lottery prize was not saved.");
                    if(Q.Execute(c,t,"UPDATE revora_lottery_draws SET claimed=true,item_uid=@item WHERE id=@id AND uid=@uid AND NOT claimed AND step=@step",
                        "item",(long)item.UID,"id",draw.Id,"uid",(long)p.Entity.UID,"step",draw.Step)!=1)throw new InvalidOperationException("Lottery claim was not saved.");
                    achievement=ProgressionEvidence.Complete(c,t,p.Entity.UID,10118,"lottery-prize-v1",draw.Id.ToString("N"),1);
                    if(!Ready(p,box))throw new InvalidOperationException("Lottery session changed.");t.Commit();
                }}
                Q.Install(p,new uint[0],new[]{item});view.Id=Guid.Empty;view.Until=Clock().AddSeconds(1);
                if(achievement)Q.Refresh(p);
                p.Send("Your lottery prize is in your inventory.");
            }catch(Exception ex){Q.Fail(p,"lottery-claim",ex);}
        }
        internal static void Process(byte[] packet,GameState p) {
            if(packet==null || packet.Length<14 || packet.Length>128 || BitConverter.ToUInt16(packet,0)+8!=packet.Length
                || BitConverter.ToUInt16(packet,2)!=1314 || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal)return;
            ushort action=BitConverter.ToUInt16(packet,4);if(action>2 || p==null)return;
            if(action==0)Claim(p);else Open(p,p.ActiveNpc,action==1);
        }
    }
}
