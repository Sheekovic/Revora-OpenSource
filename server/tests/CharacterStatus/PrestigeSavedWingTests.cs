using System;
using System.IO;
using System.Linq;
using Npgsql;
using Revora;
using Revora.Database;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static class PrestigeSavedWingTests
{
    const string Schema="revora_prestige_wing_validation";
    static void Sql(string text,params object[] parameters) {
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(text,c)){
            for(int i=0;i<parameters.Length;i+=2) {
                string name=(string)parameters[i];object value=parameters[i+1];
                if(value is DateTime || name=="expiry")q.Parameters.Add(name,NpgsqlTypes.NpgsqlDbType.TimestampTz).Value=value;
                else q.Parameters.AddWithValue(name,value);
            }
            q.ExecuteNonQuery();
        }}
    }
    static byte[] Selection(int key) {
        using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){
            w.Write((byte)1);w.Write((short)(key/10000));w.Write((short)(key%10000));w.Write((byte)1);return s.ToArray();
        }
    }
    static uint Wing(PrestigeEntry e) {
        var bytes=PrestigePackets.Appearance(e);
        using(var s=new MemoryStream(bytes,4,bytes.Length-12))return ProtoBuf.Serializer.Deserialize<PrestigePackets.Showcase>(s).Members[0].Wing;
    }
    public static int Main() {
        bool owned=false;string original=null;
        try {
            var config=Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION");
            if(string.IsNullOrEmpty(config))throw new InvalidOperationException("Explicit disposable connection required.");
            var target=new NpgsqlConnectionStringBuilder(config);
            if(target.Host!="127.0.0.1" || target.Port!=25439 || target.Database!="revora_candidate_game")throw new InvalidOperationException("Disposable loopback database required.");
            original=target.ConnectionString;DataHolder.ConnectionString=original;
            Sql("CREATE SCHEMA "+Schema);owned=true;target.SearchPath=Schema;DataHolder.ConnectionString=target.ConnectionString;
            foreach(string table in new[]{"entities","titles","subclasses","items","itemadding"})
                Sql("CREATE TABLE "+table+" AS TABLE public."+table+" WITH NO DATA");
            Sql("CREATE TABLE revora_event_awards(uid bigint,title_key integer,source text,earned_at timestamptz,expires_at timestamptz,revoked boolean)");
            var now=DateTime.UtcNow;
            int[] keys={60096009,60096009,60186018,60126012,60096009,60030022};
            for(int i=0;i<keys.Length;i++){
                Sql("INSERT INTO entities(uid,name,level,class,reborn) VALUES(@uid,@name,140,45,2)","uid",i+1,"name","WingFixture"+(i+1));
                Sql("INSERT INTO titles(id,data) VALUES(@uid,@data)","uid",i+1,"data",Selection(keys[i]));
            }
            Action<int,string,DateTime,DateTime?,bool> award=(uid,source,earned,expiry,revoked)=>Sql(
                "INSERT INTO revora_event_awards VALUES(@uid,@key,@source,@earned,@expiry,@revoked)",
                "uid",uid,"key",keys[uid-1],"source",source,"earned",earned,"expiry",expiry.HasValue?(object)expiry.Value:DBNull.Value,"revoked",revoked);
            award(1,"planet-revora-v1",now.AddDays(-31),now.AddDays(-1),false);
            award(2,"planet-revora-v1",now.AddDays(-1),now.AddDays(29),false);
            award(3,"heart-revora-v1",now.AddDays(-1),now.AddDays(29),true);
            award(4,"mrms-2017-wing-1-v1",now.AddDays(-1),null,false);
            award(4,"mrms-2017-wing-3-v1",now.AddDays(-1),now.AddDays(179),false);
            award(5,"planet-revora-v1",now.AddDays(-1),now.AddDays(30),false);
            PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
            ConquerItemInformation.Load();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("SELECT earned_at,expires_at FROM revora_event_awards WHERE uid=2",c))using(var r=q.ExecuteReader()){
                r.Read();var earned=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;var expiry=r.GetFieldValue<DateTimeOffset>(1).UtcDateTime;
                if(expiry!=earned.AddDays(30))throw new Exception("UTC fixture lifetime changed in PostgreSQL.");
            }}
            for(int pass=0;pass<2;pass++){
                PrestigeRanking.LoadSavedPlayers();var entries=PrestigeRanking.Snapshot(null).OrderBy(e=>e.Profile.UID).ToArray();
                uint[] expected={0,60096009,0,60126012,0,60030022};
                if(entries.Length!=6 || !entries.Select(Wing).SequenceEqual(expected))throw new Exception("Offline wing expiry or reload mismatch: "+string.Join(",",entries.Select(e=>e.Profile.UID+":"+Wing(e))));
            }
            Console.WriteLine("PASS: six saved wing cases across two read-only ranking reloads; expired, revoked, invalid lifetime, active, permanent overlap and ordinary cosmetic.");return 0;
        }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
        finally{if(owned){DataHolder.ConnectionString=original;Sql("DROP SCHEMA "+Schema+" CASCADE");}}
    }
}
