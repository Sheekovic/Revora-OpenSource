using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using BpTeam = Revora.Game.ConquerStructures.Team;
using KnownPersons = Revora.Database.KnownPersons;

internal static partial class PotencyHuntTests
{
    static readonly Type Sharing = typeof(Entity).Assembly.GetType("Revora.Game.Features.BattlePowerSharing");
    static int SourceBp(GameState p) { return (int)typeof(Entity).GetProperty("SourceBattlePower", Instance).GetValue(p.Entity, null); }
    static void SetBpLevel(GameState p, byte level) { p.Entity.FullyLoaded=false;p.Entity.Level=level;p.Entity.FullyLoaded=true; }
    static void RefreshBp(GameState p) { p.ReviewMentor(); }
    static uint ClanBp(GameState p) { object[] a={p,0u};return (uint)Sharing.GetMethod("ClanAmount",Hidden).Invoke(null,a); }
    static Clan BpClan(params GameState[] players)
    {
        var clan=new Clan(players[0].Entity.UID,players[0].Entity.UID,"BpFixture",players[0].Entity.Name);
        Kernel.Clans[clan.ID]=clan;
        var team=(BpTeam)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(BpTeam));
        team.Lider=players[0];team.Members=new System.Collections.Concurrent.ConcurrentDictionary<uint,BpTeam.Member>();
        foreach(var p in players) {
            p.Entity.ClanId=clan.ID;p.Team=team;
            team.Members[p.Entity.UID]=new BpTeam.Member{entry=p};
            clan.Members[p.Entity.UID]=new ClanMember{Identifier=p.Entity.UID,Name=p.Entity.Name};
        }
        return clan;
    }
    static void VerifyBattlePower(Map map)
    {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE apprentice (LIKE public.apprentice INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Call(EvidenceType,null,"Initialize");Call(Admissions,null,"Initialize");
        var mentor=AdmissionPlayer(map,140);var child=AdmissionPlayer(map,100);MentorLink(mentor,child,600);
        mentor.Entity.ExtraBattlePower=200;child.Entity.ExtraBattlePower=20;
        Check(!child.Fake && child.Account.State==AccountTable.AccountState.Player,"BP sharing uses ordinary authenticated players");
        int own=SourceBp(child),parent=SourceBp(mentor);uint share=(uint)((parent-own)/3.3F);
        RefreshBp(child);
        Check(child.Entity.BattlePower==own+share && child.Entity.MentorBattlePower==share && child.Entity.ExtraBattlePower==20+share
            && child.Entity.BattlePowerFrom(mentor.Entity)==share,
            "mentor share contributes once and preserves independent BP");
        for(int i=0;i<20;i++){RefreshBp(mentor);RefreshBp(child);}
        Check(child.Entity.BattlePower==own+share && child.Entity.ExtraBattlePower==20+share,"repeated mentor refresh cannot amplify bonuses");
        foreach(byte level in new byte[]{124,125,126,134,135,136,140}) {
            SetBpLevel(child,level);uint expected=level>=135?0:(uint)((parent-SourceBp(child))/3.3F);
            if(level>=125 && level<135)expected=expected*(uint)(135-level)/10;
            RefreshBp(child);
            Check(child.Entity.MentorBattlePower==expected && child.Entity.BattlePower==SourceBp(child)+expected,
                "mentor taper has bounded arithmetic at level "+level);
        }
        SetBpLevel(child,100);mentor.Entity.ExtraBattlePower=0;SetBpLevel(mentor,90);RefreshBp(child);
        Check(child.Entity.MentorBattlePower==0 && child.Entity.BattlePower==SourceBp(child),"weaker mentor grants no negative or wrapped BP");
        SetBpLevel(mentor,140);mentor.Entity.ExtraBattlePower=200;RefreshBp(child);share=child.Entity.MentorBattlePower;
        child.Entity.MentorBattlePower=200;child.Entity.ClanSharedBp=500;
        Check(child.Entity.BattlePower==SourceBp(child)+share,"cached share fields cannot inflate authoritative combat BP");
        RefreshBp(child);Check(child.Entity.MentorBattlePower==share && child.Entity.ClanSharedBp==0,"refresh corrects stale share projections");
        child.Entity.ExtraBattlePower=uint.MaxValue;RefreshBp(child);
        Check(child.Entity.ExtraBattlePower==child.Entity.MentorBattlePower,"overflowed independent bonus is rejected");
        child.Entity.ExtraBattlePower=20+child.Entity.MentorBattlePower;mentor.Action=0;
        Check(child.Entity.BattlePower==SourceBp(child),"disconnect invalidates live mentor BP before the next refresh");
        RefreshBp(child);Check(child.Entity.MentorBattlePower==0 && child.Entity.ExtraBattlePower==20,"disconnect clears mentor share without losing independent BP");
        mentor.Action=2;RefreshBp(child);
        foreach(string invalid in new[]{"fake","missing-parent-row","date","child-id","ambiguous"}) {
            var row=mentor.Apprentices[child.Entity.UID];
            if(invalid=="fake")mentor.Fake=true;
            if(invalid=="missing-parent-row")mentor.Apprentices.Remove(child.Entity.UID);
            if(invalid=="date")row.EnroleDate++;
            if(invalid=="child-id")child.AsApprentice.ID++;
            if(invalid=="ambiguous")typeof(GameState).GetField("AmbiguousMentor",Instance).SetValue(child,true);
            RefreshBp(child);Check(child.Entity.MentorBattlePower==0 && child.Entity.BattlePower==SourceBp(child)
                && child.Entity.BattlePowerFrom(mentor.Entity)==0,"mentor eligibility rejects "+invalid);
            mentor.Fake=false;KnownPersons.LoadMentor(mentor);KnownPersons.LoadMentor(child);
        }
        Query("INSERT INTO apprentice SELECT * FROM apprentice WHERE apprenticeid=@uid","uid",(long)child.Entity.UID);
        KnownPersons.LoadMentor(child);RefreshBp(child);
        Check((bool)typeof(GameState).GetField("AmbiguousMentor",Instance).GetValue(child) && child.Entity.MentorBattlePower==0
            && child.Entity.BattlePowerFrom(mentor.Entity)==0 && AdmissionRows(child)==2,"duplicate saved mentor rows are preserved and cannot confer a share");
        var low=AdmissionPlayer(map,100);var high=AdmissionPlayer(map,140);var middle=AdmissionPlayer(map,120);high.Entity.ExtraBattlePower=100;
        var clan=BpClan(low,high,middle);int gap=SourceBp(high)-SourceBp(low);
        foreach(byte tower in new byte[]{0,1,2,3,4}) {
            clan.BPTower=tower;uint expected=(uint)(gap*(30+10*tower)/100);RefreshBp(low);
            Check(low.Entity.ClanSharedBp==expected && low.Entity.BattlePower==SourceBp(low)+expected,
                "strongest nonleader clan teammate supplies tower level "+tower);
        }
        clan.BPTower=5;Check(ClanBp(low)==0,"invalid clan tower grants no share");clan.BPTower=0;
        high.Team.GetClanShareBp(high);
        Check(low.Entity.ClanSharedBp==ClanBp(low) && middle.Entity.ClanSharedBp==ClanBp(middle),
            "production team change hook immediately projects shares to all affected teammates");
        RefreshBp(high);Check(high.Entity.ClanSharedBp==0,"strongest clan member cannot receive its own bonus");
        foreach(string invalid in new[]{"map","team","clan","roster","fake","disconnected","replaced-session"}) {
            var team=high.Team;var member=clan.Members[high.Entity.UID];var slot=team.Members[high.Entity.UID];
            if(invalid=="map")high.Entity.MapID=(ushort)(map.ID+1);
            if(invalid=="team")high.Team=null;
            if(invalid=="clan")high.Entity.ClanId=0;
            if(invalid=="roster")clan.Members.TryRemove(high.Entity.UID,out member);
            if(invalid=="fake")high.Fake=true;
            if(invalid=="disconnected")high.Action=0;
            if(invalid=="replaced-session")team.Members[high.Entity.UID]=new BpTeam.Member{entry=middle};
            uint expected=(uint)((SourceBp(middle)-SourceBp(low))*30/100);
            Check(ClanBp(low)==expected && low.Entity.BattlePower==SourceBp(low)+expected,"clan source immediately excludes "+invalid);
            high.Entity.MapID=map.ID;high.Team=team;high.Entity.ClanId=clan.ID;high.Fake=false;high.Action=2;
            clan.Members[high.Entity.UID]=member;team.Members[high.Entity.UID]=slot;
        }
        var top=AdmissionPlayer(map,140);top.Entity.ExtraBattlePower=200;MentorLink(top,middle,0);MentorLink(middle,low,0);
        uint lowMentor=(uint)((SourceBp(middle)-SourceBp(low))/3.3F);uint clanShare=ClanBp(low);
        Parallel.For(0,40,i=>{RefreshBp(top);RefreshBp(middle);RefreshBp(low);});
        Check(low.Entity.BattlePower==SourceBp(low)+Math.Max(lowMentor,clanShare) && low.Entity.MentorBattlePower==lowMentor,
            "mixed mentor chain and clan refreshes use the higher share without recursively sharing borrowed BP");
        low.Team=null;RefreshBp(low);Check(low.Entity.ClanSharedBp==0 && low.Entity.BattlePower==SourceBp(low)+lowMentor,"leaving team removes clan BP and retains eligible mentor BP");
        low.Entity.GuildSharedBp=999;Check(SourceBp(low)==low.Entity.Level+15,"guild contribution stays capped at fifteen");
        Check(low.Entity.BattlePowerCalc(null)==0 && low.Entity.BattlePowerCalc(new Entity(EntityFlag.Monster,false))==0,
            "ownerless and null entities have no player Battle Power");
        Check(low.Entity.BattlePowerCalc(high.Entity)==high.Entity.BattlePower,"BP calculation uses the requested entity instead of its caller");
        VerifyBpAchievements(map);
    }
    static void VerifyBpAchievements(Map map)
    {
        foreach(int threshold in new[]{100,200,300}) {
            var p=ProgressPlayer(map,"");SetBpLevel(p,100);p.Entity.ExtraBattlePower=(uint)(threshold-100);
            SetBpLevel(p,99);EvidenceRefresh(p);int id=10311+threshold/100;
            Check(!HasAchievement(p,id),"BP achievement stays locked just below "+threshold);
            SetBpLevel(p,100);EvidenceRefresh(p);
            Check(HasAchievement(p,id),"BP achievement unlocks at "+threshold);
            long count=Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id BETWEEN 10312 AND 10314",p.Entity.UID);
            Parallel.For(0,8,i=>EvidenceRefresh(p));SetBpLevel(p,1);p.Entity.ExtraBattlePower=0;
            p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.MyAchievement.Load("");EvidenceRefresh(p);
            Check(HasAchievement(p,id) && count==threshold/100 && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id BETWEEN 10312 AND 10314",p.Entity.UID)==count,
                "earned BP threshold survives lost stats, replay and production reload at "+threshold);
        }
        var source=AdmissionPlayer(map,140);source.Entity.ExtraBattlePower=160;var receiver=ProgressPlayer(map,"");SetBpLevel(receiver,99);
        BpClan(receiver,source);EvidenceRefresh(receiver);
        Check(HasAchievement(receiver,10312) && !HasAchievement(receiver,10313),"eligible clan BP can earn the reached threshold only");
        var equipped=ProgressPlayer(map,"");SetBpLevel(equipped,80);
        var armor=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=130009,Position=3,Plus=12,IsWorn=false,SocketOne=(Enums.Gem)13,SocketTwo=(Enums.Gem)13};
        equipped.Equipment.Objects[2]=armor;EvidenceRefresh(equipped);
        Check(!HasAchievement(equipped,10312),"unworn alternate gear cannot earn the BP threshold");
        armor.IsWorn=true;EvidenceRefresh(equipped);
        Check(equipped.Entity.ExtraBattlePower==0 && equipped.Entity.BattlePower==100 && HasAchievement(equipped,10312),
            "ordinary worn equipment reaches the BP milestone without configured bonuses or GM privilege");
        var combined=ProgressPlayer(map,"");SetBpLevel(combined,100);combined.Entity.ExtraBattlePower=150;
        var teacher=AdmissionPlayer(map,140);teacher.Entity.ExtraBattlePower=200;MentorLink(teacher,combined,0);
        var sharedClan=BpClan(combined,teacher);RefreshBp(combined);EvidenceRefresh(combined);
        Check(combined.Entity.MentorBattlePower==27 && combined.Entity.ClanSharedBp==27 && combined.Entity.BattlePower==277
            && HasAchievement(combined,10313) && !HasAchievement(combined,10314),
            "equal mentor and clan bonuses are used once and cannot fabricate the 300 BP achievement");
        sharedClan.BPTower=4;RefreshBp(combined);
        Check(combined.Entity.BattlePower==313 && combined.Entity.MentorBattlePower==27 && combined.Entity.ClanSharedBp==63,
            "stronger clan share wins while both source projections remain intact");
        teacher.Entity.MapID=(ushort)(map.ID+1);RefreshBp(combined);
        Check(combined.Entity.BattlePower==277 && combined.Entity.MentorBattlePower==27 && combined.Entity.ClanSharedBp==0,
            "mentor share takes over immediately when the stronger clan source leaves the map");
        teacher.Entity.MapID=map.ID;sharedClan.BPTower=0;SetBpLevel(teacher,139);RefreshBp(combined);
        Check(combined.Entity.MentorBattlePower==26 && combined.Entity.ClanSharedBp==26 && combined.Entity.BattlePower==276,
            "both independently rounded shares remain stable below an equal margin");
        var excluded=ProgressPlayer(map,"");SetBpLevel(excluded,99);excluded.Entity.ClanSharedBp=999;excluded.Entity.MentorBattlePower=200;
        EvidenceRefresh(excluded);Check(!HasAchievement(excluded,10312),"forged cached shares cannot unlock a BP achievement");
        SetBpLevel(excluded,140);excluded.Fake=true;EvidenceRefresh(excluded);
        Check(!HasAchievement(excluded,10312),"simulated adventurers cannot earn BP achievements");
        excluded.Fake=false;excluded.Action=0;EvidenceRefresh(excluded);
        Check(!HasAchievement(excluded,10312),"disconnected sessions cannot supply BP observations");
        excluded.Action=2;typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(excluded.Entity,true);EvidenceRefresh(excluded);
        Check(!HasAchievement(excluded,10312),"unresolved persistence cannot supply BP observations");
    }
}
