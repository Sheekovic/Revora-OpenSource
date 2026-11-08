using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Console=System.Console;

internal static partial class CheckCandidateArenaLifecycle {
    static bool mainPackFixture;
    static string MainPackSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'uses',(SELECT json_agg(r ORDER BY item_type,id) FROM revora_silver_study_bag_uses r WHERE uid=@uid))::text",
"snapshot",NobilityClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static GameState MainPackLoad(){nextUid=4294917901;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);return p;}
    static void MainPackProcess(bool reload){
        mainPackFixture=true;const uint uid=4294917901;
        uint[] amounts={20,30,50,80,100,200,300,500,800,1000};
        if((bool)Sql("SELECT to_regclass('revora_main_pack_probe') IS NULL")){
            Require(!reload);
            if((long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0){nextUid=uid;Player(Kernel.Maps[1002]);}
            else Require((long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND id BETWEEN 3005107 AND 3005116","uid",(long)uid)==10);
            var fresh=MainPackLoad();
            for(uint id=3005107;id<=3005116;id++)if(!fresh.Inventory.Contains(id,1))RefineryClaimGive(fresh,id);
            Require(EntityTable.SaveEntity(fresh));Sql("CREATE TABLE revora_main_pack_probe(snapshot text);INSERT INTO revora_main_pack_probe VALUES(NULL)");
        }
        var p=MainPackLoad();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_main_pack_probe")==DBNull.Value){
            Require(!reload);
            if((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid","uid",(long)uid)==0){
                var first=p.Inventory.Objects.Single(i=>i.ID==3005107);
                p.Entity.BoundCps=uint.MaxValue-10;string overflow=MainPackSnapshot(p);
                RetainedUpgradeUsage(p,first.UID,0,ItemUsage.Mode.EquipItem);Require(overflow==MainPackSnapshot(p) && p.Inventory.ContainsUID(first.UID));p.Entity.BoundCps=0;
                string before=MainPackSnapshot(p);
                Sql(@"CREATE OR REPLACE FUNCTION revora_main_pack_probe_reject() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.uid=4294917901 THEN RETURN NULL; END IF; RETURN NEW; END $$;
DROP TRIGGER IF EXISTS revora_main_pack_probe_reject ON revora_silver_study_bag_uses;
CREATE TRIGGER revora_main_pack_probe_reject BEFORE INSERT ON revora_silver_study_bag_uses FOR EACH ROW EXECUTE FUNCTION revora_main_pack_probe_reject();");
                try{RetainedUpgradeUsage(p,first.UID,0,ItemUsage.Mode.EquipItem);Require(PirateFenced(p) && before==MainPackSnapshot(p));}
                finally{Sql("DROP TRIGGER revora_main_pack_probe_reject ON revora_silver_study_bag_uses;DROP FUNCTION revora_main_pack_probe_reject();");}
                p=MainPackLoad();Require(!PirateFenced(p) && before==MainPackSnapshot(p));
            }
            for(int index=0;index<amounts.Length;index++){
                uint type=3005107u+(uint)index;
                if((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type=@type","uid",(long)uid,"type",(int)type)==0){
                    var pack=p.Inventory.Objects.Single(i=>i.ID==type);uint bound=p.Entity.BoundCps,cp=p.Entity.ConquerPoints;ulong silver=p.Entity.Money;
                    System.Threading.Tasks.Parallel.For(0,8,_=>RetainedUpgradeUsage(p,pack.UID,0,ItemUsage.Mode.EquipItem));
                    Require(!PirateFenced(p) && !p.Inventory.ContainsUID(pack.UID) && p.Entity.BoundCps==bound+amounts[index] && p.Entity.ConquerPoints==cp && p.Entity.Money==silver);
                }
                Require((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type=@type AND boundcps=@amount AND silver=0 AND study=0","uid",(long)uid,"type",(int)type,"amount",(long)amounts[index])==1);
            }
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_main_pack_probe SET snapshot=@snapshot","snapshot",MainPackSnapshot(p));
        }
        Require(p.Inventory.Count==0 && p.Entity.BoundCps==3080);
        Require((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid","uid",(long)uid)==10);
        string exact=MainPackSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_pack_probe")));
        Require(EntityTable.SaveEntity(p));Require(exact==MainPackSnapshot(p));
        Console.WriteLine("[career-process] career-main-packs-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main packs did not close.");
    }
}
