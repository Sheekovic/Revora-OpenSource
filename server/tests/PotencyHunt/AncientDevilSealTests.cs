using System;
using System.Linq;
using System.Threading.Tasks;
using Revora.Network;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;

internal static partial class PotencyHuntTests
{
    static readonly Type AncientSeal=typeof(Entity).Assembly.GetType("Revora.Game.Features.AncientDevilSeal");
    static object SealOffer(GameState p) {
        var table=AncientSeal.GetField("offers",Hidden).GetValue(null);var args=new object[]{p,null};
        return (bool)table.GetType().GetMethod("TryGetValue").Invoke(table,args)?args[1]:null;
    }
    static void VerifyAncientSeal(string runtime,Map town) {
        VerifyAncientQuest(runtime,town);var p=ancientCollector;SealAt(p);
        AncientSummons.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>ancientNow));
        Check(p.Map.Npcs[300017].X==218 && p.Map.Npcs[300017].Y==208,"quest initializes reachable public Ancient Seal");
        AncientDialog(p,300017,1);Check(SummonCount(p)==0 && p.Inventory.Count==10,"unreviewed confirmation cannot spend earned amulets");
        AncientDialog(p,300017,0);var offer=SealOffer(p);uint spared=(uint)AbyssField(offer,"Spare");var selected=(uint[])AbyssField(offer,"Items");
        Check(selected.Length==9 && selected.Distinct().Count()==9 && p.Inventory.Objects.Single(i=>i.ID==spared)!=null,"seal reviews nine exact earned items and one spared class");
        PacketHandler.HandlePacket(AncientDialogFrame(300016,1),p);
        Check(SummonCount(p)==0,"wrong NPC selector cannot reuse seal review");
        p.Entity.X=250;AncientDialog(p,300017,1);p.Entity.X=216;
        p.Trade.InTrade=true;AncientDialog(p,300017,1);p.Trade.InTrade=false;
        Check(SummonCount(p)==0 && !Fenced(p),"distance and trade block seal payment before mutation");
        AncientDialog(p,300017,4);offer=SealOffer(p);Check((uint)AbyssField(offer,"Spare")!=spared,"player can change the class amulet to keep");
        selected=(uint[])AbyssField(offer,"Items");uint retained=p.Inventory.Objects.Single(i=>!selected.Contains(i.UID)).UID;
        ancientNow=ancientNow.AddSeconds(60);AncientDialog(p,300017,1);Check(SummonCount(p)==0,"expired review cannot authorize payment");
        AncientDialog(p,300017,0);offer=SealOffer(p);selected=(uint[])AbyssField(offer,"Items");
        var item=p.Inventory.Objects.Single(i=>i.UID==selected[0]);item.Lock=1;AncientDialog(p,300017,1);item.Lock=0;
        Check(!Fenced(p) && SummonCount(p)==0 && p.Inventory.Count==10,"changed offering refreshes review without charging or disconnecting");
        AncientDialog(p,300017,0);p=ReloadBall(p);SealAt(p);AncientDialog(p,300017,1);
        Check(SummonCount(p)==0,"reconnect invalidates old session review");
        AncientDialog(p,300017,0);selected=(uint[])AbyssField(SealOffer(p),"Items");retained=p.Inventory.Objects.Single(i=>!selected.Contains(i.UID)).UID;
        Parallel.For(0,8,n=>AncientDialog(p,300017,1));Check(!Fenced(p) && SummonCount(p)==1 && p.Entity.MapID>=11000 && p.Inventory.Count==1 && p.Inventory.ContainsUID(retained),"reviewed seal confirmation consumes exactly nine and enters actual guarded encounter");
        var registry=AncientBossWorld.GetField("Hunts",Hidden).GetValue(null);var hunt=registry.GetType().GetProperty("Item").GetValue(registry,new object[]{p.Entity.MapID});
        var actors=BossActors(hunt);AncientHit(p.Entity,actors[0],actors[0].Hitpoints);
        AdventureNear(p,300016);AncientDialog(p,300016,0);AncientDialog(p,300016,1);SealAt(p);
        AncientDialog(p,300017,0);Check((byte)AbyssField(SealOffer(p),"Option")==2,"paid unfinished encounter offers resume instead of new payment");
        AncientDialog(p,300017,2);Check(!Fenced(p) && p.Map.Entities.Count==4 && p.Inventory.ContainsUID(retained),"seal resumes remaining guards without another offering");
        hunt=registry.GetType().GetProperty("Item").GetValue(registry,new object[]{p.Entity.MapID});actors=BossActors(hunt);
        for(int slot=1;slot<5;slot++){AdventureWalk(p,actors[slot].X,(ushort)(actors[slot].Y-1));AncientHit(p.Entity,actors[slot],actors[slot].Hitpoints);}
        Call(AncientQuest,null,"Tick");SealAt(p);AncientDialog(p,300017,0);
        Check(!Fenced(p) && (byte)AbyssField(SealOffer(p),"Option")==3,"actual guarded victory offers reward through ordinary dialogue");
        var fill=new System.Collections.Generic.List<uint>();while(p.Inventory.Count<40)fill.Add(Pack(p,1088001,1).UID);
        AncientDialog(p,300017,3);Check(p.Inventory.Count==40 && (byte)AbyssField(SealOffer(p),"Option")==3,"full inventory preserves victory and offers claim retry");
        foreach(uint uid in fill)RemoveFixtureItem(p,uid);
        p=ReloadBall(p);SealAt(p);AncientDialog(p,300017,0);Parallel.For(0,8,n=>AncientDialog(p,300017,3));int total=p.Inventory.Count;
        Check(!Fenced(p) && (total==2 || total==7) && SealOffer(p)==null,"reconnected ordinary player claims saved victory once");
        AncientDialog(p,300017,3);AncientDialog(p,300017,1);p=ReloadBall(p);
        Check(p.Inventory.Count==total && SummonCount(p)==1,"replayed claim and summon cannot duplicate reward or payment after reload");
    }
}
