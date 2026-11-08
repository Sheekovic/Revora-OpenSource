using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class LedgerTests {
    static readonly Type Purses=typeof(Revora.Sistemas.Quarantine).Assembly.GetType("Revora.Sistemas.QuarantinePurses",true);
    static GameState Winner(uint uid) {
        var p=new GameState(null);p.Equipment=new Revora.Game.ConquerStructures.Equipment(p);p.Inventory=new Revora.Game.ConquerStructures.Inventory(p);
        p.Trade=new Revora.Game.ConquerStructures.Trade();p.Entity=new Entity(EntityFlag.Player,false);p.Entity.SendUpdates=false;p.Entity.UID=uid;p.Entity.Hitpoints=100;p.Entity.Owner=p;
        p.Fake=false;p.Socket.Alive=false;p.Action=2;p.Entity.FullyLoaded=true;Kernel.GamePool[uid]=p;return p;
    }
    static string Claim(GameState p){return (string)Purses.GetMethod("Claim",Hidden).Invoke(null,new object[]{p});}
    static void VerifyPurses() {
        Sql("CREATE TABLE entities(uid bigint PRIMARY KEY,conquerpoints bigint NOT NULL DEFAULT 0,revorapoint bigint NOT NULL DEFAULT 0,money bigint NOT NULL DEFAULT 0);CREATE TABLE items (LIKE public.items INCLUDING ALL);INSERT INTO entities(uid) VALUES(910011),(910012);");
        ConquerItemInformation.BaseInformations=new System.Collections.Generic.SafeDictionary<uint,ConquerItemBaseInformation>();
        ConquerItemInformation.BaseInformations.Add(800725,new ConquerItemBaseInformation{ID=800725,PurificationLevel=7,Durability=100});
        var id=Guid.Parse("624a8919-349a-4126-b930-9765fe56e111");var earned=DateTime.UtcNow.AddSeconds(-5);
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            using(var q=new NpgsqlCommand("INSERT INTO revora_quarantine_rounds(id,started_at,ended_at,state,white_score,black_score,winners) VALUES(@id,@now,@now,'won',100,1,ARRAY[910011::bigint])",c,t)){q.Parameters.AddWithValue("id",id);q.Parameters.AddWithValue("now",earned);q.ExecuteNonQuery();}
            Purses.GetMethod("Freeze",Hidden).Invoke(null,new object[]{c,t,id,earned,new long[]{910011},new uint[]{800725}});t.Commit();
        }}
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_purses WHERE uid=910011 AND cp=300000000 AND rvp=50000 AND gold=100000000000 AND soul_type=800725 AND claimed_at IS NULL"))==1,"frozen pending bundle");
        var other=Winner(910012);Check(Claim(other)==null,"foreign player has no entitlement");
        var p=Winner(910011);var inv=p.Inventory;var objects=typeof(Revora.Game.ConquerStructures.Inventory).GetField("objects",BindingFlags.NonPublic|BindingFlags.Instance);
        objects.SetValue(inv,new ConquerItem[40]);Check(Claim(p).Contains("bag") && Convert.ToInt64(Sql("SELECT conquerpoints FROM entities WHERE uid=910011"))==0,"full bag retains whole purse");objects.SetValue(inv,new ConquerItem[0]);
        Sql("CREATE FUNCTION reject_soul() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'fixture rejection'; END $$;CREATE TRIGGER reject_soul BEFORE INSERT ON items FOR EACH ROW EXECUTE PROCEDURE reject_soul();");
        Claim(p);Check(Convert.ToInt64(Sql("SELECT conquerpoints+revorapoint+money FROM entities WHERE uid=910011"))==0 && Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_purses WHERE uid=910011 AND claimed_at IS NULL"))==1,"item failure rolls back entire currency/receipt transaction");
        Sql("DROP TRIGGER reject_soul ON items;DROP FUNCTION reject_soul();");p=Winner(910011);
        System.Threading.Tasks.Parallel.For(0,8,i=>Claim(p));
        Check(Convert.ToInt64(Sql("SELECT conquerpoints FROM entities WHERE uid=910011"))==300000000 && p.Entity.ConquerPoints==300000000,"single committed CP award and cache");
        Check(Convert.ToInt64(Sql("SELECT revorapoint FROM entities WHERE uid=910011"))==50000 && p.Entity.RevoraPoint==50000,"single committed RVP award and cache");
        Check(Convert.ToInt64(Sql("SELECT money FROM entities WHERE uid=910011"))==100000000000 && p.Entity.Money==100000000000,"single committed silver award and cache");
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM items WHERE entityid=910011 AND id=800725 AND bound=1 AND position=0 AND warehouse=0"))==1 && p.Inventory.Count==1 && p.Inventory.Objects[0].Bound,"single bound soul saved and installed");
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_purses p JOIN items i ON i.uid=p.soul_uid AND i.entityid=p.uid WHERE p.uid=910011 AND p.claimed_at IS NOT NULL"))==1,"paid receipt identifies exact owned item");
        Check(Claim(p)==null && Convert.ToInt64(Sql("SELECT count(*) FROM items WHERE entityid=910011"))==1,"paid claim replay creates nothing");
        Sql("UPDATE revora_quarantine_rounds SET state='cancelled' WHERE state='active'");
        typeof(Revora.Sistemas.Quarantine).GetField("ledgerReady",Hidden).SetValue(null,false);Call("Initialize");
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_purses WHERE uid=910011 AND claimed_at IS NOT NULL"))==1,"component recovery preserves paid receipt");
        var second=Guid.Parse("624a8919-349a-4126-b930-9765fe56e112");
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            using(var q=new NpgsqlCommand("INSERT INTO revora_quarantine_rounds(id,started_at,ended_at,state,white_score,black_score,winners) VALUES(@id,@now,@now,'won',1,100,ARRAY[910012::bigint])",c,t)){q.Parameters.AddWithValue("id",second);q.Parameters.AddWithValue("now",earned);q.ExecuteNonQuery();}
            Purses.GetMethod("Freeze",Hidden).Invoke(null,new object[]{c,t,second,earned,new long[]{910012},new uint[]{800725}});t.Commit();
        }}
        GameState removed;Kernel.GamePool.TryRemove(910012,out removed);
        typeof(Revora.Sistemas.Quarantine).GetField("ledgerReady",Hidden).SetValue(null,false);Call("Initialize");
        Purses.GetField("nextPoll",Hidden).SetValue(null,DateTime.MinValue);Purses.GetMethod("Tick",Hidden).Invoke(null,null);
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_purses WHERE uid=910012 AND claimed_at IS NULL"))==1 && Convert.ToInt64(Sql("SELECT conquerpoints FROM entities WHERE uid=910012"))==0,"offline frozen purse survives recovery and poll");
        other=Winner(910012);Purses.GetField("nextPoll",Hidden).SetValue(null,DateTime.MinValue);Purses.GetMethod("Tick",Hidden).Invoke(null,null);
        Check(other.Entity.ConquerPoints==300000000 && other.Inventory.Count==1 && other.Inventory.Objects[0].Bound,"production poll delivers reconnect purse");
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_purses WHERE claimed_at IS NOT NULL"))==2 && Convert.ToInt64(Sql("SELECT count(DISTINCT soul_uid) FROM revora_quarantine_purses WHERE claimed_at IS NOT NULL"))==2,"distinct receipts own distinct souls");
        var admissions=typeof(Revora.Sistemas.Quarantine).Assembly.GetType("Revora.Sistemas.QuarantineAdmissions",true);
        var round=Guid.Parse("624a8919-349a-4126-b930-9765fe56e113");
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("INSERT INTO revora_quarantine_rounds(id,started_at,state) VALUES(@id,@now,'active')",c)){q.Parameters.AddWithValue("id",round);q.Parameters.AddWithValue("now",DateTime.UtcNow);q.ExecuteNonQuery();}}
        other.Entity.PKMode=Enums.PKMode.Capture;
        var reserve=admissions.GetMethod("Reserve",Hidden);
        Check(!(bool)reserve.Invoke(null,new object[]{round,other,true,880021u,0u}),"admission requires owner lock");
        lock(other.ItemSyncRoot){
            Check((bool)reserve.Invoke(null,new object[]{round,other,true,880021u,0u}),"ordinary canonical owner obtains saved reservation");
            Check(!(bool)reserve.Invoke(null,new object[]{round,other,false,880022u,0u}),"pending admission cannot be replaced");
        }
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_admissions WHERE uid=910012 AND white_team AND uniform_uid=880021 AND original_garment=0 AND NOT entered AND NOT ended AND NOT returned"))==1,"reservation freezes original identity and team before entry");
        admissions.GetMethod("Entered",Hidden).Invoke(null,new object[]{round,910012u});admissions.GetMethod("Entered",Hidden).Invoke(null,new object[]{round,910012u});
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_admissions WHERE uid=910012 AND entered AND NOT returned"))==1,"entry acknowledgement is idempotent");
        typeof(Revora.Sistemas.Quarantine).GetField("ledgerReady",Hidden).SetValue(null,false);Call("Initialize");
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_admissions WHERE uid=910012 AND ended AND NOT returned AND uniform_uid=880021"))==1,"startup retains exact pending return identity");
        Sql("ALTER TABLE entities ADD COLUMN mapid integer NOT NULL DEFAULT 10360,ADD COLUMN x integer NOT NULL DEFAULT 1,ADD COLUMN y integer NOT NULL DEFAULT 1;INSERT INTO items(uid,id,entityid,position,warehouse) VALUES(880021,181325,910012,0,0),(880040,187405,910012,9,0);");
        Action<GameState> town=player=>{foreach(var pair in new[]{Tuple.Create("_mapid",(ushort)1002),Tuple.Create("_x",(ushort)327),Tuple.Create("_y",(ushort)303)})typeof(Entity).GetField(pair.Item1,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player.Entity,pair.Item2);};
        town(other);other.Entity.PKMode=Enums.PKMode.PK;
        var replacement=new ConquerItem(true){UID=880040,ID=187405,Position=9};other.Equipment.Objects[8]=replacement;
        var inventory=(System.Collections.Generic.Dictionary<uint,ConquerItem>)typeof(Revora.Game.ConquerStructures.Inventory).GetField("inventory",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(other.Inventory);
        inventory.Add(880021,new ConquerItem(true){UID=880021,ID=181325,Bound=true});
        typeof(Revora.Game.ConquerStructures.Inventory).GetField("objects",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(other.Inventory,inventory.Values.ToArray());
        Sql("CREATE FUNCTION reject_return() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.returned THEN RAISE EXCEPTION 'fixture rejection'; END IF; RETURN NEW; END $$;CREATE TRIGGER reject_return BEFORE UPDATE ON revora_quarantine_admissions FOR EACH ROW EXECUTE PROCEDURE reject_return();");
        var returnPlayer=admissions.GetMethod("Return",Hidden);
        Check(!(bool)returnPlayer.Invoke(null,new object[]{other}),"failed acknowledgement leaves retryable return");
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_admissions WHERE uid=910012 AND ended AND NOT returned"))==1 && Convert.ToInt64(Sql("SELECT count(*) FROM entities WHERE uid=910012 AND mapid=1002 AND x=327 AND y=303"))==1,"safe destination committed before failed acknowledgement");
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM items WHERE uid=880021"))==0 && Convert.ToInt64(Sql("SELECT count(*) FROM items WHERE uid=880040 AND entityid=910012 AND position=9"))==1 && ReferenceEquals(other.Equipment.Objects[8],replacement),"recorded uniform removed and replacement garment preserved");
        Check(other.Entity.PKMode==Enums.PKMode.Capture,"saved PK mode restored before acknowledgement");
        Sql("DROP TRIGGER reject_return ON revora_quarantine_admissions;DROP FUNCTION reject_return();");
        other=Winner(910012);town(other);other.Equipment.Objects[8]=replacement;
        Check((bool)returnPlayer.Invoke(null,new object[]{other}) && other.Entity.PKMode==Enums.PKMode.Capture,"fresh canonical session finishes pending return");
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_admissions WHERE uid=910012 AND returned"))==1 && Convert.ToInt64(Sql("SELECT conquerpoints FROM entities WHERE uid=910012"))==300000000,"return releases admission without overwriting reward balance");
        other.Entity.PKMode=Enums.PKMode.Peace;Check((bool)returnPlayer.Invoke(null,new object[]{other}) && other.Entity.PKMode==Enums.PKMode.Peace,"acknowledged replay does not overwrite later PK choice");
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("INSERT INTO revora_quarantine_rounds(id,started_at,state) VALUES(@id,@now,'active')",c)){q.Parameters.AddWithValue("id",Guid.Parse("624a8919-349a-4126-b930-9765fe56e114"));q.Parameters.AddWithValue("now",DateTime.UtcNow);q.ExecuteNonQuery();}}
        lock(other.ItemSyncRoot)Check((bool)reserve.Invoke(null,new object[]{Guid.Parse("624a8919-349a-4126-b930-9765fe56e114"),other,false,880022u,880040u}),"acknowledged owner may join a later round with verified original garment");
        var restore=admissions.GetMethod("RestoreGarment",Hidden);
        lock(other.ItemSyncRoot)Check(!(bool)restore.Invoke(null,new object[]{other,880040u}),"return preserves a currently equipped garment");
        other.Equipment.Objects[8]=null;replacement.Position=0;replacement.Warehouse=0;
        inventory=(System.Collections.Generic.Dictionary<uint,ConquerItem>)typeof(Revora.Game.ConquerStructures.Inventory).GetField("inventory",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(other.Inventory);
        inventory.Add(replacement.UID,replacement);objects.SetValue(other.Inventory,inventory.Values.ToArray());
        Sql("UPDATE items SET position=0,entityid=910011 WHERE uid=880040");
        bool refused=false;
        lock(other.ItemSyncRoot)try{restore.Invoke(null,new object[]{other,880040u});}catch(TargetInvocationException e){refused=e.InnerException is InvalidOperationException;}
        Check(refused && other.Equipment.Objects[8]==null && other.Inventory.Objects.Any(i=>i.UID==880040),"foreign saved ownership cannot equip cached garment");
        Sql("UPDATE items SET entityid=910012 WHERE uid=880040;CREATE FUNCTION reject_reequip() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.uid=880040 AND NEW.position=9 THEN RAISE EXCEPTION 'fixture rejection'; END IF; RETURN NEW; END $$;CREATE TRIGGER reject_reequip BEFORE UPDATE ON items FOR EACH ROW EXECUTE PROCEDURE reject_reequip();");
        refused=false;
        lock(other.ItemSyncRoot)try{restore.Invoke(null,new object[]{other,880040u});}catch(TargetInvocationException e){refused=e.InnerException is PostgresException;}
        Check(refused && other.Equipment.Objects[8]==null && other.Inventory.Objects.Any(i=>i.UID==880040) && Convert.ToInt64(Sql("SELECT position FROM items WHERE uid=880040"))==0,"failed garment move preserves bag and saved position");
        Sql("DROP TRIGGER reject_reequip ON items;DROP FUNCTION reject_reequip();");
        lock(other.ItemSyncRoot)Check((bool)restore.Invoke(null,new object[]{other,880040u}),"original garment reequips after retry");
        Check(ReferenceEquals(other.Equipment.Objects[8],replacement) && replacement.IsWorn && replacement.Position==9 && !other.Inventory.Objects.Any(i=>i.UID==880040) && Convert.ToInt64(Sql("SELECT position FROM items WHERE uid=880040"))==9,"same garment identity moves from bag to saved equipped slot");
        lock(other.ItemSyncRoot)Check(!(bool)restore.Invoke(null,new object[]{other,880040u}),"garment restoration replay is harmless");

        VerifyHitLedger();
        Revora.Sistemas.Quarantine.Start();var start=(DateTime)typeof(Revora.Sistemas.Quarantine).GetField("roundStarted",Hidden).GetValue(null);
        var tick=typeof(Revora.Sistemas.Quarantine).GetMethod("Tick",Hidden);
        tick.Invoke(null,new object[]{start.AddMinutes(29)});Check(Revora.Sistemas.Quarantine.Started,"deadline poll leaves an unfinished valid round running");
        Sql("CREATE FUNCTION reject_deadline() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.state<>'active' THEN RAISE EXCEPTION 'fixture rejection'; END IF; RETURN NEW; END $$;CREATE TRIGGER reject_deadline BEFORE UPDATE ON revora_quarantine_rounds FOR EACH ROW EXECUTE PROCEDURE reject_deadline();");
        tick.Invoke(null,new object[]{start.AddMinutes(31)});Check(Revora.Sistemas.Quarantine.Started && typeof(Revora.Sistemas.Quarantine).GetField("pendingResult",Hidden).GetValue(null)!=null,"missed schedule window retains failed settlement for retry");
        Sql("DROP TRIGGER reject_deadline ON revora_quarantine_rounds;DROP FUNCTION reject_deadline();");
        tick.Invoke(null,new object[]{start.AddMinutes(31).AddSeconds(1)});Check(Revora.Sistemas.Quarantine.Started,"settlement retry is bounded");
        tick.Invoke(null,new object[]{start.AddMinutes(31).AddSeconds(10)});Check(!Revora.Sistemas.Quarantine.Started,"periodic retry finalizes after database recovery");
        var slot=new DateTime(2031,1,5,20,0,0,DateTimeKind.Unspecified);Revora.Sistemas.Quarantine.StartScheduled(slot.AddHours(-1));
        Check(!Revora.Sistemas.Quarantine.Started,"scheduled entry rejects the wrong hour");
        Revora.Sistemas.Quarantine.StartScheduled(slot);var scheduledId=typeof(Revora.Sistemas.Quarantine).GetField("roundId",Hidden).GetValue(null);
        Check(Revora.Sistemas.Quarantine.Started,"eligible scheduled date opens a round");
        Revora.Sistemas.Quarantine.End();Revora.Sistemas.Quarantine.StartScheduled(slot.AddSeconds(10));
        Check(!Revora.Sistemas.Quarantine.Started,"completed round cannot reopen in the same scheduler window");
        Revora.Sistemas.Quarantine.StartScheduled(slot.AddDays(1));
        Check(Revora.Sistemas.Quarantine.Started && !Equals(scheduledId,typeof(Revora.Sistemas.Quarantine).GetField("roundId",Hidden).GetValue(null)),"next eligible date owns a distinct round");
        typeof(Revora.Sistemas.Quarantine).GetField("ledgerReady",Hidden).SetValue(null,false);Call("Initialize");
        Revora.Sistemas.Quarantine.Started=false;typeof(Revora.Sistemas.Quarantine).GetField("roundId",Hidden).SetValue(null,Guid.Empty);
        Revora.Sistemas.Quarantine.StartScheduled(slot.AddDays(1).AddSeconds(20));
        Check(!Revora.Sistemas.Quarantine.Started,"cancelled interrupted schedule cannot reopen after component recovery");
        Revora.Sistemas.Quarantine.StartScheduled(slot.AddDays(3));Revora.Sistemas.Quarantine.StartScheduled(slot.AddDays(6));
        Check(!Revora.Sistemas.Quarantine.Started,"Wednesday and Saturday remain unscheduled");


    }
    static void VerifyHitLedger() {
        var type=typeof(Revora.Sistemas.Quarantine);var round=Guid.Parse("624a8919-349a-4126-b930-9765fe56e115");
        Sql("UPDATE revora_quarantine_admissions SET ended=true,returned=true;INSERT INTO revora_quarantine_rounds(id,started_at,state) VALUES('624a8919-349a-4126-b930-9765fe56e115',clock_timestamp()-interval '1 second','active');INSERT INTO revora_quarantine_admissions(round_id,uid,white_team,pkmode,uniform_uid,original_garment,entered) VALUES('624a8919-349a-4126-b930-9765fe56e115',910011,true,1,880101,0,true),('624a8919-349a-4126-b930-9765fe56e115',910012,false,1,880102,0,true);");
        var a=Winner(910011);var b=Winner(910012);
        typeof(Entity).GetField("_mapid",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(a.Entity,(ushort)10360);
        typeof(Entity).GetField("_mapid",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(b.Entity,(ushort)10360);
        a.Equipment.Objects[8]=new ConquerItem(true){UID=880101,ID=181325,Position=9};b.Equipment.Objects[8]=new ConquerItem(true){UID=880102,ID=181525,Position=9};
        Revora.Sistemas.Quarantine.White=new ThreadSafeDictionary<uint,GameState>(50);Revora.Sistemas.Quarantine.Black=new ThreadSafeDictionary<uint,GameState>(50);
        Revora.Sistemas.Quarantine.White.Add(a.Entity.UID,a);Revora.Sistemas.Quarantine.Black.Add(b.Entity.UID,b);
        Revora.Sistemas.Quarantine.WhiteScore=0;Revora.Sistemas.Quarantine.BlackScore=0;Revora.Sistemas.Quarantine.Started=true;
        type.GetField("roundId",Hidden).SetValue(null,round);type.GetField("pendingResult",Hidden).SetValue(null,null);
        var save=type.GetMethod("SaveHit",Hidden);var spell=new SpellInformation{ID=1045};
        b.Equipment.Objects[8].UID=880103;
        Check(!(bool)save.Invoke(null,new object[]{a.Entity,b.Entity,spell}),"unissued uniform cannot produce durable hit");b.Equipment.Objects[8].UID=880102;
        Check((bool)save.Invoke(null,new object[]{a.Entity,b.Entity,spell}),"white valid admission saves hit");Revora.Sistemas.Quarantine.WhiteScore=1;
        Check((bool)save.Invoke(null,new object[]{b.Entity,a.Entity,spell}),"black valid admission saves hit");Revora.Sistemas.Quarantine.BlackScore=1;
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_hits WHERE round_id='624a8919-349a-4126-b930-9765fe56e115'"))==2 && Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_rounds WHERE id='624a8919-349a-4126-b930-9765fe56e115' AND white_score=1 AND black_score=1"))==1,"hit records and aggregate commit together");
        Sql("CREATE FUNCTION reject_hit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'fixture rejection'; END $$;CREATE TRIGGER reject_hit BEFORE UPDATE ON revora_quarantine_rounds FOR EACH ROW EXECUTE PROCEDURE reject_hit();");
        Check(!Revora.Sistemas.Quarantine.HandleQuarantine(a.Entity,b.Entity,spell),"failed aggregate update rejects gameplay hit");
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_hits WHERE round_id='624a8919-349a-4126-b930-9765fe56e115'"))==2 && a.quarantineKill==0 && b.quarantineDeath==0,"failed hit rolls back receipt and changes no personal counters");
        Check(!Revora.Sistemas.Quarantine.HandleQuarantine(b.Entity,a.Entity,spell) && (bool)type.GetField("scoringFault",Hidden).GetValue(null),"database failure pauses subsequent scoring");
        Sql("DROP TRIGGER reject_hit ON revora_quarantine_rounds;DROP FUNCTION reject_hit();");
        Revora.Sistemas.Quarantine.WhiteScore=99;Revora.Sistemas.Quarantine.End();
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_quarantine_rounds WHERE id='624a8919-349a-4126-b930-9765fe56e115' AND state='draw' AND white_score=1 AND black_score=1 AND cardinality(winners)=0"))==1,"faulted finalization uses saved scores rather than cache");
    }

}
