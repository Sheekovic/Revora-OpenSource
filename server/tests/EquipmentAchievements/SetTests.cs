using System;
using System.Linq;
using System.Reflection;
using Npgsql;
using Revora.Database;
using Revora.Network.GamePackets;

internal static class EquipmentSetTests
{
    const uint Owner=930901;
    const BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    static readonly MethodInfo Record=typeof(ConquerItem).Assembly.GetType("Revora.Game.Features.EquipmentAchievements",true).GetMethod("Record",Hidden);
    static readonly int[] Slots={1,2,3,4,6,8};
    static readonly int[] All=Enumerable.Range(10410,8).ToArray();
    static readonly DateTime Now=DateTime.UtcNow;
    static uint[] Types;
    static int checks;
    static object Sql(string sql,params object[] args) {
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(sql,c)) {
            for(int i=0;i<args.Length;i+=2)q.Parameters.AddWithValue((string)args[i],args[i+1]);
            return q.ExecuteScalar();
        }}
    }
    static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);checks++;}
    static void Add(int slot,uint type,long item=930910) {
        Sql(@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,days,plus,bless,enchant,socketone,sockettwo)
VALUES(@item,@type,@owner,@slot,0,1,'0',12,7,1,255,255)","item",item,"type",(long)type,"owner",(long)Owner,"slot",slot);
    }
    static void Reset() {
        Sql("TRUNCATE items,revora_achievement_evidence");
        for(int i=0;i<Slots.Length;i++)Add(Slots[i],Types[i],930920+i);
    }
    static void Observe(bool commit=true) {
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
            try {Record.Invoke(null,new object[]{c,t,Owner,Now});}
            catch(TargetInvocationException e){throw e.InnerException;}
            if(commit)t.Commit();else t.Rollback();
        }}
    }
    static void Expect(string label,int[] expected) {
        Observe();Observe();
        var actual=(int[])Sql("SELECT COALESCE(array_agg(achievement_id ORDER BY achievement_id),'{}'::int[]) FROM revora_achievement_evidence WHERE uid=@owner AND source='equipment-set-v1'","owner",(long)Owner);
        Check(actual.SequenceEqual(expected),label+" actual="+string.Join(",",actual));
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid<>@owner","owner",(long)Owner))==0,label+" owner isolation");
    }
    static bool Earned(int id){return Convert.ToInt64(Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id","uid",(long)Owner,"id",id))==1;}
    static void ObserveAlternate(bool commit=true) {
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
            typeof(ConquerItem).Assembly.GetType("Revora.Game.Features.EquipmentAchievements",true).GetMethod("RecordBank",Hidden)
                .Invoke(null,new object[]{c,t,Owner,Now,20,null});
            if(commit)t.Commit();else t.Rollback();
        }}
    }
    static void Change(string sql,params object[] args){Reset();Sql(sql,args);}
    static int Main() {
        var b=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION"));
        Environment.SetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION",null);
        if(b.Host!="127.0.0.1" || b.Port!=25439 || b.Database!="revora_candidate_game")throw new InvalidOperationException("private candidate required");
        DataHolder.ConnectionString=b.ConnectionString;Sql("CREATE SCHEMA equipment_set_fixture");
        string previous=Environment.CurrentDirectory;
        try {
            b.SearchPath="equipment_set_fixture";DataHolder.ConnectionString=b.ConnectionString;
            Sql(@"CREATE TABLE items(LIKE public.items INCLUDING ALL);
CREATE TABLE revora_achievement_evidence(uid bigint NOT NULL,achievement_id int NOT NULL,source varchar(48) NOT NULL,
source_key varchar(96) NOT NULL,rule_version int NOT NULL,completed_at timestamptz NOT NULL DEFAULT current_timestamp,PRIMARY KEY(uid,achievement_id))");
            Environment.CurrentDirectory=@"C:\Revora\DisposablePostgres\moon-box-candidate\game";ConquerItemInformation.Load();
            Types=Slots.Select(slot=>ConquerItemInformation.BaseInformations.Keys.Where(id=>id%10==9
                && (int)ItemHandler.GetPositionFromID(id)==slot && ConquerItemInformation.BaseInformations.ContainsKey(id-1)
                && ConquerItemInformation.BaseInformations.ContainsKey(id-2)).OrderBy(id=>id).First()).ToArray();
            Reset();Expect("complete six-slot set; empty sockets count as holes",All);
            foreach(int slot in Slots) {
                Change("DELETE FROM items WHERE position=@slot","slot",slot);Expect("missing core "+slot,new int[0]);
            }
            Change("UPDATE items SET id=id-2");Expect("Unique does not satisfy Elite or Super",new[]{10410,10413,10414,10415,10416});
            Change("UPDATE items SET id=id-1");Expect("Elite satisfies Unique",new[]{10410,10411,10413,10414,10415,10416});
            foreach(int plus in new[]{0,11,13}) {
                Change("UPDATE items SET plus=@plus WHERE position=1","plus",plus);Expect("plus threshold "+plus,All.Except(new[]{10413,10417}).ToArray());
            }
            foreach(int bless in new[]{0,6,8}) {
                Change("UPDATE items SET bless=@bless WHERE position=1","bless",bless);Expect("bless threshold "+bless,All.Except(new[]{10414,10417}).ToArray());
            }
            foreach(int enchant in new[]{0,-1,256}) {
                Change("UPDATE items SET enchant=@enchant WHERE position=1","enchant",enchant);Expect("enchant threshold "+enchant,All.Except(new[]{10416}).ToArray());
            }
            Change("UPDATE items SET enchant=255");Expect("maximum enchant",All);
            Change("UPDATE items SET sockettwo=0 WHERE position=1");Expect("single socket insufficient for combined",All.Except(new[]{10417}).ToArray());
            foreach(int socket in new[]{0,-1,254,256}) {
                Change("UPDATE items SET socketone=@socket WHERE position=1","socket",socket);Expect("invalid first socket "+socket,All.Except(new[]{10415,10417}).ToArray());
            }
            Change("UPDATE items SET socketone=13,sockettwo=13");Expect("filled sockets",All);
            Change("UPDATE items SET sockettwo=254 WHERE position=1");Expect("invalid second socket",All.Except(new[]{10415,10417}).ToArray());
            foreach(string mutation in new[]{"position=0","position=21","warehouse=1","stacksize=2","entityid=930902","id=99999999","days='invalid'"}) {
                Change("UPDATE items SET "+mutation+" WHERE position=1");Expect("invalid mandatory row "+mutation,new int[0]);
            }
            Reset();Add(1,Types[0]);Expect("duplicate mandatory slot",new int[0]);
            Change("UPDATE items SET days='1',daystamp=@stamp WHERE position=1","stamp",Now.AddDays(-2).ToBinary());Expect("expired equipment",new int[0]);
            Change("UPDATE items SET days='1',daystamp=@stamp WHERE position=1","stamp",Now.ToBinary());Expect("valid rental follows existing equipment policy",All);
            Change("UPDATE items SET days='1',daystamp=@stamp WHERE position=1","stamp",long.MaxValue);Expect("invalid rental timestamp",new int[0]);
            Change("UPDATE items SET durability=0");Expect("broken equipped items remain possession evidence",All);
            Reset();Add(9,187405);Add(10,201009,930911);Expect("cosmetics and talismans excluded",All);
            foreach(uint hand in new[]{410009U,ConquerItemInformation.BaseInformations.Keys.Where(id=>id/1000==900 && id%10==9).OrderBy(id=>id).First(),
                ConquerItemInformation.BaseInformations.Keys.Where(id=>id/1000==619 && id%10==9).OrderBy(id=>id).First()}) {
                Reset();Add(5,hand);Expect("valid optional offhand "+hand,All);
                Sql("TRUNCATE revora_achievement_evidence;UPDATE items SET plus=0 WHERE position=5");Expect("offhand must meet same thresholds "+hand,All.Except(new[]{10413,10417}).ToArray());
            }
            Reset();Add(5,Types[0]);Expect("wrong-category offhand cannot disappear",new int[0]);
            Reset();Add(5,410009);Add(5,410009,930911);Expect("duplicate optional offhand",new int[0]);
            Reset();Add(5,410009);Sql("UPDATE items SET stacksize=2 WHERE position=5");Expect("invalid optional stack",new int[0]);
            Reset();Add(5,1050000);Sql("UPDATE items SET plus=0,bless=0,enchant=0,socketone=0,sockettwo=0 WHERE position=5");Expect("arrows excluded from set attributes",All);
            Reset();Observe(false);Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_achievement_evidence"))==0,"rollback leaves no partial achievement");
            Expect("commit after rollback",All);
            NpgsqlConnection.ClearAllPools();Sql("DELETE FROM items");Expect("earned achievements survive new connection, gear removal and replay",All);
            Reset();Observe();Check(!Earned(10428),"empty holes do not count as Super gems");
            Sql("UPDATE items SET socketone=13,sockettwo=13");Observe();Check(Earned(10428),"twelve repeated Super gems count");
            foreach(int gem in new[]{0,255,12,103,123,83,254}) {
                Reset();Sql("UPDATE items SET socketone=13,sockettwo=13;UPDATE items SET sockettwo=@gem WHERE position=1","gem",gem);
                Observe();Check(!Earned(10428),"eleven Super gems plus excluded value "+gem);
            }
            Reset();Sql("UPDATE items SET socketone=13,sockettwo=13;UPDATE items SET position=position+20");Observe();
            Check(!Earned(10428) && !Earned(10432),"alternate ownership alone grants neither identity");
            ObserveAlternate();Check(Earned(10432),"complete alternate predicate");
            foreach(int slot in Slots) {
                Reset();Sql("UPDATE items SET position=position+20;DELETE FROM items WHERE position=@slot","slot",slot+20);
                ObserveAlternate();Check(!Earned(10432),"alternate missing mandatory slot "+slot);
            }
            foreach(string mutation in new[]{"stacksize=2","warehouse=1","days='invalid'","id=99999999"}) {
                Reset();Sql("UPDATE items SET position=position+20;UPDATE items SET "+mutation+" WHERE position=21");
                ObserveAlternate();Check(!Earned(10432),"alternate malformed row "+mutation);
            }
            Reset();Add(5,Types[0]);Sql("UPDATE items SET position=position+20");ObserveAlternate();Check(!Earned(10432),"invalid alternate offhand rejected");
            Reset();Add(5,410009);Sql("UPDATE items SET position=position+20");ObserveAlternate(false);Check(!Earned(10432),"alternate evidence rolls back");
            ObserveAlternate();Check(Earned(10432),"valid alternate offhand accepted");
            Console.WriteLine("Equipment set database checks passed: "+checks);return 0;
        } finally {
            Environment.CurrentDirectory=previous;NpgsqlConnection.ClearAllPools();b.SearchPath="public";DataHolder.ConnectionString=b.ConnectionString;
            Sql("DROP SCHEMA equipment_set_fixture CASCADE");DataHolder.ConnectionString=null;
        }
    }
}
