using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Console = System.Console;

internal static partial class PotencyHuntTests
{
    static readonly BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
    static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Type Hunt = typeof(Entity).Assembly.GetType("Revora.Game.Features.PotencyHunt");
    static readonly Type Packs = typeof(Entity).Assembly.GetType("Revora.Game.Features.PotencyPacks");
    static readonly Type Guide = typeof(Entity).Assembly.GetType("Revora.Game.Features.PotencyGuide");
    static int checks;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; Console.WriteLine("PASS: " + name); }
    static object Call(Type type, object target, string method, params object[] args)
    { try { return type.GetMethod(method, target == null ? Hidden : Instance).Invoke(target, args); } catch (TargetInvocationException e) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; } }
    static void Query(string sql, params object[] values)
    {
        using (var c = DataHolder.MySqlConnection) { c.Open(); using (var q = new NpgsqlCommand(sql, c)) {
            for (int i=0; i<values.Length; i+=2) q.Parameters.AddWithValue((string)values[i], values[i+1]); q.ExecuteNonQuery();
        }}
    }
    static long Scalar(string sql, uint uid)
    {
        using (var c = DataHolder.MySqlConnection) { c.Open(); using (var q = new NpgsqlCommand(sql, c)) {
            q.Parameters.AddWithValue("uid", (long)uid); return Convert.ToInt64(q.ExecuteScalar());
        }}
    }
    static Map World(string runtime, ushort id)
    {
        var map = (Map)typeof(CompanionTests).GetMethod("World", Hidden).Invoke(null, new object[] { id });
        using (var reader = new BinaryReader(File.OpenRead(Path.Combine(runtime,"database/maps/"+id+".map"))))
        {
            int width = reader.ReadInt32(), height = reader.ReadInt32();
            map.Floor = new Floor(width, height, id);
            for (int y=0;y<height;y++) for(int x=0;x<width;x++) map.Floor[x,y,MapObjectType.InvalidCast] = reader.ReadByte()!=1;
        }
        map.Npcs = new Dictionary<uint, INpc>();
        map.Zones = new List<Zoning.Zone>();
        map.FloorItems = new ConcurrentDictionary<uint, FloorItem>();
        map.EntityUIDCounter = new Counter(400000);
        foreach(var field in typeof(Map).GetFields(BindingFlags.Public | BindingFlags.Instance))
            if(field.GetValue(map)==null && field.FieldType.IsGenericType && field.FieldType.GetConstructor(Type.EmptyTypes)!=null)
                field.SetValue(map,Activator.CreateInstance(field.FieldType));
        MapsTable.MapInformations[id] = new MapsTable.MapInformation { ID=id, BaseID=id, Status=2 };
        return map;
    }
    static GameState Player(Map map)
    {
        var p = (GameState)typeof(CompanionTests).GetMethod("Player", Hidden).Invoke(null, new object[]{map,false,(ushort)561,(ushort)549,(byte)15});
        foreach(var field in typeof(Screen).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            if(field.GetValue(p.Screen)==null && field.FieldType.IsGenericType && field.FieldType.GetConstructor(Type.EmptyTypes)!=null)
                field.SetValue(p.Screen,Activator.CreateInstance(field.FieldType));
        p.Action=2; p.Account.State=AccountTable.AccountState.Player;
        p.ItemSyncRoot=new object(); p.Trade=new Revora.Game.ConquerStructures.Trade();
        p.InnerPower=new InnerPower(p.Entity.Name,p.Entity.UID);
        Query("INSERT INTO entities(uid,name,unionexploits) VALUES(@uid,@name,0)","uid",(long)p.Entity.UID,"name",p.Entity.Name);
        return p;
    }
    static ConquerItem Pack(GameState p,uint id,ushort amount)
    {
        var item=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=id,StackSize=amount};
        Query("INSERT INTO items(uid,id,entityid,stacksize) VALUES(@uid,@id,@owner,@amount)","uid",(long)item.UID,"id",(int)id,"owner",(long)p.Entity.UID,"amount",(int)amount);
        p.Inventory.Add(item,Enums.ItemUse.None); return item;
    }
    static bool Fenced(GameState p) { return (bool)typeof(Entity).GetField("PersistenceRequiresReload",Instance).GetValue(p.Entity); }

    static void VerifyRetiredBossRewards(Map map)
    {
        Query("ALTER TABLE entities ADD COLUMN conquerpoints bigint NOT NULL DEFAULT 0, ADD COLUMN money bigint NOT NULL DEFAULT 0, ADD COLUMN revorapoint bigint NOT NULL DEFAULT 0");
        var player=Player(map);
        Server.GamePool=new GameState[0];
        bool enabled=Revora.Config.KillConquerPointsEnabled;
        bool moneyDrops=Revora.Config.MoneyDropEnabled;
        int chance5=Revora.Config.KillConquerPointsChance5, chance50=Revora.Config.KillConquerPointsChance50, chance250=Revora.Config.KillConquerPointsChance250;
        try
        {
            Revora.Config.KillConquerPointsEnabled=true;
            // Isolate the removed special purses from ordinary random ground loot.
            Revora.Config.MoneyDropEnabled=false;
            Revora.Config.KillConquerPointsChance5=Revora.Config.KillConquerPointsChance50=Revora.Config.KillConquerPointsChance250=0;
            player.Entity.VIPLevel=Revora.Config.KillConquerPointsMinVip;
            foreach(string name in new[]{"WhiteDragon","GreatPheasant(Nor.1)","SlayerSoldier"})
            {
                var info=MonsterInformation.MonsterInformations.Values.First(m=>m.Name==name).Copy();
                info.OwnItemRate=0;
                var monster=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=map.ID,X=570,Y=550,Name=name,MonsterInfo=info};
                info.Owner=monster;
                uint cps=player.Entity.ConquerPoints;
                var money=player.Entity.Money; var points=player.Entity.RevoraPoint;
                int ground=map.FloorItems.Count, inventory=player.Inventory.Count;
                Kernel.SoldadoSlayer1=true;
                info.Drop(player.Entity);
                Check(player.Entity.ConquerPoints==cps+Revora.Config.KillConquerPointsAlways,
                    name+" grants only the configured base kill CP reward when bonus rolls are off");
                Check(player.Entity.Money==money && player.Entity.RevoraPoint==points
                    && map.FloorItems.Count==ground && player.Inventory.Count==inventory,
                    name+" grants no retired gold, RVP or pack dump");
                if(name=="SlayerSoldier") Check(!Kernel.SoldadoSlayer1,"SlayerSoldier still clears its summon flag");
                else Check(Kernel.SoldadoSlayer1,name+" does not clear SlayerSoldier's summon flag");
                Revora.Config.KillConquerPointsEnabled=false;
                cps=player.Entity.ConquerPoints;
                info.Drop(player.Entity);
                Check(player.Entity.ConquerPoints==cps,name+" respects the disabled normal kill CP reward");
                Revora.Config.KillConquerPointsEnabled=true;
            }
            Check(Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",player.Entity.UID)==player.Entity.ConquerPoints,
                "normal boss kill CP awards persist for an ordinary player");
        }
        finally
        {
            Revora.Config.KillConquerPointsEnabled=enabled;
            Revora.Config.MoneyDropEnabled=moneyDrops;
            Revora.Config.KillConquerPointsChance5=chance5; Revora.Config.KillConquerPointsChance50=chance50; Revora.Config.KillConquerPointsChance250=chance250;
        }
    }

    static int Main(string[] args)
    {
        if(args.Length==1 && args[0]=="--forgotten-restart-check")return ForgottenRestartChild();
        if(args.Length==1 && args[0]=="--flower-restart-check")return FlowerRestartChild();
        if(args.Length==1 && args[0]=="--fashion-restart-check")return FashionRestartChild();
        if(args.Length==1 && args[0]=="--planet-collection-restart-check")return FullPlanetCollectionRestartChild();
        if(args.Length==1 && args[0]=="--planet-restart-check")return PlanetRestartChild();
        if(args.Length==1 && args[0]=="--heart-restart-check")return HeartRestartChild();
        if(args.Length==1 && args[0]=="--career-restart-check")return CareerRestartChild();
        string runtime=Environment.CurrentDirectory, original=null;
        bool schema=false;
        fullStarterSchema=args.Length==1 && (args[0]=="--revora-starter-only" || args[0]=="--second-token-shop-only" || args[0]=="--souls-only");
        string fixture=Path.GetFullPath(Path.Combine(runtime,"../../obj/chi-refill-validation/potency-fixture"));
        try
        {
            var ini=new IniFile("Configuracion.ini");
            string host=ini.ReadString("MySql","Host"),port=ini.ReadString("MySql","Port");
            if(!string.IsNullOrEmpty(port) && host.IndexOf("Port=",StringComparison.OrdinalIgnoreCase)<0) host=host.Trim().TrimEnd(';')+";Port="+port;
            string isolated=Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION");
            if(fullStarterSchema && string.IsNullOrEmpty(isolated)) throw new InvalidOperationException("The starter suite requires its explicit disposable test connection.");
            if(!string.IsNullOrEmpty(isolated)) {
                var target=new NpgsqlConnectionStringBuilder(isolated);
                if(target.Host!="127.0.0.1" || target.Port!=25439 || target.Database!="revora_candidate_game")
                    throw new InvalidOperationException("Explicit test connection must target the disposable loopback database.");
                DataHolder.ConnectionString=target.ConnectionString;
            } else {
            DataHolder.CreateConnection(ini.ReadString("MySql","Username"),ini.ReadString("MySql","Password"),ini.ReadString("MySql","Database"),host);
            }
            original=DataHolder.ConnectionString;
            if(args.Length==1 && args[0]=="--cleanup-owned-fixture") {
                Query("DROP SCHEMA IF EXISTS revora_potency_validation CASCADE");return 0;
            }
            ConquerItemInformation.Load(); MonsterInformation.Load(); InnerPowerTable.LoadDBInformation();
            MonsterInformation baseline=MonsterInformation.MonsterInformations[1413];
            Check(baseline.Name=="AlienSerpent", "baseline is the installed AlienSerpent");
            Query("CREATE SCHEMA revora_potency_validation"); schema=true;
            Query(fullStarterSchema ? "CREATE TABLE revora_potency_validation.entities AS TABLE public.entities WITH NO DATA;ALTER TABLE revora_potency_validation.entities ADD PRIMARY KEY(uid)" : "CREATE TABLE revora_potency_validation.entities(uid bigint PRIMARY KEY,name text NOT NULL,unionexploits bigint NOT NULL DEFAULT 0)");
            Query("CREATE TABLE revora_potency_validation.items (LIKE public.items INCLUDING ALL)");
            Query("CREATE TABLE revora_potency_validation.skills AS TABLE public.skills WITH NO DATA");
            Query("CREATE TABLE revora_potency_validation.arena AS TABLE public.arena WITH NO DATA");
            Query("CREATE TABLE revora_potency_validation.monsterspawns (LIKE public.monsterspawns INCLUDING ALL)");
            Query("INSERT INTO revora_potency_validation.monsterspawns SELECT s.* FROM public.monsterspawns s JOIN public.monsterinfos m ON m.id=s.npctype WHERE m.name='Pheasant'");
            var builder=new NpgsqlConnectionStringBuilder(original);builder.SearchPath="revora_potency_validation";DataHolder.ConnectionString=builder.ConnectionString;
            Call(Packs,null,"Initialize");
            Call(FirstRite,null,"Initialize");
            Call(SecondRite,null,"Initialize");
            Call(PaidRite,null,"Initialize");
            Call(AssassinTrainer,null,"Initialize");
            Call(NinjaTrainer,null,"Initialize");
            Call(TokenShop,null,"Initialize");
            Call(ExpItems,null,"Initialize");
            Call(ExpPotions,null,"Initialize");
            Call(QuestGifts,null,"Initialize");
            Call(PrizePacks,null,"Initialize");
            Call(Magnolia,null,"Initialize");
            Call(PriceQuest,null,"Initialize");
            Call(RareQuest,null,"Initialize");
            Call(SoulBall,null,"Initialize");
            Call(Anniversary,null,"Initialize");
            Call(ExorcismGifts,null,"Initialize");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ExorcismQuest"),null,"Initialize");
            Call(EvidenceType,null,"Initialize");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorDepartures"),null,"Initialize");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorStoneClaims"),null,"Initialize");
            Call(typeof(Server).Assembly.GetType("Revora.Game.Features.MentorBlessingClaims"),null,"Initialize");
            if(fullStarterSchema) Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkLedger"),null,"Initialize");
            if(fullStarterSchema) { Call(ProgressionStore,null,"Initialize");Query("ALTER TABLE entities ALTER COLUMN revora_attribute_bonus SET DEFAULT 0"); }
            typeof(CompanionTests).GetMethod("Reset",Hidden).Invoke(null,null);
            var map=World(runtime,1217); var town=World(runtime,1002);
            if(args.Length==1 && args[0]=="--forgotten-only") { VerifyForgotten(runtime,town);Console.WriteLine("Forgotten checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--planet-only") { VerifyPlanet(runtime,town);Console.WriteLine("Planet checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--guild-beast-funding-only") { VerifyGuildBeastFunding();Console.WriteLine("Guild Beast funding checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--guild-season-only") { VerifyGuildSeason(runtime);Console.WriteLine("Guild season checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--summer-trade-only") { VerifySummer(runtime,town,true);Console.WriteLine("Summer trade checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--grotto-only") { VerifyGrotto(runtime,town);Console.WriteLine("Grotto passage checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--travel-only") { VerifyTravel(runtime,town);Console.WriteLine("Travel checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--shelby-only") { VerifyShelby(runtime,town);Console.WriteLine("Shelby checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--cosmetic-ownership-only") { VerifyCosmeticOwnership(town);Console.WriteLine("Cosmetic ownership checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--appearance-only") { VerifyAppearance(town);Console.WriteLine("Appearance checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--bp-pk-only") { VerifyBpPk(runtime,town);Console.WriteLine("BP PK checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--battle-power-only") { VerifyBattlePower(town);Console.WriteLine("Battle Power checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--monster-equipment-only") { VerifyMonsterEquipment(town);Console.WriteLine("Monster equipment checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--praying-stones-only") { VerifyPrayingStones(town);Console.WriteLine("Praying Stone checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--mentor-blessings-only") { VerifyMentorBlessings(town);Console.WriteLine("Mentor blessing checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--mentor-stones-only") { VerifyMentorStones(town);Console.WriteLine("Mentor stone checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--mentor-departures-only") { VerifyMentorDepartures(town);Console.WriteLine("Mentor departure checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--mentor-admissions-only") { VerifyMentorAdmissions(town);Console.WriteLine("Mentor admission checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--partners-only") { VerifyPartners(town);Console.WriteLine("Partner checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--friends-only") { VerifyFriends(town);Console.WriteLine("Friend checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--auction-only") { VerifyAuctions(town);Console.WriteLine("Auction checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--experience-only") { VerifyExperience(town);Console.WriteLine("Experience checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--jades-only") { VerifyJades(town);Console.WriteLine("Jade checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--dis-city-only") { VerifyDisCity(runtime,town);Console.WriteLine("Dis City checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--mail-only") { VerifyMail(town);Console.WriteLine("Mail checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--refinery-only") { VerifyRefinery(town);Console.WriteLine("Refinery checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--anniversary-only") { VerifyAnniversary(runtime,town);Console.WriteLine("Anniversary checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--exorcism-only") { VerifyExorcismQuest(runtime,town);Console.WriteLine("Exorcism quest checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--exorcism-rewards-only") { VerifyExorcismRewards(runtime,town);Console.WriteLine("Exorcism reward checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--soul-ball-only") { VerifySoulBall(runtime,town);Console.WriteLine("Soul Ball checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--rare-quest-only") { VerifyRareQuest(runtime,town);Console.WriteLine("Rare Materials checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--price-quest-only") { VerifyPriceQuest(runtime,town);Console.WriteLine("Daily Price Quest checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--hunt-loot-only") { VerifyHuntLoot(runtime,town);Console.WriteLine("Daily hunt loot checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--magnolia-only") { VerifyMagnolia(runtime,town);Console.WriteLine("Magnolia checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--potions-only") { VerifyExperiencePotions(runtime,town);Console.WriteLine("EXP potion checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--packs-only") { VerifyFirstRebirth(runtime,town);VerifyTournamentExpPacks(town);Console.WriteLine("EXP pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--protection-only") { VerifyFirstRebirth(runtime,town);VerifyTimeDropIssuanceClosed(town);VerifyExperienceProtection(town);Console.WriteLine("EXP protection checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--combine-only") { VerifyFirstRebirth(runtime,town);VerifyExpCareCombine(town);Console.WriteLine("EXPCare combine checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--timedrop-only") { VerifyFirstRebirth(runtime,town);VerifyTimeDropHourglasses(town);Console.WriteLine("TimeDrop hourglass checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--welcome-only") { VerifyFirstRebirth(runtime,town);VerifyWelcomePacks(runtime,town);Console.WriteLine("Welcome pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--revora-starter-only") { VerifyFirstRebirth(runtime,town);VerifyRevoraStarter(runtime,town);VerifyBoundOnlyMaterials(town);Console.WriteLine("Revora starter checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--starter-only") { VerifyFirstRebirth(runtime,town);VerifyStarterPacks(runtime,town);Console.WriteLine("Starter pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--cp-bags-only") { VerifyFirstRebirth(runtime,town);VerifyConquerPointBags(runtime,town);Console.WriteLine("CP bag checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--purse-compatibility-only") { VerifyFirstRebirth(runtime,town);VerifyPurseCompatibility(town);Console.WriteLine("Purse compatibility checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--inner-progress-only"){VerifyFirstRebirth(runtime,town);VerifyInnerProgress(runtime,town);Console.WriteLine("Inner progress checks passed: "+checks);return 0;}
            if(args.Length==1 && args[0]=="--perfection-temper-only"){VerifyFirstRebirth(runtime,town);VerifyPerfectionTempering(town);VerifyPerfectionMaterialAcquisition(runtime,town,fixture);Console.WriteLine("Perfection tempering checks passed: "+checks);return 0;}
            if(args.Length==1 && args[0]=="--talent-public-only"){VerifyFirstRebirth(runtime,town);VerifyTalentEarning(runtime,town,fixture);Console.WriteLine("Talent earning checks passed: "+checks);return 0;}
            if(args.Length==1 && args[0]=="--grandmaster-public-only"){VerifyFirstRebirth(runtime,town);VerifyGrandmasterEarning(runtime,town,fixture);Console.WriteLine("Grandmaster earning checks passed: "+checks);return 0;}
            if(args.Length==1 && args[0]=="--homecoming-public-only"){VerifyFirstRebirth(runtime,town);VerifyHomecomingPublicTiers(town);Console.WriteLine("Homecoming public checks passed: "+checks);return 0;}
            if(args.Length==1 && args[0]=="--mrms2016-public-only"){VerifyFirstRebirth(runtime,town);VerifyMrMs2016PublicEarning(town);Console.WriteLine("MrMs2016 public checks passed: "+checks);return 0;}
            if(args.Length==1 && args[0]=="--tempest-public-only"){VerifyFirstRebirth(runtime,town);VerifyTempestPublic(runtime,town);Console.WriteLine("Tempest public checks passed: "+checks);return 0;}
            if(args.Length==1 && args[0]=="--tempest-encounter-only"){VerifyFirstRebirth(runtime,town);VerifyTempestEncounter(runtime,town);Console.WriteLine("Tempest encounter checks passed: "+checks);return 0;}
            if(args.Length==1 && args[0]=="--tempest-permit-only"){VerifyFirstRebirth(runtime,town);VerifyTempestPermit(runtime,town);Console.WriteLine("Tempest permit checks passed: "+checks);return 0;}
            if(args.Length==1 && args[0]=="--fan-public-only"){VerifyFirstRebirth(runtime,town);VerifyDefenseTalismanQuest(runtime,town,false,true);Console.WriteLine("Fan public checks passed: "+checks);return 0;}
            if(args.Length==1 && args[0]=="--fan-opening-only"){VerifyFirstRebirth(runtime,town);VerifyDefenseTalismanQuest(runtime,town,true);Console.WriteLine("Fan opening checks passed: "+checks);return 0;}
            if(args.Length==1 && args[0]=="--tower-route-only"){VerifyFirstRebirth(runtime,town);VerifyDefenseTalismanQuest(runtime,town);Console.WriteLine("Defense Talisman route checks passed: "+checks);return 0;}
            if(args.Length==1 && args[0]=="--tower-store-only"){VerifyFirstRebirth(runtime,town);VerifyDefenseTalismanStore(town);VerifyDefenseTalismanFishing(runtime,town);VerifyDefenseTalismanHouse(runtime,town);Console.WriteLine("Defense Talisman store checks passed: "+checks);return 0;}
            if(args.Length==1 && args[0]=="--expedition-population-only"){VerifyFirstRebirth(runtime,town);VerifyDesertExpeditionPopulation(runtime,town);Console.WriteLine("Desert Expedition population checks passed: "+checks);return 0;}
            if(args.Length==1 && args[0]=="--purse-review-only") { VerifyFirstRebirth(runtime,town);VerifyPurseReview(town);Console.WriteLine("Purse review checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--fixed-packs-only") { VerifyFirstRebirth(runtime,town);VerifyFixedOutputPacks(runtime,town);Console.WriteLine("Fixed pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--chi-items-only") { VerifyFirstRebirth(runtime,town);VerifyChiPointItems(runtime,town);Console.WriteLine("Chi item checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--vote-grants-only") { VerifyFirstRebirth(runtime,town);VerifyVoteGrants(runtime,town);Console.WriteLine("Vote grant checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--surprise-box-only") { VerifyFirstRebirth(runtime,town);VerifySurpriseBox(runtime,town);Console.WriteLine("Surprise box checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--silver-study-only") { VerifyFirstRebirth(runtime,town);VerifySilverStudyBags(runtime,town);Console.WriteLine("Silver/study bag checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--nested-gear-only") { VerifyFirstRebirth(runtime,town);VerifyNestedGearPacks(runtime,town);Console.WriteLine("Nested gear pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--silver-pouches-only") { VerifyFirstRebirth(runtime,town);VerifySilverPouches(runtime,town);Console.WriteLine("Silver pouch checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--medicine-bundles-only") { VerifyFirstRebirth(runtime,town);VerifyMedicineBundles(runtime,town);Console.WriteLine("Medicine bundle checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--study-books-only") { VerifyFirstRebirth(runtime,town);VerifyStudyBooks(runtime,town);Console.WriteLine("Study book checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--dragon-ball-packs-only") { VerifyFirstRebirth(runtime,town);VerifyDragonBallPacks(runtime,town);Console.WriteLine("DragonBall pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--prize-center-packs-only") { VerifyFirstRebirth(runtime,town);VerifyPrizeCenterPacks(runtime,town);Console.WriteLine("Prize Center pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--easter-gold-packs-only") { VerifyFirstRebirth(runtime,town);VerifyEasterGoldPacks(runtime,town);Console.WriteLine("Easter gold pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--daily-sign-in-packs-only") { VerifyFirstRebirth(runtime,town);VerifyDailySignInPacks(runtime,town);Console.WriteLine("Daily sign-in pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--ancient-stone-combine-only") { VerifyFirstRebirth(runtime,town);VerifyAncientStoneCombine(runtime,town);Console.WriteLine("Ancient Stone combine checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--star-stone-combine-only") { VerifyFirstRebirth(runtime,town);VerifyStarStoneCombine(runtime,town);Console.WriteLine("Star stone combine checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--attack-defense-potions-only") { VerifyFirstRebirth(runtime,town);VerifyAttackDefensePotions(runtime,town);Console.WriteLine("Attack defense potion checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--power-arena-packs-only") { VerifyFirstRebirth(runtime,town);VerifyPowerArenaPacks(runtime,town);Console.WriteLine("Power Arena pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--healing-medicine-only") { VerifyFirstRebirth(runtime,town);VerifyHealingMedicine(runtime,town);Console.WriteLine("Healing medicine checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--bloodshed-sea-only") { VerifyFirstRebirth(runtime,town);VerifyBloodshedSea(runtime,town);Console.WriteLine("Bloodshed Sea checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--vip-day-only") { VerifyFirstRebirth(runtime,town);VerifyVipDay(runtime,town);Console.WriteLine("VIP-day checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--disguise-amulet-only") { VerifyFirstRebirth(runtime,town);VerifyDisguiseAmulet(runtime,town);Console.WriteLine("Disguise amulet checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--honor-refinery-packs-only") { VerifyFirstRebirth(runtime,town);VerifyHonorRefineryPacks(runtime,town);Console.WriteLine("Honor refinery pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--gain-refinery-packs-only") { VerifyFirstRebirth(runtime,town);VerifyGainRefineryPacks(runtime,town);Console.WriteLine("Gain refinery pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--refinery-sibling-audit-only") { VerifyFirstRebirth(runtime,town);VerifyRefinerySiblingAudit(runtime,town);Console.WriteLine("Refinery sibling audit checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--hossu-hair-packs-only") { VerifyFirstRebirth(runtime,town);VerifyHossuHairPacks(runtime,town);Console.WriteLine("Hossu hair pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--hossu-inert-packs-only") { VerifyFirstRebirth(runtime,town);VerifyHossuInertPacks(runtime,town);Console.WriteLine("Hossu inert pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--epic-weapon-packs-only") { VerifyFirstRebirth(runtime,town);VerifyEpicWeaponPacks(runtime,town);Console.WriteLine("Epic weapon pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--yellow-blue-rune-packs-only") { VerifyFirstRebirth(runtime,town);VerifyYellowBlueRunePacks(runtime,town);Console.WriteLine("Yellow/blue rune pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--red-rune-packs-only") { VerifyFirstRebirth(runtime,town);VerifyRedRunePacks(runtime,town);Console.WriteLine("Red rune pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--plus-one-stone-packs-only") { VerifyFirstRebirth(runtime,town);VerifyPlusOneStonePacks(runtime,town);Console.WriteLine("Plus-one stone pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--higher-stone-packs-only") { VerifyFirstRebirth(runtime,town);VerifyHigherStonePacks(runtime,town);Console.WriteLine("Higher stone pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--gem-dragon-soul-packs-only") { VerifyFirstRebirth(runtime,town);VerifyGemDragonSoulPacks(runtime,town);Console.WriteLine("Gem and Dragon Soul pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--way-of-heroes-claims-only") { VerifyFirstRebirth(runtime,town);VerifyWayOfHeroesClaims(runtime,town);Console.WriteLine("Way of Heroes claim checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--millionaire-stone-packing-only") { VerifyFirstRebirth(runtime,town);VerifyMillionaireStonePacking(runtime,town);Console.WriteLine("Millionaire stone packing checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--worship-only") { VerifyAncestorWorship(runtime,town);Console.WriteLine("Ancestor Worship checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--path-hunt-only") { VerifyPathHunt(runtime,town);Console.WriteLine("Path Hunt checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--courier-loop-only") { VerifyCourierLoop(runtime,town);Console.WriteLine("Courier Loop checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--soul-trial-only") { VerifySoulTrial(runtime,town);Console.WriteLine("Soul Trial checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--quest-gifts-only") { VerifyQuestGifts(runtime,town);Console.WriteLine("Quest gift checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--elite-prizes-only") { VerifyElitePrizes(runtime,town);Console.WriteLine("Elite prize claim checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--prize-packs-only") { VerifyPrizePacks(runtime,town);Console.WriteLine("Elite prize pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--exp-items-only") { VerifyExpItems(runtime,town);Console.WriteLine("EXP consumable checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--first-recovery-only") { VerifyFirstRecovery(runtime,town);Console.WriteLine("First-life recovery checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--first-rebirth-only") { VerifyFirstRebirth(runtime,town);Console.WriteLine("First rebirth checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--trojan-rebirth-only") { VerifyTrojanRebirth(runtime,town);Console.WriteLine("Trojan rebirth checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--gems-only") { VerifyGemComposition(town);Console.WriteLine("Gem composition checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--installment-earning-only") { InitializeHousingFixture(runtime,town);VerifyInstallmentEarning(runtime,town);Console.WriteLine("Installment earning checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--steed-earning-only") { VerifySteedEarning(runtime,town);Console.WriteLine("Steed earning checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--elimination-store-only") { VerifyEliminationStore(runtime,town);Console.WriteLine("Elimination store checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--flame-store-only") { VerifyFlameStore(runtime,town);Console.WriteLine("Flame store checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--ancient-seal-only") { VerifyAncientSeal(runtime,town);Console.WriteLine("Ancient seal checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--ancient-boss-world-only") { VerifyAncientBossWorld(runtime,town);Console.WriteLine("Ancient boss world checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--ancient-battle-only") { VerifyAncientBattles(runtime,town);Console.WriteLine("Ancient battle checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--ancient-summon-only") { VerifyAncientSummons(runtime,town);Console.WriteLine("Ancient summon checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--ancient-quest-only") { VerifyAncientQuest(runtime,town);Console.WriteLine("Ancient quest checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--ancient-world-only") { VerifyAncientWorld(runtime,town);Console.WriteLine("Ancient world checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--ancient-store-only") { VerifyAncientStore(runtime,town);Console.WriteLine("Ancient store checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--steed-breeding-only") { VerifySteedBreeding(runtime,town);Console.WriteLine("Steed breeding checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--house-visits-only") { InitializeHousingFixture(runtime,town);VerifyHouseVisits(runtime,town);Console.WriteLine("House visit checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--purple-banshee-only") { VerifyPurpleBanshee(runtime,town);Console.WriteLine("Purple Banshee encounter checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--purple-banshee-rewards-only") { VerifyPurpleRewards(runtime,town);Console.WriteLine("Purple Banshee reward checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--purple-banshee-admission-only") { VerifyPurpleAdmissions(runtime,town);Console.WriteLine("Purple Banshee admission checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--snow-banshee-only") { VerifySnowBanshee(runtime,town);Console.WriteLine("Snow Banshee encounter checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--snow-banshee-rewards-only") { VerifySnowBansheeRewards(runtime,town);Console.WriteLine("Snow Banshee reward checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--snow-banshee-store-only") { VerifySnowBansheeStore(runtime);Console.WriteLine("Snow Banshee store checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--sword-master-only") { VerifySwordMaster(runtime,town);Console.WriteLine("Sword Master checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--housing-only") { VerifyHousing(runtime,town);Console.WriteLine("Housing checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--housing-earning-only") { VerifyHousing(runtime,town,true);Console.WriteLine("Housing earning checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--adventure-only") { VerifyMining(runtime,town,true);Console.WriteLine("Adventure journey checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--mining-only") { VerifyMining(runtime,town);Console.WriteLine("Mining checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--rebirth-materials-only") { VerifyRebirthMaterials(runtime,town);Console.WriteLine("Rebirth material checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--ninja-rebirth-only") { VerifyNinjaRebirth(runtime,town);Console.WriteLine("Ninja rebirth checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--ninja-flowers-only") { VerifyNinjaFlowers(runtime);Console.WriteLine("Ninja flower checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--ninja-epic-quest-only") { VerifyNinjaEpicQuest(runtime);Console.WriteLine("Ninja Epic quest checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--ninja-epic-rules-only") { VerifyNinjaEpicRules(runtime);Console.WriteLine("Ninja Epic rule checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--monk-rebirth-only") { VerifyMonkRebirth(runtime,town);Console.WriteLine("Monk rebirth checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--monk-epic-only") { VerifyMonkEpic(runtime);Console.WriteLine("Monk epic checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--warrior-backfire-only") { VerifyPaidReincarnation(runtime,town);VerifyWarriorBackfire(runtime,town);Console.WriteLine("Warrior Backfire checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--warrior-rebirth-only") { VerifyWarriorRebirth(runtime,town);Console.WriteLine("Warrior rebirth checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--warrior-defender-only") { VerifyRebirthMaterials(runtime,town);DataHolder.ReadStats();SpellTable.Load();RunesSystem.LoadRunesEXP();WarriorInstructorMap(runtime);Query("CREATE TABLE IF NOT EXISTS profs (LIKE public.profs INCLUDING ALL);CREATE TABLE IF NOT EXISTS enemy (LIKE public.enemy INCLUDING ALL);CREATE TABLE IF NOT EXISTS pkexploits (LIKE public.pkexploits INCLUDING ALL)");VerifyMagicDefender(town);Console.WriteLine("Warrior Magic Defender checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--warrior-stance-only") { VerifyRebirthMaterials(runtime,town);DataHolder.ReadStats();SpellTable.Load();RunesSystem.LoadRunesEXP();WarriorInstructorMap(runtime);Call(FirstRite,null,"Initialize");Query("CREATE TABLE IF NOT EXISTS profs (LIKE public.profs INCLUDING ALL);CREATE TABLE IF NOT EXISTS enemy (LIKE public.enemy INCLUDING ALL);CREATE TABLE IF NOT EXISTS pkexploits (LIKE public.pkexploits INCLUDING ALL)");VerifyWarriorStance(town);Console.WriteLine("Warrior stance checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--trojan-epic-store-only") { VerifyTrojanEpicStore(runtime);Console.WriteLine("Trojan epic storage checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--warrior-epic-store-only") { VerifyWarriorEpicStore(runtime);Console.WriteLine("Warrior epic store checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--warrior-training-only") { VerifyWarriorTraining(runtime);Console.WriteLine("Warrior training checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--monk-training-only") { VerifyMonkTraining(runtime);Console.WriteLine("Monk training checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--ninja-training-only") { VerifyNinjaTraining(runtime);Console.WriteLine("Ninja training checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--assassin-training-only") { VerifyAssassinTraining(runtime);Console.WriteLine("Assassin training checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--promotions-only") { VerifyPromotions(runtime);Console.WriteLine("Class promotion checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--rune-acquisition-only") { VerifyRuneAcquisition(runtime,town,fixture);Console.WriteLine("Rune acquisition checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--meteor-drops-only") { VerifyMeteorDrops(runtime,town);Console.WriteLine("Meteor drop checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--player-revival-only") { VerifyPlayerRevival(runtime,town);Console.WriteLine("Player revival checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--clan-membership-only") { VerifyClanMembership(runtime,town);Console.WriteLine("Clan membership checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--player-defeat-only") { VerifyPlayerDefeat(runtime,town);Console.WriteLine("Player defeat checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--aura-progress-only") { VerifyAuraProgress(runtime,town);Console.WriteLine("Aura progress checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--monster-ko-only") { VerifyMonsterKo(runtime,town);Console.WriteLine("Monster KO checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--steed-claims-only") { VerifySteedClaims(runtime,town);Console.WriteLine("Steed claim checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--equipment-sockets-only") { VerifySocketService(runtime,town);Console.WriteLine("Equipment socket checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--collection-acquisition-only") { VerifyRuneAcquisition(runtime,town,fixture,true);Console.WriteLine("Collection acquisition checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--rune-loadout-only") { VerifyRuneLoadout(town);Console.WriteLine("Rune loadout checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--subclasses-only") { VerifySubclasses(town);Console.WriteLine("Subclass checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--mastery-only") { VerifyMastery(town);Console.WriteLine("Mastery checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--couple-pk-only") { VerifyCouplePk(runtime,town);Console.WriteLine("Couple PK checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--class-pk-only") { VerifyClassPk(runtime,town);Console.WriteLine("Class PK checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--local-pk-only") { VerifyLocalPk(runtime,town);Console.WriteLine("Local PK checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--labyrinth-only") { VerifyLabyrinth(runtime,town);Console.WriteLine("Labyrinth checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--snake-only") { VerifySnake(runtime,town);Console.WriteLine("Snake Islands checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--sky-only") { VerifySky(runtime,town);Console.WriteLine("Sky Pass checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--summer-only") { VerifySummer(runtime,town);Console.WriteLine("Summer checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--quiz-only") { VerifyQuiz(runtime,town);Console.WriteLine("Quiz Show checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--p7-soul-packs-only") { VerifyP7Packs(runtime,town);Console.WriteLine("P7 soul pack checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--arena-daily-only") { VerifyArenaDaily(runtime,town);Console.WriteLine("Arena daily reward and prerequisite checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--arena-results-only") { VerifyArenaResults(runtime,town);Console.WriteLine("Arena result, ledger and participation checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--arena-ledger-only") { VerifyArenaLedger(runtime,town);Console.WriteLine("Arena ledger and participation checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--arena-only") { VerifyArenaParticipation(runtime,town);Console.WriteLine("Arena participation checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--abyss-quest-only") { VerifyAbyssQuest(runtime,town);Console.WriteLine("Abyss quest checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--abyss-world-only") { VerifyAbyssWorld(runtime,town);Console.WriteLine("Abyss world checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--abyss-journal-only") { VerifyAbyssJournal(runtime,town);Console.WriteLine("Abyss journal checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--abyss-store-only") { VerifyAbyssStore(runtime,town);Console.WriteLine("Abyss store checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--abyss-rules-only") { VerifyAbyssRules();Console.WriteLine("Abyss rule checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--second-token-shop-only") { VerifyTokenShop(runtime,town);Console.WriteLine("Second token shop checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--paid-reincarnation-only") { VerifyPaidReincarnation(runtime,town);Console.WriteLine("Paid reincarnation checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--second-rebirth-only") { VerifySecondRebirth(runtime,town);Console.WriteLine("Second rebirth checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--second-rebirth-rules-only") { VerifySecondRebirthRules();Console.WriteLine("Second rebirth rule checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--chi-tokens-only") { VerifyChiCash(runtime,town);Console.WriteLine("Chi Token checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--souls-only") { VerifySouls(runtime,town);Console.WriteLine("Release Souls checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--quest-packets-only") { VerifyQuestPackets(town);Console.WriteLine("Quest packet checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--power-only") { VerifyPower(runtime,town);Console.WriteLine("Power Arena checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--moon-only") { VerifyMoon(runtime,town);Console.WriteLine("Moon Box checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--demon-only") { VerifyDemon(runtime);Console.WriteLine("Demon hunt checks passed: "+checks);return 0; }
            if(args.Length==1 && args[0]=="--elite-pk-only") {
                VerifyElitePk(runtime,town);Console.WriteLine("Elite PK checks passed: "+checks);return 0;
            }
            if(args.Length==1 && args[0]=="--ctf-only") {
                VerifyCtf(runtime,town);Console.WriteLine("CTF checks passed: "+checks);return 0;
            }
            if(args.Length==1 && args[0]=="--progression-only") {
                VerifyProgression(map);Console.WriteLine("Progression checks passed: "+checks);return 0;
            }
            if(args.Length==1 && args[0]=="--cultivation-only") {
                VerifyCultivation(map);Console.WriteLine("Cultivation checks passed: "+checks);return 0;
            }
            if(args.Length==1 && args[0]=="--titles-only") {
                Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
                StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
                VerifyTitles(map);Console.WriteLine("Title checks passed: "+checks);return 0;
            }
            if(args.Length==1 && args[0]=="--runes-only") {
                RunesSystem.LoadRunesEXP();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
                VerifyRunes(map);Console.WriteLine("Rune checks passed: "+checks);return 0;
            }
            Directory.CreateDirectory(Path.Combine(fixture,"database")); Environment.CurrentDirectory=fixture;
            Map.CreateTimerFactories();
            // Accept timer registrations without running background world callbacks in this fixture.
            Revora.Thread.GenericThreadPool = new System.Threading.StaticPool(1);
            town.LoadMonsters();
            ((IDisposable)typeof(Map).GetField("Timer",Instance).GetValue(town)).Dispose();
            var pheasants=town.Entities.Values.Where(e=>e.Name=="Pheasant").ToArray();
            Check(pheasants.Length==480 && pheasants.All(e=>!e.Dead), "Pheasant spawn loading doubles the installed 240 to 480 on real terrain");
            Check(pheasants.Count(e=>e.MonsterInfo.BoundX==552)==200
                && pheasants.Count(e=>e.MonsterInfo.BoundX==665)==80
                && pheasants.Count(e=>e.MonsterInfo.BoundX==444)==200
                && pheasants.Select(e=>Tuple.Create(e.X,e.Y)).Distinct().Count()==480,
                "each Pheasant flock doubles independently and occupies distinct tiles");
            ConquerItem.ItemUID=new Counter(3000000);
            var p=Player(map);
            object hunt=Activator.CreateInstance(Hunt,Instance,null,new object[]{map,baseline},null);
            typeof(Map).GetField("PotencyPopulation",Instance).SetValue(map,hunt);
            Call(Hunt,hunt,"Tick",Time32.Now);
            Check(map.Entities.Count==1000 && map.Entities.Values.All(e=>!e.Dead), "1000 live monsters are placed on the real map floor");
            var mobs=map.Entities.Values.ToArray();
            Check(mobs.All(e=>e.MaxHitpoints==43813 && e.MinAttack==1035 && e.MaxAttack==1035 && e.Defence==275), "health +25%, attack +15%, defense +10% over AlienSerpent");
            Check(mobs.All(e=>e.Level==baseline.Level && e.MonsterInfo.AttackSpeed==baseline.AttackSpeed && e.Body==baseline.Mesh), "baseline level, speed and existing client appearance are preserved");
            Check(mobs.Select(e=>Tuple.Create(e.X,e.Y)).Distinct().Count()==1000 && mobs.All(e=>map.Floor[e.X,e.Y,MapObjectType.Player]), "all spawn tiles are walkable and distinct");
            Check(mobs.All(e=>Math.Abs(e.X-561)<=100 && Math.Abs(e.Y-549)<=100), "spawns stay in the intended hunting area");
            Parallel.For(0,24,i=>Call(Hunt,hunt,"Tick",Time32.Now));
            Check(map.Entities.Count==1000, "concurrent replenishment cannot exceed the population cap");
            var histogram=Enumerable.Range(0,10000).Select(i=>(uint)Call(Hunt,null,"RollPack",i)).GroupBy(id=>id).ToDictionary(g=>g.Key,g=>g.Count());
            Check(histogram[3007108]==1000 && histogram[3007109]==500 && histogram[3007110]==100 && histogram[0]==8400, "exact loot odds: 10%, 5%, 1%, 84% empty");
            int before=map.FloorItems.Count;
            Call(Hunt,hunt,"Drop",mobs[0],p.Entity);
            int rolled=map.FloorItems.Count;
            Parallel.For(0,20,i=>Call(Hunt,hunt,"Drop",mobs[0],p.Entity));
            Check(rolled-before<=1 && map.FloorItems.Count==rolled, "replayed kill notifications produce at most one pack");
            // Exercise the real death dispatcher, including its duplicate-death guard.
            var victim=mobs[1]; int kills=p.Entity.KillCount;
            victim.Die(p.Entity);
            victim.Die(p.Entity);
            Check(victim.Dead && p.Entity.KillCount==kills+1, "a managed monster death is processed exactly once");
            Call(Hunt,hunt,"Tick",victim.DeathStamp);
            Check(victim.Dead, "dead monster waits for its one-second respawn delay");
            Call(Hunt,hunt,"Tick",victim.DeathStamp.AddSeconds(2));
            Check(!victim.Dead && map.Entities.Count==1000 && !map.Floor[victim.X,victim.Y,MapObjectType.Monster], "respawn restores health and reserves the new tile");
            map.RemoveEntity(mobs[2]); Call(Hunt,hunt,"Tick",Time32.Now);
            Check(map.Entities.Count==1000, "a missing population member is replenished");
            var outsider=new Entity(EntityFlag.Monster,false){UID=12345,MapID=1217};
            Check(!(bool)Call(Hunt,hunt,"Contains",outsider) && !(bool)Call(Hunt,hunt,"Drop",outsider,p.Entity), "ordinary map monsters remain outside the managed drop table");

            Call(Guide,null,"Create");
            Check(town.Npcs.ContainsKey(900000018) && map.Npcs.ContainsKey(900000019), "guide and return NPCs spawn on valid real-map tiles");
            p.ActiveNpc=900000019;
            Check((bool)Call(Guide,null,"HandleNpc",p,new NpcRequest(true){OptionID=0}), "ordinary players can read the Potency guide");
            p.ActiveNpc=900000018;
            Call(Guide,null,"HandleNpc",p,new NpcRequest(true){OptionID=1});
            Check(p.Entity.MapID==1217, "remote guide teleport requests are rejected");
            p.ActiveNpc=900000019;
            Call(Guide,null,"HandleNpc",p,new NpcRequest(true){OptionID=1});
            Check(p.Entity.MapID==1002, "ordinary player can return through the field guide");
            p.ActiveNpc=900000018;
            Call(Guide,null,"HandleNpc",p,new NpcRequest(true){OptionID=1});
            Check(p.Entity.MapID==1217, "ordinary player can enter the hunt through the Twin City guide");

            foreach(uint id in new uint[]{3007108,3007109,3007110}) ItemHandler.UseItem(Pack(p,id,1),p);
            Check(p.InnerPower.Potency==2000 && p.Inventory.Count==0 && !Fenced(p), "ordinary player opens all three packs for 400, 600 and 1000 Potency");
            Check(Scalar("SELECT points FROM revora_potency_awards WHERE entityid=@uid",p.Entity.UID)==2000 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid",p.Entity.UID)==0, "award totals and consumption persist together");
            Check(p.UnionExploits==300 && Scalar("SELECT unionexploits FROM entities WHERE uid=@uid",p.Entity.UID)==300, "existing War Exploit bonuses are preserved");
            var single=Pack(p,3007108,1);
            Parallel.For(0,16,i=>ItemHandler.UseItem(single,p));
            Check(p.InnerPower.Potency==2400 && p.Inventory.Count==0 && !Fenced(p), "concurrent reuse of a consumed pack grants only once");
            var stack=Pack(p,3007109,2);ItemHandler.UseItem(stack,p);
            Check(p.InnerPower.Potency==3000 && stack.StackSize==1, "opening a stack consumes exactly one pack");
            p.Trade.InTrade=true;ItemHandler.UseItem(stack,p);p.Trade.InTrade=false;
            Check(p.InnerPower.Potency==3000 && stack.StackSize==1, "trading blocks pack use");
            var forged=new ConquerItem(true){UID=stack.UID,ID=stack.ID,StackSize=1};ItemHandler.UseItem(forged,p);
            Check(p.InnerPower.Potency==3000, "a forged item reference cannot award Potency");
            p.InnerPower.Potency=int.MaxValue;ItemHandler.UseItem(stack,p);
            Check(stack.StackSize==1 && p.InnerPower.Potency==int.MaxValue, "Potency overflow preserves the pack");
            p.InnerPower.Potency=3000;
            InnerPowerTable.Save();
            InnerPower.InnerPowerPolle.Clear();InnerPowerTable.Load();Call(Packs,null,"Initialize");
            Check(InnerPower.InnerPowerPolle[p.Entity.UID].Potency==3000, "saved checkpoint and award total reload without double credit");
            var recovery=new InnerPower("Recovery",p.Entity.UID){Potency=0};
            Call(Packs,null,"RecoverPlayer",recovery);Call(Packs,null,"RecoverPlayer",recovery);
            Check(recovery.Potency==3000, "a commit before the checkpoint is recovered exactly once");

            var pickupPlayer=Player(map);
            var floor=new FloorItem(true){UID=FloorItem.FloorUID.Next,ItemID=3007110,MapID=1217,X=pickupPlayer.Entity.X,Y=pickupPlayer.Entity.Y,
                Owner=pickupPlayer,OwnerUID=pickupPlayer.Entity.UID,OnFloor=Time32.Now,MapObjType=MapObjectType.Item,
                ValueType=FloorItem.FloorValueType.Item,Item=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=3007110,StackSize=1,MobDropped=true}};
            map.AddFloorItem(floor);
            var other=Player(map);Call(Packs,null,"Pickup",other,floor);
            Check(other.Inventory.Count==0 && map.FloorItems.ContainsKey(floor.UID), "another player cannot steal a protected pack");
            Parallel.For(0,12,i=>Call(Packs,null,"Pickup",pickupPlayer,floor));
            Check(pickupPlayer.Inventory.Count==1 && !map.FloorItems.ContainsKey(floor.UID)
                && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid",pickupPlayer.Entity.UID)==1, "concurrent pickup persists and grants the floor pack once");
            ItemHandler.UseItem(pickupPlayer.Inventory.Objects[0],pickupPlayer);
            Check(pickupPlayer.InnerPower.Potency==1000 && pickupPlayer.Inventory.Count==0, "picked-up pack can immediately be opened for Potency");

            var failed=Player(map);var blocked=Pack(failed,3007108,1);
            Query("ALTER TABLE revora_potency_awards ADD CONSTRAINT injected_failure CHECK(points<400) NOT VALID");
            try { ItemHandler.UseItem(blocked,failed); } catch(NullReferenceException) { } catch(ArgumentNullException) { }
            Check(failed.InnerPower.Potency==0 && failed.Inventory.Count==1 && Scalar("SELECT COUNT(*) FROM items WHERE entityid=@uid",failed.Entity.UID)==1, "database failure preserves the pack and awards no Potency");
            Check(Fenced(failed), "ambiguous persistence requires reconnect before retry");
            VerifyRetiredBossRewards(map);
            VerifyDailyBosses(runtime);
            VerifyExpedition(runtime);
            Console.WriteLine("Potency Hunt and boss reward checks passed: "+checks);return 0;
        }
        catch(Exception e) { Console.WriteLine(e.GetType().Name+": "+e.Message+"\n"+e.StackTrace);return 1; }
        finally
        {
            Environment.CurrentDirectory=runtime;
            if(original!=null) DataHolder.ConnectionString=original;
            if(schema) Query("DROP SCHEMA revora_potency_validation CASCADE");
        }
    }
}
