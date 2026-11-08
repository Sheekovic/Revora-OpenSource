using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static DateTime clanProbeNow=new DateTime(2034,1,2,3,33,0,DateTimeKind.Utc);
    static void ClanWire() {
        ClanEnvoys();var npc=Kernel.Maps[1002].Npcs[101895];
        var children=new System.Diagnostics.Process[3];var errors=new System.Threading.Tasks.Task<string>[3];
        var pool=new Npgsql.NpgsqlConnectionStringBuilder(DataHolder.ConnectionString){Pooling=true,MaxPoolSize=12,MinPoolSize=0};
        DataHolder.ConnectionString=pool.ConnectionString;Npgsql.NpgsqlConnection.ClearAllPools();
        var clan=new Clan(FirstUid,50000,"ClanWireProbe","ArenaProbe0"){LeaderName="ArenaProbe0",Level=1,Announcement="Probe"};
        Sql("INSERT INTO clans(identifier,leaderid,name,leadername,fund,level,bptower,announcement,claimed) VALUES(50000,@leader,'ClanWireProbe','ArenaProbe0',0,1,0,'Probe',0)","leader",(long)FirstUid);Kernel.Clans.Add(clan.ID,clan);
        try {
            for(int i=0;i<3;i++) {
                if(i==2) {
                    clan=new Clan(FirstUid+2,50001,"ClanWireRival","ArenaProbe2"){LeaderName="ArenaProbe2",Level=1,Announcement="Probe"};
                    Sql("INSERT INTO clans(identifier,leaderid,name,leadername,fund,level,bptower,announcement,claimed) VALUES(50001,@leader,'ClanWireRival','ArenaProbe2',0,1,0,'Probe',0)","leader",(long)(FirstUid+2));Kernel.Clans.Add(clan.ID,clan);
                }
                Player(Kernel.Maps[1002]);nextUid--;var p=Player(Kernel.Maps[1002],true);
                p.Entity.ClanId=clan.ID;p.Entity.ClanRank=i!=1?Clan.Ranks.ClanLeader:Clan.Ranks.Member;p.Entity.ClanName=clan.Name;
                clan.Members.TryAdd(p.Entity.UID,new ClanMember{UID=p.Entity.UID,Identifier=p.Entity.UID,Name=p.Entity.Name,Rank=p.Entity.ClanRank});
                EntityTable.UpdateClanID(p);EntityTable.UpdateClanRank(p);Require(EntityTable.SaveEntity(p));
                string username="TeamProbe"+i,password=Guid.NewGuid().ToString("N");
                var account=new AccountTable(username){Password=password,State=AccountTable.AccountState.Player,EntityID=p.Entity.UID};account.Insert();account.Save();Require(account.HasSrpVerifier);
                Sql("UPDATE entities SET owner=@user,x=@x,y=@y WHERE uid=@uid","user",username,"x",(int)npc.X,"y",(int)npc.Y+1,"uid",(long)p.Entity.UID);Kernel.GamePool.Remove(p.Entity.UID);
                var start=new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CandidateTeamWireProbe.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Environment.CurrentDirectory};
                start.EnvironmentVariables["REVORA_TEAM_WIRE_ROLE"]=i.ToString();start.EnvironmentVariables["REVORA_CANDIDATE_ACCOUNT_PASSWORD"]=password;start.EnvironmentVariables.Remove("REVORA_DATABASE_PASSWORD");
                children[i]=System.Diagnostics.Process.Start(start);start.EnvironmentVariables.Remove("REVORA_CANDIDATE_ACCOUNT_PASSWORD");password=null;
                errors[i]=children[i].StandardError.ReadToEndAsync();TeamWireRead(children[i],"[team-wire] ready");
            }
            Feature("ClanWarResults").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>clanProbeNow));
            var war=ClanWar.Tournaments[0];var work=war.GetType().GetMethod("Work",BindingFlags.Instance|BindingFlags.NonPublic);
            war.GetType().GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(war,new object[]{clanProbeNow,20340102,false});Require(war.Open);
            foreach(var child in children)TeamWireSend(child,"clan-enter");
            Require((long)Sql("SELECT count(*) FROM revora_clan_war_entries WHERE admitted AND clan=50000")==2);
            Require((long)Sql("SELECT count(*) FROM revora_clan_war_entries WHERE admitted AND clan=50001")==1);
            clanProbeNow=clanProbeNow.AddMinutes(20);work.Invoke(war,new object[]{0});Require(war.Open);
            Require((long)Sql("SELECT count(*) FROM revora_clan_war_awards")==0);
            var attacker=Kernel.GamePool[FirstUid];var rival=Kernel.GamePool[FirstUid+2];
            var map=attacker.Map;ushort x=0,y=0;
            for(int spot=0;spot<128 && x==0;spot++) {
                var pair=map.RandomCoordinates();
                if(map.Floor[pair.Item1+1,pair.Item2,Revora.Game.MapObjectType.Player]){x=pair.Item1;y=pair.Item2;}
            }
            Require(x!=0);attacker.Entity.X=x;attacker.Entity.Y=y;rival.Entity.X=(ushort)(x+1);rival.Entity.Y=y;
            TeamWireSend(children[0],"combat-pk");Require(attacker.Entity.PKMode==Revora.Game.Enums.PKMode.PK);
            Require(System.Threading.SpinWait.SpinUntil(()=>rival.Attackable,7000));
            uint initialHp=rival.Entity.Hitpoints;
            for(int swing=0;swing<80 && !rival.Entity.Dead;swing++) {
                System.Threading.Thread.Sleep(550);TeamWireSend(children[0],"attack:"+rival.Entity.UID);
            }
            Require(initialHp>0 && rival.Entity.Dead);
            work.Invoke(war,new object[]{0});Require(!war.Open);
            Require((long)Sql("SELECT count(*) FROM revora_clan_wars WHERE state='complete' AND winner=50000")==1);
            Require((long)Sql("SELECT count(*) FROM revora_clan_war_awards WHERE uid=@uid","uid",(long)rival.Entity.UID)==0);
            TeamWireSend(children[2],"clan-lost");
            for(int i=0;i<2;i++) {
                var p=Kernel.GamePool[FirstUid+(uint)i];uint cp=i==0?Config.ClanWarCp:Config.ClanWarCp/3,rvp=i==0?Config.ClanWarRevoraPoints:Config.ClanWarRevoraPoints>1?Config.ClanWarRevoraPoints/3:Config.ClanWarRevoraPoints;
                Require(p.Account.State==AccountTable.AccountState.Player && p.Entity.ConquerPoints==cp && p.Entity.RevoraPoint==rvp && p.Inventory.Count==1);
                var item=p.Inventory.Objects.Single();Require(item.Bound && ConquerItemInformation.BaseInformations[item.ID].PurificationLevel==7);
                TeamWireSend(children[i],"clan-prize:"+cp+":"+rvp+":"+item.UID+":"+item.ID);
                string receipt=ClanReceipts(p);Call(Feature("ClanWarResults"),"Pay",p);Require(ClanReceipts(p)==receipt);
                TeamWireSend(children[i],"clan-prize:"+cp+":"+rvp+":"+item.UID+":"+item.ID);
                children[i].StandardInput.WriteLine("stop");children[i].StandardInput.Flush();TeamWireRead(children[i],"[team-wire] stopped");Require(children[i].WaitForExit(5000) && children[i].ExitCode==0);
            }
            children[2].StandardInput.WriteLine("stop");children[2].StandardInput.Flush();TeamWireRead(children[2],"[team-wire] stopped");Require(children[2].WaitForExit(5000) && children[2].ExitCode==0);
            System.Console.WriteLine("[clan-process] clan-wire-passed");
        } finally {
            for(int i=0;i<3;i++)if(children[i]!=null) {
                if(!children[i].HasExited){children[i].Kill();children[i].WaitForExit(5000);}
                if(errors[i]!=null && errors[i].Wait(1000) && !string.IsNullOrEmpty(errors[i].Result))System.Console.Error.WriteLine(errors[i].Result);
                children[i].Dispose();
            }
        }
        Server.CommandsAI("@close");throw new InvalidOperationException("Private clan host did not stop.");
    }
    static void ClanContest(bool reload=false) {
        if(reload) {
            for(int i=0;i<4;i++) {
                var p=Player(Kernel.Maps[1002],true);
                Require(CtfItems(p)==(string)Sql("SELECT items FROM revora_clan_contest_probe WHERE uid=@uid","uid",(long)p.Entity.UID));
                Require(ClanReceipts(p)==(string)Sql("SELECT receipts FROM revora_clan_contest_probe WHERE uid=@uid","uid",(long)p.Entity.UID));
                Require((bool)Sql("SELECT cp=@cp AND rvp=@rvp FROM revora_clan_contest_probe WHERE uid=@uid","uid",(long)p.Entity.UID,"cp",(long)p.Entity.ConquerPoints,"rvp",(long)p.Entity.RevoraPoint));
                Call(Feature("ClanWarResults"),"Pay",p);
                Require((bool)Sql("SELECT cp=@cp AND rvp=@rvp FROM revora_clan_contest_probe WHERE uid=@uid","uid",(long)p.Entity.UID,"cp",(long)p.Entity.ConquerPoints,"rvp",(long)p.Entity.RevoraPoint));
                Require(ClanReceipts(p)==(string)Sql("SELECT receipts FROM revora_clan_contest_probe WHERE uid=@uid","uid",(long)p.Entity.UID));
                Require(CtfItems(p)==(string)Sql("SELECT items FROM revora_clan_contest_probe WHERE uid=@uid","uid",(long)p.Entity.UID));
            }
            System.Console.WriteLine("[clan-process] clan-contest-reload-passed");Environment.Exit(0);
        }
        ClanEnvoys();for(int i=0;i<4;i++)Player(Kernel.Maps[1002]);nextUid=FirstUid;
        var people=Enumerable.Range(0,4).Select(i=>Player(Kernel.Maps[1002],true)).ToArray();
        for(int group=0;group<2;group++) {
            var leader=people[group*2];uint id=(uint)(50000+group);string name="ContestClan"+group;
            var clan=new Clan(leader.Entity.UID,id,name,leader.Entity.Name){LeaderName=leader.Entity.Name,Level=1,Announcement="Probe"};
            Sql("INSERT INTO clans(identifier,leaderid,name,leadername,fund,level,bptower,announcement,claimed) VALUES(@clan,@leader,@name,@leadername,0,1,0,'Probe',0)","clan",(long)id,"leader",(long)leader.Entity.UID,"name",name,"leadername",leader.Entity.Name);
            Kernel.Clans.Add(clan.ID,clan);
            for(int member=0;member<2;member++) {
                var p=people[group*2+member];p.Entity.ClanId=id;p.Entity.ClanRank=member==0?Clan.Ranks.ClanLeader:Clan.Ranks.Member;p.Entity.ClanName=name;
                clan.Members.TryAdd(p.Entity.UID,new ClanMember{UID=p.Entity.UID,Identifier=p.Entity.UID,Name=p.Entity.Name,Rank=p.Entity.ClanRank});
                EntityTable.UpdateClanID(p);EntityTable.UpdateClanRank(p);Require(EntityTable.SaveEntity(p));p.LoadItemStats();
            }
        }
        Feature("ClanWarResults").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>clanProbeNow));
        var war=ClanWar.Tournaments[0];var work=war.GetType().GetMethod("Work",BindingFlags.Instance|BindingFlags.NonPublic);
        var npc=Kernel.Maps[1002].Npcs[101895];
        for(int contest=0;contest<2;contest++) {
            clanProbeNow=new DateTime(2034,2,1+contest,3,33,0,DateTimeKind.Utc);
            war.GetType().GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(war,new object[]{clanProbeNow,20340201+contest,false});Require(war.Open);
            foreach(var p in people.Take(3)) {
                p.BindWorldMap(1002);p.Entity.X=npc.X;p.Entity.Y=(ushort)(npc.Y+1);
                foreach(byte action in new byte[]{6,8}) {
                    var frame=new byte[148];Writer.WriteUInt16(140,0,frame);Writer.WriteUInt16(1313,2,frame);frame[4]=action;Writer.WriteUInt32(npc.UID,12,frame);Encoding.ASCII.GetBytes("TQClient").CopyTo(frame,140);ClanWar.Process(frame,p);
                }
                Require(p.Entity.MapID==ClanWar.GetMap(ClanWar.ClanArena.TwinCityClan));
            }
            clanProbeNow=clanProbeNow.AddMinutes(20);work.Invoke(war,new object[]{0});Require(war.Open);
            Require((long)Sql("SELECT count(*) FROM revora_clan_war_awards")==0);
            if(contest==0) {
                clanProbeNow=clanProbeNow.AddMinutes(10);work.Invoke(war,new object[]{0});Require(!war.Open);
                Require((long)Sql("SELECT count(*) FROM revora_clan_wars WHERE state='cancelled' AND day=20340201")==1);
                Require((long)Sql("SELECT count(*) FROM revora_clan_territories")==0);continue;
            }
            var attacker=people[0];var ally=people[1];var rival=people[2];var outsider=people[3];
            outsider.BindWorldMap(rival.Entity.MapID);outsider.Entity.X=rival.Entity.X;outsider.Entity.Y=rival.Entity.Y;
            foreach(var rejected in new[]{ally,outsider}) {
                uint hp=rejected.Entity.Hitpoints,damage=hp;
                Revora.Game.Attacking.Handle.ReceiveAttack(attacker.Entity,rejected.Entity,new Attack(true){AttackType=Attack.Melee,Attacker=attacker.Entity.UID,Attacked=rejected.Entity.UID},ref damage,null);
                Require(rejected.Entity.Hitpoints==hp && damage==0);
            }
            // Controlled lethal damage uses the production damage/death path,
            // not a supplied death, winning clan or prize entitlement.
            EliminationHit(attacker,rival.Entity);Require(rival.Entity.Dead);
            work.Invoke(war,new object[]{0});Require(!war.Open);
            Require((long)Sql("SELECT count(*) FROM revora_clan_territories WHERE arena=0 AND clan=50000 AND days=1")==1);
            for(int i=0;i<4;i++) {
                var p=people[i];Require(p.Entity.ConquerPoints==(i==0?Config.ClanWarCp:i==1?Config.ClanWarCp/3:0));
                Require(p.Entity.RevoraPoint==(i==0?Config.ClanWarRevoraPoints:i==1?(Config.ClanWarRevoraPoints>1?Config.ClanWarRevoraPoints/3:Config.ClanWarRevoraPoints):0));
                Require(p.Inventory.Count==(i<2?1:0));
                if(i<2)Require(p.Inventory.Objects.Single().Bound && ConquerItemInformation.BaseInformations[p.Inventory.Objects.Single().ID].PurificationLevel==7);
                Require((long)Sql("SELECT count(*) FROM revora_clan_war_awards WHERE uid=@uid","uid",(long)p.Entity.UID)==(i<2?1:0));
                Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10305","uid",(long)p.Entity.UID)==(i<2?1:0));
            }
        }
        Sql("CREATE TABLE revora_clan_contest_probe(uid bigint PRIMARY KEY,items text,cp bigint,rvp bigint,receipts text)");
        foreach(var p in people) {
            Require(EntityTable.SaveEntity(p));
            Sql("INSERT INTO revora_clan_contest_probe VALUES(@uid,@items,@cp,@rvp,@receipts)","uid",(long)p.Entity.UID,"items",CtfItems(p),"cp",(long)p.Entity.ConquerPoints,"rvp",(long)p.Entity.RevoraPoint,"receipts",ClanReceipts(p));
        }
        System.Console.WriteLine("[clan-process] clan-contest-passed");Environment.Exit(0);
    }
    static void ClanEnvoys() {
        foreach(uint id in new uint[]{101895,101897}) {
            var town=Kernel.Maps[1002];var npc=town.Npcs[id];
            Require(npc.Type==Revora.Game.Enums.NpcType.ClanInfo && npc.MapID==1002 && !Revora.Game.Features.TwinCityPlaza.IsClosed(id));
            Require(town.Floor[npc.X,npc.Y+1,Revora.Game.MapObjectType.Player]);
            Require(!town.Npcs.Values.Any(n=>n.UID!=id && Kernel.GetDistance(npc.X,npc.Y,n.X,n.Y)<2));
            Require(!Kernel.Maps.Values.Any(m=>m.ID!=1002 && m.Npcs.ContainsKey(id)));
        }
    }
    static void ClanCapture() {
        ClanEnvoys();
        Sql("CREATE TABLE revora_clan_envoy_probe AS SELECT id,cellx,celly FROM npcs WHERE id IN(101895,101897)");
        for(int i=0;i<3;i++)Player(Kernel.Maps[1002]);nextUid=FirstUid;
        var people=Enumerable.Range(0,3).Select(i=>Player(Kernel.Maps[1002],true)).ToArray();
        var clan=new Clan(FirstUid,50000,"ClanWarProbe",people[0].Entity.Name){LeaderName=people[0].Entity.Name,Level=1,Announcement="Probe"};
        // Supply existing clan membership, not entry, occupation, prizes or achievements.
        Sql("INSERT INTO clans(identifier,leaderid,name,leadername,fund,level,bptower,announcement,claimed) VALUES(50000,@leader,'ClanWarProbe',@name,0,1,0,'Probe',0)","leader",(long)FirstUid,"name",people[0].Entity.Name);
        Kernel.Clans.Add(clan.ID,clan);
        for(int i=0;i<3;i++) {
            var p=people[i];p.Entity.ClanId=clan.ID;p.Entity.ClanRank=i==0?Clan.Ranks.ClanLeader:Clan.Ranks.Member;p.Entity.ClanName=clan.Name;
            clan.Members.TryAdd(p.Entity.UID,new ClanMember{UID=p.Entity.UID,Identifier=p.Entity.UID,Name=p.Entity.Name,Rank=p.Entity.ClanRank});
            EntityTable.UpdateClanID(p);EntityTable.UpdateClanRank(p);Require(EntityTable.SaveEntity(p));p.LoadItemStats();
        }
        Feature("ClanWarResults").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>clanProbeNow));
        uint[] envoys={101895,101901,101905,101913,101909,101897,101902,101906,101914,101910};
        int[] achievements={10305,10309,10308,10306,10307};
        ulong[] expectedCp=new ulong[3],expectedRvp=new ulong[3];
        var work=typeof(ClanWar.ClanTournament).GetMethod("Work",BindingFlags.Instance|BindingFlags.NonPublic);
        for(int arena=0;arena<envoys.Length;arena++) {
            clanProbeNow=new DateTime(2034,1,2,3,33,0,DateTimeKind.Utc).AddDays(arena);
            var war=ClanWar.Tournaments[arena];
            typeof(ClanWar.ClanTournament).GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(war,new object[]{clanProbeNow,20340102+arena,false});
            Require(war.Open);
            var npc=Kernel.Maps.Values.SelectMany(m=>m.Npcs.Values).Single(n=>n.UID==envoys[arena]);
            Require(npc.MapID!=6000 && npc.Type==Revora.Game.Enums.NpcType.ClanInfo && !Revora.Game.Features.TwinCityPlaza.IsClosed(npc.UID));
            var town=Kernel.Maps[npc.MapID];int approachX=0,approachY=0;
            for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)
                if((dx!=0 || dy!=0) && town.Floor[npc.X+dx,npc.Y+dy,Revora.Game.MapObjectType.Player]){approachX=npc.X+dx;approachY=npc.Y+dy;}
            Require(approachX>0 && approachY>0);
            foreach(var p in people.Take(2)) {
                p.Entity.MapID=npc.MapID;p.Entity.X=(ushort)approachX;p.Entity.Y=(ushort)approachY;
                foreach(byte action in new byte[]{6,8}) {
                    var frame=new byte[148];Writer.WriteUInt16(140,0,frame);Writer.WriteUInt16(1313,2,frame);frame[4]=action;Writer.WriteUInt32(npc.UID,12,frame);Encoding.ASCII.GetBytes("TQClient").CopyTo(frame,140);
                    ClanWar.Process(frame,p);
                }
                Require(p.Entity.MapID==ClanWar.GetMap((ClanWar.ClanArena)arena));
            }
            work.Invoke(war,new object[]{0});Require(war.Open);
            clanProbeNow=clanProbeNow.AddMinutes(20);work.Invoke(war,new object[]{0});Require(!war.Open);
            uint cp=Config.ClanWarCp/(arena==0?1u:2u),rvp=Config.ClanWarRevoraPoints/(arena==0?1u:2u);
            expectedCp[0]+=cp;expectedCp[1]+=cp/3;expectedRvp[0]+=rvp;expectedRvp[1]+=rvp>1?rvp/3:rvp;
            for(int i=0;i<3;i++) {
                var p=people[i];Require(p.Entity.ConquerPoints==expectedCp[i] && p.Entity.RevoraPoint==expectedRvp[i]);
                Require(p.Inventory.Count==(i<2?arena+1:0));
                foreach(var soul in p.Inventory.Objects) {
                    Require(soul.Bound && soul.Days==0 && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7);
                    Require((long)Sql("SELECT count(*) FROM revora_clan_war_awards WHERE uid=@uid AND soul_uid=@item AND soul_type=@type AND paid_at IS NOT NULL","uid",(long)p.Entity.UID,"item",(long)soul.UID,"type",(long)soul.ID)==1);
                }
                foreach(int achievement in achievements)
                    Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@achievement","uid",(long)p.Entity.UID,"achievement",achievement)==(i<2 && Array.IndexOf(achievements,achievement)<=arena?1:0));
            }
            Require((long)Sql("SELECT count(*) FROM revora_clan_territories WHERE arena=@arena AND clan=50000 AND days=1","arena",arena)==1);
            System.Console.WriteLine("[clan-process] arena-passed "+arena);
        }
        // A separate earned wilderness purse is deferred by durable full bags.
        clanProbeNow=new DateTime(2034,1,12,3,33,0,DateTimeKind.Utc);
        var deferred=ClanWar.Tournaments[9];
        typeof(ClanWar.ClanTournament).GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(deferred,new object[]{clanProbeNow,20340112,false});
        Require(deferred.Open);
        foreach(var p in people.Take(2)) {
            for(int n=p.Inventory.Count;n<40;n++)Require(p.Inventory.Add(611004,0,1));
            Require(p.Inventory.Count==40);ClanDeferredEntry(p);
        }
        clanProbeNow=clanProbeNow.AddMinutes(20);work.Invoke(deferred,new object[]{0});Require(!deferred.Open);
        foreach(var p in people.Take(2)) {
            Require(p.Inventory.Count==40);
            Require(!(bool)Call(Feature("ClanWarResults"),"Pay",p));
            Require((long)Sql("SELECT count(*) FROM revora_clan_war_awards WHERE uid=@uid AND paid_at IS NULL AND soul_uid IS NULL AND soul_type<>0","uid",(long)p.Entity.UID)==1);
        }
        for(int i=0;i<3;i++)Require(people[i].Entity.ConquerPoints==expectedCp[i] && people[i].Entity.RevoraPoint==expectedRvp[i]);
        Sql("CREATE TABLE revora_clan_process_probe(uid bigint PRIMARY KEY,items text,cp bigint,rvp bigint,receipts text)");
        foreach(var p in people) {
            Require(EntityTable.SaveEntity(p));
            Sql("INSERT INTO revora_clan_process_probe(uid,items,cp,rvp,receipts) VALUES(@uid,@items,@cp,@rvp,@receipts)","uid",(long)p.Entity.UID,"items",CtfItems(p),"cp",(long)p.Entity.ConquerPoints,"rvp",(long)p.Entity.RevoraPoint,"receipts",ClanReceipts(p));
        }
        // Admit a new match normally, then exit before its deadline/settlement.
        clanProbeNow=new DateTime(2034,1,13,3,33,0,DateTimeKind.Utc);
        var interrupted=ClanWar.Tournaments[0];
        typeof(ClanWar.ClanTournament).GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(interrupted,new object[]{clanProbeNow,20340113,false});
        Require(interrupted.Open);
        var envoy=Kernel.Maps[1002].Npcs[101895];
        foreach(var p in people.Take(2)) {
            p.Entity.MapID=1002;p.Entity.X=envoy.X;p.Entity.Y=(ushort)(envoy.Y+1);
            foreach(byte action in new byte[]{6,8}) {
                var frame=new byte[148];Writer.WriteUInt16(140,0,frame);Writer.WriteUInt16(1313,2,frame);frame[4]=action;Writer.WriteUInt32(envoy.UID,12,frame);Encoding.ASCII.GetBytes("TQClient").CopyTo(frame,140);
                ClanWar.Process(frame,p);
            }
            Require(p.Entity.MapID==ClanWar.GetMap(ClanWar.ClanArena.TwinCityClan));
            Require(EntityTable.SaveEntity(p));
        }
        work.Invoke(interrupted,new object[]{0});Require(interrupted.Open);
        Require((long)Sql("SELECT count(*) FROM revora_clan_war_entries e JOIN revora_clan_wars w USING(run) WHERE w.day=20340113 AND w.state='open' AND e.admitted")==2);
        Require((long)Sql("SELECT count(*) FROM revora_clan_war_awards a JOIN revora_clan_wars w USING(run) WHERE w.day=20340113")==0);
        System.Console.WriteLine("[clan-process] clan-capture-passed");Environment.Exit(0);
    }
    static string ClanReceipts(GameState p) {
        return (string)Sql("SELECT COALESCE(string_agg(row_to_json(a)::text, ';' ORDER BY soul_uid), 'empty') FROM revora_clan_war_awards a WHERE uid=@uid","uid",(long)p.Entity.UID);
    }
    static void ClanDeferredEntry(GameState p) {
        var npc=Kernel.Maps.Values.SelectMany(m=>m.Npcs.Values).Single(n=>n.UID==101910);
        var map=Kernel.Maps[npc.MapID];bool found=false;
        for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)if((dx!=0 || dy!=0) && map.Floor[npc.X+dx,npc.Y+dy,Revora.Game.MapObjectType.Player]) {
            p.Entity.MapID=npc.MapID;p.Entity.X=(ushort)(npc.X+dx);p.Entity.Y=(ushort)(npc.Y+dy);found=true;
        }
        Require(found);
        foreach(byte action in new byte[]{6,8}) {
            var frame=new byte[148];Writer.WriteUInt16(140,0,frame);Writer.WriteUInt16(1313,2,frame);frame[4]=action;Writer.WriteUInt32(npc.UID,12,frame);Encoding.ASCII.GetBytes("TQClient").CopyTo(frame,140);ClanWar.Process(frame,p);
        }
        Require(p.Entity.MapID==ClanWar.GetMap(ClanWar.ClanArena.BirdIslandClan));
    }
    static void ClanVerify(bool paid=false) {
        ClanEnvoys();
        Require((long)Sql("SELECT count(*) FROM revora_clan_envoy_probe p JOIN npcs n USING(id) WHERE n.cellx=p.cellx AND n.celly=p.celly AND n.mapid=1002")==2);
        Require(!ClanWar.Tournaments[0].Open);
        Require((long)Sql("SELECT count(*) FROM revora_clan_wars WHERE day=20340113 AND state='cancelled' AND winner=0 AND ended_at IS NOT NULL")==1);
        Require((long)Sql("SELECT count(*) FROM revora_clan_war_awards a JOIN revora_clan_wars w USING(run) WHERE w.day=20340113")==0);
        // Same-day replay cannot reopen the cancelled run.
        clanProbeNow=new DateTime(2034,1,13,3,33,0,DateTimeKind.Utc);
        Feature("ClanWarResults").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>clanProbeNow));
        typeof(ClanWar.ClanTournament).GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ClanWar.Tournaments[0],new object[]{clanProbeNow,20340113,false});
        Require(!ClanWar.Tournaments[0].Open);
        for(int i=0;i<3;i++) {
            var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(p.Entity.ClanId==50000);
            if(i<2 && !paid) {
                Require(p.Entity.MapID==ClanWar.GetMap(ClanWar.ClanArena.TwinCityClan));
                Call(typeof(ClanWar),"RecoverLocation",p);
                Require(p.Entity.MapID==1002 && p.Entity.X==301 && p.Entity.Y==279);
                Require(EntityTable.SaveEntity(p));
                Require((bool)Sql("SELECT mapid=1002 AND x=301 AND y=279 FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID));
            }
            Call(Feature("ClanWarResults"),"Pay",p);Call(Feature("ClanWarResults"),"Pay",p);
            Require(CtfItems(p)==(string)Sql("SELECT items FROM revora_clan_process_probe WHERE uid=@uid","uid",(long)p.Entity.UID));
            Require(p.Entity.ConquerPoints==(long)Sql("SELECT cp FROM revora_clan_process_probe WHERE uid=@uid","uid",(long)p.Entity.UID)
                && p.Entity.RevoraPoint==(long)Sql("SELECT rvp FROM revora_clan_process_probe WHERE uid=@uid","uid",(long)p.Entity.UID));
            Require(ClanReceipts(p)==(string)Sql("SELECT receipts FROM revora_clan_process_probe WHERE uid=@uid","uid",(long)p.Entity.UID));
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id BETWEEN 10305 AND 10309","uid",(long)p.Entity.UID)==(i<2?5:0));
            if(i<2 && !paid) {
                Require(p.Inventory.Count==40);
                long cp=(long)Sql("SELECT cp FROM revora_clan_war_awards WHERE uid=@uid AND paid_at IS NULL","uid",(long)p.Entity.UID);
                long rvp=(long)Sql("SELECT rvp FROM revora_clan_war_awards WHERE uid=@uid AND paid_at IS NULL","uid",(long)p.Entity.UID);
                uint soulType=Convert.ToUInt32(Sql("SELECT soul_type FROM revora_clan_war_awards WHERE uid=@uid AND paid_at IS NULL","uid",(long)p.Entity.UID));
                var before=p.Inventory.Objects.Select(x=>x.UID).ToArray();long oldCp=p.Entity.ConquerPoints,oldRvp=p.Entity.RevoraPoint;
                foreach(var filler in p.Inventory.Objects.Where(x=>x.ID==611004).ToArray())Require(p.Inventory.Remove(filler,Revora.Game.Enums.ItemUse.Remove));
                Require((bool)Call(Feature("ClanWarResults"),"Pay",p));
                Require(!(bool)Call(Feature("ClanWarResults"),"Pay",p));
                Require(p.Entity.ConquerPoints==oldCp+cp && p.Entity.RevoraPoint==oldRvp+rvp && p.Inventory.Count==11);
                var soul=p.Inventory.Objects.Single(x=>!before.Contains(x.UID));
                Require(soul.ID==soulType && soul.Bound && soul.Days==0 && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7);
                Require((long)Sql("SELECT count(*) FROM revora_clan_war_awards WHERE uid=@uid AND soul_uid=@soul AND paid_at IS NOT NULL","uid",(long)p.Entity.UID,"soul",(long)soul.UID)==1);
                Require(EntityTable.SaveEntity(p));
                Sql("UPDATE revora_clan_process_probe SET items=@items,cp=@cp,rvp=@rvp,receipts=@receipts WHERE uid=@uid","uid",(long)p.Entity.UID,"items",CtfItems(p),"cp",(long)p.Entity.ConquerPoints,"rvp",(long)p.Entity.RevoraPoint,"receipts",ClanReceipts(p));
            }
            if(paid)Require(p.Inventory.Count==(i<2?11:0) && p.Entity.MapID==1002);
        }
        for(int arena=0;arena<10;arena++){var territory=ClanWar.Tournaments[arena].Client;Require(territory.UID==50000 && territory.OccupationDays==(arena==9?2u:1u));}
        Require((long)Sql("SELECT count(*) FROM revora_clan_war_awards")==22);
        Require((long)Sql("SELECT count(*) FROM revora_clan_war_awards WHERE paid_at IS NULL")==0);
        System.Console.WriteLine("[clan-process] "+(paid?"clan-paid":"clan-verify")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Clan process did not close.");
    }
}
