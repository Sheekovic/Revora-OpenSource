using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class PotencyHuntTests
{
    static readonly Type P7Packs=typeof(Entity).Assembly.GetType("Revora.Game.Features.P7SoulPacks");
    static long P7Receipts(Revora.Client.GameState p){return Scalar("SELECT count(*) FROM revora_p7_pack_openings WHERE uid=@uid",p.Entity.UID);}
    static void VerifyP7Packs(string runtime,Map town) {
        InitializeHousingFixture(runtime,town);VerifyArenaConversions(town);Call(P7Packs,null,"Initialize");var now=DateTime.UtcNow;int selected=0;var originalRoll=P7Packs.GetField("Roll",Hidden).GetValue(null);
        P7Packs.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));P7Packs.GetField("Roll",Hidden).SetValue(null,new Func<int,int>(count=>selected));
        try {
            foreach(uint type in new uint[]{3004247,3004248}) {
                var pool=(uint[])P7Packs.GetField(type==3004247?"Weapons":"Equipment",Hidden).GetValue(null);
                foreach(bool bound in new[]{false,true})for(int index=0;index<pool.Length;index++) {
                    selected=index;var p=HousePlayer(town);uint wardrobe=StoredQuestGarment(p);var pack=Pack(p,type,1);pack.Bound=bound;ConquerItemTable.UpdateBound(pack,p);ItemHandler.UseItem(pack,p);
                    var soul=p.Inventory.Objects.Single();
                    Check(!Fenced(p) && soul.ID==pool[index] && soul.Bound && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7 && P7Receipts(p)==1,
                        "every listed P7 soul is reachable and bound: "+type+"/"+index+"/"+bound);
                    p=ReloadBall(p);Check(!Fenced(p) && p.Inventory.Objects.Single().ID==pool[index] && p.Inventory.Objects.Single().Bound,"production reload retains the bound P7 result");
                    Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"P7 pack opening and production reload preserve stored wardrobe design");
                }
            }
            selected=0;var race=HousePlayer(town);var stack=Pack(race,3004247,3);
            Parallel.For(0,12,n=>ItemHandler.UseItem(stack,race));
            Check(!Fenced(race) && stack.StackSize==2 && P7Receipts(race)==1,"concurrent stack use spends one unit and persists its cooldown");
            race=ReloadBall(race);stack=race.Inventory.Objects.Single(i=>i.ID==3004247);ItemHandler.UseItem(stack,race);
            Check(stack.StackSize==2 && P7Receipts(race)==1,"reconnect cannot bypass the saved one-second stack cooldown");
            now=now.AddSeconds(1);ItemHandler.UseItem(stack,race);Check(stack.StackSize==1 && P7Receipts(race)==2,"later deliberate opening spends the next stack unit");
            var full=HousePlayer(town);var token=Pack(full,3004248,2);while(full.Inventory.Count<40)Pack(full,1088001,1);ItemHandler.UseItem(token,full);
            Check(!Fenced(full) && token.StackSize==2 && P7Receipts(full)==0,"full bag preserves a stacked soul pack");
            var single=HousePlayer(town);var one=Pack(single,3004248,0);while(single.Inventory.Count<40)Pack(single,1088001,1);ItemHandler.UseItem(one,single);
            Check(!Fenced(single) && single.Inventory.Count==40 && P7Receipts(single)==1 && !single.Inventory.ContainsUID(one.UID),"legacy singleton uses its freed inventory slot");
            foreach(string fault in new[]{"consume","gift","receipt","draw"}) {
                var p=HousePlayer(town);var source=Pack(p,3004247,1);string table=fault=="receipt"?"revora_p7_pack_openings":"items";
                if(fault=="draw")selected=-1;
                else Query("CREATE FUNCTION suppress_p7_open() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_p7_open BEFORE "+(fault=="consume"?"DELETE":"INSERT")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_p7_open()");
                try{ItemHandler.UseItem(source,p);}finally{selected=0;if(fault!="draw")Query("DROP TRIGGER suppress_p7_open ON "+table+";DROP FUNCTION suppress_p7_open()");}
                Check(Fenced(p) && p.Inventory.ContainsUID(source.UID) && P7Receipts(p)==0 && ConquerItemTable.LoadItem(source.UID).ID==3004247,"failed opening rolls back pack and reward: "+fault);
                p=ReloadBall(p);ItemHandler.UseItem(p.Inventory.Objects.Single(),p);
                Check(!Fenced(p) && P7Receipts(p)==1 && p.Inventory.Objects.Single().Bound,"production reconnect retries a rolled-back pack: "+fault);
            }
        }finally{P7Packs.GetField("Roll",Hidden).SetValue(null,originalRoll);P7Packs.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }
    static void VerifyArenaConversions(Map town) {
        var feature=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArenaPrizeConversions");Call(feature,null,"Initialize");var now=DateTime.UtcNow;
        feature.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        try {
            uint[] types={3009002,3004243,3004244,3004245},results={3009003,3004247,3004248,3004249};
            for(int n=0;n<types.Length;n++)foreach(bool bound in new[]{false,true}) {
                var p=HousePlayer(town);int count=n==0?10:15;var first=Pack(p,types[n],(ushort)(count-1));var last=Pack(p,types[n],0);
                last.Bound=bound;ConquerItemTable.UpdateBound(last,p);while(p.Inventory.Count<40)Pack(p,1088001,1);
                ItemHandler.UseItem(first,p);
                Check(!Fenced(p) && p.Inventory.Count==39 && !p.Inventory.ContainsUID(first.UID) && !p.Inventory.ContainsUID(last.UID)
                    && p.Inventory.Objects.Single(i=>i.ID==results[n]).Bound==bound,"full bag conversion consumes split and zero-stack materials with binding: "+types[n]+"/"+bound);
                p=ReloadBall(p);Check(!Fenced(p) && p.Inventory.Objects.Single(i=>i.ID==results[n]).Bound==bound,"conversion survives production reload");
            }
            var race=HousePlayer(town);var stack=Pack(race,3004243,45);Parallel.For(0,12,n=>ItemHandler.UseItem(stack,race));
            Check(!Fenced(race) && stack.StackSize==30 && race.Inventory.Objects.Count(i=>i.ID==3004247)==1,"concurrent conversion consumes exactly fifteen scraps");
            race=ReloadBall(race);stack=race.Inventory.Objects.Single(i=>i.ID==3004243);ItemHandler.UseItem(stack,race);
            Check(stack.StackSize==30,"conversion cooldown survives reconnect");now=now.AddSeconds(1);ItemHandler.UseItem(stack,race);
            Check(!Fenced(race) && stack.StackSize==15 && race.Inventory.Objects.Count(i=>i.ID==3004247)==2,"later intentional conversion consumes next fifteen");
            var shortBag=HousePlayer(town);var shortStack=Pack(shortBag,3004244,14);ItemHandler.UseItem(shortStack,shortBag);
            Check(!Fenced(shortBag) && shortStack.StackSize==14 && shortBag.Inventory.Count==1,"insufficient scraps preserved");
            var full=HousePlayer(town);var fullStack=Pack(full,3004244,30);while(full.Inventory.Count<40)Pack(full,1088001,1);ItemHandler.UseItem(fullStack,full);
            Check(!Fenced(full) && fullStack.StackSize==30 && full.Inventory.Count==40,"partial stack needs a free output slot");
            var locked=HousePlayer(town);var lockedStack=Pack(locked,3004244,15);lockedStack.Lock=1;ItemHandler.UseItem(lockedStack,locked);
            Check(!Fenced(locked) && locked.Inventory.Count==1 && lockedStack.StackSize==15,"locked materials cannot convert");
            foreach(string fault in new[]{"consume","partial","gift","receipt"}) {
                var p=HousePlayer(town);ushort amount=(ushort)(fault=="partial"?30:15);var source=Pack(p,3004243,amount);string table=fault=="receipt"?"revora_arena_material_conversions":"items";
                Query("CREATE FUNCTION suppress_conversion() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_conversion BEFORE "+(fault=="consume"?"DELETE":fault=="partial"?"UPDATE":"INSERT")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_conversion()");
                try{ItemHandler.UseItem(source,p);}finally{Query("DROP TRIGGER suppress_conversion ON "+table+";DROP FUNCTION suppress_conversion()");}
                Check(Fenced(p) && ConquerItemTable.LoadItem(source.UID).StackSize==amount && Scalar("SELECT count(*) FROM revora_arena_material_conversions WHERE uid=@uid",p.Entity.UID)==0,"failed conversion preserves materials and receipt: "+fault);
                p=ReloadBall(p);ItemHandler.UseItem(p.Inventory.Objects.Single(),p);
                Check(!Fenced(p) && p.Inventory.Objects.Count(i=>i.ID==3004247)==1,"production reconnect retries failed conversion: "+fault);
            }
            Call(P7Packs,null,"Initialize");var journey=HousePlayer(town);uint journeyWardrobe=StoredQuestGarment(journey);var scraps=Pack(journey,3004244,15);ItemHandler.UseItem(scraps,journey);
            journey=ReloadBall(journey);ItemHandler.UseItem(journey.Inventory.Objects.Single(),journey);journey=ReloadBall(journey);
            Check(!Fenced(journey) && journey.Inventory.Objects.Single().Bound && ConquerItemInformation.BaseInformations[journey.Inventory.Objects.Single().ID].PurificationLevel==7,
                "ordinary scrap conversion to pack opening persists a bound P7 soul");
            Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",journeyWardrobe)==1,"scrap conversion, pack opening and production reload preserve stored wardrobe design");
        }finally{feature.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }

}
