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
internal static class EliteGuildJourneyTests {
    const BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    static Type fixtures;
    static int checks;
    static DateTime now;
    static object Invoke(Type type,string name,params object[] args){try{return type.GetMethod(name,Hidden).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static object Helper(string name,params object[] args){return Invoke(fixtures,name,args);}
    static object Sql(string sql,params object[] args){using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(sql,c)){for(int i=0;i<args.Length;i+=2)q.Parameters.AddWithValue((string)args[i],args[i+1]);return q.ExecuteScalar();}}}
    static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;Console.WriteLine("PASS "+name);}
    static void Dialog(GameState p,byte option){p.ActiveNpc=115522;Npcs.GetDialog(new NpcRequest(true){NpcID=115522,OptionID=option},p);}
    static void Near(GameState p){p.Entity.Teleport(1002,282,163);p.ActiveNpc=115522;}
    static int Main(){
        var b=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION"));
        Environment.SetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION",null);
        if(b.Host!="127.0.0.1" || b.Port!=25439 || b.Database!="revora_candidate_game")throw new InvalidOperationException("Private candidate required.");
        string prior=Environment.CurrentDirectory,runtime=@"C:\Revora\DisposablePostgres\moon-box-candidate\game";
        // Reuse the reviewed ordinary-player/map/load helpers, never their Main or live configuration reader.
        fixtures=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PotencyHunt.Tests.exe")).GetType("PotencyHuntTests",true);
        DataHolder.ConnectionString=b.ConnectionString;Sql("CREATE SCHEMA elite_guild_journey_fixture");
        try {
            b.SearchPath="elite_guild_journey_fixture";DataHolder.ConnectionString=b.ConnectionString;Environment.CurrentDirectory=runtime;
            // Fixed source-controlled table names only; no public game rows are copied.
            foreach(string table in new[]{"entities","items","titles","skills","arena","teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding","chi","npcs","sobnpcs","monsterspawns","furniture"})
                Sql(table=="entities"?"CREATE TABLE entities AS TABLE public.entities WITH NO DATA":"CREATE TABLE "+table+" (LIKE public."+table+" INCLUDING ALL)");
            Sql("CREATE TABLE guilds(id bigint PRIMARY KEY,wins bigint NOT NULL,losts bigint NOT NULL,leadername text NOT NULL DEFAULT '');CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
            ConquerItemInformation.Load();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
            Invoke(typeof(Entity).Assembly.GetType("Revora.Game.Features.ProgressionEvidence",true),"Initialize");
            Invoke(typeof(Entity).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkLedger",true),"Initialize");
            Invoke(fixtures.Assembly.GetType("CompanionTests",true),"Reset");
            var town=(Map)Helper("World",runtime,(ushort)1002);Server.Vars=new VariableVault();
            ushort terrain=Convert.ToUInt16(Sql("SELECT mapdoc FROM public.maps WHERE id=2071"));
            var field=(Map)Helper("World",runtime,terrain);field.ID=2071;field.BaseID=terrain;Kernel.Maps[2071]=field;
            MapsTable.MapInformations[2071]=new MapsTable.MapInformation{ID=2071,BaseID=terrain,Status=2};
            Sql("INSERT INTO sobnpcs SELECT * FROM public.sobnpcs WHERE id=88816 AND mapid=2071");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("SELECT cellx,celly,lookface,type,life,maxlife,name FROM sobnpcs WHERE id=88816",c))
            using(var r=q.ExecuteReader()){if(!r.Read())throw new InvalidOperationException("Private pole catalog missing.");field.AddNpc(new SobNpcSpawn{UID=88816,MapID=2071,X=Convert.ToUInt16(r[0]),Y=Convert.ToUInt16(r[1]),Mesh=Convert.ToUInt16(r[2]),Type=(Enums.NpcType)Convert.ToByte(r[3]),Hitpoints=Convert.ToUInt32(r[4]),MaxHitpoints=Convert.ToUInt32(r[5]),Name=Convert.ToString(r[6])});}}
            Helper("LoadEliteEnvoy",runtime);EliteGuildWar.EliteGwint();
            var leader=(GameState)Helper("PrizeLeader",(uint)912);var outsider=(GameState)Helper("PrizeLeader",(uint)913);
            Check(leader.Account.State==AccountTable.AccountState.Player && !leader.Fake,"fixture leader is an ordinary player");
            Near(leader);Dialog(leader,1);Check(leader.Entity.MapID==1002,"closed scheduled battle refuses NPC entry");
            var day=DateTime.Now.Date.AddDays(1);while(day.DayOfWeek==DayOfWeek.Wednesday || day.DayOfWeek==DayOfWeek.Saturday)day=day.AddDays(1);
            now=DateTime.SpecifyKind(day.AddHours(21).AddMinutes(1).AddSeconds(5),DateTimeKind.Local).ToUniversalTime();
            typeof(EliteGuildWar).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
            Invoke(typeof(EliteGuildWar),"Tick");var battle=(Guid)typeof(EliteGuildWar).GetField("battleId",Hidden).GetValue(null);
            Check(EliteGuildWar.IsWar && (string)Sql("SELECT state FROM revora_elite_guild_battles WHERE id=@id","id",battle)=="active","calendar tick opens the saved battle");
            Near(leader);Dialog(leader,1);Check(leader.Entity.MapID==2071 && leader.Entity.X==47 && leader.Entity.Y==131,"ordinary NPC enters the scheduled arena");
            var pole=EliteGuildWar.Poles;leader.Entity.Teleport(2071,(ushort)(pole.X+1),pole.Y);
            Check(leader.Entity.MapID==2071 && Kernel.GetDistance(leader.Entity.X,leader.Entity.Y,pole.X,pole.Y)<=18,"fixture reaches the installed pole on real terrain");
            Helper("GuildHit",leader,pole,uint.MaxValue);
            Check(EliteGuildWar.PoleKeeper==leader.Guild && Convert.ToInt64(Sql("SELECT captures FROM revora_elite_guild_battles WHERE id=@id","id",battle))==1,"ordinary damage callback earns a durable capture");
            now=DateTime.SpecifyKind(day.AddHours(22).AddMinutes(2),DateTimeKind.Local).ToUniversalTime();Invoke(typeof(EliteGuildWar),"Tick");
            Check(!EliteGuildWar.IsWar && (string)Sql("SELECT state FROM revora_elite_guild_battles WHERE id=@id","id",battle)=="won","delayed deadline tick freezes the earned winning result");
            Near(outsider);Dialog(outsider,5);Check(outsider.Inventory.Count==0,"another guild cannot claim the winning prize");
            Near(leader);uint rvp=leader.Entity.RevoraPoint;ulong gold=leader.Entity.Money;
            for(int i=0;i<40;i++)Helper("Pack",leader,(uint)1088001,(ushort)1);Dialog(leader,5);
            Check(leader.Entity.Money==gold && leader.Entity.RevoraPoint==rvp && Sql("SELECT claimed_at FROM revora_elite_guild_battles WHERE id=@id","id",battle)==DBNull.Value,"full bag preserves the unpaid scheduled prize");
            foreach(var item in leader.Inventory.Objects.ToArray())leader.Inventory.Remove(item,Enums.ItemUse.Remove);
            Dialog(leader,5);var soul=leader.Inventory.Objects.Single();
            Check(leader.Entity.Money==gold+500000000000UL && leader.Entity.RevoraPoint==rvp+200000 && soul.Bound && soul.Days==0 && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7,"NPC delivers exact currency and a bound permanent P7");
            Check(Convert.ToInt64(Sql("SELECT soul_uid FROM revora_elite_guild_battles WHERE id=@id AND claimed_by=@uid","id",battle,"uid",(long)leader.Entity.UID))==soul.UID,"claim receipt retains its actual leader and soul identity");
            Dialog(leader,5);
            Check(leader.Entity.Money==gold+500000000000UL && leader.Entity.RevoraPoint==rvp+200000 && leader.Inventory.Count==1,"eligible winning leader cannot claim the same result twice");
            Check(EntityTable.SaveEntity(leader),"winner saves after committed payout");
            var reload=(GameState)Helper("ReloadGold",leader);ConquerItemTable.LoadItems(reload);Near(reload);
            Check(reload.Account.State==AccountTable.AccountState.Player && reload.Guild==leader.Guild && reload.AsMember==reload.Guild.Leader,"reloaded ordinary character retains winning guild leadership");
            Dialog(reload,5);
            Check(reload.Entity.Money==gold+500000000000UL && reload.Entity.RevoraPoint==rvp+200000 && reload.Inventory.Objects.Count(i=>i.UID==soul.UID && i.Bound)==1,"production character/item reload preserves prizes without replay");
            Console.WriteLine("Elite Guild scheduled journey checks passed: "+checks);return 0;
        } catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message+"\n"+e.StackTrace);return 1;}
        finally {
            Environment.CurrentDirectory=prior;NpgsqlConnection.ClearAllPools();b.SearchPath="public";DataHolder.ConnectionString=b.ConnectionString;
            Sql("DROP SCHEMA elite_guild_journey_fixture CASCADE");DataHolder.ConnectionString=null;
        }
    }
}
