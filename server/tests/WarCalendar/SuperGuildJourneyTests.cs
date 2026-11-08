using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static class SuperGuildJourneyTests {
    const BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    static Type fixtures;
    static int checks;
    static DateTime now;
    static object Invoke(Type type,string name,params object[] args){try{return type.GetMethod(name,Hidden).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static object Helper(string name,params object[] args){return Invoke(fixtures,name,args);}
    static object Sql(string sql,params object[] args){using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(sql,c)){for(int i=0;i<args.Length;i+=2)q.Parameters.AddWithValue((string)args[i],args[i+1]);return q.ExecuteScalar();}}}
    static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;Console.WriteLine("PASS "+name);}
    static NpcSpawn clerk;
    static void Enter(GameState p){p.ActiveNpc=380;Npcs.GetDialog(new NpcRequest(true){NpcID=380,OptionID=23},p);}
    static void Near(GameState p){p.Entity.Teleport(1002,(ushort)(clerk.X+1),clerk.Y);p.ActiveNpc=380;}
    static readonly Type Purses=typeof(Entity).Assembly.GetType("Revora.Game.Features.SuperGuildWarPurses",true);
    static void Poll(){now=now.AddSeconds(11);Invoke(Purses,"Tick");}
    static int Main(){
        var b=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION"));
        Environment.SetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION",null);
        if(b.Host!="127.0.0.1" || b.Port!=25439 || b.Database!="revora_candidate_game")throw new InvalidOperationException("Private candidate required.");
        string prior=Environment.CurrentDirectory,runtime=@"C:\Revora\DisposablePostgres\moon-box-candidate\game";
        // Reuse the reviewed ordinary-player/map/load helpers, never their Main or live configuration reader.
        fixtures=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PotencyHunt.Tests.exe")).GetType("PotencyHuntTests",true);
        DataHolder.ConnectionString=b.ConnectionString;Sql("CREATE SCHEMA super_guild_journey_fixture");
        try {
            b.SearchPath="super_guild_journey_fixture";DataHolder.ConnectionString=b.ConnectionString;Environment.CurrentDirectory=runtime;
            // Fixed source-controlled table names only; no public game rows are copied.
            foreach(string table in new[]{"entities","items","titles","skills","arena","teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding","chi","npcs","sobnpcs","monsterspawns","furniture"})
                Sql(table=="entities"?"CREATE TABLE entities AS TABLE public.entities WITH NO DATA":"CREATE TABLE "+table+" (LIKE public."+table+" INCLUDING ALL)");
            Sql("CREATE TABLE guilds(id bigint PRIMARY KEY,wins bigint NOT NULL,losts bigint NOT NULL,leadername text NOT NULL DEFAULT '');CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
            ConquerItemInformation.Load();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
            Invoke(typeof(Entity).Assembly.GetType("Revora.Game.Features.ProgressionEvidence",true),"Initialize");
            Invoke(typeof(Entity).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkLedger",true),"Initialize");
            Invoke(fixtures.Assembly.GetType("CompanionTests",true),"Reset");
            var town=(Map)Helper("World",runtime,(ushort)1002);Server.Vars=new VariableVault();
            ushort terrain=Convert.ToUInt16(Sql("SELECT mapdoc FROM public.maps WHERE id=1234"));
            var field=(Map)Helper("World",runtime,terrain);field.ID=1234;field.BaseID=terrain;Kernel.Maps[1234]=field;
            MapsTable.MapInformations[1234]=new MapsTable.MapInformation{ID=1234,BaseID=terrain,Status=2};
            Sql("INSERT INTO sobnpcs SELECT * FROM public.sobnpcs WHERE mapid=1234 AND id IN(811,516076,516077)");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("SELECT id,cellx,celly,lookface,type,life,maxlife,name FROM sobnpcs WHERE mapid=1234",c))
            using(var r=q.ExecuteReader())while(r.Read())field.AddNpc(new SobNpcSpawn{UID=Convert.ToUInt32(r[0]),MapID=1234,X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),Mesh=Convert.ToUInt16(r[3]),Type=(Enums.NpcType)Convert.ToByte(r[4]),Hitpoints=Convert.ToUInt32(r[5]),MaxHitpoints=Convert.ToUInt32(r[6]),Name=Convert.ToString(r[7])});}
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("SELECT mapid,cellx,celly,lookface,type FROM public.npcs WHERE id=380",c))
            using(var r=q.ExecuteReader()){if(!r.Read())throw new InvalidOperationException("Private clerk catalog missing.");clerk=new NpcSpawn{UID=380,MapID=Convert.ToUInt16(r[0]),X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),Mesh=Convert.ToUInt16(r[3]),Type=(Enums.NpcType)Convert.ToByte(r[4])};}}
            Check(clerk.MapID==1002 && !Revora.Game.Features.TwinCityPlaza.IsClosed(380),"installed Super Guild entry clerk is an open town service");town.AddNpc(clerk);
            SuperGuildWar.Initiate();
            var leader=(GameState)Helper("PrizeLeader",(uint)914);
            var member=(GameState)Helper("PurseMember",leader,Enums.GuildMemberRank.Member);
            var absent=(GameState)Helper("PurseMember",leader,Enums.GuildMemberRank.Member);
            var outsider=(GameState)Helper("PrizeLeader",(uint)915);
            Check(new[]{leader,member,absent,outsider}.All(p=>p.Account.State==AccountTable.AccountState.Player && !p.Fake),"all fixture participants are ordinary players");
            Near(leader);Enter(leader);Check(leader.Entity.MapID==1002,"closed scheduled battle refuses NPC entry");
            var day=DateTime.Now.Date.AddDays(1);while(day.DayOfWeek!=DayOfWeek.Wednesday)day=day.AddDays(1);
            now=DateTime.SpecifyKind(day.AddHours(20).AddSeconds(1),DateTimeKind.Local).ToUniversalTime();
            typeof(SuperGuildWar).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Purses.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
            Invoke(typeof(SuperGuildWar),"Tick");var battle=(Guid)typeof(SuperGuildWar).GetField("battleId",Hidden).GetValue(null);
            Check(SuperGuildWar.IsWar && (string)Sql("SELECT state FROM revora_super_guild_battles WHERE id=@id","id",battle)=="active","calendar tick opens the saved battle");
            foreach(var p in new[]{leader,member,outsider}){Near(p);Enter(p);Check(p.Entity.MapID==1234 && p.Map.ID==1234,"ordinary NPC enters scheduled arena: "+p.Entity.UID);}
            var pole=SuperGuildWar.Pole;leader.Entity.Teleport(1234,(ushort)(pole.X+1),pole.Y);
            Check(leader.Entity.MapID==1234 && Kernel.GetDistance(leader.Entity.X,leader.Entity.Y,pole.X,pole.Y)<=18,"fixture reaches installed pole on real terrain");
            Helper("GuildHit",leader,pole,uint.MaxValue);
            Check(SuperGuildWar.PoleKeeper1==leader.Guild && Convert.ToInt64(Sql("SELECT captures FROM revora_super_guild_battles WHERE id=@id","id",battle))==1,"ordinary damage callback earns a durable capture");
            uint cp=leader.Entity.ConquerPoints,rvp=leader.Entity.RevoraPoint,memberCp=member.Entity.ConquerPoints,memberRvp=member.Entity.RevoraPoint;
            ulong gold=leader.Entity.Money,memberGold=member.Entity.Money;
            for(int i=0;i<40;i++)Helper("Pack",leader,(uint)1088001,(ushort)1);
            now=DateTime.SpecifyKind(day.AddHours(21).AddMinutes(2),DateTimeKind.Local).ToUniversalTime();Invoke(typeof(SuperGuildWar),"Tick");
            Check(!SuperGuildWar.IsWar && (string)Sql("SELECT state FROM revora_super_guild_battles WHERE id=@id AND purses_frozen","id",battle)=="won","delayed deadline tick freezes the earned winning purses");
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_super_guild_purses WHERE battle=@id","id",battle))==2,"only present winning guild members earn purses");
            Poll();
            Check(leader.Entity.ConquerPoints==cp && leader.Entity.Money==gold && leader.Entity.RevoraPoint==rvp && Sql("SELECT claimed_at FROM revora_super_guild_purses WHERE battle=@id AND uid=@uid","id",battle,"uid",(long)leader.Entity.UID)==DBNull.Value,"full bag defers the leader's entire purse");
            Check(member.Entity.ConquerPoints==memberCp+Config.GuildWarMemberCp && member.Entity.Money==memberGold+Config.GuildWarMemberGold && member.Entity.RevoraPoint==memberRvp+Config.GuildWarMemberRevoraPoints && member.Inventory.Count==2,"automatic poll delivers the member's configured purse");
            Check(outsider.Inventory.Count==0 && absent.Inventory.Count==0,"outsider and absent guild member receive no prizes");
            foreach(var item in leader.Inventory.Objects.ToArray())leader.Inventory.Remove(item,Enums.ItemUse.Remove);
            Poll();Poll();
            var soul=leader.Inventory.Objects.Single(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7);
            Check(leader.Entity.ConquerPoints==cp+Config.SuperGuildWarCp && leader.Entity.Money==gold+Config.SuperGuildWarGold && leader.Entity.RevoraPoint==rvp+Config.GuildWarLeaderRevoraPoints && leader.Inventory.Count==6 && soul.Bound && soul.Days==0,"deferred poll delivers exact leader purse and bound permanent P7");
            Check((bool)Sql("SELECT @soul=ANY(rewards) AND claimed_at IS NOT NULL FROM revora_super_guild_purses WHERE battle=@id AND uid=@uid","soul",(long)soul.UID,"id",battle,"uid",(long)leader.Entity.UID),"paid receipt retains the actual soul identity");
            Poll();Invoke(Purses,"Claim",leader);
            Check(leader.Entity.ConquerPoints==cp+Config.SuperGuildWarCp && leader.Inventory.Count==6,"winning leader cannot collect the same purse twice");
            Check(EntityTable.SaveEntity(leader),"winner saves after committed payout");
            var reload=(GameState)Helper("ReloadGold",leader);ConquerItemTable.LoadItems(reload);
            Check(reload.Account.State==AccountTable.AccountState.Player && reload.Guild==leader.Guild && reload.AsMember==reload.Guild.Leader,"reloaded ordinary character retains winning guild leadership");
            Invoke(Purses,"Claim",reload);
            Check(reload.Entity.ConquerPoints==cp+Config.SuperGuildWarCp && reload.Entity.Money==gold+Config.SuperGuildWarGold && reload.Entity.RevoraPoint==rvp+Config.GuildWarLeaderRevoraPoints && reload.Inventory.Count==6 && reload.Inventory.Objects.Count(i=>i.UID==soul.UID && i.Bound)==1,"production character/item reload preserves exact prizes without replay");
            Console.WriteLine("Super Guild scheduled journey checks passed: "+checks);return 0;
        } catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message+"\n"+e.StackTrace);return 1;}
        finally {
            Environment.CurrentDirectory=prior;NpgsqlConnection.ClearAllPools();b.SearchPath="public";DataHolder.ConnectionString=b.ConnectionString;
            Sql("DROP SCHEMA super_guild_journey_fixture CASCADE");DataHolder.ConnectionString=null;
        }
    }
}
