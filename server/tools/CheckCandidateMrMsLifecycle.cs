using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Game;
using Revora.Network.GamePackets;

// Shares the bounded private process/profile harness; no live calendar mutation.
internal static partial class CheckCandidateArenaLifecycle
{
    static DateTime mrmsNow;
    static bool mrmsExtendedFixture;
    static string mrmsSeason;
    static void PrepareMrMs(string phase) {
        mrmsExtendedFixture=phase.StartsWith("mrms-extended-");
        mrmsNow=phase.EndsWith("-register")?DateTime.UtcNow:
            DateTime.SpecifyKind((DateTime)Sql("SELECT closes AT TIME ZONE 'UTC' FROM revora_mrms_process_probe"),DateTimeKind.Utc);
        mrmsSeason=phase.EndsWith("-register")?(string)Call(Feature("MrMs2016"),"SeasonOf",mrmsNow):
            (string)Sql("SELECT season FROM revora_mrms_process_probe");
        Feature("MrMs2016").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>mrmsNow));
        GameAssembly.GetType("Revora.TitleRequirements",true).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>mrmsNow));
    }
    static void MrMsRegister() {
        Require((long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN 4294903001 AND 4294903007")==0);
        Require((long)Sql("SELECT count(*) FROM revora_mrms_entrants WHERE season=@season","season",mrmsSeason)==0);
        // Advance only through the actual due-calendar service, without seeding standings.
        for(int n=0;n<120 && (bool)Call(Feature("MrMs2016"),"ProcessDue");n++){}
        DateTime close=(DateTime)Call(Feature("MrMs2016"),"NextMonth",mrmsNow);
        Require(DateTime.SpecifyKind((DateTime)Sql("SELECT next_close AT TIME ZONE 'UTC' FROM revora_mrms_calendar WHERE id=1"),DateTimeKind.Utc)==close);
        Sql("CREATE TABLE revora_mrms_process_probe(season text NOT NULL,closes timestamptz NOT NULL,fingerprint text); INSERT INTO revora_mrms_process_probe(season,closes) VALUES(@season,@close)","season",mrmsSeason,"close",close);
        var town=Kernel.Maps[1002];Revora.Interfaces.INpc actor;
        Require(town.Npcs.TryGetValue(380,out actor) && actor.MapID==1002);
        Action<GameState,byte> talk=(p,option)=>{
            p.ActiveNpc=380;p.Entity.X=actor.X;p.Entity.Y=actor.Y;
            Npcs.GetDialog(new NpcRequest(true){NpcID=380,OptionID=option},p);
        };
        var entrants=new GameState[4];
        for(int i=0;i<4;i++) {
            entrants[i]=Player(town,false,(ushort)(i<2?2001:1003));
            talk(entrants[i],0);talk(entrants[i],220);talk(entrants[i],222);
        }
        Require((long)Sql("SELECT count(*) FROM revora_mrms_entrants WHERE season=@season","season",mrmsSeason)==4);
        var voters=new[]{Player(town),Player(town),Player(town)};
        Action<GameState,GameState,byte> vote=(voter,candidate,flower)=>{
            talk(voter,flower);var ids=(uint[])Call(Feature("MrMs2016"),"PinnedCandidates",voter);
            int index=Array.IndexOf(ids,candidate.Entity.UID);Require(index>=0 && index<8);
            talk(voter,(byte)(226+index));
        };
        foreach(int first in new[]{0,2}) {
            vote(voters[0],entrants[first],224);vote(voters[1],entrants[first],224);
            vote(voters[0],entrants[first],225);vote(voters[2],entrants[first+1],224);
        }
        Require((long)Sql("SELECT count(*) FROM revora_mrms_votes WHERE season=@season","season",mrmsSeason)==8);
        Require((long)Sql("SELECT count(*) FROM revora_mrms_awards WHERE season=@season","season",mrmsSeason)==0);
        System.Console.WriteLine("[mrms-process] mrms-register-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Mr/Ms register process did not exit.");
    }
    static void MrMsVerify() {
        // The real world callback also calls this; the same calendar lock owns both.
        Call(Feature("MrMs2016"),"Tick");
        Require((long)Sql("SELECT count(*) FROM revora_mrms_awards WHERE season=@season","season",mrmsSeason)==6);
        Require((long)Sql("SELECT count(*) FROM revora_mrms_votes WHERE season=@season","season",mrmsSeason)==8);
        bool replay=Sql("SELECT fingerprint FROM revora_mrms_process_probe")!=DBNull.Value;
        int[] keys={20202020,20212021,20232023,20242024};
        for(int i=0;i<4;i++) {
            var p=Player(Kernel.Maps[1002],true);new TitleStorage().Login(p);
            if(replay)Require(p.Entity.EquippedTitle==keys[i] && (i%2!=0 || p.Entity.EquippedWing==60056005));
            foreach(int key in i%2==0?new[]{keys[i],60056005}:new[]{keys[i]})
                new TitleStorage{Info=new TitleStorageProto{Type=key/10000,Id=key%10000,ActionId=TitleStorage.Action.Equip,
                    Value=new TitleValue{Type=key/10000,Id=key%10000}}}.Handle(p);
            Require(p.Entity.EquippedTitle==keys[i] && (i%2!=0 || p.Entity.EquippedWing==60056005));
            Require(!(bool)typeof(Entity).GetField("PersistenceRequiresReload",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(p.Entity));
        }
        Call(Feature("MrMs2016"),"Tick");
        Require((long)Sql("SELECT count(*) FROM revora_event_awards WHERE source='mrms-2016-revora-v1' AND source_key LIKE @key AND expires_at IS NULL","key",mrmsSeason+":%")==6);
        Require((long)Sql("SELECT count(*) FROM revora_mrms_awards WHERE season=@season AND award_recorded","season",mrmsSeason)==6);
        string fingerprint=(string)Sql("SELECT md5(string_agg(uid::text||':'||title_key::text||':'||award_recorded::text,',' ORDER BY uid,title_key)) FROM revora_mrms_awards WHERE season=@season","season",mrmsSeason);
        object before=Sql("SELECT fingerprint FROM revora_mrms_process_probe");
        if(before==DBNull.Value)Sql("UPDATE revora_mrms_process_probe SET fingerprint=@value","value",fingerprint);
        else Require((string)before==fingerprint);
        System.Console.WriteLine("[mrms-process] mrms-verify-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Mr/Ms verify process did not exit.");
    }
}
