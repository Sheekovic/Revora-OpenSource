using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static DateTime anniversaryNow=new DateTime(2034,1,7,19,0,0,DateTimeKind.Utc);
    static System.Diagnostics.Process anniversaryWireChild;
    static System.Threading.Tasks.Task<string> anniversaryWireErrors;
    static GameState AnniversaryWire(GameState p,string command) {
        string password=Guid.NewGuid().ToString("N");var account=new AccountTable("TeamProbe0");
        if(!account.exists){account.Password=password;account.State=AccountTable.AccountState.Player;account.EntityID=p.Entity.UID;account.Insert();}
        account.Password=password;account.Save();Require(account.HasSrpVerifier && account.EntityID==FirstUid);
        // Fixture proximity/account ownership only; earned bead and progress remain intact.
        Sql("UPDATE entities SET owner='TeamProbe0',mapid=1002,x=323,y=309 WHERE uid=@uid","uid",(long)FirstUid);
        Kernel.GamePool.Remove(p.Entity.UID);
        var start=new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CandidateTeamWireProbe.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Environment.CurrentDirectory};
        start.EnvironmentVariables["REVORA_ANNIVERSARY_WIRE"]="1";start.EnvironmentVariables["REVORA_TEAM_WIRE_ROLE"]="0";start.EnvironmentVariables["REVORA_CANDIDATE_ACCOUNT_PASSWORD"]=password;start.EnvironmentVariables.Remove("REVORA_DATABASE_PASSWORD");
        anniversaryWireChild=System.Diagnostics.Process.Start(start);start.EnvironmentVariables.Remove("REVORA_CANDIDATE_ACCOUNT_PASSWORD");password=null;
        anniversaryWireErrors=anniversaryWireChild.StandardError.ReadToEndAsync();TeamWireRead(anniversaryWireChild,"[team-wire] ready");
        TeamWireSend(anniversaryWireChild,command);return Kernel.GamePool[FirstUid];
    }
    static void AnniversaryWireStop() {
        if(anniversaryWireChild==null)return;
        try {
            anniversaryWireChild.StandardInput.WriteLine("stop");anniversaryWireChild.StandardInput.Flush();TeamWireRead(anniversaryWireChild,"[team-wire] stopped");Require(anniversaryWireChild.WaitForExit(5000) && anniversaryWireChild.ExitCode==0);
        } finally {
            if(!anniversaryWireChild.HasExited){anniversaryWireChild.Kill();anniversaryWireChild.WaitForExit(5000);}
            if(anniversaryWireErrors.Wait(1000) && !string.IsNullOrEmpty(anniversaryWireErrors.Result))System.Console.Error.WriteLine(anniversaryWireErrors.Result);
            anniversaryWireChild.Dispose();anniversaryWireChild=null;
        }
    }
    static void AnniversaryTalk(GameState p,byte option) {
        var actor=Kernel.Maps[1002].Npcs[900028];Require(actor.X==323 && actor.Y==307);
        p.BindWorldMap(1002);p.Entity.X=323;p.Entity.Y=309;p.ActiveNpc=actor.UID;
        var frame=new byte[28];Writer.WriteUInt16(20,0,frame);Writer.WriteUInt16(2032,2,frame);frame[14]=option;Writer.WriteUInt64(PacketHandler.ClientSeal,20,frame);PacketHandler.HandlePacket(frame,p);
    }
    static long AnniversarySpirits() {return (long)Sql("SELECT spirits FROM revora_anniversary_runs WHERE uid=@uid","uid",(long)FirstUid);}
    static void AnniversaryProcess(string phase) {
        string requested=phase;bool wire=phase.StartsWith("anniv-wire-");phase=phase.Replace("anniv-wire-","anniv-");
        try {
        Feature("AnniversaryQuest").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>anniversaryNow));
        Feature("DailySoulBall").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>anniversaryNow));
        Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;
        Feature("MonsterEquipmentDrops").GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max-1));
        if(phase=="anniv-start") {Player(Kernel.Maps[1002]);nextUid=FirstUid;}
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        if(phase=="anniv-start") {
            // Supply eligibility, never quest progress or rewards.
            p.Entity.Level=140;p.Entity.Reborn=2;p.Entity.VIPLevel=6;p.LastResetTime=anniversaryNow.ToLocalTime();Require(EntityTable.SaveEntity(p));
            if(wire)p=AnniversaryWire(p,"anniv-start");else AnniversaryTalk(p,8);
            Require(p.Inventory.Count==1 && p.Inventory.Objects.Single().ID==3001172 && AnniversarySpirits()==3);
            Sql("CREATE TABLE revora_anniversary_process_probe(kills integer NOT NULL,items text,rvp bigint,money bigint,study integer,bless bigint,receipt text); INSERT INTO revora_anniversary_process_probe(kills,rvp,money,study,bless) VALUES(0,@rvp,@money,@study,@bless)","rvp",(long)p.Entity.RevoraPoint,"money",(long)p.Entity.Money,"study",(int)p.Entity.SubClasses.StudyPoints,"bless",(long)p.Entity.HeavenBlessing);
        } else if(phase=="anniv-hunt") {
            int before=Convert.ToInt32(Sql("SELECT kills FROM revora_anniversary_process_probe"));Require(before>=0 && before<3750 && AnniversarySpirits()==Math.Min(15000,3+before*4));
            ushort[] worlds={1207,1208,1212,1214,1215,1217,1762,1927,1999,2054,2055,2056};
            var map=Kernel.Maps.Values.Where(m=>worlds.Contains(m.ID)).OrderByDescending(m=>m.Entities.Values.Count(e=>e.MonsterInfo!=null && e.MonsterInfo.Level>=120 && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard && !e.MonsterInfo.Reviver)).First();
            p.BindWorldMap(map.ID);
            for(int n=0;n<750;n++) {
                var mob=map.Entities.Values.FirstOrDefault(e=>!e.Dead && e.MonsterInfo!=null && e.MonsterInfo.Level>=120 && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard && !e.MonsterInfo.Reviver);
                if(mob==null) {
                    var dead=map.Entities.Values.Where(e=>e.Dead && e.MonsterInfo!=null).ToArray();Require(dead.Length>0);
                    int future=dead.Max(e=>e.DeathStamp.AddSeconds(e.MonsterInfo.RespawnTime+1).AllMilliseconds());
                    Call(typeof(Map),"_timerCallBack",map,unchecked((int)future));
                    mob=map.Entities.Values.First(e=>!e.Dead && e.MonsterInfo!=null && e.MonsterInfo.Level>=120 && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard && !e.MonsterInfo.Reviver);
                }
                EliminationHit(p,mob);
                Require(AnniversarySpirits()==Math.Min(15000,3+(before+n+1)*4));
            }
            Sql("UPDATE revora_anniversary_process_probe SET kills=@kills","kills",before+750);
            System.Console.WriteLine("[anniv-process] earned-kills="+(before+750)+" map="+map.ID);
        } else if(phase=="anniv-claim") {
            Require(AnniversarySpirits()==15000 && (int)Sql("SELECT kills FROM revora_anniversary_process_probe")==3750);
            if(wire)p=AnniversaryWire(p,"anniv-claim");else {var bead=p.Inventory.Objects.Single(i=>i.ID==3001172);ItemHandler.UseItem(bead,p);ItemHandler.UseItem(bead,p);}
            Require(p.Inventory.Count==21 && p.Inventory.Objects.Count(i=>i.ID==3006048)==20);
            var soul=p.Inventory.Objects.Single(i=>i.ID!=3006048);Require(soul.Bound && soul.Days==0 && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7);
            Require(p.Entity.RevoraPoint==(long)Sql("SELECT rvp+50000 FROM revora_anniversary_process_probe") && p.Entity.Money==(ulong)(long)Sql("SELECT money+50000000 FROM revora_anniversary_process_probe"));
            Require(p.Entity.SubClasses.StudyPoints==(int)Sql("SELECT study+100 FROM revora_anniversary_process_probe") && p.Entity.HeavenBlessing==(long)Sql("SELECT bless+172800 FROM revora_anniversary_process_probe"));
            Sql("UPDATE revora_anniversary_process_probe SET items=@items,receipt=(SELECT row_to_json(r)::text FROM revora_anniversary_runs r WHERE uid=@uid),rvp=@rvp,money=@money,study=@study,bless=@bless","items",CtfItems(p),"uid",(long)FirstUid,"rvp",(long)p.Entity.RevoraPoint,"money",(long)p.Entity.Money,"study",(int)p.Entity.SubClasses.StudyPoints,"bless",(long)p.Entity.HeavenBlessing);
        } else {
            Require(CtfItems(p)==(string)Sql("SELECT items FROM revora_anniversary_process_probe"));
            Require((bool)Sql("SELECT p.receipt=row_to_json(r)::text AND r.completed_day<>'' AND cardinality(r.reward_uids)=21 FROM revora_anniversary_process_probe p CROSS JOIN revora_anniversary_runs r WHERE r.uid=@uid","uid",(long)FirstUid));
            Require(p.Entity.RevoraPoint==(long)Sql("SELECT rvp FROM revora_anniversary_process_probe") && p.Entity.Money==(ulong)(long)Sql("SELECT money FROM revora_anniversary_process_probe") && p.Entity.SubClasses.StudyPoints==(int)Sql("SELECT study FROM revora_anniversary_process_probe") && p.Entity.HeavenBlessing==(long)Sql("SELECT bless FROM revora_anniversary_process_probe"));
            AnniversaryTalk(p,8);Require(CtfItems(p)==(string)Sql("SELECT items FROM revora_anniversary_process_probe"));
        }
        Require(EntityTable.SaveEntity(p));AnniversaryWireStop();System.Console.WriteLine("[anniv-process] "+requested+"-passed");Environment.Exit(0);
        } finally {AnniversaryWireStop();}
    }
}
