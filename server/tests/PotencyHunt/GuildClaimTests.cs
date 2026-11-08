using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures.Society;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type GuildClaims=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildSeasonClaims");
    static readonly Type Leadership=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildLeadership");
    static GameState GuildClaimant(Map town,Guild guild,bool leader) {
        var p=TravelPlayer(town,317,322);p.Guild=guild;p.Entity.GuildID=(ushort)guild.ID;
        p.AsMember=new Guild.Member(guild.ID){ID=p.Entity.UID,Name=p.Entity.Name,Rank=leader?Enums.GuildMemberRank.GuildLeader:Enums.GuildMemberRank.Member};
        guild.Members[p.Entity.UID]=p.AsMember;p.Entity.GuildRank=(ushort)p.AsMember.Rank;
        if(leader){guild.LeaderName=p.Entity.Name;guild.Leader=p.AsMember;Query("UPDATE guilds SET leadername=@name WHERE id=@id","name",p.Entity.Name,"id",(long)guild.ID);}
        if(!EntityTable.SaveEntity(p))throw new Exception("Guild claimant save failed");return p;
    }
    static void GuildClaim(GameState p) {p.ActiveNpc=10003;Npcs.GetDialog(new NpcRequest(true){NpcID=10003,OptionID=81},p);}
    static long GuildTokens(GameState p) {return Scalar("SELECT count(*) FROM revora_guild_token_claims WHERE uid=@uid",p.Entity.UID);}
    static byte[] GuildTransferPacket(Guild.Member target) {
        var bytes=new byte[34+target.Name.Length];Writer.WriteUInt16((ushort)(bytes.Length-8),0,bytes);Writer.WriteUInt16(1107,2,bytes);
        Writer.WriteUInt32(GuildCommand.ConfirmPromote,4,bytes);Writer.WriteUInt16((ushort)Enums.GuildMemberRank.GuildLeader,8,bytes);
        bytes[24]=1;bytes[25]=(byte)target.Name.Length;Writer.WriteString(target.Name,26,bytes);Writer.WriteUInt64(PacketHandler.ClientSeal,bytes.Length-8,bytes);return bytes;
    }
    static void TransferGuild(GameState p,Guild.Member target) {GuildCommand.Process(GuildTransferPacket(target),p);}
    static void VerifyGuildClaims() {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL);ALTER TABLE guilds ADD COLUMN leadername text NOT NULL DEFAULT ''");
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        Kernel.GamePool.Clear();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);Call(EvidenceType,null,"Initialize");
        var town=Kernel.Maps[1002];town.AddNpc(new NpcSpawn{UID=10003,MapID=1002,X=317,Y=321,Mesh=150,Name="GuildDirector",Type=Enums.NpcType.Talker});
        ConquerItem.ItemUID=new Counter(6800000);Call(GuildClaims,null,"Initialize");Call(GuildClaims,null,"Initialize");
        var guild=Kernel.Guilds[202];guild.Members.Clear();var old=GuildClaimant(town,guild,true);var next=GuildClaimant(town,guild,false);
        guildNow=GuildLocal(9,20,17);GuildClaim(old);Check(GuildTokens(old)==0 && old.Inventory.Count==0,"Sunday winner cannot claim before the documented Monday window");
        guildNow=GuildLocal(9,21,1,29);GuildClaim(old);Check(GuildTokens(old)==0,"Monday 01:29 remains outside the guild token window");
        guildNow=GuildLocal(9,21,1,30);GuildClaim(next);Check(GuildTokens(next)==0,"ordinary non-leader cannot claim the guild's weekly token");
        foreach(string mode in new[]{"far","dead","fake","trade","replaced"}) {
            if(mode=="far")old.Entity.X=1;else if(mode=="dead")old.Entity.Hitpoints=0;else if(mode=="fake")old.Fake=true;else if(mode=="trade")old.Trade.InTrade=true;else Kernel.GamePool[old.Entity.UID]=next;
            GuildClaim(old);old.Entity.X=317;old.Entity.Hitpoints=old.Entity.MaxHitpoints;old.Fake=false;old.Trade.InTrade=false;Kernel.GamePool[old.Entity.UID]=old;
            Check(GuildTokens(old)==0,"invalid seasonal claim is rejected: "+mode);
        }
        foreach(string mode in new[]{"short","length","family","seal","strings","name","oversize"}) {
            var packet=GuildTransferPacket(next.AsMember);if(mode=="short")Array.Resize(ref packet,12);else if(mode=="length")packet[0]--;else if(mode=="family")packet[2]++;
            else if(mode=="seal")packet[packet.Length-1]^=1;else if(mode=="strings")packet[24]=2;else if(mode=="name")packet[25]=255;else Array.Resize(ref packet,129);
            GuildCommand.Process(packet,old);Check(guild.Leader==old.AsMember && !Fenced(old),"malformed leader promotion cannot mutate ownership: "+mode);
        }
        Query("CREATE FUNCTION suppress_guild_leader() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_guild_leader BEFORE UPDATE ON guilds FOR EACH ROW EXECUTE FUNCTION suppress_guild_leader()");
        try{TransferGuild(old,next.AsMember);}finally{Query("DROP TRIGGER suppress_guild_leader ON guilds;DROP FUNCTION suppress_guild_leader()");}
        Check(Fenced(old) && Fenced(next) && guild.LeaderName==old.Entity.Name && old.AsMember.Rank==Enums.GuildMemberRank.GuildLeader
            && Scalar("SELECT guildrank FROM entities WHERE uid=@uid",old.Entity.UID)==(int)Enums.GuildMemberRank.GuildLeader
            && Scalar("SELECT guildrank FROM entities WHERE uid=@uid",next.Entity.UID)==(int)Enums.GuildMemberRank.Member,"failed leader-name update rolls back both persisted member ranks and runtime ownership");
        DemonReset(old);DemonReset(next);Parallel.For(0,8,i=>TransferGuild(old,next.AsMember));
        Check(!Fenced(old) && !Fenced(next) && guild.Leader==next.AsMember && guild.LeaderName==next.Entity.Name
            && old.Entity.GuildRank==(ushort)Enums.GuildMemberRank.DeputyLeader && next.Entity.GuildRank==(ushort)Enums.GuildMemberRank.GuildLeader,
            "native promotion dispatcher transfers ordinary guild leadership once and updates both clients");
        Check(Scalar("SELECT guildrank FROM entities WHERE uid=@uid",old.Entity.UID)==(int)Enums.GuildMemberRank.DeputyLeader
            && Scalar("SELECT guildrank FROM entities WHERE uid=@uid",next.Entity.UID)==(int)Enums.GuildMemberRank.GuildLeader,"leadership transfer persists both ranks immediately");
        GuildClaim(old);Check(GuildTokens(old)==0,"former leader cannot take an unclaimed weekly entitlement after transfer");
        StoredQuestGarment(next);
        for(int i=0;i<40;i++)Pack(next,1088001,1);GuildClaim(next);Check(GuildTokens(next)==0 && next.Inventory.Count==40,"full bag leaves the seasonal token entitlement unclaimed");
        foreach(var item in next.Inventory.Objects.ToArray())DisRemove(next,item);
        foreach(string table in new[]{"items","revora_guild_token_claims"}) {
            Query("CREATE FUNCTION suppress_guild_token() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_guild_token BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_guild_token()");
            try{GuildClaim(next);}finally{Query("DROP TRIGGER suppress_guild_token ON "+table+";DROP FUNCTION suppress_guild_token()");}
            Check(Fenced(next) && GuildTokens(next)==0 && next.Inventory.Count==0 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0",next.Entity.UID)==0,"suppressed "+table+" insert retains the complete token entitlement");DemonReset(next);
        }
        Parallel.For(0,12,i=>GuildClaim(next));var token=next.Inventory.Objects.Single();
        Check(!Fenced(next) && GuildTokens(next)==1 && token.ID==723467 && token.Bound && token.StackSize==1,"new current leader claims one bound provenance-backed Lord Token despite repeated clicks");
        var loaded=ConquerItemTable.LoadItem(token.UID);
        Check(loaded.ID==723467 && loaded.Bound && loaded.Days==35 && loaded.DayStamp.AddDays(loaded.Days).ToUniversalTime()==GuildLocal(10,25,15),"saved seasonal token retains the exact season boundary through production item loading");
        TransferGuild(next,old.AsMember);GuildClaim(old);Check(GuildTokens(old)==0 && GuildTokens(next)==1,"later leadership transfer cannot replay a claimed round or transfer its owned token");
        Check(EntityTable.SaveEntity(next),"ordinary character save preserves transferred rank and claimed inventory");
        var fresh=TravelPlayer(town,317,322);uint placeholder=fresh.Entity.UID;fresh.Account.EntityID=next.Entity.UID;fresh.Account.Username="guild_token_fixture";fresh.Quests=new Quests(fresh);
        Check((bool)Call(typeof(EntityTable),null,"LoadEntityState",fresh),"ordinary former-leader character reload succeeds");
        Kernel.GamePool.Remove(placeholder);Kernel.GamePool[next.Entity.UID]=fresh;fresh.Action=2;ConquerItemTable.LoadItems(fresh);
        Check(fresh.Entity.GuildRank==(ushort)Enums.GuildMemberRank.DeputyLeader && fresh.Inventory.ContainsUID(token.UID),"reloaded former leader keeps the earned token and the committed deputy rank");
        var offline=GuildClaimant(town,guild,false);Kernel.GamePool.Remove(offline.Entity.UID);TransferGuild(old,offline.AsMember);
        Check(!Fenced(old) && guild.Leader==offline.AsMember && Scalar("SELECT guildrank FROM entities WHERE uid=@uid",offline.Entity.UID)==(int)Enums.GuildMemberRank.GuildLeader,
            "native leadership transfer also persists the rank of an offline guild member");
        var reloadedGuild=new Guild(offline.Entity.Name){ID=guild.ID,Name=guild.Name};Call(typeof(GuildTable),null,"LoadMembers",reloadedGuild);
        Check(reloadedGuild.Leader.ID==offline.Entity.UID && reloadedGuild.Members[old.Entity.UID].Rank==Enums.GuildMemberRank.DeputyLeader,
            "production guild-member loader reconstructs the offline leader and demoted former leader");
        // The fifth-round winner was earned through actual battle damage earlier in this suite.
        var lateGuild=Kernel.Guilds[205];lateGuild.Members.Clear();var late=GuildClaimant(town,lateGuild,true);
        guildNow=GuildLocal(10,23,0);GuildClaim(late);Check(GuildTokens(late)==0,"Friday midnight closes the last weekly claim window");
        guildNow=GuildLocal(10,22,23,59);Query("UPDATE guilds SET leadername='different_saved_leader' WHERE id=205");GuildClaim(late);
        Check(!Fenced(late) && GuildTokens(late)==0,"stale in-memory leader cannot override the saved guild ownership");
        Query("UPDATE guilds SET leadername=@name WHERE id=205","name",late.Entity.Name);GuildClaim(late);Check(GuildTokens(late)==1,"Thursday's last minute still admits the fifth-round winner");
        guildNow=GuildLocal(10,25,15);GuildClaim(late);Check(GuildTokens(late)==1,"season rollover cannot mint another token from an old victory");
        VerifyGuildPrizes(fresh,late);
    }
}
