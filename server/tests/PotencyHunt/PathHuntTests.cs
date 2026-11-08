using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly string[] PathKeys={PathHuntSystem.PheasantKey,PathHuntSystem.RobinKey,PathHuntSystem.DoveKey,PathHuntSystem.ApparitionKey};
    static long PathClaims(GameState p){return Scalar("SELECT count(*) FROM revora_path_hunt_claims WHERE uid=@uid",p.Entity.UID);}
    static void PathNpc(GameState p,byte option){ExplorationNpc(p,PathHuntSystem.NpcId,option);}
    static void PathReturn(GameState p){p.Entity.Teleport(1002,320,299);PathNpc(p,0);}
    static GameState ReloadPath(GameState p){p=ReloadBall(p);EntityVariableTable.Load(p);p.GemAlgorithm();p.CalculateStatBonus();p.CalculateHPBonus();return p;}
    static GameState PathPlayer(Map town,byte life=0,byte level=50,bool legacy=false){
        var p=BallPlayer(town);p.Entity.Reborn=life;p.Entity.Level=level;p.Entity.Vitality=100;p.Entity.X=320;p.Entity.Y=299;p.Variables=new VariableVault();
        if(legacy)p[PathHuntSystem.LifeKey]=(uint)(life+1);
        Query("INSERT INTO variablevault(entityid,data) VALUES(@uid,@data)","uid",(long)p.Entity.UID,"data",p.Variables.Serialize());
        if(!EntityTable.SaveEntity(p))throw new Exception("Path fixture save failed.");
        PathNpc(p,1);Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && (uint)p[PathHuntSystem.LifeKey]==life+1
            && (uint)p[PathHuntSystem.RewardKey]==(legacy?0:1),"ordinary Path Hunt acceptance preserves new/legacy reward contract");return p;
    }
    static void ReadyPath(GameState p){p[PathKeys[0]]=Config.PathHuntPheasantKills;p[PathKeys[1]]=Config.PathHuntRobinKills;p[PathKeys[2]]=Config.PathHuntDoveKills;p[PathKeys[3]]=Config.PathHuntApparitionKills;EntityVariableTable.Save(p);}
    static GameState EarnPath(GameState p){
        string[] names={"Pheasant","Robin","Turtledove","Apparition"};uint[] quotas={Config.PathHuntPheasantKills,Config.PathHuntRobinKills,Config.PathHuntDoveKills,Config.PathHuntApparitionKills};
        for(int stage=0;stage<4;stage++){
            PathReturn(p);PathNpc(p,(byte)(stage+3));var x=p.Entity.X;var y=p.Entity.Y;
            var family=MonsterInformation.MonsterInformations.Values.First(m=>m.Name==names[stage]);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT count(*) FROM public.monsterspawns WHERE mapid=1002 AND npctype=@id",c)){
                q.Parameters.AddWithValue("id",(long)family.ID);Check(Convert.ToInt64(q.ExecuteScalar())>0,"installed Twin City source for Path Hunt: "+names[stage]);}}
            for(int n=0;n<quotas[stage];n++){
                var mob=HuntMonster(p,names[stage]);mob.Hitpoints=mob.MaxHitpoints=100;mob.IsDropped=true;mob.ClearFlagsMobs();uint damage=mob.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,mob,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=mob.UID},ref damage,null);if(!mob.Dead)throw new Exception("Path combat survived uid="+mob.UID+" hp="+mob.Hitpoints+" damage="+damage+" life="+p.Entity.Reborn);PathHuntSystem.OnMonsterKill(p,mob);
                if(Fenced(p))throw new Exception("Path hunt death fenced its owner.");
                if(n==9){EntityTable.SaveEntity(p);p=ReloadPath(p);p.Entity.X=x;p.Entity.Y=y;Check((uint)p[PathKeys[stage]]==10,"Path Hunt partial earning survives reconnect: "+names[stage]+" life="+p.Entity.Reborn+" count="+(uint)p[PathKeys[stage]]);}
            }
            Check((uint)p[PathKeys[stage]]==quotas[stage],"actual owned deaths meet Path Hunt quota without corpse replay: "+names[stage]);
        }
        PathReturn(p);return p;
    }
    static void VerifyPathHunt(string runtime,Map town){
        VerifyFirstRebirth(runtime,town);Query("CREATE TABLE IF NOT EXISTS variablevault(entityid bigint PRIMARY KEY,data bytea NOT NULL)");
        PathHuntSystem.Create();Call(typeof(PathHuntSystem),null,"InitializeRewards");
        foreach(byte life in new byte[]{0,1,2})foreach(bool legacy in new[]{false,true}){
            var p=EarnPath(PathPlayer(town,life,50,legacy));if(!EntityTable.SaveEntity(p))throw new Exception("Path preclaim save failed.");
            ulong gold=p.Entity.Money;uint rvp=p.Entity.RevoraPoint;byte target=life==0?Config.PathHuntPuff0:life==1?Config.PathHuntPuff1:Config.PathHuntPuff2;
            Parallel.For(0,12,i=>PathNpc(p,2));
            var souls=p.Inventory.Objects.Where(i=>i.ID>=800000 && i.ID<900000).ToArray();
            Check(!Fenced(p) && PathClaims(p)==1 && souls.Length==(legacy?0:1) && souls.All(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)
                && p.Inventory.Objects.Count(i=>i.ID==Config.PathHuntExpPotionId)==Config.PathHuntExpPotions && p.Entity.Level==target && p.Entity.Experience==0
                && p.Entity.Money==gold+Config.PathHuntGold && p.Entity.RevoraPoint==rvp+Config.PathHuntRevoraPoints,"earned Path Hunt pays exact frozen reward once: life="+life+" legacy="+legacy);
            var ids=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();ushort[] stats={p.Entity.Strength,p.Entity.Agility,p.Entity.Vitality,p.Entity.Spirit,p.Entity.Atributes};
            p=ReloadPath(p);PathReturn(p);PathNpc(p,1);PathNpc(p,2);
            Check(!Fenced(p) && PathClaims(p)==1 && p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(ids) && p.Inventory.Objects.Where(i=>i.ID>=800000 && i.ID<900000).All(i=>i.Bound)
                && p.Entity.Level==target && p.Entity.Experience==0 && p.Entity.Money==gold+Config.PathHuntGold && p.Entity.RevoraPoint==rvp+Config.PathHuntRevoraPoints
                && new ushort[]{p.Entity.Strength,p.Entity.Agility,p.Entity.Vitality,p.Entity.Spirit,p.Entity.Atributes}.SequenceEqual(stats),"fresh character/item/variable reload retains earned Path reward and refuses replay");
        }
        var racing=PathPlayer(town);ReadyPath(racing);
        Parallel.For(0,12,i=>{if(i%3==0){if(!EntityTable.SaveEntity(racing))throw new Exception("Concurrent Path autosave failed.");}else PathNpc(racing,2);});
        racing=ReloadPath(racing);
        Check(!Fenced(racing) && PathClaims(racing)==1 && racing.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000)==1,
            "separate Path race fixture keeps one reward under concurrent autosave and claim");
        foreach(string fault in new[]{"purse","profile","soul","potion","variables","receipt"}){
            var p=PathPlayer(town);ReadyPath(p);string table=fault=="variables"?"variablevault":fault=="receipt"?"revora_path_hunt_claims":fault=="soul" || fault=="potion"?"items":"entities";
            string verb=table=="items" || fault=="receipt"?"INSERT":"UPDATE";
            string when=fault=="profile"?" WHEN(NEW.level<>OLD.level)":fault=="soul"?" WHEN(NEW.id BETWEEN 800000 AND 899999)":fault=="potion"?" WHEN(NEW.id="+Config.PathHuntExpPotionId+")":"";
            Query("CREATE FUNCTION suppress_path() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_path BEFORE "+verb+" ON "+table+" FOR EACH ROW"+when+" EXECUTE FUNCTION suppress_path()");
            try{PathNpc(p,2);}finally{Query("DROP TRIGGER suppress_path ON "+table+";DROP FUNCTION suppress_path()");}
            Check(Fenced(p) && PathClaims(p)==0 && p.Inventory.Count==0 && p.Entity.Level==50,"failed "+fault+" rolls back the whole Path Hunt claim");
            p=ReloadPath(p);PathReturn(p);Check(!Fenced(p) && p.Entity.Level==50 && p.Entity.Money==0 && p.Entity.RevoraPoint==0 && (uint)p[PathHuntSystem.DoneKey]==0 && (uint)p[PathHuntSystem.RewardKey]==1
                && PathKeys.All(key=>(uint)p[key]==20),"fresh reload retains unpaid Path Hunt contract after "+fault);
            PathNpc(p,2);Check(!Fenced(p) && PathClaims(p)==1 && p.Inventory.Count==1+Config.PathHuntExpPotions,"Path Hunt claim retries once after "+fault);
        }
        foreach(string field in new[]{"money","revorapoint","experience"}){
            var p=PathPlayer(town);ReadyPath(p);Query("UPDATE entities SET "+field+"="+field+"+1 WHERE uid=@uid","uid",(long)p.Entity.UID);PathNpc(p,2);
            Check(Fenced(p) && PathClaims(p)==0 && p.Inventory.Count==0,"Path Hunt refuses stale "+field);
            p=ReloadPath(p);PathReturn(p);PathNpc(p,2);Check(!Fenced(p) && PathClaims(p)==1,"Path Hunt uses reloaded "+field+" preimage");
        }
        var high=PathPlayer(town,0,135);ReadyPath(high);for(int i=0;i<29;i++)Pack(high,1088001,1);PathNpc(high,2);
        Check(!Fenced(high) && PathClaims(high)==0,"Path Hunt capacity includes its bound soul and extra potions");DisRemove(high,high.Inventory.Objects.First());PathNpc(high,2);
        Check(!Fenced(high) && PathClaims(high)==1 && high.Inventory.Count==40 && high.Entity.Level==135 && high.Inventory.Objects.Count(i=>i.ID==Config.PathHuntExpPotionId)==11,"Path Hunt exact capacity preserves the high-level potion alternative");
        high[PathHuntSystem.DoneKey]=0u;high[PathHuntSystem.LifeKey]=1u;ReadyPath(high);PathNpc(high,2);high=ReloadPath(high);
        Check(!Fenced(high) && PathClaims(high)==1 && (uint)high[PathHuntSystem.DoneKey]==1,"Path Hunt receipt durably repairs stale completion without repayment");
        var old=PathPlayer(town);ReadyPath(old);old[PathHuntSystem.DoneKey]=1u;EntityVariableTable.Save(old);PathNpc(old,2);
        Check(!Fenced(old) && PathClaims(old)==0,"old paid Path Hunt remains paid without a retroactive soul");
        var remote=PathPlayer(town);ReadyPath(remote);remote.Entity.X=10;remote.Entity.Y=10;PathNpc(remote,2);
        Check(!Fenced(remote) && PathClaims(remote)==0,"remote Path Hunt continuation cannot claim");
        foreach(string field in new[]{"money","revorapoint"}){
            var p=PathPlayer(town);ReadyPath(p);Query("UPDATE entities SET "+field+"=@value WHERE uid=@uid","value",field=="money"?long.MaxValue:(long)uint.MaxValue,"uid",(long)p.Entity.UID);
            p=ReloadPath(p);PathReturn(p);PathNpc(p,2);Check(!Fenced(p) && PathClaims(p)==0,"Path Hunt refuses overflowing "+field);
        }
        var hunter=PathPlayer(town);PathNpc(hunter,3);
        foreach(string invalid in new[]{"alive","removed","other-map","guard","companion","far","wrong-owner","fake-family"}){
            var mob=HuntMonster(hunter,"Pheasant");if(invalid=="alive")mob.Hitpoints=100;if(invalid=="removed")hunter.Map.Entities.Remove(mob.UID);
            if(invalid=="other-map")mob.MapID=1015;if(invalid=="guard")mob.MonsterInfo.Guard=true;if(invalid=="companion")mob.Companion=true;
            if(invalid=="far")mob.X=(ushort)(hunter.Entity.X+40);if(invalid=="wrong-owner")mob.MonsterInfo.Owner=null;if(invalid=="fake-family")mob.MonsterInfo.ID=uint.MaxValue;
            PathHuntSystem.OnMonsterKill(hunter,mob);Check(!Fenced(hunter) && (uint)hunter[PathKeys[0]]==0,"Path Hunt rejects invalid kill source: "+invalid);
        }
        var corpse=HuntMonster(hunter,"Pheasant");PathHuntSystem.OnMonsterKill(hunter,corpse);PathHuntSystem.OnMonsterKill(hunter,corpse);
        Check((uint)hunter[PathKeys[0]]==1,"Path Hunt corpse replay cannot double credit");
        corpse.Hitpoints=corpse.MaxHitpoints=100;corpse.ClearFlagsMobs();Call(typeof(PathHuntSystem),null,"Revived",corpse);corpse.IsDropped=true;corpse.Die(hunter.Entity);
        Check(!Fenced(hunter) && (uint)hunter[PathKeys[0]]==2,"Path Hunt revival starts a new monster credit");
        PathHuntSystem.OnReborn(hunter);EntityVariableTable.Load(hunter);
        Check((uint)hunter[PathHuntSystem.LifeKey]==0 && (uint)hunter[PathHuntSystem.RewardKey]==0 && PathKeys.All(key=>(uint)hunter[key]==0),"Path Hunt rebirth reset clears both progress and reward contract");
        VerifyCareerVariableMerges(town);
        VerifyEarnedCareer(runtime,town);
    }

    static void VerifyCareerVariableMerges(Map town) {
        Action<GameState,string,uint> external=(actor,key,value)=> {
            byte[] image;using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT data FROM variablevault WHERE entityid=@uid",c)){
                q.Parameters.AddWithValue("uid",(long)actor.Entity.UID);image=(byte[])q.ExecuteScalar();}}
            var saved=new VariableVault();saved.Deserialize(image);saved[key]=value;
            Query("UPDATE variablevault SET data=@data WHERE entityid=@uid","data",saved.Serialize(),"uid",(long)actor.Entity.UID);
        };
        var merged=PathPlayer(town);ReadyPath(merged);merged["career.local"]=11u;external(merged,"career.remote",22u);PathNpc(merged,2);
        Check(!Fenced(merged) && PathClaims(merged)==1 && (uint)merged["career.local"]==11 && (uint)merged["career.remote"]==22,
            "quest reward merges independent local and durable variables instead of overwriting another quest");
        merged["career.later"]=33u;EntityVariableTable.Save(merged);merged=ReloadPath(merged);
        Check(!Fenced(merged) && (uint)merged["career.local"]==11 && (uint)merged["career.remote"]==22
            && (uint)merged["career.later"]==33 && (uint)merged[PathHuntSystem.DoneKey]==1,
            "post-reward variable save uses the committed baseline and preserves all merged progress on reload");
        var changed=PathPlayer(town);ReadyPath(changed);external(changed,PathHuntSystem.LifeKey,2u);PathNpc(changed,2);
        Check(Fenced(changed) && PathClaims(changed)==0 && changed.Inventory.Count==0,
            "changed durable quest admission rejects reward and all tentative writes");
        changed=ReloadPath(changed);Check((uint)changed[PathHuntSystem.LifeKey]==2,"rejected reward preserves newer durable quest admission");
        var conflict=PathPlayer(town);ReadyPath(conflict);conflict["career.conflict"]=1u;EntityVariableTable.Save(conflict);
        conflict["career.conflict"]=2u;external(conflict,"career.conflict",3u);PathNpc(conflict,2);
        Check(Fenced(conflict) && PathClaims(conflict)==0 && conflict.Inventory.Count==0,
            "conflicting edits to an unrelated quest variable roll back the reward safely");
        conflict=ReloadPath(conflict);Check((uint)conflict["career.conflict"]==3,"conflict reload retains the newer saved quest variable");
    }

    static void VerifyEarnedCareer(string runtime,Map town){
        foreach(ushort id in new ushort[]{1015,1020,1000,1011})World(runtime,id);
        SoulTrialSystem.Create();CourierLoopSystem.Create();CpShopClerkSystem.Create();
        CityGuardSystem.SpawnTalker(CityGuardSystem.CommanderNpcId,1002,CityGuardSystem.CommanderX,CityGuardSystem.CommanderY,50000,"City Commander");
        Call(EvidenceType,null,"Initialize");
        var firstClock=FirstRite.GetField("Clock",Hidden).GetValue(null);
        var secondClock=SecondRite.GetField("Clock",Hidden).GetValue(null);
        var titleClock=TitleRules.GetField("Clock",Hidden).GetValue(null);
        FirstRite.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));
        SecondRite.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));
        try{
            var p=PathPlayer(town,0,1);p.Entity.Class=10;p.GemAlgorithm();p.CalculateStatBonus();p.CalculateHPBonus();EntityTable.SaveEntity(p);p=EarnPath(p);uint uid=p.Entity.UID;PathNpc(p,2);
            Check(!Fenced(p) && PathClaims(p)==1,"career journey earns Path Hunt final receipt through NPC");
            TrialReturn(p);TrialNpc(p,1);p=EarnTrial(p);TrialNpc(p,2);
            Check(!Fenced(p) && TrialClaims(p)==1,"same career character earns Soul Trial final receipt");
            CourierReturn(p);CourierNpc(p,1);p=EarnCourier(p);CourierNpc(p,2);
            Check(!Fenced(p) && CourierClaims(p)==1,"same career character earns Courier final receipt");
            EvidenceRefresh(p);RefreshTitles(p);
            Check(!OwnedTitles(p).ContainsKey(20572057),"three earned quests alone do not unlock Honored Hero");
            // Quest rewards earn rite levels; trainer actions earn both master ranks.
            // Never insert final receipts or replace this character between routes.
            Check(p.Entity.Level==120,"first-life quest rewards earn the first rite level without a level override");
            p=EarnCareerPromotions(runtime,town,p);p.Entity.Body=1003;
            EarnCareerGems(runtime,town,p);
            p.Entity.Teleport(1212,517,732);
            var floorBefore=p.Map.FloorItems.Keys.ToArray();var water=WaterMonster(p);
            int savedRoll=waterRoll;waterRoll=0;
            try{water.Hitpoints=water.MaxHitpoints=100;water.IsDropped=false;uint damage=water.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,water,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=water.UID},ref damage,null);}
            finally{waterRoll=savedRoll;}
            var drop=p.Map.FloorItems.Values.Single(i=>!floorBefore.Contains(i.UID) && i.ItemID==721258);
            Loot(p,drop);
            Check(!Fenced(p) && Waters(p)==1 && p.Inventory.ContainsUID(drop.Item.UID),
                "career character picks up its own Waterlord Clean Water with durable provenance");
            p.Entity.Teleport(1002,(ushort)(town.Npcs[59558].X-2),(ushort)(town.Npcs[59558].Y-2));
            if(!EntityTable.SaveEntity(p))throw new Exception("Career first rite save failed");
            LoadAppearance(p);p.ActiveNpc=59558;StoneNpc(p,38);StoneNpc(p,39);
            Check(!Fenced(p) && Stones(p)==1 && p.Inventory.Contains(721259,1),
                "career character crafts its Heavenly Stone from hunted water and seven mined gems");
            RebirthOffer(p);StoneNpc(p,214);
            Check(!Fenced(p) && p.Entity.Reborn==1 && Rebirths(p)==1,"career first rite produces its own final receipt");
            EvidenceRefresh(p);RefreshTitles(p);
            Check(!OwnedTitles(p).ContainsKey(20572057),"four earned routes still require second rebirth");
            p=ReloadPath(p);
            Check(p.Entity.Level==15 && p.Entity.Reborn==1,"first rite persists the reset before second-life progression");
            PathReturn(p);PathNpc(p,1);p=EarnPath(p);PathNpc(p,2);
            TrialReturn(p);TrialNpc(p,1);p=EarnTrial(p);TrialNpc(p,2);
            CourierReturn(p);CourierNpc(p,1);p=EarnCourier(p);CourierNpc(p,2);
            Check(!Fenced(p) && p.Entity.Level==120 && PathClaims(p)==2 && TrialClaims(p)==2 && CourierClaims(p)==2,
                "same character earns the second rite level through three per-life quest rewards");
            p=ReloadPath(p);
            Check(p.Entity.Level==120,"earned second-life level survives full reload before the rite");
            p=EarnCareerPromotions(runtime,town,p);
            if(!EntityTable.SaveEntity(p))throw new Exception("Career second rite save failed");
            EShopFile.Load();Call(TokenShop,null,"Initialize");
            uint cpBeforeToken=p.Entity.ConquerPoints;
            uint tokenPrice=ConquerItemInformation.BaseInformations[723701].ConquerPointsWorth;
            ItemUsage.Process(BuyToken().ToArray(),p);
            Check(!Fenced(p) && tokenPrice>0 && cpBeforeToken>=tokenPrice && TokenPurchases(p)==1
                && p.Inventory.Contains(723701,1) && p.Entity.ConquerPoints==cpBeforeToken-tokenPrice,
                "career character buys its Exemption Token through the native shop path using earned Courier CPs");
            p=ReloadPath(p);p.Entity.Teleport(1002,(ushort)(town.Npcs[59558].X-2),(ushort)(town.Npcs[59558].Y-2));p.ActiveNpc=59558;
            // Hold the consumer at the exact game-clock completion boundary. DB wall time may be ahead.
            DateTime completed=DateTime.UtcNow;completed=new DateTime(completed.Ticks-completed.Ticks%10,DateTimeKind.Utc);
            SecondRite.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>completed));
            TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>completed));
            SecondOffer(p);StoneNpc(p,180);
            Check(RiteReceiptUtc(p,true)==completed && OwnedTitles(p).ContainsKey(20572057),"earned career title appears at the exact game-clock second-rite boundary");
            TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>completed.AddTicks(-10)));
            Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",completed.AddTicks(-10),true);RefreshTitles(p);
            Check(!OwnedTitles(p).ContainsKey(20572057),"career title still rejects a snapshot before its earned boundary");
            TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>completed));
            EvidenceRefresh(p);RefreshTitles(p);
            Check(!Fenced(p) && p.Entity.UID==uid && !p.Fake && p.Account.State==AccountTable.AccountState.Player
                && p.Entity.Reborn==2 && SecondCount(p)==1 && OwnedTitles(p).ContainsKey(20572057),
                "one ordinary character earns all five route receipts and Honored Hero: fenced="+Fenced(p)+" life="+p.Entity.Reborn+" second="+SecondCount(p)+" title="+OwnedTitles(p).ContainsKey(20572057)+" level="+p.Entity.Level+" class="+p.Entity.Class);
            new TitleStorage().Login(p);TitleRequest(20572057,TitleStorage.Action.Equip).Handle(p);
            p=ReloadPath(p);new TitleStorage().Login(p);
            Check(OwnedTitles(p).ContainsKey(20572057) && p.Entity.EquippedTitle==20572057
                && PathClaims(p)==2 && TrialClaims(p)==2 && CourierClaims(p)==2 && Rebirths(p)==1 && SecondCount(p)==1,
                "earned career title remains equipped after full character and inventory reload");
            Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='honored-hero-revora-v1' AND source_key='career-v1' AND expires_at IS NULL",uid)==1,
                "earned career reconciliation persists exactly one permanent award");
            VerifyCareerRestartProcess(p);
        }finally{
            FirstRite.GetField("Clock",Hidden).SetValue(null,firstClock);
            SecondRite.GetField("Clock",Hidden).SetValue(null,secondClock);
            TitleRules.GetField("Clock",Hidden).SetValue(null,titleClock);
        }
    }

    static string CareerItems(uint uid) {
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT md5(COALESCE(string_agg(uid::text||':'||id::text||':'||bound::text||':'||stacksize::text,',' ORDER BY uid),'')) FROM items WHERE entityid=@uid",c)){
            q.Parameters.AddWithValue("uid",(long)uid);return (string)q.ExecuteScalar();}}
    }
    static void VerifyCareerRestartProcess(GameState earned) {
        uint uid=earned.Entity.UID;string items=CareerItems(uid);
        var activity=typeof(Entity).Assembly.GetType("Revora.Database.AccountActivity");
        Call(activity,null,"Relinquish");
        try {
            var start=new System.Diagnostics.ProcessStartInfo {
                FileName=typeof(PotencyHuntTests).Assembly.Location,Arguments="--career-restart-check",
                WorkingDirectory=Environment.CurrentDirectory,UseShellExecute=false,CreateNoWindow=true,
                RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            using(var child=System.Diagnostics.Process.Start(start)) {
                var output=child.StandardOutput.ReadToEndAsync();var errors=child.StandardError.ReadToEndAsync();
                child.StandardInput.WriteLine(DataHolder.ConnectionString);
                child.StandardInput.WriteLine(uid.ToString(System.Globalization.CultureInfo.InvariantCulture));
                child.StandardInput.WriteLine(items);
                child.StandardInput.WriteLine(earned.Entity.Money.ToString(System.Globalization.CultureInfo.InvariantCulture));
                child.StandardInput.WriteLine(earned.Entity.ConquerPoints.ToString(System.Globalization.CultureInfo.InvariantCulture));
                child.StandardInput.Close();
                if(!child.WaitForExit(30000)){child.Kill();child.WaitForExit();throw new Exception("Career restart child timed out.");}
                string result=output.GetAwaiter().GetResult(),error=errors.GetAwaiter().GetResult();
                if(child.ExitCode!=0)System.Console.WriteLine(error);
                Check(child.ExitCode==0 && error.Length==0 && result.Contains("Career restart checks passed:"),"fresh process reloads the earned career and permanent equipped title without parent memory");
                System.Console.WriteLine(result.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Last());
            }
            Check(CareerItems(uid)==items && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='honored-hero-revora-v1' AND source_key='career-v1' AND expires_at IS NULL",uid)==1,
                "fresh-process reconciliation preserves item identities and one permanent career award");
        }finally{Call(activity,null,"Initialize");}
    }
    static int CareerRestartChild() {
        try {
            string connection=System.Console.ReadLine();if(string.IsNullOrEmpty(connection) || connection.Length>8192)return 2;
            var builder=new Npgsql.NpgsqlConnectionStringBuilder(connection);if(builder.SearchPath!="revora_potency_validation")return 2;
            DataHolder.ConnectionString=builder.ConnectionString;
            uint uid=uint.Parse(System.Console.ReadLine(),System.Globalization.CultureInfo.InvariantCulture);
            string items=System.Console.ReadLine();ulong silver=ulong.Parse(System.Console.ReadLine(),System.Globalization.CultureInfo.InvariantCulture);
            uint cps=uint.Parse(System.Console.ReadLine(),System.Globalization.CultureInfo.InvariantCulture);
            Check(uid!=0 && Kernel.GamePool.Count==0,"career child starts without inherited player sessions");
            ConquerItemInformation.Load();
            var catalogs=new Npgsql.NpgsqlConnectionStringBuilder(builder.ConnectionString);catalogs.SearchPath="public";
            try{DataHolder.ConnectionString=catalogs.ConnectionString;Refinery.LoadItems();}
            finally{DataHolder.ConnectionString=builder.ConnectionString;}
            StorageManager.Load();StorageItem.Load();
            PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);DataHolder.ReadStats();SpellTable.Load();RunesSystem.LoadRunesEXP();
            InnerPowerTable.LoadDBInformation();Call(EvidenceType,null,"Initialize");
            var town=World(Environment.CurrentDirectory,1002);
            var p=ReloadPath(PlanetRestartPlayer(town,uid));
            Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && p.Entity.Reborn==2
                && PathClaims(p)==2 && TrialClaims(p)==2 && CourierClaims(p)==2 && Rebirths(p)==1 && SecondCount(p)==1,
                "career cold entity load retains both earned lives and all five prerequisite receipts");
            new TitleStorage().Login(p);
            Check(OwnedTitles(p).ContainsKey(20572057) && p.Entity.EquippedTitle==20572057,
                "career title login restores the permanent equipped Honored Hero");
            Check(CareerItems(uid)==items && p.Entity.Money==silver && p.Entity.ConquerPoints==cps,
                "cold career recovery preserves earned item identities, silver and CPs");
            new TitleStorage().Login(p);RefreshTitles(p);
            Check(!Fenced(p) && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='honored-hero-revora-v1' AND source_key='career-v1' AND expires_at IS NULL",uid)==1,
                "cold repeated title reconciliation cannot duplicate or expire the career award");
            System.Console.WriteLine("Career restart checks passed: "+checks);return 0;
        }catch(Exception ex){System.Console.Error.WriteLine("Career restart failed: "+ex.GetType().Name+"\n"+ex.StackTrace);return 1;}
    }

    static GameState EarnCareerPromotions(string runtime,Map town,GameState p){
        if(!Kernel.Maps.ContainsKey(1004))World(runtime,1004);
        var job=Kernel.Maps[1004];
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT lookface,cellx,celly FROM public.npcs WHERE id=10022 AND mapid=1004",c))
        using(var r=q.ExecuteReader()){
            if(!r.Read())throw new Exception("Career trainer missing.");
            if(!job.Npcs.ContainsKey(10022))job.AddNpc(new NpcSpawn{UID=10022,MapID=1004,Mesh=Convert.ToUInt16(r[0]),X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),Type=Enums.NpcType.Talker});
        }}
        Action promote=()=>{byte before=p.Entity.Class;p.Entity.Teleport(1004,job.Npcs[10022].X,job.Npcs[10022].Y);p.ActiveNpc=10022;PromotionPrepare(p);PromotionNpc(p,3);
            Check(!Fenced(p) && p.Entity.Class==before+1,"career earns trainer rank "+(before+1)+" in life "+p.Entity.Reborn);};
        while(p.Entity.Class<12)promote();
        Call(HuntLoot,null,"Initialize");var oldHunt=HuntLoot.GetField("Roll",Hidden).GetValue(null);
        HuntLoot.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>0));
        try{
            p.Entity.Teleport(1000,611,636);var before=p.Map.FloorItems.Keys.ToArray();var mob=HuntMonster(p,"HillMonster");
            mob.Hitpoints=mob.MaxHitpoints=100;mob.IsDropped=false;uint damage=100;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,mob,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=mob.UID},ref damage,null);
            var drop=p.Map.FloorItems.Values.Single(f=>!before.Contains(f.UID) && f.ItemID==1080001);Loot(p,drop);
            Check(!Fenced(p) && p.Inventory.ContainsUID(drop.Item.UID),"career hunts and picks up its own Emerald");
            promote();Check(!p.Inventory.ContainsUID(drop.Item.UID),"career promotion consumes the earned Emerald");
        }finally{HuntLoot.GetField("Roll",Hidden).SetValue(null,oldHunt);}
        Query("CREATE TABLE IF NOT EXISTS npcs (LIKE public.npcs INCLUDING ALL)");
        Query("INSERT INTO npcs SELECT n.* FROM public.npcs n WHERE (id IN (600050,600003) OR (id BETWEEN 600005 AND 600017 AND id%2=1) OR id BETWEEN 600018 AND 600042) AND NOT EXISTS(SELECT 1 FROM npcs x WHERE x.id=n.id)");
        for(ushort id=1042;id<=1050;id++)if(!Kernel.Maps.ContainsKey(id))World(runtime,id);
        Call(Moon,null,"Initialize");Call(Moon,null,"PlaceNpcs");var oldMoon=Moon.GetField("Roll",Hidden).GetValue(null);
        Moon.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max==7?moonRoute:0));
        try{
            foreach(byte choice in new byte[]{2,1}){
                p.Entity.Teleport(1002,317,322);
                foreach(var reward in p.Inventory.Objects.Where(i=>i.ID>=800000 && i.ID<900000 || i.ID==Config.PathHuntExpPotionId || i.ID==Config.SoulTrialExpPotionId).ToArray()){
                    Warehouse.Process(StorageFrame(1,reward.UID),p);
                    Check(!Fenced(p) && !p.Inventory.ContainsUID(reward.UID) && TownStorage(p).Objects.Any(i=>i.UID==reward.UID),"career safely stores earned reward before the six-token hunt");
                }
                p.Entity.Teleport(1002,489,622);MoonNpc(p,600050,1);
                for(int stage=0;stage<6;stage++){
                    moonRoute=stage;MoonNpc(p,600003,1);var mob=MoonMob(p);mob.Hitpoints=mob.MaxHitpoints=100;uint damage=100;
                    Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,mob,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=mob.UID},ref damage,null);
                    MoonNpc(p,(uint)(600005+stage*2),1);
                }
                MoonNpc(p,600003,1);MoonNpc(p,600017,1);MoonNpc(p,600018,choice);
                Check(!Fenced(p) && p.Entity.MapID==1002 && p.Inventory.Contains(choice==1?721090u:1088001u,1),"career earns its Moon quest promotion offering "+choice);
                promote();
            }
        }finally{Moon.GetField("Roll",Hidden).SetValue(null,oldMoon);}
        p=ReloadPath(p);Check(p.Entity.Class==15 && PromotionCount(p)==5,"career master rank and five lifetime trainer receipts survive reload");return p;
    }

    static void EarnCareerGems(string runtime,Map town,GameState p){
        var existingItems=p.Inventory.Objects.Select(i=>i.UID).ToArray();
        Call(MiningService,null,"Initialize");Call(MinePassage,null,"Initialize");Call(BookShop,null,"Initialize");ShopFile.Load();
        foreach(ushort id in new ushort[]{1028,1025,1026})World(runtime,id);
        MiningNpc(town,1);MiningNpc(town,10080);MiningNpc(Kernel.Maps[1011],125);MiningNpc(Kernel.Maps[1020],126);
        Check(p.Entity.Money>=1100,"career character kept enough earned quest silver for a Pickaxe and Plain Mine fare");
        p.Entity.Teleport(1002,(ushort)(town.Npcs[1].X-2),(ushort)(town.Npcs[1].Y-2));
        ulong before=p.Entity.Money;
        ItemHandler.HandleBuyFromNPC(BuyBook(562000,1,1),p);
        var pick=p.Inventory.Objects.Single(i=>i.ID==562000);
        ItemHandler.EquipItem(new ItemUsage(true){UID=pick.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
        Check(!Fenced(p) && p.Entity.Money==before-1000 && p.Equipment.TryGetItem(4)==pick && pick.Durability==1099,
            "career character buys and equips a Pickaxe using earned quest silver");
        var oldClock=MiningService.GetField("Clock",Hidden).GetValue(null);
        var oldRoll=MiningService.GetField("Roll",Hidden).GetValue(null);
        int gemChance=Config.MiningNormalGemBasisPoints;
        Config.MiningNormalGemBasisPoints=100;
        MiningService.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>miningNow));
        MiningService.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max==10000?0:max==100?99:max==3||max==4?miningGem:0));
        try{
            p.Entity.Teleport(1002,(ushort)(town.Npcs[10080].X-2),(ushort)(town.Npcs[10080].Y-2));p.ActiveNpc=10080;
            ulong fare=p.Entity.Money;MineTravel(p);
            Check(!Fenced(p) && p.Entity.MapID==1028 && p.Entity.Money==fare-100,"career Conductress mine option spends the earned 100-silver fare");
            StartMining(p);
            for(int i=0;i<3;i++){miningGem=i;miningNow=miningNow.AddSeconds(2);MineTick(p);}
            p.Entity.Teleport(1011,(ushort)(Kernel.Maps[1011].Npcs[125].X-2),(ushort)(Kernel.Maps[1011].Npcs[125].Y-2));p.ActiveNpc=125;
            Npcs.GetDialog(new NpcRequest(true){NpcID=125,OptionID=1},p);
            Check(!Fenced(p) && p.Entity.MapID==1025,"career Forest Mine assistant admits the same character");
            StartMining(p);
            for(int i=2;i<4;i++){miningGem=i;miningNow=miningNow.AddSeconds(2);MineTick(p);}
            p.Entity.Teleport(1020,(ushort)(Kernel.Maps[1020].Npcs[126].X-2),(ushort)(Kernel.Maps[1020].Npcs[126].Y-2));p.ActiveNpc=126;
            Npcs.GetDialog(new NpcRequest(true){NpcID=126,OptionID=1},p);
            Check(!Fenced(p) && p.Entity.MapID==1026,"career Canyon Mine assistant admits the same character");
            StartMining(p);
            MiningService.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max==2?miningGem:0));
            for(int i=0;i<2;i++){miningGem=i;miningNow=miningNow.AddSeconds(2);MineTick(p);}
            Check(!Fenced(p) && MineCount(p)==7
                && p.Inventory.Objects.Where(i=>!existingItems.Contains(i.UID)).Select(i=>i.ID).Where(id=>id>=700001 && id<=700061).OrderBy(id=>id)
                    .SequenceEqual(Enumerable.Range(0,7).Select(i=>700001u+(uint)i*10)),
                "career character earns every normal Heavenly Stone gem from three mine pools");
        }finally{
            Config.MiningNormalGemBasisPoints=gemChance;
            MiningService.GetField("Clock",Hidden).SetValue(null,oldClock);
            MiningService.GetField("Roll",Hidden).SetValue(null,oldRoll);
        }
    }
}
