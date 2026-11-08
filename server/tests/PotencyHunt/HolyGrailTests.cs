using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type GrailCrafting=typeof(Entity).Assembly.GetType("Revora.Game.Features.HolyGrailCrafting");
    static long GrailCrafts(GameState p){return Scalar("SELECT count(*) FROM revora_holy_grail_crafts WHERE uid=@uid",p.Entity.UID);}
    static long GrailAwards(GameState p){return Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='holy-grail-craft-v1'",p.Entity.UID);}
    static long GrailMaterials(GameState p){return Scalar("SELECT count(*) FROM revora_holy_grail_materials m JOIN revora_holy_grail_crafts c ON c.id=m.craft WHERE c.uid=@uid",p.Entity.UID);}
    static void VerifyHolyGrail() {
        Call(GrailCrafting,null,"Initialize");Call(GrailCrafting,null,"Initialize");
        var previous=(Func<DateTime>)TitleRules.GetField("Clock",Hidden).GetValue(null);TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>guildNow));
        try{VerifyHolyGrailRoute();}finally{TitleRules.GetField("Clock",Hidden).SetValue(null,previous);}
    }
    static void VerifyHolyGrailRoute() {
        var p=PrizeLeader(215);uint wardrobe=StoredQuestGarment(p);var first=new DateTime(2027,8,1);
        for(int season=0;season<12;season++) {
            var start=first.AddDays(35*season);
            for(int round=0;round<3;round++){var day=start.AddDays(7*round);EarnGuildToken(p,day.Month,day.Day,day.Year);}
            GuildPrize(p,84);GuildPrize(p,85);guildNow=DateTime.SpecifyKind(start.AddDays(35).AddHours(15),DateTimeKind.Local).ToUniversalTime();RollGuild(p);
            Check(!Fenced(p) && Rolled(p)==season+1 && p.Inventory.Objects.Count(i=>i.ID==2100085)==season+1,
                "Grail candidate earns permanent trophy "+(season+1)+" through three real battles, leader claims, prize exchange and rollover");
        }
        var trophies=p.Inventory.Objects.OrderBy(i=>i.UID).ToArray();uint cp=p.Entity.ConquerPoints;
        new TitleStorage().Login(p);var pointsBefore=p.Entity.WTitles.Points;Check(!OwnedTitles(p).ContainsKey(20342034) && !OwnedTitles(p).ContainsKey(60116011),"owning twelve trophies alone cannot unlock either Grail wardrobe award");
        bool rejected=false;try{GrantEvidence(p.Entity.UID,"holy-grail-craft-v1","wrong-award",40011000,guildNow);}catch(ArgumentException){rejected=true;}
        Check(rejected && GrailAwards(p)==0,"Grail award source cannot grant an unrelated wardrobe design");
        var legacy=PrizeLeader(216);for(int i=0;i<12;i++){var loose=Pack(legacy,2100085,1);loose.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)loose.UID);}
        GuildPrize(legacy,86);GuildPrize(legacy,87);Check(GrailCrafts(legacy)==0 && GrailAwards(legacy)==0 && legacy.Inventory.Count==12,"twelve permanent loose legacy trophies cannot establish the Grail earning requirement");
        var looseGrail=Pack(legacy,2100095,1);new TitleStorage().Login(legacy);
        Check(!OwnedTitles(legacy).ContainsKey(20342034) && !OwnedTitles(legacy).ContainsKey(60116011),"possessing a legacy Holy Grail is not proof of crafting it");DisRemove(legacy,looseGrail);
        GuildPrize(p,87);Check(GrailCrafts(p)==0,"unoffered Grail confirmation cannot spend trophies");
        foreach(string mode in new[]{"locked","timed","unbound","suspicious","missing"}) {
            var item=trophies[0];if(mode=="locked")item.Lock=1;else if(mode=="timed")item.Days=1;else if(mode=="unbound")item.Bound=false;else if(mode=="suspicious")item.Suspicious=true;
            else p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);
            GuildPrize(p,86);GuildPrize(p,87);
            item.Lock=0;item.Days=0;item.Bound=true;item.Suspicious=false;if(mode=="missing")p.Inventory.Add(item,Enums.ItemUse.None);
            Check(GrailCrafts(p)==0 && GrailAwards(p)==0 && p.Inventory.Count==12,"ineligible twelfth trophy cannot complete the recipe: "+mode);
        }
        GuildPrize(p,86);guildNow=guildNow.AddMinutes(2);GuildPrize(p,87);Check(GrailCrafts(p)==0,"Grail confirmation expires at the exact two-minute deadline");
        GuildPrize(p,86);uint originalUid=trophies[0].UID;trophies[0].UID++;GuildPrize(p,87);trophies[0].UID=originalUid;
        Check(GrailCrafts(p)==0 && GrailAwards(p)==0,"changed runtime trophy UID cannot reuse an earlier selected material identity");
        GuildPrize(p,86);p.Entity.X=1;GuildPrize(p,87);p.Entity.X=317;GuildPrize(p,80);Check(GrailCrafts(p)==0,"remote Grail confirmation cannot consume an offered set");
        foreach(string fault in new[]{"items:DELETE","items:INSERT","revora_holy_grail_crafts:INSERT","revora_holy_grail_materials:INSERT","title:INSERT","wings:INSERT"}) {
            string table=fault.Split(':')[0],operation=fault.Split(':')[1],body="RETURN NULL;";
            if(table=="title" || table=="wings"){body="IF NEW.title_key="+(table=="title"?20342034:60116011)+" THEN RETURN NULL; END IF; RETURN NEW;";table="revora_event_awards";}
            GuildPrize(p,86);Query("CREATE FUNCTION suppress_grail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN "+body+" END $$;CREATE TRIGGER suppress_grail BEFORE "+operation+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_grail()");
            try{GuildPrize(p,87);}finally{Query("DROP TRIGGER suppress_grail ON "+table+";DROP FUNCTION suppress_grail()");}
            Check(Fenced(p) && GrailCrafts(p)==0 && GrailMaterials(p)==0 && GrailAwards(p)==0 && trophies.All(i=>p.Inventory.ContainsUID(i.UID))
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=2100085",p.Entity.UID)==12,
                "suppressed "+fault+" restores all twelve trophies, the Grail and both wardrobe grants");DemonReset(p);
        }
        GuildPrize(p,86);Query("UPDATE items SET bound=0 WHERE uid=@uid","uid",(long)trophies[7].UID);GuildPrize(p,87);
        Check(Fenced(p) && GrailCrafts(p)==0 && GrailAwards(p)==0 && GrailMaterials(p)==0 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=2100085",p.Entity.UID)==12,
            "changed saved trophy preimage rolls back earlier trophy deletions");Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)trophies[7].UID);DemonReset(p);
        var successor=GuildClaimant(Kernel.Maps[1002],p.Guild,false);TransferGuild(p,successor.AsMember);
        Check(!Fenced(p) && p.AsMember.Rank==Enums.GuildMemberRank.DeputyLeader,"earned trophy owner may transfer guild leadership before crafting");
        for(int i=0;i<28;i++)Pack(p,1088001,1);GuildPrize(p,86);Parallel.For(0,12,i=>GuildPrize(p,87));
        var grail=p.Inventory.Objects.Single(i=>i.ID==2100095);
        Check(!Fenced(p) && GrailCrafts(p)==1 && GrailMaterials(p)==12 && GrailAwards(p)==2 && grail.Bound && grail.Days==0 && p.Inventory.Count==29
            && trophies.All(i=>!p.Inventory.ContainsUID(i.UID)) && p.Entity.ConquerPoints==cp,"ordinary former leader crafts one permanent bound Grail and both awards using twelve freed bag slots without a CP charge");
        Check(OwnedTitles(p).ContainsKey(20342034) && OwnedTitles(p).ContainsKey(60116011) && p.Entity.WTitles.Points==pointsBefore+1000,
            "Grail craft immediately projects both permanent wardrobe rewards and their 1000 combined points");
        Check(Scalar("SELECT count(DISTINCT source_key) FROM revora_event_awards WHERE uid=@uid AND source='holy-grail-craft-v1' AND expires_at IS NULL",p.Entity.UID)==2,
            "title and wings use distinct permanent source keys within the same atomic craft");
        TitleRequest(20342034,TitleStorage.Action.Equip).Handle(p);TitleRequest(60116011,TitleStorage.Action.Equip).Handle(p);
        Check(p.Entity.EquippedTitle==20342034 && p.Entity.EquippedWing==60116011,"ordinary crafter can equip Invincible and Supreme through the existing wardrobe handler");
        GuildPrize(p,86);GuildPrize(p,87);Check(GrailCrafts(p)==1 && GrailAwards(p)==2 && GrailMaterials(p)==12,"consumed trophies cannot replay a Grail craft or duplicate either award");
        Check(EntityTable.SaveEntity(p),"ordinary save succeeds after Grail crafting and wardrobe selection");
        var fresh=ReloadGold(p);ConquerItemTable.LoadItems(fresh);StorageManager.Load();fresh.Entity.WTitles=null;new TitleStorage().Login(fresh);
        Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"twelve-season Grail earning, crafting and production reload preserve stored wardrobe design");
        Check(!Fenced(fresh) && fresh.Inventory.ContainsUID(grail.UID) && trophies.All(i=>!fresh.Inventory.ContainsUID(i.UID))
            && fresh.Entity.EquippedTitle==20342034 && fresh.Entity.EquippedWing==60116011 && OwnedTitles(fresh).ContainsKey(20342034) && OwnedTitles(fresh).ContainsKey(60116011),
            "ordinary character, item and wardrobe reload restores the Grail and both equipped earned rewards");
        guildNow=guildNow.AddYears(5);RefreshTitles(fresh);Check(OwnedTitles(fresh).ContainsKey(20342034) && OwnedTitles(fresh).ContainsKey(60116011),"earned Grail title and wings remain permanent across later seasons");
    }
}
