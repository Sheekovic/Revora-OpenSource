using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle {
    static DateTime localEliteNow=new DateTime(2027,1,2,3,55,0,DateTimeKind.Utc);
    static object EliteField(object value,string name) { return value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value); }
    static void ElitePulse(object value,int seconds) {
        localEliteNow=localEliteNow.AddSeconds(seconds);
        value.GetType().GetMethod("Tick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,null);
    }
    static void EliteWardrobeCheck(GameState p,bool restoring) {
        GameAssembly.GetType("Revora.TitleRequirements",true).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>localEliteNow));
        new TitleStorage().Login(p);
        int key=Convert.ToInt32(Sql("SELECT title_key FROM revora_event_awards WHERE uid=@uid AND source='elite-pk-local-wardrobe-v1'","uid",(long)p.Entity.UID));
        Require(key>=20352035 && key<=20382038);
        if(restoring)Require(p.Entity.EquippedTitle==key);
        new TitleStorage{Info=new TitleStorageProto{Type=key/10000,Id=key%10000,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=key/10000,Id=key%10000}}}.Handle(p);
        Require(p.Entity.EquippedTitle==key);
        Require((bool)Sql("SELECT expires_at=earned_at+interval '14 days' FROM revora_event_awards WHERE uid=@uid AND source='elite-pk-local-wardrobe-v1'","uid",(long)p.Entity.UID));
        Require(EntityTable.SaveEntity(p));
    }
    static void EliteWardrobeCapture() {
        for(int i=0;i<4;i++)Player(Kernel.Maps[1002]);nextUid=FirstUid;
        var players=Enumerable.Range(0,4).Select(i=>Player(Kernel.Maps[1002],true)).ToArray();
        var type=Feature("ElitePkEvent");
        var arena=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,
            new object[]{(Func<ushort,Map>)(id=>Kernel.Maps[id].MakeDynamicMap()),(Func<DateTime>)(()=>localEliteNow)},null);
        type.GetField("Current",Hidden).SetValue(null,arena);ElitePulse(arena,0);
        foreach(var p in players){p.Entity.Level=140;Require(EntityTable.SaveEntity(p));p.LoadItemStats();HeartTalk(p,448582,3);}
        var group=((IEnumerable)EliteField(arena,"groups")).Cast<object>().Single(g=>(int)EliteField(g,"Id")==3);
        string run=(string)EliteField(group,"Run");
        Require((long)Sql("SELECT count(*) FROM revora_epk_entries WHERE run=@run","run",run)==4);
        Sql(@"CREATE FUNCTION reject_elite_wardrobe() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
IF NEW.source='elite-pk-local-wardrobe-v1' THEN RETURN NULL; END IF; RETURN NEW; END $$;
CREATE TRIGGER reject_elite_wardrobe BEFORE INSERT ON revora_event_awards FOR EACH ROW EXECUTE FUNCTION reject_elite_wardrobe();");
        ElitePulse(arena,300);bool refused=false;
        for(int step=0;step<20 && !(bool)EliteField(group,"Done");step++) {
            ElitePulse(arena,11);ElitePulse(arena,11);
            foreach(var match in ((IEnumerable)EliteField(group,"Matches")).Cast<object>().Where(m=>(bool)EliteField(m,"Imported") && EliteField(m,"Winner")==null).ToArray()) {
                var pair=EliteField(match,"Pair");
                var a=Kernel.GamePool[(uint)EliteField(pair,"Left")];var b=Kernel.GamePool[(uint)EliteField(pair,"Right")];
                a.Attackable=b.Attackable=true;uint damage=100;
                Revora.Game.Attacking.Handle.ReceiveAttack(a.Entity,b.Entity,new Attack(true){AttackType=Attack.Melee},ref damage,null);
                b.Entity.Die(a.Entity);
            }
            ElitePulse(arena,1);
            if(!refused && ((ICollection)EliteField(group,"Alive")).Count==1 && ((ICollection)EliteField(group,"Matches")).Count==0) {
                ElitePulse(arena,1);
                Require(!(bool)EliteField(group,"Done"));
                Require((long)Sql("SELECT count(*) FROM revora_epk_placements WHERE run=@run","run",run)==0);
                Require((long)Sql("SELECT count(*) FROM revora_event_awards WHERE source='elite-pk-local-wardrobe-v1'")==0);
                Sql("DROP TRIGGER reject_elite_wardrobe ON revora_event_awards;DROP FUNCTION reject_elite_wardrobe();");refused=true;
            }
        }
        Require(refused && (bool)EliteField(group,"Done"));
        Require((long)Sql("SELECT count(DISTINCT title_key) FROM revora_event_awards WHERE source='elite-pk-local-wardrobe-v1'")==4);
        foreach(var p in players)EliteWardrobeCheck(p,false);
        Sql("CREATE TABLE revora_local_elite_probe(run text,completed_at timestamptz,fingerprint text)");
        Sql("INSERT INTO revora_local_elite_probe SELECT @run,@now,md5(string_agg(uid::text||':'||title_key::text||':'||earned_at::text,',' ORDER BY uid)) FROM revora_event_awards WHERE source='elite-pk-local-wardrobe-v1'","run",run,"now",localEliteNow);
        ElitePulse(arena,1);Require((long)Sql("SELECT count(*) FROM revora_event_awards WHERE source='elite-pk-local-wardrobe-v1'")==4);
        System.Console.WriteLine("[local-wardrobe] elite-wardrobe-capture-passed");Server.CommandsAI("@close");throw new Exception("Elite fixture did not close.");
    }
    static void EliteWardrobeReload() {
        localEliteNow=DateTime.SpecifyKind((DateTime)Sql("SELECT completed_at AT TIME ZONE 'UTC' FROM revora_local_elite_probe"),DateTimeKind.Utc);
        for(int i=0;i<4;i++){var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();EliteWardrobeCheck(p,true);}
        Require((bool)Sql("SELECT fingerprint=(SELECT md5(string_agg(uid::text||':'||title_key::text||':'||earned_at::text,',' ORDER BY uid)) FROM revora_event_awards WHERE source='elite-pk-local-wardrobe-v1') FROM revora_local_elite_probe"));
        System.Console.WriteLine("[local-wardrobe] elite-wardrobe-reload-passed");Server.CommandsAI("@close");throw new Exception("Elite fixture did not close.");
    }
}
