using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Relics=typeof(Entity).Assembly.GetType("Revora.Game.Features.DisCityRelics");
    static long RelicClaims(GameState p){return Scalar("SELECT count(*) FROM revora_dis_relic_claims WHERE uid=@uid",p.Entity.UID);}
    static ConquerItem HistoricalSword(GameState p,int number) {
        var run=Guid.NewGuid();var sword=Pack(p,723088,1);sword.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)sword.UID);
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            using(var q=new Npgsql.NpgsqlCommand("INSERT INTO revora_dis_completions(uid,week,run,elapsed_seconds,completed_at) VALUES(@uid,@week,@run,500,@now)",c,t)){
                q.Parameters.AddWithValue("uid",(long)p.Entity.UID);q.Parameters.AddWithValue("week",new DateTime(2024,1,1).AddDays(7*number).ToString("yyyyMMdd"));q.Parameters.AddWithValue("run",run);q.Parameters.AddWithValue("now",new DateTimeOffset(disNow));q.ExecuteNonQuery();}
            Call(Relics,null,"Issue",c,t,p.Entity.UID,run,sword);t.Commit();}}return sword;
    }
    static void VerifyDisRelics(GameState p,GameState foreign) {
        var sword=p.Inventory.Objects.Single(i=>i.ID==723088);
        Check(sword.Bound && DisValue(p,"star_sword")==1 && Scalar("SELECT count(*) FROM revora_dis_relics WHERE uid=@uid AND item_type=723088",p.Entity.UID)==1,"genuine Pluto victory persists the selected Star Sword and issues it with the final reward");
        foreach(int draw in new[]{0,19,20,99}){Relics.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>draw));Check((bool)Call(Relics,null,"DrawSword")== (draw<20),"Star Sword's reviewed twenty-percent boundary "+draw);}
        Relics.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>100));bool invalid=false;try{Call(Relics,null,"DrawSword");}catch(InvalidOperationException){invalid=true;}Check(invalid,"invalid relic draw fails without inventing a reward");
        Relics.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>0));
        DisNpc(p,3215,40);Check(RelicClaims(p)==0 && p.Inventory.ContainsUID(sword.UID),"a forged dress choice without a presented sword offer cannot consume it");
        var unrelated=Pack(foreign,723088,1);unrelated.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)unrelated.UID);DisNpc(foreign,3215,4);DisNpc(foreign,3215,40);
        Check(!Fenced(foreign) && RelicClaims(foreign)==0 && foreign.Inventory.ContainsUID(unrelated.UID),"unissued Star Sword cannot purchase an exotic dress");DisRemove(foreign,unrelated);
        ItemHandler.UseItem(sword,p);Check(p.Inventory.ContainsUID(sword.UID),"right-click explains the exchange without consuming the sword");
        DisNpc(p,3215,4);disNow=disNow.AddMinutes(2);DisNpc(p,3215,40);Check(RelicClaims(p)==0,"expired dress offer cannot be redeemed");
        sword.Lock=1;Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)sword.UID);DisNpc(p,3215,4);DisNpc(p,3215,40);
        Check(RelicClaims(p)==0 && p.Inventory.ContainsUID(sword.UID),"locked sword cannot be offered or consumed");sword.Lock=0;Query("UPDATE items SET locked=0 WHERE uid=@uid","uid",(long)sword.UID);
        DisNpc(p,3215,4);Query("ALTER TABLE revora_dis_relic_claims ADD CONSTRAINT fail_relic CHECK(false) NOT VALID");DisNpc(p,3215,40);
        Check(Fenced(p) && RelicClaims(p)==0 && p.Inventory.ContainsUID(sword.UID) && Scalar("SELECT count(*) FROM revora_dis_relics WHERE uid=@uid AND used_at IS NOT NULL",p.Entity.UID)==0,"failed dress receipt rolls back sword consumption and its replacement outfit");
        Query("ALTER TABLE revora_dis_relic_claims DROP CONSTRAINT fail_relic");DemonReset(p);DisNpc(p,3215,4);LabNear(p,3215);Parallel.For(0,12,n=>Npcs.GetDialog(new NpcRequest(true){NpcID=3215,OptionID=40},p));
        var phoenix=p.Inventory.Objects.Single(i=>i.ID==137023);
        Check(RelicClaims(p)==1 && !p.Inventory.ContainsUID(sword.UID) && phoenix.Bound && phoenix.Days==0 && p.Inventory.Count==40,"concurrent exchange replaces one sword with a permanent bound Phoenix Dress in a full bag");
        foreach(var item in p.Inventory.Objects.Where(i=>i.ID==1088001).ToArray())DisRemove(p,item);
        var second=HistoricalSword(p,1);var third=HistoricalSword(p,2);DisNpc(p,3215,42);
        Check(RelicClaims(p)==1 && p.Inventory.ContainsUID(second.UID) && p.Inventory.ContainsUID(third.UID),"replayed choice cannot silently spend the next earned sword");
        DisNpc(p,3215,4);DisNpc(p,3215,41);DisNpc(p,3215,4);DisNpc(p,3215,42);
        Check(RelicClaims(p)==3 && p.Inventory.Objects.Any(i=>i.ID==137033) && p.Inventory.Objects.Any(i=>i.ID==137043),"other earned swords offer the Elegant and Celestial Dress choices");
        foreach(uint id in new uint[]{137023,137033,137043}) {
            var dress=p.Inventory.Objects.Single(i=>i.ID==id);var reload=ConquerItemTable.LoadItem(dress.UID);
            Check(reload.ID==id && reload.Bound && reload.Days==0 && ItemHandler.ItemPosition(id)==ConquerItem.Armor,"exotic dress identity, binding and permanent armor-slot classification survive loading "+id);
            ItemHandler.EquipItem(new ItemUsage(true){UID=dress.UID,dwParam=ConquerItem.Armor,Action=ItemUsage.Mode.EquipItem},p);
            Check(p.Equipment.TryGetItem(ConquerItem.Armor)!=null && p.Equipment.TryGetItem(ConquerItem.Armor).UID==dress.UID && SavedRune(dress,"position")==ConquerItem.Armor,"ordinary equipment handler can wear the selected exotic dress "+id);
            ItemHandler.UnEquipItem(p,ConquerItem.Armor);
        }
        Query("DELETE FROM revora_dis_relics WHERE uid=@uid AND run IN (SELECT run FROM revora_dis_completions WHERE uid=@uid AND week LIKE '2024%')","uid",(long)p.Entity.UID);
        Query("DELETE FROM revora_dis_completions WHERE uid=@uid AND week LIKE '2024%'","uid",(long)p.Entity.UID);
    }
}
