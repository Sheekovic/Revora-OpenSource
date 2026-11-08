using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures.Society;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type GuildPrizes=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildSeasonPrizes");
    static void GuildPrize(GameState p,byte option){p.ActiveNpc=10003;Npcs.GetDialog(new NpcRequest(true){NpcID=10003,OptionID=option},p);}
    static long PrizeCount(GameState p){return Scalar("SELECT count(*) FROM revora_guild_prizes WHERE uid=@uid",p.Entity.UID);}
    static long SpentTokens(GameState p){return Scalar("SELECT count(*) FROM revora_guild_token_claims WHERE uid=@uid AND spent_at IS NOT NULL",p.Entity.UID);}
    static GameState PrizeLeader(uint id) {
        var guild=new Guild("pending"){ID=id,Name="PrizeGuild"+id};Kernel.Guilds[id]=guild;Query("INSERT INTO guilds(id,wins,losts) VALUES(@id,0,0)","id",(long)id);
        return GuildClaimant(Kernel.Maps[1002],guild,true);
    }
    static void EarnGuildToken(GameState p,int month,int day,int year=2026) {
        var guild=p.Guild;guildNow=new DateTime(year,month,day,15,0,0,DateTimeKind.Local).ToUniversalTime();var start=guildNow;p.Entity.MapID=1038;p.Entity.X=(ushort)(GuildWar.Pole.X+1);p.Entity.Y=GuildWar.Pole.Y;
        GuildWar.Start();GuildHit(p,GuildWar.Pole,uint.MaxValue);p.Guild=null;guildNow=guildNow.AddHours(1);GuildWar.End();p.Guild=guild;
        p.Entity.MapID=1002;p.Entity.X=317;p.Entity.Y=322;guildNow=start.AddDays(1);GuildClaim(p);
    }
    static void VerifyGuildPrizes(GameState bronze,GameState late) {
        Call(GuildPrizes,null,"Initialize");Call(GuildPrizes,null,"Initialize");bronze.Entity.FullyLoaded=true;
        guildNow=GuildLocal(9,21,2);var token=bronze.Inventory.Objects.Single(i=>i.ID==723467);uint before=bronze.Entity.ConquerPoints;
        GuildPrize(bronze,85);Check(PrizeCount(bronze)==0 && SpentTokens(bronze)==0,"unoffered confirmation cannot redeem a guild prize");
        GuildPrize(bronze,84);GuildPrize(bronze,85);Check(PrizeCount(bronze)==0,"one token cannot buy a three-token Gold Prize");
        token.Lock=1;GuildPrize(bronze,82);GuildPrize(bronze,85);token.Lock=0;
        Check(PrizeCount(bronze)==0 && !Fenced(bronze),"locked seasonal tokens cannot enter a prize offer");
        GuildPrize(bronze,82);guildNow=guildNow.AddMinutes(2);GuildPrize(bronze,85);Check(PrizeCount(bronze)==0,"prize offer expires at its exact confirmation deadline");
        GuildPrize(bronze,82);bronze.Entity.X=1;GuildPrize(bronze,85);bronze.Entity.X=317;GuildPrize(bronze,80);
        Check(PrizeCount(bronze)==0,"remote confirmation cannot spend tokens or pay CPs");
        GuildPrize(bronze,82);token.Bound=false;GuildPrize(bronze,85);token.Bound=true;Check(PrizeCount(bronze)==0,"changed token preimage invalidates the pending prize offer");
        foreach(string fault in new[]{"items:DELETE","items:INSERT","revora_guild_token_claims:UPDATE","revora_guild_prizes:INSERT","revora_guild_prize_tokens:INSERT","entities:UPDATE"}) {
            string table=fault.Split(':')[0],operation=fault.Split(':')[1];GuildPrize(bronze,82);
            Query("CREATE FUNCTION suppress_guild_prize() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_guild_prize BEFORE "+operation+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_guild_prize()");
            try{GuildPrize(bronze,85);}finally{Query("DROP TRIGGER suppress_guild_prize ON "+table+";DROP FUNCTION suppress_guild_prize()");}
            Check(Fenced(bronze) && PrizeCount(bronze)==0 && SpentTokens(bronze)==0 && bronze.Inventory.ContainsUID(token.UID) && bronze.Entity.ConquerPoints==before
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",bronze.Entity.UID)==before
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=723467",bronze.Entity.UID)==1,"suppressed "+fault+" rolls back tokens, CPs and every prize receipt");DemonReset(bronze);
        }
        GuildPrize(bronze,82);Query("UPDATE entities SET conquerpoints=conquerpoints+1 WHERE uid=@uid","uid",(long)bronze.Entity.UID);GuildPrize(bronze,85);
        Check(Fenced(bronze) && PrizeCount(bronze)==0 && SpentTokens(bronze)==0,"stale saved CP purse cannot be overwritten by a prize claim");
        Query("UPDATE entities SET conquerpoints=@cp WHERE uid=@uid","cp",(long)before,"uid",(long)bronze.Entity.UID);DemonReset(bronze);
        GuildPrize(bronze,82);Query("UPDATE items SET bound=0 WHERE uid=@uid","uid",(long)token.UID);GuildPrize(bronze,85);
        Check(Fenced(bronze) && PrizeCount(bronze)==0 && SpentTokens(bronze)==0,"changed saved material rolls back its preceding spent-token update");
        Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)token.UID);DemonReset(bronze);
        Call(typeof(Entity),bronze.Entity,"ApplyCommittedConquerPoints",uint.MaxValue);Query("UPDATE entities SET conquerpoints=@cp WHERE uid=@uid","cp",(long)uint.MaxValue,"uid",(long)bronze.Entity.UID);
        GuildPrize(bronze,82);GuildPrize(bronze,85);Check(!Fenced(bronze) && PrizeCount(bronze)==0 && SpentTokens(bronze)==0,"CP overflow preserves earned tokens without fencing a healthy player");
        Call(typeof(Entity),bronze.Entity,"ApplyCommittedConquerPoints",before);Query("UPDATE entities SET conquerpoints=@cp WHERE uid=@uid","cp",(long)before,"uid",(long)bronze.Entity.UID);
        for(int i=bronze.Inventory.Count;i<40;i++)Pack(bronze,1088001,1);GuildPrize(bronze,82);Parallel.For(0,12,i=>GuildPrize(bronze,85));
        var prize=bronze.Inventory.Objects.Single(i=>i.ID==2100055);
        Check(!Fenced(bronze) && PrizeCount(bronze)==1 && SpentTokens(bronze)==1 && bronze.Entity.ConquerPoints==before+3000 && bronze.Inventory.Count==40
            && prize.Bound && prize.Days==0 && !bronze.Inventory.ContainsUID(token.UID),"former guild leader redeems one earned token for Bronze and 3000 CPs once, using the freed bag slot");
        var saved=ConquerItemTable.LoadItem(prize.UID);Check(saved.ID==2100055 && saved.Bound && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",bronze.Entity.UID)==before+3000,
            "production item loader and saved CP purse retain the committed Bronze reward");
        GuildPrize(bronze,82);GuildPrize(bronze,85);Check(PrizeCount(bronze)==1 && bronze.Entity.ConquerPoints==before+3000,"spent token cannot authorize another prize after a fresh menu visit");
        Check(EntityTable.SaveEntity(bronze),"ordinary save retains the committed prize and CP balance");
        var loadedPlayer=TravelPlayer(Kernel.Maps[1002],317,322);uint placeholder=loadedPlayer.Entity.UID;loadedPlayer.Account.EntityID=bronze.Entity.UID;
        loadedPlayer.Account.Username="guild_prize_fixture";loadedPlayer.Quests=new Quests(loadedPlayer);
        Check((bool)Call(typeof(EntityTable),null,"LoadEntityState",loadedPlayer),"ordinary prize recipient reload succeeds");
        Kernel.GamePool.Remove(placeholder);Kernel.GamePool[bronze.Entity.UID]=loadedPlayer;loadedPlayer.Action=2;loadedPlayer.Entity.FullyLoaded=true;ConquerItemTable.LoadItems(loadedPlayer);
        GuildPrize(loadedPlayer,82);GuildPrize(loadedPlayer,85);
        Check(loadedPlayer.Entity.ConquerPoints==before+3000 && loadedPlayer.Inventory.ContainsUID(prize.UID) && !loadedPlayer.Inventory.ContainsUID(token.UID)
            && PrizeCount(loadedPlayer)==1,"reloaded CPs and prize survive while the spent token remains unavailable for replay");
        Check(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",loadedPlayer.Entity.UID)==1,"guild token claim, Bronze redemption and production reload preserve stored wardrobe design");
        var loose=Pack(late,723467,1);loose.Bound=true;loose.Days=35;loose.DayStamp=GuildLocal(10,25,15).ToLocalTime().AddDays(-35);
        Query("UPDATE items SET bound=1,days='35',daystamp=@stamp WHERE uid=@uid","stamp",loose.DayStamp.ToBinary(),"uid",(long)loose.UID);
        guildNow=GuildLocal(10,22,23,59);GuildPrize(late,83);GuildPrize(late,85);Check(PrizeCount(late)==0,"one earned token plus a matching loose legacy token cannot buy Silver");
        GuildPrize(late,82);guildNow=GuildLocal(10,25,15);GuildPrize(late,85);GuildPrize(late,82);GuildPrize(late,85);
        Check(PrizeCount(late)==0 && SpentTokens(late)==0,"expired seasonal receipt cannot redeem through an old offer or a new menu");
        var gold=PrizeLeader(206);foreach(int day in new[]{8,15,22})EarnGuildToken(gold,11,day);
        Check(GuildTokens(gold)==3 && gold.Inventory.Objects.Count(i=>i.ID==723467)==3,"three actual Sunday victories yield three distinct same-season leader tokens");
        uint goldBefore=gold.Entity.ConquerPoints;GuildPrize(gold,84);Parallel.For(0,8,i=>GuildPrize(gold,85));
        Check(!Fenced(gold) && PrizeCount(gold)==1 && SpentTokens(gold)==3 && gold.Inventory.Objects.Single().ID==2100075 && gold.Entity.ConquerPoints==goldBefore+20000,
            "three-token Gold Prize and 20000 CPs follow the full ordinary battlefield-to-exchange route");
        Check(Scalar("SELECT count(*) FROM revora_guild_prize_tokens t JOIN revora_guild_prizes p ON p.id=t.prize WHERE p.uid=@uid",gold.Entity.UID)==3,
            "Gold Prize keeps all three distinct consumed weekly-token receipts for rollover provenance");
        var silver=PrizeLeader(207);EarnGuildToken(silver,11,29);EarnGuildToken(silver,12,6);uint silverBefore=silver.Entity.ConquerPoints;
        GuildPrize(silver,83);GuildPrize(silver,85);Check(!Fenced(silver) && PrizeCount(silver)==1 && SpentTokens(silver)==2 && silver.Inventory.Objects.Single().ID==2100065
            && silver.Entity.ConquerPoints==silverBefore+6000,"two actual weekly victories exchange for Silver and 6000 CPs");
        VerifyGuildRollover(gold,silver);
    }
}
