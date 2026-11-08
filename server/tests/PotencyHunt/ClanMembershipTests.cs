using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;
internal static partial class PotencyHuntTests {
    static void ClanWire(GameState p,byte action,uint peer,byte accept=1){
        var frame=new byte[26];Writer.WriteUInt16(18,0,frame);Writer.WriteUInt16(1312,2,frame);frame[4]=action;Writer.WriteUInt32(peer,8,frame);frame[16]=accept;
        Encoding.ASCII.GetBytes("TQClient").CopyTo(frame,18);Clan.Process(frame,p);
    }
    static void VerifyClanMembership(string runtime,Map town){

        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding","enemy"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        RunesSystem.LoadRunesEXP();SpellTable.Load();Call(EvidenceType,null,"Initialize");
        foreach(ushort id in new ushort[]{1011,1020,1000,1004})World(runtime,id);
        ConquerItem.ItemUID=new Counter(7900000);
        Server.GamePool=new GameState[0];



        Query("CREATE TABLE clans AS TABLE public.clans WITH NO DATA");
        Query("CREATE TABLE clanrelation AS TABLE public.clanrelation WITH NO DATA");
        Func<GameState> player=()=>{var p=FullRunePlayer(town,15);p.Entity.DailySignDays=new byte[0];EntityTable.SaveEntity(p);return p;};
        ClanWar.Tournaments=new ClanWar.ClanTournament[0];
        uint next=9000;
        Func<GameState,Clan> found=p=>{while(Kernel.Clans.ContainsKey(next+1))next++;var clan=new Clan(p.Entity.UID,++next,"Clan"+next,p.Entity.Name){LeaderName=p.Entity.Name,Level=1,Announcement="Welcome"};
            p.Entity.ClanId=clan.ID;p.Entity.ClanRank=Clan.Ranks.ClanLeader;p.Entity.ClanName=clan.Name;
            clan.Members.TryAdd(p.Entity.UID,new ClanMember{UID=p.Entity.UID,Identifier=p.Entity.UID,Name=p.Entity.Name,Rank=Clan.Ranks.ClanLeader});Kernel.Clans.Add(clan.ID,clan);
            Query("INSERT INTO clans(identifier,leaderid,name,leadername,fund,level,bptower,announcement,claimed) VALUES(@id,@leader,@name,@leadername,0,1,0,'Welcome',0)","id",(long)clan.ID,"leader",(long)p.Entity.UID,"name",clan.Name,"leadername",p.Entity.Name);
            EntityTable.UpdateClanID(p);EntityTable.UpdateClanRank(p);return clan;};
        foreach(bool recruit in new[]{true,false}){
            var leader=player();var member=player();var clan=found(leader);
            ClanWire(recruit?leader:member,recruit?(byte)9:(byte)11,recruit?member.Entity.UID:leader.Entity.UID);
            Check(member.Entity.ClanId==0 && !HasAchievement(member,10304),"clan request alone cannot join or earn credit");
            Parallel.For(0,12,n=>ClanWire(recruit?member:leader,recruit?(byte)10:(byte)12,recruit?leader.Entity.UID:member.Entity.UID));
            Check(!Fenced(member) && member.Entity.ClanId==clan.ID && clan.Members.ContainsKey(member.Entity.UID) && HasAchievement(member,10304),"mutually accepted membership commits through clan packet handler: "+recruit);
            Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10304",member.Entity.UID)==1,"acceptance replay earns one membership receipt");
            member=ReloadBall(member);member.Entity.MyAchievement.Send();Check(member.Entity.ClanId==clan.ID && member.Entity.ClanRank==Clan.Ranks.Member && HasAchievement(member,10304),"clan membership and achievement survive character reload");
        }
        foreach(string mode in new[]{"unsolicited","remote","expired","replacement","already-member","revoked","declined"}){
            var leader=player();var member=player();var clan=found(leader);
            if(mode!="unsolicited")ClanWire(leader,9,member.Entity.UID);
            if(mode=="remote")member.Entity.X+=30;
            var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.ClanMembership",true);
            if(mode=="expired")service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow.AddMinutes(3)));
            if(mode=="replacement")member=ReloadBall(member);
            if(mode=="already-member")member.Entity.ClanId=999;if(mode=="revoked")leader.Entity.ClanRank=Clan.Ranks.Member;
            try{ClanWire(member,10,leader.Entity.UID,mode=="declined"?(byte)0:(byte)1);if(mode=="declined")ClanWire(member,10,leader.Entity.UID);}
            finally{service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
            Check(!clan.Members.ContainsKey(member.Entity.UID) && !HasAchievement(member,10304),"invalid clan acceptance does not join: "+mode);
        }
        foreach(string table in new[]{"entities","revora_achievement_evidence"}){
            var leader=player();var member=player();var clan=found(leader);ClanWire(leader,9,member.Entity.UID);
            Query("CREATE FUNCTION suppress_clan_join() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_clan_join BEFORE INSERT OR UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_clan_join()");
            try{ClanWire(member,10,leader.Entity.UID);}finally{Query("DROP TRIGGER suppress_clan_join ON "+table+";DROP FUNCTION suppress_clan_join()");}
            Check(Fenced(member) && !clan.Members.ContainsKey(member.Entity.UID) && Scalar("SELECT clanid FROM entities WHERE uid=@uid",member.Entity.UID)==0,"failed membership transaction rolls back both state and credit: "+table);
            member=ReloadBall(member);member.Entity.MyAchievement.Send();Check(member.Entity.ClanId==0 && !HasAchievement(member,10304),"failed clan acceptance remains unearned after reload");
        }
        foreach(string malformed in new[]{"short","length","seal","oversize","peer"}){
            var leader=player();var member=player();var clan=found(leader);ClanWire(leader,9,member.Entity.UID);
            var frame=new byte[malformed=="short"?17:malformed=="oversize"?1033:26];Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1312,2,frame);frame[4]=10;Writer.WriteUInt32(malformed=="peer"?uint.MaxValue:leader.Entity.UID,8,frame);frame[16]=1;
            if(frame.Length>=26)Encoding.ASCII.GetBytes("TQClient").CopyTo(frame,frame.Length-8);
            if(malformed=="length")frame[0]++;if(malformed=="seal")frame[frame.Length-1]=0;Clan.Process(frame,member);
            Check(member.Entity.ClanId==0 && !HasAchievement(member,10304),"malformed or unknown-peer clan acceptance grants nothing: "+malformed);
        }
        var left=player();var right=player();var applicant=player();var leftClan=found(left);var rightClan=found(right);
        ClanWire(applicant,11,left.Entity.UID);
        var clockField=typeof(Entity).Assembly.GetType("Revora.Game.Features.ClanMembership",true).GetField("Clock",Hidden);
        clockField.SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow.AddSeconds(2)));try{ClanWire(applicant,11,right.Entity.UID);}finally{clockField.SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
        Parallel.Invoke(()=>ClanWire(left,12,applicant.Entity.UID),()=>ClanWire(right,12,applicant.Entity.UID));
        Check(!Fenced(applicant) && (leftClan.Members.ContainsKey(applicant.Entity.UID)?1:0)+(rightClan.Members.ContainsKey(applicant.Entity.UID)?1:0)==1
            && Scalar("SELECT clanid FROM entities WHERE uid=@uid",applicant.Entity.UID)==applicant.Entity.ClanId,"concurrent accepts into different clans save exactly one membership");
        NpcSpawn registrar;
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT cellx,celly,lookface FROM public.npcs WHERE id=9450 AND mapid=1002",c))
        using(var r=q.ExecuteReader()){if(!r.Read())throw new Exception("Clan registrar missing.");registrar=new NpcSpawn{UID=9450,MapID=1002,X=Convert.ToUInt16(r[0]),Y=Convert.ToUInt16(r[1]),Mesh=Convert.ToUInt16(r[2]),Type=Enums.NpcType.Talker};}}
        town.AddNpc(registrar);
        Func<GameState> founder=()=>{var p=player();p.Entity.Money=Config.ClanCreateGold+12345;EntityTable.SaveEntity(p);p=ReloadBall(p);p.BindWorldMap(1002);p.Entity.X=(ushort)(registrar.X-1);p.Entity.Y=registrar.Y;p.ActiveNpc=9450;return p;};
        Action<GameState,string> create=(p,name)=>{var bytes=Encoding.Default.GetBytes(name);var frame=new byte[28+bytes.Length];Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(2032,2,frame);Writer.WriteUInt32(9450,8,frame);frame[14]=8;frame[17]=(byte)bytes.Length;bytes.CopyTo(frame,18);Encoding.ASCII.GetBytes("TQClient").CopyTo(frame,frame.Length-8);PacketHandler.HandlePacket(frame,p);};
        var owner=founder();create(owner,"FoundedClan");
        Check(!Fenced(owner) && owner.Entity.ClanId!=0 && owner.Entity.ClanRank==Clan.Ranks.ClanLeader && owner.Entity.Money==12345 && HasAchievement(owner,10304),"ordinary NPC name submission atomically founds a clan for one configured fee");
        uint founded=owner.Entity.ClanId;create(owner,"AnotherClan");
        Check(owner.Entity.ClanId==founded && owner.Entity.Money==12345 && owner.Entity.GetClan.Fund==Config.ClanCreateFund,"founding replay cannot charge twice and preserves configured clan fund");
        owner=ReloadBall(owner);owner.Entity.MyAchievement.Send();Check(owner.Entity.ClanId==founded && owner.Entity.ClanRank==Clan.Ranks.ClanLeader && owner.Entity.Money==12345 && HasAchievement(owner,10304),"founder membership, payment and achievement survive character reload");
        var newMember=player();newMember.Entity.X=owner.Entity.X;newMember.Entity.Y=owner.Entity.Y;ClanWire(owner,9,newMember.Entity.UID);ClanWire(newMember,10,owner.Entity.UID);
        Check(newMember.Entity.ClanId==founded && HasAchievement(newMember,10304),"NPC-founded clan admits another ordinary player through mutual invitation");
        foreach(string table in new[]{"clans","entities","revora_achievement_evidence","fee"}){
            var p=founder();ulong money=p.Entity.Money;
            string target=table=="fee"?"entities":table;
            Query("CREATE FUNCTION suppress_clan_founder() RETURNS trigger LANGUAGE plpgsql AS '"+(table=="fee"?"BEGIN IF NEW.money<OLD.money THEN RETURN NULL; END IF; RETURN NEW; END":"BEGIN RETURN NULL; END")+"'");
            Query("CREATE TRIGGER suppress_clan_founder BEFORE INSERT OR UPDATE ON "+target+" FOR EACH ROW EXECUTE FUNCTION suppress_clan_founder()");
            try{create(p,"Fail"+table.Length);}finally{Query("DROP TRIGGER suppress_clan_founder ON "+target+";DROP FUNCTION suppress_clan_founder()");}
            Check(Fenced(p) && Scalar("SELECT count(*) FROM clans WHERE leaderid=@uid",p.Entity.UID)==0 && Scalar("SELECT clanid FROM entities WHERE uid=@uid",p.Entity.UID)==0
                && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)money,"failed founding rolls back clan, member and fee: "+table);
            p=ReloadBall(p);p.Entity.MyAchievement.Send();Check(!HasAchievement(p,10304),"failed founding creates no achievement after reload");
        }
        foreach(string reason in new[]{"poor","level","remote","name"}){
            var p=founder();if(reason=="poor")p.Entity.Money=0;if(reason=="level")p.Entity.Level=(byte)(Config.ClanCreateMinLevel-1);if(reason=="remote")p.Entity.X+=30;
            create(p,reason=="name"?"A":"Denied"+reason);Check(p.Entity.ClanId==0 && !HasAchievement(p,10304),"founder rejects invalid eligibility: "+reason);
        }
        var rivalOne=founder();var rivalTwo=founder();Parallel.Invoke(()=>create(rivalOne,"SameClan"),()=>create(rivalTwo,"sameclan"));
        Check((rivalOne.Entity.ClanId!=0?1:0)+(rivalTwo.Entity.ClanId!=0?1:0)==1 && (rivalOne.Entity.Money==12345?1:0)+(rivalTwo.Entity.Money==12345?1:0)==1,"concurrent case-insensitive clan names create and charge exactly once");
        var captain=player();var group=found(captain);
        for(int i=0;i<10;i++){var filler=player();filler.Entity.ClanId=group.ID;EntityTable.UpdateClanID(filler);group.Members.TryAdd(filler.Entity.UID,new ClanMember{UID=filler.Entity.UID,Identifier=filler.Entity.UID});}
        var one=player();var two=player();ClanWire(captain,9,one.Entity.UID);
        var clock=typeof(Entity).Assembly.GetType("Revora.Game.Features.ClanMembership",true).GetField("Clock",Hidden);
        clock.SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow.AddSeconds(2)));try{ClanWire(captain,9,two.Entity.UID);}finally{clock.SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
        Parallel.Invoke(()=>ClanWire(one,10,captain.Entity.UID),()=>ClanWire(two,10,captain.Entity.UID));
        Check(group.Members.Count==12 && (one.Entity.ClanId==group.ID?1:0)+(two.Entity.ClanId==group.ID?1:0)==1,"competing final-slot acceptances admit exactly one player");
        Action<GameState,byte,string> depart=(p,action,name)=>{var bytes=Encoding.Default.GetBytes(name);var frame=new byte[26+bytes.Length];Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1312,2,frame);frame[4]=action;Writer.WriteUInt32(p.Entity.UID,8,frame);frame[17]=(byte)bytes.Length;bytes.CopyTo(frame,18);Encoding.ASCII.GetBytes("TQClient").CopyTo(frame,frame.Length-8);Clan.Process(frame,p);};
        Action<GameState,GameState> join=(leader,member)=>{ClanWire(member,11,leader.Entity.UID);ClanWire(leader,12,member.Entity.UID);};
        foreach(string mode in new[]{"quit","kick","offline"}){
            var chief=player();var guest=player();var clan=found(chief);join(chief,guest);uint uid=guest.Entity.UID;
            Check(guest.Entity.ClanId==clan.ID,"departure fixture joins through mutual consent: "+mode);
            if(mode=="offline")Kernel.GamePool.Remove(uid);
            depart(mode=="quit"?guest:chief,mode=="quit"?(byte)23:(byte)22,mode=="quit"?"":guest.Entity.Name);
            Check(!clan.Members.ContainsKey(uid) && Scalar("SELECT clanid FROM entities WHERE uid=@uid",uid)==0 && Scalar("SELECT clanrank FROM entities WHERE uid=@uid",uid)==0
                && Scalar("SELECT clandonation FROM entities WHERE uid=@uid",uid)==0 && chief.Entity.ClanId==clan.ID,"departure clears only intended member and preserves leader: "+mode);
            if(mode!="offline")Check(guest.Entity.ClanId==0 && guest.Entity.ClanRank==Clan.Ranks.None && guest.Entity.ClanSharedBp==0,"online departure updates member state immediately: "+mode);
            guest=ReloadBall(guest);guest.Entity.MyAchievement.Send();Check(guest.Entity.ClanId==0 && HasAchievement(guest,10304),"departure survives reload without erasing earned membership achievement: "+mode);
            depart(chief,22,guest.Entity.Name);Check(chief.Entity.ClanId==clan.ID,"replayed kick cannot remove the leader: "+mode);
        }
        foreach(string invalid in new[]{"member-kick","outsider","leader-quit","foreign-member","self-kick"}){
            var chief=player();var guest=player();var clan=found(chief);join(chief,guest);var stranger=player();
            if(invalid=="member-kick")depart(guest,22,chief.Entity.Name);if(invalid=="outsider")depart(stranger,22,guest.Entity.Name);
            if(invalid=="leader-quit")depart(chief,23,"");if(invalid=="foreign-member")depart(chief,22,stranger.Entity.Name);if(invalid=="self-kick")depart(chief,22,chief.Entity.Name);
            Check(chief.Entity.ClanId==clan.ID && guest.Entity.ClanId==clan.ID && clan.Members.Count==2,"unauthorized clan removal changes nothing: "+invalid);
        }
        var failedChief=player();var failedGuest=player();var failedClan=found(failedChief);join(failedChief,failedGuest);
        Query("CREATE FUNCTION suppress_clan_departure() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.clanid=0 AND OLD.clanid<>0 THEN RETURN NULL; END IF; RETURN NEW; END'");
        Query("CREATE TRIGGER suppress_clan_departure BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_clan_departure()");
        try{depart(failedChief,22,failedGuest.Entity.Name);}finally{Query("DROP TRIGGER suppress_clan_departure ON entities;DROP FUNCTION suppress_clan_departure()");}
        Check(Fenced(failedChief) && Fenced(failedGuest) && failedClan.Members.ContainsKey(failedGuest.Entity.UID)
            && Scalar("SELECT clanid FROM entities WHERE uid=@uid",failedGuest.Entity.UID)==failedClan.ID,"failed departure preserves durable and live membership");
        failedGuest=ReloadBall(failedGuest);Check(failedGuest.Entity.ClanId==failedClan.ID,"failed departure remains joined after reload");
        var oldChief=player();var newChief=player();var mover=player();var oldClan=found(oldChief);var newClan=found(newChief);
        ClanWire(mover,11,oldChief.Entity.UID);
        clockField.SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow.AddSeconds(2)));try{join(newChief,mover);}finally{clockField.SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
        depart(mover,23,"");ClanWire(oldChief,12,mover.Entity.UID);
        Check(mover.Entity.ClanId==0 && !oldClan.Members.ContainsKey(mover.Entity.UID),"joining and leaving another clan invalidates an older pending application");
        var busyChief=player();var busyGuest=player();var busyClan=found(busyChief);join(busyChief,busyGuest);
        var install=typeof(Entity).Assembly.GetType("Revora.Game.Features.SessionInstall",true);
        var pendingBody=busyGuest.Entity;busyGuest.Entity=null;
        try{depart(busyChief,22,pendingBody.Name);}finally{busyGuest.Entity=pendingBody;}
        Check(!Fenced(busyChief) && busyClan.Members.ContainsKey(busyGuest.Entity.UID),"partially loaded online character is rejected without changing or fencing the leader");
        Call(install,null,"Enter",busyGuest.Entity.UID);
        try{var attempt=Task.Run(()=>depart(busyChief,22,busyGuest.Entity.Name));Check(attempt.Wait(TimeSpan.FromSeconds(5)) && busyGuest.Entity.ClanId==busyClan.ID,"busy login lease rejects removal promptly without changing membership");}
        finally{Call(install,null,"Exit",busyGuest.Entity.UID);}
        depart(busyChief,22,busyGuest.Entity.Name);Check(busyGuest.Entity.ClanId==0,"removal retries successfully once the login lease is released");
        var malformedChief=player();var malformedGuest=player();var malformedClan=found(malformedChief);join(malformedChief,malformedGuest);
        var malformedKick=new byte[26];Writer.WriteUInt16(18,0,malformedKick);Writer.WriteUInt16(1312,2,malformedKick);malformedKick[4]=22;malformedKick[17]=250;Encoding.ASCII.GetBytes("TQClient").CopyTo(malformedKick,18);Clan.Process(malformedKick,malformedChief);
        Check(malformedGuest.Entity.ClanId==malformedClan.ID && malformedClan.Members.Count==2,"oversized kick name is rejected before lookup or mutation");
        Query("UPDATE entities SET clanrank=10 WHERE uid=@uid","uid",(long)malformedChief.Entity.UID);depart(malformedChief,22,malformedGuest.Entity.Name);
        Check(malformedGuest.Entity.ClanId==malformedClan.ID && Scalar("SELECT clanid FROM entities WHERE uid=@uid",malformedGuest.Entity.UID)==malformedClan.ID,"stale in-memory leader rank cannot override durable authority");
        var allyBoss=player();var allied=found(allyBoss);
        var dissolver=founder();create(dissolver,"DissolveMe");uint retired=dissolver.Entity.ClanId;var dissolving=dissolver.Entity.GetClan;
        var remaining=player();var absent=player();remaining.Entity.X=absent.Entity.X=dissolver.Entity.X;remaining.Entity.Y=absent.Entity.Y=dissolver.Entity.Y;join(dissolver,remaining);join(dissolver,absent);
        allied.Allies.Add(retired,dissolving);dissolving.Allies.Add(allied.ID,allied);
        Query("INSERT INTO clanrelation(clanid,associatedid,type) VALUES(@clan,@peer,1),(@peer,@clan,1)","clan",(long)retired,"peer",(long)allied.ID);
        Kernel.GamePool.Remove(absent.Entity.UID);ulong beforeDisband=dissolver.Entity.Money;
        SoulWire(dissolver,19,9450);Check(dissolver.Entity.ClanId==retired,"unsolicited disband confirmation does nothing");
        SoulWire(dissolver,9,9450);SoulWire(dissolver,255,9450);SoulWire(dissolver,19,9450);Check(dissolver.Entity.ClanId==retired,"cancelled disband confirmation cannot be replayed");
        var relationView=allied.Allies.GetEnumerator();relationView.MoveNext();
        SoulWire(dissolver,9,9450);Parallel.For(0,12,n=>SoulWire(dissolver,19,9450));
        while(relationView.MoveNext()){}relationView.Dispose();Check(true,"relation readers tolerate concurrent disband link removal");
        Check(!Fenced(dissolver) && dissolver.Entity.ClanId==0 && remaining.Entity.ClanId==0 && !Kernel.Clans.ContainsKey(retired)
            && Scalar("SELECT count(*) FROM clans WHERE identifier=@uid",retired)==0 && Scalar("SELECT count(*) FROM entities WHERE clanid=@uid",retired)==0,"confirmed disband clears all online and offline memberships and deletes exactly the intended clan");
        Check(!allied.Allies.ContainsKey(retired) && Scalar("SELECT count(*) FROM clanrelation WHERE clanid=@uid OR associatedid=@uid",retired)==0
            && Kernel.Clans.ContainsKey(allied.ID),"disband clears both directions of clan links without deleting the allied clan");
        absent=ReloadBall(absent);absent.Entity.MyAchievement.Send();Check(absent.Entity.ClanId==0 && HasAchievement(absent,10304) && dissolver.Entity.Money==beforeDisband,"offline member reload preserves earned credit and disband does not invent a fund refund");
        World(runtime,1505);remaining.BindWorldMap(1505);var tournament=new ClanWar.ClanTournament(ClanWar.ClanArena.TwinCityClan);
        uint departureCp=remaining.Entity.ConquerPoints;uint departureRvp=remaining.Entity.RevoraPoint;
        Call(typeof(ClanWar.ClanTournament),tournament,"GetClientReward",remaining,dissolving);
        Check(!Fenced(remaining) && remaining.Entity.ConquerPoints==departureCp && remaining.Entity.RevoraPoint==departureRvp,"departed member in a Clan War winner snapshot receives no currency or missing-clan exception");
        newMember.BindWorldMap(1505);uint idleCp=newMember.Entity.ConquerPoints;Call(typeof(ClanWar.ClanTournament),tournament,"Work",0);
        Check(newMember.Entity.ConquerPoints==idleCp && newMember.Entity.MapID==1505,"closed Clan War callback cannot award or teleport a member");
        var successor=founder();create(successor,"NextClan");Check(successor.Entity.ClanId>retired,"a retired highest clan ID is never reused by later founding");
        foreach(string table in new[]{"entities","clans","clanrelation"}){
            var chief=founder();var clan=found(chief);var guest=player();guest.Entity.X=chief.Entity.X;guest.Entity.Y=chief.Entity.Y;join(chief,guest);
            Query("INSERT INTO clanrelation(clanid,associatedid,type) VALUES(@clan,@peer,1)","clan",(long)clan.ID,"peer",(long)allied.ID);
            SoulWire(chief,9,9450);
            Query("CREATE FUNCTION suppress_disband() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_disband BEFORE "+(table=="entities"?"UPDATE":"DELETE")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_disband()");
            try{SoulWire(chief,19,9450);}finally{Query("DROP TRIGGER suppress_disband ON "+table+";DROP FUNCTION suppress_disband()");}
            Check(Fenced(chief) && Kernel.Clans.ContainsKey(clan.ID) && Scalar("SELECT count(*) FROM entities WHERE clanid=@uid",clan.ID)==2
                && Scalar("SELECT count(*) FROM clans WHERE identifier=@uid",clan.ID)==1 && Scalar("SELECT count(*) FROM clanrelation WHERE clanid=@uid",clan.ID)==1,"failed disband rolls back all memberships, clan and relation rows: "+table);
            chief=ReloadBall(chief);guest=ReloadBall(guest);Check(chief.Entity.ClanId==clan.ID && guest.Entity.ClanId==clan.ID,"failed disband remains joined after character reload");
        }
        foreach(string invalid in new[]{"member","remote","expired","replacement","durable-rank"}){
            var chief=founder();var clan=found(chief);var guest=player();guest.Entity.X=chief.Entity.X;guest.Entity.Y=chief.Entity.Y;guest.ActiveNpc=9450;join(chief,guest);
            var clockDisband=typeof(Entity).Assembly.GetType("Revora.Game.Features.ClanDisbanding",true).GetField("Clock",Hidden);
            SoulWire(invalid=="member"?guest:chief,9,9450);if(invalid=="remote")chief.Entity.X+=30;
            if(invalid=="expired")clockDisband.SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow.AddMinutes(3)));
            if(invalid=="replacement"){chief=ReloadBall(chief);chief.ActiveNpc=9450;}
            if(invalid=="durable-rank")Query("UPDATE entities SET clanrank=10 WHERE uid=@uid","uid",(long)chief.Entity.UID);
            try{SoulWire(invalid=="member"?guest:chief,19,9450);}finally{clockDisband.SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
            Check(Kernel.Clans.ContainsKey(clan.ID) && Scalar("SELECT count(*) FROM entities WHERE clanid=@uid",clan.ID)==2,"disband rejects invalid authority or consent: "+invalid);
        }
        var transferChief=founder();var transferClan=found(transferChief);var heir=player();var formerSpouse=player();var nextSpouse=player();
        foreach(var member in new[]{heir,formerSpouse,nextSpouse}){member.Entity.X=transferChief.Entity.X;member.Entity.Y=transferChief.Entity.Y;join(transferChief,member);}
        Query("UPDATE entities SET spouse=@name,clanrank=11 WHERE uid=@uid","name",transferChief.Entity.Name,"uid",(long)formerSpouse.Entity.UID);
        Query("UPDATE entities SET spouse=@name WHERE uid=@uid","name",heir.Entity.Name,"uid",(long)nextSpouse.Entity.UID);
        Kernel.GamePool.Remove(formerSpouse.Entity.UID);Kernel.GamePool.Remove(nextSpouse.Entity.UID);
        SoulWire(transferChief,9,9450);
        var invitee=player();invitee.Entity.X=transferChief.Entity.X;invitee.Entity.Y=transferChief.Entity.Y;
        ClanWire(transferChief,9,invitee.Entity.UID);
        depart(transferChief,21,heir.Entity.Name);
        Check(!Fenced(transferChief) && !Fenced(heir) && transferClan.LeaderId==heir.Entity.UID && transferClan.LeaderName==heir.Entity.Name
            && transferChief.Entity.ClanRank==Clan.Ranks.Member && heir.Entity.ClanRank==Clan.Ranks.ClanLeader,"ordinary leadership transfer updates both leaders and clan identity");
        Check(Scalar("SELECT leaderid FROM clans WHERE identifier=@uid",transferClan.ID)==heir.Entity.UID
            && Scalar("SELECT count(*) FROM entities WHERE clanid=@uid AND clanrank=100",transferClan.ID)==1
            && Scalar("SELECT clanrank FROM entities WHERE uid=@uid",formerSpouse.Entity.UID)==10
            && Scalar("SELECT clanrank FROM entities WHERE uid=@uid",nextSpouse.Entity.UID)==11,"transfer atomically saves one leader and existing spouse-name rank rules for offline members");
        depart(transferChief,21,formerSpouse.Entity.Name);Check(transferClan.LeaderId==heir.Entity.UID,"old leader cannot replay transfer authority");
        depart(heir,21,transferChief.Entity.Name);
        SoulWire(transferChief,19,9450);ClanWire(invitee,10,transferChief.Entity.UID);
        Check(Kernel.Clans.ContainsKey(transferClan.ID) && invitee.Entity.ClanId==0,"transfer away and back invalidates old disband and recruitment offers");
        depart(transferChief,21,heir.Entity.Name);
        transferChief=ReloadBall(transferChief);heir=ReloadBall(heir);formerSpouse=ReloadBall(formerSpouse);nextSpouse=ReloadBall(nextSpouse);
        Check(transferChief.Entity.ClanRank==Clan.Ranks.Member && heir.Entity.ClanRank==Clan.Ranks.ClanLeader
            && formerSpouse.Entity.ClanRank==Clan.Ranks.Member && nextSpouse.Entity.ClanRank==Clan.Ranks.Spouse,"transferred ranks survive actual character reload");
        foreach(string invalid in new[]{"member","self","foreign","offline","saved-rank","saved-leader"}){
            var chief=player();var clan=found(chief);var guest=player();join(chief,guest);var outsider=player();
            if(invalid=="offline")Kernel.GamePool.Remove(guest.Entity.UID);
            if(invalid=="saved-rank")Query("UPDATE entities SET clanrank=10 WHERE uid=@uid","uid",(long)chief.Entity.UID);
            if(invalid=="saved-leader")Query("UPDATE clans SET leaderid=@next WHERE identifier=@uid","next",(long)guest.Entity.UID,"uid",(long)clan.ID);
            depart(invalid=="member"?guest:chief,21,invalid=="self"?chief.Entity.Name:invalid=="foreign"?outsider.Entity.Name:guest.Entity.Name);
            Check(clan.LeaderId==chief.Entity.UID && chief.Entity.ClanRank==Clan.Ranks.ClanLeader && guest.Entity.ClanRank==Clan.Ranks.Member,"transfer rejects invalid authority or recipient: "+invalid);
        }
        foreach(string table in new[]{"clans","entities"}){
            var chief=player();var clan=found(chief);var guest=player();join(chief,guest);
            Query("CREATE FUNCTION suppress_transfer() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_transfer BEFORE UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_transfer()");
            try{depart(chief,21,guest.Entity.Name);}finally{Query("DROP TRIGGER suppress_transfer ON "+table+";DROP FUNCTION suppress_transfer()");}
            Check(Fenced(chief) && Fenced(guest) && clan.LeaderId==chief.Entity.UID
                && Scalar("SELECT leaderid FROM clans WHERE identifier=@uid",clan.ID)==chief.Entity.UID
                && Scalar("SELECT clanrank FROM entities WHERE uid=@uid",chief.Entity.UID)==100
                && Scalar("SELECT clanrank FROM entities WHERE uid=@uid",guest.Entity.UID)==10,"suppressed transfer write rolls back clan identity and member ranks: "+table);
        }
        var racingChief=player();var racingClan=found(racingChief);var firstHeir=player();var secondHeir=player();join(racingChief,firstHeir);join(racingChief,secondHeir);
        Parallel.Invoke(()=>depart(racingChief,21,firstHeir.Entity.Name),()=>depart(racingChief,21,secondHeir.Entity.Name));
        Check((racingClan.LeaderId==firstHeir.Entity.UID || racingClan.LeaderId==secondHeir.Entity.UID)
            && Scalar("SELECT count(*) FROM entities WHERE clanid=@uid AND clanrank=100",racingClan.ID)==1,"competing transfers select one durable leader");
        var warResults=typeof(Entity).Assembly.GetType("Revora.Game.Features.ClanWarResults",true);Call(warResults,null,"Initialize");
        Check(Scalar("SELECT count(*) FROM public.npcs WHERE id IN (101895,101897,101901,101902,101905,101906,101913,101914,101909,101910) AND type=31",0)==10,"all ten existing Clan War envoys are installed with the dedicated clan-window NPC type");
        var warClock=warResults.GetField("Clock",Hidden);DateTime warNow=new DateTime(2026,9,25,3,33,0,DateTimeKind.Utc);
        warClock.SetValue(null,(Func<DateTime>)(()=>warNow));
        Action<GameState,byte,uint> warWire=(p,action,npc)=>{var frame=new byte[148];Writer.WriteUInt16(140,0,frame);Writer.WriteUInt16(1313,2,frame);frame[4]=action;Writer.WriteUInt32(npc,12,frame);Encoding.ASCII.GetBytes("TQClient").CopyTo(frame,140);ClanWar.Process(frame,p);};
        ClanWar.Tournaments=new ClanWar.ClanTournament[10];for(int index=0;index<10;index++)ClanWar.Tournaments[index]=new ClanWar.ClanTournament((ClanWar.ClanArena)index);
        var unrelated=player();var combatGate=typeof(Entity).Assembly.GetType("Revora.Game.Features.TournamentCombat",true).GetField("Gate",Hidden).GetValue(null);
        lock(combatGate){var recovery=Task.Run(()=>Call(typeof(ClanWar),null,"RecoverLocation",unrelated));Check(recovery.Wait(TimeSpan.FromSeconds(5)),"ordinary players outside clan arenas never wait on the tournament combat gate for recovery");}
        try{
            int[] arenas={0,3,4,2,1,5,6,7,8,9};uint[] envoys={101895,101913,101909,101905,101901,101897,101902,101906,101914,101910};
            for(int index=0;index<arenas.Length;index++){
                var arena=(ClanWar.ClanArena)arenas[index];ushort arenaId=ClanWar.GetMap(arena);
                ushort terrain=checked((ushort)Scalar("SELECT mapdoc FROM public.maps WHERE id=@uid",arenaId));
                Check(terrain!=0,"Clan arena has an installed terrain identity: "+arena);
                Map previousMap;Kernel.Maps.TryGetValue(terrain,out previousMap);
                MapsTable.MapInformation previousInfo;bool hadInfo=MapsTable.MapInformations.TryGetValue(terrain,out previousInfo);
                var field=World(runtime,terrain);field.ID=arenaId;field.BaseID=terrain;Kernel.Maps[arenaId]=field;
                if(terrain!=arenaId){
                    if(previousMap!=null)Kernel.Maps[terrain]=previousMap;else Kernel.Maps.Remove(terrain);
                    if(hadInfo)MapsTable.MapInformations[terrain]=previousInfo;else MapsTable.MapInformations.Remove(terrain);
                }
                MapsTable.MapInformations[arenaId]=new MapsTable.MapInformation{ID=arenaId,BaseID=terrain,Status=2};
                var war=ClanWar.Tournaments[(int)arena];
                var chief=player();var clan=found(chief);var guest=player();join(chief,guest);var spectator=player();join(chief,spectator);
                uint npc=envoys[index];bool city=index<5;
                if(city)town.AddNpc(new NpcSpawn{UID=npc,MapID=1002,X=chief.Entity.X,Y=chief.Entity.Y,Mesh=310,Type=Enums.NpcType.ClanInfo});
                else {
                    NpcSpawn envoy;
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT mapid,cellx,celly,lookface,type FROM public.npcs WHERE id=@id",c)){
                        q.Parameters.AddWithValue("id",(long)npc);using(var r=q.ExecuteReader()){
                            if(!r.Read())throw new Exception("Installed wilderness envoy is missing.");
                            envoy=new NpcSpawn{UID=npc,MapID=Convert.ToUInt16(r[0]),X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),Mesh=Convert.ToUInt16(r[3]),Type=(Enums.NpcType)Convert.ToByte(r[4])};
                        }
                    }}
                    Map origin;if(!Kernel.Maps.TryGetValue(envoy.MapID,out origin))origin=World(runtime,envoy.MapID);
                    Check(envoy.Type==Enums.NpcType.ClanInfo && envoy.X<origin.Floor.Bounds.Width && envoy.Y<origin.Floor.Bounds.Height,
                        "wilderness envoy uses installed type, map and in-bounds coordinates: "+arena);
                    origin.AddNpc(envoy);bool near=false;ushort x=0,y=0;
                    for(int dx=-3;dx<=3 && !near;dx++)for(int dy=-3;dy<=3 && !near;dy++){
                        int nx=envoy.X+dx,ny=envoy.Y+dy;
                        if(nx>=0 && ny>=0 && nx<origin.Floor.Bounds.Width && ny<origin.Floor.Bounds.Height && !origin.Floor[nx,ny,MapObjectType.InvalidCast]){x=(ushort)nx;y=(ushort)ny;near=true;}
                    }
                    Check(near,"real terrain provides a walkable interaction tile at the installed wilderness envoy: "+arena);
                    foreach(var p in new[]{chief,guest}){p.BindWorldMap(envoy.MapID);p.Entity.X=x;p.Entity.Y=y;}
                }
                warNow=warNow.Date.AddDays(1).AddHours(3).AddMinutes(33);DateTime start=warNow;Call(typeof(ClanWar.ClanTournament),war,"Begin",start,20261001+index,false);
                uint beforeCp=chief.Entity.ConquerPoints,beforeRvp=chief.Entity.RevoraPoint,memberCp=guest.Entity.ConquerPoints;
                foreach(var p in new[]{chief,guest}){warWire(p,6,npc);warWire(p,8,npc);}
                Check(chief.Entity.MapID==ClanWar.GetMap(arena) && guest.Entity.MapID==ClanWar.GetMap(arena),"ordinary clan members enter through the territory envoy packet: "+arena);
                Call(typeof(ClanWar.ClanTournament),war,"Work",0);Check(war.Open && (!city || !HasAchievement(chief,10305+index)),"territory cannot settle before the twenty-minute boundary");
                warNow=start.AddMinutes(20);Parallel.For(0,6,n=>Call(typeof(ClanWar.ClanTournament),war,"Work",0));
                uint cp=Config.ClanWarCp/(arena==ClanWar.ClanArena.TwinCityClan?1u:2u),rvp=Config.ClanWarRevoraPoints/(arena==ClanWar.ClanArena.TwinCityClan?1u:2u);
                Check(!war.Open && !Fenced(chief) && chief.Entity.ConquerPoints==beforeCp+cp && chief.Entity.RevoraPoint==beforeRvp+rvp && guest.Entity.ConquerPoints==memberCp+cp/3,"territory settlement pays configured leader/member currency once: "+arena);
                Check(city?HasAchievement(chief,10305+index) && HasAchievement(guest,10305+index) && !HasAchievement(spectator,10305+index)
                    :Enumerable.Range(10305,5).All(id=>!HasAchievement(chief,id) && !HasAchievement(guest,id)),
                    "city victories grant their identity; wilderness victories invent no city achievement: "+arena);
                Check(Scalar("SELECT count(*) FROM revora_clan_war_awards WHERE uid=@uid AND paid_at IS NOT NULL",chief.Entity.UID)==1,"territory payout has one durable receipt");
                uint soulType=checked((uint)Scalar("SELECT soul_type FROM revora_clan_war_awards WHERE uid=@uid",chief.Entity.UID));
                long soulUid=Scalar("SELECT soul_uid FROM revora_clan_war_awards WHERE uid=@uid",chief.Entity.UID);
                Check(chief.Inventory.Objects.Count(i=>i.UID==soulUid && i.ID==soulType && i.Bound && i.Days==0)==1
                    && ConquerItemInformation.BaseInformations[soulType].PurificationLevel==7
                    && guest.Inventory.Objects.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1,
                    "ordinary Clan War leader and member receive one frozen permanent bound P7: "+arena);
                chief=ReloadBall(chief);guest=ReloadBall(guest);ConquerItemTable.LoadItems(chief);ConquerItemTable.LoadItems(guest);Call(warResults,null,"Pay",chief);chief.Entity.MyAchievement.Send();
                Check(chief.Inventory.Objects.Count(i=>i.UID==soulUid && i.ID==soulType && i.Bound && i.Days==0)==1,
                    "exact earned Clan War soul and binding survive character/item reload: "+arena);
                Check(chief.Entity.ConquerPoints==beforeCp+cp && chief.Entity.RevoraPoint==beforeRvp+rvp && (!city || HasAchievement(chief,10305+index)),"territory purse and achievement survive reload without repaying");
                var restored=new ClanWar.ClanTournament[10];for(int j=0;j<10;j++)restored[j]=new ClanWar.ClanTournament((ClanWar.ClanArena)j);Call(warResults,null,"Load",(object)restored);
                Check(restored[(int)arena].Client.UID==clan.ID && restored[(int)arena].Client.OccupationDays==1,"territory occupation reload is database-backed");
                Call(typeof(ClanWar.ClanTournament),war,"Begin",start,20261001+index,false);Check(!war.Open,"same daily territory run cannot reopen after settlement");
            }
            {
                var chief=player();var clan=found(chief);var rival=player();found(rival);var ally=player();join(chief,ally);
                var war=new ClanWar.ClanTournament(ClanWar.ClanArena.TwinCityClan);ClanWar.Tournaments[0]=war;var envoy=town.Npcs[101895];
                warNow=warNow.AddDays(1);var start=warNow;Call(typeof(ClanWar.ClanTournament),war,"Begin",start,20261020,false);
                foreach(var p in new[]{chief,rival,ally}){p.Entity.X=envoy.X;p.Entity.Y=envoy.Y;warWire(p,6,101895);warWire(p,8,101895);p.ReviveStamp=Time32.Now.AddSeconds(-60);p.Attackable=true;}
                chief.Entity.X=rival.Entity.X;chief.Entity.Y=rival.Entity.Y;chief.Screen.Add(rival.Entity);rival.Screen.Add(chief.Entity);
                uint friendlyHp=ally.Entity.Hitpoints,damage=friendlyHp;
                Revora.Game.Attacking.Handle.ReceiveAttack(chief.Entity,ally.Entity,new Attack(true){AttackType=Attack.Melee,Attacker=chief.Entity.UID,Attacked=ally.Entity.UID},ref damage,null);
                Check(ally.Entity.Hitpoints==friendlyHp,"actual clan combat blocks damage to a clanmate");
                lock(chief.ItemSyncRoot){var busyHit=Task.Run(()=>{uint amount=1;Revora.Game.Attacking.Handle.ReceiveAttack(chief.Entity,rival.Entity,new Attack(true){AttackType=Attack.Melee,Attacker=chief.Entity.UID,Attacked=rival.Entity.UID},ref amount,null);return amount;});
                    Check(busyHit.Wait(TimeSpan.FromSeconds(5)) && busyHit.Result==0,"clan combat refuses busy owner locks promptly instead of inverting settlement lock order");}
                damage=rival.Entity.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(chief.Entity,rival.Entity,new Attack(true){AttackType=Attack.Melee,Attacker=chief.Entity.UID,Attacked=rival.Entity.UID},ref damage,null);
                Check(rival.Entity.Dead,"ordinary lethal combat eliminates the rival clan from the field");
                warNow=start.AddMinutes(20);Call(typeof(ClanWar.ClanTournament),war,"Work",0);
                Check(!war.Open && HasAchievement(chief,10305) && HasAchievement(ally,10305) && !HasAchievement(rival,10305),"actual clan fight settles credit only for surviving winning members");
            }
            int trialDay=20261101;
            foreach(string invalid in new[]{"remote","clanless","malformed","closed","unadmitted","dead","left","empty","rivals"}){
                var chief=player();var clan=found(chief);var rival=player();found(rival);var war=new ClanWar.ClanTournament(ClanWar.ClanArena.TwinCityClan);ClanWar.Tournaments[0]=war;
                uint npc=101895;var envoy=town.Npcs[npc];chief.Entity.X=rival.Entity.X=envoy.X;chief.Entity.Y=rival.Entity.Y=envoy.Y;
                warNow=warNow.AddDays(1);var start=warNow;Call(typeof(ClanWar.ClanTournament),war,"Begin",start,trialDay++,false);
                if(invalid=="remote")chief.Entity.X+=30;
                if(invalid=="clanless"){chief=player();chief.Entity.X=envoy.X;chief.Entity.Y=envoy.Y;}
                if(invalid=="left"){var member=player();join(chief,member);chief=member;chief.Entity.X=envoy.X;chief.Entity.Y=envoy.Y;}
                if(invalid=="closed")warNow=start.AddMinutes(20);
                if(invalid=="malformed"){var shortFrame=new byte[17];shortFrame[4]=6;ClanWar.Process(shortFrame,chief);}
                else if(invalid!="unadmitted" && invalid!="empty"){warWire(chief,6,npc);warWire(chief,8,npc);}
                if(invalid=="unadmitted")chief.BindWorldMap(1505);
                if(invalid=="dead")chief.Entity.Hitpoints=0;
                if(invalid=="left")depart(chief,23,"");
                if(invalid=="rivals"){warWire(rival,6,npc);warWire(rival,8,npc);Check(!((bool)Call(typeof(ClanWar),null,"BlockAttack",chief.Entity,rival.Entity)),"admitted rival clans may fight");}
                else if(chief.Entity.MapID==1505)Check((bool)Call(typeof(ClanWar),null,"BlockAttack",chief.Entity,rival.Entity),"invalid or unadmitted clan combat is blocked: "+invalid);
                else Check(chief.Entity.MapID==1002,"invalid entry stays outside the clan arena: "+invalid);
                warNow=start.AddMinutes(20);Call(typeof(ClanWar.ClanTournament),war,"Work",0);
                if(invalid=="rivals"){Check(war.Open,"two surviving clans continue into overtime");warNow=start.AddMinutes(30);Call(typeof(ClanWar.ClanTournament),war,"Work",0);}
                Check(!war.Open && Scalar("SELECT count(*) FROM revora_clan_war_awards WHERE uid=@uid",chief.Entity.UID)==0 && !HasAchievement(chief,10305),"invalid or tied field cannot mint clan rewards or credit: "+invalid);
                if(chief.Entity.MapID==1505)chief.BindWorldMap(1002);
            }
            foreach(bool legacy in new[]{false,true}) {
                var chief=player();var clan=found(chief);chief.Entity.ConquerPoints=uint.MaxValue;
                var war=new ClanWar.ClanTournament(ClanWar.ClanArena.TwinCityClan);ClanWar.Tournaments[0]=war;
                var envoy=town.Npcs[101895];chief.Entity.X=envoy.X;chief.Entity.Y=envoy.Y;warNow=warNow.AddDays(1);var start=warNow;
                Call(typeof(ClanWar.ClanTournament),war,"Begin",start,trialDay++,false);warWire(chief,6,101895);warWire(chief,8,101895);
                warNow=start.AddMinutes(20);Call(typeof(ClanWar.ClanTournament),war,"Work",0);
                Check(!war.Open && !Fenced(chief) && chief.Entity.ConquerPoints==uint.MaxValue && chief.Entity.RevoraPoint==0
                    && Scalar("SELECT count(*) FROM revora_clan_war_awards WHERE uid=@uid AND paid_at IS NULL",chief.Entity.UID)==1,"full CP purse defers the whole reward without overflow or losing the entitlement");
                uint frozen=checked((uint)Scalar("SELECT soul_type FROM revora_clan_war_awards WHERE uid=@uid",chief.Entity.UID));
                for(int i=0;i<40;i++)Pack(chief,1088001,1);chief.Entity.ConquerPoints=0;
                if(legacy){Query("UPDATE revora_clan_war_awards SET soul_type=0 WHERE uid=@uid","uid",(long)chief.Entity.UID);Call(warResults,null,"Initialize");}
                Call(warResults,null,"Pay",chief);
                if(legacy){
                    Check(!Fenced(chief) && chief.Inventory.Count==40 && chief.Entity.ConquerPoints==Config.ClanWarCp
                        && Scalar("SELECT count(*) FROM revora_clan_war_awards WHERE uid=@uid AND paid_at IS NOT NULL AND soul_type=0 AND soul_uid IS NULL",chief.Entity.UID)==1,
                        "pre-upgrade pending currency-only purse keeps its terms and can pay with a full bag");
                    Call(warResults,null,"Initialize");Call(warResults,null,"Pay",chief);
                    Check(chief.Inventory.Count==40 && chief.Entity.ConquerPoints==Config.ClanWarCp,"migration/replay never reopens an old paid Clan War purse");
                }else{
                    Check(!Fenced(chief) && chief.Inventory.Count==40 && chief.Entity.ConquerPoints==0 && chief.Entity.RevoraPoint==0
                        && Scalar("SELECT count(*) FROM revora_clan_war_awards WHERE uid=@uid AND paid_at IS NULL",chief.Entity.UID)==1,
                        "full inventory retains the entire new Clan War soul and currency purse");
                    DisRemove(chief,chief.Inventory.Objects.First());
                    Query("CREATE FUNCTION suppress_clan_soul() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.id BETWEEN 800000 AND 899999 THEN RETURN NULL; END IF; RETURN NEW; END $$;CREATE TRIGGER suppress_clan_soul BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_clan_soul()");
                    try{Call(warResults,null,"Pay",chief);}finally{Query("DROP TRIGGER suppress_clan_soul ON items;DROP FUNCTION suppress_clan_soul()");}
                    Check(Fenced(chief) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",chief.Entity.UID)==0
                        && Scalar("SELECT count(*) FROM revora_clan_war_awards WHERE uid=@uid AND paid_at IS NULL AND soul_uid IS NULL",chief.Entity.UID)==1,
                        "failed Clan War P7 insert preserves currencies and the original pending soul");
                    chief=ReloadBall(chief);ConquerItemTable.LoadItems(chief);Parallel.For(0,6,n=>Call(warResults,null,"Pay",chief));
                    Check(!Fenced(chief) && chief.Inventory.Count==40 && chief.Entity.ConquerPoints==Config.ClanWarCp && chief.Entity.RevoraPoint==Config.ClanWarRevoraPoints
                        && chief.Inventory.Objects.Count(i=>i.ID==frozen && i.Bound && i.Days==0)==1,
                        "reconnect with one free slot pays the exact frozen bound Clan War soul and both currencies once");
                }
            }
            foreach(string table in new[]{"revora_clan_war_awards","revora_clan_territories","revora_clan_wars","revora_achievement_evidence"}){
                var chief=player();var clan=found(chief);var war=new ClanWar.ClanTournament(ClanWar.ClanArena.TwinCityClan);ClanWar.Tournaments[0]=war;
                var envoy=town.Npcs[101895];chief.Entity.X=envoy.X;chief.Entity.Y=envoy.Y;warNow=warNow.AddDays(1);var start=warNow;
                Call(typeof(ClanWar.ClanTournament),war,"Begin",start,trialDay++,false);warWire(chief,6,101895);warWire(chief,8,101895);warNow=start.AddMinutes(20);
                Query("CREATE FUNCTION suppress_war() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                Query("CREATE TRIGGER suppress_war BEFORE "+(table=="revora_clan_wars"?"UPDATE":"INSERT")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_war()");
                try{Call(typeof(ClanWar.ClanTournament),war,"Work",0);}finally{Query("DROP TRIGGER suppress_war ON "+table+";DROP FUNCTION suppress_war()");}
                Check(war.Open && Scalar("SELECT count(*) FROM revora_clan_war_awards WHERE uid=@uid",chief.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10305",chief.Entity.UID)==0,"failed result rolls back occupation, entitlements and achievement: "+table);
                Call(typeof(ClanWar.ClanTournament),war,"Work",0);Check(!war.Open && HasAchievement(chief,10305),"unchanged field settles once after result persistence recovers");
            }
            {
                var chief=player();var clan=found(chief);var war=new ClanWar.ClanTournament(ClanWar.ClanArena.TwinCityClan);ClanWar.Tournaments[0]=war;
                var envoy=town.Npcs[101895];chief.Entity.X=envoy.X;chief.Entity.Y=envoy.Y;warNow=warNow.AddDays(1);var start=warNow;
                Call(typeof(ClanWar.ClanTournament),war,"Begin",start,trialDay++,false);warWire(chief,6,101895);warWire(chief,8,101895);uint beforeCp=chief.Entity.ConquerPoints,beforeRvp=chief.Entity.RevoraPoint;
                Query("CREATE FUNCTION suppress_war_payment() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                Query("CREATE TRIGGER suppress_war_payment BEFORE UPDATE ON revora_clan_war_awards FOR EACH ROW EXECUTE FUNCTION suppress_war_payment()");
                warNow=start.AddMinutes(20);try{Call(typeof(ClanWar.ClanTournament),war,"Work",0);}finally{Query("DROP TRIGGER suppress_war_payment ON revora_clan_war_awards;DROP FUNCTION suppress_war_payment()");}
                Check(!war.Open && Fenced(chief) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",chief.Entity.UID)==beforeCp
                    && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",chief.Entity.UID)==beforeRvp
                    && Scalar("SELECT count(*) FROM revora_clan_war_awards WHERE uid=@uid AND paid_at IS NULL",chief.Entity.UID)==1,"failed payment rolls back both balances while preserving the earned entitlement");
                Check(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0",chief.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_clan_war_awards WHERE uid=@uid AND soul_uid IS NOT NULL",chief.Entity.UID)==0,
                    "failed Clan War paid receipt also rolls back its preceding P7 insert");
                chief=ReloadBall(chief);Parallel.For(0,6,n=>Call(warResults,null,"Tick",chief));chief.Entity.MyAchievement.Send();
                Check(!Fenced(chief) && chief.Entity.ConquerPoints==beforeCp+Config.ClanWarCp && chief.Entity.RevoraPoint==beforeRvp+Config.ClanWarRevoraPoints && HasAchievement(chief,10305),"earned unpaid prize recovers after reconnect exactly once under concurrent delivery");
                Check(chief.Entity.MapID==1002,"automatic prize delivery also recovers a reconnected player from a closed arena");
                Check(chief.Inventory.Objects.Count(i=>i.Bound && i.Days==0 && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1,
                    "concurrent reconnect delivery installs the frozen Clan War soul once");
                var interrupted=new ClanWar.ClanTournament(ClanWar.ClanArena.TwinCityClan);Call(typeof(ClanWar.ClanTournament),interrupted,"Begin",warNow,trialDay,false);Call(warResults,null,"CancelInterrupted");
                var reopened=new ClanWar.ClanTournament(ClanWar.ClanArena.TwinCityClan);Call(typeof(ClanWar.ClanTournament),reopened,"Begin",warNow,trialDay,false);
                Check(!reopened.Open && Scalar("SELECT count(*) FROM revora_clan_war_awards WHERE uid=@uid AND paid_at IS NOT NULL",chief.Entity.UID)==1,"startup cancels unfinished runs without reopening the day or removing completed prizes");
            }
        }finally{warClock.SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
        Kernel.Clans.Clear();Clan.LoadClans();
        Check(Kernel.Clans[transferClan.ID].LeaderId==heir.Entity.UID && Kernel.Clans[transferClan.ID].LeaderName==heir.Entity.Name,"production clan registry reload preserves transferred leadership");
        Check(Kernel.Clans.ContainsKey(founded) && Kernel.Clans[founded].LeaderId==owner.Entity.UID && Kernel.Clans[founded].Fund==Config.ClanCreateFund
            && Kernel.Clans[founded].Members.ContainsKey(newMember.Entity.UID),"production registry reload restores NPC-founded clan, fund and invited membership");

    }
}
