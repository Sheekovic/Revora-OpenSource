using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Game;
using Revora.Database;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshRebirthSnapshot(GameState p) {return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,
'profile',(SELECT row_to_json(r) FROM (SELECT level,class,reborn,firstrebornclass,firstrebornlevel,strength,agility,vitality,spirit,atributes FROM entities WHERE uid=@uid) r),
'rite',(SELECT row_to_json(r) FROM revora_first_rebirths r WHERE uid=@uid),
'skills',(SELECT json_agg(r ORDER BY id) FROM skills r WHERE entityid=@uid),
'profs',(SELECT json_agg(r ORDER BY id) FROM profs r WHERE entityid=@uid),
'garments',(SELECT json_agg(r ORDER BY pack_uid) FROM revora_rebirth_garments r WHERE uid=@uid))::text","career",FreshChoiceSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshRebirthProcess(string phase) {
        AppDomain.CurrentDomain.FirstChanceException += (sender,e) => {
            var ex=e.Exception as InvalidOperationException;
            if(ex!=null && ex.TargetSite!=null && ex.TargetSite.DeclaringType!=null && new[]{"FirstRebirth","CharacterProgression","AppearanceState","RebirthRules","QuestInventory"}.Contains(ex.TargetSite.DeclaringType.Name))Console.WriteLine("[fresh-rebirth] invariant="+ex.Message);
        };
        var p=FreshLoadSaved();uint uid=p.Entity.UID;Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();
        if(phase.EndsWith("rite")) {
            Require((bool)Sql("SELECT to_regclass('revora_fresh_rebirth_probe') IS NULL") && (long)Sql("SELECT count(*) FROM revora_first_rebirths WHERE uid=@uid","uid",(long)uid)==0);
            bool purchased=p.Inventory.Contains(721259,1);
            if(!purchased)Require(FreshChoiceSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_choice_probe")));
            else {
                // Recover the interrupted attempt only if the entire prior snapshot differs
                // solely by the observed 30-CP stone purchase, controlled travel and its 10612 receipt.
                string recovery=Convert.ToString(Sql(@"WITH a AS (SELECT jsonb_set(@current::jsonb,'{career,career,character,promotion}',(SELECT jsonb_agg(r ORDER BY (r->>'achievement_id')::int) FROM jsonb_array_elements(@current::jsonb#>'{career,career,character,promotion}') r WHERE NOT ((r->>'achievement_id')::int=10612 AND r->>'source'='travel-region-v1' AND r->>'source_key'='10612'))) cur,snapshot::jsonb old FROM revora_fresh_choice_probe),
                b AS (SELECT cur,old,cur#>'{career,career,character,starter}' cs,old#>'{career,career,character,starter}' os FROM a)
                SELECT jsonb_build_array((cur #- '{career,career,character,starter,profile}' #- '{career,career,character,starter,items}')=(old #- '{career,career,character,starter,profile}' #- '{career,career,character,starter,items}'),
                (cs->'profile')-'mapid'-'x'-'y'-'cps'=(os->'profile')-'mapid'-'x'-'y'-'cps',
                (cs#>>'{profile,cps}')::bigint=(os#>>'{profile,cps}')::bigint-30,
                (SELECT jsonb_agg(i ORDER BY (i->>'uid')::bigint) FROM jsonb_array_elements(cs->'items') i WHERE (i->>'id')::int<>721259)=os->'items')::text FROM b","current",FreshChoiceSnapshot(p)));Console.WriteLine("[fresh-rebirth] recovery-checks="+recovery);Require(recovery=="[true, true, true, true]");
            }
            Require(p.Entity.Reborn==0 && p.Entity.Level==120 && p.Entity.Class==15);
            uint cps=p.Entity.ConquerPoints,bound=p.Entity.BoundCps;ulong silver=p.Entity.Money;
            if(p.Entity.MapID==1004)CareerPortal(p,1002);Require(p.Entity.MapID==1002);
            if(!purchased)CareerBuy(p,721259);else cps+=30;var stone=p.Inventory.Objects.Single(i=>i.ID==721259);Require(!stone.Bound && p.Entity.ConquerPoints==cps-30);
            var steed=p.Equipment.GetItems().Single(i=>i.ID==300000);
            string steedBefore=Convert.ToString(Sql("SELECT row_to_json(i)::text FROM items i WHERE uid=@item AND entityid=@uid","item",(long)steed.UID,"uid",(long)uid));
            Console.WriteLine("[fresh-rebirth] steed-wire-red="+steed.NextRed+" saved-bless="+Sql("SELECT bless FROM items WHERE uid=@item","item",(long)steed.UID));
            foreach(byte option in new byte[]{1,200,210,212,214})HeartTalk(p,59558,option);
            Require(!PirateFenced(p) && p.Entity.Reborn==1 && p.Entity.Level==15 && p.Entity.Class==11 && p.Entity.FirstRebornClass==15 && !p.Inventory.ContainsUID(stone.UID));
            Require(steedBefore==Convert.ToString(Sql("SELECT row_to_json(i)::text FROM items i WHERE uid=@item AND entityid=@uid","item",(long)steed.UID,"uid",(long)uid)));
            Require(p.Entity.ConquerPoints==cps-30 && p.Entity.BoundCps==bound && p.Entity.Money==silver);
            Require((long)Sql("SELECT count(*) FROM revora_first_rebirths WHERE uid=@uid AND old_class=15 AND old_level=120 AND new_class=11 AND stone_uid=@stone","uid",(long)uid,"stone",(long)stone.UID)==1);
            Require(p.Inventory.Objects.Any(i=>i.ID==187405 && !i.Bound && i.Days==0));
            Require(EntityTable.SaveEntity(p));string once=FreshRebirthSnapshot(p);HeartTalk(p,59558,214);Require(once==FreshRebirthSnapshot(p));
            Sql("CREATE TABLE revora_fresh_rebirth_probe(uid bigint PRIMARY KEY,stage text,snapshot text);INSERT INTO revora_fresh_rebirth_probe VALUES(@uid,'reborn',@snapshot)","uid",(long)uid,"snapshot",once);
        } else {
            Require(FreshRebirthSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_rebirth_probe")) && p.Entity.Reborn==1 && p.Entity.Level==15 && p.Entity.Class==11);
            if(phase.EndsWith("starter")) {
                Require(Convert.ToString(Sql("SELECT stage FROM revora_fresh_rebirth_probe"))=="reborn");
                var starter=p.Inventory.Objects.Single(i=>i.ID==727990);var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();int count=p.Inventory.Count;
                uint cps=p.Entity.ConquerPoints,bound=p.Entity.BoundCps;ulong silver=p.Entity.Money;
                ItemHandler.UseItem(starter,p);Require(!PirateFenced(p) && p.Inventory.Count==count+8 && p.Inventory.ContainsUID(starter.UID));
                var added=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();
                Require(added.Select(i=>i.ID).OrderBy(i=>i).SequenceEqual(new uint[]{118009,130009,120009,150009,160019,410009,420009,723981}.OrderBy(i=>i)));
                Require(added.All(i=>i.Bound) && added.Where(i=>i.ID!=723981).All(i=>i.Plus==12 && i.Bless==7 && i.SocketOne==Enums.Gem.SuperDragonGem && i.SocketTwo==Enums.Gem.SuperDragonGem));
                string once=FreshRebirthSnapshot(p);ItemHandler.UseItem(starter,p);Require(once==FreshRebirthSnapshot(p));
                var pack=added.Single(i=>i.ID==723981);ItemHandler.UseItem(pack,p);Require(!PirateFenced(p) && !p.Inventory.ContainsUID(pack.UID) && p.Inventory.Count==count+8);
                uint result=Convert.ToUInt32(Sql("SELECT result_uid FROM revora_rebirth_garments WHERE uid=@uid AND pack_uid=@pack","uid",(long)uid,"pack",(long)pack.UID));
                var garment=p.Inventory.Objects.Single(i=>i.UID==result);uint stars=StorageItem.Garments[garment.ID].Stars;
                Require(garment.Bound && (stars==4 || stars==5) && garment.Bless<=1 && garment.Days==0 && garment.TimeLeftInMinutes==0);
                Require(p.Entity.ConquerPoints==cps && p.Entity.BoundCps==bound && p.Entity.Money==silver);
                once=FreshRebirthSnapshot(p);ItemHandler.UseItem(pack,p);Require(once==FreshRebirthSnapshot(p));
                Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_rebirth_probe SET stage='claimed',snapshot=@snapshot","snapshot",FreshRebirthSnapshot(p));
                Console.WriteLine("[fresh-rebirth] bound-garment="+garment.ID+" stars="+stars+" blessing="+garment.Bless);
            }
            Require(Convert.ToString(Sql("SELECT stage FROM revora_fresh_rebirth_probe"))=="claimed");
            string saved=FreshRebirthSnapshot(p);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==727990),p);HeartTalk(p,59558,214);Require(!PirateFenced(p) && saved==FreshRebirthSnapshot(p));
            Require((long)Sql("SELECT count(*) FROM revora_custom_starter_claims WHERE uid=@uid AND reborn=1 AND milestone=1","uid",(long)uid)==1);
        }
        Console.WriteLine("[career-process] "+phase+"-passed uid="+uid+" level="+p.Entity.Level+" class="+p.Entity.Class+" reborn="+p.Entity.Reborn);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh rebirth did not close.");
    }
}
