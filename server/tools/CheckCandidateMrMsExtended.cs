using System;
using Revora;
using Revora.Client;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static void MrMsExtendedRegister() {
        for(int n=0;n<120 && (bool)Call(Feature("MrMs2016"),"ProcessDue");n++){}
        DateTime close=(DateTime)Call(Feature("MrMs2016"),"NextMonth",mrmsNow);
        Sql("CREATE TABLE revora_mrms_process_probe(season text NOT NULL,closes timestamptz NOT NULL,fingerprint text); INSERT INTO revora_mrms_process_probe(season,closes) VALUES(@season,@close)","season",mrmsSeason,"close",close);
        var entrants=new GameState[64];
        for(int i=0;i<64;i++) {
            entrants[i]=Player(Kernel.Maps[1002],false,(ushort)(i<32?2001:1003));
            HeartTalk(entrants[i],380,222);
        }
        Require((long)Sql("SELECT count(*) FROM revora_mrms_entrants WHERE season=@season","season",mrmsSeason)==64);
        Require((bool)Sql("SELECT extended_placements FROM revora_mrms_seasons WHERE season=@season","season",mrmsSeason));
        for(int contest=0;contest<2;contest++)for(int rank=0;rank<31;rank++) {
            var voter=entrants[(1-contest)*32+rank];uint candidate=entrants[contest*32+rank].Entity.UID;
            HeartTalk(voter,380,224);int index=-1;
            for(int page=0;page<8;page++) {
                index=Array.IndexOf((uint[])Call(Feature("MrMs2016"),"PinnedCandidates",voter),candidate);
                if(index>=0)break;
                HeartTalk(voter,380,234);
            }
            Require(index>=0 && index<8);HeartTalk(voter,380,(byte)(226+index));
        }
        Require((long)Sql("SELECT count(*) FROM revora_mrms_votes WHERE season=@season","season",mrmsSeason)==62);
        Sql(@"CREATE FUNCTION revora_mrms_tier_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'tier probe'; END $$;
CREATE TRIGGER revora_mrms_tier_failure BEFORE INSERT ON revora_mrms_awards FOR EACH ROW WHEN(NEW.title_key=20222022) EXECUTE FUNCTION revora_mrms_tier_failure();");
        bool failed=false;try{Call(Feature("MrMs2016"),"Freeze",mrmsSeason,close);}catch{failed=true;}
        Sql("DROP TRIGGER revora_mrms_tier_failure ON revora_mrms_awards; DROP FUNCTION revora_mrms_tier_failure()");
        Require(failed && (bool)Sql("SELECT closed_at='infinity' FROM revora_mrms_seasons WHERE season=@season","season",mrmsSeason));
        Require((long)Sql("SELECT count(*) FROM revora_mrms_awards WHERE season=@season","season",mrmsSeason)==0);
        System.Console.WriteLine("[mrms-process] mrms-extended-register-passed");
        Server.CommandsAI("@close");throw new Exception("Mr/Ms extended registration did not close.");
    }
    static void MrMsExtendedVerify() {
        Call(Feature("MrMs2016"),"Tick");
        Require((long)Sql("SELECT count(*) FROM revora_mrms_awards WHERE season=@season","season",mrmsSeason)==60);
        bool replay=Sql("SELECT fingerprint FROM revora_mrms_process_probe")!=DBNull.Value;
        for(int i=0;i<64;i++) {
            var p=Player(Kernel.Maps[1002],true);new TitleStorage().Login(p);
            bool ms=i<32;int place=i%32+1;
            int key=place==1?(ms?20202020:20232023):place<=8?(ms?20212021:20242024):place<=30?(ms?20222022:20252025):0;
            if(replay)Require(p.Entity.EquippedTitle==key);
            if(key!=0)new TitleStorage{Info=new TitleStorageProto{Type=key/10000,Id=key%10000,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=key/10000,Id=key%10000}}}.Handle(p);
            Require(p.Entity.EquippedTitle==key && p.Action==2);
            Require((long)Sql("SELECT count(*) FROM revora_mrms_awards WHERE uid=@uid AND season=@season AND title_key=@key","uid",(long)p.Entity.UID,"season",mrmsSeason,"key",key)==(key==0?0:1));
        }
        Require((long)Sql("SELECT count(*) FROM revora_event_awards WHERE source='mrms-2016-revora-v1' AND source_key LIKE @key AND expires_at IS NULL","key",mrmsSeason+":%")==60);
        string saved=(string)Sql("SELECT md5(string_agg(uid::text||':'||title_key::text||':'||earned_at::text,',' ORDER BY uid,title_key)) FROM revora_event_awards WHERE source='mrms-2016-revora-v1' AND source_key LIKE @key","key",mrmsSeason+":%");
        if(replay)Require(saved==(string)Sql("SELECT fingerprint FROM revora_mrms_process_probe"));
        else Sql("UPDATE revora_mrms_process_probe SET fingerprint=@f","f",saved);
        Call(Feature("MrMs2016"),"Tick");
        Require((long)Sql("SELECT count(*) FROM revora_mrms_awards WHERE season=@season AND award_recorded","season",mrmsSeason)==60);
        System.Console.WriteLine("[mrms-process] mrms-extended-verify-passed");
        Server.CommandsAI("@close");throw new Exception("Mr/Ms extended verification did not close.");
    }
}
