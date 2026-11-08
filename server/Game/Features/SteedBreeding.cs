using System;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class SteedBreeding
    {
        private static bool enabled;
        private const uint MaxProgress = 2000000;
        internal static void Initialize()
        {
            if(!ConquerItemInformation.BaseInformations.ContainsKey(300000))throw new InvalidOperationException("Mount catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_steed_breedings(
id uuid PRIMARY KEY,uid bigint NOT NULL REFERENCES entities(uid),main_uid bigint NOT NULL,mode integer NOT NULL CHECK(mode IN(2,3)),
before_level integer NOT NULL,after_level integer NOT NULL CHECK(after_level BETWEEN 0 AND 12),
before_progress bigint NOT NULL,after_progress bigint NOT NULL CHECK(after_progress BETWEEN 0 AND 2000000),
before_color bigint NOT NULL,after_color bigint NOT NULL CHECK(after_color BETWEEN 0 AND 16777215),
created_at timestamptz NOT NULL DEFAULT now());
CREATE INDEX IF NOT EXISTS revora_steed_owner ON revora_steed_breedings(uid);
CREATE TABLE IF NOT EXISTS revora_steed_parents(
breeding uuid NOT NULL REFERENCES revora_steed_breedings(id),item_uid bigint PRIMARY KEY,
ordinal integer NOT NULL,level integer NOT NULL,progress bigint NOT NULL,color bigint NOT NULL,preview_color bigint NOT NULL,bound boolean NOT NULL,
UNIQUE(breeding,ordinal));");t.Commit();}}
            enabled=true;
        }
        private static bool Ready(GameState p)
        {return enabled && Q.Player(p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static bool Usable(ConquerItem i,bool main)
        {return i!=null && i.ID==300000 && (i.Position==0 || main && i.Position==ConquerItem.Steed)
            && i.Warehouse==0 && i.StackSize<=1 && i.Lock==0 && !i.Suspicious && !i.InWardrobe
            && i.Days==0 && i.TimeLeftInMinutes==0 && i.Plus<=12 && i.SocketProgress<=0xffffff;}
        private static ConquerItem Find(GameState p,uint uid)
        {ConquerItem i;if(p.Inventory.TryGetItem(uid,out i))return i;return p.Equipment==null?null:p.Equipment.TryGetItem(uid);}
        private static uint Preview(ConquerItem i){return (uint)(i.NextGreen | i.NextBlue<<8 | i.NextRed<<16);}
        private static bool Saved(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem i)
        {
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@item AND entityid=@uid AND id=300000
AND position=@position AND warehouse=0 AND stacksize=@stack AND bound=@bound AND plus=@plus AND plusprogress=@progress
AND socketprogress=@color AND nextsteedcolor=@preview AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')='0')","item",(long)i.UID,"uid",(long)p.Entity.UID,"position",(int)i.Position,
                "stack",(int)i.StackSize,"bound",i.Bound?1:0,"plus",(int)i.Plus,"progress",(long)i.PlusProgress,"color",(long)i.SocketProgress,"preview",(long)Preview(i)))
                return Equals(q.ExecuteScalar(),true);
        }
        // Called under the owner item lock, including direct ComposePlus callers.
        internal static bool Handle(GameState p,byte[] frame)
        {
            if(frame==null || frame.Length<6)return true;
            bool mountMode=frame[4]==2 || frame[4]==3;
            if(!mountMode)
            {
                if(p!=null && p.Inventory!=null && frame.Length>=16)
                {
                    var selected=Find(p,BitConverter.ToUInt32(frame,8));
                    if(selected!=null && selected.ID==300000){p.Send("Use the mount breeding panel for this steed.");return true;}
                }
                return false;
            }
            SteedBreedingRules.Request request;
            if(!Ready(p) || !SteedBreedingRules.TryRequest(frame,PacketHandler.ClientSeal,out request))return true;
            try
            {
                var main=Find(p,request.Items[0]);if(!Usable(main,true))return true;
                uint threshold;byte level=main.Plus;uint progress=main.PlusProgress,color=main.SocketProgress;
                if(level==12?progress>MaxProgress:!SteedBreedingRules.TryThreshold(level,out threshold) || progress>=threshold)return true;
                var minors=new ConquerItem[request.Items.Length-1];
                for(int n=0;n<minors.Length;n++)
                {
                    ConquerItem minor;uint credit;
                    if(!p.Inventory.TryGetItem(request.Items[n+1],out minor) || !Usable(minor,false)
                        || minor.Bound && !main.Bound || !SteedBreedingRules.TryMinorCredit(minor.Plus,minor.PlusProgress,out credit))return true;
                    minors[n]=minor;progress=checked(progress+credit);
                    while(level<12 && SteedBreedingRules.TryThreshold(level,out threshold) && progress>=threshold){progress-=threshold;level++;}
                    if(level==12)progress=Math.Min(progress,MaxProgress);
                    if(request.Mode==3 && !SteedBreedingRules.TryBlend(color,Preview(minor),out color))return true;
                }
                // Never consume parents for an operation with no remaining effect.
                if(level==main.Plus && progress==main.PlusProgress && color==main.SocketProgress)return true;
                Guid receipt=Guid.NewGuid();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,p);
                    foreach(var i in new[]{main}.Concat(minors))if(!Saved(c,t,p,i))throw new InvalidOperationException("Breeding parent ownership changed.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_steed_breedings(id,uid,main_uid,mode,before_level,after_level,before_progress,after_progress,before_color,after_color)
VALUES(@id,@uid,@item,@mode,@before,@after,@oldprogress,@progress,@oldcolor,@color)","id",receipt,"uid",(long)p.Entity.UID,"item",(long)main.UID,
                        "mode",(int)request.Mode,"before",(int)main.Plus,"after",(int)level,"oldprogress",(long)main.PlusProgress,"progress",(long)progress,
                        "oldcolor",(long)main.SocketProgress,"color",(long)color)!=1)throw new InvalidOperationException("Breeding receipt was not saved.");
                    for(int n=0;n<minors.Length;n++)
                    {
                        var i=minors[n];
                        if(Q.Execute(c,t,@"INSERT INTO revora_steed_parents(breeding,item_uid,ordinal,level,progress,color,preview_color,bound)
VALUES(@id,@item,@n,@plus,@progress,@color,@preview,@bound)","id",receipt,"item",(long)i.UID,"n",n,"plus",(int)i.Plus,"progress",(long)i.PlusProgress,"color",(long)i.SocketProgress,"preview",(long)Preview(i),"bound",i.Bound)!=1)
                            throw new InvalidOperationException("Breeding parent receipt was not saved.");
                        if(Q.Execute(c,t,"DELETE FROM items WHERE uid=@item AND entityid=@uid AND position=0 AND warehouse=0","item",(long)i.UID,"uid",(long)p.Entity.UID)!=1)
                            throw new InvalidOperationException("Breeding parent was not consumed.");
                    }
                    if(Q.Execute(c,t,@"UPDATE items SET plus=@plus,plusprogress=@progress,socketprogress=@color,nextsteedcolor=@color
WHERE uid=@item AND entityid=@uid AND position=@position AND warehouse=0","plus",(int)level,"progress",(long)progress,"color",(long)color,
                        "item",(long)main.UID,"uid",(long)p.Entity.UID,"position",(int)main.Position)!=1)throw new InvalidOperationException("Bred mount was not saved.");
                    ProgressionEvidence.Complete(c,t,p.Entity.UID,10112,"steed-breeding-v1",receipt.ToString("N"),1);
                    if(!Ready(p) || !ReferenceEquals(Find(p,main.UID),main) || !Usable(main,true)
                        || minors.Any(i=>!ReferenceEquals(Find(p,i.UID),i) || !Usable(i,false)))throw new InvalidOperationException("Breeding session changed.");
                    t.Commit();
                }}
                Q.Install(p,minors.Select(i=>i.UID),new ConquerItem[0]);
                main.Plus=level;main.PlusProgress=progress;main.SocketProgress=color;
                main.NextGreen=(byte)color;main.NextBlue=(byte)(color>>8);main.NextRed=(byte)(color>>16);
                // Send the committed item frame directly: legacy Send may write NextSteedColor.
                main.Mode=Enums.ItemMode.Update;p.Send(main.ToArray());main.Mode=Enums.ItemMode.Default;
                if(main.Position==ConquerItem.Steed){p.LoadItemStats();p.Equipment.UpdateEntityPacket();}
                Q.Refresh(p);
            }catch(Exception ex){Q.Fail(p,"steed-breeding",ex);}
            return true;
        }
    }
}
