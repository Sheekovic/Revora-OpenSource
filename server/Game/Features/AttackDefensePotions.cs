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
    // Ordinary leftover Attack/Defense potions issued by owned StarterPacks.
    // Durations match the live handler: 721623/721624 15, 721672/721673 30,
    // 721627/721628 45 on Stigma/MagicShield Time, Increase and AddFlag.
    internal static class AttackDefensePotions
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action<string> Spoken;
        private static bool enabled;
        internal static bool Enabled{get{return enabled;}}
        private sealed class Spec{internal bool Stigma;internal int Seconds;internal string Text;}
        private static readonly Dictionary<uint,Spec> Catalog=new Dictionary<uint,Spec>{
            {721623,new Spec{Stigma=true,Seconds=15,Text="Your attack will increase during the next 15 minutes"}},
            {721672,new Spec{Stigma=true,Seconds=30,Text="Your attack will increase during the next 30 minutes"}},
            {721627,new Spec{Stigma=true,Seconds=45,Text="Your attack will increase during the next 45 minutes"}},
            {721624,new Spec{Stigma=false,Seconds=15,Text="Your Defense will increase during the next 15 minutes"}},
            {721673,new Spec{Stigma=false,Seconds=30,Text="Your Defense will increase during the next 30 minutes"}},
            {721628,new Spec{Stigma=false,Seconds=45,Text="Your Defense will increase during the next 45 minutes"}}
        };
        private static bool Handles(uint id){return Catalog.ContainsKey(id);}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_attack_defense_state(
uid bigint PRIMARY KEY CHECK(uid>0),
stigma_seconds integer NOT NULL CHECK(stigma_seconds IN(0,15,30,45)),
stigma_increase integer NOT NULL CHECK(stigma_increase IN(0,15,30,45)),
stigma_granted_at timestamptz,
magic_seconds integer NOT NULL CHECK(magic_seconds IN(0,15,30,45)),
magic_increase integer NOT NULL CHECK(magic_increase IN(0,15,30,45)),
magic_granted_at timestamptz,
CHECK((stigma_seconds=0 AND stigma_increase=0 AND stigma_granted_at IS NULL) OR (stigma_seconds IN(15,30,45) AND stigma_increase=stigma_seconds AND stigma_granted_at IS NOT NULL)),
CHECK((magic_seconds=0 AND magic_increase=0 AND magic_granted_at IS NULL) OR (magic_seconds IN(15,30,45) AND magic_increase=magic_seconds AND magic_granted_at IS NOT NULL)));");
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_attack_defense_potion_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,
item_type integer NOT NULL CHECK(item_type IN(721623,721624,721627,721628,721672,721673)),
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,
seconds integer NOT NULL CHECK(seconds IN(15,30,45)),
kind varchar(12) NOT NULL CHECK(kind IN('stigma','magicshield')),used_at timestamptz NOT NULL,
CHECK((item_type IN(721623,721672,721627) AND kind='stigma') OR (item_type IN(721624,721673,721628) AND kind='magicshield')),
CHECK((item_type IN(721623,721624) AND seconds=15) OR (item_type IN(721672,721673) AND seconds=30) OR (item_type IN(721627,721628) AND seconds=45)));");
            }enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Hear(GameState p,string text){p.Send(new Message(text,Message.System));var s=Spoken;if(s!=null)s(text);}
        internal static void Project(GameState p,AttackDefenseBalance.Image image)
        {
            DateTime now=Clock();
            int stigmaLeft=AttackDefenseBalance.Remaining(image.StigmaSeconds,image.StigmaGranted,now);
            if(stigmaLeft>0)
            {
                p.Entity.StigmaStamp=Time32.Now;
                p.Entity.StigmaTime=(byte)stigmaLeft;
                p.Entity.StigmaIncrease=image.StigmaIncrease;
                if(!p.Entity.BitVector.UpdateFlag((int)FlagsUpdate.Flags.Stigma,stigmaLeft,true,stigmaLeft))
                    p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Stigma,stigmaLeft,true);
            }
            int magicLeft=AttackDefenseBalance.Remaining(image.MagicSeconds,image.MagicGranted,now);
            if(magicLeft>0)
            {
                p.Entity.MagicShieldStamp=Time32.Now;
                p.Entity.MagicShieldTime=(byte)magicLeft;
                p.Entity.MagicShieldIncrease=image.MagicIncrease;
                if(!p.Entity.BitVector.UpdateFlag((int)FlagsUpdate.Flags.MagicShield,magicLeft,true,magicLeft))
                    p.Entity.AddFlag((ulong)FlagsUpdate.Flags.MagicShield,magicLeft,true);
            }
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
                Spec spec=Catalog[item.ID];
                var next=AttackDefenseBalance.Current(p);
                DateTime granted=AttackDefenseBalance.GrantStamp(now);
                if(spec.Stigma){next.StigmaSeconds=spec.Seconds;next.StigmaIncrease=spec.Seconds;next.StigmaGranted=granted;}
                else{next.MagicSeconds=spec.Seconds;next.MagicIncrease=spec.Seconds;next.MagicGranted=granted;}
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Attack/Defense potion owner missing.");
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Attack/Defense potion inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Attack/Defense potion inventory changed.");
                    string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Attack/Defense potion ownership changed.");
                    AttackDefenseBalance.Persist(c,t,p,next);
                    if(Q.Execute(c,t,@"INSERT INTO revora_attack_defense_potion_uses(id,uid,item_uid,item_type,before_stack,bound,seconds,kind,used_at)
VALUES(@id,@uid,@item,@type,@stack,@bound,@seconds,@kind,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"seconds",spec.Seconds,
                        "kind",spec.Stigma?"stigma":"magicshield","now",now)!=1)
                        throw new InvalidOperationException("Attack/Defense potion receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Attack/Defense potion session changed.");t.Commit();
                }}
                Q.Install(p,item.StackSize<=1?new[]{item.UID}:new uint[0],new ConquerItem[0]);
                if(item.StackSize>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                AttackDefenseBalance.Committed(p,next);
                Project(p,next);
                Hear(p,spec.Text);
            }catch(Exception ex){Q.Fail(p,"attack-defense-potion",ex);}return true;
        }
    }
}
