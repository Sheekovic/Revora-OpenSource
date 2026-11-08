using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type AncientStone=typeof(Entity).Assembly.GetType("Revora.Game.Features.AncientStoneCombine");
    static readonly Type EasterGoldService=typeof(Entity).Assembly.GetType("Revora.Game.Features.EasterGoldPacks");
    static DateTime holyStoneNow=DateTime.UtcNow;
    static long HolyStoneUses(GameState p){return Scalar("SELECT count(*) FROM revora_ancient_stone_combines WHERE uid=@uid",p.Entity.UID);}
    static void OpenHolyStone(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static int StoneUnits(GameState p){return p.Inventory.Objects.Where(i=>i.ID==3004246u).Sum(i=>i.StackSize<=1?1:i.StackSize);}
    static void VerifyAncientStoneCombine(string runtime,Map town){
        Call(AncientStone,null,"Initialize");Call(AncientStone,null,"Initialize");
        Call(EasterGoldService,null,"Initialize");
        AncientStone.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>holyStoneNow));
        EasterGoldService.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>holyStoneNow));
        EasterGoldService.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>1));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/AncientStoneCombine.cs")));
        string inventory=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/ConquerStructures/Inventory.cs")));
        string monsters=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/MonsterTable.cs")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        string chat=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/Chat.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string shopDat=File.ReadAllText("database\\shops\\Shop.dat");
        string v2=File.ReadAllText("database\\shops\\emoneyshopV2.ini");
        string eshop=File.ReadAllText("database\\shops\\emoneyshop.ini");
        string shopTxt=File.ReadAllText("database\\shops\\Shop.txt");
        string exchange=File.ReadAllText("database\\shops\\exchange_shop_goods.txt");
        string heroes=File.ReadAllText("database\\Slayer\\StageGoal.txt");
        Check(handler.IndexOf("Ancient Stone combine must use the owned service.")>=0 && handler.IndexOf("AncientStoneCombine.TryUse")>=0
            && handler.IndexOf("case 3004246:")>=0,"3004246 is fail-closed on the live handler");
        Check(handler.IndexOf("AncientStoneCombine.TryUse")>handler.IndexOf("DailySignInPacks.TryUse")
            && usage.IndexOf("AncientStoneCombine.TryHandle")>usage.IndexOf("DailySignInPacks.TryHandle")
            && thread.IndexOf("AncientStoneCombine.Initialize")>thread.IndexOf("DailySignInPacks.Initialize"),
            "AncientStoneCombine intercepts sit after DailySignInPacks");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0
            && audit.IndexOf("| single-consume | 55 |")>=0 && audit.IndexOf("| 721624 | owned-fail-closed | source+timer |")>=0,
            "census is 145 live-split-write / 275 owned-fail-closed / 55 single-consume after Bloodshed Sea ownership");
        Check(audit.IndexOf("| 3004246 | owned-fail-closed | source+items |")>=0,"audit owns 3004246");
        Check(catalog.IndexOf("3004246@@AncientStone")>=0 && catalog.IndexOf("Collect~200~to~get~a~P7")>=0
            && service.IndexOf("Cost=20")>=0 && service.IndexOf("Count>31")>=0
            && service.IndexOf("You do not have 20 pieces, or you lack 5 inventory spaces.")>=0
            && service.IndexOf("Collect~200")<0,
            "catalog Collect-200/P7 copy is not adopted; leftover 20-unit Count<=31 contract is owned");
        Check(inventory.IndexOf("public bool Contains(uint ID, uint times)")>=0 && inventory.IndexOf("public bool Remove(uint ID, uint times)")>=0,
            "leftover combine used inventory-wide Contains/Remove of 20 units");
        Check(monsters.IndexOf("ItemID = 3004246; //HolyStone")>=0 && monsters.IndexOf("Name == \"FuriousBear\"")>=0
            && monsters.IndexOf("Kernel.Random.Next(1, 17)")>=0
            && PublicScalar("SELECT count(*) FROM public.monsterspawns s JOIN public.monsterinfos m ON m.id=s.npctype WHERE m.name='FuriousBear'")>=1,
            "spawned SuperBoss FuriousBear VIP6/VIP7 loot can floor-drop 3004246");
        Check(npcs.IndexOf("FuriousBear[xx:56]")>=0 && npcs.IndexOf("case 900031:")>=0,
            "NPC 900031 can summon FuriousBear on the xx:56 window");
        Check(chat.IndexOf("SpecialID = 3004246;")>=0 && chat.IndexOf("Contains(\"santo\")")>=0,
            "GM event chat santo can issue 3004246");
        Check(eshop.IndexOf("3004246")<0 && v2.IndexOf("3004246")<0 && shopTxt.IndexOf("3004246")<0 && shopDat.IndexOf("3004246")<0
            && exchange.IndexOf("3004246")<0 && heroes.IndexOf("3004246")<0,
            "3004246 has no gold/emoney/exchange/StageGoal producer");
        string heard="";AncientStone.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        ushort packDur=ConquerItemInformation.BaseInformations[720121u].Durability;
        heard="";var one=WelcomePlayer(town,1,0,15);var oneSrc=Pack(one,3004246,20);OpenHolyStone(one,oneSrc);
        var pack=one.Inventory.Objects.Single(i=>i.ID==720121);
        Check(!Fenced(one) && HolyStoneUses(one)==1 && !one.Inventory.ContainsUID(oneSrc.UID) && CountId(one,720121)==1 && StoneUnits(one)==0
            && !pack.Bound && pack.Plus==0 && pack.StackSize==1 && pack.Days==0 && pack.Durability==packDur && pack.MaximDurability==packDur
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid AND bound=0 AND plus=0 AND stacksize=1 AND durability="+packDur+" AND maximdurability="+packDur,pack.UID)==1
            && heard=="","20-stack combine pays one permanent unbound plus-0 720121 silently");
        foreach(bool boundSrc in new[]{false,true})foreach(ushort stack in new ushort[]{20,21,22}){
            var p=WelcomePlayer(town,1,0,15);var src=NestedPack(p,3004246,stack,boundSrc);
            Parallel.For(0,24,i=>{if(i%3==0)EntityTable.SaveEntity(p);else OpenHolyStone(p,src);});
            Check(!Fenced(p) && HolyStoneUses(p)==1 && CountId(p,720121)==1 && !p.Inventory.Objects.Any(i=>i.ID==720121 && i.Bound)
                && (stack==20?!p.Inventory.ContainsUID(src.UID):src.StackSize==(ushort)(stack-20)),
                "3004246 pays with leftover stack remainder: "+boundSrc+"/"+stack);
        }
        var singles=WelcomePlayer(town,1,0,15);for(int n=0;n<20;n++)Pack(singles,3004246,1);var click=singles.Inventory.Objects.First(i=>i.ID==3004246);OpenHolyStone(singles,click);
        Check(!Fenced(singles) && HolyStoneUses(singles)==1 && CountId(singles,720121)==1 && StoneUnits(singles)==0,"20 singleton objects combine exactly 20 units");
        var zeros=WelcomePlayer(town,1,0,15);for(int n=0;n<20;n++)NestedPack(zeros,3004246,0,false);OpenHolyStone(zeros,zeros.Inventory.Objects.First(i=>i.ID==3004246));
        Check(!Fenced(zeros) && HolyStoneUses(zeros)==1 && CountId(zeros,720121)==1 && StoneUnits(zeros)==0,"20 stack-0 objects still combine as 20 leftover units");
        var mixed=WelcomePlayer(town,1,0,15);var small=Pack(mixed,3004246,15);var large=Pack(mixed,3004246,10);OpenHolyStone(mixed,small);
        Check(!Fenced(mixed) && HolyStoneUses(mixed)==1 && !mixed.Inventory.ContainsUID(small.UID) && large.StackSize==5 && CountId(mixed,720121)==1,
            "15+10 stacks consume exactly 20 and keep a remainder of 5");
        var skip=WelcomePlayer(town,1,0,15);var lone=Pack(skip,3004246,1);var pile=Pack(skip,3004246,25);OpenHolyStone(skip,lone);
        Check(!Fenced(skip) && HolyStoneUses(skip)==1 && skip.Inventory.ContainsUID(lone.UID) && pile.StackSize==5 && CountId(skip,720121)==1,
            "a later stack of 20+ supplies the whole combine and leaves earlier small stacks");
        var boundMix=WelcomePlayer(town,1,0,15);for(int n=0;n<10;n++)NestedPack(boundMix,3004246,1,true);for(int n=0;n<10;n++)NestedPack(boundMix,3004246,1,false);
        OpenHolyStone(boundMix,boundMix.Inventory.Objects.First(i=>i.ID==3004246));
        Check(!Fenced(boundMix) && HolyStoneUses(boundMix)==1 && StoneUnits(boundMix)==0 && CountId(boundMix,720121)==1
            && boundMix.Inventory.Objects.Single(i=>i.ID==720121).Bound==false,"mixed source bindings still yield unbound 720121");
        heard="";var fit=WelcomePlayer(town,1,0,15);var fitSrc=Pack(fit,3004246,20);while(fit.Inventory.Count<31)Pack(fit,1000000,1);OpenHolyStone(fit,fitSrc);
        Check(!Fenced(fit) && HolyStoneUses(fit)==1 && CountId(fit,720121)==1,"Count 31 still accepts leftover Count<=31");
        heard="";var full=WelcomePlayer(town,1,0,15);var fullSrc=Pack(full,3004246,20);while(full.Inventory.Count<32)Pack(full,1000000,1);OpenHolyStone(full,fullSrc);
        Check(!Fenced(full) && HolyStoneUses(full)==0 && full.Inventory.ContainsUID(fullSrc.UID) && CountId(full,720121)==0
            && heard.IndexOf("You do not have 20 pieces, or you lack 5 inventory spaces.")>=0,"Count 32 refuses with leftover 20-piece/5-space text");
        heard="";var shortStack=WelcomePlayer(town,1,0,15);var shortSrc=Pack(shortStack,3004246,19);OpenHolyStone(shortStack,shortSrc);
        Check(!Fenced(shortStack) && HolyStoneUses(shortStack)==0 && shortStack.Inventory.ContainsUID(shortSrc.UID)
            && heard.IndexOf("You do not have 20 pieces, or you lack 5 inventory spaces.")>=0,"19 units refuse with leftover 20-piece text");
        foreach(string fault in new[]{"source","output","receipt"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,3004246,20);
            string table=fault=="receipt"?"revora_ancient_stone_combines":"items";
            string verb=fault=="source"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_ancient_stone() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_ancient_stone BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_ancient_stone()");OpenHolyStone(p,item);
            Check(Fenced(p) && HolyStoneUses(p)==0 && p.Inventory.ContainsUID(item.UID) && CountId(p,720121)==0,"suppressed "+fault+" preserves 3004246");
            Query("DROP TRIGGER suppress_ancient_stone ON "+table+";DROP FUNCTION suppress_ancient_stone()");p=ReloadBall(p);OpenHolyStone(p,p.Inventory.Objects.Single(i=>i.ID==3004246));
            Check(!Fenced(p) && HolyStoneUses(p)==1 && CountId(p,720121)==1,"production reload retries 3004246 after "+fault+" rollback");
        }
        var replaced=WelcomePlayer(town,1,0,15);var replacedPack=Pack(replaced,3004246,20);
        Query("UPDATE items SET id=1000000 WHERE uid=@uid","uid",(long)replacedPack.UID);OpenHolyStone(replaced,replacedPack);
        Check(Fenced(replaced) && HolyStoneUses(replaced)==0 && CountId(replaced,720121)==0,"same-UID durable source replacement cannot combine Ancient Stones");
        Query("UPDATE items SET id=3004246 WHERE uid=@uid","uid",(long)replacedPack.UID);replaced=ReloadBall(replaced);
        OpenHolyStone(replaced,replaced.Inventory.Objects.Single(i=>i.ID==3004246));
        Check(!Fenced(replaced) && HolyStoneUses(replaced)==1 && CountId(replaced,720121)==1,"restored source replacement retries once after production reload");
        var extraBag=WelcomePlayer(town,1,0,15);var extraSrc=Pack(extraBag,3004246,20);uint extraUid=ConquerItem.ItemUID.Next;
        Query("INSERT INTO items(uid,id,entityid,stacksize) VALUES(@uid,@id,@owner,1)","uid",(long)extraUid,"id",1000000,"owner",(long)extraBag.Entity.UID);OpenHolyStone(extraBag,extraSrc);
        Check(Fenced(extraBag) && HolyStoneUses(extraBag)==0 && extraBag.Inventory.ContainsUID(extraSrc.UID),"an extra durable bag UID cannot combine Ancient Stones");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)extraUid);extraBag=ReloadBall(extraBag);OpenHolyStone(extraBag,extraBag.Inventory.Objects.Single(i=>i.ID==3004246));
        Check(!Fenced(extraBag) && HolyStoneUses(extraBag)==1,"production reload retries after the extra durable bag UID is removed");
        var missingBag=WelcomePlayer(town,1,0,15);var missingSrc=Pack(missingBag,3004246,20);var cacheOnly=Pack(missingBag,1000000,1);
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)cacheOnly.UID);OpenHolyStone(missingBag,missingSrc);
        Check(Fenced(missingBag) && HolyStoneUses(missingBag)==0 && missingBag.Inventory.ContainsUID(missingSrc.UID),"a cache-only bag UID cannot combine Ancient Stones");
        missingBag=ReloadBall(missingBag);OpenHolyStone(missingBag,missingBag.Inventory.Objects.Single(i=>i.ID==3004246));
        Check(!Fenced(missingBag) && HolyStoneUses(missingBag)==1,"production reload retries after the cache-only bag UID is dropped");
        var frames=WelcomePlayer(town,1,0,15);var wire=Pack(frames,3004246,20);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && HolyStoneUses(frames)==0 && frames.Inventory.ContainsUID(wire.UID),"bounded Ancient Stone combine rejects "+bad+" frame");
        }OpenHolyStone(frames,wire);Check(!Fenced(frames) && HolyStoneUses(frames)==1,"ordinary Ancient Stone combine works after malformed frames");
        var replay=WelcomePlayer(town,1,0,15);var once=Pack(replay,3004246,20);uint uid=once.UID;OpenHolyStone(replay,once);
        ItemHandler.UseItem(once,replay);Check(HolyStoneUses(replay)==1 && !replay.Inventory.ContainsUID(uid),"consumed Ancient Stone UID cannot replay");
        var direct=WelcomePlayer(town,1,0,15);var directPack=Pack(direct,3004246,20);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && HolyStoneUses(direct)==1 && CountId(direct,720121)==1,"direct UseItem combines 3004246");
        var race=WelcomePlayer(town,1,0,15);var racePack=Pack(race,3004246,20);
        Parallel.For(0,16,n=>OpenHolyStone(race,racePack));
        Check(!Fenced(race) && HolyStoneUses(race)==1 && CountId(race,720121)==1 && StoneUnits(race)==0,"parallel packets cannot duplicate Ancient Stone output");
        var chain=WelcomePlayer(town,1,0,15);var chainSrc=Pack(chain,3004246,20);OpenHolyStone(chain,chainSrc);
        var holy=chain.Inventory.Objects.Single(i=>i.ID==720121);ItemUsage.Process(PrayerPacket(holy.UID),chain);
        Check(!Fenced(chain) && HolyStoneUses(chain)==1 && Scalar("SELECT count(*) FROM revora_easter_gold_pack_uses WHERE uid=@uid",chain.Entity.UID)==1
            && CountId(chain,193555)==1 && CountId(chain,720121)==0,"combined 720121 pays exactly once through EasterGoldPacks");
        var hunter=WelcomePlayer(town,1,0,15);hunter.Entity.VIPLevel=7;
        var info=MonsterInformation.MonsterInformations.Values.First(m=>m.Name=="FuriousBear").Copy();
        var mob=new Entity(EntityFlag.Monster,false){UID=town.EntityUIDCounter.Next,MapID=town.ID,X=hunter.Entity.X,Y=hunter.Entity.Y,Name="FuriousBear",MonsterInfo=info};
        info.Owner=mob;FloorItem drop=null;
        for(int n=0;n<40 && drop==null;n++){
            info.Drop(hunter.Entity);
            drop=town.FloorItems.Values.FirstOrDefault(i=>i.Item!=null && i.Item.ID==3004246u && !i.PickedUpAlready);
        }
        Check(drop!=null,"FuriousBear VIP7 SuperBoss Drop can place 3004246 on the floor");
        hunter.Entity.X=drop.X;hunter.Entity.Y=drop.Y;hunter.Screen.Add(drop);
        drop.OnFloor=Time32.Now.AddSeconds(-Constants.FloorItemAvailableAfter);
        Loot(hunter,drop);
        Check(CountId(hunter,3004246)>=1,"ordinary floor pickup collects SuperBoss 3004246");
        while(StoneUnits(hunter)<20)Pack(hunter,3004246,1);
        OpenHolyStone(hunter,hunter.Inventory.Objects.First(i=>i.ID==3004246));
        Check(!Fenced(hunter) && HolyStoneUses(hunter)==1 && CountId(hunter,720121)==1,"picked-up SuperBoss stones combine through the owned service");
        var paid=hunter.Inventory.Objects.Single(i=>i.ID==720121);ItemUsage.Process(PrayerPacket(paid.UID),hunter);
        Check(!Fenced(hunter) && Scalar("SELECT count(*) FROM revora_easter_gold_pack_uses WHERE uid=@uid",hunter.Entity.UID)==1 && CountId(hunter,193555)==1,
            "producer pickup-combine-use chain pays one EasterGoldPack reward");
        var skipHoly=WelcomePlayer(town,1,0,15);var holyPack=Pack(skipHoly,720121,1);
        Check(!(bool)Call(AncientStone,null,"TryUse",holyPack,skipHoly) && skipHoly.Inventory.ContainsUID(holyPack.UID),"EasterGoldPack 720121 stays outside AncientStoneCombine");
        AncientStone.GetField("Spoken",Hidden).SetValue(null,null);
        EasterGoldService.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>Revora.Kernel.Random.Next(min,max)));
    }
}
