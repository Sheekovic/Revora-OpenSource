using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type ArenaProof=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaParticipation");
    static GameState ArenaPlayer(Map town) {
        var p=MoonPlayer(town);p.Entity.Level=40;p.ArenaStatistic.Name=p.Entity.Name;p.ArenaStatistic.EntityID=p.Entity.UID;
        p.ArenaStatistic.UID=p.Entity.UID;p.ArenaStatistic.AcceptBox=true;p.ArenaState=Qualifier.Fight;return p;
    }
    static long ArenaReceipts(GameState p) {return Scalar("SELECT count(*) FROM revora_arena_participation WHERE uid=@uid",p.Entity.UID);}
    static void VerifyArenaParticipation(string runtime,Map town) {
        Query("ALTER TABLE entities ADD COLUMN my_title integer DEFAULT 0,ADD COLUMN level integer DEFAULT 140,ADD COLUMN mapid integer DEFAULT 1002,ADD COLUMN x integer DEFAULT 0,ADD COLUMN y integer DEFAULT 0");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");Query("CREATE TABLE npcs (LIKE public.npcs INCLUDING ALL)");
        Query("CREATE TABLE sobnpcs (LIKE public.sobnpcs INCLUDING ALL)");
        Query("CREATE TABLE furniture (LIKE public.furniture INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Map.CreateTimerFactories();Revora.Thread.GenericThreadPool=new System.Threading.StaticPool(1);
        var arena=World(runtime,700);arena.Path="fixture-arena";Map.ArenaBaseFloor=arena.Floor;DMaps.MapPaths[700]=arena.Path;
        Call(ArenaProof,null,"Initialize");
        var a=ArenaPlayer(town);var b=ArenaPlayer(town);var group=new Qualifier.QualifierList.QualifierGroup(a,b);
        Call(ArenaProof,null,"RecordStart",group);
        Check(ArenaReceipts(a)==0 && ArenaReceipts(b)==0,"constructing a match or accepting its invitation alone earns nothing");
        group.Import();
        Check(a.Map.ID==b.Map.ID && a.Map.ID!=700 && a.Map.BaseID==700,"actual Qualifier import places two ordinary level-forty players in their dynamic arena");
        Check(ArenaReceipts(a)==1 && ArenaReceipts(b)==1 && HasAchievement(a,11032) && HasAchievement(b,11032),"successfully started local fight saves both first participation achievements");
        Check(Scalar("SELECT count(*) FROM revora_arena_participation a JOIN revora_arena_participation b ON a.match_id=b.match_id AND a.opponent=b.uid AND b.opponent=a.uid WHERE a.uid=@uid",a.Entity.UID)==1,"both receipts identify the same match and the actual opposing player");
        ushort entered=a.Map.ID;Parallel.For(0,12,i=>{group.Import();Call(ArenaProof,null,"RecordStart",group);});
        Check(a.Map.ID==entered && ArenaReceipts(a)==1 && ArenaReceipts(b)==1,"concurrent import and participation replays neither move the match nor duplicate evidence");
        a.Entity.MyAchievement=new Achievement(a.Entity);a.Entity.MyAchievement.Load("");a.Entity.MyAchievement.Send();
        Check(HasAchievement(a,11032),"ordinary achievement reload retains first Arena participation");
        var spectator=ArenaPlayer(town);group.Watchers.Add(spectator);Call(ArenaProof,null,"RecordStart",group);
        Check(ArenaReceipts(spectator)==0 && !HasAchievement(spectator,11032),"watching a valid fight earns no participation achievement");
        var c=ArenaPlayer(town);a.BindWorldMap(1002);a.QualifierGroup=null;var second=new Qualifier.QualifierList.QualifierGroup(a,c);second.Import();
        Check(ArenaReceipts(a)==1 && ArenaReceipts(c)==1 && HasAchievement(c,11032),"a later opponent earns first participation without rewriting the veteran's receipt");
        var x=ArenaPlayer(town);var y=ArenaPlayer(town);x.Fake=true;var invalid=new Qualifier.QualifierList.QualifierGroup(x,y);invalid.Import();
        Check(ArenaReceipts(x)==0 && ArenaReceipts(y)==0,"a fake competitor cannot establish participation for either side");x.Fake=false;
        x.Entity.Level=39;Call(ArenaProof,null,"RecordStart",invalid);Check(ArenaReceipts(x)==0,"under-level competitors cannot earn a receipt");x.Entity.Level=40;
        y.ArenaStatistic.AcceptBox=false;Call(ArenaProof,null,"RecordStart",invalid);Check(ArenaReceipts(x)==0,"unaccepted match invitation earns nothing");y.ArenaStatistic.AcceptBox=true;
        y.ArenaState=Qualifier.FindMatch;Call(ArenaProof,null,"RecordStart",invalid);Check(ArenaReceipts(x)==0,"queue state is not a started fight");y.ArenaState=Qualifier.Fight;
        invalid.Done=true;Call(ArenaProof,null,"RecordStart",invalid);Check(ArenaReceipts(x)==0,"ended match cannot mint new start evidence");invalid.Done=false;
        Qualifier.QualifierList.Groups.Remove(invalid.ID);Call(ArenaProof,null,"RecordStart",invalid);Check(ArenaReceipts(x)==0,"unregistered match cannot write evidence");Qualifier.QualifierList.Groups[invalid.ID]=invalid;
        y.QualifierGroup=null;Call(ArenaProof,null,"RecordStart",invalid);Check(ArenaReceipts(x)==0,"stale match ownership cannot write evidence");y.QualifierGroup=invalid;
        y.BindWorldMap(1002);Call(ArenaProof,null,"RecordStart",invalid);Check(ArenaReceipts(x)==0,"player outside the assigned arena cannot establish participation");y.BindWorldMap(x.Map.ID);
        Kernel.GamePool.Remove(y.Entity.UID);Call(ArenaProof,null,"RecordStart",invalid);Check(ArenaReceipts(x)==0,"disconnected or superseded session cannot establish participation");Kernel.GamePool[y.Entity.UID]=y;
        Call(ArenaProof,null,"RecordStart",invalid);Check(ArenaReceipts(x)==1 && ArenaReceipts(y)==1,"eligible active competitors can persist valid start evidence");
        var left=ArenaPlayer(town);var right=ArenaPlayer(town);DMaps.MapPaths.Remove(700);
        var failed=new Qualifier.QualifierList.QualifierGroup(left,right);failed.Import();DMaps.MapPaths[700]=arena.Path;
        Check(left.Map.ID==1002 && right.Map.ID==1002 && ArenaReceipts(left)==0 && ArenaReceipts(right)==0,"two failed transfers left in the same city never count as an Arena start");
        var first=ArenaPlayer(town);var last=ArenaPlayer(town);
        foreach(var p in new[]{first,last}) {
            p.TeamArenaStatistic=new TeamQualifierStatistic(true){EntityID=p.Entity.UID};
            Qualifier.ArenaStatistics[p.Entity.UID]=p.ArenaStatistic;
            TeamQualifier.ArenaStatistics[p.Entity.UID]=p.TeamArenaStatistic;
        }
        Query("ALTER TABLE revora_arena_participation ADD CONSTRAINT arena_failure CHECK(uid<opponent) NOT VALID");
        var broken=new Qualifier.QualifierList.QualifierGroup(first,last);broken.Import();
        Check(Fenced(first) && Fenced(last) && ArenaReceipts(first)==0 && ArenaReceipts(last)==0 && !HasAchievement(first,11032),"failure on the second receipt rolls back both players' evidence and fences further actions");
        var cache=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaCacheRecovery");
        Check(new[]{first,last}.All(p=>(bool)Call(cache,null,"RequiresReload",p.ArenaStatistic)
            && (bool)Call(cache,null,"RequiresReload",p.TeamArenaStatistic) && p.Action==0),
            "participation failure marks both solo and team cache references for owned Arena recovery");
        Check(new[]{first,last}.All(p=>!Qualifier.ArenaStatistics.ContainsKey(p.Entity.UID)
            && !TeamQualifier.ArenaStatistics.ContainsKey(p.Entity.UID)),
            "participation failure evicts stale Arena statistics from both shared caches");
        Query("ALTER TABLE revora_arena_participation DROP CONSTRAINT arena_failure");DemonReset(first);DemonReset(last);Call(ArenaProof,null,"RecordStart",broken);
        Check(ArenaReceipts(first)==1 && ArenaReceipts(last)==1,"failed transaction can recover without partial or duplicate participation");
        var self=ArenaPlayer(town);var own=new Qualifier.QualifierList.QualifierGroup(self,self);own.Import();
        Check(ArenaReceipts(self)==0,"a self match never satisfies the competitive participation requirement");
    }
}
