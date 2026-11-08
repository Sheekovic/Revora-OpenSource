using System;
using System.Linq;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type AbyssStore = typeof(Entity).Assembly.GetType("Revora.Game.Features.AbyssTrialStore");
    static readonly Type AbyssMaterials = typeof(Entity).Assembly.GetType("Revora.Game.Features.AbyssTrialMaterials");
    static object AbyssField(object o, string field) { return o.GetType().GetField(field, Instance).GetValue(o); }
    static object AbyssRead(GameState p) {
        using (var c = DataHolder.MySqlConnection) { c.Open(); return Call(AbyssStore, null, "Read", c, null, p.Entity.UID, false); }
    }
    static int AbyssVersion(GameState p) { return (int)AbyssField(AbyssRead(p), "Version"); }
    static object AbyssWrite(GameState p, string method, params object[] args) {
        lock (p.ItemSyncRoot) using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
            var result = Call(AbyssStore, null, method, new object[] { c,t,p }.Concat(args).ToArray()); t.Commit(); return result;
        }}
    }
    static object AbyssSelect(GameState p, string recipe, uint product) {
        lock(p.ItemSyncRoot) return Call(AbyssMaterials, null, "Select", p, AbyssCost(recipe, product));
    }
    static object AbyssVigor(GameState p) {
        Type material = AbyssRules.GetNestedType("Material", System.Reflection.BindingFlags.NonPublic);
        Array costs = Array.CreateInstance(material, 1);
        costs.SetValue(Activator.CreateInstance(material, Instance, null, new object[] {722731u, (ushort)3}, null), 0);
        lock (p.ItemSyncRoot) return Call(AbyssMaterials, null, "Select", p, costs);
    }
    static void AbyssApply(GameState p, object plan, object item) { lock(p.ItemSyncRoot) Call(AbyssMaterials, null, "Apply", p, plan, item); }
    static void AbyssAt(GameState p, Map map, uint npc) {
        p.Entity.MapID = map.ID; p.Entity.X = (ushort)(map.Npcs[npc].X + 1); p.Entity.Y = map.Npcs[npc].Y; p.ActiveNpc = npc;
        if (!EntityTable.SaveEntity(p)) throw new Exception("Abyss fixture location save failed.");
    }
    static bool AbyssRejected(Action action) {
        try { action(); return false; } catch(InvalidOperationException) { return true; } catch(PostgresException) { return true; }
    }
    static Guid AbyssId(object value) { return (Guid)AbyssField(value, "Id"); }
    static int AbyssAmount(GameState p, uint type) { return p.Inventory.Objects.Where(i => i.ID == type).Sum(i => Math.Max(1, (int)i.StackSize)); }
    static long AbyssPending(GameState p) { return Scalar("SELECT count(*) FROM revora_abyss_rewards w JOIN revora_abyss_trials r ON r.id=w.run WHERE r.uid=@uid AND w.claimed_at IS NULL", p.Entity.UID); }
    static object AbyssSummon(GameState p, Map map, uint family) {
        uint npc = family == 3632 ? 3605u : family == 3633 ? 3606u : family == 3634 ? 3607u : family == 3635 ? 3609u
            : family >= 3644 ? 3608u : new uint[] {3616,3618,3620,3622,3628,3630,3633,3635}[family - 3636];
        AbyssAt(p, map, npc);
        object plan = family >= 3632 && family <= 3635 || family == 3644 ? AbyssSelect(p, "SummonCost", family) : null;
        var encounter = AbyssWrite(p, "Summon", AbyssVersion(p), family, plan);
        if ((bool)AbyssField(encounter, "Created")) AbyssApply(p, plan, null);
        return encounter;
    }
    static void AbyssClaim(GameState p) { var item = AbyssWrite(p, "Claim"); AbyssApply(p, null, item); }
    static void AbyssCraftStored(GameState p, Map map, uint output) {
        uint family = output == 722722 ? 3632u : output == 722726 ? 3633u : output == 722729 ? 3634u : output == 722731 ? 3635u : 0;
        foreach (object cost in AbyssCost(family == 0 ? "ExchangeCost" : "SummonCost", family == 0 ? output : family))
            while (AbyssAmount(p, AbyssType(cost)) < AbyssCount(cost)) AbyssCraftStored(p, map, AbyssType(cost));
        if (family == 0) {
            AbyssAt(p, map, 3601); var plan = AbyssSelect(p, "ExchangeCost", output);
            var item = AbyssWrite(p, "Exchange", AbyssVersion(p), output, plan); AbyssApply(p, plan, item);
        } else {
            var a = AbyssSummon(p, map, family); Check((bool)AbyssWrite(p, "Defeat", AbyssId(a), family), "saved material encounter defeats once: " + family);
            Check(!(bool)AbyssWrite(p, "Defeat", AbyssId(a), family), "repeated material defeat creates no second spoils: " + family); AbyssClaim(p);
        }
    }
    static void VerifyAbyssStore(string runtime, Map town) {
        VerifyFirstRebirth(runtime, town); VerifyAbyssRules(); Call(AbyssStore, null, "Initialize"); Call(AbyssStore, null, "Initialize");
        var map = World(runtime, 1700);
        uint[][] sites = {
            new uint[]{3601,607,637},new uint[]{3602,609,633},new uint[]{3603,611,638},new uint[]{3605,444,878},new uint[]{3606,883,447},
            new uint[]{3607,920,929},new uint[]{3608,331,340},new uint[]{3609,715,744},new uint[]{3616,338,580},new uint[]{3618,339,592},
            new uint[]{3620,447,757},new uint[]{3622,660,441},new uint[]{3628,651,341},new uint[]{3630,680,919},new uint[]{3633,635,340},new uint[]{3635,956,789}};
        foreach (var site in sites) map.AddNpc(new NpcSpawn { UID=site[0], MapID=1700, X=(ushort)site[1], Y=(ushort)site[2], Mesh=2177, Type=Enums.NpcType.Talker });
        var p = SecondPlayer(town); var admitted = AbyssWrite(p, "Enroll"); var id = AbyssId(admitted);
        Check(AbyssId(AbyssWrite(p, "Enroll")) == id && AbyssVersion(p)==0, "ordinary first-reborn admission is idempotent and preserves its run identity");
        p.Quests=new Quests(p); p.Quests.Load();
        p.Entity.TareaCopa=19; p.Entity.SecoundQuestPoint=7654; p.Entity.SecoundQuestDone=true;
        p.Quests.Accept(QuestID.SecondQuestStageOne); p.Quests.IncreaseQuestKills(QuestID.SecondQuestStageOne,1);
        if (!EntityTable.SaveEntity(p)) throw new Exception("Cup fixture save failed.");
        uint cupKills=p.Quests.GetQuest(QuestID.SecondQuestStageOne).Kills; p.Quests.Save();
        AbyssWrite(p,"SelectCup"); Check(!(bool)AbyssField(AbyssRead(p),"Selected") && AbyssRejected(()=>AbyssWrite(p,"Require")), "saved Cup route disables Abyss mutations without deleting either record");
        AbyssWrite(p,"Enroll");
        // Fixture inputs establish 117 each; actual wild-death earning is checked below.
        foreach(uint type in new uint[]{722723,722724,722725}) NestedPack(p,type,117,type==722723);
        for(int n=0;n<3;n++) AbyssCraftStored(p,map,722731);
        Check(AbyssAmount(p,722731)==3 && new uint[]{722723,722724,722725}.All(type=>AbyssAmount(p,type)==0)
            && Scalar("SELECT count(*) FROM revora_abyss_encounters e JOIN revora_abyss_trials r ON r.id=e.run WHERE r.uid=@uid AND e.defeated_at IS NOT NULL",p.Entity.UID)==120,
            "durable three-vigor dependency graph spends exactly 117 of each base across 120 paid encounters");
        Check(AbyssPending(p)==0 && p.Entity.TareaCopa==19 && p.Entity.SecoundQuestPoint==7654 && p.Entity.SecoundQuestDone
            && p.Quests.GetQuest(QuestID.SecondQuestStageOne).Kills==cupKills, "Abyss crafting and route changes preserve every legacy Cup field and quest entry");
        AbyssAt(p,map,3601); var vigor=AbyssVigor(p);
        Check((bool)AbyssWrite(p,"TurnIn",AbyssVersion(p),3601u,vigor),"three owned Pure Vigors commit stage two"); AbyssApply(p,vigor,null);
        Check(!(bool)AbyssWrite(p,"WildDeath",Guid.NewGuid(),3603u,99),"grievance cannot begin before Bryan explains the trial");
        AbyssAt(p,map,3602); Check((bool)AbyssWrite(p,"Introduce",AbyssVersion(p)),"Bryan acknowledges the next trial durably");
        for(int n=0;n<80;n++) { var life=Guid.NewGuid(); Check((bool)AbyssWrite(p,"WildDeath",life,3603u,99) && !(bool)AbyssWrite(p,"WildDeath",life,3603u,0), "grievance saves a lord exactly once: "+n); }
        AbyssAt(p,map,3602); Check((bool)AbyssWrite(p,"TurnIn",AbyssVersion(p),3602u,null),"40,000 saved meters admit the ordered spirit stage");
        AbyssAt(p,map,3603); Check((bool)AbyssWrite(p,"Introduce",AbyssVersion(p)),"Angela explains the binding curse before its first encounter");
        for(int n=0;n<72;n++) { uint family=3636u+(uint)(n%8); var a=AbyssSummon(p,map,family);
            Check(!(bool)AbyssWrite(p,"Defeat",Guid.NewGuid(),family) && !(bool)AbyssWrite(p,"Defeat",AbyssId(a),family+1)
                && (bool)AbyssWrite(p,"Defeat",AbyssId(a),family),"only the saved owned spirit advances its ordered round: "+n); }
        AbyssAt(p,map,3603); Check((bool)AbyssWrite(p,"TurnIn",AbyssVersion(p),3603u,null),"72 ordered saved spirits admit Satan's trial");
        Check((bool)AbyssWrite(p,"WildDeath",Guid.NewGuid(),3603u,24) && AbyssPending(p)==1,"reviewed bead source records recoverable final-trial loot"); AbyssClaim(p);
        for(uint family=3644;family<=3646;family++) { var a=AbyssSummon(p,map,family); Check((bool)AbyssWrite(p,"Defeat",AbyssId(a),family),"saved Satan chain advances its exact phase: "+family); }
        Check(AbyssValue(AbyssField(AbyssRead(p),"Progress"),"Step")==5 && (bool)AbyssField(AbyssRead(p),"Bound") && !HasAchievement(p,11031) && !HasAchievement(p,10316),
            "persistence fixture reaches its terminal state without pretending to prove world combat or grant achievements");
        var loaded=ReloadBall(p); SkillTable.LoadProficiencies(loaded);
        Check(AbyssId(AbyssRead(loaded))==id && AbyssValue(AbyssField(AbyssRead(loaded),"Progress"),"Spirits")==72
            && loaded.Entity.TareaCopa==19
            && loaded.Quests.GetQuest(QuestID.SecondQuestStageOne).Kills==cupKills,
            "separate Abyss state and legacy Cup fields survive production character reload");
        VerifyAbyssPayments(town,map); VerifyAbyssFaults(town,map); VerifyAbyssEarning(town,map); VerifyAbyssRecovery(town,map);
    }
    static void VerifyAbyssPayments(Map town,Map map) {
        var p=SecondPlayer(town); AbyssWrite(p,"Enroll"); AbyssAt(p,map,3601);
        var a=Pack(p,722722,0); var b=NestedPack(p,722722,1,true); var c=Pack(p,722722,2);
        var plan=AbyssSelect(p,"ExchangeCost",722721); int before=AbyssVersion(p);
        var item=(ConquerItem)AbyssWrite(p,"Exchange",before,722721u,plan); AbyssApply(p,plan,item);
        Check(item.Bound && !p.Inventory.Objects.Any(i=>i.UID==a.UID || i.UID==b.UID) && c.StackSize==1 && AbyssAmount(p,722721)==1,
            "mixed zero-singleton, bound and partial-stack recipe preserves binding and exact leftovers");
        Check(AbyssRejected(()=>AbyssWrite(p,"Exchange",before,722721u,plan)) && AbyssAmount(p,722721)==1,"stale exchange confirmation cannot charge or grant again");
        foreach(uint type in new uint[]{722723,722724,722725}) Pack(p,type,2);
        var boss=AbyssSummon(p,map,3632); int version=AbyssVersion(p);
        var duplicate=AbyssWrite(p,"Summon",before,3632u,null);
        Check(AbyssId(boss)==AbyssId(duplicate) && !(bool)AbyssField(duplicate,"Created") && AbyssVersion(p)==version
            && new uint[]{722723,722724,722725}.All(type=>AbyssAmount(p,type)==1),"durable active entitlement resumes without another debit or version change");
        AbyssAt(p,town,59558); Check(AbyssRejected(()=>AbyssWrite(p,"SelectCup")),"outstanding paid encounter cannot be stranded by route switching"); AbyssAt(p,map,3605);
        while(p.Inventory.Count<40)Pack(p,1000000,1);
        Check((bool)AbyssWrite(p,"Defeat",AbyssId(boss),3632u) && AbyssWrite(p,"Claim")==null && AbyssPending(p)==1,
            "full bag preserves defeated encounter reward for a later claim");
        var remove=p.Inventory.Objects.First(i=>i.ID==1000000); Query("DELETE FROM items WHERE uid=@uid","uid",(long)remove.UID); p.Inventory.Remove(remove.UID,Enums.ItemUse.None,true);
        AbyssClaim(p); Check(AbyssPending(p)==0 && AbyssAmount(p,722722)==2,"freeing one slot delivers the durable spoils exactly once");
        Check(AbyssWrite(p,"Claim")==null,"already claimed spoils cannot be replayed");
    }
    static void VerifyAbyssFaults(Map town,Map map) {
        foreach(string target in new[]{"revora_abyss_operations","revora_abyss_materials","items","revora_abyss_trials"}) {
            var p=SecondPlayer(town); AbyssWrite(p,"Enroll"); AbyssAt(p,map,3601); var horns=Pack(p,722722,3); var plan=AbyssSelect(p,"ExchangeCost",722721);
            Query("CREATE FUNCTION abyss_reject() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$");
            Query("CREATE TRIGGER abyss_reject BEFORE "+(target=="revora_abyss_trials"?"UPDATE":"INSERT")+" ON "+target+" FOR EACH ROW EXECUTE FUNCTION abyss_reject()");
            try { Check(AbyssRejected(()=>AbyssWrite(p,"Exchange",0,722721u,plan)) && AbyssVersion(p)==0
                    && Scalar("SELECT stacksize FROM items WHERE uid=@uid",horns.UID)==3 && AbyssAmount(p,722721)==0,
                    "suppressed required write rolls back the complete exchange: "+target); }
            finally { Query("DROP TRIGGER abyss_reject ON "+target); Query("DROP FUNCTION abyss_reject()"); }
        }
        foreach(string changed in new[]{"locked=1","bound=1","stacksize=4","entityid=entityid+99999","days='1'","suspicious=1"}) {
            var p=SecondPlayer(town); AbyssWrite(p,"Enroll"); AbyssAt(p,map,3601); var horns=Pack(p,722722,3); var plan=AbyssSelect(p,"ExchangeCost",722721);
            Query("UPDATE items SET "+changed+" WHERE uid=@uid","uid",(long)horns.UID);
            Check(AbyssRejected(()=>AbyssWrite(p,"Exchange",0,722721u,plan)) && AbyssVersion(p)==0
                && Scalar("SELECT count(*) FROM revora_abyss_operations o JOIN revora_abyss_trials r ON r.id=o.run WHERE r.uid=@uid",p.Entity.UID)==0,
                "stale owned material metadata rolls back the attempted recipe: "+changed);
        }
    }
    static void VerifyAbyssEarning(Map town,Map map) {
        var p=SecondPlayer(town); AbyssWrite(p,"Enroll"); AbyssAt(p,map,3601);
        for(int roll=0;roll<100;roll++) AbyssWrite(p,"WildDeath",Guid.NewGuid(),3600u,roll);
        Check(AbyssPending(p)==50,"Revora material rate records exactly fifty pending drops per complete hundred-roll distribution");
        for(int n=0;n<40;n++) { if(p.Inventory.Count==40)break; AbyssClaim(p); }
        Check(AbyssPending(p)>0,"earning remains durable when the claim bag fills");
        var q=SecondPlayer(town); AbyssWrite(q,"Enroll"); AbyssAt(q,map,3601); var life=Guid.NewGuid(); AbyssWrite(p,"WildDeath",life,3600u,99);
        Check(!(bool)AbyssWrite(q,"WildDeath",life,3600u,0),"one server monster life cannot be credited to two different owners");
        q.Trade.InTrade=true; Check(AbyssRejected(()=>AbyssWrite(q,"Claim")),"trade session cannot mutate Abyss materials"); q.Trade.InTrade=false;
        q.Entity.FirstRebornLevel=129; Check(AbyssRejected(()=>AbyssWrite(q,"Require")),"changed first-life cache cannot reuse a saved run");
    }
    static void VerifyAbyssRecovery(Map town,Map map) {
        var p=SecondPlayer(town); AbyssWrite(p,"Enroll");
        foreach(uint type in new uint[]{722723,722724,722725}) NestedPack(p,type,0,type==722723);
        var a=AbyssSummon(p,map,3632); Guid id=AbyssId(a);
        p=ReloadBall(p); SkillTable.LoadProficiencies(p); AbyssAt(p,map,3605);
        var resumed=AbyssWrite(p,"Summon",-1,3632u,null);
        Check(AbyssId(resumed)==id && !(bool)AbyssField(resumed,"Created") && (bool)AbyssField(resumed,"Bound")
            && new uint[]{722723,722724,722725}.All(type=>AbyssAmount(p,type)==0),"production reconnect recovers the paid bound encounter with no offering remaining");
        foreach(string target in new[]{"revora_abyss_encounters","revora_abyss_rewards","revora_abyss_trials"}) {
            Query("CREATE FUNCTION abyss_reject() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$");
            Query("CREATE TRIGGER abyss_reject BEFORE "+(target=="revora_abyss_rewards"?"INSERT":"UPDATE")+" ON "+target+" FOR EACH ROW EXECUTE FUNCTION abyss_reject()");
            try { Check(AbyssRejected(()=>AbyssWrite(p,"Defeat",id,3632u)) && AbyssPending(p)==0
                    && Scalar("SELECT count(*) FROM revora_abyss_encounters e JOIN revora_abyss_trials r ON r.id=e.run WHERE r.uid=@uid AND e.defeated_at IS NULL",p.Entity.UID)==1,
                    "failed defeat write preserves the same paid encounter and no partial reward: "+target); }
            finally { Query("DROP TRIGGER abyss_reject ON "+target); Query("DROP FUNCTION abyss_reject()"); }
        }
        AbyssWrite(p,"Defeat",id,3632u); p=ReloadBall(p); SkillTable.LoadProficiencies(p);
        Check(AbyssPending(p)==1,"unclaimed bound encounter spoils survive production reconnect");
        foreach(string target in new[]{"items","revora_abyss_rewards"}) {
            Query("CREATE FUNCTION abyss_reject() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$");
            Query("CREATE TRIGGER abyss_reject BEFORE "+(target=="items"?"INSERT":"UPDATE")+" ON "+target+" FOR EACH ROW EXECUTE FUNCTION abyss_reject()");
            try { Check(AbyssRejected(()=>AbyssWrite(p,"Claim")) && AbyssPending(p)==1 && AbyssAmount(p,722722)==0,
                    "failed spoils claim leaves its saved reward recoverable: "+target); }
            finally { Query("DROP TRIGGER abyss_reject ON "+target); Query("DROP FUNCTION abyss_reject()"); }
        }
        AbyssClaim(p); Check(p.Inventory.Objects.Single(i=>i.ID==722722).Bound && AbyssPending(p)==0,"retried spoils claim preserves binding once");
        var q=SecondPlayer(town); AbyssWrite(q,"Enroll"); AbyssAt(q,map,3601); Pack(q,722722,6);
        var plan=AbyssSelect(q,"ExchangeCost",722721); int wins=0;
        System.Threading.Tasks.Parallel.For(0,12,n=>{
            lock(q.ItemSyncRoot) try { var item=AbyssWrite(q,"Exchange",0,722721u,plan); AbyssApply(q,plan,item); wins++; } catch(InvalidOperationException) { }
        });
        Check(wins==1 && AbyssAmount(q,722722)==3 && AbyssAmount(q,722721)==1 && AbyssVersion(q)==1,"concurrent confirmations produce one owned debit and output");
        Pack(q,722722,1); while(q.Inventory.Count<40)Pack(q,1000000,1);
        plan=AbyssSelect(q,"ExchangeCost",722721);
        // Consuming the remaining three-horn stack frees its output slot.
        var exchange=AbyssWrite(q,"Exchange",1,722721u,plan); AbyssApply(q,plan,exchange);
        Check(q.Inventory.Count==40 && AbyssAmount(q,722721)==2,"a full bag can exchange when the exact debit frees its output slot");
        var r=SecondPlayer(town); AbyssWrite(r,"Enroll"); AbyssAt(r,map,3601); Pack(r,722722,4); while(r.Inventory.Count<40)Pack(r,1000000,1);
        plan=AbyssSelect(r,"ExchangeCost",722721);
        Check(AbyssWrite(r,"Exchange",0,722721u,plan)==null && AbyssVersion(r)==0 && AbyssAmount(r,722722)==4,"full bag with a partial-stack debit cannot charge for an undeliverable exchange");
        var lost=SecondPlayer(town); AbyssWrite(lost,"Enroll"); AbyssAt(lost,map,3605);
        foreach(uint type in new uint[]{722723,722724,722725})Pack(lost,type,1);
        plan=AbyssSelect(lost,"SummonCost",3632); var paid=AbyssWrite(lost,"Summon",0,3632u,plan);
        var previous=lost; lost=ReloadBall(lost); SkillTable.LoadProficiencies(lost); AbyssAt(lost,map,3605);
        Check(AbyssRejected(()=>AbyssWrite(previous,"Require")) && AbyssId(AbyssWrite(lost,"Summon",0,3632u,null))==AbyssId(paid)
            && new uint[]{722723,722724,722725}.All(type=>AbyssAmount(lost,type)==0),"lost post-commit projection recovers on reconnect and rejects the superseded session");
        foreach(string mode in new[]{"remote","low-level","wrong-life","fake","dead","unloaded","npc-map"}) {
            var denied=SecondPlayer(town); uint npcMap=town.Npcs[59558].MapID;
            if(mode=="remote")denied.Entity.X=10;
            if(mode=="low-level")denied.Entity.Level=119;
            if(mode=="wrong-life")denied.Entity.Reborn=2;
            if(mode=="fake")denied.Fake=true;
            if(mode=="dead")denied.Entity.Hitpoints=0;
            if(mode=="unloaded")denied.Entity.FullyLoaded=false;
            if(mode=="npc-map")town.Npcs[59558].MapID=1020;
            try { Check(AbyssRejected(()=>AbyssWrite(denied,"Enroll")) && AbyssRead(denied)==null,"invalid ordinary admission creates no run: "+mode); }
            finally { if(mode=="npc-map")town.Npcs[59558].MapID=(ushort)npcMap; }
        }
    }
}
