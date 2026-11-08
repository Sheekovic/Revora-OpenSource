using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type EliminationStore=typeof(Entity).Assembly.GetType("Revora.Game.Features.EliminationStore");
    static readonly Type EliminationRules=typeof(Entity).Assembly.GetType("Revora.Game.Features.EliminationRules");
    static void EliminationAt(GameState p,uint npc) {
        var actor=Kernel.Maps[1011].Npcs[npc];p.BindWorldMap(1011);p.Entity.X=actor.X;p.Entity.Y=(ushort)(actor.Y+2);p.ActiveNpc=npc;
        if(!EntityTable.SaveEntity(p))throw new Exception("Elimination fixture save failed.");
    }
    static object EliminationWrite(GameState p,string method,params object[] args) {
        lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            var result=Call(EliminationStore,null,method,new object[]{c,t,p}.Concat(args).ToArray());t.Commit();
            if(result!=null && !(result is bool))Call(EliminationStore,null,"Apply",p,result);
            return result;
        }}
    }
    static void VerifyEliminationStore(string runtime,Map town) {
        InitializeHousingFixture(runtime,town);var forest=World(runtime,1011);
        var now=new DateTime(2026,9,20,18,0,0,DateTimeKind.Utc);
        EliminationStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        Call(EliminationStore,null,"Initialize");Call(EliminationStore,null,"Initialize");
        forest.AddNpc(new NpcSpawn{UID=300020,MapID=1011,X=423,Y=55,Mesh=4680,Type=Enums.NpcType.Talker});
        for(uint npc=300021;npc<=300032;npc++) {
            var site=Call(EliminationRules,null,"Herb",npc);
            forest.AddNpc(new NpcSpawn{UID=npc,MapID=1011,X=(ushort)AbyssField(site,"X"),Y=(ushort)AbyssField(site,"Y"),Mesh=4700,Type=Enums.NpcType.Talker});
        }
        var p=HousePlayer(town);uint wardrobe=StoredQuestGarment(p);p.Entity.Reborn=0;EliminationAt(p,300020);
        Check(p.Account.State==AccountTable.AccountState.Player,"ordinary Elimination player");
        Check(!(bool)EliminationWrite(p,"Begin"),"unreborn admission refused");
        p.Entity.Reborn=1;p.Entity.Level=69;EliminationAt(p,300020);
        Check(!(bool)EliminationWrite(p,"Begin"),"level 69 admission refused");
        p.Entity.Level=70;EliminationAt(p,300020);p.Entity.X=450;
        Check(!(bool)EliminationWrite(p,"Begin"),"distant admission refused");EliminationAt(p,300020);
        p.Trade.InTrade=true;Check(AbyssRejected(()=>EliminationWrite(p,"Begin")),"trading admission refused");p.Trade.InTrade=false;
        Parallel.For(0,8,i=>EliminationWrite(p,"Begin"));
        Check(Scalar("SELECT count(*) FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==1,"concurrent daily admission once");
        EliminationAt(p,300021);
        Query(@"CREATE FUNCTION refuse_elimination_herb() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER refuse_elimination_herb BEFORE INSERT ON revora_elimination_herbs FOR EACH ROW EXECUTE FUNCTION refuse_elimination_herb();");
        Check(AbyssRejected(()=>EliminationWrite(p,"Gather",300021u)) && p.Inventory.Count==0
            && Scalar("SELECT herbs FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==0
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0",p.Entity.UID)==0,"late herb receipt failure rolls back item and progress");
        Query("DROP TRIGGER refuse_elimination_herb ON revora_elimination_herbs;DROP FUNCTION refuse_elimination_herb()");
        Parallel.For(0,8,i=>EliminationWrite(p,"Gather",300021u));
        Check(p.Inventory.Count==1 && Scalar("SELECT herbs FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==1,"concurrent plant requests give one herb");
        p=ReloadBall(p);EliminationAt(p,300021);
        Check(EliminationWrite(p,"Gather",300021u)==null && p.Inventory.Count==1,"reloaded plant cannot be gathered twice");
        for(uint npc=300022;npc<=300032;npc++){EliminationAt(p,npc);Check(EliminationWrite(p,"Gather",npc)!=null,"distinct earned herb");}
        EliminationAt(p,300020);uint lost=p.Inventory.Objects.First().UID;
        Check(EliminationWrite(p,"Recover",lost)==null,"existing herb cannot be recovered");
        Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)lost);
        Check(EliminationWrite(p,"Recover",lost)==null,"stored herb cannot be recovered despite stale bag");
        Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)lost);
        ItemHandler.DropItem(new ItemUsage(true){UID=lost},p);
        Check(!p.Inventory.ContainsUID(lost) && Scalar("SELECT count(*) FROM items WHERE uid=@uid",lost)==0
            && !forest.FloorItems.Values.Any(x=>x.Item!=null && x.Item.UID==lost),"normal bound herb discard destroys item without floor copy");
        Query(@"CREATE FUNCTION refuse_elimination_replacement() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER refuse_elimination_replacement BEFORE INSERT ON revora_elimination_replacements FOR EACH ROW EXECUTE FUNCTION refuse_elimination_replacement();");
        Check(AbyssRejected(()=>EliminationWrite(p,"Recover",lost)) && p.Inventory.Count==11
            && Scalar("SELECT count(*) FROM revora_elimination_herbs WHERE item_uid=@uid",lost)==1
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0",p.Entity.UID)==11,"late replacement receipt failure restores original proof and inventory");
        Query("DROP TRIGGER refuse_elimination_replacement ON revora_elimination_replacements;DROP FUNCTION refuse_elimination_replacement()");
        Parallel.For(0,8,i=>EliminationWrite(p,"Recover",lost));
        uint replacement=checked((uint)Scalar("SELECT new_uid FROM revora_elimination_replacements WHERE old_uid=@uid",lost));
        Check(p.Inventory.Count==12 && p.Inventory.ContainsUID(replacement)
            && Scalar("SELECT herbs FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==4095,"concurrent herb recovery preserves twelve-plant proof");
        p=ReloadBall(p);EliminationAt(p,300020);
        Check(p.Inventory.ContainsUID(replacement) && EliminationWrite(p,"Recover",lost)==null,"reloaded replacement rejects obsolete missing UID");
        EliminationAt(p,300020);var first=p.Inventory.Objects.First();first.Lock=1;
        Check(EliminationWrite(p,"Spice")==null && p.Inventory.Count==12,"locked earned herb preserves all materials");first.Lock=0;
        Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)first.UID);
        Check(AbyssRejected(()=>EliminationWrite(p,"Spice")) && p.Inventory.Count==12,"saved restriction overrides stale unlocked cache");
        Query("UPDATE items SET locked=0 WHERE uid=@uid","uid",(long)first.UID);
        var other=HousePlayer(town);other.Entity.Reborn=1;EliminationAt(other,300020);
        Query("UPDATE items SET entityid=@owner WHERE uid=@uid","owner",(long)other.Entity.UID,"uid",(long)first.UID);
        Check(AbyssRejected(()=>EliminationWrite(p,"Spice")),"saved foreign ownership refuses spice");
        Check(EliminationWrite(p,"Recover",first.UID)==null,"saved foreign ownership is not recoverable loss");
        Query("UPDATE items SET entityid=@owner WHERE uid=@uid","owner",(long)p.Entity.UID,"uid",(long)first.UID);
        Check((bool)EliminationWrite(other,"Begin"),"independent player daily admission");
        for(int i=0;i<40;i++)Pack(other,1088001,1);
        EliminationAt(other,300021);
        Check(AbyssRejected(()=>EliminationWrite(other,"Gather",300021u))
            && Scalar("SELECT herbs FROM revora_elimination_runs WHERE uid=@uid",other.Entity.UID)==0,"full bag preserves unharvested plant");
        Query(@"CREATE FUNCTION refuse_elimination_spice() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.stage=1 THEN RETURN NULL; END IF; RETURN NEW; END $$;
CREATE TRIGGER refuse_elimination_spice BEFORE UPDATE ON revora_elimination_runs FOR EACH ROW EXECUTE FUNCTION refuse_elimination_spice();");
        Check(AbyssRejected(()=>EliminationWrite(p,"Spice")) && p.Inventory.Count==12
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=711042",p.Entity.UID)==12
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=723019",p.Entity.UID)==0,"late spice progress failure restores twelve herbs");
        Query("DROP TRIGGER refuse_elimination_spice ON revora_elimination_runs;DROP FUNCTION refuse_elimination_spice()");
        Parallel.For(0,8,i=>EliminationWrite(p,"Spice"));
        Check(p.Inventory.Count==1 && p.Inventory.Objects.Single().ID==723019 && p.Inventory.Objects.Single().Bound
            && Scalar("SELECT stage FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==1,"twelve earned herbs become one bound spice once");
        uint spice=p.Inventory.Objects.Single().UID;p=ReloadBall(p);EliminationAt(p,300020);
        Check(p.Inventory.ContainsUID(spice) && EliminationWrite(p,"Spice")==null && !(bool)EliminationWrite(p,"Begin"),"reload preserves spice and daily replay protection");
        Check(EliminationWrite(p,"Recover",replacement)==null,"consumed herbs cannot return after spice exchange");
        ItemHandler.DropItem(new ItemUsage(true){UID=spice},p);
        Check(p.Inventory.Count==0,"normal bound spice discard removes inventory");
        int clockReads=0;
        EliminationStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>++clockReads<3?now:now.AddDays(1)));
        try {
            Check(AbyssRejected(()=>EliminationWrite(p,"Recover",spice)) && p.Inventory.Count==0
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0",p.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_elimination_replacements WHERE old_uid=@uid",spice)==0,
                "day rollover during recovery rolls back replacement and receipt");
        }finally{EliminationStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));}
        var fillers=Enumerable.Range(0,40).Select(i=>Pack(p,1088001,1).UID).ToArray();
        Check(AbyssRejected(()=>EliminationWrite(p,"Recover",spice))
            && Scalar("SELECT count(*) FROM revora_elimination_replacements WHERE old_uid=@uid",spice)==0,"full bag recovery leaves original spice proof intact");
        foreach(uint uid in fillers)RemoveFixtureItem(p,uid);
        Parallel.For(0,8,i=>EliminationWrite(p,"Recover",spice));
        Check(p.Inventory.Count==1 && p.Inventory.Objects.Single().ID==723019 && p.Inventory.Objects.Single().UID!=spice,"concurrent spice recovery creates one bound replacement");
        spice=p.Inventory.Objects.Single().UID;p=ReloadBall(p);EliminationAt(p,300020);
        Check(p.Inventory.ContainsUID(spice) && EliminationWrite(p,"Recover",spice)==null,"spice recovery survives reconnect without duplication");
        foreach(uint uid in other.Inventory.Objects.Select(x=>x.UID).ToArray())RemoveFixtureItem(other,uid);
        EliminationAt(other,300021);EliminationWrite(other,"Gather",300021u);uint oldHerb=other.Inventory.Objects.Single().UID;
        EliminationAt(p,300020);
        Check(EliminationWrite(p,"Cleanup")==null && p.Inventory.ContainsUID(spice),"same-day spice is not expired");
        now=now.AddDays(1);Check((bool)EliminationWrite(p,"Begin"),"next Pacific day admits a new run");
        Check(EliminationWrite(p,"Spice")==null && p.Inventory.ContainsUID(spice),"previous day spice cannot replace new herbs");
        var unrelated=Pack(p,711042,1);
        EliminationAt(p,300021);EliminationWrite(p,"Gather",300021u);
        uint current=p.Inventory.Objects.Single(x=>x.ID==711042 && x.UID!=unrelated.UID).UID;
        EliminationAt(p,300020);var savedSpice=p.Inventory.Objects.Single(x=>x.UID==spice);
        Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)spice);
        p.Inventory.Remove(spice,Enums.ItemUse.None,true);
        Check(EliminationWrite(p,"Cleanup")==null && Scalar("SELECT count(*) FROM items WHERE uid=@uid",spice)==1,"expired stored item deferred without deletion");
        Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)spice);p.Inventory.Add(savedSpice,Enums.ItemUse.None);
        Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)spice);savedSpice.Lock=1;
        Check(EliminationWrite(p,"Cleanup")==null && p.Inventory.ContainsUID(spice),"locked expired material is preserved");
        Query("UPDATE items SET locked=0 WHERE uid=@uid","uid",(long)spice);savedSpice.Lock=0;
        Query(@"CREATE FUNCTION refuse_elimination_cleanup() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER refuse_elimination_cleanup BEFORE INSERT ON revora_elimination_cleanup FOR EACH ROW EXECUTE FUNCTION refuse_elimination_cleanup();");
        Check(AbyssRejected(()=>EliminationWrite(p,"Cleanup")) && p.Inventory.ContainsUID(spice)
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid",spice)==1,"late cleanup receipt failure restores expired item");
        Query("DROP TRIGGER refuse_elimination_cleanup ON revora_elimination_cleanup;DROP FUNCTION refuse_elimination_cleanup()");
        Parallel.For(0,8,i=>EliminationWrite(p,"Cleanup"));
        Check(!p.Inventory.ContainsUID(spice) && p.Inventory.ContainsUID(current) && p.Inventory.ContainsUID(unrelated.UID)
            && Scalar("SELECT count(*) FROM revora_elimination_cleanup WHERE item_uid=@uid",spice)==1,"cleanup removes only exact expired owner item once");
        Check(other.Inventory.ContainsUID(oldHerb) && Scalar("SELECT count(*) FROM items WHERE uid=@uid",oldHerb)==1,"cleanup preserves another character's expired herb");
        p=ReloadBall(p);EliminationAt(p,300020);
        Check(!p.Inventory.ContainsUID(spice) && p.Inventory.ContainsUID(current) && EliminationWrite(p,"Cleanup")==null,"cleanup and retained current herb survive reload");
        EliminationAt(other,300020);Check(EliminationWrite(other,"Cleanup")!=null && other.Inventory.Count==0,"expired herb is removed through its own owner");
        Check(EliminationWrite(p,"Recover",spice)==null,"expired daily spice is not recoverable in a new run");
        Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"Elimination herbs, spice, cleanup and production reload preserve stored wardrobe design");
        VerifyEliminationDialogue(town);
        VerifyEliminationRescueAdmission(town);
    }
    static void VerifyEliminationRescueAdmission(Map town) {
        var p=HousePlayer(town);p.Entity.Reborn=1;p.Entity.Level=70;EliminationAt(p,300020);
        EliminationWrite(p,"Begin");
        for(uint npc=300021;npc<=300032;npc++){EliminationAt(p,npc);EliminationWrite(p,"Gather",npc);}
        EliminationAt(p,300020);EliminationWrite(p,"Spice");uint spice=p.Inventory.Objects.Single().UID;
        Check(EliminationWrite(p,"Summon",spice)==null && p.Inventory.ContainsUID(spice),"spice cannot summon away from clearing");
        p.Entity.X=311;p.Entity.Y=256;EntityTable.SaveEntity(p);
        var unrelated=Pack(p,723019,1);
        Check(EliminationWrite(p,"Summon",unrelated.UID)==null && p.Inventory.ContainsUID(unrelated.UID),"unearned spice cannot substitute for daily receipt");
        Check(EliminationWrite(p,"ResumeRescue")==null,"no rescue resume before paid admission");
        var item=p.Inventory.Objects.Single(x=>x.UID==spice);item.Lock=1;
        Check(EliminationWrite(p,"Summon",spice)==null && p.Inventory.ContainsUID(spice),"locked spice cannot summon");item.Lock=0;
        Query("UPDATE items SET bound=0 WHERE uid=@uid","uid",(long)spice);
        Check(AbyssRejected(()=>EliminationWrite(p,"Summon",spice)) && p.Inventory.ContainsUID(spice),"saved spice flags override stale cache");
        Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)spice);
        foreach(string fault in new[]{"receipt","stage"}) {
            string table=fault=="receipt"?"revora_elimination_rescues":"revora_elimination_runs",verb=fault=="receipt"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION refuse_rescue_admission() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER refuse_rescue_admission BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION refuse_rescue_admission()");
            try {
                Check(AbyssRejected(()=>EliminationWrite(p,"Summon",spice)) && p.Inventory.ContainsUID(spice)
                    && Scalar("SELECT count(*) FROM items WHERE uid=@uid",spice)==1
                    && Scalar("SELECT stage FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==1
                    && Scalar("SELECT count(*) FROM revora_elimination_rescues s JOIN revora_elimination_runs r ON r.id=s.run WHERE r.uid=@uid",p.Entity.UID)==0,
                    "failed rescue "+fault+" restores exact spice and run stage");
            }finally{Query("DROP TRIGGER refuse_rescue_admission ON "+table+";DROP FUNCTION refuse_rescue_admission()");}
        }
        var clock=(Func<DateTime>)EliminationStore.GetField("Clock",Hidden).GetValue(null);var stamp=clock();int reads=0;
        try {
            EliminationStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>++reads==1?stamp:stamp.AddDays(1)));
            Check(AbyssRejected(()=>EliminationWrite(p,"Summon",spice)) && p.Inventory.ContainsUID(spice)
                && Scalar("SELECT stage FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==1,"midnight admission rolls back spice consumption");
        }finally{EliminationStore.GetField("Clock",Hidden).SetValue(null,clock);}
        Parallel.For(0,8,i=>EliminationWrite(p,"Summon",spice));
        Check(!p.Inventory.ContainsUID(spice) && p.Inventory.ContainsUID(unrelated.UID)
            && Scalar("SELECT count(*) FROM revora_elimination_rescues s JOIN revora_elimination_runs r ON r.id=s.run WHERE r.uid=@uid",p.Entity.UID)==1,
            "concurrent summon consumes one exact spice and saves one admission");
        var resume=EliminationWrite(p,"ResumeRescue");Guid run=(Guid)AbyssField(resume,"RescueRun");DateTime until=(DateTime)AbyssField(resume,"RescueUntil");
        Check(run!=Guid.Empty && until>stamp && until<=stamp.AddDays(1),"rescue admission retains fixed daily deadline");
        p=ReloadBall(p);p.Entity.X=311;p.Entity.Y=256;EntityTable.SaveEntity(p);
        Parallel.For(0,8,i=>EliminationWrite(p,"ResumeRescue"));resume=EliminationWrite(p,"ResumeRescue");
        Check((Guid)AbyssField(resume,"RescueRun")==run && (DateTime)AbyssField(resume,"RescueUntil")==until
            && p.Inventory.Count==1 && p.Inventory.ContainsUID(unrelated.UID),"reload and repeated resume retain paid identity and deadline without another item debit");
        Check(EliminationWrite(p,"Summon",unrelated.UID)==null,"admitted run cannot pay again with another spice");
        VerifyEliminationBinding(p);
        p=VerifyEliminationTravel(p,town);
        EliminationAt(p,300020);Check(EliminationWrite(p,"Recover",spice)==null,"consumed summoning spice cannot be recovered");
        p.Entity.X=311;p.Entity.Y=256;EntityTable.SaveEntity(p);
        var foreign=HousePlayer(town);foreign.Entity.Reborn=1;foreign.BindWorldMap(1011);foreign.Entity.X=311;foreign.Entity.Y=256;EntityTable.SaveEntity(foreign);
        Check(EliminationWrite(foreign,"ResumeRescue")==null,"another owner cannot resume the paid rescue");
        try {
            EliminationStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>until));
            Check(EliminationWrite(p,"ResumeRescue")==null,"rescue admission expires at daily reset");
        }finally{EliminationStore.GetField("Clock",Hidden).SetValue(null,clock);}
    }
    static void VerifyEliminationBinding(GameState p) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.EliminationRescueBindings");
        MonsterInformation.MonsterInformations.Add(9916701,new MonsterInformation{ID=9916701,Name="Collision"});
        try{Check(AbyssRejected(()=>Call(service,null,"Initialize")),"rescue family collision fails initialization");}
        finally{MonsterInformation.MonsterInformations.Remove(9916701);}
        Call(service,null,"Initialize");Call(service,null,"Initialize");
        Func<Map,Entity> spawn=map=>{
            var info=(MonsterInformation)Call(service,null,"Profile");
            var body=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=map.ID,X=311,Y=256,
                MonsterInfo=info,MapObjType=MapObjectType.Monster,Name=info.Name,Body=info.Mesh,Level=info.Level,
                MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,Defence=info.Defence};
            info.Owner=body;body.MaxHitpoints=body.Hitpoints=info.Hitpoints;map.AddEntity2(body);return body;
        };
        Func<Map,Entity,object> bind=(map,body)=>{
            lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                var binding=Call(service,null,"Bind",c,t,p,map,body);t.Commit();return binding;
            }}
        };
        DMaps.MapPaths[1011]="maps\\1011.map";
        var first=(Map)Call(typeof(Map),null,"CreateDynamic",(ushort)1011,"maps\\1011.map",false);
        var second=(Map)Call(typeof(Map),null,"CreateDynamic",(ushort)1011,"maps\\1011.map",false);
        try {
            first.Portals.Clear();second.Portals.Clear();
            MapsTable.MapInformations.Add(first.ID,new MapsTable.MapInformation{ID=first.ID,BaseID=1011,Owner=p.Entity.UID,Status=8192});
            MapsTable.MapInformations.Add(second.ID,new MapsTable.MapInformation{ID=second.ID,BaseID=1011,Owner=p.Entity.UID,Status=8192});
            var body=spawn(first);var nextBody=spawn(second);
            Check(body.Name=="RockBehemoth" && body.Body==105 && body.Level==42 && body.Hitpoints==3656
                && !body.MonsterInfo.Boss && body.MonsterInfo.OwnItemRate==0,"staged Behemoth has documented native-display values and explicit Revora body/combat profile");
            var metadata=MapsTable.MapInformations[first.ID];metadata.Owner++;MapsTable.MapInformations[first.ID]=metadata;
            Check(AbyssRejected(()=>bind(first,body)),"foreign private-map ownership refuses binding");metadata.Owner=p.Entity.UID;MapsTable.MapInformations[first.ID]=metadata;
            var clone=new Entity(EntityFlag.Monster,false){UID=body.UID,MapID=first.ID,MonsterInfo=body.MonsterInfo};
            Check(AbyssRejected(()=>bind(first,clone)),"counterfeit same-UID body cannot bind");
            Query(@"CREATE FUNCTION refuse_elimination_binding() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER refuse_elimination_binding BEFORE INSERT ON revora_elimination_rescue_bindings FOR EACH ROW EXECUTE FUNCTION refuse_elimination_binding();");
            try{Check(AbyssRejected(()=>bind(first,body)),"binding receipt failure leaves paid admission resumable");}
            finally{Query("DROP TRIGGER refuse_elimination_binding ON revora_elimination_rescue_bindings;DROP FUNCTION refuse_elimination_binding()");}
            var binding=bind(first,body);Guid generation=(Guid)AbyssField(binding,"Generation");
            Check(generation!=Guid.Empty && (ushort)AbyssField(binding,"Map")==first.ID && (uint)AbyssField(binding,"Actor")==body.UID,"canonical private actor receives durable encounter generation");
            Check((Guid)AbyssField(bind(first,body),"Generation")==generation,"same live body binding is idempotent");
            Check(AbyssRejected(()=>bind(second,nextBody)),"second scene cannot supersede a registered first scene");
            MapsTable.MapInformations.Remove(first.ID);first.Dispose();
            var rebound=bind(second,nextBody);
            Check((Guid)AbyssField(rebound,"Generation")!=generation && (ushort)AbyssField(rebound,"Map")==second.ID,"retired scene rebind gets a fresh generation without charging spice");
            Check(EliminationWrite(p,"ResumeRescue")!=null,"scene binding preserves owned pending rescue");
        }finally {
            foreach(var map in new[]{first,second})if(Kernel.Maps.ContainsKey(map.ID)){MapsTable.MapInformations.Remove(map.ID);map.Dispose();}
        }
    }
    static GameState VerifyEliminationTravel(GameState p,Map town) {
        var world=typeof(Entity).Assembly.GetType("Revora.Game.Features.EliminationRescueWorld");Call(world,null,"Initialize");
        Func<object> start=()=>{lock(p.ItemSyncRoot)return Call(world,null,"Start",p,0u);};
        object hunt=start();var map=(Map)AbyssField(hunt,"Map");var body=(Entity)AbyssField(hunt,"Body");
        Check(!Fenced(p) && p.Entity.MapID==map.ID && p.Map.BaseID==1011 && p.Entity.X==309 && p.Entity.Y==256
            && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==map.ID,"resumed rescue uses saved ordinary travel to owned forest copy");
        Check(start()==null,"second live rescue entry refuses duplicate scene");
        var outsider=HousePlayer(town);outsider.Entity.Teleport((ushort)1011,map.ID,(ushort)309,(ushort)256);
        Check(outsider.Entity.MapID==1002,"foreign teleport cannot enter private rescue");
        uint hp=body.Hitpoints;Check(AncientHit(outsider.Entity,body,hp)==0 && body.Hitpoints==hp,"foreign rescue damage is blocked before HP mutation");
        body.Die(outsider.Entity);Check(!body.Dead,"foreign rescue death callback cannot grant victory");
        Check(AncientHit(p.Entity,body,100)>0 && body.Hitpoints<hp,"owned rescue accepts normal partial damage");
        p.Attackable=false;Check(!Revora.Game.Attacking.Handle.CanAttack(body,p.Entity,null,true),"Behemoth respects ordinary player attackability protection");
        p.Attackable=true;Check(body.MonsterInfo.InSight==p.Entity.UID && Revora.Game.Attacking.Handle.CanAttack(body,p.Entity,null,true),"Behemoth targets its own encounter player through normal attack authorization");
        Guid generation=(Guid)AbyssField(AbyssField(hunt,"Binding"),"Generation");
        lock(p.ItemSyncRoot)Call(world,null,"Leave",hunt);
        Check(p.Entity.MapID==1011 && !Kernel.Maps.ContainsKey(map.ID) && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1011,
            "leaving returns owner durably and releases rescue map");
        p=ReloadBall(p);EliminationAt(p,300020);AncientDialog(p,300020,0);AncientDialog(p,300020,9);
        hunt=EliminationHunt(world,p);map=(Map)AbyssField(hunt,"Map");
        Check(!Fenced(p) && (Guid)AbyssField(AbyssField(hunt,"Binding"),"Generation")!=generation,"reconnect resumes paid rescue with fresh actor generation");
        p.Entity.Teleport(1002,345,529);EntityTable.SaveEntity(p);Call(world,null,"Tick");
        Check(!Kernel.Maps.ContainsKey(map.ID) && p.Entity.MapID==1002 && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1002,
            "scene retirement after external travel frees map and does not teleport player back");
        var fresh=HousePlayer(town);fresh.Entity.Reborn=1;fresh.Entity.Level=70;EliminationAt(fresh,300020);EliminationWrite(fresh,"Begin");
        for(uint npc=300021;npc<=300032;npc++){EliminationAt(fresh,npc);EliminationWrite(fresh,"Gather",npc);}
        EliminationAt(fresh,300020);EliminationWrite(fresh,"Spice");uint freshSpice=fresh.Inventory.Objects.Single().UID;
        fresh.Entity.X=311;fresh.Entity.Y=256;EntityTable.SaveEntity(fresh);
        Query(@"CREATE FUNCTION refuse_rescue_scene() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER refuse_rescue_scene BEFORE INSERT ON revora_elimination_rescue_bindings FOR EACH ROW EXECUTE FUNCTION refuse_rescue_scene();");
        try {
            Check(AbyssRejected(()=>{lock(fresh.ItemSyncRoot)Call(world,null,"Start",fresh,freshSpice);}) && fresh.Inventory.ContainsUID(freshSpice)
                && Scalar("SELECT stage FROM revora_elimination_runs WHERE uid=@uid",fresh.Entity.UID)==1,
                "failed scene binding rolls back original spice debit and admission together");
        }finally{Query("DROP TRIGGER refuse_rescue_scene ON revora_elimination_rescue_bindings;DROP FUNCTION refuse_rescue_scene()");}
        var spiceItem=fresh.Inventory.Objects.Single();ItemHandler.UseItem(spiceItem,fresh);
        object paid=EliminationHunt(world,fresh);ItemHandler.UseItem(spiceItem,fresh);
        Check(!Fenced(fresh) && paid!=null && !fresh.Inventory.ContainsUID(freshSpice) && fresh.Entity.MapID!=1011,
            "earned spice creates and enters one paid private scene after retry");
        var paidBody=(Entity)AbyssField(paid,"Body");
        var counterfeit=new Entity(EntityFlag.Monster,false){UID=paidBody.UID,MapID=paidBody.MapID,MonsterInfo=paidBody.MonsterInfo.Copy(),Hitpoints=paidBody.Hitpoints};
        counterfeit.MonsterInfo.Owner=counterfeit;counterfeit.Die(fresh.Entity);
        Check(!paidBody.Dead,"counterfeit rescue monster cannot report death");
        fresh.Trade.InTrade=true;Check(AncientHit(fresh.Entity,paidBody,100)==0,"trade suspends rescue damage");fresh.Trade.InTrade=false;
        using(var ready=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()) {
            var held=Task.Run(()=>{lock(fresh.ItemSyncRoot){ready.Set();release.Wait();}});ready.Wait();
            try{Check(AncientHit(fresh.Entity,paidBody,100)==0,"busy rescue owner defers damage without blocking");}
            finally{release.Set();held.Wait();}
        }
        Query(@"CREATE FUNCTION refuse_rescue_kill() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER refuse_rescue_kill BEFORE INSERT ON revora_elimination_rescue_kills FOR EACH ROW EXECUTE FUNCTION refuse_rescue_kill();");
        try {
            AncientHit(fresh.Entity,paidBody,paidBody.Hitpoints);
            Check(Fenced(fresh) && Scalar("SELECT count(*) FROM revora_elimination_rescue_kills k JOIN revora_elimination_runs r ON r.id=k.run WHERE r.uid=@uid",fresh.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_elimination_rescues s JOIN revora_elimination_runs r ON r.id=s.run WHERE r.uid=@uid AND s.state='pending'",fresh.Entity.UID)==1,
                "failed defeat receipt preserves paid pending rescue and fences stale session");
        }finally{Query("DROP TRIGGER refuse_rescue_kill ON revora_elimination_rescue_kills;DROP FUNCTION refuse_rescue_kill()");}
        Call(world,null,"Tick");fresh=ReloadBall(fresh);fresh.Entity.X=311;fresh.Entity.Y=256;EntityTable.SaveEntity(fresh);
        lock(fresh.ItemSyncRoot)paid=Call(world,null,"Start",fresh,0u);
        paidBody.Die(fresh.Entity);var resumedBody=(Entity)AbyssField(paid,"Body");
        Check(!resumedBody.Dead && !Fenced(fresh),"retired rescue body cannot grant victory in resumed generation");
        var liveBinding=AbyssField(paid,"Binding");Guid liveGeneration=(Guid)AbyssField(liveBinding,"Generation"),liveRun=(Guid)AbyssField(liveBinding,"Run");
        Query("UPDATE revora_elimination_rescue_bindings SET generation=@generation WHERE run=@run","generation",Guid.NewGuid(),"run",liveRun);
        AncientHit(fresh.Entity,resumedBody,resumedBody.Hitpoints);
        Check(!resumedBody.Dead,"stale saved rescue generation refuses death credit");
        Query("UPDATE revora_elimination_rescue_bindings SET generation=@generation WHERE run=@run","generation",liveGeneration,"run",liveRun);
        AncientHit(fresh.Entity,resumedBody,resumedBody.Hitpoints);resumedBody.Die(fresh.Entity);
        Check(!Fenced(fresh) && resumedBody.Dead && fresh.Map.FloorItems.Count==0
            && Scalar("SELECT count(*) FROM revora_elimination_rescue_kills k JOIN revora_elimination_runs r ON r.id=k.run WHERE r.uid=@uid",fresh.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_elimination_rescues s JOIN revora_elimination_runs r ON r.id=s.run WHERE r.uid=@uid AND s.state='rescued'",fresh.Entity.UID)==1,
            "owned lethal damage saves one rescue victory without legacy floor loot");
        Check(!HasAchievement(fresh,11010),"Behemoth death alone does not grant full Elimination achievement");
        Check(fresh.Map.Npcs.ContainsKey(300033) && fresh.Map.Npcs[300033].Mesh==4690,"saved Behemoth victory publishes Landy in owned scene");
        AncientDialog(fresh,300033,1);
        Check(Scalar("SELECT stage FROM revora_elimination_runs WHERE uid=@uid",fresh.Entity.UID)==2,"unoffered Landy acknowledgement cannot advance rescue");
        lock(fresh.ItemSyncRoot)Call(world,null,"Leave",paid);
        fresh=ReloadBall(fresh);fresh.Entity.X=311;fresh.Entity.Y=256;EntityTable.SaveEntity(fresh);
        lock(fresh.ItemSyncRoot)paid=Call(world,null,"Start",fresh,0u);
        Check(!Fenced(fresh) && AbyssField(paid,"Body")==null && fresh.Map.Entities.Count==0 && fresh.Map.Npcs.ContainsKey(300033),
            "post-victory reconnect restores Landy without respawning Behemoth");
        var landyClock=(Func<DateTime>)EliminationStore.GetField("Clock",Hidden).GetValue(null);var landyNow=landyClock();
        AncientDialog(fresh,300033,0);
        try {
            EliminationStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>landyNow.AddSeconds(61)));
            AncientDialog(fresh,300033,1);
            Check(Scalar("SELECT stage FROM revora_elimination_runs WHERE uid=@uid",fresh.Entity.UID)==2,"expired Landy offer cannot advance rescue");
        }finally{EliminationStore.GetField("Clock",Hidden).SetValue(null,landyClock);}
        Query(@"CREATE FUNCTION refuse_landy() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER refuse_landy BEFORE INSERT ON revora_elimination_landy FOR EACH ROW EXECUTE FUNCTION refuse_landy();");
        try {
            AncientDialog(fresh,300033,0);AncientDialog(fresh,300033,1);
            Check(Fenced(fresh) && Scalar("SELECT stage FROM revora_elimination_runs WHERE uid=@uid",fresh.Entity.UID)==2,
                "late Landy receipt failure preserves defeated rescue for reconnect");
        }finally{Query("DROP TRIGGER refuse_landy ON revora_elimination_landy;DROP FUNCTION refuse_landy()");}
        Call(world,null,"Tick");fresh=ReloadBall(fresh);fresh.Entity.X=311;fresh.Entity.Y=256;EntityTable.SaveEntity(fresh);
        lock(fresh.ItemSyncRoot)paid=Call(world,null,"Start",fresh,0u);
        ushort rescuedMap=fresh.Entity.MapID;AncientDialog(fresh,300033,0);AncientDialog(fresh,300033,1);AncientDialog(fresh,300033,1);
        Check(!Fenced(fresh) && fresh.Entity.MapID==1011 && !Kernel.Maps.ContainsKey(rescuedMap)
            && Scalar("SELECT stage FROM revora_elimination_runs WHERE uid=@uid",fresh.Entity.UID)==3
            && Scalar("SELECT count(*) FROM revora_elimination_landy l JOIN revora_elimination_runs r ON r.id=l.run WHERE r.uid=@uid",fresh.Entity.UID)==1,
            "ordinary Landy dialogue saves one report and returns rescuer to Georgia's forest");
        fresh=ReloadBall(fresh);fresh.Entity.X=311;fresh.Entity.Y=256;EntityTable.SaveEntity(fresh);
        Check(EliminationWrite(fresh,"ResumeRescue")==null && !HasAchievement(fresh,11010),"saved Landy report prevents replay and leaves final achievement unearned");
        VerifyEliminationCure(fresh);
        // Restore the caller's canonical session after the reload used above.
        p.BindWorldMap(1011);p.Entity.X=311;p.Entity.Y=256;EntityTable.SaveEntity(p);return p;
    }
    static void VerifyEliminationCure(GameState p) {
        var pills=typeof(Entity).Assembly.GetType("Revora.Game.Features.EliminationPills");
        EliminationAt(p,300020);Check(EliminationWrite(p,"AdmitDen")==null,"den admission requires completed cure");
        var early=HuntMonster(p,"FireSpiritL43");Call(pills,null,"Collect",early,p.Entity);
        Check(p.Inventory.Count==0,"Fire Pills require Georgia's accepted cure request");
        EliminationAt(p,300020);AncientDialog(p,300020,0);AncientDialog(p,300020,5);AncientDialog(p,300020,5);
        Check(!Fenced(p) && Scalar("SELECT stage FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==4,"Georgia accepts Landy's report once through ordinary dialogue");
        Call(pills,null,"Collect",early,p.Entity);Check(p.Inventory.Count==0,"earlier corpse cannot be replayed after cure acceptance");
        var wrong=HuntMonster(p,"FireSpirit");Call(pills,null,"Collect",wrong,p.Entity);
        Check(p.Inventory.Count==0,"ordinary FireSpirit is not FireSpiritL43 source");
        var genuine=HuntMonster(p,"FireSpiritL43");var clone=new Entity(EntityFlag.Monster,false){UID=genuine.UID,MapID=genuine.MapID,MonsterInfo=genuine.MonsterInfo,Hitpoints=0};
        Call(pills,null,"Collect",clone,p.Entity);Check(p.Inventory.Count==0,"counterfeit same UID cannot earn Fire Pill");
        Query(@"CREATE FUNCTION refuse_fire_pill() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER refuse_fire_pill BEFORE INSERT ON revora_elimination_pills FOR EACH ROW EXECUTE FUNCTION refuse_fire_pill();");
        try {
            Call(pills,null,"Collect",genuine,p.Entity);
            Check(Fenced(p) && p.Inventory.Count==0 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==0,
                "late Fire Pill receipt failure rolls back inventory");
        }finally{Query("DROP TRIGGER refuse_fire_pill ON revora_elimination_pills;DROP FUNCTION refuse_fire_pill()");}
        p=ReloadBall(p);EliminationAt(p,300020);
        bool money=Config.MoneyDropEnabled,cpRewards=Config.KillConquerPointsEnabled;int equipmentChance=Config.MonsterPlusEquipmentBasisPoints;
        Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;Config.MonsterPlusEquipmentBasisPoints=0;
        try {
            for(int i=0;i<10;i++) {
                var mob=HuntMonster(p,"FireSpiritL43");mob.MaxHitpoints=mob.Hitpoints=100;mob.IsDropped=false;mob.Die(p.Entity);
                Parallel.For(0,8,j=>Call(pills,null,"Collect",mob,p.Entity));
                Check(!Fenced(p) && p.Inventory.Objects.Count(x=>x.ID==711041 && x.Bound)==i+1,"canonical FireSpiritL43 death issues one pill despite repeated callbacks");
            }
        }finally{Config.MoneyDropEnabled=money;Config.KillConquerPointsEnabled=cpRewards;Config.MonsterPlusEquipmentBasisPoints=equipmentChance;}
        Call(pills,null,"Collect",HuntMonster(p,"FireSpiritL43"),p.Entity);Check(p.Inventory.Count==10,"cure run caps earned pills at ten");
        EliminationAt(p,300020);uint lost=p.Inventory.Objects.First().UID;
        Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)lost);
        Check(EliminationWrite(p,"Recover",lost)==null,"stored Fire Pill is not recoverable loss");
        Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)lost);
        ItemHandler.DropItem(new ItemUsage(true){UID=lost},p);
        Check(!p.Inventory.ContainsUID(lost) && Scalar("SELECT count(*) FROM items WHERE uid=@uid",lost)==0,"normal bound Fire Pill discard destroys the earned item");
        Query(@"CREATE FUNCTION refuse_pill_replacement() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER refuse_pill_replacement BEFORE INSERT ON revora_elimination_replacements FOR EACH ROW EXECUTE FUNCTION refuse_pill_replacement();");
        try {Check(AbyssRejected(()=>EliminationWrite(p,"Recover",lost)) && p.Inventory.Count==9
            && Scalar("SELECT count(*) FROM revora_elimination_pills WHERE item_uid=@uid",lost)==1,"failed pill replacement preserves earned receipt and nine remaining pills");}
        finally{Query("DROP TRIGGER refuse_pill_replacement ON revora_elimination_replacements;DROP FUNCTION refuse_pill_replacement()");}
        AncientDialog(p,300020,0);AncientDialog(p,300020,3);
        uint replacement=checked((uint)Scalar("SELECT new_uid FROM revora_elimination_replacements WHERE old_uid=@uid",lost));
        Parallel.For(0,8,i=>EliminationWrite(p,"Recover",lost));
        Check(p.Inventory.Count==10 && p.Inventory.ContainsUID(replacement),"ordinary Georgia replacement and concurrent replay preserve ten earned pills");
        p=ReloadBall(p);EliminationAt(p,300020);
        Check(p.Inventory.ContainsUID(replacement) && EliminationWrite(p,"Recover",lost)==null && EliminationWrite(p,"Cleanup")==null,"pill recovery survives reload and current pills survive cleanup");
        var pillClock=(Func<DateTime>)EliminationStore.GetField("Clock",Hidden).GetValue(null);var pillNow=pillClock();
        try {
            EliminationStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>pillNow.AddDays(1)));
            Check(EliminationWrite(p,"Recover",replacement)==null,"expired cure run cannot replace pills");
            lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                var cleanup=Call(EliminationStore,null,"Cleanup",c,t,p);
                Check(cleanup!=null && ((uint[])AbyssField(cleanup,"Removed")).Length==10,
                    "expired cleanup selects all ten earned pills including replacement");
                t.Rollback();
            }}
        }finally{EliminationStore.GetField("Clock",Hidden).SetValue(null,pillClock);}
        Check(p.Inventory.Count==10 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=711041",p.Entity.UID)==10,
            "rolled-back expired cleanup preserves pills for cure validation");
        var item=p.Inventory.Objects.First();item.Lock=1;
        Check(EliminationWrite(p,"Cure")==null && p.Inventory.Count==10,"locked earned pill blocks cure without consumption");item.Lock=0;
        Query(@"CREATE FUNCTION refuse_cure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER refuse_cure BEFORE INSERT ON revora_elimination_cures FOR EACH ROW EXECUTE FUNCTION refuse_cure();");
        try {
            Check(AbyssRejected(()=>EliminationWrite(p,"Cure")) && p.Inventory.Count==10
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=711041",p.Entity.UID)==10
                && Scalar("SELECT stage FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==4,"late cure receipt failure restores all ten pills");
        }finally{Query("DROP TRIGGER refuse_cure ON revora_elimination_cures;DROP FUNCTION refuse_cure()");}
        AncientDialog(p,300020,0);AncientDialog(p,300020,6);AncientDialog(p,300020,6);
        Check(!Fenced(p) && p.Inventory.Count==0 && Scalar("SELECT stage FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==5
            && Scalar("SELECT count(*) FROM revora_elimination_cures c JOIN revora_elimination_runs r ON r.id=c.run WHERE r.uid=@uid",p.Entity.UID)==1,
            "ordinary Georgia dialogue consumes ten earned pills and saves one cure");
        Query(@"CREATE FUNCTION refuse_den_admission() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER refuse_den_admission BEFORE INSERT ON revora_elimination_dens FOR EACH ROW EXECUTE FUNCTION refuse_den_admission();");
        try {Check(AbyssRejected(()=>EliminationWrite(p,"AdmitDen")) && Scalar("SELECT count(*) FROM revora_elimination_dens d JOIN revora_elimination_runs r ON r.id=d.run WHERE r.uid=@uid",p.Entity.UID)==0,
            "failed den admission preserves cure without reservation");}
        finally{Query("DROP TRIGGER refuse_den_admission ON revora_elimination_dens;DROP FUNCTION refuse_den_admission()");}
        p.ActiveNpc=0;Check(EliminationWrite(p,"AdmitDen")==null,"den admission requires actual Georgia interaction");EliminationAt(p,300020);
        Parallel.For(0,8,i=>EliminationWrite(p,"AdmitDen"));
        var den=EliminationWrite(p,"AdmitDen");var deadline=(DateTime)AbyssField(den,"DenUntil");
        Check((Guid)AbyssField(den,"DenRun")!=Guid.Empty && Scalar("SELECT count(*) FROM revora_elimination_dens d JOIN revora_elimination_runs r ON r.id=d.run WHERE r.uid=@uid",p.Entity.UID)==1,
            "concurrent den admission stores one owned cured run");
        p=ReloadBall(p);EliminationAt(p,300020);Call(pills,null,"Collect",HuntMonster(p,"FireSpiritL43"),p.Entity);
        Check((DateTime)AbyssField(EliminationWrite(p,"AdmitDen"),"DenUntil")==deadline,"den admission reload preserves fixed daily deadline");
        var denClock=(Func<DateTime>)EliminationStore.GetField("Clock",Hidden).GetValue(null);
        try {EliminationStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>deadline));
            Check(EliminationWrite(p,"AdmitDen")==null,"den reservation expires at daily reset");
        }finally{EliminationStore.GetField("Clock",Hidden).SetValue(null,denClock);}

        Check(p.Inventory.Count==0 && EliminationWrite(p,"Cure")==null && EliminationWrite(p,"Recover",replacement)==null && !HasAchievement(p,11010),"cure reload prevents more pills or repeat cure without granting final achievement");
        VerifyEliminationDen(p);
    }
    static object EliminationHunt(Type world,GameState p) {
        foreach(var entry in (System.Collections.IEnumerable)world.GetField("Hunts",Hidden).GetValue(null)){
            var value=entry.GetType().GetProperty("Value").GetValue(entry,null);if(ReferenceEquals(AbyssField(value,"Player"),p))return value;
        }return null;
    }
    static object EliminationClaim(GameState p) {
        var mentor=typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorExperience");
        lock(p.ItemSyncRoot)lock(mentor.GetField("Sync",Hidden).GetValue(null))using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            var reward=Call(EliminationStore,null,"Claim",c,t,p);t.Commit();Call(EliminationStore,null,"ApplyReward",p,reward);return reward;
        }}
    }
    static void VerifyEliminationDen(GameState p) {
        uint wardrobe=StoredQuestGarment(p);
        DMaps.MapPaths[1021]="maps\\1021.map";
        var world=typeof(Entity).Assembly.GetType("Revora.Game.Features.EliminationDenWorld");Call(world,null,"Initialize");
        Check(EliminationClaim(p)==null,"cured run cannot claim before den victory");
        Func<object> start=()=>{lock(p.ItemSyncRoot)return Call(world,null,"Start",p);};
        AncientDialog(p,300020,0);AncientDialog(p,300020,7);
        object h=EliminationHunt(world,p);
        Check(h!=null && p.Map.BaseID==1021 && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==p.Entity.MapID,"ordinary Georgia choice enters owned mine den durably");
        var map=(Map)AbyssField(h,"Map");var outsider=HousePlayer(Kernel.Maps[1002]);outsider.Entity.Teleport((ushort)1021,map.ID,(ushort)32,(ushort)40);
        var body=(Entity)AbyssField(h,"Body");uint hp=body.Hitpoints;
        Check(outsider.Entity.MapID==1002 && AncientHit(outsider.Entity,body,hp)==0 && body.Hitpoints==hp,"foreign den entry and damage are rejected");
        for(int slot=0;slot<7;slot++) {
            if(slot==1){p.Entity.MaxHitpoints=100;p.Entity.Hitpoints=0;Call(world,null,"Tick");
                Check(!p.Entity.Dead && p.Entity.MapID==1011 && !Kernel.Maps.ContainsKey(map.ID),"death tick revives and returns den owner without losing kills");
                EliminationAt(p,300020);h=start();map=(Map)AbyssField(h,"Map");}
            if(slot==3){var lifecycle=typeof(Entity).Assembly.GetType("Revora.Game.Features.EliminationLifecycle");
                Check(AbyssRejected(()=>Call(lifecycle,null,"RecoverStartup")),"startup recovery refuses active runtime den");
                Call(world,null,"Dispose",h);
                Check(AbyssRejected(()=>Call(lifecycle,null,"RecoverStartup")),"startup recovery refuses admitted player despite lost scene");
                Kernel.GamePool.Remove(p.Entity.UID);
                Query(@"CREATE FUNCTION refuse_elimination_return() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER refuse_elimination_return BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION refuse_elimination_return();");
                try{Check(AbyssRejected(()=>Call(lifecycle,null,"RecoverStartup")) && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==map.ID
                    && Scalar("SELECT count(*) FROM revora_elimination_den_bindings b JOIN revora_elimination_runs r ON r.id=b.run WHERE r.uid=@uid AND b.active",p.Entity.UID)==1,
                    "failed startup return preserves private location and active recovery evidence");}
                finally{Query("DROP TRIGGER refuse_elimination_return ON entities;DROP FUNCTION refuse_elimination_return()");}
                Call(lifecycle,null,"RecoverStartup");Call(lifecycle,null,"RecoverStartup");
                Check(Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1011
                    && Scalar("SELECT count(*) FROM revora_elimination_den_kills k JOIN revora_elimination_runs r ON r.id=k.run WHERE r.uid=@uid",p.Entity.UID)==3,
                    "startup recovery returns exact saved owner and preserves three earned kills idempotently");
                p=ReloadBall(p);EliminationAt(p,300020);h=start();map=(Map)AbyssField(h,"Map");}
            if(slot==4){Kernel.GamePool.Remove(p.Entity.UID);Call(world,null,"Tick");
                Check(!Kernel.Maps.ContainsKey(map.ID) && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1011,"disconnect tick durably returns owner and releases den");
                p=ReloadBall(p);EliminationAt(p,300020);h=start();map=(Map)AbyssField(h,"Map");}
            if(slot==5){p=ReloadBall(p);Call(world,null,"Tick");
                Check(p.Entity.MapID!=map.ID && Kernel.Maps.ContainsKey(p.Entity.MapID) && !Kernel.Maps.ContainsKey(map.ID),"replacement session remains on a valid public map after den retirement");
                EliminationAt(p,300020);h=start();map=(Map)AbyssField(h,"Map");}
            if(slot==6){var savedClock=(Func<DateTime>)EliminationStore.GetField("Clock",Hidden).GetValue(null);var end=(DateTime)AbyssField(h,"Until");
                try{EliminationStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>end));Call(world,null,"Tick");
                    Check(p.Entity.MapID==1011 && !Kernel.Maps.ContainsKey(map.ID)
                        && Scalar("SELECT count(*) FROM revora_elimination_den_kills k JOIN revora_elimination_runs r ON r.id=k.run WHERE r.uid=@uid",p.Entity.UID)==6,
                        "daily expiry retires den without fabricating leader victory");}
                finally{EliminationStore.GetField("Clock",Hidden).SetValue(null,savedClock);}
                // Restore the fixture clock only to continue the independent final-reward path.
                EliminationAt(p,300020);h=start();map=(Map)AbyssField(h,"Map");}
            body=(Entity)AbyssField(h,"Body");Check((int)AbyssField(AbyssField(h,"Binding"),"Slot")==slot && body.Name==(slot==6?"RockLeader":"RockLackey"),"den reveals only next earned actor");
            p.Entity.X=body.X;p.Entity.Y=(ushort)(body.Y+2);EntityTable.SaveEntity(p);
            if(slot==0){Check(AncientHit(p.Entity,body,10)>0 && body.Hitpoints<hp,"normal owned den partial hit works");
                Query(@"CREATE FUNCTION refuse_den_kill() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER refuse_den_kill BEFORE INSERT ON revora_elimination_den_kills FOR EACH ROW EXECUTE FUNCTION refuse_den_kill();");
                try{AncientHit(p.Entity,body,body.Hitpoints+100);Check(Fenced(p) && Scalar("SELECT count(*) FROM revora_elimination_den_kills k JOIN revora_elimination_runs r ON r.id=k.run WHERE r.uid=@uid",p.Entity.UID)==0,"late den kill failure fences session without progress");}
                finally{Query("DROP TRIGGER refuse_den_kill ON revora_elimination_den_kills;DROP FUNCTION refuse_den_kill()");}
                lock(p.ItemSyncRoot)Call(world,null,"Leave",h);p=ReloadBall(p);EliminationAt(p,300020);h=start();map=(Map)AbyssField(h,"Map");body=(Entity)AbyssField(h,"Body");p.Entity.X=body.X;p.Entity.Y=(ushort)(body.Y+2);EntityTable.SaveEntity(p);
            }
            AncientHit(p.Entity,body,body.Hitpoints+100);body.Die(p.Entity);
            Check(!Fenced(p) && Scalar("SELECT count(*) FROM revora_elimination_den_kills k JOIN revora_elimination_runs r ON r.id=k.run WHERE r.uid=@uid",p.Entity.UID)==slot+1,"normal lethal hit saves one den kill despite repeated death");
            if(slot==2){p.Entity.X=32;p.Entity.Y=40;EntityTable.SaveEntity(p);AncientDialog(p,300034,0);AncientDialog(p,300034,1);
                Check(p.Entity.MapID==1011 && !Kernel.Maps.ContainsKey(map.ID),"den exit dialogue returns and releases scene");
                p=ReloadBall(p);EliminationAt(p,300020);h=start();map=(Map)AbyssField(h,"Map");
                Check((int)AbyssField(AbyssField(h,"Binding"),"Slot")==3,"den reconnect retains three distinct lackey kills");}
        }
        Check(p.Entity.MapID==1011 && !Kernel.Maps.ContainsKey(map.ID) && Scalar("SELECT stage FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==6,"leader victory returns owner with saved final stage");
        EliminationAt(p,300020);ushort before=p.Entity.SubClasses.StudyPoints;ulong exp=p.Entity.Experience;
        var math=typeof(Entity).Assembly.GetType("Revora.Game.Features.ExperienceMath");ulong expectedBall=(ulong)Call(math,null,"Ball",p.Entity.Level);
        while(p.Inventory.Count<40)Pack(p,1088001,1);
        Check(EliminationClaim(p)==null && p.Inventory.Count==40 && p.Entity.SubClasses.StudyPoints==before,"full bag defers all Elimination rewards without partial payment");
        foreach(uint filler in p.Inventory.Objects.Select(i=>i.UID).ToArray())RemoveFixtureItem(p,filler);
        Query(@"CREATE FUNCTION refuse_elimination_claim() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER refuse_elimination_claim BEFORE INSERT ON revora_elimination_claims FOR EACH ROW EXECUTE FUNCTION refuse_elimination_claim();");
        try{Check(AbyssRejected(()=>EliminationClaim(p)) && p.Entity.SubClasses.StudyPoints==before && p.Entity.Experience==exp
            && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",p.Entity.UID)==before
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND (id BETWEEN 800000 AND 899999 OR id=4030101)",p.Entity.UID)==0,"late final claim failure rolls back experience and study");}
        finally{Query("DROP TRIGGER refuse_elimination_claim ON revora_elimination_claims;DROP FUNCTION refuse_elimination_claim()");}
        // Evaluate a pre-upgrade pending contract in a rolled-back transaction.
        // Its obsolete P7 flag must never produce a P7 or retroactive rune.
        var mentor=typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorExperience");
        lock(p.ItemSyncRoot)lock(mentor.GetField("Sync",Hidden).GetValue(null))using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            using(var q=new Npgsql.NpgsqlCommand("UPDATE revora_elimination_runs SET rune_reward=false,valuable_reward=true WHERE uid=@uid",c,t)){
                q.Parameters.AddWithValue("uid",(long)p.Entity.UID);q.ExecuteNonQuery();}
            var legacy=Call(EliminationStore,null,"Claim",c,t,p);
            using(var q=new Npgsql.NpgsqlCommand("SELECT count(*) FROM items WHERE entityid=@uid AND (id BETWEEN 800000 AND 899999 OR id=4030101)",c,t)){
                q.Parameters.AddWithValue("uid",(long)p.Entity.UID);
                Check(legacy!=null && AbyssField(legacy,"Rune")==null && Convert.ToInt64(q.ExecuteScalar())==0,"old daily Elimination P7 flag grants neither a P7 nor a retroactive rune");}
            t.Rollback();
        }}
        AncientDialog(p,300020,0);AncientDialog(p,300020,8);Parallel.For(0,8,i=>EliminationClaim(p));
        Check(!Fenced(p) && p.Entity.SubClasses.StudyPoints==before+50 && Scalar("SELECT count(*) FROM revora_elimination_claims c JOIN revora_elimination_runs r ON r.id=c.run WHERE r.uid=@uid",p.Entity.UID)==1,
            "ordinary Georgia claim awards exactly fifty Study Points once");
        Check(Scalar("SELECT c.experience FROM revora_elimination_claims c JOIN revora_elimination_runs r ON r.id=c.run WHERE r.uid=@uid",p.Entity.UID)==(long)expectedBall,
            "final receipt records exactly one EXP-ball unit at the earned level");
        var earnedRune=p.Inventory.Objects.Single(i=>i.ID==4030101);
        Check(earnedRune.Bound && ConquerItemInformation.BaseInformations[earnedRune.ID].Class==0
            && Scalar("SELECT c.rune_uid FROM revora_elimination_claims c JOIN revora_elimination_runs r ON r.id=c.run WHERE r.uid=@uid",p.Entity.UID)==earnedRune.UID,
            "Elimination final claim includes one bound level-one yellow rune with saved receipt");
        Check(!p.Inventory.Objects.Any(i=>i.ID>=800000 && i.ID<900000)
            && Scalar("SELECT count(*) FROM revora_elimination_claims c JOIN revora_elimination_runs r ON r.id=c.run WHERE r.uid=@uid AND c.soul_uid IS NOT NULL",p.Entity.UID)==0,"daily Elimination saves no P7 item or soul receipt");
        byte claimedLevel=p.Entity.Level;ulong claimedExp=p.Entity.Experience;
        p=ReloadBall(p);EliminationAt(p,300020);new TitleStorage().Login(p);
        Check(p.Entity.Level==claimedLevel && p.Entity.Experience==claimedExp && p.Inventory.ContainsUID(earnedRune.UID),"final experience projection matches saved reload");
        Check(HasAchievement(p,11010) && EliminationClaim(p)==null && p.Entity.SubClasses.StudyPoints==before+50,"earned final achievement and reward survive reload without replay");
        Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"staged Elimination den reward and production reload preserve stored wardrobe design");
    }
    static void VerifyEliminationDialogue(Map town) {
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.EliminationQuest");
        var p=HousePlayer(town);p.Entity.Reborn=1;p.Entity.Level=70;EliminationAt(p,300020);
        AncientDialog(p,300020,0);AncientDialog(p,300020,1);
        Check(Scalar("SELECT count(*) FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==0,"uninitialized Elimination dispatcher is disabled");
        var plant=Kernel.Maps[1011].Npcs[300021];plant.Mesh=1;
        Check(AbyssRejected(()=>Call(quest,null,"Initialize")),"quest initialization refuses conflicting actor identity");plant.Mesh=4700;
        Call(quest,null,"Initialize");Call(quest,null,"Initialize");
        Check(Kernel.Maps[1011].Npcs[300021].Mesh==4700 && Kernel.Maps[1011].Npcs[300020].Mesh==4680,"idempotent initialization keeps native Georgia and herb profiles");
        AncientDialog(p,300020,1);
        Check(Scalar("SELECT count(*) FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==0,"forged admission without a menu refused");
        AncientDialog(p,300020,0);
        var clock=(Func<DateTime>)EliminationStore.GetField("Clock",Hidden).GetValue(null);var stamp=clock();
        try {
            EliminationStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>stamp.AddSeconds(61)));
            AncientDialog(p,300020,1);
            Check(Scalar("SELECT count(*) FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==0,"expired admission menu refused");
        }finally{EliminationStore.GetField("Clock",Hidden).SetValue(null,clock);}
        AncientDialog(p,300020,0);p.Entity.Level=71;AncientDialog(p,300020,1);
        Check(Scalar("SELECT count(*) FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==0,"changed eligibility invalidates menu");p.Entity.Level=70;
        AncientDialog(p,300020,0);AncientDialog(p,300020,1);AncientDialog(p,300020,1);
        Check(!Fenced(p) && Scalar("SELECT count(*) FROM revora_elimination_runs WHERE uid=@uid",p.Entity.UID)==1,"ordinary NPC admission is replay safe");
        AncientDialog(p,300020,2);Check(p.Inventory.Count==0,"unoffered spice choice creates no item");
        EliminationAt(p,300021);AncientDialog(p,300021,0);p.Entity.X=800;AncientDialog(p,300021,1);
        Check(p.Inventory.Count==0,"out-of-range herb choice refused");EliminationAt(p,300021);
        AncientDialog(p,300021,0);p.ActiveNpc=300022;
        Call(quest,null,"HandleNpc",p,new NpcRequest(true){NpcID=300021,OptionID=1});
        Check(p.Inventory.Count==0,"NPC identity mismatch refused");
        for(uint npc=300021;npc<=300032;npc++) {
            EliminationAt(p,npc);AncientDialog(p,npc,0);AncientDialog(p,npc,1);AncientDialog(p,npc,1);
        }
        Check(!Fenced(p) && p.Inventory.Count==12,"twelve distinct NPC gathers issue twelve herbs through sealed option packets");
        uint lost=p.Inventory.Objects.First().UID;ItemHandler.DropItem(new ItemUsage(true){UID=lost},p);
        EliminationAt(p,300020);AncientDialog(p,300020,0);AncientDialog(p,300020,3);AncientDialog(p,300020,3);
        Check(!Fenced(p) && p.Inventory.Count==12 && !p.Inventory.ContainsUID(lost),"Georgia replacement offer names the exact missing item once");
        AncientDialog(p,300020,0);AncientDialog(p,300020,2);AncientDialog(p,300020,2);
        Check(!Fenced(p) && p.Inventory.Count==1 && p.Inventory.Objects.Single().ID==723019,"Georgia dialogue atomically exchanges earned herbs once");
        uint spice=p.Inventory.Objects.Single().UID;p=ReloadBall(p);EliminationAt(p,300020);AncientDialog(p,300020,0);AncientDialog(p,300020,2);
        Check(p.Inventory.ContainsUID(spice) && p.Inventory.Count==1,"dialogue reload cannot replay spice reward");
        try {
            EliminationStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>stamp.AddDays(1)));
            AncientDialog(p,300020,4);Check(p.Inventory.ContainsUID(spice),"previous-day menu cannot clean items after reset");
            AncientDialog(p,300020,0);AncientDialog(p,300020,4);
            Check(!Fenced(p) && p.Inventory.Count==0,"fresh Georgia menu cleans expired owned spice");
        }finally{EliminationStore.GetField("Clock",Hidden).SetValue(null,clock);}
        // Shared packet dispatch consumes option 255 before feature handlers.
        AncientDialog(p,300020,255);Check(!Fenced(p) && p.Inventory.Count==0,"farewell packet has no quest side effects");
    }
}
