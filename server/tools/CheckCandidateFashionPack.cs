using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool fashionPackFixture;
    static string FashionPackSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'uses',(SELECT json_agg(u ORDER BY id) FROM revora_fashion_suit_pack_uses u WHERE uid=@uid))::text",
        "snapshot",NobilityClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FashionPackProcess(bool reload){
        fashionPackFixture=true;const uint uid=4294910601;
        if((bool)Sql("SELECT to_regclass('revora_fashion_pack_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=100 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_fashion_pack_probe(snapshot text);INSERT INTO revora_fashion_pack_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);p.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_fashion_pack_probe")==DBNull.Value){
            Require(!reload && p.Inventory.Count==0);var pack=RefineryClaimGive(p,3001264);uint packUid=pack.UID;
            var feature=Feature("GemDragonSoulPacks");var roll=feature.GetField("Roll",Hidden);object oldRoll=roll.GetValue(null);
            var pool=(uint[])Call(feature,"Pool",3001264u);Require(pool.Length>0 && pool.All(id=>StorageItem.Garments[id].Stars==5));
            try{
                string before=FashionPackSnapshot(p);roll.SetValue(null,(Func<int,int,int>)((min,max)=>max));
                RetainedUpgradeUsage(p,packUid,0,ItemUsage.Mode.EquipItem);Require(before==FashionPackSnapshot(p) && !PirateFenced(p));
                roll.SetValue(null,(Func<int,int,int>)((min,max)=>0));
                Sql(@"CREATE FUNCTION revora_fashion_pack_fault() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
IF NEW.uid=4294910601 THEN RAISE EXCEPTION 'controlled fashion receipt fault'; END IF; RETURN NEW; END $$;
CREATE TRIGGER revora_fashion_pack_fault BEFORE INSERT ON revora_fashion_suit_pack_uses FOR EACH ROW EXECUTE FUNCTION revora_fashion_pack_fault();");
                try{RetainedUpgradeUsage(p,packUid,0,ItemUsage.Mode.EquipItem);Require(PirateFenced(p) && before==FashionPackSnapshot(p));}
                finally{Sql("DROP TRIGGER revora_fashion_pack_fault ON revora_fashion_suit_pack_uses;DROP FUNCTION revora_fashion_pack_fault()");}
                nextUid=uid;p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);Require(!PirateFenced(p) && p.Inventory.ContainsUID(packUid));
                var actor=p;System.Threading.Tasks.Parallel.For(0,8,_=>RetainedUpgradeUsage(actor,packUid,0,ItemUsage.Mode.EquipItem));
                var reward=p.Inventory.Objects.Single();Require(reward.ID==pool[0] && reward.Bound && reward.Plus==0 && reward.StackSize==1 && reward.Days==7);
                Require(!p.Inventory.ContainsUID(packUid) && !PirateFenced(p));
                string once=FashionPackSnapshot(p);RetainedUpgradeUsage(p,packUid,0,ItemUsage.Mode.EquipItem);Require(once==FashionPackSnapshot(p));
                RetainedUpgradeUsage(p,reward.UID,9,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(9).UID==reward.UID && reward.Days==7);
            }finally{roll.SetValue(null,oldRoll);}
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fashion_pack_probe SET snapshot=@snapshot","snapshot",FashionPackSnapshot(p));
        }
        var garment=p.Equipment.TryGetItem(9);Require(garment!=null && garment.Days==7 && garment.Bound && StorageItem.Garments[garment.ID].Stars==5 && p.Inventory.Count==0);
        Require((long)Sql("SELECT count(*) FROM revora_fashion_suit_pack_uses WHERE uid=@uid AND days=7 AND result_type=@type","uid",(long)uid,"type",(int)garment.ID)==1);
        Require((bool)Sql("SELECT to_regprocedure('revora_fashion_pack_fault()') IS NULL"));
        string exact=FashionPackSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_fashion_pack_probe")));
        System.Console.WriteLine("[career-process] career-fashion-pack-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fashion pack did not close.");
    }
}
