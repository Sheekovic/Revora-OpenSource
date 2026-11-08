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
    static readonly Type Treasury=typeof(Entity).Assembly.GetType("Revora.Game.Features.DisCityTreasury");
    static long Openings(GameState p){return Scalar("SELECT COALESCE((SELECT openings FROM revora_dis_treasury WHERE uid=@uid),0)",p.Entity.UID);}
    static ConquerItem HistoricalKey(GameState p,int week) {
        // Seed an earlier completed weekly run; the key uses the same issuer as the genuine final claim.
        var run=Guid.NewGuid();var key=Pack(p,3006982,1);key.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)key.UID);
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            using(var q=new Npgsql.NpgsqlCommand("INSERT INTO revora_dis_completions(uid,week,run,elapsed_seconds,completed_at) VALUES(@uid,@week,@run,500,@now)",c,t)){
                q.Parameters.AddWithValue("uid",(long)p.Entity.UID);q.Parameters.AddWithValue("week",new DateTime(2025,1,6).AddDays(7*week).ToString("yyyyMMdd"));q.Parameters.AddWithValue("run",run);q.Parameters.AddWithValue("now",new DateTimeOffset(disNow));q.ExecuteNonQuery();}
            Call(Treasury,null,"Issue",c,t,p.Entity.UID,run,key);t.Commit();}}
        return key;
    }
    static void VerifyDisTreasury(GameState p,GameState foreign,ConquerItem earnedKey,Map town) {
        foreach(var filler in p.Inventory.Objects.Where(i=>i.ID==1088001).ToArray())DisRemove(p,filler);
        var loaded=ConquerItemTable.LoadItem(earnedKey.UID);
        Check(loaded.UID==earnedKey.UID && loaded.ID==3006982 && loaded.Bound && loaded.StackSize==1 && loaded.Days==0,"production item deserialization restores the issued bound key without an expiry");
        p.Inventory.Remove(earnedKey.UID,Enums.ItemUse.None,false);p.Inventory.Add(loaded,Enums.ItemUse.None);earnedKey=loaded;
        var orphan=Pack(p,3006982,1);orphan.Bound=true;bool rejected=false;
        try{using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){Call(Treasury,null,"Issue",c,t,p.Entity.UID,Guid.NewGuid(),orphan);t.Commit();}}}catch(Npgsql.PostgresException ex){rejected=ex.SqlState=="23503";}
        Check(rejected && Scalar("SELECT count(*) FROM revora_dis_keys WHERE uid=@uid",p.Entity.UID)==1,"deferred victory foreign key rejects an issuer receipt without a completed run");DisRemove(p,orphan);
        DisNpc(foreign,3215,3);Check(foreign.Entity.MapID==1002 && !Fenced(foreign),"treasury admission requires an issued unspent victory key");
        var fake=Pack(foreign,3006982,1);fake.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)fake.UID);
        DisNpc(foreign,3215,3);Check(!Fenced(foreign) && foreign.Entity.MapID==1002,"an unrelated same-type key does not grant treasury admission");DisRemove(foreign,fake);
        ItemHandler.UseItem(earnedKey,p);Check(p.Inventory.ContainsUID(earnedKey.UID) && Openings(p)==0,"using a key outside the treasury preserves it and directs the owner to Solar Saint");
        DisNpc(p,3215,3);Check(!Fenced(p) && p.Map.BaseID==3954 && p.Map.Entities.Values.All(e=>e.EntityFlag!=EntityFlag.Monster),"victorious ordinary player enters the actual private treasure floor without spending a key");
        ushort room=p.Entity.MapID;
        Check(p.Map.Npcs[900000089].Mesh==37490 && MapsTable.MapInformations[room].BaseID==3954,"treasury uses the installed Hell Treasury appearance and explicit treasure mapdoc");
        foreign.Entity.Teleport(3954,room,p.Entity.X,p.Entity.Y);Check(foreign.Entity.MapID==1002,"foreign players cannot enter an owned treasury room");
        // No dialogue offer exists for a forged direct choice.
        DisNpc(p,900000089,1);Check(Openings(p)==0 && p.Inventory.ContainsUID(earnedKey.UID),"direct NPC choice cannot spend a key before its chest offer is presented");
        Treasury.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>0));
        Query("ALTER TABLE revora_dis_treasury_claims ADD CONSTRAINT fail_treasury CHECK(false) NOT VALID");
        DisNpc(p,900000089,0);DisNpc(p,900000089,1);
        Check(Fenced(p) && Openings(p)==0 && p.Inventory.ContainsUID(earnedKey.UID) && Scalar("SELECT count(*) FROM revora_dis_keys WHERE uid=@uid AND used_at IS NOT NULL",p.Entity.UID)==0,"failed treasury receipt rolls back key consumption, reward insertion and opening count");
        Query("ALTER TABLE revora_dis_treasury_claims DROP CONSTRAINT fail_treasury");DemonReset(p);
        var extras=Enumerable.Range(1,5).Select(n=>HistoricalKey(p,n)).ToArray();int before=p.Inventory.Objects.Count(i=>i.ID==3008057);
        DisNpc(p,900000089,0);LabNear(p,900000089);Parallel.For(0,12,n=>Npcs.GetDialog(new NpcRequest(true){NpcID=900000089,OptionID=1},p));
        Check(Openings(p)==1 && !p.Inventory.ContainsUID(earnedKey.UID) && extras.All(i=>p.Inventory.ContainsUID(i.UID)) && p.Inventory.Objects.Count(i=>i.ID==3008057)==before+1,"concurrent chest clicks consume only the offered key and deliver the regular prize once");
        ItemHandler.UseItem(earnedKey,p);Check(Openings(p)==1,"replayed right-click for the spent key cannot use another key");
        DisNpc(p,900000089,0);disNow=disNow.AddMinutes(2);DisNpc(p,900000089,1);Check(Openings(p)==1,"expired chest offer cannot spend a later key");
        var second=extras[0];second.Lock=1;Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)second.UID);ItemHandler.UseItem(second,p);
        Check(Openings(p)==1 && p.Inventory.ContainsUID(second.UID),"locked treasury keys cannot be consumed");second.Lock=0;Query("UPDATE items SET locked=0 WHERE uid=@uid","uid",(long)second.UID);
        Treasury.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>50));int sacred=p.Inventory.Objects.Count(i=>i.ID==3008058);ItemHandler.UseItem(second,p);
        Check(Openings(p)==2 && p.Inventory.Objects.Count(i=>i.ID==3008058)==sacred+1,"regular draw boundary 50 grants the Sacred Refinery prize");
        Treasury.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>85));int yin=p.Inventory.Objects.Count(i=>i.ID==3008059);ItemHandler.UseItem(extras[1],p);
        Check(Openings(p)==3 && p.Inventory.Objects.Count(i=>i.ID==3008059)==yin+1,"regular draw boundary 85 grants the Yin Jade prize");
        Treasury.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>99));ItemHandler.UseItem(extras[2],p);Check(Openings(p)==4,"the last regular draw value remains within its reviewed prize pool");
        while(p.Inventory.Count<40)Pack(p,1088001,1);
        ItemHandler.UseItem(extras[3],p);Check(Openings(p)==4 && p.Inventory.ContainsUID(extras[3].UID),"full bag retains the fifth-opening key and greater prize entitlement");
        var fillers=p.Inventory.Objects.Where(i=>i.ID==1088001).Take(2).ToArray();foreach(var item in fillers)DisRemove(p,item);
        sacred=p.Inventory.Objects.Count(i=>i.ID==3008058);yin=p.Inventory.Objects.Count(i=>i.ID==3008059);
        Treasury.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>{throw new Exception("Fifth opening must not draw.");}));
        Parallel.For(0,12,n=>ItemHandler.UseItem(extras[3],p));
        Check(!Fenced(p) && Openings(p)==5 && p.Inventory.Count==40 && p.Inventory.Objects.Count(i=>i.ID==3008058)==sacred+2 && p.Inventory.Objects.Count(i=>i.ID==3008059)==yin+1,"fifth opening atomically grants two bound Sacred packs and one Yin Jade without a random draw or duplicate charge");
        Check(Scalar("SELECT count(*) FROM revora_dis_treasury_claims WHERE uid=@uid",p.Entity.UID)==5 && Scalar("SELECT count(*) FROM revora_dis_keys WHERE uid=@uid AND used_at IS NOT NULL",p.Entity.UID)==5,"five distinct keys have exactly five durable opening receipts");
        Treasury.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>100));ItemHandler.UseItem(extras[4],p);
        Check(Fenced(p) && Openings(p)==5 && p.Inventory.ContainsUID(extras[4].UID),"invalid regular reward draw preserves the sixth key and opening count");DemonReset(p);
        Treasury.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>49));ItemHandler.UseItem(extras[4],p);
        Check(!Fenced(p) && Openings(p)==6 && p.Inventory.Count==40,"regular opening after the fifth prize restarts the cycle and replaces its key in a full bag");
        DisRemove(p,p.Inventory.Objects.First(i=>i.ID==1088001));var final=HistoricalKey(p,6);
        DisNpc(p,3215,9);Check(p.Entity.MapID==1002 && !Kernel.Maps.ContainsKey(room) && Openings(p)==6,"leaving the treasury releases the room and retains its durable opening history");
        DisNpc(p,3215,3);ushort reload=p.Entity.MapID;var instance=((System.Collections.IDictionary)DisWorld.GetField("Players",Hidden).GetValue(null))[p.Entity.UID];Call(DisWorld,null,"Release",instance);Call(Dis,null,"OnLogin",p);
        Check(!Fenced(p) && p.Entity.MapID==1002 && !Kernel.Maps.ContainsKey(reload) && Openings(p)==6,"persisted treasury map identity recovers login after runtime maps are lost");
        DisNpc(p,3215,3);LabNear(p,900000089);disNow=disNow.AddHours(2);ItemHandler.UseItem(final,p);DisTick();
        Check(Openings(p)==6 && p.Inventory.ContainsUID(final.UID) && p.Entity.MapID==1002,"expired treasury cannot consume a late key and is cleaned up");
        // Remove synthetic historical completion rows after their FK-backed key receipts,
        // leaving the genuine current-week completion for the parent fixture's audit.
        Query("DELETE FROM revora_dis_keys WHERE uid=@uid AND run IN (SELECT run FROM revora_dis_completions WHERE uid=@uid AND week LIKE '2025%')","uid",(long)p.Entity.UID);
        Query("DELETE FROM revora_dis_completions WHERE uid=@uid AND week LIKE '2025%'","uid",(long)p.Entity.UID);
        foreach(var item in p.Inventory.Objects.Where(i=>i.ID==1088001 || i.ID==3006982).ToArray())DisRemove(p,item);
    }
}
