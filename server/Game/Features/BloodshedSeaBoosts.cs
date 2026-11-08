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
    // Ordinary leftover Bloodshed Sea boosts from installed ConquerPoints
    // EShops 16834/16852-16856 on map 3846. Leftover 80-second stamp/refresh
    // does not stack numeric bonuses.
    internal static class BloodshedSeaBoosts
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action<string> Spoken;
        private static bool enabled;
        internal static bool Enabled{get{return enabled;}}
        private sealed class Spec{internal int Kind;internal string Text;}
        private static readonly Dictionary<uint,Spec> Catalog=new Dictionary<uint,Spec>{
            {3004230,new Spec{Kind=0,Text="Your immunize-control increased for 80 seconds. Please seize the chance!"}},
            {3004182,new Spec{Kind=1,Text="Your immunize increased for 80 seconds. Please seize the chance!"}},
            {3004231,new Spec{Kind=2,Text="Your Break increased by 150%for 80 seconds. Please seize the chance!"}},
            {3004232,new Spec{Kind=3,Text="Your P-Strikeis And M-Strikeis increased by 150%for 80 seconds. Please seize the chance!"}},
            {3004233,new Spec{Kind=4,Text="Your Anti-break is increased by 150%for 80 seconds. Please seize the chance!"}},
            {3004234,new Spec{Kind=5,Text="Your Immunity increased by 150%for 80 seconds. Please seize the chance!"}},
            {3004235,new Spec{Kind=6,Text="Your Final-P-Attack increased by 3000%for 80 seconds. Please seize the chance!"}},
            {3004236,new Spec{Kind=7,Text="Your Final-M-Attack increased by 3000%for 80 seconds. Please seize the chance!"}},
            {3004237,new Spec{Kind=8,Text="Your Final-P-Damage increased by 3000%for 80 seconds. Please seize the chance!"}},
            {3004238,new Spec{Kind=9,Text="Your Final-M-Damage increased by 3000%for 80 seconds. Please seize the chance!"}}
        };
        private static bool Handles(uint id){return Catalog.ContainsKey(id);}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_bloodshed_state(
uid bigint PRIMARY KEY CHECK(uid>0),
attr_at timestamptz,shield_at timestamptz,break_at timestamptz,strike_at timestamptz,anti_at timestamptz,
immune_at timestamptz,patk_at timestamptz,matk_at timestamptz,pdef_at timestamptz,mdef_at timestamptz);");
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_bloodshed_boost_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,
item_type integer NOT NULL CHECK(item_type IN(3004230,3004182,3004231,3004232,3004233,3004234,3004235,3004236,3004237,3004238)),
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,
kind integer NOT NULL CHECK(kind BETWEEN 0 AND 9),used_at timestamptz NOT NULL);");
            }enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Hear(GameState p,string text){p.Send(new Message(text,Message.System));var s=Spoken;if(s!=null)s(text);}
        internal static void Project(GameState p,BloodshedSeaBalance.Image image,bool applyStats)
        {
            DateTime now=Clock();
            int shieldLeft=BloodshedSeaBalance.Remaining(image.At[1],now);
            p.Entity.IncreaseAttribute=BloodshedSeaBalance.Remaining(image.At[0],now)>0;
            if(p.Entity.IncreaseAttribute)p.Entity.IncreaseAttributeStamp=Time32.Now;
            p.Entity.GodlyShield=shieldLeft>0;
            if(shieldLeft>0)
            {
                p.Entity.GodlyShieldStamp=Time32.Now;
                EnsureShield(p,StatusFlagsBigVector32.PermanentFlag);
                SendShieldUi(p,shieldLeft);
            }
            else
            {
                int raceLeft=RaceGuardLeft(p);
                if(raceLeft>0)
                    EnsureShield(p,raceLeft);
                else if(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield))
                    RemoveShield(p);
            }
            p.Entity.IncreaseBreack=BloodshedSeaBalance.Remaining(image.At[2],now)>0;
            if(p.Entity.IncreaseBreack)p.Entity.IncreaseBreackStamp=Time32.Now;
            p.Entity.IncreasePStrike=BloodshedSeaBalance.Remaining(image.At[3],now)>0;
            if(p.Entity.IncreasePStrike)p.Entity.IncreasePStrikeStamp=Time32.Now;
            p.Entity.IncreaseAntiBreack=BloodshedSeaBalance.Remaining(image.At[4],now)>0;
            if(p.Entity.IncreaseAntiBreack)p.Entity.IncreaseAntiBreackStamp=Time32.Now;
            p.Entity.IncreaseImunity=BloodshedSeaBalance.Remaining(image.At[5],now)>0;
            if(p.Entity.IncreaseImunity)p.Entity.IncreaseImunityStamp=Time32.Now;
            p.Entity.IncreaseFinalPAttack=BloodshedSeaBalance.Remaining(image.At[6],now)>0;
            if(p.Entity.IncreaseFinalPAttack)p.Entity.IncreaseFinalPAttackStamp=Time32.Now;
            p.Entity.IncreaseFinalMAttack=BloodshedSeaBalance.Remaining(image.At[7],now)>0;
            if(p.Entity.IncreaseFinalMAttack)p.Entity.IncreaseFinalMAttackStamp=Time32.Now;
            p.Entity.IncreaseFinalPDamage=BloodshedSeaBalance.Remaining(image.At[8],now)>0;
            if(p.Entity.IncreaseFinalPDamage)p.Entity.IncreaseFinalPDamageStamp=Time32.Now;
            p.Entity.IncreaseFinalMDamage=BloodshedSeaBalance.Remaining(image.At[9],now)>0;
            if(p.Entity.IncreaseFinalMDamage)p.Entity.IncreaseFinalMDamageStamp=Time32.Now;
            if(applyStats)p.LoadItemStats();
        }
        private static void SendUi(GameState p,int kind)
        {
            if(kind==1)
            {
                return;
            }
            FlagsUpdate upgrade=new FlagsUpdate(true);
            upgrade.UID=p.Entity.UID;
            if(kind==0){upgrade.Append((byte)FlagsUpdate.DataType.IncreaseMaxHp,0,80,3000,0);upgrade.Append((byte)FlagsUpdate.DataType.IncreasePAttack,0,80,3000,0);upgrade.Append((byte)FlagsUpdate.DataType.IncreaseMAttack,0,80,3000,0);}
            else if(kind==2)upgrade.Append((byte)FlagsUpdate.DataType.IncreaseBreack,0,80,150,0);
            else if(kind==3){upgrade.Append((byte)FlagsUpdate.DataType.IncreasePStrike,0,80,150,0);upgrade.Append((byte)FlagsUpdate.DataType.IncreaseMStrike,0,80,150,0);}
            else if(kind==4)upgrade.Append((byte)FlagsUpdate.DataType.IncreaseAntiBreack,0,80,150,0);
            else if(kind==5)upgrade.Append((byte)FlagsUpdate.DataType.IncreaseImunity,0,80,150,0);
            else if(kind==6)upgrade.Append((byte)FlagsUpdate.DataType.IncreaseFinalPAttack,0,80,3000,0);
            else if(kind==7)upgrade.Append((byte)FlagsUpdate.DataType.IncreaseFinalMAttack,0,80,3000,0);
            else if(kind==8)upgrade.Append((byte)FlagsUpdate.DataType.IncreaseFinalPDamage,0,80,3000,0);
            else if(kind==9)upgrade.Append((byte)FlagsUpdate.DataType.IncreaseFinalMDamage,0,80,3000,0);
            p.Send(upgrade.ToArray());
        }
        private static int RaceGuardLeft(GameState p)
        {
            if(p==null || !p.RaceGuard)return 0;
            Time32 left=p.GuardStamp.AddSeconds(10)-Time32.Now;
            if(left.TotalMilliseconds<=0)return 0;
            return Math.Min(10,Math.Max(1,(left.TotalMilliseconds+999)/1000));
        }
        internal static bool RaceGuardLive(GameState p)
        {
            return RaceGuardLeft(p)>0;
        }
        // GuardPotion's 10-second UI must not shorten an active Bloodshed
        // shield. The ordinary no-overlap duration remains exactly 10 seconds.
        internal static int CoordinateRaceGuard(GameState p)
        {
            if(p==null || p.Entity==null)return 10;
            int bloodLeft;
            lock(p.ItemSyncRoot ?? p)bloodLeft=BloodshedSeaBalance.ShieldLeft(p);
            if(bloodLeft<=0)return 10;
            EnsureShield(p,StatusFlagsBigVector32.PermanentFlag);
            return Math.Max(10,bloodLeft);
        }
        private static void EnsureShield(GameState p,int seconds)
        {
            int flag=(int)FlagsUpdate.Flags.GodlyShield;
            if(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield))
                p.Entity.BitVector.UpdateFlag(flag,seconds,true,seconds);
            else p.Entity.AddFlag((ulong)FlagsUpdate.Flags.GodlyShield,seconds,true);
        }
        private static void RemoveShield(GameState p)
        {
            var update=new GameCharacterUpdates(true);
            update.UID=p.Entity.UID;
            update.Remove(GameCharacterUpdates.DivineShield);
            p.SendScreen(update);
            p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.GodlyShield);
        }
        private static void SendShieldUi(GameState p,int seconds)
        {
            var update=new GameCharacterUpdates(true);
            update.UID=p.Entity.UID;
            update.Add(GameCharacterUpdates.DivineShield,0,(uint)Math.Max(1,seconds));
            p.SendScreen(update,true);
        }
        // Production CharactersCallback path. RaceGuard's 10-second leftover
        // expiry still completes when the generic flag loop already removed
        // flag 57, and only leaves the flag absent when no Bloodshed shield remains.
        internal static void ExpireSharedShield(GameState client)
        {
            if(client==null || client.Entity==null)return;
            if(client.Entity.MapID==3846 || !client.RaceGuard || RaceGuardLive(client))return;
            client.RaceGuard=false;
            int bloodLeft;bool bloodPending;
            lock(client.ItemSyncRoot ?? client)
            {
                bloodLeft=BloodshedSeaBalance.ShieldLeft(client);
                bloodPending=BloodshedSeaBalance.ShieldPending(client);
            }
            if(bloodPending)
            {
                if(bloodLeft>0)
                {
                    EnsureShield(client,StatusFlagsBigVector32.PermanentFlag);
                    SendShieldUi(client,bloodLeft);
                }
                return;
            }
            RemoveShield(client);
        }
        internal static void Tick(GameState p)
        {
            if(!enabled || p==null || p.Entity==null || p.Entity.EntityFlag!=Revora.Game.EntityFlag.Player)return;
            lock(p.ItemSyncRoot ?? p)try{
                if(!Q.Player(p,false))return;
                DateTime now=Clock();
                var next=BloodshedSeaBalance.Current(p);
                bool expired=false;
                for(int i=0;i<BloodshedSeaBalance.Kinds;i++)
                    if(next.At[i].HasValue && BloodshedSeaBalance.Remaining(next.At[i],now)==0){next.At[i]=null;expired=true;}
                bool restore=BloodshedSeaBalance.Remaining(next.At[1],now)>0 && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield);
                if(!expired && !restore)return;
                if(expired)
                {
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                        using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                            if(q.ExecuteScalar()==null)throw new InvalidOperationException("Bloodshed Sea owner missing.");
                        BloodshedSeaBalance.Persist(c,t,p,next);
                        t.Commit();
                    }}
                    BloodshedSeaBalance.Committed(p,next);
                }
                Project(p,next,expired);
            }catch(Exception ex){Q.Fail(p,"bloodshed-sea-tick",ex);}
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
                if(p.Entity.MapID!=3846){Hear(p,"This~potion~can~only~be~used~in~the~Bloodshed~Sea!");return true;}
                Spec spec=Catalog[item.ID];
                var next=BloodshedSeaBalance.Current(p);
                next.At[spec.Kind]=BloodshedSeaBalance.GrantStamp(now);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Bloodshed Sea owner missing.");
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Bloodshed Sea inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Bloodshed Sea inventory changed.");
                    BloodshedSeaBalance.AssertSaved(c,t,p);
                    string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Bloodshed Sea ownership changed.");
                    BloodshedSeaBalance.Persist(c,t,p,next);
                    if(Q.Execute(c,t,@"INSERT INTO revora_bloodshed_boost_uses(id,uid,item_uid,item_type,before_stack,bound,kind,used_at)
VALUES(@id,@uid,@item,@type,@stack,@bound,@kind,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"kind",spec.Kind,"now",now)!=1)
                        throw new InvalidOperationException("Bloodshed Sea receipt was not saved.");
                    if(!Ready(p) || p.Entity.MapID!=3846)throw new InvalidOperationException("Bloodshed Sea session changed.");t.Commit();
                }}
                Q.Install(p,item.StackSize<=1?new[]{item.UID}:new uint[0],new ConquerItem[0]);
                if(item.StackSize>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                BloodshedSeaBalance.Committed(p,next);
                Project(p,next,true);
                SendUi(p,spec.Kind);
                p.Entity.Update(10,"eidolon",true);
                Hear(p,"You`ve used an item.");
                Hear(p,spec.Text);
            }catch(Exception ex){Q.Fail(p,"bloodshed-sea",ex);}return true;
        }
    }
}
