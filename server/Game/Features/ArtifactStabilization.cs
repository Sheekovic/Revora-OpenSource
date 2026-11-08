using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class ArtifactStabilization
    {
        internal static bool Eligible(GameState p) {return Q.Player(p) && p.Booth==null && (p.Trade==null || !p.Trade.InTrade);}
        internal static bool Usable(ConquerItem item) {return item!=null && item.StackSize>=1 && item.Warehouse==0 && !item.InWardrobe && !item.Suspicious && item.Days==0;}
        internal static void Own(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item) {
            using(var q=Q.Command(c,t,@"SELECT uid FROM items WHERE uid=@item AND entityid=@owner AND id=@type
AND position=@pos AND warehouse=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(suspicious,0)=0
AND COALESCE(NULLIF(days,''),'0')='0' AND locked=@locked AND bound=@bound AND stacksize=@stack FOR UPDATE",
                "item",(long)item.UID,"owner",(long)p.Entity.UID,"type",(int)item.ID,"pos",(int)item.Position,
                "locked",(int)item.Lock,"bound",item.Bound?1:0,"stack",(int)item.StackSize))
                if(q.ExecuteScalar()==null)throw new InvalidOperationException("Artifact ownership changed.");
        }
        internal static void Apply(GameState p,ItemAddingStabilization request) {
            if(!Eligible(p))return;
            var packet=request.ToArray();
            if(packet==null || packet.Length<24 || packet.Length>184 || request.Mode!=ItemAddingStabilization.Artifact
                || BitConverter.ToUInt16(packet,0)!=packet.Length-8 || BitConverter.ToUInt16(packet,2)!=PacketID.ItemAdding)return;
            uint count=request.PurificationItemCount;
            if(count==0 || count>40 || packet.Length!=24+4*count)return;
            var ids=request.PurificationItems;
            if(ids.Any(id=>id==0 || id==request.ItemUID) || ids.Distinct().Count()!=ids.Count)return;
            lock(p.ItemSyncRoot)try {
                if(!Eligible(p))return;
                ConquerItem item;if(!p.Inventory.TryGetItem(request.ItemUID,out item))item=p.Equipment.TryGetItem(request.ItemUID);
                if(!Usable(item) || item.StackSize!=1)return;
                var stones=new List<ConquerItem>();int points=0;
                foreach(uint id in ids) {
                    ConquerItem stone;
                    if(!p.Inventory.TryGetItem(id,out stone) || !Usable(stone) || stone.Position!=0 || stone.Lock!=0
                        || stone.ID!=723694 && stone.ID!=723695 || stone.Bound && !item.Bound)return;
                    stones.Add(stone);points+=stone.ID==723694?10:100;
                }
                ItemAdding.Purification_ effect=item.Purification;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Artifact owner missing.");
                    foreach(var owned in stones.Concat(new[]{item}).OrderBy(i=>i.UID))Own(c,t,p,owned);
                    using(var q=Q.Command(c,t,"SELECT addingid,addinglevel,duration,addedon FROM itemadding WHERE uid=@uid AND addingtype=0 FOR UPDATE","uid",(long)item.UID))
                    using(var r=q.ExecuteReader()) {
                        if(!r.Read())return;
                        uint soul=Convert.ToUInt32(r.GetValue(0)),level=Convert.ToUInt32(r.GetValue(1)),duration=Convert.ToUInt32(r.GetValue(2));
                        long added=Convert.ToInt64(r.GetValue(3));
                        if(r.Read())throw new InvalidOperationException("Duplicate artifact effect.");
                        if(effect.PurificationItemID!=soul || effect.PurificationLevel!=level || effect.PurificationDuration!=duration || effect.AddedOn.Ticks!=DateTime.FromBinary(added).Ticks)
                            throw new InvalidOperationException("Artifact effect changed.");
                        if(duration==0 || level<1 || level>7 || DateTime.Now>=DateTime.FromBinary(added).AddSeconds(duration))return;
                        if(!ConquerItemInformation.BaseInformations.ContainsKey(soul) || ConquerItemInformation.BaseInformations[soul].PurificationLevel!=level)
                            throw new InvalidOperationException("Unknown artifact definition.");
                        if(points<DataHolder.PurifyStabilizationPoints((byte)level))return;
                    }
                    if(Q.Execute(c,t,"UPDATE itemadding SET duration=0 WHERE uid=@uid AND addingtype=0 AND addingid=@soul AND duration=@duration",
                        "uid",(long)item.UID,"soul",(long)effect.PurificationItemID,"duration",(long)effect.PurificationDuration)!=1)
                        throw new InvalidOperationException("Artifact effect was not stabilized.");
                    foreach(var stone in stones) {
                        string sql=stone.StackSize==1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                        if(Q.Execute(c,t,sql+" WHERE uid=@uid AND entityid=@owner AND stacksize=@stack","uid",(long)stone.UID,"owner",(long)p.Entity.UID,"stack",(int)stone.StackSize)!=1)
                            throw new InvalidOperationException("Artifact stone was not consumed.");
                    }
                    ProgressionEvidence.Complete(c,t,p.Entity.UID,10502,"artifact-stabilize",item.UID+"-"+effect.PurificationItemID,1);
                    t.Commit();
                }}
                Q.Install(p,stones.Where(i=>i.StackSize==1).Select(i=>i.UID),new ConquerItem[0]);
                foreach(var stone in stones.Where(i=>i.StackSize>1)){stone.StackSize--;stone.Mode=Enums.ItemMode.Update;stone.Send(p);}
                effect.PurificationDuration=0;item.Purification=effect;item.Mode=Enums.ItemMode.Update;item.Send(p);
                var update=new ItemAdding(true){Type=ItemAdding.StabilizationEffect};update.Append2(effect);p.Send(update);Q.Refresh(p);
            }catch(Exception ex){Q.Fail(p,"artifact-stabilize",ex);}
        }
    }
}
