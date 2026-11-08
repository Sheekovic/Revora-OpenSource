using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Revora;
using Revora.Database;
using Revora.Game;

internal static partial class CheckCandidateArenaLifecycle {
    static string SpunFingerprint(){return (string)Sql("SELECT jsonb_build_object('run',(SELECT to_jsonb(q) FROM revora_spun_gold q WHERE uid=4294906004 AND life=0),'items',(SELECT jsonb_agg(jsonb_build_array(uid,id,bound,plus,bless,socketone,sockettwo,position,warehouse) ORDER BY uid) FROM items WHERE entityid=4294906004))::text");}
    static void SpunProcess(bool reload) {
        const uint uid=4294906004;
        if(!reload)Require((long)Sql("SELECT count(*) FROM revora_spun_gold WHERE uid=@uid","uid",(long)uid)==0);
        else Require((bool)Sql("SELECT process<>@pid AND started<>pg_postmaster_start_time() FROM revora_spun_wire_checkpoint","pid",Process.GetCurrentProcess().Id));
        string password=Guid.NewGuid().ToString("N");var account=new AccountTable("SupervisorProbe");Require(account.exists && account.EntityID==uid && account.State==AccountTable.AccountState.Player);account.Password=password;account.Save();
        var start=new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CandidateKingdomWireProbe.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Environment.CurrentDirectory};
        start.EnvironmentVariables["REVORA_CANDIDATE_ACCOUNT_PASSWORD"]=password;start.EnvironmentVariables["REVORA_SUPERVISOR_WIRE"]="1";start.EnvironmentVariables["REVORA_SPUN_WIRE"]="1";start.EnvironmentVariables.Remove("REVORA_DATABASE_PASSWORD");
        var roll=Feature("SpunGoldStore").GetField("Roll",Hidden);object previous=roll.GetValue(null);roll.SetValue(null,(Func<int,int>)(max=>0));
        try{using(var child=Process.Start(start)) {
            start.EnvironmentVariables.Remove("REVORA_CANDIDATE_ACCOUNT_PASSWORD");password=null;var errors=child.StandardError.ReadToEndAsync();
            try {
                TeamWireRead(child,"[spun-wire] ready");var p=Kernel.GamePool[uid];Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake && p.Entity.Reborn==0);
                Action<string> send=command=>{child.StandardInput.WriteLine(command);child.StandardInput.Flush();TeamWireRead(child,"[spun-wire] done "+command);};
                Action<ushort,uint,byte> talk=(map,npc,option)=>{var actor=Kernel.Maps[map].Npcs[npc];p.Entity.Teleport(map,actor.X,(ushort)(actor.Y+2));send("npc:"+npc+":"+option);};
                Action<uint> fight=family=>{
                    var body=Kernel.Maps[1011].Entities.Values.First(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==family && !e.Dead);
                    for(int n=0;n<100 && !body.Dead;n++){p.Entity.Teleport(1011,body.X,body.Y);p.Screen.Add(body);send("hit:"+body.UID);System.Threading.Thread.Sleep(550);}
                    Require(body.Dead && !p.Entity.Dead);
                };
                if(!reload) {
                    talk(1002,900071727,61);talk(1011,4464,1);fight(7);talk(1011,4464,1);talk(1011,4465,1);
                    p.Entity.Teleport(1011,100,430);send("use:"+p.Inventory.Objects.Single(i=>i.ID==729972).UID);
                    fight(9921723);Call(Feature("SpunGoldQuest"),"Tick");send("use:"+p.Inventory.Objects.Single(i=>i.ID==729973).UID);
                    talk(1011,4464,1);talk(1002,900071727,61);
                    Require((long)Sql("SELECT count(*) FROM revora_spun_gold WHERE uid=@uid AND stage=8 AND cardinality(rewards)=2","uid",(long)uid)==1 && EntityTable.SaveEntity(p));
                    Sql("CREATE TABLE revora_spun_wire_checkpoint(process integer,started timestamptz,fingerprint text);INSERT INTO revora_spun_wire_checkpoint VALUES(@pid,pg_postmaster_start_time(),@fingerprint)","pid",Process.GetCurrentProcess().Id,"fingerprint",SpunFingerprint());
                } else Require(SpunFingerprint()==(string)Sql("SELECT fingerprint FROM revora_spun_wire_checkpoint"));
                talk(1002,900071727,61);Require(SpunFingerprint()==(string)Sql("SELECT fingerprint FROM revora_spun_wire_checkpoint"));
                var rewards=(long[])Sql("SELECT rewards FROM revora_spun_gold WHERE uid=@uid AND life=0","uid",(long)uid);
                foreach(long reward in rewards){var item=p.Inventory.Objects.Single(i=>i.UID==(uint)reward);Require(item.Bound);send("reward:"+item.UID+":"+item.ID);}
                child.StandardInput.WriteLine("stop");child.StandardInput.Flush();TeamWireRead(child,"[supervisor-wire] passed");Require(child.WaitForExit(5000) && child.ExitCode==0);
            } finally {if(!child.HasExited){child.Kill();child.WaitForExit();}System.Console.Error.WriteLine(errors.Result);}
        }}finally{roll.SetValue(null,previous);}
        System.Console.WriteLine("[spun-process] spun-wire"+(reload?"-reload":"")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Spun wire did not close.");
    }
}
