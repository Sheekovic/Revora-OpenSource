using System;
using Console=System.Console;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Forgotten=typeof(Entity).Assembly.GetType("Revora.Game.Features.ForgottenRealms",true);
    static DateTime forgottenNow=new DateTime(2030,4,1,12,0,0,DateTimeKind.Utc);
    static void ForgottenNpc(GameState p,byte option){Npcs.GetDialog(new NpcRequest(true){NpcID=p.ActiveNpc,OptionID=option},p);}
    static void ForgottenSetup(string runtime,Map town){
        Revora.Config.KillConquerPointsEnabled=false;Revora.Config.MoneyDropEnabled=false;
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Map.CreateTimerFactories();if(Revora.Thread.GenericThreadPool==null)Revora.Thread.GenericThreadPool=new System.Threading.StaticPool(1);
        MapsTable.Load();
        for(ushort id=1360;id<=1364;id++)new Map(id,"map/map/maidan-"+(char)('A'+id-1360)+".dmap");
        town.Npcs[900000016]=new NpcSpawn{UID=900000016,MapID=1002,X=319,Y=299,Mesh=50050,Type=Enums.NpcType.Talker};
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkLedger",true),null,"Initialize");
        Call(EvidenceType,null,"Initialize");Call(Travel,null,"Initialize");
        Forgotten.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>forgottenNow));
        Call(Forgotten,null,"Initialize");
    }
    static GameState ForgottenReload(Map town,uint uid){
        var p=PlanetRestartPlayer(town,uid);p.Entity.FullyLoaded=false;p.Entity.Vitality=100;p.Entity.Level=140;p.Entity.FullyLoaded=true;
        ConquerItemTable.LoadItems(p);PlanetTown(p,town);return p;
    }
    static Entity[] ForgottenMobs(GameState p){return p.Map.Entities.Values.Where(m=>m.MonsterInfo!=null && m.MonsterInfo.ID>=9926300 && m.MonsterInfo.ID<=9926304).ToArray();}
    static void ForgottenSlay(GameState p,Entity mob){p.Entity.X=mob.X;p.Entity.Y=mob.Y;mob.Die(p.Entity);}
    static void ForgottenGuide(GameState p){var npc=p.Map.Npcs[900000196];p.Entity.X=(ushort)(npc.X+2);p.Entity.Y=npc.Y;p.ActiveNpc=900000196;}
    static long ForgottenKills(GameState p){return Scalar("SELECT coalesce(sum(kills),0) FROM revora_forgotten_realms WHERE uid=@uid",p.Entity.UID);}
    static long ForgottenPaid(GameState p){return Scalar("SELECT count(*) FROM revora_forgotten_realms WHERE uid=@uid AND reward_uid IS NOT NULL",p.Entity.UID);}
    static void VerifyForgotten(string runtime,Map town){
        Query("ALTER TABLE entities ADD COLUMN my_title integer DEFAULT 0, ADD COLUMN level integer DEFAULT 140, ADD COLUMN mapid integer DEFAULT 1002, ADD COLUMN x integer DEFAULT 319, ADD COLUMN y integer DEFAULT 299");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        Query("CREATE TABLE maps AS SELECT * FROM public.maps WHERE id BETWEEN 1360 AND 1364;CREATE TABLE npcs AS SELECT * FROM public.npcs WHERE mapid BETWEEN 1360 AND 1364;CREATE TABLE sobnpcs AS SELECT * FROM public.sobnpcs WHERE mapid BETWEEN 1360 AND 1364;CREATE TABLE furniture AS SELECT * FROM public.furniture WHERE mapid BETWEEN 1360 AND 1364;INSERT INTO monsterspawns SELECT * FROM public.monsterspawns WHERE mapid BETWEEN 1360 AND 1364");
        ForgottenSetup(runtime,town);ConquerItem.ItemUID=new Counter(4950000);
        var p=PlanetPlayer(town);p.Entity.FullyLoaded=false;p.Entity.Vitality=100;p.Entity.FullyLoaded=true;PlanetTown(p,town);
        p.Entity.Level=99;ForgottenNpc(p,11);Check(p.Entity.MapID==1002,"under-level ordinary admission refused");p.Entity.Level=140;
        p.Entity.X=340;ForgottenNpc(p,11);Check(p.Entity.MapID==1002,"remote entrance refused");PlanetTown(p,town);
        var stranger=PlanetPlayer(town);PlanetTown(stranger,town);
        for(int i=0;i<5;i++){
            ForgottenNpc(p,(byte)(11+i));ushort map=(ushort)(1360+i);
            Check(!Fenced(p) && p.Entity.MapID==map && p.Account.State==AccountTable.AccountState.Player,"ordinary NPC enters realm "+map);
            Check(Scalar("SELECT count(*) FROM revora_travel_visits WHERE uid=@uid AND map_id="+map+" AND arrival=3",p.Entity.UID)==1,"entrance earns exact world travel receipt "+map);
            var mobs=ForgottenMobs(p);Check(mobs.Length==10 && mobs.All(m=>!m.Dead),"ten live owned sentries in realm "+map);
            ForgottenGuide(p);ForgottenNpc(p,2);Check(ForgottenPaid(p)==i,"unfinished hunt pays nothing "+map);
            int count=i==4?5:10;for(int k=0;k<count;k++)ForgottenSlay(p,mobs[k]);
            Check(!Fenced(p) && ForgottenKills(p)==i*10+count,"actual deaths credit the admitted killer "+map);
            Parallel.For(0,8,k=>Call(Forgotten,null,"OnDeath",p,mobs[0]));
            Call(Forgotten,null,"OnDeath",stranger,mobs[0]);
            Check(ForgottenKills(p)==i*10+count && ForgottenKills(stranger)==0,"duplicate and unrelated death callbacks refused "+map);
            if(i==4)break;
            ForgottenGuide(p);
            if(i==0){
                var filled=Enumerable.Range(0,40).Select(k=>Pack(p,611004,1)).ToArray();ForgottenNpc(p,2);
                Check(ForgottenPaid(p)==0,"full bag preserves unclaimed rune");
                foreach(var item in filled){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,true);}
                Forgotten.GetField("BeforeCommit",Hidden).SetValue(null,(Action)(()=>{throw new InvalidOperationException("fixture rollback");}));
                try{ForgottenNpc(p,2);}finally{Forgotten.GetField("BeforeCommit",Hidden).SetValue(null,(Action)(()=>{}));}
                Check(Fenced(p) && ForgottenPaid(p)==0 && CountId(p,4030101)==0,"injected reward failure rolls back item and receipt");
                p=ForgottenReload(town,p.Entity.UID);ForgottenNpc(p,11);ForgottenGuide(p);
            }
            Parallel.For(0,8,k=>ForgottenNpc(p,2));
            Check(!Fenced(p) && ForgottenPaid(p)==i+1 && CountId(p,4030101)==i+1,"concurrent claim grants one bound rune "+map);
            ForgottenNpc(p,2);Check(ForgottenPaid(p)==i+1,"claim replay cannot duplicate rune "+map);
            ForgottenNpc(p,1);Check(p.Entity.MapID==1002 && p.Entity.X==319 && p.Entity.Y==299,"Trail Keeper returns to Rowan "+map);p.ActiveNpc=900000016;
        }
        uint uid=p.Entity.UID;Call(typeof(Entity).Assembly.GetType("Revora.Database.AccountActivity"),null,"Relinquish");
        var start=new System.Diagnostics.ProcessStartInfo{FileName=typeof(PotencyHuntTests).Assembly.Location,Arguments="--forgotten-restart-check",WorkingDirectory=runtime,
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var child=System.Diagnostics.Process.Start(start)){
            var output=child.StandardOutput.ReadToEndAsync();var errors=child.StandardError.ReadToEndAsync();
            child.StandardInput.WriteLine(DataHolder.ConnectionString);child.StandardInput.WriteLine(uid);child.StandardInput.Close();
            if(!child.WaitForExit(30000)){child.Kill();child.WaitForExit();throw new Exception("Expedition recovery timeout.");}
            string result=output.GetAwaiter().GetResult(),error=errors.GetAwaiter().GetResult();
            if(child.ExitCode!=0){Console.WriteLine(result);Console.WriteLine(error);}
            Check(child.ExitCode==0 && error.Length==0,"new process reloads four rewards and resumes the fifth hunt");
            Console.WriteLine(result.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Last());
        }
        Check(Scalar("SELECT count(*) FROM revora_forgotten_realms r JOIN items i ON i.uid=r.reward_uid AND i.entityid=r.uid WHERE r.uid=@uid AND i.id=4030101 AND i.bound=1",uid)==5,"all five earned reward identities remain durable after child exit");
    }
    static int ForgottenRestartChild(){try{
        var b=new Npgsql.NpgsqlConnectionStringBuilder(Console.ReadLine());
        if(b.Host!="127.0.0.1" || b.Port!=25439 || b.Database!="revora_candidate_game" || b.SearchPath!="revora_potency_validation")return 2;
        DataHolder.ConnectionString=b.ConnectionString;uint uid=uint.Parse(Console.ReadLine());string runtime=Environment.CurrentDirectory;
        ConquerItemInformation.Load();var catalog=new Npgsql.NpgsqlConnectionStringBuilder(b.ConnectionString);catalog.SearchPath="public";DataHolder.ConnectionString=catalog.ConnectionString;
        try{MonsterInformation.Load();InnerPowerTable.LoadDBInformation();}finally{DataHolder.ConnectionString=b.ConnectionString;}
        typeof(CompanionTests).GetMethod("Reset",Hidden).Invoke(null,null);var town=World(runtime,1002);ForgottenSetup(runtime,town);
        ConquerItem.ItemUID=new Counter(4960000);var p=ForgottenReload(town,uid);
        Check(CountId(p,4030101)==4 && ForgottenPaid(p)==4 && ForgottenKills(p)==45,"restart restores four exact earned runes and partial hunt");
        ForgottenNpc(p,11);ForgottenGuide(p);ForgottenNpc(p,2);Check(ForgottenPaid(p)==4 && CountId(p,4030101)==4,"restart replay cannot pay an old hunt twice");
        ForgottenNpc(p,1);p.ActiveNpc=900000016;ForgottenNpc(p,15);
        foreach(var mob in ForgottenMobs(p).Take(5))ForgottenSlay(p,mob);
        ForgottenGuide(p);ForgottenNpc(p,2);Check(!Fenced(p) && ForgottenPaid(p)==5 && CountId(p,4030101)==5,"remaining earned kills unlock final rune after restart");
        ForgottenNpc(p,1);Check(p.Entity.MapID==1002,"last realm safe return survives restart");
        Check(Scalar("SELECT count(*) FROM revora_travel_visits WHERE uid=@uid AND map_id BETWEEN 1360 AND 1364",uid)==5,"all five travel achievements retain original receipts");
        forgottenNow=forgottenNow.AddDays(7);p.ActiveNpc=900000016;ForgottenNpc(p,11);ForgottenGuide(p);ForgottenNpc(p,2);
        Check(ForgottenPaid(p)==5 && Scalar("SELECT count(*) FROM revora_forgotten_realms WHERE uid=@uid",uid)==6,"next UTC week starts empty without reusing old kills");
        var revived=ForgottenMobs(p)[0];ForgottenSlay(p,revived);
        Call(typeof(Map),null,"_timerCallBack",p.Map,revived.DeathStamp.AddSeconds(46).TotalMilliseconds);
        Check(!revived.Dead,"ordinary map timer revives a Lost Sentry");ForgottenSlay(p,revived);
        Check(ForgottenKills(p)==52,"respawned life earns one new kill without reusing the prior death");
        Console.WriteLine("Forgotten restart checks passed: "+checks);return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex.GetType().Name+": "+ex.Message+"\n"+ex.StackTrace);return 1;}}
}
