using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type PowerArenaPacks=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerArenaPacks");
    static DateTime powerPackNow=new DateTime(2026,9,14,20,0,0,DateTimeKind.Utc);
    static long PowerPackResults(GameState p){return Scalar("SELECT count(*) FROM revora_power_pack_results WHERE uid=@uid",p.Entity.UID);}
    static long PowerExchanges(GameState p){return Scalar("SELECT count(*) FROM revora_power_exchanges WHERE uid=@uid",p.Entity.UID);}
    static void OpenPowerPack(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifyPowerArenaPacks(string runtime,Map town){
        Call(Power,null,"Initialize");Call(PowerArenaPacks,null,"Initialize");
        Power.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>powerPackNow));
        PowerArenaPacks.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>powerPackNow));
        PowerArenaPacks.GetField("Extra",Hidden).SetValue(null,(Func<byte>)(()=>(byte)0));
        Check(Scalar("SELECT count(*) FROM pg_index i JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=ANY(i.indkey) WHERE i.indrelid='revora_power_pack_results'::regclass AND i.indisprimary AND a.attname='id'",0)==1,
            "Power Arena pack receipts keep an independent primary key for stacked uses");
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/PowerArenaPacks.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        Check(handler.IndexOf("Power Arena pack must use the owned service.")>=0 && handler.IndexOf("PowerArenaPacks.TryUse")>=0
            && usage.IndexOf("PowerArenaPacks.TryHandle")>=0 && service.IndexOf("id==724449?724445:id")>=0,
            "48 Power Arena leftover packs fail closed and keep the 724445 exception");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        string block=handler.Substring(switchStart,switchEnd-switchStart);
        var caseHits=Regex.Matches(block,@"(?m)^ {16}case (\d+):");
        Check(caseHits.Count==518,"UseItem switch still has 518 numeric top-level case IDs");
        var bodies=new System.Collections.Generic.List<string>();var ids=new System.Collections.Generic.List<uint>();
        for(int i=0;i<caseHits.Count;i++){
            ids.Add(uint.Parse(caseHits[i].Groups[1].Value));
            int a=caseHits[i].Index+caseHits[i].Length;
            int b=i+1<caseHits.Count?caseHits[i+1].Index:block.Length;
            bodies.Add(block.Substring(a,b-a));
        }
        var derived=new System.Collections.Generic.Dictionary<uint,string>();var leftover=new System.Collections.Generic.Dictionary<uint,string>();
        for(int i=0;i<ids.Count;i++){
            string kind=CaseKind(bodies[i]);string disp=LeftoverDisp(bodies[i]);
            string stripped=Regex.Replace(StripCase(bodies[i]),@"\s+"," ").Trim();
            if(kind=="other" && stripped.IndexOf("break")<0 && stripped.IndexOf("return")<0 && stripped.IndexOf("throw")<0 && stripped.IndexOf("(")<0){
                for(int j=i+1;j<ids.Count;j++){
                    string nextKind=CaseKind(bodies[j]);string nextDisp=LeftoverDisp(bodies[j]);
                    if(nextKind!="other" || bodies[j].IndexOf("(")>=0){kind=nextKind;disp=nextDisp;break;}
                }
            }
            derived[ids[i]]=kind;leftover[ids[i]]=disp;
        }
        var auditRows=Regex.Matches(audit,@"(?m)^\| (\d+) \| ([a-z-]+) \| ([^|]*)\|");
        Check(auditRows.Count==518,"compound audit enumerates the exact 518 case IDs");
        int live=0,owned=0,single=0,other=0;
        for(int i=0;i<auditRows.Count;i++){
            uint id=uint.Parse(auditRows[i].Groups[1].Value);
            string kind=auditRows[i].Groups[2].Value;
            if(kind=="live-split-write")live++;else if(kind=="owned-fail-closed")owned++;else if(kind=="single-consume")single++;else other++;
            Check(derived[id]==kind,"derived kind matches audit for "+id);
        }
        Check(live==145 && owned==275 && single==55 && other==43,"census is 145 live-split-write / 275 owned-fail-closed / 55 single-consume / 43 other");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0
            && audit.IndexOf("| single-consume | 55 |")>=0 && audit.IndexOf("| other | 43 |")>=0,
            "audit table totals parse all four census categories");
        var interceptNames=new System.Collections.Generic.List<string>();
        foreach(Match im in Regex.Matches(handler.Substring(0,switchStart),@"if \(Revora\.Game\.Features\.(\w+)\.TryUse\(item, client\)\) return;"))
            interceptNames.Add(im.Groups[1].Value);
        Check(interceptNames.Count==67,"UseItem still has 67 pre-switch TryUse intercepts");
        string featDir=Path.GetFullPath(Path.Combine(runtime,"../../Game/Features"));
        string configText=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../System/Config.cs")));
        int catAt=audit.IndexOf("## TryUse handled IDs"),dispAt=audit.IndexOf("## Leftover TryUse disposition");
        var catalogRows=Regex.Matches(audit.Substring(catAt,dispAt-catAt),@"(?m)^\| ([A-Za-z][A-Za-z0-9]+) \| ([0-9][0-9,/\-]*) \|");
        Check(catalogRows.Count==67,"handled-ID catalog lists every intercept family");
        var handledCatalog=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.HashSet<uint>>();
        for(int i=0;i<catalogRows.Count;i++){
            string fam=catalogRows[i].Groups[1].Value;
            Check(!handledCatalog.ContainsKey(fam),"handled-ID catalog has one row per family "+fam);
            handledCatalog[fam]=ExpandNotes(catalogRows[i].Groups[2].Value);
        }
        var switchSet=new System.Collections.Generic.HashSet<uint>(ids);
        var matrix=Regex.Matches(audit.Substring(dispAt),@"(?m)^\| ([A-Za-z][A-Za-z0-9]+) \| (\d+|-) \| ([a-z-]+) \|");
        var matrixFams=new System.Collections.Generic.HashSet<string>();
        var matrixPairs=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.HashSet<uint>>();
        var seenPair=new System.Collections.Generic.HashSet<string>();
        int mxFail=0,mxInd=0,mxInit=0,mxNone=0,mxInert=0;
        for(int i=0;i<matrix.Count;i++){
            string fam=matrix[i].Groups[1].Value;string idTok=matrix[i].Groups[2].Value;string disp=matrix[i].Groups[3].Value;
            matrixFams.Add(fam);
            if(!matrixPairs.ContainsKey(fam))matrixPairs[fam]=new System.Collections.Generic.HashSet<uint>();
            if(idTok=="-"){Check(disp=="no-legacy-case",fam+" dash rows are no-legacy-case");mxNone++;continue;}
            uint nid=uint.Parse(idTok);
            string pair=fam+"/"+nid;
            Check(!seenPair.Contains(pair),"no duplicate leftover pair "+pair);
            seenPair.Add(pair);matrixPairs[fam].Add(nid);
            if(disp=="fail-closed"){mxFail++;Check(leftover[nid]=="fail-closed" && derived[nid]=="owned-fail-closed",fam+" "+nid+" leftover is fail-closed");}
            else if(disp=="independently-live"){mxInd++;Check(leftover[nid].StartsWith("independently-live:"+fam),fam+" "+nid+" leftover still calls "+fam+".TryUse");}
            else if(disp=="init-absent"){mxInit++;Check(leftover[nid]=="leftover-live" && derived[nid]=="live-split-write",fam+" "+nid+" leftover stays live if Initialize never ran");}
            else if(disp=="leftover-live"){mxInert++;Check(leftover[nid]=="leftover-live" && derived[nid]=="other",fam+" "+nid+" leftover is inert");}
            else Check(false,"unknown leftover disposition "+disp+" for "+fam);
        }
        foreach(string fam in interceptNames){
            Check(handledCatalog.ContainsKey(fam) && matrixFams.Contains(fam),"catalog and leftover matrix include intercept "+fam);
            var src=FeatureHandled(fam,featDir,configText);
            Check(src.Count>0 && src.SetEquals(handledCatalog[fam]),fam+" source-derived handled IDs match the catalog exactly");
            var inter=new System.Collections.Generic.HashSet<uint>(src);inter.IntersectWith(switchSet);
            System.Collections.Generic.HashSet<uint> listed=matrixPairs.ContainsKey(fam)?matrixPairs[fam]:new System.Collections.Generic.HashSet<uint>();
            Check(inter.SetEquals(listed),fam+" switch intersection equals leftover numeric rows with no omitted or extra IDs");
            if(inter.Count==0)Check(listed.Count==0,fam+" no-legacy-case has zero switch IDs");
        }
        Check(matrixFams.SetEquals(interceptNames) && matrixFams.SetEquals(handledCatalog.Keys)
            && matrixFams.Count==interceptNames.Count,"every pre-switch TryUse family is catalogued exactly once");
        Check(mxFail==275 && mxInd==6 && mxInit==0 && mxInert==2 && mxNone==33,"leftover matrix is 275 fail-closed / 6 independently-live / 0 init-absent / 2 leftover-live / 33 no-legacy-case");
        Check(derived[723678u]=="live-split-write" && derived[724148u]=="live-split-write" && derived[723679u]=="owned-fail-closed",
            "unstocked primary leftover refinery IDs stay live-split-write; HonorShop 723679 is owned");
        var families=(uint[])PowerArenaPacks.GetField("Bases",Hidden).GetValue(null);
        Check(families.Length==16 && (uint)Call(PowerArenaPacks,null,"Result",6,0,(byte)0)==724445u,"installed 724449 one-hand Breakthrough identity remains 724445");
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=BallPlayer(town);var item=NestedPack(p,723654,stack,bound);
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else OpenPowerPack(p,item);});
            uint expect=(uint)Call(PowerArenaPacks,null,"Result",0,0,(byte)0);
            int uses=stack>1?2:1;
            Check(!Fenced(p) && PowerPackResults(p)==uses && CountId(p,expect)==uses && (stack>1?!p.Inventory.ContainsUID(item.UID):!p.Inventory.ContainsUID(item.UID))
                && p.Inventory.Objects.Where(i=>i.ID==expect).All(i=>i.Bound==bound),
                "ordinary Power Arena pack packet/autosave pays leftover extra-0 material: "+bound+"/"+stack);
            p=ReloadBall(p);Check(CountId(p,expect)==uses && PowerPackResults(p)==uses,"Power Arena pack material survives production reload: "+bound+"/"+stack);
        }
        var remain=BallPlayer(town);var remainItem=NestedPack(remain,723654,2,true);OpenPowerPack(remain,remainItem);
        Check(!Fenced(remain) && remainItem.StackSize==1 && PowerPackResults(remain)==1 && CountId(remain,724405)==1,"stacked Power Arena pack keeps leftover remainder");
        foreach(string fault in new[]{"source","output","receipt"}){
            var p=BallPlayer(town);var item=Pack(p,723654,1);
            string table=fault=="receipt"?"revora_power_pack_results":"items";
            string verb=fault=="source"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_power_pack() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_power_pack BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_power_pack()");OpenPowerPack(p,item);
            Check(Fenced(p) && PowerPackResults(p)==0 && p.Inventory.ContainsUID(item.UID) && CountId(p,724405)==0,"suppressed "+fault+" preserves Power Arena pack and material together");
            Query("DROP TRIGGER suppress_power_pack ON "+table+";DROP FUNCTION suppress_power_pack()");p=ReloadBall(p);OpenPowerPack(p,p.Inventory.Objects.Single(i=>i.ID==723654));
            Check(!Fenced(p) && PowerPackResults(p)==1 && CountId(p,724405)==1,"production reload retries the Power Arena pack after "+fault+" rollback");
        }
        var extraBag=BallPlayer(town);var extraSrc=Pack(extraBag,723654,1);uint extraUid=ConquerItem.ItemUID.Next;
        Query("INSERT INTO items(uid,id,entityid,stacksize) VALUES(@uid,@id,@owner,1)","uid",(long)extraUid,"id",1000000,"owner",(long)extraBag.Entity.UID);OpenPowerPack(extraBag,extraSrc);
        Check(Fenced(extraBag) && PowerPackResults(extraBag)==0,"an extra durable bag UID cannot open a Power Arena pack");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)extraUid);extraBag=ReloadBall(extraBag);OpenPowerPack(extraBag,extraBag.Inventory.Objects.Single(i=>i.ID==723654));
        Check(!Fenced(extraBag) && PowerPackResults(extraBag)==1,"production reload retries after the extra durable bag UID is removed");
        var missingBag=BallPlayer(town);var missingSrc=Pack(missingBag,723654,1);var cacheOnly=Pack(missingBag,1000000,1);
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)cacheOnly.UID);OpenPowerPack(missingBag,missingSrc);
        Check(Fenced(missingBag) && PowerPackResults(missingBag)==0,"a cache-only bag UID cannot open a Power Arena pack");
        missingBag=ReloadBall(missingBag);OpenPowerPack(missingBag,missingBag.Inventory.Objects.Single(i=>i.ID==723654));
        Check(!Fenced(missingBag) && PowerPackResults(missingBag)==1,"production reload retries after the cache-only bag UID is dropped");
        var frames=BallPlayer(town);var wire=Pack(frames,723654,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && PowerPackResults(frames)==0,"bounded Power Arena pack rejects "+bad+" frame");
        }OpenPowerPack(frames,wire);Check(!Fenced(frames) && PowerPackResults(frames)==1,"ordinary Power Arena pack works after malformed frames");
        var replay=BallPlayer(town);var once=Pack(replay,723654,1);uint uid=once.UID;OpenPowerPack(replay,once);
        ItemHandler.UseItem(once,replay);Check(PowerPackResults(replay)==1 && !replay.Inventory.ContainsUID(uid),"consumed Power Arena pack UID cannot replay");
        var race=BallPlayer(town);var racePack=Pack(race,723654,1);
        Parallel.For(0,16,n=>OpenPowerPack(race,racePack));
        Check(!Fenced(race) && PowerPackResults(race)==1 && CountId(race,724405)==1,"parallel packets cannot duplicate Power Arena pack output");
        var breakthrough=BallPlayer(town);var bt=Pack(breakthrough,723660,1);OpenPowerPack(breakthrough,bt);
        Check(!Fenced(breakthrough) && CountId(breakthrough,724445)==1 && CountId(breakthrough,724449)==0,"primary 1-hand Breakthrough extra-0 uses installed 724445");
        Query("CREATE TABLE IF NOT EXISTS npcs (LIKE public.npcs INCLUDING ALL)");
        Query("INSERT INTO npcs SELECT * FROM public.npcs p WHERE (p.id=500010 OR p.id BETWEEN 3956 AND 3962) AND NOT EXISTS(SELECT 1 FROM npcs n WHERE n.id=p.id)");
        for(ushort map=1730;map<=1736;map++)World(runtime,map);
        Call(Power,null,"PlaceNpcs");
        var hunter=MoonPlayer(town);MoonNear(hunter,500010);PowerNpc(hunter,1);
        Check(hunter.Entity.MapID==1730,"ordinary Wang entry reaches floor one during the Pacific window");
        PowerHunt(hunter,30);MoonNear(hunter,3956);PowerNpc(hunter,1);PowerNpc(hunter,3);
        PowerHunt(hunter,45);MoonNear(hunter,3957);PowerNpc(hunter,1);PowerNpc(hunter,5);
        Check(CountId(hunter,720148)==2 && hunter.Inventory.Objects.Where(i=>i.ID==720148).All(i=>i.Bound),"ordinary two-floor climb earns two bound Refinery Badges");
        MoonNear(hunter,500010);PowerPreview(hunter,0,0);PowerNpc(hunter,230);
        var earned=hunter.Inventory.Objects.Single(i=>i.ID==723654);
        Check(!Fenced(hunter) && PowerExchanges(hunter)==1 && earned.Bound && CountId(hunter,720148)==0,"ordinary earned badges exchange into a bound Primary 1-hand Critical pack");
        OpenPowerPack(hunter,earned);hunter=ReloadBall(hunter);
        Check(!Fenced(hunter) && PowerPackResults(hunter)==1 && CountId(hunter,724405)==1 && hunter.Inventory.Objects.Single(i=>i.ID==724405).Bound,
            "ordinary earned pack use pays leftover extra-0 material and survives production reload");
        PowerArenaPacks.GetField("Extra",Hidden).SetValue(null,(Func<byte>)(()=>ItemHandler.GetNextRefineryItem()));
    }
}
