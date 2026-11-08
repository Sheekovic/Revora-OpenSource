using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type RefineProgress=typeof(Entity).Assembly.GetType("Revora.Game.Features.RefineryProgress");
    static byte[] RefineRequest(uint target,uint material) {
        var packet=new byte[24];Writer.WriteUInt16(16,0,packet);Writer.WriteUInt16(2076,2,packet);Writer.WriteUInt32(target,8,packet);Writer.WriteUInt32(material,12,packet);return packet;
    }
    static byte[] StabilizeRequest(uint target,params uint[] materials) {
        var packet=new byte[24+materials.Length*4];Writer.WriteUInt16((ushort)(packet.Length-8),0,packet);Writer.WriteUInt16(PacketID.ItemAdding,2,packet);
        Writer.WriteUInt32(target,8,packet);Writer.WriteUInt32((uint)materials.Length,12,packet);for(int i=0;i<materials.Length;i++)Writer.WriteUInt32(materials[i],16+i*4,packet);return packet;
    }
    static void Refine(GameState p,ConquerItem target,ConquerItem material){Purification.Process(RefineRequest(target.UID,material.UID),p);}
    static ConquerItem[] RefineStones(GameState p,int big,int small){return Enumerable.Range(0,big+small).Select(i=>Pack(p,i<big?723695u:723694u,1)).ToArray();}
    static void RefineStable(GameState p,ConquerItem target,ConquerItem[] stones){ItemAddingStabilization.Process(StabilizeRequest(target.UID,stones.Select(i=>i.UID).ToArray()),p);}
    static long RefineRows(ConquerItem item,string predicate){return Scalar("SELECT count(*) FROM itemadding WHERE uid=@uid AND addingtype=1 AND "+predicate,item.UID);}
    static void VerifyRefineryEvidenceFailures(Map map) {
        foreach(string fault in new[]{"effect","receipt","wardrobe","locked"}) {
            var p=LoadoutPlayer(map);var gear=Pack(p,410009,1);var material=Pack(p,724409,1);
            if(fault=="wardrobe")Query("UPDATE items SET inwardrobe=1 WHERE uid=@uid","uid",(long)material.UID);
            if(fault=="locked")Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)material.UID);
            string table=fault=="effect"?"itemadding":"revora_achievement_evidence";
            if(fault=="effect" || fault=="receipt")Query("CREATE FUNCTION suppress_refinery_evidence() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_refinery_evidence BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_refinery_evidence()");
            try{Refine(p,gear,material);}finally{if(fault=="effect" || fault=="receipt")Query("DROP TRIGGER suppress_refinery_evidence ON "+table+";DROP FUNCTION suppress_refinery_evidence()");}
            Check(Fenced(p) && p.Inventory.ContainsUID(material.UID) && RefineRows(gear,"true")==0 && SavedRune(gear,"refineryitem")==0
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10431",p.Entity.UID)==0,
                "failed refinery preserves material, target and achievement together: "+fault);
        }
        var owner=LoadoutPlayer(map);var target=Pack(owner,410009,1);var first=Pack(owner,724409,1);Refine(owner,target,first);
        var next=Pack(owner,724405,1);
        Query("CREATE FUNCTION suppress_refinery_delete() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_refinery_delete BEFORE DELETE ON itemadding FOR EACH ROW EXECUTE FUNCTION suppress_refinery_delete()");
        try{Refine(owner,target,next);}finally{Query("DROP TRIGGER suppress_refinery_delete ON itemadding;DROP FUNCTION suppress_refinery_delete()");}
        Check(Fenced(owner) && owner.Inventory.ContainsUID(next.UID) && SavedRune(target,"refineryitem")==724409 && RefineRows(target,"true")==1
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10431",owner.Entity.UID)==1,
            "suppressed previous-effect deletion preserves the original effect, material and single achievement");
        DemonReset(owner); // Existing fixture recovery; not a full character login.
        var restored=new Achievement(owner.Entity);restored.Load("");Call(typeof(Achievement),restored,"Refresh",DateTime.UtcNow,true);
        int position=(10431/100-101)*32+10431%100-1;
        Check((System.BitConverter.ToUInt32(restored.ToArray(),16+position/32*4)&(1u<<(position%32)))!=0,
            "fresh production achievement loading restores committed first refinery after a failed replacement");
    }
    static void VerifyArtifactApplication(Map map) {
        SoulProtection.Load();var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArtifactApplication");
        var info=ConquerItemInformation.BaseInformations.Values.Where(i=>i.PurificationLevel>=1 && i.PurificationLevel<=7
            && i.PurificationMeteorNeed>0 && i.PurificationMeteorNeed%10!=0 && SoulProtection.Soul_Protections.ContainsKey(i.ID)).OrderBy(i=>i.ID)
            .First(i=>ConquerItemInformation.BaseInformations.Values.Any(t=>(bool)Call(service,null,"Fits",t.ID,i.ID)));
        uint gearType=ConquerItemInformation.BaseInformations.Values.Where(t=>(bool)Call(service,null,"Fits",t.ID,info.ID)).OrderBy(t=>t.ID).First().ID;
        int cost=info.PurificationMeteorNeed;int scrolls=(cost+9)/10;int change=scrolls*10-cost;
        DateTime now=DateTime.UtcNow;service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        foreach(string mode in new[]{"scroll","loose","mixed","bound","short","full","foreign","effect","receipt","payment","change","locked","wardrobe","bound-denied","wrong-target","missing-map","malformed"}) {
            var p=LoadoutPlayer(map);var target=Pack(p,gearType,1);var soul=Pack(p,info.ID,2);
            bool bound=mode=="bound";
            if(bound)foreach(var item in new[]{target,soul}){item.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)item.UID);}
            var money=Pack(p,mode=="loose"?1088001u:720027u,(ushort)(mode=="loose"?cost+2:scrolls+1));
            if(bound){money.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)money.UID);}
            if(mode=="mixed")Pack(p,1088001,(ushort)cost);
            if(mode=="short"){p.Inventory.Remove(money.UID,Enums.ItemUse.None,false);Query("DELETE FROM items WHERE uid=@uid","uid",(long)money.UID);}
            if(mode=="full")while(p.Inventory.Count<40)Pack(p,1088000,1);
            if(mode=="foreign")Query("UPDATE items SET entityid=1 WHERE uid=@uid","uid",(long)soul.UID);
            if(mode=="locked")Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)money.UID);
            if(mode=="wardrobe")Query("UPDATE items SET inwardrobe=1 WHERE uid=@uid","uid",(long)soul.UID);
            if(mode=="bound-denied"){soul.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)soul.UID);}
            if(mode=="wrong-target"){target.ID=1088000;Query("UPDATE items SET id=1088000 WHERE uid=@uid","uid",(long)target.UID);}
            var mapping=SoulProtection.Soul_Protections[info.ID];
            if(mode=="missing-map")SoulProtection.Soul_Protections.Remove(info.ID);
            long before=Scalar("SELECT COALESCE(sum(CASE WHEN id=720027 THEN 10*stacksize WHEN id=1088001 THEN stacksize ELSE 0 END),0) FROM items WHERE entityid=@uid",p.Entity.UID);
            bool fault=new[]{"effect","receipt","payment","change"}.Contains(mode);
            string table=mode=="effect"?"itemadding":mode=="receipt"?"revora_achievement_evidence":"items";
            string operation=mode=="payment"?"UPDATE":"INSERT";
            if(fault)Query("CREATE FUNCTION suppress_artifact_apply() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_artifact_apply BEFORE "+operation+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_artifact_apply()");
            var packet=RefineRequest(target.UID,soul.UID);packet[4]=1;
            if(mode=="malformed")packet[0]--;
            try{Parallel.For(0,8,n=>Purification.Process(packet,p));}
            finally{if(mode=="missing-map")SoulProtection.Soul_Protections[info.ID]=mapping;if(fault)Query("DROP TRIGGER suppress_artifact_apply ON "+table+";DROP FUNCTION suppress_artifact_apply()");}
            long after=Scalar("SELECT COALESCE(sum(CASE WHEN id=720027 THEN 10*stacksize WHEN id=1088001 THEN stacksize ELSE 0 END),0) FROM items WHERE entityid=@uid",p.Entity.UID);
            bool success=new[]{"scroll","loose","mixed","bound"}.Contains(mode);
            long receipts=Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10501",p.Entity.UID);
            if(success) {
                Check(!Fenced(p) && target.Purification.PurificationItemID==info.ID && soul.StackSize==1 && SavedRune(soul,"stacksize")==1
                    && before-after==cost && receipts==1 && HasAchievement(p,10501),
                    "artifact application charges exact Meteor value and one soul once under concurrent requests: "+mode);
                if(mode=="scroll" || bound)Check(p.Inventory.Objects.Where(i=>i.ID==1088001).Count()==change
                    && p.Inventory.Objects.Where(i=>i.ID==1088001).All(i=>i.Bound==bound),"Meteor scroll change preserves exact quantity and binding: "+mode);
                var loaded=ConquerItemTable.LoadItem(target.UID);ItemAddingTable.GetAddingsForItem(loaded);
                Check(loaded.Purification.PurificationItemID==info.ID && loaded.Purification.PurificationDuration==604800,
                    "production item load restores the applied Dragon Soul: "+mode);
                if(mode=="scroll") {
                    now=now.AddSeconds(2);long stamp=loaded.Purification.AddedOn.Ticks;
                    Query("CREATE FUNCTION suppress_artifact_replace() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_artifact_replace BEFORE DELETE ON itemadding FOR EACH ROW EXECUTE FUNCTION suppress_artifact_replace()");
                    try{Purification.Process(packet,p);}finally{Query("DROP TRIGGER suppress_artifact_replace ON itemadding;DROP FUNCTION suppress_artifact_replace()");}
                    Check(Fenced(p) && soul.StackSize==1 && Scalar("SELECT addedon FROM itemadding WHERE uid=@uid AND addingtype=0",target.UID)==stamp
                        && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10501",p.Entity.UID)==1,
                        "suppressed artifact replacement retains the previous effect, unused soul and single achievement");
                }
            } else Check(before==after && soul.StackSize==2 && SavedRune(soul,"stacksize")==2 && receipts==0
                && Scalar("SELECT count(*) FROM itemadding WHERE uid=@uid AND addingtype=0",target.UID)==0,
                "rejected or failed artifact application preserves soul, Meteor payment and achievement: "+mode);
        }
        service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));
    }
    static void VerifyModernArtifacts(Map map) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArtifactApplication");
        foreach(uint soulId in Enumerable.Range(801000,5).Concat(Enumerable.Range(801100,5))
            .Concat(Enumerable.Range(0,10).Select(i=>801200+i*2)).Concat(Enumerable.Range(0,5).Select(i=>801300+i*2)).Select(i=>(uint)i)) {
            uint[] families=soulId<801100?new uint[]{617}:soulId<801200?new uint[]{421,619,620}:soulId<801300?new uint[]{624}:new uint[]{626};
            var info=ConquerItemInformation.BaseInformations[soulId];
            foreach(uint family in families) {
                var targetInfo=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==family && i.Level>=info.Level).OrderBy(i=>i.ID).First();
                var low=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==family && i.Level<info.Level).OrderBy(i=>i.ID).First();
                Check((bool)Call(service,null,"Fits",targetInfo.ID,soulId) && !(bool)Call(service,null,"Fits",low.ID,soulId),
                    "modern soul accepts its reviewed weapon family only at the catalog level: "+soulId+"/"+family);
                foreach(uint wrongFamily in new uint[]{410,500,617,421,619,620,624,626}.Except(families)) {
                    var wrong=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==wrongFamily && i.Level>=info.Level).OrderBy(i=>i.ID).First();
                    Check(!(bool)Call(service,null,"Fits",wrong.ID,soulId),"modern soul rejects incompatible high-level family: "+soulId+"/"+wrongFamily);
                }
                var p=LoadoutPlayer(map);var target=Pack(p,targetInfo.ID,1);var soul=Pack(p,soulId,1);var meteors=Pack(p,1088001,info.PurificationMeteorNeed);
                var packet=RefineRequest(target.UID,soul.UID);packet[4]=1;Purification.Process(packet,p);
                Check(!Fenced(p) && !p.Inventory.ContainsUID(soul.UID) && !p.Inventory.ContainsUID(meteors.UID)
                    && target.Purification.PurificationItemID==soulId && HasAchievement(p,10501),
                    "ordinary handler applies modern soul with exact payment and achievement: "+soulId+"/"+family);
                var restored=ConquerItemTable.LoadItem(target.UID);ItemAddingTable.GetAddingsForItem(restored);
                Check(restored.Purification.PurificationItemID==soulId && restored.Purification.PurificationLevel==info.PurificationLevel,
                    "modern soul identity and phase survive production item reload: "+soulId+"/"+family);
            }
        }
        foreach(uint pack in new uint[]{727465,729305,3100000,3100010,3300242,3301736,3304557,3306602})
            Check(!(bool)Call(service,null,"Fits",410339u,pack),"non-soul pack cannot exploit its purification-level catalog field: "+pack);
    }
    static void VerifyArtifactStabilization(Map map) {
        uint soul=ConquerItemInformation.BaseInformations.Values.First(i=>i.PurificationLevel==7).ID;
        foreach(string mode in new[]{"success","duplicate","short","expired","bound","foreign","effect","receipt","stone"}) {
            var p=LoadoutPlayer(map);var target=Pack(p,410009,1);var first=Pack(p,723695,2);var second=Pack(p,723695,1);
            var effect=new ItemAdding.Purification_{Available=true,ItemUID=target.UID,PurificationItemID=soul,PurificationLevel=7,
                PurificationDuration=604800,AddedOn=DateTime.Now.AddDays(mode=="expired"?-8:0)};
            target.Purification=effect;ItemAddingTable.AddPurification(effect);
            if(mode=="bound"){first.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)first.UID);}
            if(mode=="foreign")Query("UPDATE items SET entityid=1 WHERE uid=@uid","uid",(long)first.UID);
            uint[] ids=mode=="duplicate"?new[]{first.UID,first.UID}:mode=="short"?new[]{first.UID}:new[]{first.UID,second.UID};
            var packet=StabilizeRequest(target.UID,ids);packet[4]=1;
            string table=mode=="effect"?"itemadding":mode=="stone"?"items":"revora_achievement_evidence";
            bool fault=mode=="effect" || mode=="receipt" || mode=="stone";
            string operation=mode=="receipt"?"INSERT":"UPDATE";
            if(fault)Query("CREATE FUNCTION suppress_artifact() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_artifact BEFORE "+operation+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_artifact()");
            try{Parallel.For(0,8,n=>ItemAddingStabilization.Process(packet,p));}
            finally{if(fault)Query("DROP TRIGGER suppress_artifact ON "+table+";DROP FUNCTION suppress_artifact()");}
            long saved=Scalar("SELECT duration FROM itemadding WHERE uid=@uid AND addingtype=0",target.UID);
            long receipt=Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10502",p.Entity.UID);
            if(mode=="success") {
                Check(!Fenced(p) && saved==0 && target.Purification.PurificationDuration==0 && first.StackSize==1
                    && !p.Inventory.ContainsUID(second.UID) && receipt==1 && HasAchievement(p,10502),
                    "concurrent artifact stabilization spends one unit per distinct stone and saves one achievement");
                var loaded=ConquerItemTable.LoadItem(target.UID);ItemAddingTable.GetAddingsForItem(loaded);
                Check(loaded.Purification.PurificationItemID==soul && loaded.Purification.PurificationDuration==0,
                    "production item reload retains the permanent Dragon Soul");
            } else Check(saved==604800 && target.Purification.PurificationDuration==604800 && first.StackSize==2
                && p.Inventory.ContainsUID(second.UID) && receipt==0 && SavedRune(first,"stacksize")==2,
                "invalid or failed artifact stabilization preserves effect, stones and achievement: "+mode);
        }
    }
    static void VerifyRefinery(Map map) {
        Query("ALTER TABLE entities ADD COLUMN level integer DEFAULT 140,ADD COLUMN my_title integer DEFAULT 0");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        Query("CREATE TABLE refinery (LIKE public.refinery INCLUDING ALL);INSERT INTO refinery SELECT * FROM public.refinery;CREATE TABLE refineryboxes (LIKE public.refineryboxes INCLUDING ALL);INSERT INTO refineryboxes SELECT * FROM public.refineryboxes");
        Refinery.LoadItems();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);ConquerItem.ItemUID=new Counter(7900000);
        var now=DateTime.UtcNow;RefineProgress.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        var p=LoadoutPlayer(map);var gear=Pack(p,410009,1);var material=Pack(p,724409,1);
        Check(p.Account.State==AccountTable.AccountState.Player,"refinery uses an ordinary authenticated player");
        for(int size=0;size<24;size++){Purification.Process(RefineRequest(gear.UID,material.UID).Take(size).ToArray(),p);ItemAddingStabilization.Process(StabilizeRequest(gear.UID).Take(size).ToArray(),p);}
        Check(p.Inventory.Count==2 && gear.RefineItem==0,"truncated refinery and stabilization frames cannot mutate items");
        var wrong=Pack(p,724404,1);Refine(p,gear,wrong);Check(gear.RefineItem==0 && p.Inventory.Objects.Any(i=>i.UID==wrong.UID),"bow-specific material cannot refine a blade");
        Parallel.For(0,12,i=>Refine(p,gear,material));
        Check(!Fenced(p) && gear.RefineItem==724409 && p.Inventory.Objects.All(i=>i.UID!=material.UID) && RefineRows(gear,"duration=604800")==1,"concurrent application creates one seven-day Super effect and consumes one material");
        Check(SavedRune(gear,"refineryitem")==724409 && SavedRune(gear,"refinerytime")==gear.RefineryTime.Ticks && gear.RefineStats.Level==5,"effect identity, lifetime and calculated level persist consistently");
        Check(HasAchievement(p,10431) && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10431",p.Entity.UID)==1,
            "committed first refinery records one durable achievement after concurrent application");
        VerifyRefineryEvidenceFailures(map);
        VerifyArtifactStabilization(map);
        VerifyArtifactApplication(map);
        VerifyModernArtifacts(map);
        var shortBatch=RefineStones(p,2,0);RefineStable(p,gear,shortBatch);
        Check(gear.ExtraEffect.EffectDuration==604800 && shortBatch.All(i=>p.Inventory.Objects.Any(j=>j.UID==i.UID)),"two hundred points cannot stabilize a Super refinery for the old artifact price");
        var extra=RefineStones(p,0,7);var full=shortBatch.Concat(extra).ToArray();
        Query("CREATE TABLE refinery_fixture_reference(uid bigint REFERENCES items(uid));INSERT INTO refinery_fixture_reference VALUES(@uid)","uid",(long)full.Last().UID);
        RefineStable(p,gear,full);
        Check(Fenced(p) && gear.ExtraEffect.EffectDuration==604800 && RefineRows(gear,"duration=604800")==1 && full.All(i=>SavedRune(i,"id")==i.ID),"late stone-consumption failure rolls back permanence and all selected stones");
        Query("DROP TABLE refinery_fixture_reference");DemonReset(p);Parallel.For(0,12,i=>RefineStable(p,gear,full));
        Check(!Fenced(p) && gear.ExtraEffect.EffectDuration==0 && RefineRows(gear,"duration=0 AND stabilizationpoints=270")==1 && full.All(i=>!p.Inventory.Objects.Any(j=>j.UID==i.UID)),"Super permanence costs exactly 270 points and repeated terminal requests cannot charge twice");
        var sacred=Pack(p,3004137,1);now=now.AddSeconds(2);Refine(p,gear,sacred);
        Check(gear.RefineItem==3004137 && RefineRows(gear,"duration=0")==1 && RefineRows(gear,"duration>0")==1,"a Sacred temporary effect preserves the existing permanent Super effect");
        now=now.AddDays(7);Check(gear.RefineStats.Identifier==724409,"expired temporary effect stops affecting calculations at its exact boundary");
        Call(RefineProgress,null,"Expire",p,gear);
        Check(!Fenced(p) && gear.RefineItem==724409 && gear.ExtraEffect.EffectDuration==0 && RefineRows(gear,"duration>0")==0 && SavedRune(gear,"refinerytime")==0,"expiry atomically restores the permanent effect and clears the temporary timestamp");
        sacred=Pack(p,3004137,1);Refine(p,gear,sacred);var delta=RefineStones(p,1,6);RefineStable(p,gear,delta);
        Check(gear.ExtraEffect.EffectLevel==6 && gear.ExtraEffect.EffectDuration==0 && RefineRows(gear,"stabilizationpoints=430")==1,"retained 270-point credit means Sacred permanence needs only another 160 points");
        var lower=Pack(p,724405,1);now=now.AddSeconds(2);Refine(p,gear,lower);RefineStable(p,gear,new ConquerItem[0]);
        Check(gear.RefineItem==724405 && gear.ExtraEffect.EffectDuration==0 && RefineRows(gear,"stabilizationpoints=430")==1,"retained credit can stabilize a lower-level replacement without charging another stone");
        var retained=Pack(p,3004137,1);now=now.AddSeconds(2);Refine(p,gear,retained);var needless=RefineStones(p,1,0);RefineStable(p,gear,needless);
        Check(p.Inventory.Objects.Any(i=>i.UID==needless[0].UID) && gear.ExtraEffect.EffectDuration==0,"unnecessary selected stones remain owned when retained credit already covers the effect");
        p.Inventory.Remove(gear.UID,Enums.ItemUse.None,false);ConquerItemTable.LoadItems(p);gear=p.Inventory.Objects.Single(i=>i.UID==gear.UID);
        Check(gear.RefineItem==3004137 && gear.RefineStats.Level==6 && gear.ExtraEffect.EffectDuration==0,"production inventory reload restores the effective permanent refinery without timestamp truncation");
        var unbound=Pack(p,410009,1);var bound=Pack(p,724409,1);bound.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)bound.UID);Refine(p,unbound,bound);
        Check(unbound.RefineItem==0 && p.Inventory.Objects.Any(i=>i.UID==bound.UID),"bound material cannot silently bind or enhance unbound equipment");
        unbound.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)unbound.UID);Refine(p,unbound,bound);
        Check(unbound.RefineItem==724409 && !p.Inventory.Objects.Any(i=>i.UID==bound.UID),"bound material applies successfully to owned bound equipment");
        var outsider=LoadoutPlayer(map);var foreign=Pack(outsider,724409,1);Refine(p,unbound,foreign);Check(p.Inventory.Objects.All(i=>i.UID!=foreign.UID) && outsider.Inventory.Count==1,"foreign refinery material cannot be consumed");
        var fail=Pack(p,410009,1);var input=Pack(p,724409,1);Query("ALTER TABLE itemadding ADD CONSTRAINT refinery_failure CHECK(addingpercent<>6) NOT VALID");Refine(p,fail,input);
        Check(Fenced(p) && fail.RefineItem==0 && RefineRows(fail,"true")==0 && SavedRune(input,"id")==724409,"failed effect insertion leaves both equipment and material unchanged");
        Query("ALTER TABLE itemadding DROP CONSTRAINT refinery_failure");DemonReset(p);Refine(p,fail,input);now=now.AddDays(7);
        Query("ALTER TABLE items ADD CONSTRAINT refinery_expiry_failure CHECK(refineryitem<>0) NOT VALID");Call(RefineProgress,null,"Expire",p,fail);
        Check(Fenced(p) && RefineRows(fail,"duration>0")==1 && fail.RefineStats==null,"failed expiry cleanup preserves its saved row but cannot reactivate expired combat bonuses");
        Query("ALTER TABLE items DROP CONSTRAINT refinery_expiry_failure");DemonReset(p);Call(RefineProgress,null,"Expire",p,fail);
        Check(fail.RefineItem==0 && RefineRows(fail,"true")==0 && SavedRune(fail,"refineryitem")==0,"expiry retries without reward loss or resurrecting the temporary effect");
        for(byte vip=0;vip<=7;vip++)Check((uint)Call(RefineProgress,null,"Duration",vip)==(uint)(vip==0?7:vip==1?8:vip==2?9:vip==3?11:14)*86400,"VIP refinery lifetime: "+vip);
        for(byte level=1;level<=6;level++)Check(DataHolder.RefineryStabilizationPoints(level)==new[]{10,30,70,150,270,430}[level-1],"official refinery stabilization cost: "+level);
        var fighter=LoadoutPlayer(map);var weapon=Pack(fighter,410009,1);fighter.Inventory.Remove(weapon.UID,Enums.ItemUse.None,false);weapon.Position=4;
        Query("UPDATE items SET position=4 WHERE uid=@uid","uid",(long)weapon.UID);fighter.Equipment.Objects[3]=weapon;fighter.LoadItemStats();int critical=fighter.Entity.CriticalStrike;
        var buff=Pack(fighter,724409,1);Refine(fighter,weapon,buff);
        Check(!Fenced(fighter) && fighter.Entity.CriticalStrike>critical,"equipped refinery immediately changes ordinary-player combat stats");
        now=now.AddDays(7);ConquerItem.CheckItemExtra(weapon,fighter);
        Check(!Fenced(fighter) && weapon.RefineItem==0 && fighter.Entity.CriticalStrike==critical,"online expiry recalculates equipped stats back to their unrefined value");
        var packType=typeof(Entity).Assembly.GetType("Revora.Game.Features.DisCityPacks");Call(packType,null,"Initialize");
        var pools=(uint[][])packType.GetField("materials",Hidden).GetValue(null);int pick=0;packType.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(count=>pick));
        var buyer=LoadoutPlayer(map);uint wardrobe=StoredQuestGarment(buyer);
        for(int quality=0;quality<2;quality++)for(int index=0;index<pools[quality].Length;index++) {
            pick=index;var box=Pack(buyer,(uint)(3008057+quality),1);ItemHandler.UseItem(box,buyer);var reward=buyer.Inventory.Objects.Single();
            Check(!Fenced(buyer) && reward.ID==pools[quality][index] && Kernel.DatabaseRefinery[reward.ID].Level==5+quality && reward.Days==0,
                "Dis City pack gives usable installed quality "+(quality+5)+" material "+reward.ID+" without expiring the loose item");
            Query("DELETE FROM items WHERE uid=@uid","uid",(long)reward.UID);buyer.Inventory.Remove(reward.UID,Enums.ItemUse.None,false);
        }
        pick=0;var stack=Pack(buyer,3008057,3);Parallel.For(0,12,i=>ItemHandler.UseItem(stack,buyer));
        Check(!Fenced(buyer) && stack.StackSize==2 && buyer.Inventory.Count==2 && SavedRune(stack,"stacksize")==2,"concurrent pack-stack replay consumes one unit and delivers one material");
        now=now.AddSeconds(1);ItemHandler.UseItem(stack,buyer);Check(stack.StackSize==1 && buyer.Inventory.Count==3,"a deliberate later opening can consume the next pack-stack unit");
        var reject=Pack(buyer,3008058,1);Query("ALTER TABLE revora_dis_refinery_packs ADD CONSTRAINT dis_pack_failure CHECK(pack_type<>3008058) NOT VALID");ItemHandler.UseItem(reject,buyer);
        Check(Fenced(buyer) && buyer.Inventory.Objects.Any(i=>i.UID==reject.UID) && SavedRune(reject,"id")==3008058,"late Dis City pack receipt failure rolls back material creation and keeps the pack");
        Query("ALTER TABLE revora_dis_refinery_packs DROP CONSTRAINT dis_pack_failure");DemonReset(buyer);reject.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)reject.UID);ItemHandler.UseItem(reject,buyer);
        Check(!Fenced(buyer) && buyer.Inventory.Objects.Any(i=>i.ID==pools[1][0] && i.Bound),"bound Dis City packs preserve binding on their refinery material");
        var blocked=LoadoutPlayer(map);var blockedPack=Pack(blocked,3008057,2);for(int i=0;i<39;i++)Pack(blocked,1088001,1);ItemHandler.UseItem(blockedPack,blocked);
        Check(blocked.Inventory.Count==40 && blockedPack.StackSize==2,"full bag blocks a stacked pack opening before consuming or rolling a prize");
        var complete=Pack(outsider,3008057,1);var copied=new ConquerItem(true){UID=complete.UID,ID=complete.ID,StackSize=1};ItemHandler.UseItem(copied,outsider);ItemHandler.UseItem(complete,buyer);
        Check(outsider.Inventory.Objects.Any(i=>i.UID==complete.UID),"copied pack objects and foreign pack UIDs cannot be opened");
        Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"Dis City refinery packs preserve stored wardrobe design through opening and failed receipt recovery");
        VerifySuperiorPacks(packType,map);
    }
    static long RefineryPackReceipts(GameState p) {return Scalar("SELECT count(*) FROM revora_dis_refinery_packs WHERE uid=@uid",p.Entity.UID);}
    static void SuperiorDraw(Type type,int quality,int index=0) {int call=0;type.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>call++==0?quality:index));}
    static void VerifySuperiorPacks(Type type,Map map) {
        foreach(int draw in new[]{0,69,70,99}) {
            var p=LoadoutPlayer(map);uint wardrobe=StoredQuestGarment(p);var box=Pack(p,720399,1);SuperiorDraw(type,draw);ItemHandler.UseItem(box,p);
            var result=p.Inventory.Objects.Single();
            Check(!Fenced(p) && result.ID!=720399 && Kernel.DatabaseRefinery[result.ID].Level==(draw<70?4:5)
                && !result.Bound && RefineryPackReceipts(p)==1,"Superior pack follows the installed 70/30 quality boundary: "+draw);
            var restored=ConquerItemTable.LoadItem(result.UID);
            Check(restored.ID==result.ID && !restored.Bound && restored.Days==0,"Superior pack's permanent loose material survives production item load: "+draw);
            ItemHandler.UseItem(box,p);Check(RefineryPackReceipts(p)==1 && p.Inventory.Count==1,"spent Superior pack replay cannot create another result: "+draw);
            Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"Superior pack opening preserves stored wardrobe design: "+draw);
        }
        var pool=(uint[])type.GetField("superiorLevel4",Hidden).GetValue(null);
        for(int index=0;index<pool.Length;index++) {
            var p=LoadoutPlayer(map);var box=Pack(p,720399,1);box.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)box.UID);
            SuperiorDraw(type,0,index);ItemHandler.UseItem(box,p);var result=p.Inventory.Objects.Single();
            Check(!Fenced(p) && result.ID==pool[index] && result.Bound && Kernel.DatabaseRefinery[result.ID].Level==4,
                "Superior pack issues installed usable bound level-four material: "+result.ID);
        }
        var buyer=LoadoutPlayer(map);var stack=Pack(buyer,720399,3);type.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>0));
        Parallel.For(0,12,n=>ItemHandler.UseItem(stack,buyer));
        Check(!Fenced(buyer) && stack.StackSize==2 && RefineryPackReceipts(buyer)==1 && buyer.Inventory.Count==2,"concurrent Superior pack clicks spend one stack unit and grant one result");
        var fighter=LoadoutPlayer(map);var blade=Pack(fighter,410009,1);var offering=Pack(fighter,720399,1);
        SuperiorDraw(type,0,Array.IndexOf(pool,724408u));ItemHandler.UseItem(offering,fighter);
        var critical=fighter.Inventory.Objects.Single(i=>i.ID==724408);Refine(fighter,blade,critical);
        Check(!Fenced(fighter) && blade.RefineItem==724408 && blade.RefineStats.Level==4 && SavedRune(blade,"refineryitem")==724408
            && !fighter.Inventory.ContainsUID(critical.UID),"a level-four result from an opened Superior pack applies through the real refinery handler and persists on a blade");
        type.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>0));
        var full=LoadoutPlayer(map);var single=Pack(full,720399,1);for(int i=0;i<39;i++)Pack(full,1088001,1);ItemHandler.UseItem(single,full);
        Check(!Fenced(full) && full.Inventory.Count==40 && !full.Inventory.ContainsUID(single.UID) && RefineryPackReceipts(full)==1,"a single Superior pack may replace itself in a full bag");
        var blocked=LoadoutPlayer(map);var multiple=Pack(blocked,720399,2);for(int i=0;i<39;i++)Pack(blocked,1088001,1);
        type.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>{throw new Exception("Capacity rejection must not draw.");}));ItemHandler.UseItem(multiple,blocked);
        Check(!Fenced(blocked) && multiple.StackSize==2 && RefineryPackReceipts(blocked)==0,"full bag rejects a stacked Superior pack before its random draw");
        type.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>0));
        foreach(string state in new[]{"locked","suspicious","wardrobe","expiry","foreign","copied","trade","dead","fake","replaced"}) {
            var p=LoadoutPlayer(map);var box=Pack(p,720399,1);ConquerItem request=box;
            if(state=="locked")box.Lock=1;if(state=="suspicious")box.Suspicious=true;if(state=="wardrobe")box.InWardrobe=true;if(state=="expiry")box.Days=1;
            if(state=="copied")request=new ConquerItem(true){UID=box.UID,ID=box.ID};if(state=="trade")p.Trade.InTrade=true;
            if(state=="dead")p.Entity.Hitpoints=0;if(state=="fake")p.Fake=true;if(state=="replaced")Kernel.GamePool.Remove(p.Entity.UID);
            ItemHandler.UseItem(request,state=="foreign"?buyer:p);
            Check(p.Inventory.ContainsUID(box.UID) && RefineryPackReceipts(p)==0,"ineligible Superior opening preserves its pack: "+state);
        }
        foreach(string failure in new[]{"locked","inventory","result","receipt"}) {
            var p=LoadoutPlayer(map);var box=Pack(p,720399,1);
            if(failure=="locked")Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)box.UID);
            if(failure=="inventory")Query("INSERT INTO items(uid,id,entityid,position,warehouse,stacksize) VALUES(@item,1088001,@uid,0,0,1)","item",(long)ConquerItem.ItemUID.Next,"uid",(long)p.Entity.UID);
            string table=failure=="result"?"items":"revora_dis_refinery_packs";
            if(failure=="result" || failure=="receipt")Query("CREATE FUNCTION suppress_refinery_pack() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_refinery_pack BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_refinery_pack()");
            try{ItemHandler.UseItem(box,p);}finally{if(failure=="result" || failure=="receipt")Query("DROP TRIGGER suppress_refinery_pack ON "+table+";DROP FUNCTION suppress_refinery_pack()");}
            Check(Fenced(p) && p.Inventory.ContainsUID(box.UID) && SavedRune(box,"id")==720399 && RefineryPackReceipts(p)==0
                && p.Inventory.Count==1,"Superior pack debit and reward roll back on "+failure);
            if(failure=="result" || failure=="receipt") {DemonReset(p);ItemHandler.UseItem(box,p);
                Check(!Fenced(p) && !p.Inventory.ContainsUID(box.UID) && RefineryPackReceipts(p)==1,"Superior pack can retry a rolled-back "+failure+" failure once");}
        }
        foreach(int invalid in new[]{-1,100}) {
            var p=LoadoutPlayer(map);var box=Pack(p,720399,1);SuperiorDraw(type,invalid);ItemHandler.UseItem(box,p);
            Check(Fenced(p) && p.Inventory.ContainsUID(box.UID) && RefineryPackReceipts(p)==0,"invalid Superior quality draw cannot consume the pack: "+invalid);
        }
        type.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>0));
    }
}
