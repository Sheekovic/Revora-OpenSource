using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class RuneLoadout
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private sealed class Recent {internal readonly Dictionary<uint,DateTime> Moves=new Dictionary<uint,DateTime>();}
        private static readonly ConditionalWeakTable<GameState,Recent> recent=new ConditionalWeakTable<GameState,Recent>();
        private sealed class Change {internal ConquerItem Item;internal byte Before,After;}
        private static bool Eligible(GameState p) {
            return Q.Player(p) && p.RunesItem!=null && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;
        }
        private static bool Valid(ConquerItem item) {
            if(item==null || !RuneProgress.IsRune(item.ID) || item.Warehouse!=0 || item.StackSize>1
                || !ConquerItemInformation.BaseInformations.ContainsKey(item.ID))return false;
            uint level=item.ID%100;return level>=1 && level<=(item.ID/10000==401?1:item.ID/10000==402?27:9);
        }
        private static void Write(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Change a) {
            var i=a.Item;
            if(Q.Execute(c,t,@"UPDATE items SET position=@after WHERE uid=@item AND entityid=@uid AND id=@type
AND position=@before AND warehouse=0 AND stacksize=@stack AND bound=@bound AND runesexp=@exp",
                "after",(int)a.After,"item",(long)i.UID,"uid",(long)p.Entity.UID,"type",(int)i.ID,
                "before",(int)a.Before,"stack",(int)i.StackSize,"bound",i.Bound?1:0,"exp",(long)i.RunesEXP)!=1)
                throw new InvalidOperationException("Rune loadout ownership changed.");
        }
        private static bool Evidence(NpgsqlConnection c,NpgsqlTransaction t,GameState p,IEnumerable<Change> changes,ConquerItem upgraded=null,uint nextType=0,uint nextExp=0,bool nextBound=false) {
            // Read the post-change persisted equipment. Every counted row must also be the
            // session's exact owned item, including position, level, binding and progress.
            var planned=changes.ToDictionary(a=>a.Item.UID,a=>a.After);
            var primary=new HashSet<int>();var alternate=new HashSet<int>();
            var equipment=new List<ConquerItem>();
            VerifyProfile(c,t,p);
            using(var q=Q.Command(c,t,@"SELECT uid,id,position,stacksize,bound,runesexp FROM items WHERE entityid=@uid
AND warehouse=0 AND (position BETWEEN 101 AND 108 OR position BETWEEN 121 AND 125) FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()) {
                uint uid=Convert.ToUInt32(r.GetValue(0));ConquerItem item;byte after;
                if(!p.RunesItem.TryGetValue(uid,out item) || item.UID!=uid || !Valid(item))throw new InvalidOperationException("Rune equipment cache requires reload.");
                int pos=planned.TryGetValue(uid,out after)?after:item.Position;
                bool upgrading=ReferenceEquals(item,upgraded);uint type=upgrading?nextType:item.ID,exp=upgrading?nextExp:item.RunesEXP;bool bound=upgrading?nextBound:item.Bound;
                if(type!=Convert.ToUInt32(r.GetValue(1)) || pos!=Convert.ToInt32(r.GetValue(2))
                    || item.StackSize!=Convert.ToUInt16(r.GetValue(3)) || bound!=(Convert.ToInt32(r.GetValue(4))!=0)
                    || exp!=Convert.ToUInt32(r.GetValue(5)) || !RuneProgress.ValidSlot(type,(uint)pos))
                    throw new InvalidOperationException("Rune equipment preimage changed.");
                if(!(pos<120?primary:alternate).Add(pos))throw new InvalidOperationException("Duplicate persisted rune slot.");
                equipment.Add(new ConquerItem(true){UID=uid,ID=type,Position=(byte)pos,StackSize=item.StackSize});
            }
            var legal=RuneProgress.LegalRunes(equipment,p.Entity.Class,p.Entity.Level,p.Entity.Reborn);
            primary=new HashSet<int>(legal.Where(i=>i.Position<120).Select(i=>(int)i.Position));
            alternate=new HashSet<int>(legal.Where(i=>i.Position>=120).Select(i=>(int)i.Position));
            int primaryMax=legal.Count(i=>i.Position<120 && i.ID%100==(i.ID/10000==401?1u:i.ID/10000==402?27u:9u));
            int alternateMax=legal.Count(i=>i.Position>=120 && i.ID%100==(i.ID/10000==401?1u:i.ID/10000==402?27u:9u));
            if(upgraded==null && primary.Count+alternate.Count>=1)ProgressionEvidence.Complete(c,t,p.Entity.UID,11310,"rune-loadout-v1","equip-one",1);
            if(upgraded==null && (primary.Count>=5 || alternate.Count>=5))ProgressionEvidence.Complete(c,t,p.Entity.UID,11311,"rune-loadout-v1","equip-five",1);
            if(primaryMax<5 && alternateMax<5)return false;
            if(!Eligible(p))throw new InvalidOperationException("Rune achievement session changed.");
            RecordClass(c,t,p);return true;
        }
        internal static void VerifyProfile(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            using(var q=Q.Command(c,t,"SELECT class,level,reborn FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r.GetValue(0))!=p.Entity.Class || Convert.ToInt32(r.GetValue(1))!=p.Entity.Level
                    || Convert.ToInt32(r.GetValue(2))!=p.Entity.Reborn)
                    throw new InvalidOperationException("Rune equipment profile changed.");
        }
        internal static bool RecordUpgrade(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem target,uint nextType,uint nextExp,bool nextBound) {
            return Evidence(c,t,p,new Change[0],target,nextType,nextExp,nextBound);
        }
        private static void RecordClass(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            int profession=p.Entity.Class,id=0;bool inserted=false;
            if(profession>=10 && profession<=15) {id=11312;inserted=ProgressionEvidence.Complete(c,t,p.Entity.UID,11312,"rune-full-level-v1","equip-five-max",1);}
            if(profession>=20 && profession<=25) {id=11313;inserted=ProgressionEvidence.Complete(c,t,p.Entity.UID,11313,"rune-full-level-v1","equip-five-max",1);}
            if(profession>=140 && profession<=145) {id=11314;inserted=ProgressionEvidence.Complete(c,t,p.Entity.UID,11314,"rune-full-level-v1","equip-five-max",1);}
            if(profession>=130 && profession<=135) {id=11315;inserted=ProgressionEvidence.Complete(c,t,p.Entity.UID,11315,"rune-full-level-v1","equip-five-max",1);}
            if(profession>=40 && profession<=45) {id=11316;inserted=ProgressionEvidence.Complete(c,t,p.Entity.UID,11316,"rune-full-level-v1","equip-five-max",1);}
            if(profession>=50 && profession<=55) {id=11317;inserted=ProgressionEvidence.Complete(c,t,p.Entity.UID,11317,"rune-full-level-v1","equip-five-max",1);}
            if(profession>=60 && profession<=65) {id=11318;inserted=ProgressionEvidence.Complete(c,t,p.Entity.UID,11318,"rune-full-level-v1","equip-five-max",1);}
            if(profession>=80 && profession<=85) {id=11319;inserted=ProgressionEvidence.Complete(c,t,p.Entity.UID,11319,"rune-full-level-v1","equip-five-max",1);}
            if(profession>=70 && profession<=75) {id=11320;inserted=ProgressionEvidence.Complete(c,t,p.Entity.UID,11320,"rune-full-level-v1","equip-five-max",1);}
            if(profession>=160 && profession<=165) {id=11321;inserted=ProgressionEvidence.Complete(c,t,p.Entity.UID,11321,"rune-full-level-v1","equip-five-max",1);}
            if(id!=0 && !inserted)using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id)","uid",(long)p.Entity.UID,"id",id))
                if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Full-level rune receipt not saved.");
        }
        internal static void Process(GameState p,byte[] packet) {
            if(packet==null || packet.Length<38 || packet.Length>128 || BitConverter.ToUInt16(packet,0)!=packet.Length-8
                || BitConverter.ToUInt16(packet,2)!=1009 || !Eligible(p))return;
            ushort action=BitConverter.ToUInt16(packet,20);if(action!=57 && action!=58 && action!=61)return;
            uint uid=BitConverter.ToUInt32(packet,8),destination=BitConverter.ToUInt32(packet,12);
            lock(p.ItemSyncRoot)try {
                if(!Eligible(p))return;
                var history=recent.GetValue(p,_=>new Recent());var now=Clock();
                foreach(var old in history.Moves.Where(a=>a.Value<=now).Select(a=>a.Key).ToArray())history.Moves.Remove(old);
                // Action 61 toggles bags and has no proven nonce. A duplicate must not
                // immediately undo the move; a deliberate reverse is allowed one second later.
                if(action==61 && history.Moves.ContainsKey(uid))return;
                ConquerItem item;bool inventory=p.Inventory.TryGetItem(uid,out item);
                if(!inventory && !p.RunesItem.TryGetValue(uid,out item))return;
                if(item.UID!=uid || !Valid(item) || inventory && (item.Position!=0 || p.RunesItem.ContainsKey(uid)))return;
                var changes=new List<Change>();byte target;
                if(action==57) {
                    if(inventory || item.Position!=211 && !RuneProgress.Equipped(item) || !RuneProgress.ValidSlot(item.ID,destination))return;
                    if(!RuneProgress.CanUse(p,item.ID) || !RuneProgress.UsableSlot(item.ID,destination))return;
                    target=(byte)destination;if(item.Position==target)return;
                    if(p.RunesItem.Values.Any(i=>i.UID!=uid && i.Position!=target && RuneProgress.Equipped(i)
                        && (i.Position>=121)==(target>=121) && i.ID/100==item.ID/100))return;
                    var displaced=p.RunesItem.Values.Where(i=>i.UID!=uid && i.Position==target).ToArray();
                    if(displaced.Length>1 || displaced.Any(i=>!Valid(i) || !RuneProgress.Equipped(i)))return;
                    foreach(var other in displaced)changes.Add(new Change{Item=other,Before=other.Position,After=211});
                }else if(action==58) {
                    if(inventory || !RuneProgress.Equipped(item))return;target=211;
                }else {
                    if(!inventory && (item.Position!=211 || p.Inventory.Count>=40))return;
                    target=inventory?(byte)211:(byte)0;
                }
                changes.Add(new Change{Item=item,Before=item.Position,After=target});
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Rune owner missing.");
                    foreach(var a in changes.OrderBy(a=>a.Item.UID))Write(c,t,p,a);
                    if(action==57)Evidence(c,t,p,changes);
                    if(!Eligible(p))throw new InvalidOperationException("Rune session changed before commit.");
                    t.Commit();
                }}
                if(action==61) {
                    if(history.Moves.Count>=64)history.Moves.Remove(history.Moves.OrderBy(a=>a.Value).First().Key);
                    history.Moves[uid]=now.AddSeconds(1);
                    if(inventory) {
                        if(!p.Inventory.Remove(uid,Enums.ItemUse.None,true))throw new InvalidOperationException("Committed rune removal requires reload.");
                        p.RunesItem.Add(uid,item);
                    }else {
                        p.RunesItem.Remove(uid);
                        if(!p.Inventory.Add(item,Enums.ItemUse.None))throw new InvalidOperationException("Committed rune inventory requires reload.");
                    }
                }
                foreach(var a in changes){a.Item.Position=a.After;a.Item.Mode=Enums.ItemMode.Update;a.Item.Send(p);a.Item.Mode=Enums.ItemMode.Default;}
                p.Send(packet);
                if(action!=61){RuneProgress.RefreshSkills(p);p.LoadItemStats();PrestigeRanking.SendSelf(p);Q.Refresh(p);}
            }catch(Exception ex){Q.Fail(p,"rune-loadout",ex);}
        }
    }
}
