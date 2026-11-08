using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static void VerifyHeartRestartProcess(Type heart,GameState winner,DateTime expiry)
    {
        var encounter=typeof(Entity).Assembly.GetType("Revora.Game.Features.HeartEncounter");
        var clock=heart.GetField("Clock",Hidden);object previous=clock.GetValue(null);
        DateTime now=new DateTime(2026,4,11,7,0,0,DateTimeKind.Utc);
        clock.SetValue(null,(Func<DateTime>)(()=>now));
        var activity=typeof(Entity).Assembly.GetType("Revora.Database.AccountActivity");
        try {
            var town=Kernel.Maps[1002];var interrupted=PlanetPlayer(town);var cache=PlanetPlayer(town);var closing=PlanetPlayer(town);
            foreach(var p in new[]{interrupted,cache,closing}) {
                p.ActiveNpc=380;p.Entity.X=321;p.Entity.Y=299;
                Call(heart,null,"Page",p,(byte)160);Call(heart,null,"Page",p,(byte)162);Call(heart,null,"Page",p,(byte)163);Call(heart,null,"Page",p,(byte)164);
                Check(p.Entity.MapID!=1002,"Heart process fixture enters through ordinary commander dialogue");
            }
            PlanetCombatDefeat(interrupted,interrupted.Map.Entities.Values.First(e=>e.EntityFlag==EntityFlag.Monster));
            Check(Scalar("SELECT count(*) FROM revora_heart_objectives o JOIN revora_heart_runs r ON r.id=o.run WHERE r.uid=@uid",interrupted.Entity.UID)==1,
                "Heart process fixture earns one durable objective before interruption");
            foreach(var mob in cache.Map.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster).ToArray())PlanetCombatDefeat(cache,mob);
            PlanetCombatDefeat(cache,cache.Map.Entities.Values.Single(e=>e.EntityFlag==EntityFlag.Monster));
            cache.ActiveNpc=900000191;cache.Entity.X=48;cache.Entity.Y=48;Call(heart,null,"Page",cache,(byte)0);
            Query("CREATE FUNCTION defer_heart_return() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER defer_heart_return BEFORE UPDATE ON revora_heart_encounters FOR EACH ROW WHEN (NEW.returned) EXECUTE FUNCTION defer_heart_return()");
            try {Call(heart,null,"Page",cache,(byte)1);}
            finally {Query("DROP TRIGGER defer_heart_return ON revora_heart_encounters; DROP FUNCTION defer_heart_return()");}
            Check(Fenced(cache) && Scalar("SELECT count(*) FROM revora_heart_encounters WHERE uid=@uid AND state='complete' AND NOT returned",cache.Entity.UID)==1,
                "Heart restart source is an earned cache with interrupted return acknowledgement");
            foreach(var mob in closing.Map.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster).ToArray())PlanetCombatDefeat(closing,mob);
            PlanetCombatDefeat(closing,closing.Map.Entities.Values.Single(e=>e.EntityFlag==EntityFlag.Monster));
            closing.ActiveNpc=900000191;closing.Entity.X=48;closing.Entity.Y=48;Call(heart,null,"Page",closing,(byte)0);
            Query("CREATE FUNCTION defer_heart_close() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER defer_heart_close BEFORE UPDATE ON revora_heart_encounters FOR EACH ROW WHEN (NEW.state='complete') EXECUTE FUNCTION defer_heart_close()");
            try {Call(heart,null,"HandleNpc",closing,new NpcRequest(true){OptionID=1});}
            finally {Query("DROP TRIGGER defer_heart_close ON revora_heart_encounters; DROP FUNCTION defer_heart_close()");}
            Check(Fenced(closing) && Scalar("SELECT count(*) FROM revora_heart_encounters e JOIN revora_heart_runs r ON r.id=e.run WHERE e.uid=@uid AND e.state='active' AND NOT e.returned AND r.mask=4095",closing.Entity.UID)==1,
                "Heart interrupted close leaves a complete earned cache with an active encounter receipt");
            // Remove fixture memory without running graceful encounter shutdown. The child
            // owns recovery from database state; this is not a production Host launch.
            var scenes=encounter.GetField("Scenes",Hidden).GetValue(null);
            foreach(var p in new[]{interrupted,cache,closing}) {GameState ignored;Kernel.GamePool.TryRemove(p.Entity.UID,out ignored);}
            var values=(System.Collections.IEnumerable)scenes.GetType().GetProperty("Values").GetValue(scenes,null);
            var arenas=new List<Map>();foreach(object scene in values)arenas.Add((Map)scene.GetType().GetField("Map",Instance).GetValue(scene));
            scenes.GetType().GetMethod("Clear").Invoke(scenes,null);
            foreach(var arena in arenas){arena.Dispose();MapsTable.MapInformations.Remove(arena.ID);}
            Call(activity,null,"Relinquish");
            var start=new System.Diagnostics.ProcessStartInfo {
                FileName=typeof(PotencyHuntTests).Assembly.Location,Arguments="--heart-restart-check",
                WorkingDirectory=Environment.CurrentDirectory,UseShellExecute=false,CreateNoWindow=true,
                RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            using(var child=System.Diagnostics.Process.Start(start)) {
                var output=child.StandardOutput.ReadToEndAsync();var errors=child.StandardError.ReadToEndAsync();
                child.StandardInput.WriteLine(DataHolder.ConnectionString);
                foreach(long value in new long[]{now.Ticks,interrupted.Entity.UID,cache.Entity.UID,winner.Entity.UID,expiry.Ticks,closing.Entity.UID})
                    child.StandardInput.WriteLine(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                child.StandardInput.Close();
                if(!child.WaitForExit(30000)){child.Kill();child.WaitForExit();throw new Exception("Heart restart child timed out.");}
                string result=output.GetAwaiter().GetResult(),error=errors.GetAwaiter().GetResult();
                if(child.ExitCode!=0) {
                    System.Console.WriteLine(error);
                    foreach(string line in result.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Where(line=>line.StartsWith("PASS:") || line.Contains("[quest]")).Take(20))System.Console.WriteLine(line);
                }
                Check(child.ExitCode==0 && error.Length==0 && result.Contains("Heart restart checks passed:"),"Heart new process recovers earned fight cache and prizes without parent memory");
                System.Console.WriteLine(result.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Last());
            }
            Check(Scalar("SELECT count(*) FROM revora_heart_encounters WHERE uid=@uid AND returned",interrupted.Entity.UID)==1
                && Scalar("SELECT count(*) FROM revora_heart_runs WHERE uid=@uid AND delivered_at IS NOT NULL",cache.Entity.UID)==1,
                "Heart fresh-process recovery and cache delivery remain committed after exit");
            Check(Scalar("SELECT count(*) FROM revora_heart_encounters WHERE uid=@uid AND state='complete' AND returned",closing.Entity.UID)==1,
                "Heart recovered close remains complete and returned after child exit");
        } finally {clock.SetValue(null,previous);Call(activity,null,"Initialize");}
    }
    static int HeartRestartChild()
    {
        try {
            string connection=System.Console.ReadLine();if(string.IsNullOrEmpty(connection) || connection.Length>8192)return 2;
            var builder=new Npgsql.NpgsqlConnectionStringBuilder(connection);if(builder.SearchPath!="revora_potency_validation")return 2;
            DataHolder.ConnectionString=builder.ConnectionString;
            var values=new long[6];for(int n=0;n<values.Length;n++)values[n]=long.Parse(System.Console.ReadLine(),System.Globalization.CultureInfo.InvariantCulture);
            DateTime now=new DateTime(values[0],DateTimeKind.Utc),expiry=new DateTime(values[4],DateTimeKind.Utc);
            uint interrupted=checked((uint)values[1]),cache=checked((uint)values[2]),winner=checked((uint)values[3]),closing=checked((uint)values[5]);
            var heart=typeof(Entity).Assembly.GetType("Revora.Game.Features.HeartCampaign");
            var encounter=typeof(Entity).Assembly.GetType("Revora.Game.Features.HeartEncounter");
            var scenes=encounter.GetField("Scenes",Hidden).GetValue(null);
            Check(Kernel.GamePool.Count==0 && (int)scenes.GetType().GetProperty("Count").GetValue(scenes,null)==0
                && !(bool)encounter.GetField("initialized",Hidden).GetValue(null),"Heart child starts without sessions scenes or initialized world state");
            heart.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
            TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
            ConquerItemInformation.Load();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
            Call(EvidenceType,null,"Initialize");Call(encounter,null,"Initialize");
            Check(Scalar("SELECT count(*) FROM revora_heart_encounters WHERE uid=@uid AND state='abandoned' AND NOT returned",interrupted)==1,
                "Heart fresh startup abandons interrupted combat without inventing a cache");
            Check(Scalar("SELECT count(*) FROM revora_heart_encounters WHERE uid=@uid AND state='complete' AND NOT returned",closing)==1,
                "Heart fresh startup classifies the saved full objective mask as complete after interrupted close");
            var town=World(Environment.CurrentDirectory,1002);DMaps.MapPaths[1002]="heart-restart-town";
            town.Npcs[380]=new NpcSpawn{UID=380,MapID=1002,X=321,Y=299,Mesh=50050,Type=Enums.NpcType.Talker};
            var recovered=PlanetRestartPlayer(town,closing);Call(encounter,null,"OnLogin",recovered);
            recovered.ActiveNpc=380;recovered.Entity.X=321;recovered.Entity.Y=299;
            Call(heart,null,"Page",recovered,(byte)160);Call(heart,null,"Page",recovered,(byte)165);Call(heart,null,"Page",recovered,(byte)165);
            Check(!Fenced(recovered) && recovered.Entity.MapID==1002
                && Scalar("SELECT count(*) FROM revora_heart_runs WHERE uid=@uid AND delivered_at IS NOT NULL",closing)==1
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='heart-revora-v1'",closing)==0,
                "Heart interrupted-close cache returns and delivers once without granting an early wing");
            var lost=PlanetRestartPlayer(town,interrupted);Call(encounter,null,"OnLogin",lost);
            Check(!Fenced(lost) && lost.Entity.MapID==1002 && Scalar("SELECT count(*) FROM revora_heart_encounters WHERE uid=@uid AND NOT returned",interrupted)==0
                && Scalar("SELECT count(*) FROM revora_heart_objectives o JOIN revora_heart_runs r ON r.id=o.run WHERE r.uid=@uid",interrupted)==1,
                "Heart new ordinary session returns safely and retains its single earned objective");
            var carried=PlanetRestartPlayer(town,cache);Call(encounter,null,"OnLogin",carried);
            Check(!Fenced(carried) && Scalar("SELECT count(*) FROM revora_heart_encounters WHERE uid=@uid AND state='complete' AND returned",cache)==1,
                "Heart fresh process acknowledges the earned cache return");
            carried.ActiveNpc=380;carried.Entity.X=321;carried.Entity.Y=299;Call(heart,null,"Page",carried,(byte)160);Call(heart,null,"Page",carried,(byte)165);
            Check(Scalar("SELECT count(*) FROM revora_heart_runs WHERE uid=@uid AND mask=4095 AND delivered_at IS NOT NULL",cache)==1,
                "Heart recovered cache delivers through the ordinary commander");
            var awarded=PlanetRestartPlayer(town,winner);
            Call(typeof(Entity),awarded.Entity,"ApplyCommittedConquerPoints",checked((uint)Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",winner)));
            Call(typeof(Entity),awarded.Entity,"ProjectRevoraPoints",checked((uint)Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",winner)));
            Call(heart,null,"RecoverPrizes",awarded);new TitleStorage().Login(awarded);TitleRequest(60186018,TitleStorage.Action.Equip).Handle(awarded);
            Check(!Fenced(awarded) && awarded.Entity.ConquerPoints==5000 && awarded.Entity.RevoraPoint==1000 && awarded.Entity.EquippedWing==60186018,
                "Heart new process restores committed currencies and equips the earned wing without repaying");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT expires_at FROM revora_event_awards WHERE uid=@uid AND source='heart-revora-v1'",c)) {
                q.Parameters.AddWithValue("uid",(long)winner);using(var r=q.ExecuteReader())Check(r.Read() && r.GetFieldValue<DateTimeOffset>(0).UtcDateTime==expiry,
                    "Heart fresh process reads the exact original prize expiry from durable storage");
            }}
            Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='heart-revora-v1'",cache)==0
                && Scalar("SELECT count(*) FROM revora_heart_runs WHERE uid=@uid AND delivered_at IS NOT NULL",interrupted)==0,
                "Heart restart cannot turn partial participation into a wing or unfinished fight into delivery");
            Call(encounter,null,"OnLogin",carried);Call(heart,null,"Page",carried,(byte)165);Call(heart,null,"RecoverPrizes",awarded);
            Check(Scalar("SELECT count(*) FROM revora_heart_runs WHERE uid=@uid AND delivered_at IS NOT NULL",cache)==1
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='heart-revora-v1'",winner)==1,
                "Heart fresh-process repeated recovery cannot duplicate delivery or wing receipt");
            now=expiry;new TitleStorage().Login(awarded);
            Check(!OwnedTitles(awarded).ContainsKey(60186018) && awarded.Entity.EquippedWing==0,
                "Heart fresh process retains the original wing expiry");
            System.Console.WriteLine("Heart restart checks passed: "+checks);return 0;
        }catch(Exception ex){System.Console.Error.WriteLine("Heart restart failed: "+ex.GetType().Name+"\n"+ex.StackTrace);return 1;}
    }
    static GameState EarnPlanetAttempt(Map town,Map birds)
    {
        var p=PlanetPlayer(town);PlanetTown(p,town);PlanetNpc(p,211);
        DemonMove(p,birds);for(int n=0;n<100;n++)PlanetKill(p,birds);
        PlanetTown(p,town);PlanetNpc(p,212);
        Check(!Fenced(p) && p.Map.BaseID==700 && p.Map.ID!=700,"process fixture earns its guarded arena through Rowan and 100 actual Birdman deaths");
        p.Entity.X=48;p.Entity.Y=45;return p;
    }
    static void VerifyPlanetRestartProcess(Map town,Map birds,GameState winner,DateTime expiry)
    {
        var encounter=typeof(Entity).Assembly.GetType("Revora.Game.Features.PlanetEncounter");
        var interrupted=EarnPlanetAttempt(town,birds);var arena=interrupted.Map;
        arena.Entities.Values.First(e=>e.EntityFlag==EntityFlag.Monster).Die(interrupted.Entity);
        Check(Scalar("SELECT defeated FROM revora_planet_attempts WHERE uid=@uid AND NOT returned",interrupted.Entity.UID)>0,
            "restart interruption begins with an earned partial guard defeat");
        var cache=EarnPlanetAttempt(town,birds);
        foreach(var mob in cache.Map.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster).ToArray())mob.Die(cache.Entity);
        cache.ActiveNpc=900000190;
        Query("CREATE FUNCTION defer_planet_return() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER defer_planet_return BEFORE UPDATE ON revora_planet_attempts FOR EACH ROW WHEN (NEW.returned) EXECUTE FUNCTION defer_planet_return()");
        try {Check((bool)Call(encounter,null,"Collect",cache),"process fixture earns a cache with a deliberately unacknowledged return");}
        finally {Query("DROP TRIGGER defer_planet_return ON revora_planet_attempts; DROP FUNCTION defer_planet_return()");}
        Check(Fenced(cache) && Scalar("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND state='victory' AND NOT returned",cache.Entity.UID)==1,
            "restart cache source is a committed victory rather than a seeded reward");
        // Stop these offline fixture sessions without calling graceful abandonment.
        // The child must recover their committed receipts without any scene objects.
        GameState ignored;Kernel.GamePool.TryRemove(interrupted.Entity.UID,out ignored);Kernel.GamePool.TryRemove(cache.Entity.UID,out ignored);
        var maps=encounter.GetField("Maps",Hidden).GetValue(null);
        var removed=new object[]{arena.ID,null};maps.GetType().GetMethod("TryRemove").Invoke(maps,removed);
        arena.Dispose();MapsTable.MapInformations.Remove(arena.ID);
        // Model a stopped parent: its session lease must not outlive the restart.
        var activity=typeof(Entity).Assembly.GetType("Revora.Database.AccountActivity");
        Call(activity,null,"Relinquish");
        var start=new System.Diagnostics.ProcessStartInfo {
            FileName=typeof(PotencyHuntTests).Assembly.Location,Arguments="--planet-restart-check",
            WorkingDirectory=Environment.CurrentDirectory,UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var child=System.Diagnostics.Process.Start(start)) {
            var output=child.StandardOutput.ReadToEndAsync();var errors=child.StandardError.ReadToEndAsync();
            // No secrets in command-line arguments, files or test output.
            child.StandardInput.WriteLine(DataHolder.ConnectionString);
            foreach(long value in new long[]{planetNow.Ticks,interrupted.Entity.UID,cache.Entity.UID,winner.Entity.UID,expiry.Ticks})
                child.StandardInput.WriteLine(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            child.StandardInput.Close();
            if(!child.WaitForExit(30000)){child.Kill();child.WaitForExit();throw new Exception("Planet restart child timed out.");}
            string result=output.GetAwaiter().GetResult(),error=errors.GetAwaiter().GetResult();
            if(child.ExitCode!=0) {
                System.Console.WriteLine(error);
                foreach(string line in result.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Where(line=>line.StartsWith("PASS:") || line.Contains("[quest]") || line.StartsWith("Planet child")).Take(20))System.Console.WriteLine(line);
            }
            Check(child.ExitCode==0 && error.Length==0 && result.Contains("Planet restart checks passed:"),
                "fresh process recovers earned interrupted fight cache return and fixed-expiry wing without parent memory");
            System.Console.WriteLine(result.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Last());
        }
        Check(Scalar("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND state='abandoned' AND returned",interrupted.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_planet_completions WHERE uid=@uid",cache.Entity.UID)==1,
            "child recovery and earned cache delivery remain committed after the child exits");
        Call(activity,null,"Initialize");
    }
    static GameState PlanetRestartPlayer(Map town,uint uid)
    {
        // Build a canonical ordinary fixture session for an existing durable character.
        // This is not wire authentication or full EntityTable loading.
        typeof(CompanionTests).GetField("nextUid",Hidden).SetValue(null,checked((uint)Scalar("SELECT max(uid)+100 FROM entities",0)));
        var p=(GameState)typeof(CompanionTests).GetMethod("Player",Hidden).Invoke(null,new object[]{town,false,(ushort)321,(ushort)299,(byte)15});
        GameState ignored;Kernel.GamePool.TryRemove(p.Entity.UID,out ignored);
        p.Entity.UID=p.Account.EntityID=uid;Kernel.GamePool[uid]=p;
        foreach(var field in typeof(Screen).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
            if(field.GetValue(p.Screen)==null && field.FieldType.IsGenericType && field.FieldType.GetConstructor(Type.EmptyTypes)!=null)
                field.SetValue(p.Screen,Activator.CreateInstance(field.FieldType));
        p.Action=2;p.Account.State=AccountTable.AccountState.Player;p.ItemSyncRoot=new object();p.Trade=new Revora.Game.ConquerStructures.Trade();
        p.Entity.FullyLoaded=true;p.Entity.MyAchievement=new Achievement(p.Entity);p.RunesItem=new SafeDictionary<uint,ConquerItem>();
        p.ChiData=new ChiTable.ChiData{Powers=p.ChiPowers};p.InnerPower=new InnerPower(p.Entity.Name,uid);Collection(p);
        p.Entity.Titles=new System.Collections.Concurrent.ConcurrentDictionary<TitlePacket.Titles,DateTime>();return p;
    }
    static string FullPlanetCollectionSnapshot(uint uid)
    {
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand(@"SELECT json_build_object(
'items',(SELECT json_agg(i ORDER BY uid) FROM (SELECT uid,id,bound,stacksize,warehouse,position,inwardrobe,days,bless FROM items WHERE entityid=@uid) i),
'claims',(SELECT json_agg(e ORDER BY design) FROM revora_planet_exchange e WHERE uid=@uid),
'runs',(SELECT json_agg(r ORDER BY collection_step) FROM revora_planet_runs r WHERE uid=@uid),
'completions',(SELECT json_agg(c ORDER BY weekend) FROM revora_planet_completions c WHERE uid=@uid),
'achievement',(SELECT json_agg(a ORDER BY achievement_id) FROM revora_achievement_evidence a WHERE uid=@uid AND achievement_id IN(11228,11229,11231)))::text",c)){
            q.Parameters.AddWithValue("uid",(long)uid);return Convert.ToString(q.ExecuteScalar());
        }}
    }
    static void VerifyFullPlanetCollectionRestart(GameState p)
    {
        string before=FullPlanetCollectionSnapshot(p.Entity.UID);
        var activity=typeof(Entity).Assembly.GetType("Revora.Database.AccountActivity");Call(activity,null,"Relinquish");
        var start=new System.Diagnostics.ProcessStartInfo{FileName=typeof(PotencyHuntTests).Assembly.Location,
            Arguments="--planet-collection-restart-check",WorkingDirectory=Environment.CurrentDirectory,
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var child=System.Diagnostics.Process.Start(start)){
            var output=child.StandardOutput.ReadToEndAsync();var errors=child.StandardError.ReadToEndAsync();
            child.StandardInput.WriteLine(DataHolder.ConnectionString);child.StandardInput.WriteLine(p.Entity.UID);child.StandardInput.Close();
            if(!child.WaitForExit(30000)){child.Kill();child.WaitForExit();throw new Exception("Full Planet collection restart timed out.");}
            string result=output.GetAwaiter().GetResult(),error=errors.GetAwaiter().GetResult();
            if(child.ExitCode!=0){System.Console.WriteLine(result);System.Console.WriteLine(error);}
            Check(child.ExitCode==0 && error.Length==0,"full276-design wardrobe and100garment achievement recover in a fresh process");
            System.Console.WriteLine(result.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Last());
        }
        Check(FullPlanetCollectionSnapshot(p.Entity.UID)==before,"full collection recovery and257 replay attempts preserve exact items, credits, runs and achievement receipts");
        Call(activity,null,"Initialize");
    }
    static int FullPlanetCollectionRestartChild()
    {
        try{
            var b=new Npgsql.NpgsqlConnectionStringBuilder(System.Console.ReadLine());
            if(b.Host!="127.0.0.1" || b.Port!=25439 || b.Database!="revora_candidate_game" || b.SearchPath!="revora_potency_validation")return 2;
            DataHolder.ConnectionString=b.ConnectionString;uint uid=uint.Parse(System.Console.ReadLine());
            ConquerItemInformation.Load();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
            Call(Planet,null,"Initialize");var town=World(Environment.CurrentDirectory,1002);
            town.Npcs[900000016]=new NpcSpawn{UID=900000016,MapID=1002,X=319,Y=299,Mesh=50050,Type=Enums.NpcType.Talker};
            var p=PlanetRestartPlayer(town,uid);p.Entity.FullyLoaded=false;p.Entity.Vitality=100;p.Entity.FullyLoaded=true;
            ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);PlanetTown(p,town);
            Check(p.Account.State==AccountTable.AccountState.Player && !Fenced(p) && p.Entity.StorageItems.Count==276,"ordinary recovery loads all257 exchange designs and19 earned Planet gifts");
            var exchange=typeof(Entity).Assembly.GetType("Revora.Game.Features.PlanetCollectionExchange",true);
            var garments=(uint[])exchange.GetField("Garments",Hidden).GetValue(null);var mounts=(uint[])exchange.GetField("Mounts",Hidden).GetValue(null);
            var receipts=new System.Collections.Generic.Dictionary<uint,uint>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT design,item_uid FROM revora_planet_exchange WHERE uid=@uid",c)){
                q.Parameters.AddWithValue("uid",(long)uid);using(var r=q.ExecuteReader())while(r.Read())receipts.Add(Convert.ToUInt32(r[0]),Convert.ToUInt32(r[1]));
            }}
            Check(receipts.Count==257,"all257 exchange receipts survive process replacement");
            foreach(uint id in garments.Concat(mounts)){
                uint saved=receipts[id];
                ConquerItem item;Check(p.Entity.StorageItems.TryGetValue(saved,out item) && item.ID==id && item.InWardrobe && item.Bound && item.Days==0 && item.StackSize==1,"exact exchange reward recovered: "+id);
            }
            p.Entity.MyAchievement.Send();
            Check(HasAchievement(p,11231),"saved100garment receipt projects into the recovered achievement view");
            for(int i=0;i<257;i++)Check((string)Call(exchange,null,"Buy",p,i)=="You have already claimed that design.","recovered receipt refuses exchange replay: "+i);
            Check(!Fenced(p) && p.Entity.StorageItems.Count==276,"all replay refusals preserve the recovered ordinary session");
            System.Console.WriteLine("Full Planet collection restart checks passed: "+checks);return 0;
        }catch(Exception ex){System.Console.Error.WriteLine(ex.GetType().Name+"\n"+ex.StackTrace);return 1;}
    }
    static int PlanetRestartChild()
    {
        try {
            string connection=System.Console.ReadLine();if(string.IsNullOrEmpty(connection) || connection.Length>8192)return 2;
            var builder=new Npgsql.NpgsqlConnectionStringBuilder(connection);if(builder.SearchPath!="revora_potency_validation")return 2;
            DataHolder.ConnectionString=builder.ConnectionString;
            var values=new long[5];for(int n=0;n<values.Length;n++)values[n]=long.Parse(System.Console.ReadLine(),System.Globalization.CultureInfo.InvariantCulture);
            planetNow=new DateTime(values[0],DateTimeKind.Utc);uint interrupted=checked((uint)values[1]),cache=checked((uint)values[2]),winner=checked((uint)values[3]);
            DateTime expiry=new DateTime(values[4],DateTimeKind.Utc);
            var encounter=typeof(Entity).Assembly.GetType("Revora.Game.Features.PlanetEncounter");
            var maps=encounter.GetField("Maps",Hidden).GetValue(null);
            Check(Kernel.GamePool.Count==0 && (int)maps.GetType().GetProperty("Count").GetValue(maps,null)==0 && !(bool)Planet.GetField("initialized",Hidden).GetValue(null),
                "Planet child starts without sessions encounter objects or initialized progress caches");
            Planet.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>planetNow));
            TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>planetNow));
            ConquerItemInformation.Load();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
            Call(EvidenceType,null,"Initialize");Call(Planet,null,"Initialize");Call(encounter,null,"Initialize");Call(encounter,null,"RecoverInterrupted");
            Check(Scalar("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND state='abandoned' AND defeated>0 AND NOT returned",interrupted)==1,
                "fresh process abandons a saved partial fight without claiming victory");
            var town=World(Environment.CurrentDirectory,1002);DMaps.MapPaths[1002]="planet-restart-town";
            town.Npcs[900000016]=new NpcSpawn{UID=900000016,MapID=1002,X=319,Y=299,Mesh=50050,Type=Enums.NpcType.Talker};
            var lost=PlanetRestartPlayer(town,interrupted);Call(encounter,null,"OnLogin",lost);Call(Planet,null,"OnLogin",lost);
            Check(!Fenced(lost) && lost.Entity.MapID==1002 && Scalar("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND NOT returned",interrupted)==0
                && Scalar("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid AND stage='encounter' AND kills=100",interrupted)==1,
                "new ordinary session recovers safe return and retains its earned hunt");
            var carried=PlanetRestartPlayer(town,cache);Call(encounter,null,"OnLogin",carried);Call(Planet,null,"OnLogin",carried);
            Check(!Fenced(carried) && Scalar("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND state='victory' AND returned AND defeated=7",cache)==1,
                "new process acknowledges pending safe return without erasing the earned cache");
            PlanetTown(carried,town);PlanetNpc(carried,213);
            System.Console.WriteLine("Planet child delivery: fenced="+Fenced(carried)+" completions="+Scalar("SELECT count(*) FROM revora_planet_completions WHERE uid=@uid",cache));
            Check(!Fenced(carried) && Scalar("SELECT count(*) FROM revora_planet_completions WHERE uid=@uid",cache)==1
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='planet-revora-v1'",cache)==0,
                "recovered earned cache completes one weekend through Rowan without an early wing");
            var awarded=PlanetRestartPlayer(town,winner);new TitleStorage().Login(awarded);TitleRequest(60096009,TitleStorage.Action.Equip).Handle(awarded);
            if(Scalar("SELECT count(*) FROM revora_planet_exchange WHERE uid=@uid",winner)>0) {
                ConquerItemTable.LoadItems(awarded);new CoatStorage().Login(awarded);PlanetTown(awarded,town);
                uint saved=checked((uint)Scalar("SELECT item_uid FROM revora_planet_exchange WHERE uid=@uid AND design=189135",winner));
                Check(awarded.Entity.StorageItems.Values.Any(i=>i.UID==saved && i.ID==189135 && i.Bound && i.Days==0),"new process loads the exact permanent bound exchange gift in the wardrobe");
                Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.PlanetCollectionExchange",true),null,"Buy",awarded,0);
                Check(Scalar("SELECT count(*) FROM revora_planet_exchange WHERE uid=@uid",winner)==80 && Scalar("SELECT count(*) FROM revora_planet_exchange e JOIN items i ON i.uid=e.item_uid AND i.entityid=e.uid AND i.id=e.design WHERE e.uid=@uid AND i.inwardrobe=1 AND i.bound=1 AND i.days::text='0'",winner)==80,"new process preserves all eighty exact wardrobe gifts and refuses repeated exchange");
            }
            Check(OwnedTitles(awarded).ContainsKey(60096009) && awarded.Entity.EquippedWing==60096009,
                "new process reloads and equips the earned fourth-weekend wing");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT expires_at FROM revora_event_awards WHERE uid=@uid AND source='planet-revora-v1'",c)) {
                q.Parameters.AddWithValue("uid",(long)winner);using(var r=q.ExecuteReader())Check(r.Read() && r.GetFieldValue<DateTimeOffset>(0).UtcDateTime==expiry,
                    "fresh process preserves the original fourth-delivery expiry");
            }}
            Call(encounter,null,"OnLogin",carried);PlanetNpc(carried,213);new TitleStorage().Login(awarded);
            Check(Scalar("SELECT count(*) FROM revora_planet_completions WHERE uid=@uid",cache)==1
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='planet-revora-v1'",winner)==1,
                "repeated recovery and delivery in the new process cannot duplicate rewards");
            planetNow=expiry;new TitleStorage().Login(awarded);
            Check(!OwnedTitles(awarded).ContainsKey(60096009) && awarded.Entity.EquippedWing==0,
                "new process expires and unequips the wing at the original deadline");
            System.Console.WriteLine("Planet restart checks passed: "+checks);return 0;
        }catch(Exception ex){System.Console.Error.WriteLine("Planet restart failed: "+ex.GetType().Name+"\n"+ex.StackTrace);return 1;}
    }
}
