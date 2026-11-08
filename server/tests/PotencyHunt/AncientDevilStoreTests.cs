using System;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type AncientStore=typeof(Entity).Assembly.GetType("Revora.Game.Features.AncientDevilStore");
    static object AncientWrite(GameState p,string method,params object[] args) {
        lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            var result=Call(AncientStore,null,method,new object[]{c,t,p}.Concat(args).ToArray());t.Commit();return result;
        }}
    }
    static void AncientApply(GameState p,ConquerItem item,params uint[] removed) {
        if(item==null)return;
        lock(p.ItemSyncRoot)Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.QuestInventory"),null,"Install",p,removed,new[]{item});
    }
    static Map AncientMap(ushort id,Map terrain) {
        var map=(Map)typeof(CompanionTests).GetMethod("World",Hidden).Invoke(null,new object[]{id});
        map.Floor=terrain.Floor;map.BaseID=1082;
        foreach(var field in typeof(Map).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            if(field.GetValue(map)==null && field.FieldType.IsGenericType && field.FieldType.GetConstructor(Type.EmptyTypes)!=null)
                field.SetValue(map,Activator.CreateInstance(field.FieldType));
        MapsTable.MapInformations[id]=new MapsTable.MapInformation{ID=id,BaseID=1082,Status=8192};return map;
    }
    static void AncientAt(GameState p,Map map,uint guard=300011) {
        p.Entity.MapID=map.ID;p.Entity.X=227;p.Entity.Y=233;p.ActiveNpc=guard;
        if(!EntityTable.SaveEntity(p))throw new Exception("Ancient fixture location save failed.");
    }
    static long AncientState(GameState p,string state) {
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("SELECT count(*) FROM revora_ancient_trials WHERE uid=@uid AND state=@state",c)){
            q.Parameters.AddWithValue("uid",(long)p.Entity.UID);q.Parameters.AddWithValue("state",state);return (long)q.ExecuteScalar();
        }}
    }
    static void VerifyAncientStore(string runtime,Map town) {
        InitializeHousingFixture(runtime,town);Call(AncientStore,null,"Initialize");Call(AncientStore,null,"Initialize");
        var terrain=World(runtime,1082);var island=AncientMap(1052,terrain);var trialMap=AncientMap(11901,terrain);
        island.AddNpc(new NpcSpawn{UID=300011,MapID=1052,X=229,Y=233,Mesh=2177,Type=Enums.NpcType.Talker});
        var p=HousePlayer(town);p.Entity.Class=25;AncientAt(p,island);
        Check(p.Account.State==AccountTable.AccountState.Player,"fixture uses ordinary player");
        Guid id=AbyssId(AncientWrite(p,"Begin",300011u));
        var admissions=new Guid[8];Parallel.For(0,8,n=>admissions[n]=AbyssId(AncientWrite(p,"Begin",300011u)));
        Check(admissions.All(value=>value==id),"concurrent admission resumes one trial");
        Check(AncientWrite(p,"Claim",id)==null,"unfinished trial grants no certificate");
        Guid life=(Guid)AncientWrite(p,"Bind",id,(ushort)11901,400099u);
        Check((Guid)AncientWrite(p,"Bind",id,(ushort)11901,400099u)==life,"repeat encounter binding retains its life identity");
        Check(AbyssRejected(()=>AncientWrite(p,"Bind",id,(ushort)11901,400100u)),"encounter identity cannot be silently replaced");
        Check(!(bool)AncientWrite(p,"Defeat",id,life,7001u,400099u),"island death cannot credit private trial");
        AncientAt(p,trialMap);
        Check(!(bool)AncientWrite(p,"Defeat",id,Guid.NewGuid(),7001u,400099u),"foreign life cannot credit trial");
        Check(!(bool)AncientWrite(p,"Defeat",id,life,7002u,400099u),"wrong family cannot credit trial");
        Check(!(bool)AncientWrite(p,"Defeat",id,life,7001u,400100u),"wrong monster cannot credit trial");
        Query("CREATE FUNCTION suppress_ancient_state() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_ancient_state BEFORE UPDATE ON revora_ancient_trials FOR EACH ROW EXECUTE FUNCTION suppress_ancient_state()");
        Check(AbyssRejected(()=>AncientWrite(p,"Defeat",id,life,7001u,400099u)),"suppressed victory write aborts encounter credit");
        Check(Scalar("SELECT count(*) FROM revora_ancient_lives l JOIN revora_ancient_trials t ON t.id=l.trial WHERE t.uid=@uid AND l.defeated",p.Entity.UID)==0,"failed victory restores undefeated life");
        Query("DROP TRIGGER suppress_ancient_state ON revora_ancient_trials;DROP FUNCTION suppress_ancient_state()");
        Check((bool)AncientWrite(p,"Defeat",id,life,7001u,400099u),"bound encounter commits pending certificate");
        Check(!(bool)AncientWrite(p,"Defeat",id,life,7001u,400099u),"duplicate death grants nothing");
        Check(!(bool)AncientWrite(p,"Abandon",id),"earned certificate entitlement survives abandonment");
        AncientAt(p,island);p=ReloadBall(p);AncientAt(p,island);
        Check(AncientState(p,"victory")==1,"pending certificate survives production reload");
        for(int i=0;i<40;i++)Pack(p,1088001,1);
        Check(AncientWrite(p,"Claim",id)==null && AncientState(p,"victory")==1,"full bag preserves pending certificate");
        var spare=p.Inventory.Objects.First();p.Inventory.Remove(spare.UID,Enums.ItemUse.None,true);Query("DELETE FROM items WHERE uid=@uid","uid",(long)spare.UID);
        Query("CREATE FUNCTION suppress_ancient_item() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_ancient_item BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_ancient_item()");
        Check(AbyssRejected(()=>AncientWrite(p,"Claim",id)) && AncientState(p,"victory")==1,"failed certificate insertion retains entitlement");
        Query("DROP TRIGGER suppress_ancient_item ON items;DROP FUNCTION suppress_ancient_item()");
        var cert=(ConquerItem)AncientWrite(p,"Claim",id);AncientApply(p,cert);
        Check(cert.ID==710016 && cert.Bound && p.Inventory.Count==40 && AncientState(p,"certificate")==1,"certificate is personal and saved once");
        Check(AncientWrite(p,"Claim",id)==null,"claim replay grants nothing");
        p=ReloadBall(p);AncientAt(p,island);cert=p.Inventory.Objects.Single(i=>i.ID==710016);
        Check(AncientWrite(p,"Exchange",id,cert.UID+1)==null,"unrecorded certificate cannot exchange");
        cert.Lock=1;Check(AncientWrite(p,"Exchange",id,cert.UID)==null,"locked certificate cannot exchange");cert.Lock=0;
        Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)cert.UID);
        Check(AbyssRejected(()=>AncientWrite(p,"Exchange",id,cert.UID)),"stale saved certificate cannot exchange");
        Query("UPDATE items SET locked=0 WHERE uid=@uid","uid",(long)cert.UID);
        Query("CREATE FUNCTION suppress_ancient_item() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_ancient_item BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_ancient_item()");
        Check(AbyssRejected(()=>AncientWrite(p,"Exchange",id,cert.UID)) && AncientState(p,"certificate")==1
            && ConquerItemTable.LoadItem(cert.UID).ID==710016,"failed amulet insert restores consumed certificate and receipt");
        Query("DROP TRIGGER suppress_ancient_item ON items;DROP FUNCTION suppress_ancient_item()");
        var amulet=(ConquerItem)AncientWrite(p,"Exchange",id,cert.UID);AncientApply(p,amulet,cert.UID);
        Check(amulet.ID==710011 && !amulet.Bound && p.Inventory.Count==40 && AncientState(p,"exchanged")==1,"earned certificate converts to transferable own-class amulet in a full bag");
        Check(AncientWrite(p,"Exchange",id,cert.UID)==null,"exchange replay grants nothing");
        p=ReloadBall(p);AncientAt(p,island);
        Check(p.Inventory.Objects.Single(i=>i.ID==710011).UID==amulet.UID && !p.Inventory.ContainsUID(cert.UID),"production reload preserves exact exchanged inventory");
        var other=HousePlayer(town);other.Entity.Class=25;AncientAt(other,island);
        Check(AbyssRejected(()=>AncientWrite(other,"Claim",id)),"foreign owner cannot claim a trial");
        Guid abandoned=AbyssId(AncientWrite(p,"Begin",300011u));
        Check((bool)AncientWrite(p,"Abandon",abandoned) && !(bool)AncientWrite(p,"Abandon",abandoned),"unfinished abandonment is idempotent");
        Check(AncientWrite(p,"Claim",abandoned)==null,"abandoned trial grants nothing");
        Check(AbyssId(AncientWrite(p,"Begin",300011u))!=abandoned,"abandonment permits a fresh trial");
        p.Trade.InTrade=true;Check(AbyssRejected(()=>AncientWrite(p,"Begin",300011u)),"trading owner refused");p.Trade.InTrade=false;
        p.Entity.Class=15;Check(AbyssRejected(()=>AncientWrite(p,"Begin",300011u)),"stale or foreign class refused");
        var concurrent=HousePlayer(town);concurrent.Entity.Class=25;AncientAt(concurrent,island);
        Guid concurrentId=AbyssId(AncientWrite(concurrent,"Begin",300011u));
        Guid concurrentLife=(Guid)AncientWrite(concurrent,"Bind",concurrentId,(ushort)11901,400099u);
        AncientAt(concurrent,trialMap);
        Check((bool)AncientWrite(concurrent,"Defeat",concurrentId,concurrentLife,7001u,400099u),"reused map/monster numbers receive a distinct trial life");
        AncientAt(concurrent,island);
        Parallel.For(0,8,n=>{lock(concurrent.ItemSyncRoot){var reward=(ConquerItem)AncientWrite(concurrent,"Claim",concurrentId);AncientApply(concurrent,reward);}});
        Check(concurrent.Inventory.Count==1 && AncientState(concurrent,"certificate")==1,"concurrent claims commit and install exactly one certificate");
        uint concurrentCert=concurrent.Inventory.Objects.Single().UID;
        Query("CREATE FUNCTION suppress_ancient_state() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_ancient_state BEFORE UPDATE ON revora_ancient_trials FOR EACH ROW EXECUTE FUNCTION suppress_ancient_state()");
        Check(AbyssRejected(()=>AncientWrite(concurrent,"Exchange",concurrentId,concurrentCert)),"suppressed final exchange receipt aborts transaction");
        Check(AncientState(concurrent,"certificate")==1 && ConquerItemTable.LoadItem(concurrentCert).ID==710016
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=710011",concurrent.Entity.UID)==0,"final receipt failure restores certificate and removes inserted amulet");
        Query("DROP TRIGGER suppress_ancient_state ON revora_ancient_trials;DROP FUNCTION suppress_ancient_state()");
        Parallel.For(0,8,n=>{lock(concurrent.ItemSyncRoot){var reward=(ConquerItem)AncientWrite(concurrent,"Exchange",concurrentId,concurrentCert);AncientApply(concurrent,reward,concurrentCert);}});
        Check(concurrent.Inventory.Count==1 && concurrent.Inventory.Objects.Single().ID==710011
            && AncientState(concurrent,"exchanged")==1,"concurrent exchanges install exactly one transferable amulet");
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction())
            Check(AbyssRejected(()=>Call(AncientStore,null,"Begin",c,t,concurrent,300011u)),"store refuses missing owner lock");}
        lock(concurrent.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();
            Check(AbyssRejected(()=>Call(AncientStore,null,"Begin",c,null,concurrent,300011u)),"store refuses missing transaction");}
        Check(!HasAchievement(concurrent,11020),"single-class trial and exchange do not award collection achievement");

    }
}
