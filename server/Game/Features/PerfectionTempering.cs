using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class PerfectionTempering
    {
        internal static Action<GameState> BeforeCommit, BeforeInstall;
        private static bool Ready(GameState p) { return Q.Player(p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0; }
        // Legacy equipment uses StackSize=0 for one item. A target's trade lock
        // does not prohibit improving it in place; consumed materials stay unlocked.
        private static bool Usable(ConquerItem i) {return i!=null && i.StackSize<=1 && i.Warehouse==0
            && !i.InWardrobe && !i.Suspicious && i.Days==0 && i.TimeLeftInMinutes==0;}
        private static uint Value(ConquerItem stone,ConquerItem target) {
            ConquerItemBaseInformation minor,major;
            // Family includes equipment kind and class; level/quality may differ.
            if(stone.ID/1000==target.ID/1000 && stone.Plus<=8
                && ConquerItemInformation.BaseInformations.TryGetValue(stone.ID,out minor)
                && ConquerItemInformation.BaseInformations.TryGetValue(target.ID,out major)
                && minor.Class==major.Class && ItemHandler.IsEquipment(stone.ID))
                return DataHolder.StonePlusPoints(stone.Plus);
            if(stone.ID>=730001 && stone.ID<=730008)
                return stone.Plus==stone.ID-730000?DataHolder.StonePlusPoints(stone.Plus):0;
            switch(stone.ID) {case 3009000:return 10;case 3009001:return 100;case 3009002:return 1000;case 3009003:return 10000;default:return 0;}
        }
        private static bool MaterialUsable(ConquerItem stone,ConquerItem target) {
            return Usable(stone) && stone.Position==0 && stone.Lock==0
                && Value(stone,target)>0 && (!stone.Bound || target.Bound);
        }
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_perfection_exchanges(
id uuid PRIMARY KEY,uid bigint NOT NULL,first_item bigint NOT NULL,second_item bigint NOT NULL,
cost bigint NOT NULL CHECK(cost=100000),exchanged_at timestamptz NOT NULL,CHECK(first_item<second_item));
CREATE INDEX IF NOT EXISTS revora_perfection_exchange_recent ON revora_perfection_exchanges(uid,first_item,second_item,exchanged_at);
CREATE TABLE IF NOT EXISTS revora_perfection_boosts(
id uuid PRIMARY KEY,uid bigint NOT NULL,item bigint NOT NULL,cost bigint NOT NULL CHECK(cost>0 AND cost%8=0),
bound_cost bigint NOT NULL CHECK(bound_cost>=0 AND bound_cost<=cost),level_before integer NOT NULL,progress_before bigint NOT NULL,
level_after integer NOT NULL,progress_after bigint NOT NULL,purchased_at timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS revora_perfection_boost_recent ON revora_perfection_boosts(uid,item,purchased_at);");}
        }
        private static bool Find(GameState p,uint uid,out ConquerItem item){return p.Equipment.TryGetItem(uid,out item) || p.Inventory.TryGetItem(uid,out item);}
        private static bool SamePerfection(ConquerItem a,ConquerItem b){return a.PerfectionLevel==b.PerfectionLevel && a.PerfectionProgress==b.PerfectionProgress
            && a.PerfectionOwnerGuid==b.PerfectionOwnerGuid && (a.PerfectionOwner??"")== (b.PerfectionOwner??"");}
        private static bool ExchangeGear(GameState p,ConquerItem item){
            if(!Usable(item) || item.StackSize>1 || item.PerfectionLevel>54 || item.PerfectionOwnerGuid!=0 && item.PerfectionOwnerGuid!=p.Entity.UID
                || !ConquerItemInformation.BaseInformations.ContainsKey(item.ID))return false;
            int slot=(int)ItemHandler.GetPositionFromID(item.ID);
            return slot>=1 && slot<=6 || slot==8 || slot==10 || slot==11 || slot==12 || slot==18 || slot==19;
        }
        internal static void Exchange(GameState p,uint target,uint[] selected){
            const uint fee=100000;
            if(!Ready(p) || selected==null || selected.Length!=1 || selected[0]==target || !SessionInstall.TryEnter(p.Entity.UID))return;
            uint other=selected[0];
            try{lock(p.ItemSyncRoot){
                ConquerItem a,b;
                if(!Ready(p) || !Find(p,target,out a) || !Find(p,other,out b) || !ExchangeGear(p,a) || !ExchangeGear(p,b)
                    || a.Bound!=b.Bound || ItemHandler.GetPositionFromID(a.ID)!=ItemHandler.GetPositionFromID(b.ID)
                    || (int)ItemHandler.GetPositionFromID(a.ID)==4 && a.ID/1000!=b.ID/1000
                    || a.PerfectionLevel==b.PerfectionLevel && a.PerfectionProgress==b.PerfectionProgress)return;
                if(p.Entity.RevoraPoint<fee){p.Send("You need 100,000 RVPs to exchange Perfection.");return;}
                var originals=new[]{a.DeepCopy(),b.DeepCopy()};var items=new[]{a,b};uint before=p.Entity.RevoraPoint;
                uint low=Math.Min(a.UID,b.UID),high=Math.Max(a.UID,b.UID);DateTime now=Clock();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");SkillTable.LockOwner(c,t,p);
                    foreach(var item in items.OrderBy(i=>i.UID))ArtifactStabilization.Own(c,t,p,item);
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_perfection_exchanges WHERE uid=@uid AND first_item=@first AND second_item=@second AND exchanged_at>@now-interval '1 second')",
                        "uid",(long)p.Entity.UID,"first",(long)low,"second",(long)high,"now",now))if(Equals(q.ExecuteScalar(),true))return;
                    for(int index=0;index<2;index++){
                        var old=originals[index];var next=originals[1-index];bool owned=next.PerfectionLevel!=0 || next.PerfectionProgress!=0;
                        if(Q.Execute(c,t,@"UPDATE items SET perfectionlevel=@level,perfectionprogress=@progress,perfectionownerguid=@newOwner,perfectionowner=@newName
WHERE uid=@item AND entityid=@uid AND COALESCE(perfectionlevel,0)=@oldLevel AND COALESCE(perfectionprogress,0)=@oldProgress
AND COALESCE(perfectionownerguid,0)=@oldOwner AND COALESCE(perfectionowner,'')=@oldName",
                            "level",(long)next.PerfectionLevel,"progress",(long)next.PerfectionProgress,"newOwner",owned?(long)p.Entity.UID:0L,"newName",owned?p.Entity.Name:"",
                            "item",(long)items[index].UID,"uid",(long)p.Entity.UID,"oldLevel",(long)old.PerfectionLevel,"oldProgress",(long)old.PerfectionProgress,
                            "oldOwner",(long)old.PerfectionOwnerGuid,"oldName",old.PerfectionOwner??"")!=1)throw new InvalidOperationException("Perfection exchange item changed.");
                    }
                    if(Q.Execute(c,t,"UPDATE entities SET revorapoint=@after WHERE uid=@uid AND COALESCE(revorapoint,0)=@before",
                        "after",(long)(before-fee),"uid",(long)p.Entity.UID,"before",(long)before)!=1)throw new InvalidOperationException("Perfection exchange balance changed.");
                    if(Q.Execute(c,t,"INSERT INTO revora_perfection_exchanges(id,uid,first_item,second_item,cost,exchanged_at) VALUES(@id,@uid,@first,@second,@cost,@now)",
                        "id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"first",(long)low,"second",(long)high,"cost",(long)fee,"now",now)!=1)throw new InvalidOperationException("Perfection exchange receipt missing.");
                    var hook=BeforeCommit;if(hook!=null)hook(p);
                    ConquerItem current;
                    if(!Ready(p) || p.Entity.RevoraPoint!=before || !Find(p,target,out current) || !ReferenceEquals(current,a)
                        || !Find(p,other,out current) || !ReferenceEquals(current,b) || !ExchangeGear(p,a) || !ExchangeGear(p,b)
                        || !SamePerfection(a,originals[0]) || !SamePerfection(b,originals[1]))throw new InvalidOperationException("Perfection exchange session changed.");
                    t.Commit();
                }}
                var install=BeforeInstall;if(install!=null)install(p);p.Entity.ApplyCommittedRevoraPoints(before-fee);
                for(int index=0;index<2;index++){
                    var item=items[index];var next=originals[1-index];bool owned=next.PerfectionLevel!=0 || next.PerfectionProgress!=0;
                    item.PerfectionLevel=next.PerfectionLevel;item.PerfectionProgress=next.PerfectionProgress;item.PerfectionOwnerGuid=owned?p.Entity.UID:0;item.PerfectionOwner=owned?p.Entity.Name:"";
                    item.Mode=Enums.ItemMode.Update;item.Send(p);p.Send(new CMsgItemRefine(item,p));
                }
                p.LoadItemStats();new MsgUserTotalRefineLev().Send(p);Q.Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"perfection-exchange",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void Transfer(GameState p,uint target) {
            const uint fee=100000;
            if(!Ready(p) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot){
                ConquerItem item;
                if(!Ready(p) || (!p.Equipment.TryGetItem(target,out item) && !p.Inventory.TryGetItem(target,out item))
                    || !Usable(item) || item.StackSize>1 || !ItemHandler.IsEquipment(item.ID)
                    || item.PerfectionLevel==0 || item.PerfectionLevel>54 || item.PerfectionOwnerGuid==0
                    || item.PerfectionOwnerGuid==p.Entity.UID)return;
                if(p.Entity.RevoraPoint<fee){p.Send("You need 100,000 RVPs to transfer Perfection ownership.");return;}
                uint before=p.Entity.RevoraPoint,owner=item.PerfectionOwnerGuid,level=item.PerfectionLevel,progress=item.PerfectionProgress;
                string name=item.PerfectionOwner??"";
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");SkillTable.LockOwner(c,t,p);ArtifactStabilization.Own(c,t,p,item);
                    if(Q.Execute(c,t,"UPDATE entities SET revorapoint=@after WHERE uid=@uid AND COALESCE(revorapoint,0)=@before",
                        "after",(long)(before-fee),"uid",(long)p.Entity.UID,"before",(long)before)!=1)throw new InvalidOperationException("Perfection transfer balance changed.");
                    if(Q.Execute(c,t,@"UPDATE items SET perfectionownerguid=@owner,perfectionowner=@name
WHERE uid=@item AND entityid=@owner AND COALESCE(perfectionownerguid,0)=@oldOwner AND COALESCE(perfectionowner,'')=@oldName
AND COALESCE(perfectionlevel,0)=@level AND COALESCE(perfectionprogress,0)=@progress",
                        "owner",(long)p.Entity.UID,"name",p.Entity.Name,"item",(long)item.UID,"oldOwner",(long)owner,"oldName",name,
                        "level",(long)level,"progress",(long)progress)!=1)throw new InvalidOperationException("Perfection transfer item changed.");
                    var hook=BeforeCommit;if(hook!=null)hook(p);
                    ConquerItem current;
                    if(!Ready(p) || p.Entity.RevoraPoint!=before || !Usable(item) || item.PerfectionOwnerGuid!=owner
                        || (item.PerfectionOwner??"")!=name || item.PerfectionLevel!=level || item.PerfectionProgress!=progress
                        || (!p.Equipment.TryGetItem(target,out current) && !p.Inventory.TryGetItem(target,out current)) || !ReferenceEquals(current,item))
                        throw new InvalidOperationException("Perfection transfer session changed.");
                    t.Commit();
                }}
                var install=BeforeInstall;if(install!=null)install(p);
                p.Entity.ApplyCommittedRevoraPoints(before-fee);item.PerfectionOwnerGuid=p.Entity.UID;item.PerfectionOwner=p.Entity.Name;
                item.Mode=Enums.ItemMode.Update;item.Send(p);p.Send(new CMsgItemRefine(item,p));Q.Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"perfection-transfer",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void Boost(GameState p,uint target,uint[] amounts) {
            if(!Ready(p) || amounts==null || amounts.Length!=1 || amounts[0]==0 || amounts[0]%8!=0 || !SessionInstall.TryEnter(p.Entity.UID))return;
            uint fee=amounts[0];
            try{lock(p.ItemSyncRoot){
                ConquerItem item;
                if(!Ready(p) || !p.Equipment.TryGetItem(target,out item) || !ExchangeGear(p,item) || item.PerfectionLevel>=54)return;
                var old=item.DeepCopy();var proposed=item.DeepCopy();var costs=new CMsgItemRefineOpt(null);bool wasBound=item.Bound;
                ulong remaining=0;
                for(uint step=old.PerfectionLevel;step<54;step++){
                    proposed.PerfectionLevel=step;uint cost=costs.GetPerfectionProgress(proposed);
                    if(cost==0)throw new InvalidOperationException("Boost cost missing.");remaining+=cost;
                }
                if(old.PerfectionProgress>=costs.GetPerfectionProgress(old) || remaining<=old.PerfectionProgress)return;
                remaining-=old.PerfectionProgress;
                if(fee>((remaining+9)/10)*8)return;
                uint cp=p.Entity.ConquerPoints,bound=p.Entity.BoundCps;
                uint boundFee=wasBound?Math.Min(bound,fee):0,cpFee=fee-boundFee;
                if(cp<cpFee)return;
                ulong points=(ulong)old.PerfectionProgress+(ulong)(fee/8)*10;uint level=old.PerfectionLevel;
                while(level<54){proposed.PerfectionLevel=level;uint cost=costs.GetPerfectionProgress(proposed);
                    if(points<cost)break;points-=cost;level++;}
                uint progress=level==54?0:checked((uint)points);DateTime now=Clock();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");SkillTable.LockOwner(c,t,p);ArtifactStabilization.Own(c,t,p,item);
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_perfection_boosts WHERE uid=@uid AND item=@item AND purchased_at>@now-interval '1 second')",
                        "uid",(long)p.Entity.UID,"item",(long)target,"now",now))if(Equals(q.ExecuteScalar(),true))return;
                    if(Q.Execute(c,t,@"UPDATE items SET perfectionlevel=@level,perfectionprogress=@progress,perfectionownerguid=@owner,perfectionowner=@name
WHERE uid=@item AND entityid=@owner AND COALESCE(perfectionlevel,0)=@oldLevel AND COALESCE(perfectionprogress,0)=@oldProgress
AND COALESCE(perfectionownerguid,0)=@oldOwner AND COALESCE(perfectionowner,'')=@oldName",
                        "level",(long)level,"progress",(long)progress,"owner",(long)p.Entity.UID,"name",p.Entity.Name,"item",(long)target,
                        "oldLevel",(long)old.PerfectionLevel,"oldProgress",(long)old.PerfectionProgress,"oldOwner",(long)old.PerfectionOwnerGuid,"oldName",old.PerfectionOwner??"")!=1)
                        throw new InvalidOperationException("Boost item changed.");
                    if(Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cpAfter,boundcps=@boundAfter WHERE uid=@uid
AND COALESCE(conquerpoints,0)=@cpBefore AND COALESCE(boundcps,0)=@boundBefore",
                        "cpAfter",(long)(cp-cpFee),"boundAfter",(long)(bound-boundFee),"uid",(long)p.Entity.UID,
                        "cpBefore",(long)cp,"boundBefore",(long)bound)!=1)throw new InvalidOperationException("Boost wallet changed.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_perfection_boosts(id,uid,item,cost,bound_cost,level_before,progress_before,level_after,progress_after,purchased_at)
VALUES(@id,@uid,@item,@cost,@bound,@beforeLevel,@beforeProgress,@level,@progress,@now)",
                        "id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)target,"cost",(long)fee,"bound",(long)boundFee,
                        "beforeLevel",(int)old.PerfectionLevel,"beforeProgress",(long)old.PerfectionProgress,"level",(int)level,"progress",(long)progress,"now",now)!=1)
                        throw new InvalidOperationException("Boost receipt missing.");
                    var hook=BeforeCommit;if(hook!=null)hook(p);ConquerItem current;
                    if(!Ready(p) || !p.Equipment.TryGetItem(target,out current) || !ReferenceEquals(current,item) || !ExchangeGear(p,item)
                        || !SamePerfection(item,old) || item.Bound!=wasBound || p.Entity.ConquerPoints!=cp || p.Entity.BoundCps!=bound)
                        throw new InvalidOperationException("Boost session changed.");
                    t.Commit();
                }}
                var install=BeforeInstall;if(install!=null)install(p);
                p.Entity.ApplyCommittedConquerPoints(cp-cpFee);p.Entity.ApplyCommittedBoundCps(bound-boundFee);
                item.PerfectionLevel=level;item.PerfectionProgress=progress;item.PerfectionOwnerGuid=p.Entity.UID;item.PerfectionOwner=p.Entity.Name;
                item.Mode=Enums.ItemMode.Update;item.Send(p);p.Send(new CMsgItemRefine(item,p));p.LoadItemStats();new MsgUserTotalRefineLev().Send(p);Q.Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"perfection-boost",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static Func<double,bool> QuickenChance=percent=>Kernel.ChanceSuccess(percent);
        internal static void Quicken(GameState p,uint target) {
            if(!Ready(p) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot){
                ConquerItem item;
                if(!Ready(p) || !p.Equipment.TryGetItem(target,out item) || !ExchangeGear(p,item)
                    || item.PerfectionOwnerGuid!=p.Entity.UID || item.PerfectionLevel<10 || item.PerfectionLevel>=54 || item.PerfectionProgress==0)return;
                var old=item.DeepCopy();uint cost=new CMsgItemRefineOpt(null).GetPerfectionProgress(item),level=old.PerfectionLevel;
                if(cost==0)return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");SkillTable.LockOwner(c,t,p);ArtifactStabilization.Own(c,t,p,item);
                    using(var q=Q.Command(c,t,@"SELECT uid FROM items WHERE uid=@item AND entityid=@owner
AND COALESCE(perfectionlevel,0)=@level AND COALESCE(perfectionprogress,0)=@progress
AND COALESCE(perfectionownerguid,0)=@owner AND COALESCE(perfectionowner,'')=@name",
                        "item",(long)target,"owner",(long)p.Entity.UID,"level",(long)old.PerfectionLevel,
                        "progress",(long)old.PerfectionProgress,"name",old.PerfectionOwner??""))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Quicken state changed.");
                    if(QuickenChance(Math.Min(100d,100d*old.PerfectionProgress/cost)))level++;
                    if(Q.Execute(c,t,"UPDATE items SET perfectionlevel=@level,perfectionprogress=0 WHERE uid=@item AND entityid=@owner",
                        "level",(long)level,"item",(long)target,"owner",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("Quicken result was not saved.");
                    var hook=BeforeCommit;if(hook!=null)hook(p);ConquerItem current;
                    if(!Ready(p) || !p.Equipment.TryGetItem(target,out current) || !ReferenceEquals(current,item)
                        || !ExchangeGear(p,item) || !SamePerfection(item,old))throw new InvalidOperationException("Quicken session changed.");
                    t.Commit();
                }}
                var install=BeforeInstall;if(install!=null)install(p);
                item.PerfectionLevel=level;item.PerfectionProgress=0;item.Mode=Enums.ItemMode.Update;item.Send(p);
                p.Send(new CMsgItemRefine(item,p));p.LoadItemStats();new MsgUserTotalRefineLev().Send(p);Q.Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"perfection-quicken",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void Sign(GameState p,uint target,string text) {
            if(!Ready(p) || string.IsNullOrWhiteSpace(text) || text.Any(char.IsControl))return;
            byte[] encoded=Server.Encoding.GetBytes(text);
            if(encoded.Length>32 || Server.Encoding.GetString(encoded)!=text || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot){
                ConquerItem item;
                if(!Ready(p) || !p.Equipment.TryGetItem(target,out item) || !Usable(item) || item.StackSize>1
                    || !ItemHandler.IsEquipment(item.ID) || item.PerfectionLevel<18 || item.PerfectionLevel>54
                    || item.PerfectionOwnerGuid!=p.Entity.UID)return;
                string old=item.Signature??"";if(old==text)return;
                uint fee=old.Length==0?0u:270u,balance=p.Entity.ConquerPoints,level=item.PerfectionLevel;
                if(balance<fee)return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");SkillTable.LockOwner(c,t,p);ArtifactStabilization.Own(c,t,p,item);
                    if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@after WHERE uid=@uid AND COALESCE(conquerpoints,0)=@before",
                        "after",(long)(balance-fee),"uid",(long)p.Entity.UID,"before",(long)balance)!=1)
                        throw new InvalidOperationException("Signature balance changed.");
                    if(Q.Execute(c,t,@"UPDATE items SET signature=@text WHERE uid=@item AND entityid=@owner
AND COALESCE(signature,'')=@old AND COALESCE(perfectionlevel,0)=@level AND COALESCE(perfectionownerguid,0)=@owner",
                        "text",text,"item",(long)target,"owner",(long)p.Entity.UID,"old",old,"level",(long)level)!=1)
                        throw new InvalidOperationException("Signature item changed.");
                    var hook=BeforeCommit;if(hook!=null)hook(p);ConquerItem current;
                    if(!Ready(p) || !p.Equipment.TryGetItem(target,out current) || !ReferenceEquals(current,item) || !Usable(item)
                        || item.PerfectionOwnerGuid!=p.Entity.UID || item.PerfectionLevel!=level || item.Signature!=old || p.Entity.ConquerPoints!=balance)
                        throw new InvalidOperationException("Signature session changed.");
                    t.Commit();
                }}
                var install=BeforeInstall;if(install!=null)install(p);
                p.Entity.ApplyCommittedConquerPoints(balance-fee);item.Signature=text;item.Mode=Enums.ItemMode.Update;
                item.Send(p);p.Send(new CMsgItemRefine(item,p));Q.Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"perfection-signature",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void Apply(GameState p,uint target,uint[] selected) {
            if(!Ready(p) || selected==null || selected.Length<1 || selected.Length>40)return;
            selected=selected.ToArray();
            if(selected.Any(id=>id==0 || id==target) || selected.Distinct().Count()!=selected.Length || !SessionInstall.TryEnter(p.Entity.UID))return;
            try {lock(p.ItemSyncRoot) {
                ConquerItem item;if(!Ready(p) || !p.Equipment.TryGetItem(target,out item) || !Usable(item) || item.StackSize>1
                    || item.PerfectionLevel>=54 || !ConquerItemInformation.BaseInformations.ContainsKey(item.ID))return;
                int slot=item.Position>20?item.Position-20:item.Position;
                if(!(slot>=1 && slot<=6 || slot==8 || slot==10 || slot==11 || slot==12 || slot==18 || slot==19))return;
                if(item.PerfectionOwnerGuid!=0 && item.PerfectionOwnerGuid!=p.Entity.UID)return;
                var materials=new ConquerItem[selected.Length];
                ulong points=item.PerfectionProgress;
                for(int i=0;i<selected.Length;i++) {
                    ConquerItem stone;
                    if(!p.Inventory.TryGetItem(selected[i],out stone) || !MaterialUsable(stone,item))return;
                    materials[i]=stone;points+=Value(stone,item);
                }
                uint beforeLevel=item.PerfectionLevel,beforeProgress=item.PerfectionProgress,owner=item.PerfectionOwnerGuid;
                string ownerName=item.PerfectionOwner??"";uint level=beforeLevel;
                var costReader=new CMsgItemRefineOpt(null);var proposed=item.DeepCopy();
                while(level<54) {
                    proposed.PerfectionLevel=level;uint cost=costReader.GetPerfectionProgress(proposed);
                    if(cost==0)throw new InvalidOperationException("Perfection cost missing.");
                    if(points<cost)break;points-=cost;level++;
                }
                uint progress=level==54?0:checked((uint)points);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");SkillTable.LockOwner(c,t,p);
                    foreach(var owned in materials.Concat(new[]{item}).OrderBy(i=>i.UID))ArtifactStabilization.Own(c,t,p,owned);
                    foreach(var stone in materials)
                        using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@item AND plus=@plus","item",(long)stone.UID,"plus",(int)stone.Plus))
                            if(q.ExecuteScalar()==null)throw new InvalidOperationException("Perfection material plus changed.");
                    if(Q.Execute(c,t,@"UPDATE items SET perfectionlevel=@level,perfectionprogress=@progress,perfectionownerguid=@owner,perfectionowner=@name
WHERE uid=@item AND entityid=@owner AND COALESCE(perfectionlevel,0)=@beforeLevel AND COALESCE(perfectionprogress,0)=@beforeProgress
AND COALESCE(perfectionownerguid,0)=@beforeOwner AND COALESCE(perfectionowner,'')=@beforeName",
                        "level",(long)level,"progress",(long)progress,"owner",(long)p.Entity.UID,"name",p.Entity.Name,"item",(long)item.UID,
                        "beforeLevel",(long)beforeLevel,"beforeProgress",(long)beforeProgress,"beforeOwner",(long)owner,"beforeName",ownerName)!=1)
                        throw new InvalidOperationException("Perfection target changed.");
                    foreach(var stone in materials) {
                        Q.Execute(c,t,"DELETE FROM itemadding WHERE uid=@item","item",(long)stone.UID);
                        if(Q.Execute(c,t,"DELETE FROM items WHERE uid=@item AND entityid=@owner","item",(long)stone.UID,"owner",(long)p.Entity.UID)!=1)
                            throw new InvalidOperationException("Perfection material changed.");
                    }
                    var hook=BeforeCommit;if(hook!=null)hook(p);
                    if(!Ready(p) || !ReferenceEquals(p.Equipment.TryGetItem(target),item) || !Usable(item)
                        || item.PerfectionLevel!=beforeLevel || item.PerfectionProgress!=beforeProgress || item.PerfectionOwnerGuid!=owner
                        || (item.PerfectionOwner??"")!=ownerName || materials.Any(i=>!p.Inventory.Objects.Any(j=>ReferenceEquals(i,j)) || !MaterialUsable(i,item)))
                        throw new InvalidOperationException("Perfection session changed.");
                    t.Commit();
                }}
                var installed=BeforeInstall;if(installed!=null)installed(p);
                item.PerfectionLevel=level;item.PerfectionProgress=progress;item.PerfectionOwnerGuid=p.Entity.UID;item.PerfectionOwner=p.Entity.Name;
                Q.Install(p,selected,new ConquerItem[0]);item.Mode=Enums.ItemMode.Update;item.Send(p);
                p.Send(Kernel.FinalizeProtoBuf(new MsgFixedPezzi.MsgFixedPezziProto2 {ItemUID=item.UID,EntityUID=p.Entity.UID,
                    OwnerUID=item.PerfectionOwnerGuid,OwnerName=item.PerfectionOwner,Progress=progress,Stars=level},3250));
                p.LoadItemStats();new MsgUserTotalRefineLev().Send(p);Q.Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"perfection-temper",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
    }
}
