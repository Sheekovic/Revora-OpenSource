using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Ordinary Power Arena badge exchange and leftover refinery-pack opening.
    // Sixteen families, three tiers, leftover GetNextRefineryItem extras, and
    // the installed 724449->724445 one-hand Breakthrough identity.
    internal static class PowerArenaPacks
    {
        internal const uint Badge=720148;
        internal static readonly uint[] Bases={723654,723651,723657,723133,724136,724139,723660,723666,723663,724142,723672,723675,724145,723669,723684,723130};
        private static readonly uint[] Offsets={751,749,753,1287,294,301,789,793,791,331,811,813,208,689,696,1260};
        internal static readonly string[] Names={"1-hand Critical Strike","Bow Critical Strike","2-hand Critical Strike","Backsword Skill Critical","Armor Immunity","Headgear Intensification","1-hand Breakthrough","Bow Breakthrough","2-hand Breakthrough","Armor Counteraction","Headgear Detoxication","Armor Detoxication","Headgear Block","Shield Block","Headgear Penetration","Bracelet Penetration"};
        private static readonly int[] Costs={2,6,12};
        internal static Func<DateTime> Clock=()=>PowerArenaEvent.Clock();
        internal static Func<byte> Extra=()=>ItemHandler.GetNextRefineryItem();
        private sealed class Offer {internal int Family,Tier;internal DateTime Expires;}
        private static readonly ConcurrentDictionary<uint,Offer> offers=new ConcurrentDictionary<uint,Offer>();
        private static bool enabled;
        private static bool Handles(uint id){return Array.FindIndex(Bases,b=>id>=b && id<=b+2)>=0;}
        internal static void ExpireOffers(DateTime now) {
            foreach(var entry in offers.ToArray())if(entry.Value.Expires<=now)
                ((ICollection<KeyValuePair<uint,Offer>>)offers).Remove(entry);
        }
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_power_exchanges(
id uuid PRIMARY KEY,uid bigint NOT NULL,pack_uid bigint NOT NULL UNIQUE,pack_type integer NOT NULL,badges integer NOT NULL,created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);");
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_power_pack_results(id bigserial PRIMARY KEY,pack_uid bigint NOT NULL,uid bigint NOT NULL,result_uid bigint NOT NULL,result_type integer NOT NULL,created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);");
                // The original ledger keyed pack_uid, which rejected a legitimate second use
                // from the same stacked item. Give every use its own immutable receipt instead
                // of leaving the migrated ledger without a primary key.
                Q.Execute(c,null,@"ALTER TABLE revora_power_pack_results ADD COLUMN IF NOT EXISTS id bigserial;
ALTER TABLE revora_power_pack_results DROP CONSTRAINT IF EXISTS revora_power_pack_results_pkey;
ALTER TABLE revora_power_pack_results ADD CONSTRAINT revora_power_pack_results_pkey PRIMARY KEY(id);");
            }
            for(int f=0;f<Bases.Length;f++)for(int tier=0;tier<3;tier++)for(byte extra=0;extra<3;extra++) {
                if(!ConquerItemInformation.BaseInformations.ContainsKey(Bases[f]+(uint)tier)
                    || !ConquerItemInformation.BaseInformations.ContainsKey(Result(f,tier,extra)))throw new InvalidOperationException("Power Arena refinery catalog incomplete.");
            }
            offers.Clear();enabled=true;
        }
        internal static uint Result(int family,int tier,byte extra) {
            uint id=Bases[family]+(uint)tier+Offsets[family]+extra;
            return id==724449?724445:id; // Installed one-hand Breakthrough material identity.
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static List<uint> Bag(NpgsqlConnection c,NpgsqlTransaction t,GameState p,string name){
            var ids=new List<uint>();
            using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException(name+" inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException(name+" inventory changed.");
            return ids;
        }
        internal static bool Menu(GameState p,Npcs d,byte option) {
            if(option==10 || option==11) {
                Offer ignored;offers.TryRemove(p.Entity.UID,out ignored);
                d.Text("Choose the craft your equipment needs. Bring the badges in your bag.");
                int first=option==10?0:8;for(int i=first;i<first+8;i++)d.Option(Names[i],(byte)(40+i));
                d.Option(option==10?"More crafts.":"Previous crafts.",option==10?(byte)11:(byte)10);return true;
            }
            if(option>=40 && option<56) {
                int f=option-40;d.Text(Names[f]+": choose the quality of your pack.");
                for(int tier=0;tier<3;tier++)d.Option((tier==0?"Primary":tier==1?"Medium":"Superior")+" - "+Costs[tier]+" badges.",(byte)(100+f*3+tier));return true;
            }
            if(option>=100 && option<148) {
                int f=(option-100)/3,tier=(option-100)%3;
                offers[p.Entity.UID]=new Offer{Family=f,Tier=tier,Expires=Clock().AddMinutes(2)};
                d.Text("I will exchange "+Costs[tier]+" badges for a bound "+Names[f]+" pack.");d.Option("Make the exchange.",230);return true;
            }
            if(option==230) {
                Offer offer;if(!offers.TryRemove(p.Entity.UID,out offer) || offer.Expires<=Clock()){d.Text("Choose your pack again when you are ready.");return true;}
                d.Text(Exchange(p,offer));return true;
            }
            return false;
        }
        private sealed class Take {internal ConquerItem Item;internal ushort Before,Count;internal bool Bound;internal byte Days;internal DateTime Stamp;}
        private static string Exchange(GameState p,Offer offer) {
            if(!Ready(p))return "Choose your pack again when you are ready.";
            int price=Costs[offer.Tier],left=price;var takes=new List<Take>();
            foreach(var item in p.Inventory.Objects.Where(i=>i.ID==Badge && i.Position==0 && i.Warehouse==0 && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.StackSize>0).OrderBy(i=>i.UID)) {
                ushort amount=(ushort)Math.Min(left,item.StackSize);takes.Add(new Take{Item=item,Before=item.StackSize,Count=amount,Bound=item.Bound,Days=item.Days,Stamp=item.DayStamp});left-=amount;if(left==0)break;
            }
            if(left!=0)return "You need "+price+" Refinery Badges in your bag.";
            int freed=takes.Count(a=>a.Count==a.Before);
            if(p.Inventory.Count-freed+1>40)return "Make one space for the pack first.";
            var pack=Q.New(Bases[offer.Family]+(uint)offer.Tier);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Power Arena exchange owner missing.");
                Bag(c,t,p,"Power Arena exchange");
                foreach(var a in takes) {
                    string sql=a.Count==a.Before?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-@amount";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0 AND stacksize=@before AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "amount",(int)a.Count,"item",(long)a.Item.UID,"uid",(long)p.Entity.UID,"type",(int)Badge,"before",(int)a.Before,"bound",a.Bound?1:0,
                        "days",a.Days.ToString(CultureInfo.InvariantCulture),"permanent",a.Days==0,"stamp",a.Stamp.ToBinary())!=1)
                        throw new InvalidOperationException("Refinery Badge ownership changed.");
                }
                Q.Insert(c,t,p.Entity.UID,pack);
                if(Q.Execute(c,t,"INSERT INTO revora_power_exchanges(id,uid,pack_uid,pack_type,badges) VALUES(@id,@uid,@pack,@type,@badges)",
                    "id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"pack",(long)pack.UID,"type",(int)pack.ID,"badges",price)!=1)
                    throw new InvalidOperationException("Power Arena exchange receipt was not saved.");
                if(!Ready(p))throw new InvalidOperationException("Power Arena exchange session changed.");
                t.Commit();
            }}
            Q.Install(p,takes.Where(a=>a.Count==a.Before).Select(a=>a.Item.UID),new[]{pack});
            foreach(var a in takes.Where(a=>a.Count<a.Before)){a.Item.StackSize=(ushort)(a.Before-a.Count);a.Item.Mode=Enums.ItemMode.Update;a.Item.Send(p);a.Item.Mode=Enums.ItemMode.Default;}
            return "Your pack is ready. Open it to receive the refinery material.";
        }
        internal static bool TryHandle(GameState p,byte[] packet){
            ConquerItem item;
            if(p==null || p.Inventory==null || packet==null || packet.Length<12 || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet,8),out item) || !Handles(item.ID))return false;
            if(packet.Length<32 || packet.Length>128 || BitConverter.ToUInt16(packet,0)+8!=packet.Length || BitConverter.ToUInt16(packet,2)!=1009
                || BitConverter.ToUInt16(packet,20)!=4 || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal)return true;
            TryUse(item,p);return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || !Handles(item.ID))return false;if(!Ready(p))return true;
            int f=Array.FindIndex(Bases,id=>item.ID>=id && item.ID<=id+2);if(f<0)return false;
            lock(p.ItemSyncRoot)try {
                ConquerItem owned;DateTime now=Clock();
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item) || item.Position!=0 || item.Warehouse!=0
                    || item.Lock!=0 || item.Suspicious || item.InWardrobe || (item.Days==0?item.TimeLeftInMinutes!=0:now.ToLocalTime()>=item.DayStamp.AddDays(item.Days)))return true;
                bool deplete=item.StackSize<=1;
                if(p.Inventory.Count+(deplete?0:1)>40){p.Send(Constants.FullInventory);return true;}
                byte extra=Extra();if(extra>2)return true;
                var reward=Q.New(Result(f,(int)(item.ID-Bases[f]),extra),item.Bound);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Power Arena pack owner missing.");
                    Bag(c,t,p,"Power Arena pack");
                    string sql=deplete?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Power Arena pack ownership changed.");
                    Q.Insert(c,t,p.Entity.UID,reward);
                    if(Q.Execute(c,t,"INSERT INTO revora_power_pack_results(pack_uid,uid,result_uid,result_type) VALUES(@pack,@uid,@result,@type)",
                        "pack",(long)item.UID,"uid",(long)p.Entity.UID,"result",(long)reward.UID,"type",(int)reward.ID)!=1)
                        throw new InvalidOperationException("Power Arena pack receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Power Arena pack session changed.");
                    t.Commit();
                }}
                Q.Install(p,deplete?new[]{item.UID}:new uint[0],new[]{reward});
                if(!deplete){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
            }catch(Exception ex){Q.Fail(p,"power-arena-pack",ex);}
            return true;
        }
    }
}
