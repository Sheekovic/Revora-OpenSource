using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using A=Revora.Game.Features.ArtifactStabilization;

namespace Revora.Game.Features
{
    internal static class ArtifactApplication
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static bool Fits(uint target,uint soul) {
            if(!ConquerItemInformation.BaseInformations.ContainsKey(target) || !ConquerItemInformation.BaseInformations.ContainsKey(soul))return false;
            var item=ConquerItemInformation.BaseInformations[target];var material=ConquerItemInformation.BaseInformations[soul];
            int pos=(int)ItemHandler.GetPositionFromID(target);
            if(!(pos>=1 && pos<=6 || pos==8) || material.PurificationLevel<1 || material.PurificationLevel>7 || item.Level<material.Level)return false;
            // Numeric compatibility reviewed against installed catalog descriptions;
            // see ARTIFACT_MODERN_COMPATIBILITY.md for evidence and inference limits.
            uint family=target/1000;
            if(soul>=801000 && soul<=801004)return family==617;
            if(soul>=801100 && soul<=801104)return family==421 || family==619 || family==620;
            if(soul>=801200 && soul<=801218 && soul%2==0)return family==624;
            if(soul>=801300 && soul<=801308 && soul%2==0)return family==626;
            if(!SoulProtection.Soul_Protections.ContainsKey(soul))return false;
            var rule=SoulProtection.Soul_Protections[soul];
            return rule.ItemType>=ConquerItem.AlternateGarment?target/1000==rule.ItemType:
                ItemHandler.IsTwoHand(target)?rule.Pos==ItemHandler.Positions.Left:ItemHandler.GetPositionFromID(target)==rule.Pos;
        }
        internal static void Apply(GameState p,Purification request) {
            if(!A.Eligible(p))return;
            var packet=request.ToArray();
            if(packet==null || packet.Length<24 || packet.Length>4096 || BitConverter.ToUInt16(packet,0)!=packet.Length-8
                || BitConverter.ToUInt16(packet,2)!=2076 || request.Mode!=Purification.Purify || request.ItemUID==request.AddUID)return;
            lock(p.ItemSyncRoot)try {
                ConquerItem item,soul;
                if(!A.Eligible(p) || !p.Inventory.TryGetItem(request.ItemUID,out item) || !p.Inventory.TryGetItem(request.AddUID,out soul)
                    || !A.Usable(item) || !A.Usable(soul) || item.StackSize!=1 || item.Position!=0 || soul.Position!=0 || soul.Lock!=0
                    || soul.Bound && !item.Bound || !Fits(item.ID,soul.ID))return;
                var info=ConquerItemInformation.BaseInformations[soul.ID];
                int remaining=info.PurificationMeteorNeed;var paid=new Dictionary<ConquerItem,int>();paid.Add(soul,1);
                bool changeBound=false;int change=0;
                foreach(uint type in new uint[]{1088001,720027})foreach(var candidate in p.Inventory.Objects.Where(i=>i.ID==type).OrderBy(i=>i.UID)) {
                    if(remaining<=0)break;
                    if(!A.Usable(candidate) || candidate.Position!=0 || candidate.Lock!=0 || candidate.Bound && !item.Bound)continue;
                    int unit=type==1088001?1:10;
                    int amount=Math.Min(candidate.StackSize,(remaining+unit-1)/unit);
                    paid.Add(candidate,amount);remaining-=amount*unit;
                    if(remaining<0){change=-remaining;changeBound=candidate.Bound;}
                }
                if(remaining>0)return;
                int slots=p.Inventory.Count-paid.Count(pair=>pair.Key.StackSize==pair.Value)+change;
                if(slots>40){p.Send(Constants.FullInventory);return;}
                var rewards=Enumerable.Range(0,change).Select(_=>Q.New(1088001,changeBound)).ToArray();
                var now=Clock().ToLocalTime();
                // Preserve the installed artifact lifetime schedule, including VIP levels above six.
                uint duration=(uint)(p.Entity.VIPLevel==1?8:p.Entity.VIPLevel==2?9:p.Entity.VIPLevel==3?11:p.Entity.VIPLevel>=4 && p.Entity.VIPLevel<=6?14:7)*86400;
                var next=new ItemAdding.Purification_{Available=true,ItemUID=item.UID,PurificationItemID=soul.ID,
                    PurificationLevel=info.PurificationLevel,PurificationDuration=duration,AddedOn=now};
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    foreach(var owned in paid.Keys.Concat(new[]{item}).OrderBy(i=>i.UID))A.Own(c,t,p,owned);
                    var bag=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read())bag.Add(checked(Convert.ToUInt32(r.GetValue(0))));
                    if(!bag.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Artifact inventory changed.");
                    int previous=0;
                    using(var q=Q.Command(c,t,"SELECT addingid,addinglevel,duration,addedon FROM itemadding WHERE uid=@uid AND addingtype=0 FOR UPDATE","uid",(long)item.UID))
                    using(var r=q.ExecuteReader()) {
                        if(r.Read()) {
                            previous=1;var old=item.Purification;
                            uint id=Convert.ToUInt32(r.GetValue(0)),level=Convert.ToUInt32(r.GetValue(1)),seconds=Convert.ToUInt32(r.GetValue(2));
                            DateTime added=DateTime.FromBinary(Convert.ToInt64(r.GetValue(3)));
                            if(r.Read())throw new InvalidOperationException("Duplicate saved artifact.");
                            if(old.PurificationItemID!=id || old.PurificationLevel!=level || old.PurificationDuration!=seconds || old.AddedOn.Ticks!=added.Ticks)
                                throw new InvalidOperationException("Artifact preimage changed.");
                            if(id==soul.ID && now<added.AddSeconds(1))return;
                        }else if(item.Purification.PurificationItemID!=0)throw new InvalidOperationException("Saved artifact missing.");
                    }
                    if(Q.Execute(c,t,"DELETE FROM itemadding WHERE uid=@uid AND addingtype=0","uid",(long)item.UID)!=previous)
                        throw new InvalidOperationException("Previous artifact was not removed.");
                    if(Q.Execute(c,t,@"INSERT INTO itemadding(uid,addingtype,addingid,addinglevel,addingpercent,duration,addedon)
VALUES(@uid,0,@soul,@level,0,@duration,@added)","uid",(long)item.UID,"soul",(long)soul.ID,"level",(int)info.PurificationLevel,
                        "duration",(long)duration,"added",now.Ticks)!=1)throw new InvalidOperationException("Artifact was not saved.");
                    foreach(var pair in paid) {
                        string sql=pair.Key.StackSize==pair.Value?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-@amount";
                        if(Q.Execute(c,t,sql+" WHERE uid=@uid AND entityid=@owner AND stacksize=@stack","uid",(long)pair.Key.UID,"owner",(long)p.Entity.UID,
                            "stack",(int)pair.Key.StackSize,"amount",pair.Value)!=1)throw new InvalidOperationException("Artifact payment changed.");
                    }
                    foreach(var reward in rewards)Q.Insert(c,t,p.Entity.UID,reward);
                    ProgressionEvidence.Complete(c,t,p.Entity.UID,10501,"artifact-apply",item.UID+"-"+soul.UID,1);
                    t.Commit();
                }}
                Q.Install(p,paid.Where(pair=>pair.Key.StackSize==pair.Value).Select(pair=>pair.Key.UID),rewards);
                foreach(var pair in paid.Where(pair=>pair.Key.StackSize>pair.Value)){pair.Key.StackSize-=(ushort)pair.Value;pair.Key.Mode=Enums.ItemMode.Update;pair.Key.Send(p);}
                item.Purification=next;item.Mode=Enums.ItemMode.Update;item.Send(p);
                var update=new ItemAdding(true){Type=ItemAdding.PurificationEffect};update.Append2(next);p.Send(update);
                p.CalculateStatBonus();p.LoadItemStats();p.CalculateHPBonus();p.Send(request);Q.Refresh(p);
            }catch(Exception ex){Q.Fail(p,"artifact-apply",ex);}
        }
    }
}
