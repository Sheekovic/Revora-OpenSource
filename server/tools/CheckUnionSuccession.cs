using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static class CheckUnionSuccession {
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Static;
    static Type fixtures;static int checks;
    static object Invoke(Type type,string method,params object[] args){try{return type.GetMethods(Hidden).Single(m=>m.Name==method && m.GetParameters().Length==args.Length).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static object Helper(string name,params object[] args){return Invoke(fixtures,name,args);}
    static object Sql(string sql,params object[] values){using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(sql,c)){for(int i=0;i<values.Length;i+=2)q.Parameters.AddWithValue((string)values[i],values[i+1]);return q.ExecuteScalar();}}}
    static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;Console.WriteLine("PASS "+name);}
    static long Count(string sql){return Convert.ToInt64(Sql(sql));}
    static byte[] Packet(string name){var text=System.Text.Encoding.Default.GetBytes(name);var bytes=new byte[28+text.Length];Revora.Network.Writer.WriteUInt16((ushort)(bytes.Length-8),0,bytes);Revora.Network.Writer.WriteUInt16((ushort)PacketID.MsgLeagueOpt,2,bytes);Revora.Network.Writer.WriteUInt16(7,4,bytes);bytes[18]=1;bytes[19]=(byte)text.Length;Array.Copy(text,0,bytes,20,text.Length);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,bytes.Length-8,bytes);return bytes;}
    static void Create(GameState p,byte[] bytes){Revora.Network.GamePackets.Union.Union.Prosses(bytes,p);}
    static void Action(GameState p,ushort action,uint target,uint parameter=0){var bytes=Packet("");Revora.Network.Writer.WriteUInt16(action,4,bytes);Revora.Network.Writer.WriteUInt32(target,10,bytes);Revora.Network.Writer.WriteUInt32(parameter,14,bytes);Create(p,bytes);}
    static void NamedAction(GameState p,ushort action,string name,uint rank){var bytes=Packet(name);Revora.Network.Writer.WriteUInt16(action,4,bytes);Revora.Network.Writer.WriteUInt32(rank,14,bytes);Create(p,bytes);}
    static int Main(){
        var b=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION"));Environment.SetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION",null);
        if(b.Host!="127.0.0.1" || b.Port!=25439 || b.Database!="revora_candidate_game")throw new InvalidOperationException("Fixed private fixture required.");
        DataHolder.ConnectionString=b.ConnectionString;string prior=Environment.CurrentDirectory;bool owned=false;
        try {
            Sql("CREATE SCHEMA revora_union_succession_fixture;COMMENT ON SCHEMA revora_union_succession_fixture IS 'revora-owned-union-succession-v1'");owned=true;
            b.SearchPath="revora_union_succession_fixture";DataHolder.ConnectionString=b.ConnectionString;
            const string runtime=@"C:\Revora\DisposablePostgres\moon-box-candidate\game";Environment.CurrentDirectory=runtime;
            foreach(string table in new[]{"entities","items","titles","skills","arena","teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding","chi","npcs","sobnpcs","monsterspawns","furniture","unions"})Sql(table=="entities"?"CREATE TABLE entities AS TABLE public.entities WITH NO DATA":"CREATE TABLE "+table+" (LIKE public."+table+" INCLUDING ALL)");
            Sql("CREATE TABLE guilds(id bigint PRIMARY KEY,wins bigint NOT NULL,losts bigint NOT NULL,leadername text NOT NULL DEFAULT '',unionid bigint NOT NULL DEFAULT 0);CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
            fixtures=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"KingdomField.Helpers.exe")).GetType("PotencyHuntTests",true);
            ConquerItemInformation.Load();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
            Invoke(typeof(Entity).Assembly.GetType("Revora.Game.Features.ProgressionEvidence",true),"Initialize");Invoke(typeof(Entity).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkLedger",true),"Initialize");
            Invoke(fixtures.Assembly.GetType("CompanionTests",true),"Reset");var town=(Map)Helper("World",runtime,(ushort)1002);Server.Vars=new VariableVault();
            Sql("ALTER TABLE entities ALTER COLUMN unionid SET DEFAULT 0,ALTER COLUMN offical SET DEFAULT 0,ALTER COLUMN harem SET DEFAULT 0,ALTER COLUMN guards SET DEFAULT 0;CREATE TABLE configuration(unionid bigint NOT NULL);INSERT INTO configuration VALUES(100)");
            var a=(GameState)Helper("PrizeLeader",921u);var bOwner=(GameState)Helper("PrizeLeader",922u);var failed=(GameState)Helper("PrizeLeader",923u);
            var member=(GameState)Helper("PurseMember",a,Enums.GuildMemberRank.Member);
            Check(a.Account.State==AccountTable.AccountState.Player && !a.Fake,"creation uses an ordinary non-GM guild leader");
            foreach(byte[] invalid in new[]{new byte[0],new byte[19],new byte[600],Packet(""),Packet("   "),Packet("bad\nname")})Create(a,invalid);
            var damaged=Packet("BadLength");damaged[19]=255;Create(a,damaged);damaged=Packet("BadSeal");damaged[damaged.Length-1]^=1;Create(a,damaged);
            Check(Count("SELECT count(*) FROM unions")==0,"malformed frames, lengths, seal and blank/control names have no effects");
            Create(member,Packet("NotLeader"));a.Fake=true;Create(a,Packet("NotReal"));a.Fake=false;
            Check(Count("SELECT count(*) FROM unions")==0,"ordinary member and fake founder cannot create unions");
            Sql("UPDATE entities SET guildrank=200 WHERE uid=@uid","uid",(long)a.Entity.UID);Create(a,Packet("StaleRank"));
            Check(Count("SELECT count(*) FROM unions")==0 && a.Union==null,"saved rank overrides a stale runtime leader claim");Sql("UPDATE entities SET guildrank=1000 WHERE uid=@uid","uid",(long)a.Entity.UID);
            var oldCache=Kernel.Unions;var request=Packet("FirstRealm");System.Threading.Tasks.Parallel.For(0,8,n=>Create(a,request));
            Check(Count("SELECT count(*) FROM unions")==1 && a.Union!=null && a.UnionID==101 && a.Offical==1 && a.Entity.CharBuffer._UnionID==101,"concurrent creation produces one union and immediate founder projection");
            Check(!ReferenceEquals(oldCache,Kernel.Unions) && oldCache.Count==0 && a.Guild.UnionID==101,"cache publication preserves readers of the prior dictionary");
            Check(Count("SELECT count(*) FROM entities WHERE unionid=101 AND offical=1")==1 && Count("SELECT count(*) FROM guilds WHERE unionid=101")==1 && Count("SELECT unionid FROM configuration")==101,"union, founder, guild and allocator commit together");
            Create(a,Packet("Again"));Create(bOwner,Packet("FirstRealm"));Check(Count("SELECT count(*) FROM unions")==1 && bOwner.Union==null,"request replay and duplicate union name do not allocate another identity");
            Sql("CREATE FUNCTION reject_formation() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_formation BEFORE UPDATE ON guilds FOR EACH ROW EXECUTE FUNCTION reject_formation()");
            Create(failed,Packet("RetryRealm"));
            Check(Count("SELECT count(*) FROM unions")==1 && Count("SELECT count(*) FROM entities WHERE unionid>0")==1 && Count("SELECT unionid FROM configuration")==101 && failed.Union==null && failed.Guild.UnionID==0,"suppressed guild link rolls back prior union/founder writes and allocator");
            Sql("DROP TRIGGER reject_formation ON guilds;DROP FUNCTION reject_formation()");failed=(GameState)Helper("ReloadGold",failed);Create(failed,Packet("RetryRealm"));
            Check(failed.UnionID==102 && Count("SELECT count(*) FROM unions")==2,"ordinary reconnect retries the rolled-back creation once");
            var formation=typeof(Entity).Assembly.GetType("Revora.Game.Features.UnionFormation",true);var hook=formation.GetField("BeforeProjection",Hidden);
            hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Injected postcommit projection failure");}));
            try{Create(bOwner,Packet("CommittedRealm"));}finally{hook.SetValue(null,null);}
            Check(Count("SELECT count(*) FROM unions")==3 && bOwner.Union==null && bOwner.Action==0 && Kernel.Unions.ContainsKey(103),"postcommit projection failure fences the founder without rolling back committed identity");
            bOwner=(GameState)Helper("ReloadGold",bOwner);Create(bOwner,Packet("CommittedRealm"));
            Check(bOwner.UnionID==103 && bOwner.Offical==1 && Count("SELECT count(*) FROM unions")==3 && Count("SELECT unionid FROM configuration")==103,"normal reconnect recovers a committed formation without allocating twice");
            a=(GameState)Helper("ReloadGold",a);Check(a.UnionID==101 && a.Offical==1 && a.Entity.CharBuffer._UnionID==101 && a.Union.LeaderUID==a.Entity.UID,"founder union and office survive normal character login");
            var successor=(GameState)Helper("TravelPlayer",town,(ushort)317,(ushort)322);Check(EntityTable.SaveEntity(successor),"ordinary guildless successor is saved");
            Action(successor,8,0,a.UnionID);Check(successor.UnionID==101 && successor.Union==a.Union,"ordinary allegiance request joins the created union");
            NamedAction(a,28,successor.Entity.Name,2000);NamedAction(a,24,successor.Entity.Name,0);
            Check(a.Union.Coreofficals.Contains(successor.Entity.UID) && a.Union.Guards.Contains(successor.Entity.UID),"ordinary leader appointments fill successor offices before transfer");
            Action(member,14,successor.Entity.UID);Action(a,14,bOwner.Entity.UID);Action(a,14,a.Entity.UID);
            Check(a.Union.LeaderUID==a.Entity.UID,"nonleader, foreign-union and self succession requests are refused");
            Sql("CREATE FUNCTION reject_succession() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_succession BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION reject_succession()");
            Action(a,14,successor.Entity.UID);
            Check(Count("SELECT leaderuid FROM unions WHERE id=101")==a.Entity.UID && a.Union.Coreofficals.Contains(successor.Entity.UID) && a.Union.Guards.Contains(successor.Entity.UID),"suppressed character write rolls back leadership and office-array changes");
            Sql("DROP TRIGGER reject_succession ON entities;DROP FUNCTION reject_succession()");
            a=(GameState)Helper("ReloadGold",a);successor=(GameState)Helper("ReloadGold",successor);
            System.Threading.Tasks.Parallel.For(0,8,n=>Action(a,14,successor.Entity.UID));
            Check(a.Union.LeaderUID==successor.Entity.UID && successor.Offical==1 && successor.Harem==0 && successor.Guards==0 && a.Offical==0,"concurrent succession installs one new leader and updates both players immediately");
            Check(a.Union.Coreofficals.All(x=>x!=successor.Entity.UID) && a.Union.Harem.All(x=>x!=successor.Entity.UID) && a.Union.Guards.All(x=>x!=successor.Entity.UID)
                && Count("SELECT count(*) FROM entities WHERE unionid=101 AND offical=1")==1,"successor loses former office slots and exactly one saved leader office remains");
            Action(a,14,a.Entity.UID);NamedAction(a,28,a.Entity.Name,2000);Action(successor,29,successor.Entity.UID);
            Check(successor.Offical==1 && a.Offical==0 && a.Union.LeaderUID==successor.Entity.UID,"old-leader authority and legacy office removal cannot undo succession");
            a=(GameState)Helper("ReloadGold",a);successor=(GameState)Helper("ReloadGold",successor);
            Check(a.Offical==0 && successor.Offical==1 && successor.Union.LeaderUID==successor.Entity.UID,"normal login preserves the successor and former leader offices");
            var succession=typeof(Entity).Assembly.GetType("Revora.Game.Features.UnionSuccession",true);var transferHook=succession.GetField("BeforeProjection",Hidden);
            transferHook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Injected succession projection failure");}));
            try{Action(successor,14,a.Entity.UID);}finally{transferHook.SetValue(null,null);}
            Check(Count("SELECT leaderuid FROM unions WHERE id=101")==a.Entity.UID && successor.Action==0 && a.Action==0,"postcommit succession failure fences both clients while keeping durable leadership");
            a=(GameState)Helper("ReloadGold",a);successor=(GameState)Helper("ReloadGold",successor);Action(successor,14,a.Entity.UID);
            Check(a.Offical==1 && successor.Offical==0 && a.Union.LeaderUID==a.Entity.UID,"both clients recover committed succession and an old request cannot replay it");
            var unionRows=Convert.ToString(Sql("SELECT jsonb_agg(to_jsonb(u) ORDER BY id)::text FROM unions u"));Kernel.Unions=new System.Collections.Generic.Dictionary<uint,Revora.Network.GamePackets.Union.UnionClass>();Revora.Union.UnionTable.Load();
            Check(unionRows==Convert.ToString(Sql("SELECT jsonb_agg(to_jsonb(u) ORDER BY id)::text FROM unions u")) && Kernel.Unions[101].Guilds.Contains(a.Guild.ID) && Kernel.Unions[102].Coreofficals.Length==8,"fresh union cache restores exact saved formation and complete offices without rewriting rows");
            Console.WriteLine("Union formation and succession checks passed: "+checks);return 0;
        }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message+"\n"+e.StackTrace);return 1;}
        finally {
            Environment.CurrentDirectory=prior;NpgsqlConnection.ClearAllPools();b.SearchPath="public";DataHolder.ConnectionString=b.ConnectionString;
            if(owned)Sql("DROP SCHEMA revora_union_succession_fixture CASCADE");DataHolder.ConnectionString=null;
        }
    }
}
