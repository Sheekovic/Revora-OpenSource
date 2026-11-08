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
    static Guid ancientPaidRun;
    static readonly Type AncientSummons=typeof(Entity).Assembly.GetType("Revora.Game.Features.AncientDevilSummons");
    static object ReserveAncient(GameState p,uint[] selected,bool install=true) {
        lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            var run=Call(AncientSummons,null,"Reserve",c,t,p,selected);t.Commit();
            if(run!=null && install && (bool)AbyssField(run,"Created"))Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.QuestInventory"),null,"Install",p,(uint[])AbyssField(run,"Removed"),new ConquerItem[0]);
            return run;
        }}
    }
    static long SummonCount(GameState p){return Scalar("SELECT count(*) FROM revora_ancient_summons WHERE uid=@uid",p.Entity.UID);}
    static void SealAt(GameState p) {
        p.BindWorldMap(1052);p.Entity.X=216;p.Entity.Y=208;p.ActiveNpc=300017;
        if(!EntityTable.SaveEntity(p))throw new Exception("Summon fixture location save failed.");
    }
    static void VerifyAncientSummons(string runtime,Map town,bool leavePending=false) {
        VerifyAncientQuest(runtime,town);var p=ancientCollector;var island=Kernel.Maps[1052];
        island.AddNpc(new NpcSpawn{UID=300017,MapID=1052,X=218,Y=208,Mesh=2177,Type=Enums.NpcType.Talker});
        Call(AncientSummons,null,"Initialize");Call(AncientSummons,null,"Initialize");
        ancientNow=new DateTime(2033,1,2,8,0,0,DateTimeKind.Utc);AncientSummons.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>ancientNow));
        SealAt(p);uint[] selected=p.Inventory.Objects.OrderBy(i=>i.UID).Take(9).Select(i=>i.UID).ToArray();
        uint spare=p.Inventory.Objects.Single(i=>!selected.Contains(i.UID)).UID;
        foreach(string bad in new[]{"null","eight","ten","duplicate","foreign","bound","locked","stack","warehouse","trade","distant"}) {
            uint[] offered=(uint[])selected.Clone();var first=p.Inventory.Objects.Single(i=>i.UID==selected[0]);
            if(bad=="null")offered=null;if(bad=="eight")offered=selected.Take(8).ToArray();if(bad=="ten")offered=selected.Concat(new[]{spare}).ToArray();
            if(bad=="duplicate")offered[8]=offered[0];if(bad=="foreign")offered[8]=uint.MaxValue;
            if(bad=="bound")first.Bound=true;if(bad=="locked")first.Lock=1;if(bad=="stack")first.StackSize=2;if(bad=="warehouse")first.Warehouse=8;
            if(bad=="trade")p.Trade.InTrade=true;if(bad=="distant")p.Entity.X=250;
            try{Check(AbyssRejected(()=>ReserveAncient(p,offered)) && SummonCount(p)==0 && p.Inventory.Count==10,"invalid offering preserves full earned collection: "+bad);}
            finally{first.Bound=false;first.Lock=0;first.StackSize=1;first.Warehouse=0;p.Trade.InTrade=false;p.Entity.X=216;}
        }
        var original=p.Inventory.Objects.Single(i=>i.UID==selected[0]);var counterfeit=Pack(p,original.ID,1);
        counterfeit.Bound=false;Query("UPDATE items SET bound=0 WHERE uid=@uid","uid",(long)counterfeit.UID);
        var substituted=(uint[])selected.Clone();substituted[0]=counterfeit.UID;
        Check(AbyssRejected(()=>ReserveAncient(p,substituted)) && SummonCount(p)==0,"catalog identity without its earned trial receipt cannot fund a summon");
        p.Inventory.Remove(counterfeit.UID,Enums.ItemUse.None,true);Query("DELETE FROM items WHERE uid=@uid","uid",(long)counterfeit.UID);
        Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)selected[0]);
        Check(AbyssRejected(()=>ReserveAncient(p,selected)) && SummonCount(p)==0,"stale saved item state cannot fund a summon");
        Query("UPDATE items SET locked=0 WHERE uid=@uid","uid",(long)selected[0]);
        Query("CREATE FUNCTION suppress_ancient_offering() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.item_type=3004947 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_ancient_offering BEFORE INSERT ON revora_ancient_offerings FOR EACH ROW EXECUTE FUNCTION suppress_ancient_offering()");
        try{Check(AbyssRejected(()=>ReserveAncient(p,selected)),"suppressed late offering receipt aborts payment");}
        finally{Query("DROP TRIGGER suppress_ancient_offering ON revora_ancient_offerings;DROP FUNCTION suppress_ancient_offering()");}
        Check(SummonCount(p)==0 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==10
            && Scalar("SELECT count(*) FROM revora_ancient_offerings o JOIN revora_ancient_summons s ON s.id=o.summon WHERE s.uid=@uid",p.Entity.UID)==0,
            "late receipt failure restores all nine items and removes partial summon history");
        ulong silver=p.Entity.Money;uint cp=p.Entity.ConquerPoints;
        var initial=new object[8];Parallel.For(0,8,n=>initial[n]=ReserveAncient(p,selected,false));
        object paid=initial.Single(r=>(bool)AbyssField(r,"Created"));Guid id=AbyssId(paid);
        Check(initial.All(r=>AbyssId(r)==id) && initial.Count(r=>(bool)AbyssField(r,"Created"))==1,
            "concurrent first confirmations create one payment and one encounter identity");
        Check((bool)AbyssField(paid,"Created") && ((uint[])AbyssField(paid,"Removed")).SequenceEqual(selected)
            && SummonCount(p)==1 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==1,"nine earned amulets atomically fund one pending summon");
        // Simulate loss after commit, before installing the inventory result.
        p=ReloadBall(p);SealAt(p);
        Check(p.Inventory.Count==1 && p.Inventory.ContainsUID(spare) && p.Entity.Money==silver && p.Entity.ConquerPoints==cp,
            "post-commit reconnect recovers payment while retaining the unselected tenth amulet and currencies");
        var resumed=new object[8];Parallel.For(0,8,n=>resumed[n]=ReserveAncient(p,selected));
        Check(resumed.All(r=>AbyssId(r)==id && !(bool)AbyssField(r,"Created") && ((uint[])AbyssField(r,"Removed")).Length==0)
            && SummonCount(p)==1 && p.Inventory.Count==1,"concurrent retries resume the same paid encounter without repeated removal");
        Check(Scalar("SELECT count(*) FROM revora_ancient_offerings o JOIN revora_ancient_summons s ON s.id=o.summon JOIN revora_ancient_trials t ON t.id=o.origin_trial WHERE s.uid=@uid AND t.state='exchanged' AND o.item_uid=t.amulet_uid AND o.item_type=t.amulet",p.Entity.UID)==9,
            "all consumed offerings retain their actual class-trial provenance after trade");
        Check((string)AbyssField(paid,"Day")=="20330102","paid attempt uses the Pacific quest day");
        if(leavePending){ancientCollector=p;ancientClassPlayers[0]=p;ancientPaidRun=id;return;}
        ancientNow=ancientNow.AddMinutes(30);
        Check(ReserveAncient(p,selected)==null && SummonCount(p)==1 && p.Inventory.ContainsUID(spare),"exact expiry cannot charge another summon on the same quest day");
        Check(Scalar("SELECT count(*) FROM revora_ancient_summons WHERE uid=@uid AND state='expired'",p.Entity.UID)==1,"expired paid encounter is durably closed");
        ancientNow=ancientNow.AddDays(1);
        Check(AbyssRejected(()=>ReserveAncient(p,new[]{spare})) && SummonCount(p)==1,"next day still requires nine newly owned earned amulets");
        new TitleStorage().Login(p);
        Check(HasAchievement(p,11020),"consumed offering and expired summon retain the previously earned collection achievement");
    }
}
