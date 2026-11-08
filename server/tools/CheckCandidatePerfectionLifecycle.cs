using System;
using System.Linq;
using Revora;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static string BoostSnapshot() {
        return Convert.ToString(Sql(@"SELECT json_build_object('wallet',(SELECT row_to_json(r) FROM
(SELECT conquerpoints,boundcps,revorapoint FROM entities WHERE uid=@uid) r),
'items',(SELECT json_agg(r ORDER BY uid) FROM (SELECT uid,id,bound,position,perfectionlevel,perfectionprogress,perfectionownerguid,perfectionowner FROM items WHERE entityid=@uid) r),
'receipts',(SELECT json_agg(r ORDER BY item) FROM (SELECT * FROM revora_perfection_boosts WHERE uid=@uid) r))::text","uid",(long)FirstUid));
    }
    static void PerfectionWire(bool reload,bool rollback=false) {
        Server.GameServer.OnClientConnect+=socket=>{
            var receive=socket.Callback;
            socket.Callback=(buffer,length,connection)=>{
                var actor=connection.Connector as Revora.Client.GameState;
                bool login=actor!=null && actor.Action!=2;
                var elapsed=System.Diagnostics.Stopwatch.StartNew();
                try{receive(buffer,length,connection);}
                finally{if(login)System.Console.WriteLine("[wire-timing] login-receive-ms="+elapsed.ElapsedMilliseconds+" alive="+connection.Alive+" action="+(actor==null?0:actor.Action));}
            };
        };
        var clock=Feature("PerfectionTempering").GetField("Clock",Hidden);
        clock.SetValue(null,(Func<DateTime>)(()=>new DateTime(2033,1,1,0,0,0,DateTimeKind.Utc)));
        if(rollback){Player(Kernel.Maps[1002]);Sql("UPDATE entities SET class=75,reborn=2,conquerpoints=1000,boundcps=10 WHERE uid=@uid","uid",(long)FirstUid);}
        nextUid=FirstUid;var p=Player(Kernel.Maps[1002],true);
        if(rollback){
            var smith=p.Map.Npcs[5];p.Entity.X=smith.X;p.Entity.Y=(ushort)(smith.Y+2);
            foreach(uint type in new uint[]{611004,612004}) {
                var buy=new ItemUsage(true){UID=5,dwParam=type,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC}.ToArray();
                Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,buy.Length-8,buy);ItemUsage.Process(buy,p);
                var item=p.Inventory.Objects.Single(i=>i.ID==type);
                ItemHandler.EquipItem(new ItemUsage(true){UID=item.UID,dwParam=type==611004?4u:5u,Action=ItemUsage.Mode.EquipItem},p);
            }
            Require(p.Equipment.TryGetItem(4)!=null && p.Equipment.TryGetItem(5)!=null);Require(EntityTable.SaveEntity(p));
            // Supply binding to cover both wallet policies; rewards/progress are not seeded.
            Sql("UPDATE items SET bound=1 WHERE entityid=@uid AND position=5","uid",(long)FirstUid);
        }else Require(System.IO.File.ReadAllText(reload?"perfection-boost.snapshot":"perfection-rollback.snapshot")==BoostSnapshot());
        string password=Guid.NewGuid().ToString("N");var account=new AccountTable("TeamProbe0");
        if(!account.exists){account.Password=password;account.State=AccountTable.AccountState.Player;account.EntityID=FirstUid;account.Insert();}
        account.Password=password;account.Save();Require(account.HasSrpVerifier && account.EntityID==FirstUid);
        Sql("UPDATE entities SET owner='TeamProbe0' WHERE uid=@uid","uid",(long)FirstUid);Kernel.GamePool.Remove(FirstUid);
        var start=new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CandidateTeamWireProbe.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Environment.CurrentDirectory};
        start.EnvironmentVariables["REVORA_LOGIN_TIMING"]="1";start.EnvironmentVariables["REVORA_PIRATE_WIRE"]="1";start.EnvironmentVariables["REVORA_PERFECTION_WIRE"]="1";start.EnvironmentVariables["REVORA_TEAM_WIRE_ROLE"]="0";start.EnvironmentVariables["REVORA_CANDIDATE_ACCOUNT_PASSWORD"]=password;start.EnvironmentVariables.Remove("REVORA_DATABASE_PASSWORD");
        using(var child=System.Diagnostics.Process.Start(start)) {
            password=null;start.EnvironmentVariables.Remove("REVORA_CANDIDATE_ACCOUNT_PASSWORD");var errors=child.StandardError.ReadToEndAsync();
            try {
                TeamWireRead(child,"[team-wire] ready");p=Kernel.GamePool[FirstUid];Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake);
                foreach(byte slot in new byte[]{4,5}) {
                    var item=p.Equipment.TryGetItem(slot);Require(item!=null && item.Bound==(slot==5));
                    if(rollback) {
                        string before=BoostSnapshot();uint cpBefore=p.Entity.ConquerPoints,boundBefore=p.Entity.BoundCps;
                        var hook=Feature("PerfectionTempering").GetField("BeforeCommit",Hidden);int failures=0;
                        hook.SetValue(null,(Action<Revora.Client.GameState>)(actor=>{
                            Require(actor.Entity.UID==FirstUid);System.Threading.Interlocked.Increment(ref failures);
                            throw new InvalidOperationException("Private Boost rollback fixture.");
                        }));
                        try{TeamWireSend(child,"perfection-boost:"+item.UID+":24:"+cpBefore+":"+boundBefore+":0");
                            Require(System.Threading.SpinWait.SpinUntil(()=>(bool)p.Entity.GetType().GetField("PersistenceRequiresReload",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(p.Entity) && p.Action==0,5000));}
                        finally{hook.SetValue(null,null);}
                        Require(failures==1 && before==BoostSnapshot());
                        Require(item.PerfectionLevel==0 && item.PerfectionProgress==0 && item.PerfectionOwnerGuid==0);
                        Require(p.Entity.ConquerPoints==cpBefore && p.Entity.BoundCps==boundBefore);
                        System.IO.File.WriteAllText("perfection-rollback.snapshot",before);break;
                    }
                    // The same frozen one-second window tests the persisted cooldown, not indefinite deduplication.
                    uint cp=reload?962u:slot==4?976u:962u,bound=reload?0u:slot==4?10u:0u;
                    TeamWireSend(child,"perfection-boost:"+item.UID+":24:"+cp+":"+bound);
                    TeamWireSend(child,"perfection-boost:"+item.UID+":24:"+cp+":"+bound);
                    Require(item.PerfectionLevel==1 && item.PerfectionProgress==0 && item.PerfectionOwnerGuid==FirstUid);
                    Require(p.Entity.ConquerPoints==cp && p.Entity.BoundCps==bound);
                }
                if(!rollback) {
                Require((bool)Sql("SELECT count(*)=2 AND sum(cost)=48 AND sum(bound_cost)=10 FROM revora_perfection_boosts WHERE uid=@uid","uid",(long)FirstUid));
                if(!reload)System.IO.File.WriteAllText("perfection-boost.snapshot",BoostSnapshot());
                Require(System.IO.File.ReadAllText("perfection-boost.snapshot")==BoostSnapshot());
                Require(EntityTable.SaveEntity(p));}
                child.StandardInput.WriteLine("stop");child.StandardInput.Flush();TeamWireRead(child,"[team-wire] stopped");Require(child.WaitForExit(5000) && child.ExitCode==0);
            }finally{if(!child.HasExited){child.Kill();child.WaitForExit(5000);}if(errors.Wait(1000)&&!string.IsNullOrEmpty(errors.Result))System.Console.Error.WriteLine(errors.Result);}
        }
        System.Console.WriteLine("[perfection-process] perfection-wire"+(rollback?"-rollback":reload?"-reload":"")+"-passed");Server.CommandsAI("@close");throw new Exception("Perfection wire did not close.");
    }
}
