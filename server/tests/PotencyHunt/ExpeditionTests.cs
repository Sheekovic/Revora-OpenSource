using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Expedition=typeof(ExpeditionEvent);
    static DateTime expeditionNow;
    static object ExpeditionMember(GameState c)
    {return ((IDictionary)Expedition.GetField("Members",Hidden).GetValue(null))[c.Entity.UID];}
    static List<uint> Satchel(GameState c)
    {var m=ExpeditionMember(c);return (List<uint>)m.GetType().GetField("Loot").GetValue(m);}
    static string ExpeditionState(GameState c)
    {var m=ExpeditionMember(c);return (string)m.GetType().GetField("State").GetValue(m);}
    static void MoveTo(GameState c,Point p) {c.Entity.X=(ushort)p.X;c.Entity.Y=(ushort)p.Y;}
    static void VerifyExtractionPurse(Map town){
        Query("CREATE TABLE IF NOT EXISTS revora_expedition_claims(cycle text NOT NULL,uid bigint NOT NULL,items integer NOT NULL,rvp bigint NOT NULL,soul_type bigint NOT NULL DEFAULT 0,soul_uid bigint NOT NULL DEFAULT 0,PRIMARY KEY(cycle,uid))");
        var memberType=Expedition.GetNestedType("Member",BindingFlags.NonPublic);
        var gate=Expedition.GetField("Gate",Hidden).GetValue(null);
        foreach(string mode in new[]{"success","external-rvp","item-fault","saved-full","stale-session"}){
            var p=ReloadBall(WelcomePlayer(town,140,2,15));p.Entity.RevoraPoint=50;
            Check(EntityTable.SaveEntity(p),"Extraction managed fixture saves: "+mode);
            p=ReloadBall(p);
            object m=Activator.CreateInstance(memberType,true);
            memberType.GetField("Uid").SetValue(m,p.Entity.UID);memberType.GetField("State").SetValue(m,"extracting");
            ((List<uint>)memberType.GetField("Loot").GetValue(m)).Add(1088001);
            uint before=p.Entity.RevoraPoint;
            uint prize=Revora.Config.ExpeditionExtractRevoraPoints+Revora.Config.ExpeditionRevoraPointsPerItem;
            if(mode=="external-rvp")Query("UPDATE entities SET revorapoint=revorapoint+7 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(mode=="item-fault")Query("ALTER TABLE items ADD CONSTRAINT extraction_item_fault CHECK(id<>1088001) NOT VALID");
            if(mode=="saved-full")for(int i=0;i<40;i++){
                var item=Pack(p,1088000,1);p.Inventory.Remove(item,Enums.ItemUse.None,true);
            }
            if(mode=="stale-session")ReloadBall(p);
            bool paid=false;
            try {lock(gate)paid=(bool)Call(Expedition,null,"CommitExtraction",p,m);}
            catch(ArgumentNullException){if(!Fenced(p))throw;}
            finally{if(mode=="item-fault")Query("ALTER TABLE items DROP CONSTRAINT extraction_item_fault");}
            if(mode=="success"){
                Check(paid && !Fenced(p) && p.Entity.RevoraPoint==before+prize && CountId(p,1088001)==1,
                    "Extraction installs committed RVP exactly once without fencing a managed session");
                Check(EntityTable.SaveEntity(p),"Extraction post-claim autosave accepts committed purse preimage");
                p=ReloadBall(p);
                Check(p.Entity.RevoraPoint==before+prize && CountId(p,1088001)==1
                    && Scalar("SELECT count(*) FROM revora_expedition_claims WHERE uid=@uid",p.Entity.UID)==1,
                    "Extraction item, RVP and receipt survive full character reload");
            }else{
                Check(!paid && Scalar("SELECT count(*) FROM revora_expedition_claims WHERE uid=@uid",p.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=1088001",p.Entity.UID)==0
                    && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==before+(mode=="external-rvp"?7:0),
                    "Extraction rejects without overwriting or partial payout: "+mode);
            }
        }
    }
    static void VerifyExtractionSoul(Map town){
        var members=(IDictionary)Expedition.GetField("Members",Hidden).GetValue(null);
        var gate=Expedition.GetField("Gate",Hidden).GetValue(null);
        var clock=Expedition.GetField("Clock",Hidden);var cycle=Expedition.GetField("CycleStart",Hidden);
        object oldClock=clock.GetValue(null),oldCycle=cycle.GetValue(null);
        DateTime now=new DateTime(2031,1,1,8,58,0,DateTimeKind.Local);
        clock.SetValue(null,(Func<DateTime>)(()=>now));cycle.SetValue(null,now.AddMinutes(2));
        try{
            foreach(string mode in new[]{"success","full-bag","item-fault"}){
                var p=ReloadBall(WelcomePlayer(town,140,2,15));
                Check(ExpeditionEvent.Register(p).StartsWith("Seat confirmed"),"Extraction ordinary registration selects a valuable contract: "+mode);
                var m=members[p.Entity.UID];var type=m.GetType();
                uint soul=(uint)type.GetField("SoulType").GetValue(m);
                Check(soul>=800000 && soul<900000 && ConquerItemInformation.BaseInformations[soul].PurificationLevel==7
                    && ExpeditionEvent.Register(p).Contains("already") && (uint)type.GetField("SoulType").GetValue(m)==soul,
                    "Extraction duplicate registration preserves its catalog P7: "+mode);
                type.GetField("State").SetValue(m,"extracting");((List<uint>)type.GetField("Loot").GetValue(m)).Add(1088001);
                if(mode=="full-bag")for(int i=0;i<39;i++)Pack(p,1088000,1);
                if(mode=="item-fault")Query("ALTER TABLE items ADD CONSTRAINT extraction_soul_fault CHECK(id NOT BETWEEN 800000 AND 899999) NOT VALID");
                bool paid=false;
                try{lock(gate)paid=(bool)Call(Expedition,null,"CommitExtraction",p,m);}
                catch(ArgumentNullException){if(!Fenced(p))throw;}
                finally{if(mode=="item-fault")Query("ALTER TABLE items DROP CONSTRAINT extraction_soul_fault");}
                if(mode=="full-bag"){
                    Check(!paid && Scalar("SELECT count(*) FROM revora_expedition_claims WHERE uid=@uid",p.Entity.UID)==0
                        && ((List<uint>)type.GetField("Loot").GetValue(m)).Count==1,"Extraction reserves capacity for soul and satchel together");
                    foreach(var item in p.Inventory.Objects.ToArray())p.Inventory.Remove(item,Enums.ItemUse.None,true);
                    Query("DELETE FROM items WHERE entityid=@uid AND id=1088000","uid",(long)p.Entity.UID);
                    lock(gate)paid=(bool)Call(Expedition,null,"CommitExtraction",p,m);
                }
                if(mode=="item-fault"){
                    Check(!paid && Scalar("SELECT count(*) FROM revora_expedition_claims WHERE uid=@uid",p.Entity.UID)==0
                        && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==0,
                        "Extraction soul failure rolls back satchel, soul and receipt");
                }else{
                    var item=p.Inventory.Objects.Single(i=>i.ID==soul);
                    Check(paid && item.Bound && Scalar("SELECT soul_type FROM revora_expedition_claims WHERE uid=@uid",p.Entity.UID)==soul
                        && Scalar("SELECT soul_uid FROM revora_expedition_claims WHERE uid=@uid",p.Entity.UID)==item.UID,
                        "Extraction receipt records its exact frozen bound soul: "+mode);
                    p=ReloadBall(p);
                    Check(CountId(p,soul)==1 && p.Inventory.Objects.Single(i=>i.ID==soul).Bound && CountId(p,1088001)==1,
                        "Extraction valuable output survives production reload: "+mode);
                    uint paidRvp=p.Entity.RevoraPoint;bool replay=false;
                    try{lock(gate)replay=(bool)Call(Expedition,null,"CommitExtraction",p,m);}
                    catch(ArgumentNullException){if(!Fenced(p))throw;}
                    Check(!replay && Scalar("SELECT count(*) FROM revora_expedition_claims WHERE uid=@uid",p.Entity.UID)==1
                        && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==paidRvp
                        && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==2,
                        "Extraction consumed valuable claim cannot pay again after reload: "+mode);
                }
                members.Remove(p.Entity.UID);
            }
        }finally{clock.SetValue(null,oldClock);cycle.SetValue(null,oldCycle);}
    }
    static void VerifyExtractionRoute(string runtime){
        var status=Expedition.GetField("StatusPath",Hidden);var clock=Expedition.GetField("Clock",Hidden);
        object before=status.GetValue(null),beforeClock=clock.GetValue(null);
        status.SetValue(null,System.IO.Path.GetFullPath(System.IO.Path.Combine(runtime,"../../obj/extraction-route-status.json")));
        try{VerifyExpedition(runtime,false);}
        finally{status.SetValue(null,before);clock.SetValue(null,beforeClock);}
    }
    static void VerifyExpedition(string runtime,bool includeOther=true)
    {
        var field=World(runtime,1015);var town=Kernel.Maps[1002];
        Expedition.GetField("EventMap",Hidden).SetValue(null,field);
        expeditionNow=new DateTime(2030,1,15,8,56,0,DateTimeKind.Local);
        Expedition.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>expeditionNow));
        Call(Expedition,null,"InitializeField");
        var cells=(List<Point>)Expedition.GetField("FieldCells",Hidden).GetValue(null);
        var starts=(List<Point>)Expedition.GetField("Entries",Hidden).GetValue(null);
        var exits=(Point[])Expedition.GetField("Exits",Hidden).GetValue(null);
        var camps=(Point[])Expedition.GetField("Camps",Hidden).GetValue(null);
        Check(field.Floor.Bounds.Width==1024 && field.Floor.Bounds.Height==1024 && cells.Count>2000,"Extraction uses large real Bird Island terrain and a connected hunting area");
        Check(starts.Count==16 && starts.Distinct().Count()==16 && starts.All(p=>starts.Where(q=>q!=p).All(q=>Math.Max(Math.Abs(p.X-q.X),Math.Abs(p.Y-q.Y))>=35)),"sixteen entry positions are walkable, reachable and widely separated");
        Check(exits.Concat(camps).All(cells.Contains) && exits.Distinct().Count()==3 && field.Npcs.Count==6 && field.Portals.Count==0,"three reachable ferrymen and three camps replace the tiny arena exit");
        var cycle=expeditionNow.Date.AddHours(9);Expedition.GetField("CycleStart",Hidden).SetValue(null,cycle);
        var c=ReloadBall(WelcomePlayer(town,140,2,15));var rival=ReloadBall(WelcomePlayer(town,140,2,15));var late=ReloadBall(WelcomePlayer(town,140,2,15));var failed=ReloadBall(WelcomePlayer(town,140,2,15));
        c.Entity.PKMode=Enums.PKMode.Capture;
        c.Entity.RevoraPoint=50;Query("UPDATE entities SET revorapoint=50 WHERE uid=@uid","uid",(long)c.Entity.UID);
        Check(ExpeditionEvent.Register(c).StartsWith("Seat confirmed") && ExpeditionEvent.Register(rival).StartsWith("Seat confirmed")
            && ExpeditionEvent.Register(late).StartsWith("Seat confirmed") && ExpeditionEvent.Register(failed).StartsWith("Seat confirmed"),"ordinary solo players can reserve Extraction seats");
        Check(ExpeditionEvent.Register(c).Contains("already"),"duplicate registration does not reserve another seat");
        expeditionNow=cycle;Call(Expedition,null,"StartCycle");
        Check(ExpeditionEvent.InEvent(c.Entity) && ExpeditionEvent.InEvent(rival.Entity) && c.Entity.X!=rival.Entity.X,"registered players enter separated field positions");
        Check(c.Entity.PKMode==Enums.PKMode.PK,"Extraction enters open PvP mode for the hunt");
        var encounters=(IDictionary)Expedition.GetField("Encounters",Hidden).GetValue(null);
        Check(encounters.Count==75 && field.Entities.Values.Count(e=>e.MonsterInfo.ID==9913000)==72 && field.Entities.Values.Count(e=>e.MonsterInfo.ID==9913001)==3,"cycle creates 72 scouts and three guarded caches");
        Check(field.Entities.Values.All(e=>field.Floor[e.X,e.Y,MapObjectType.Player]),"every encounter uses actual walkable terrain");
        MoveTo(c,exits[0]);Check(!ExpeditionEvent.BeginExtract(c),"boats cannot extract before minute five");
        MoveTo(c,camps[0]);Check(((string)Call(Expedition,null,"ClaimCache",c,0)).Contains("guardian"),"living guardian blocks a cache claim");
        object guard=encounters.Values.Cast<object>().First(e=>(int)e.GetType().GetField("Camp",Instance).GetValue(e)==0);
        var body=(Entity)guard.GetType().GetField("Body",Instance).GetValue(guard);
        body.Die(c.Entity);body.Die(c.Entity);
        Check(((string)Call(Expedition,null,"ClaimCache",c,0)).Contains("Three relics") && Satchel(c).Count==3 && c.Inventory.Count==0,"defeated guardian unlocks three temporary relics without granting permanent items");
        MoveTo(rival,camps[0]);
        Parallel.For(0,8,i=>Call(Expedition,null,"ClaimCache",rival,0));
        Check(Satchel(rival).Count==0 && Satchel(c).Count==3,"guarded treasure is claimed once across competing players");
        expeditionNow=cycle.AddMinutes(5);MoveTo(c,camps[1]);Check(!ExpeditionEvent.BeginExtract(c),"forged remote extraction requests fail away from ferrymen");
        MoveTo(c,exits[1]);Check(!ExpeditionEvent.BeginExtract(c),"east boat remains closed until minute eight");
        MoveTo(c,exits[0]);Check(ExpeditionEvent.BeginExtract(c),"west boat accepts ordinary player after minute five");
        expeditionNow=expeditionNow.AddSeconds(9);Call(Expedition,null,"CompleteExtract",c);
        Check(c.Inventory.Count==0 && ExpeditionState(c)=="extracting","early completion cannot skip ten-second hold");
        ExpeditionEvent.NotifyAction(c,"you were hit");expeditionNow=expeditionNow.AddSeconds(2);Call(Expedition,null,"CompleteExtract",c);
        Check(c.Inventory.Count==0 && ExpeditionState(c)=="entered","damage cancels the extraction payout");
        Check(ExpeditionEvent.BeginExtract(c),"cancelled extraction can be retried");
        c.Entity.X++;expeditionNow=expeditionNow.AddSeconds(11);ExpeditionEvent.Tick();
        Check(ExpeditionState(c)=="entered" && c.Inventory.Count==0,"position changes cancel even when a movement notification is missed");
        for(int i=0;i<38;i++)Pack(c,1088001,1);
        MoveTo(c,exits[0]);ExpeditionEvent.BeginExtract(c);expeditionNow=expeditionNow.AddSeconds(11);Call(Expedition,null,"CompleteExtract",c);
        Check(ExpeditionState(c)=="entered" && Satchel(c).Count==3 && c.Entity.RevoraPoint==50,"full inventory keeps temporary loot and gives no partial payout");
        foreach(var item in c.Inventory.Objects.ToArray())c.Inventory.Remove(item,Enums.ItemUse.None,true);
        Query("DELETE FROM items WHERE entityid=@uid","uid",(long)c.Entity.UID);
        ExpeditionEvent.BeginExtract(c);expeditionNow=expeditionNow.AddSeconds(11);
        Parallel.For(0,8,i=>Call(Expedition,null,"CompleteExtract",c));
        Check(c.Entity.MapID==1002 && c.Inventory.Count==4 && c.Entity.RevoraPoint==460 && ExpeditionState(c)=="extracted","successful extraction grants three relics, one bound P7 and exactly 410 RVPs once, then returns home");
        Check(c.Entity.PKMode==Enums.PKMode.Capture,"returning from Extraction restores the player's original PK mode");
        Check(Scalar("SELECT count(*) FROM items WHERE entityid=@uid",c.Entity.UID)==4 && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",c.Entity.UID)==460
            && Scalar("SELECT count(*) FROM revora_expedition_claims WHERE uid=@uid",c.Entity.UID)==1,"extracted inventory and RVP reward persist with one claim marker");
        uint soul=(uint)Scalar("SELECT soul_type FROM revora_expedition_claims WHERE uid=@uid",c.Entity.UID);
        uint soulUid=(uint)Scalar("SELECT soul_uid FROM revora_expedition_claims WHERE uid=@uid",c.Entity.UID);
        Check(soul!=0 && c.Inventory.Objects.Any(i=>i.UID==soulUid && i.ID==soul && i.Bound) && !Fenced(c),
            "guarded cache and boat extraction install the exact bound soul without fencing");
        Check(EntityTable.SaveEntity(c),"earned Extraction reward permits a normal full save");
        c=ReloadBall(c);
        Check(c.Inventory.Count==4 && c.Entity.RevoraPoint==460 && c.Inventory.Objects.Any(i=>i.UID==soulUid && i.ID==soul && i.Bound),
            "full guarded-cache and boat route preserves soul identity and purse on reload");
        var satchel=Satchel(rival);satchel.Add(1088000);ExpeditionEvent.OnDeath(rival);
        Check(rival.Entity.MapID==1002 && satchel.Count==0 && rival.Inventory.Count==0,"death forfeits temporary loot and returns player safely");
        expeditionNow=cycle.AddMinutes(10);ExpeditionEvent.Tick();
        var specter=field.Entities.Values.First(e=>e.MonsterInfo.ID==9913002);
        Check(specter.MonsterInfo.Level==140 && specter.Name=="HorrifyingSpecter" && specter.Hitpoints==5000000 && specter.CharBuffer._Hitpoints==5000000 && specter.Boss==1,"minute-ten encounter uses the existing corrected Specter boss profile");
        Check(!HasAchievement(c,10525),"lower-level Extraction scouts and cache guardians grant no unarmed achievement");
        MoveTo(late,new Point(specter.X,specter.Y));
        late.Fake=true;specter.Die(late.Entity);late.Fake=false;
        Check(!specter.Dead && Satchel(late).Count==0,"fake late-boss killer cannot settle encounter or receive temporary rewards");
        Query("CREATE FUNCTION reject_extraction_unarmed() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER reject_extraction_unarmed BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN(NEW.achievement_id=10525) EXECUTE FUNCTION reject_extraction_unarmed()");
        try{specter.Die(late.Entity);}finally{Query("DROP TRIGGER reject_extraction_unarmed ON revora_achievement_evidence;DROP FUNCTION reject_extraction_unarmed()");}
        Check(!specter.Dead && !Fenced(late) && Satchel(late).Count==0 && !HasAchievement(late,10525),"failed unarmed save preserves living Specter without temporary loot");
        var victory=typeof(Entity).Assembly.GetType("Revora.Game.Features.WildMonsterVictory");
        victory.GetField("AfterCommit",Hidden).SetValue(null,(Action)(()=>{throw new System.IO.IOException("fixture acknowledgement lost");}));
        try{specter.Die(late.Entity);}finally{victory.GetField("AfterCommit",Hidden).SetValue(null,null);}
        Check(!specter.Dead && Satchel(late).Count==0 && Scalar("SELECT count(*) FROM revora_unarmed_wild_victories WHERE uid=@uid",late.Entity.UID)==1,
            "uncertain Specter commit saves one life while deferring temporary rewards until confirmed death");
        Parallel.For(0,12,i=>specter.Die(late.Entity));
        Check(specter.Dead && HasAchievement(late,10525) && Scalar("SELECT count(*) FROM revora_unarmed_wild_victories WHERE uid=@uid",late.Entity.UID)==1,
            "concurrent late-boss retries confirm one death and immediate unarmed achievement");
        Check(Satchel(late).Count==3 && (uint)ExpeditionMember(late).GetType().GetField("BonusPoints").GetValue(ExpeditionMember(late))==500,"late boss awards exactly three temporary relics and a 500-RVP extraction bonus");
        MoveTo(failed,exits[0]);Satchel(failed).Add(1088000);
        Query("ALTER TABLE revora_expedition_claims ADD CONSTRAINT fixture_extraction_failure CHECK(rvp<0) NOT VALID");
        ExpeditionEvent.BeginExtract(failed);expeditionNow=expeditionNow.AddSeconds(11);
        // This detached fixture has no screen timer lock or live socket to disconnect.
        try {Call(Expedition,null,"CompleteExtract",failed);}catch(ArgumentNullException) {if(!Fenced(failed))throw;}
        Check(Fenced(failed) && failed.Inventory.Count==0 && Scalar("SELECT count(*) FROM revora_expedition_claims WHERE uid=@uid",failed.Entity.UID)==0
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",failed.Entity.UID)==0,"failed extraction transaction grants neither items nor RVP and requires safe reload");
        Query("ALTER TABLE revora_expedition_claims DROP CONSTRAINT fixture_extraction_failure");
        expeditionNow=cycle.AddMinutes(20);ExpeditionEvent.Tick();
        Check(late.Entity.MapID==1002 && SatchelAfterEnd() && field.Entities.Count==0,"deadline forfeits unextracted rewards and removes every event encounter");
        late=ReloadBall(late);late.Entity.MyAchievement.Send();
        Check(HasAchievement(late,10525) && late.Inventory.Count==0,"Specter kill credit survives full reload while unextracted temporary rewards remain forfeited");
        Expedition.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.Now));
        if(includeOther)VerifyOtherEvents(runtime);
    }
    static void VerifyOtherEvents(string runtime)
    {
        var arena=World(runtime,700);var player=Player(arena);var rival=Player(arena);
        player.Entity.Hitpoints=5000;player.Entity.MaxHitpoints=10000;
        Call(typeof(WaveDefenseSystem),null,"RecoverBetweenWaves",player.Entity,15u);
        Check(player.Entity.Hitpoints==6500,"Wave Defense recovery restores 15% of maximum health");
        player.Entity.Hitpoints=9900;Call(typeof(WaveDefenseSystem),null,"RecoverBetweenWaves",player.Entity,15u);
        Check(player.Entity.Hitpoints==10000,"wave recovery never exceeds maximum HP");
        player.Entity.Hitpoints=0;Call(typeof(WaveDefenseSystem),null,"RecoverBetweenWaves",player.Entity,15u);
        Check(player.Entity.Dead,"wave recovery never revives a fallen player or post");player.Entity.Hitpoints=5000;
        var wave=typeof(WaveDefenseSystem);var instanceType=wave.GetNestedType("Instance",BindingFlags.NonPublic);
        var instance=Activator.CreateInstance(instanceType,true);instanceType.GetField("Arena").SetValue(instance,arena);
        ((List<GameState>)instanceType.GetField("Players").GetValue(instance)).Add(player);
        var postType=wave.GetNestedType("Post",BindingFlags.NonPublic);var posts=Array.CreateInstance(postType,3);
        for(int i=0;i<3;i++) {
            var mt=MonsterInformation.MonsterInformations[2].Copy();
            var e=new Entity(EntityFlag.Monster,false){MonsterInfo=mt,UID=(uint)(495000+i),MapID=arena.ID,X=(ushort)(42+i*8),Y=44,MaxHitpoints=12000};mt.Owner=e;e.Hitpoints=i==0?0u:6000u;
            var post=Activator.CreateInstance(postType,true);postType.GetField("Body").SetValue(post,e);postType.GetField("MaxHp").SetValue(post,12000u);posts.SetValue(post,i);
        }
        instanceType.GetField("Posts").SetValue(instance,posts);instanceType.GetField("Wave").SetValue(instance,1);
        var now=DateTime.Now;instanceType.GetField("FightUntil").SetValue(instance,now.AddMinutes(15));instanceType.GetField("NextMedic").SetValue(instance,now.AddMinutes(1));
        Call(wave,null,"TickInstance",instance,now);
        Check((DateTime)instanceType.GetField("NextWaveAt").GetValue(instance)==now.AddSeconds(8) && player.Entity.Hitpoints==6500,"cleared wave opens an eight-second regroup break and heals once");
        Call(wave,null,"TickInstance",instance,now.AddSeconds(3));
        Check(player.Entity.Hitpoints==6500 && (int)instanceType.GetField("Wave").GetValue(instance)==1,"repeated empty-field ticks do not repeatedly heal or skip the break");
        Call(wave,null,"TickInstance",instance,now.AddSeconds(8));
        var targets=(Dictionary<uint,uint>)instanceType.GetField("MonsterPost").GetValue(instance);
        Check((int)instanceType.GetField("Wave").GetValue(instance)==2 && targets.Count==8 && targets.Values.All(uid=>uid!=495000),"next wave starts on time and never targets the fallen post");
        foreach(var e in arena.Entities.Values.ToArray())arena.RemoveEntity(e);
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.RelicAdmissions"),null,"Initialize");var relic=typeof(RelicRushSystem);relic.GetField("EventMap",Hidden).SetValue(null,arena);relic.GetField("Running",Hidden).SetValue(null,true);
        var until=now.AddMinutes(2);relic.GetField("FightUntil",Hidden).SetValue(null,until);
        DateTime relicNow=now;relic.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>relicNow));
        var alive=(HashSet<uint>)relic.GetField("Alive",Hidden).GetValue(null);alive.Clear();alive.Add(player.Entity.UID);alive.Add(rival.Entity.UID);
        var scores=(Dictionary<uint,uint>)relic.GetField("Scores",Hidden).GetValue(null);scores.Clear();
        relic.GetField("Holder",Hidden).SetValue(null,player.Entity.UID);MoveTo(player,new Point(44,44));MoveTo(rival,new Point(75,75));
        Call(relic,null,"ScoreHolder");Check(scores[player.Entity.UID]==1,"Relic Rush uses normal hold points before the final minute");
        relicNow=until.AddMinutes(-1);Call(relic,null,"ScoreHolder");Check(scores[player.Entity.UID]==3,"final-minute possession earns two points per pulse");
        relicNow=until;Call(relic,null,"ScoreHolder");Check(scores[player.Entity.UID]==3,"expired Relic Rush cannot award extra hold points");
        relicNow=until.AddSeconds(-30);player.Entity.MapID=1002;Call(relic,null,"Pulse");
        Check((uint)relic.GetField("Holder",Hidden).GetValue(null)==0 && arena.Npcs.ContainsKey(RelicRushSystem.RelicNpcId)
            && arena.Npcs[RelicRushSystem.RelicNpcId].X==44 && arena.Npcs[RelicRushSystem.RelicNpcId].Y==44,"departed carrier leaves a visible relic at its last position instead of losing it");
        Call(relic,null,"ResetCycle",false);relic.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.Now));
    }
    static bool SatchelAfterEnd() {return ((IDictionary)Expedition.GetField("Members",Hidden).GetValue(null)).Count==0;}
}
