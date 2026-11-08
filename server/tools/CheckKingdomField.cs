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
internal static class CheckKingdomField {
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Static;
    static readonly Type Field=typeof(Entity).Assembly.GetType("Revora.Game.Features.KingdomCompetitionField",true);
    static Type fixtures;static DateTime now;static int checks;
    static object Invoke(Type type,string method,params object[] args){try{return type.GetMethods(Hidden).Single(m=>m.Name==method && m.GetParameters().Length==args.Length).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static object Helper(string name,params object[] args){return Invoke(fixtures,name,args);}
    static object Sql(string sql,params object[] values){using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(sql,c)){for(int i=0;i<values.Length;i+=2)q.Parameters.AddWithValue((string)values[i],values[i+1]);return q.ExecuteScalar();}}}
    static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;Console.WriteLine("PASS "+name);}
    static long Count(string sql,params object[] values){return Convert.ToInt64(Sql(sql,values));}
    static void Talk(GameState p,uint npc,byte option){p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);}
    static void Town(GameState p){p.Entity.Teleport(1002,316,325);}
    static void At(GameState p,uint npc){var actor=p.Map.Npcs[npc];if(!p.Map.Floor[actor.X+2,actor.Y,MapObjectType.Player])throw new InvalidOperationException("Fixture approach is blocked by an actor footprint.");p.Entity.X=(ushort)(actor.X+2);p.Entity.Y=actor.Y;}
    static string Wealth(GameState a,GameState b){return Convert.ToString(Sql("SELECT jsonb_agg(jsonb_build_array(uid,money,conquerpoints,experience,pkpoints) ORDER BY uid)::text FROM entities WHERE uid IN (@a,@b)","a",(long)a.Entity.UID,"b",(long)b.Entity.UID));}
    static string StoredItems(GameState a,GameState b){return Convert.ToString(Sql("SELECT COALESCE(jsonb_agg(to_jsonb(i) ORDER BY uid),'[]'::jsonb)::text FROM items i WHERE entityid IN (@a,@b)","a",(long)a.Entity.UID,"b",(long)b.Entity.UID));}
    static bool Can(GameState a,GameState b){return Revora.Game.Attacking.Handle.CanAttack(a.Entity,b.Entity,null,true);}
    static void Hit(GameState a,GameState b,uint damage){Revora.Game.Attacking.Handle.ReceiveAttack(a.Entity,b.Entity,new Attack(true){AttackType=Attack.Melee,Attacker=a.Entity.UID,Attacked=b.Entity.UID},ref damage,null);}
    static void Ready(GameState p){p.Attackable=true;p.CantAttack=new Time32(0);p.ReviveStamp=Time32.Now.AddSeconds(-60);}
    static void Tick(int seconds){now=now.AddSeconds(seconds);Invoke(Field,"Tick");}
    static int Main(){
        var b=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION"));Environment.SetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION",null);
        if(b.Host!="127.0.0.1" || b.Port!=25439 || b.Database!="revora_candidate_game")throw new InvalidOperationException("Fixed private fixture required.");
        DataHolder.ConnectionString=b.ConnectionString;string prior=Environment.CurrentDirectory;bool owned=false;
        try {
            Sql("CREATE SCHEMA revora_kingdom_field_fixture;COMMENT ON SCHEMA revora_kingdom_field_fixture IS 'revora-owned-kingdom-field-v1'");owned=true;
            b.SearchPath="revora_kingdom_field_fixture";DataHolder.ConnectionString=b.ConnectionString;
            const string runtime=@"C:\Revora\DisposablePostgres\moon-box-candidate\game";Environment.CurrentDirectory=runtime;
            foreach(string table in new[]{"entities","items","titles","skills","arena","teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding","chi","npcs","sobnpcs","monsterspawns","furniture","unions"})Sql(table=="entities"?"CREATE TABLE entities AS TABLE public.entities WITH NO DATA":"CREATE TABLE "+table+" (LIKE public."+table+" INCLUDING ALL)");
            Sql("CREATE TABLE guilds(id bigint PRIMARY KEY,wins bigint NOT NULL,losts bigint NOT NULL,leadername text NOT NULL DEFAULT '');CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
            fixtures=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"KingdomField.Helpers.exe")).GetType("PotencyHuntTests",true);
            ConquerItemInformation.Load();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
            Invoke(typeof(Entity).Assembly.GetType("Revora.Game.Features.ProgressionEvidence",true),"Initialize");Invoke(typeof(Entity).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkLedger",true),"Initialize");
            Invoke(fixtures.Assembly.GetType("CompanionTests",true),"Reset");var town=(Map)Helper("World",runtime,(ushort)1002);var field=(Map)Helper("World",runtime,(ushort)3998);Server.Vars=new VariableVault();
            Invoke(Field,"ConfigureStartup",new object[]{null});Check(!(bool)Field.GetProperty("Enabled",Hidden).GetValue(null,null) && !town.Npcs.ContainsKey(903980),"omitted startup setting leaves the staged event disabled");
            bool malformed=false;try{Invoke(Field,"ConfigureStartup","not-a-boolean");}catch(InvalidOperationException){malformed=true;}
            Check(malformed && !(bool)Field.GetProperty("Enabled",Hidden).GetValue(null,null),"malformed startup setting fails closed before event activation");
            Check(field.Floor[296,153,MapObjectType.Player] && !field.Floor[114,300,MapObjectType.Player],"loaded Floor confirms zero-byte walkability and rejects old blocked anchor");
            Invoke(Field,"ValidateField",field.Floor);Check(true,"all quarry and depot approaches connect with occupied actor tiles excluded");
            town.AddNpc(new NpcSpawn{UID=903980,MapID=1002,X=310,Y=310,Mesh=10,Type=Enums.NpcType.Talker});bool collision=false;
            try{Invoke(Field,"Initialize");}catch(InvalidOperationException){collision=true;}
            town.RemoveNpc(town.Npcs[903980]);Check(collision && !field.Npcs.ContainsKey(903981),"identity collision cannot partially add field actors");
            town.AddNpc(new NpcSpawn{UID=380,MapID=1002,X=319,Y=325,Mesh=10,Name="GuildController",Type=Enums.NpcType.Talker});
            town.AddNpc(new NpcSpawn{UID=10003,MapID=1002,X=317,Y=321,Mesh=10,Name="GuildDirector",Type=Enums.NpcType.Talker});
            Check(!town.Floor[319,324,MapObjectType.Player],"loaded guild controller footprint blocks the retired town return tile");
            Invoke(Field,"Initialize");Check(town.Npcs.ContainsKey(903980) && field.Npcs.Count==4,"entry and four field actors stage on loaded maps");
            Check(town.Floor[316,325,MapObjectType.Player] && field.Npcs.Values.All(n=>field.Floor[n.X+2,n.Y,MapObjectType.Player]),"town return and every field approach remain walkable after actor footprints are placed");
            var a=(GameState)Helper("PrizeLeader",911u);var outsider=(GameState)Helper("PurseMember",a,Enums.GuildMemberRank.Member);
            Sql("INSERT INTO unions(id,name,leaderuid,leadername) VALUES(91,'FieldUnion',@uid,@name)","uid",(long)a.Entity.UID,"name",a.Entity.Name);Sql("INSERT INTO unions(id,name,leaderuid,leadername) VALUES(92,'RivalUnion',@uid,@name)","uid",(long)outsider.Entity.UID,"name",outsider.Entity.Name);
            Revora.Union.UnionTable.Load();a.Union=Kernel.Unions[91];a.UnionID=91;
            Check(a.Account.State==AccountTable.AccountState.Player && outsider.Account.State==AccountTable.AccountState.Player && !a.Fake && !outsider.Fake,"NPC journey uses ordinary non-GM players");
            var zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");now=TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026,10,12,16,59,59,DateTimeKind.Unspecified),zone);Field.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
            Town(a);Talk(a,903980,1);Check(a.Entity.MapID==1002 && Count("SELECT count(*) FROM revora_kingdom_entries")==0,"closed NPC cannot admit a player");
            now=now.AddSeconds(1);Town(outsider);Talk(outsider,903980,1);Check(outsider.Entity.MapID==1002,"unionless ordinary player cannot enter");
            a.Entity.X=350;Talk(a,903980,1);Check(a.Entity.MapID==1002,"remote entry dialogue cannot bypass proximity");
            Town(a);a.Entity.PKMode=Enums.PKMode.Peace;Talk(a,903980,1);
            Check(a.Entity.MapID==3998 && ReferenceEquals(a.Map,field) && a.Entity.X==296 && a.Entity.Y==153 && a.Entity.PKMode==Enums.PKMode.Union,"ordinary entry saves admission then transfers to the validated field");
            Check(Count("SELECT count(*) FROM revora_kingdom_entries WHERE union_id=91 AND pkmode=1 AND NOT returned")==1,"entry snapshots prior PK mode durably");
            At(a,903982);Talk(a,903982,1);Talk(a,903982,1);Tick(4);Check(Count("SELECT count(*) FROM revora_kingdom_entries WHERE carried")==0,"early repeated quarry dialogue grants no brick");
            a.LastMove=a.LastMove.AddMilliseconds(1);Tick(1);Check(Count("SELECT count(*) FROM revora_kingdom_entries WHERE nonce IS NOT NULL")==0,"moving away and back cancels via movement stamp");
            Talk(a,903982,1);Tick(5);Check(Count("SELECT count(*) FROM revora_kingdom_entries WHERE carried")==1,"stationary quarry work produces one durable cargo");
            Talk(a,903981,1);Check(Count("SELECT count(*) FROM revora_kingdom_deliveries")==0,"remote bank request cannot score");
            At(a,903981);Talk(a,903981,1);Talk(a,903981,1);Check(Count("SELECT count(*) FROM revora_kingdom_deliveries")==1,"ordinary keeper dialog banks exactly one point");
            // Public combat handlers, with independently admitted ordinary opponents.
            var ally=(GameState)Helper("PurseMember",a,Enums.GuildMemberRank.Member);ally.Union=Kernel.Unions[91];ally.UnionID=91;
            Town(ally);Talk(ally,903980,1);outsider.Union=Kernel.Unions[92];outsider.UnionID=92;Town(outsider);Talk(outsider,903980,1);
            foreach(var p in new[]{a,ally,outsider}){At(p,903981);Ready(p);}
            Check(!Can(a,outsider) && !Can(outsider,a),"new admissions have ten-second protection in both combat directions");
            outsider.Entity.Die(a.Entity);Check(!outsider.Entity.Dead,"direct death cannot bypass entry protection");Tick(10);
            Check(Can(a,outsider) && Can(outsider,a) && !Can(a,ally) && !Can(a,a),"only opposing admitted unions can fight");
            uint allyHp=ally.Entity.Hitpoints;Hit(a,ally,1);ally.Entity.Die(a.Entity);Check(ally.Entity.Hitpoints==allyHp && !ally.Entity.Dead,"same-union direct damage and direct death cannot bypass admission rules");
            outsider.Entity.AddFlag((ulong)FlagsUpdate.Flags.Fly,30,true);Check(!Can(a,outsider),"field combat preserves melee flight restriction");outsider.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Fly);
            outsider.Attackable=false;uint rivalHp=outsider.Entity.Hitpoints;Hit(a,outsider,1);outsider.Entity.Die(a.Entity);Check(!Can(a,outsider) && outsider.Entity.Hitpoints==rivalHp,"protected target rejects direct damage");Ready(outsider);
            a.CantAttack=Time32.Now.AddSeconds(60);Check(!Can(a,outsider),"attacker cooldown remains enforced");Ready(a);
            outsider.Fake=true;Check(!Can(a,outsider),"fake participant cannot fight");outsider.Fake=false;
            a.Entity.PKMode=Enums.PKMode.Peace;Check(!Can(a,outsider),"peace mode cannot attack inside the contest");a.Entity.PKMode=Enums.PKMode.Union;
            At(outsider,903982);Talk(outsider,903982,1);Tick(5);Check(Count("SELECT count(*) FROM revora_kingdom_entries WHERE union_id=92 AND carried")==1,"combat victim carries a completed brick");At(a,903982);Ready(a);Ready(outsider);
            outsider.Entity.Money=123456;outsider.Entity.ConquerPoints=789;outsider.Entity.Experience=12345;
            ConquerItem.ItemUID=new Counter(8900000);Check(outsider.Inventory.Add(new ConquerItem(true){ID=1088001,StackSize=1,Bound=false},Enums.ItemUse.CreateAndAdd) && EntityTable.SaveEntity(outsider),"combat fixture saves nonzero wealth and an unbound inventory item");
            outsider.Entity.PKPoints=100;a.Spells.Add(40307,new Spell(true){ID=40307,Level=1});
            var gold=outsider.Entity.Money;var cps=outsider.Entity.ConquerPoints;var xp=outsider.Entity.Experience;var items=outsider.Inventory.Objects.Select(i=>i.UID).OrderBy(x=>x).ToArray();
            ushort pk=a.Entity.PKPoints;int kills=a.Entity.KillCount;uint bricks=a.Entity.PuntosUnion;string savedWealth=Wealth(a,outsider),savedItems=StoredItems(a,outsider);
            Hit(a,outsider,1);Check(!outsider.Entity.Dead && outsider.Entity.Hitpoints<rivalHp,"ordinary opposing-union hit applies nonlethal damage");
            Hit(a,outsider,outsider.Entity.Hitpoints);Check(outsider.Entity.Dead && outsider.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Dead),"ordinary lethal hit sends the standard player death state");
            outsider.Entity.DropRandomStuff(a.Entity);outsider.Entity.Die(a.Entity);
            Check(a.Entity.PKPoints==pk && a.Entity.KillCount==kills && a.Entity.PuntosUnion==bricks && !a.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.FlashingName)
                && outsider.Entity.Money==gold && outsider.Entity.ConquerPoints==cps && outsider.Entity.Experience==xp && items.SequenceEqual(outsider.Inventory.Objects.Select(i=>i.UID).OrderBy(x=>x)),"field death and replay preserve purses, experience, items and legacy kill counters");
            Check(savedWealth==Wealth(a,outsider) && savedItems==StoredItems(a,outsider),"combat and duplicate death preserve exact persisted economy and item rows");
            Check(Count("SELECT count(*) FROM revora_kingdom_entries WHERE union_id=92 AND carried")==0 && Count("SELECT count(*) FROM revora_kingdom_deliveries")==1,"actual death loses cargo and grants no deposit credit");
            outsider.Entity.BringToLife();Ready(outsider);Check(Can(a,outsider),"revived participant can fight again");Hit(a,outsider,outsider.Entity.Hitpoints);Check(outsider.Entity.Dead,"a second life can be defeated without stale killed-state immunity");
            outsider.Entity.BringToLife();Ready(outsider);At(outsider,903981);Talk(outsider,903981,2);At(ally,903981);Talk(ally,903981,2);
            Check(!Can(a,outsider) && !Can(outsider,a),"field boundary blocks attacks in both directions after departure");
            Town(a);Ready(a);Ready(outsider);Revora.Game.Features.PlanderWar.open=true;Check(!Can(a,outsider),"legacy open flag cannot authorize Union attacks in Twin City");Revora.Game.Features.PlanderWar.open=false;Tick(1);
            Town(a);Talk(a,903980,1);Tick(10);
            At(a,903983);Talk(a,903983,1);Tick(5);a.Entity.DeathStamp=a.Entity.DeathStamp.AddMilliseconds(1);At(a,903981);Talk(a,903981,1);
            Check(Count("SELECT count(*) FROM revora_kingdom_deliveries")==1 && Count("SELECT count(*) FROM revora_kingdom_entries WHERE carried")==0,"death followed by revival before pulse still loses the cargo");
            At(a,903984);Talk(a,903984,1);Tick(5);
            Check(Count("SELECT count(*) FROM revora_kingdom_entries WHERE carried")==1,"exit failure fixture has completed cargo");At(a,903981);
            Sql("CREATE FUNCTION reject_return() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_return BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION reject_return()");
            bool returnFailed=false;try{Invoke(typeof(Entity).Assembly.GetType("Revora.Game.Features.KingdomCompetitionLedger",true),"Return",a.Entity.UID);}catch(InvalidOperationException){returnFailed=true;}
            Check(returnFailed && a.Entity.MapID==3998 && a.Entity.PKMode==Enums.PKMode.Union && Count("SELECT count(*) FROM revora_kingdom_entries WHERE NOT returned AND carried")==1,"failed position write rolls back return and preserves cargo for retry");
            Sql("DROP TRIGGER reject_return ON entities;DROP FUNCTION reject_return()");Talk(a,903981,2);
            Check(a.Entity.MapID==1002 && a.Entity.PKMode==Enums.PKMode.Peace && Count("SELECT count(*) FROM revora_kingdom_entries WHERE returned AND NOT carried")==3,"voluntary exit saves safe position, discards cargo and restores prior PK mode");
            Town(a);Talk(a,903980,1);At(a,903982);Talk(a,903982,1);Tick(5);Check(EntityTable.SaveEntity(a),"ordinary entrant saves before interrupted-session reload");
            var reloaded=(GameState)Helper("ReloadGold",a);
            Check(reloaded.Entity.MapID==1002 && reloaded.Entity.X==316 && reloaded.Entity.Y==325 && reloaded.Entity.PKMode==Enums.PKMode.Peace
                && Count("SELECT count(*) FROM revora_kingdom_entries WHERE NOT returned OR carried")==0,"normal character loader returns interrupted admission without cargo or PK-mode loss");
            Check(reloaded.UnionID==91 && reloaded.Entity.CharBuffer._UnionID==91 && ReferenceEquals(reloaded.Union,Kernel.Unions[91]),"normal reload restores union identity in runtime and spawn projection");
            a=reloaded;Town(a);Talk(a,903980,1);Check(a.Entity.MapID==3998,"reloaded ordinary union member can re-enter through the public clerk");At(a,903982);Talk(a,903982,1);Tick(5);
            Town(outsider);Talk(outsider,903980,1);At(outsider,903982);Ready(outsider);Ready(a);Tick(10);Check(Can(a,outsider),"re-entry restores opposing combat before close");
            now=TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026,10,12,18,0,0,DateTimeKind.Unspecified),zone);uint closingHp=outsider.Entity.Hitpoints;Hit(a,outsider,1);
            Check(!Can(a,outsider) && outsider.Entity.Hitpoints==closingHp,"closing time refuses damage before the return pulse runs");Invoke(Field,"Tick");
            Check(a.Entity.MapID==1002 && Count("SELECT count(*) FROM revora_kingdom_contests WHERE state='final' AND winner=91 AND score=1")==1,"closing pulse returns entrant and settles only deposited score");
            Check(Count("SELECT count(*) FROM revora_kingdom_deliveries")==1,"return/reload/closing cannot create another deposit");
            now=TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026,10,13,17,0,0,DateTimeKind.Unspecified),zone);
            Town(a);Talk(a,903980,1);At(a,903982);Talk(a,903982,1);Tick(5);
            Check(EntityTable.SaveEntity(a) && Count("SELECT count(*) FROM revora_kingdom_entries WHERE NOT returned AND carried")==1,"interrupted entrant persists carried cargo before disabled-startup recovery");
            // Simulate a fresh process with activation off, retaining only durable recovery data.
            Field.GetField("<Enabled>k__BackingField",Hidden).SetValue(null,false);
            Field.GetField("recoveryReady",Hidden).SetValue(null,false);Invoke(Field,"ConfigureStartup","false");
            a=(GameState)Helper("ReloadGold",a);
            Check(!(bool)Field.GetProperty("Enabled",Hidden).GetValue(null,null) && a.Entity.MapID==1002 && a.Entity.X==316 && a.Entity.Y==325
                && a.Entity.PKMode==Enums.PKMode.Peace && Count("SELECT count(*) FROM revora_kingdom_entries WHERE NOT returned OR carried")==0,
                "disabled startup still returns interrupted ordinary login and restores PK mode without cargo");
            Check(Count("SELECT count(*) FROM entities WHERE uid=@uid AND mapid=1002 AND x=316 AND y=325","uid",(long)a.Entity.UID)==1,"disabled recovery commits the safe return position");
            a=(GameState)Helper("ReloadGold",a);Talk(a,903980,1);Invoke(Field,"Tick");
            Check(a.Entity.MapID==1002 && Count("SELECT count(*) FROM revora_kingdom_deliveries")==1,"disabled recovery replay cannot re-admit or manufacture score");
            Console.WriteLine("Kingdom NPC field checks passed: "+checks);return 0;
        }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message+"\n"+e.StackTrace);return 1;}
        finally {
            Environment.CurrentDirectory=prior;NpgsqlConnection.ClearAllPools();b.SearchPath="public";DataHolder.ConnectionString=b.ConnectionString;
            if(owned)Sql("DROP SCHEMA revora_kingdom_field_fixture CASCADE");DataHolder.ConnectionString=null;
        }
    }
}
