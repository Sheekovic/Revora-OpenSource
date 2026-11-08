using System;
using System.Collections.Generic;
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
    static readonly Type StarStone=typeof(Entity).Assembly.GetType("Revora.Game.Features.StarStoneCombine");
    static DateTime starStoneNow=DateTime.UtcNow;
    static long StarStoneUses(GameState p){return Scalar("SELECT count(*) FROM revora_star_stone_combines WHERE uid=@uid",p.Entity.UID);}
    static void OpenStarStone(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static int BrightUnits(GameState p){return p.Inventory.Objects.Where(i=>i.ID==3009001u).Sum(i=>i.StackSize<=1?1:i.StackSize);}
    static string StripCase(string body){
        string t=Regex.Replace(body,@"//.*","");
        t=Regex.Replace(t,@"#region.*","");
        t=Regex.Replace(t,@"#endregion","");
        return t;
    }
    static string CaseEffects(string body){
        string t=StripCase(body);
        var labels=new List<string>();
        if(t.IndexOf("Inventory.Remove")>=0)labels.Add("source");
        if(t.IndexOf("Inventory.Add")>=0 || t.IndexOf("AddBoundItem")>=0 || t.IndexOf("AddRune")>=0 || t.IndexOf("AddTime1")>=0)labels.Add("items");
        if(t.IndexOf("AdjustMoney")>=0)labels.Add("silver");
        if(t.IndexOf("ConquerPoints +=")>=0 || t.IndexOf("AddConquerPoints(")>=0)labels.Add("cp");
        if(t.IndexOf("BoundCps +=")>=0)labels.Add("boundcps");
        if(t.IndexOf("RevoraPoint +=")>=0 || t.IndexOf("AddRevoraPoints(")>=0)labels.Add("rvp");
        if(t.IndexOf("AdjustChiPoints")>=0)labels.Add("chi");
        if(t.IndexOf("StudyPoints +=")>=0)labels.Add("study");
        bool timer=Regex.IsMatch(t,@"client\.Entity\.\w+(Time|Stamp|Days)\s*\+?=") || t.IndexOf("AddFlag(")>=0;
        if(timer)labels.Add("timer");
        return string.Join("+",labels.ToArray());
    }
    static string CaseKind(string body){
        string t=StripCase(body);
        if(t.IndexOf("must use the owned service")>=0)return "owned-fail-closed";
        string e=CaseEffects(body);
        if(e.IndexOf('+')>=0 || (e.Length>0 && e!="source"))return "live-split-write";
        if(e=="source")return "single-consume";
        return "other";
    }
    static string LeftoverDisp(string body){
        string t=StripCase(body);
        if(t.IndexOf("must use the owned service")>=0)return "fail-closed";
        var m=Regex.Match(t,@"Features\.(\w+)\.TryUse\(");
        if(m.Success)return "independently-live:"+m.Groups[1].Value;
        return "leftover-live";
    }
    static string ExtractMethod(string text,string name){
        var m=Regex.Match(text,@"static (?:bool|Rule) "+name+@"\([^)]*\)\s*\{");
        if(!m.Success)return "";
        int i=m.Index+m.Length,depth=1;
        while(i<text.Length && depth>0){
            if(text[i]=='{')depth++;else if(text[i]=='}')depth--;
            i++;
        }
        return text.Substring(m.Index+m.Length,i-1-(m.Index+m.Length));
    }
    static Dictionary<string,uint> FeatureConsts(string text){
        var d=new Dictionary<string,uint>();
        foreach(Match block in Regex.Matches(text,@"const uint ([^;]+);"))
            foreach(Match n in Regex.Matches(block.Groups[1].Value,@"(\w+)\s*=\s*(\d+)"))
                d[n.Groups[1].Value]=uint.Parse(n.Groups[2].Value);
        return d;
    }
    static string FeatureRelated(string fam,string featDir){
        var files=new List<string>();
        files.Add(Path.Combine(featDir,fam+".cs"));
        string stem=fam;
        foreach(string suf in new[]{"Quest","Encounter","Packs","Combine","Items","Bags","Pouches","Books","Cash","Amulets","Conversions","Rewards","Hourglasses","Consumables","Potions","Protection","Crafting"})
            if(stem.EndsWith(suf)){stem=stem.Substring(0,stem.Length-suf.Length);break;}
        foreach(string extra in new[]{stem+"Rules.cs",stem+"Admission.cs",stem+"Seeds.cs",stem+"Store.cs"}){
            string p=Path.Combine(featDir,extra);
            if(File.Exists(p) && !files.Contains(p))files.Add(p);
        }
        var sb=new System.Text.StringBuilder();
        foreach(string p in files)if(File.Exists(p))sb.AppendLine(File.ReadAllText(p));
        return sb.ToString();
    }
    static void AddSpan(HashSet<uint> ids,uint a,uint b,uint step){
        if(a>b){uint t=a;a=b;b=t;}
        for(uint i=a;i<=b;i+=step){ids.Add(i);if(i>uint.MaxValue-step)break;}
    }
    static uint? ConstOrNumber(string tok,Dictionary<string,uint> consts){
        tok=tok.Trim();
        uint n;if(uint.TryParse(tok,out n))return n;
        int dot=tok.LastIndexOf('.');if(dot>=0)tok=tok.Substring(dot+1);
        if(consts.ContainsKey(tok))return consts[tok];
        return null;
    }
    static void ParsePredicate(string blob,Dictionary<string,uint> consts,HashSet<uint> ids){
        if(string.IsNullOrEmpty(blob))return;
        foreach(Match eqn in Regex.Matches(blob,@"(?:id|item\.ID|list\.ID|seed\.ID)\s*==\s*(\d+)"))ids.Add(uint.Parse(eqn.Groups[1].Value));
        foreach(Match eqi in Regex.Matches(blob,@"(?:id|item\.ID|list\.ID|seed\.ID)\s*==\s*(?:[A-Za-z]+\.)?(\w+)"))
            if(consts.ContainsKey(eqi.Groups[1].Value))ids.Add(consts[eqi.Groups[1].Value]);
        foreach(Match nen in Regex.Matches(blob,@"(?:id|item\.ID|list\.ID|seed\.ID)\s*!=\s*(\d+)"))ids.Add(uint.Parse(nen.Groups[1].Value));
        foreach(Match nei in Regex.Matches(blob,@"(?:id|item\.ID|list\.ID|seed\.ID)\s*!=\s*(?:[A-Za-z]+\.)?(\w+)"))
            if(consts.ContainsKey(nei.Groups[1].Value))ids.Add(consts[nei.Groups[1].Value]);
        string packed=Regex.Replace(blob,@"\s+","");
        foreach(Match spanN in Regex.Matches(packed,@"(?:id|item\.ID)>=(\d+)&&(?:id|item\.ID)<=(\d+)"))
            AddSpan(ids,uint.Parse(spanN.Groups[1].Value),uint.Parse(spanN.Groups[2].Value),1);
        foreach(Match spanI in Regex.Matches(packed,@"(?:id|item\.ID)>=([A-Za-z]\w*)&&(?:id|item\.ID)<=([A-Za-z]\w*)")){
            var spanA=ConstOrNumber(spanI.Groups[1].Value,consts);var spanB=ConstOrNumber(spanI.Groups[2].Value,consts);
            if(spanA.HasValue && spanB.HasValue)AddSpan(ids,spanA.Value,spanB.Value,1);
        }
        var inv=Regex.Match(blob,@"item\.ID\s*<\s*(?:[A-Za-z]+\.)?(\w+)\s*\|\|\s*item\.ID\s*>\s*(?:[A-Za-z]+\.)?(\w+)");
        if(inv.Success){
            var invA=ConstOrNumber(inv.Groups[1].Value,consts);var invB=ConstOrNumber(inv.Groups[2].Value,consts);
            if(invA.HasValue && invB.HasValue)AddSpan(ids,invA.Value,invB.Value,1);
        }
        var inv2=Regex.Match(packed,@"item\.ID<(\d+)\|\|item\.ID>([A-Za-z]\w*)");
        if(inv2.Success){
            var inv2B=ConstOrNumber(inv2.Groups[2].Value,consts);
            if(inv2B.HasValue)AddSpan(ids,uint.Parse(inv2.Groups[1].Value),inv2B.Value,1);
        }
    }
    static HashSet<uint> FeatureHandled(string fam,string featDir,string configText){
        string main=File.ReadAllText(Path.Combine(featDir,fam+".cs"));
        string related=FeatureRelated(fam,featDir);
        var consts=FeatureConsts(related);
        string tryuse=ExtractMethod(main,"TryUse");
        string use=ExtractMethod(main,"Use");
        var ids=new HashSet<uint>();
        ParsePredicate(tryuse,consts,ids);ParsePredicate(use,consts,ids);
        string gate=tryuse+"\n"+use;
        foreach(string helper in new[]{"Handles","Matches","Material","Identity","Book","SeedType"}){
            if(gate.IndexOf(helper+"(")<0)continue;
            string blob=ExtractMethod(related,helper);
            ParsePredicate(blob,consts,ids);
            if(helper=="Book")foreach(Match bookCase in Regex.Matches(blob,@"case (\d+):"))ids.Add(uint.Parse(bookCase.Groups[1].Value));
            if(helper=="Matches"){
                var dye=Regex.Match(blob,@"id\s*>=\s*(\d+)\s*&&\s*id\s*<=\s*(\d+)");
                if(dye.Success){
                    uint dyeA=uint.Parse(dye.Groups[1].Value),dyeB=uint.Parse(dye.Groups[2].Value);
                    uint dyeStep=(uint)(Regex.IsMatch(blob,@"%\s*10\s*==\s*0")?10:1);
                    for(uint dyeI=dyeA;dyeI<=dyeB;dyeI++)if((dyeI-dyeA)%dyeStep!=0)ids.Remove(dyeI);
                    AddSpan(ids,dyeA,dyeB,dyeStep);
                }
            }
        }
        if(gate.IndexOf("Result(item.ID)")>=0){
            var rm=Regex.Match(related,@"Result\(uint \w+\)\s*\{switch\(\w+\)\{([^}]+)\}");
            if(rm.Success)foreach(Match resultCase in Regex.Matches(rm.Groups[1].Value,@"case (\d+):"))ids.Add(uint.Parse(resultCase.Groups[1].Value));
        }
        if(gate.IndexOf("Types.Contains")>=0){
            var getter=Regex.Match(related,@"uint\[\] Types[^\{]*\{ get \{ return new\[\]\{([^}]+)\}");
            if(getter.Success){
                foreach(Match cfgName in Regex.Matches(getter.Groups[1].Value,@"Config\.(\w+)")){
                    var cm=Regex.Match(configText,@"public static uint "+cfgName.Groups[1].Value+@"\s*=\s*(\d+)");
                    if(cm.Success)ids.Add(uint.Parse(cm.Groups[1].Value));
                }
                foreach(Match cfgNum in Regex.Matches(getter.Groups[1].Value,@"(\d+)"))ids.Add(uint.Parse(cfgNum.Groups[1].Value));
            }
        }
        string handles=ExtractMethod(related,"Handles");
        if(handles.IndexOf("ContainsKey")>=0 || tryuse.IndexOf("ContainsKey")>=0){
            foreach(string dictname in new[]{"Catalog","Amounts"}){
                var dm=Regex.Match(related,dictname+@"\s*=\s*new Dictionary[^\{]*\{(.*?)\}\s*;",RegexOptions.Singleline);
                if(dm.Success)foreach(Match dictId in Regex.Matches(dm.Groups[1].Value,@"\{\s*(\d+)"))ids.Add(uint.Parse(dictId.Groups[1].Value));
            }
        }
        if(tryuse.IndexOf("Bases")>=0){
            var bm=Regex.Match(related,@"uint\[\] Bases\s*=\s*\{([^}]+)\}");
            if(bm.Success)foreach(string raw in bm.Groups[1].Value.Split(',')){
                uint baseId;if(uint.TryParse(raw.Trim(),out baseId)){ids.Add(baseId);ids.Add(baseId+1);ids.Add(baseId+2);}
            }
        }
        return ids;
    }
    static HashSet<uint> ExpandNotes(string notes){
        var ids=new HashSet<uint>();
        if(string.IsNullOrEmpty(notes))return ids;
        foreach(string part in notes.Split(',')){
            string p=part.Trim();if(p.Length==0)continue;
            int slash=p.IndexOf('/');uint step=1;
            if(slash>0){step=uint.Parse(p.Substring(slash+1));p=p.Substring(0,slash);}
            int dash=p.IndexOf('-');
            if(dash>0)AddSpan(ids,uint.Parse(p.Substring(0,dash)),uint.Parse(p.Substring(dash+1)),step);
            else ids.Add(uint.Parse(p));
        }
        return ids;
    }
    static void VerifyStarStoneCombine(string runtime,Map town){
        Call(StarStone,null,"Initialize");Call(StarStone,null,"Initialize");
        StarStone.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>starStoneNow));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/StarStoneCombine.cs")));
        string shop=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/RevoraPointShopSystem.cs")));
        string monsters=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/MonsterTable.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        string block=handler.Substring(switchStart,switchEnd-switchStart);
        var caseHits=Regex.Matches(block,@"(?m)^ {16}case (\d+):");
        Check(caseHits.Count==518,"UseItem switch still has 518 numeric top-level case IDs");
        var bodies=new List<string>();var ids=new List<uint>();
        for(int i=0;i<caseHits.Count;i++){
            ids.Add(uint.Parse(caseHits[i].Groups[1].Value));
            int a=caseHits[i].Index+caseHits[i].Length;
            int b=i+1<caseHits.Count?caseHits[i+1].Index:block.Length;
            bodies.Add(block.Substring(a,b-a));
        }
        var derived=new Dictionary<uint,string>();var effects=new Dictionary<uint,string>();var leftover=new Dictionary<uint,string>();
        for(int i=0;i<ids.Count;i++){
            string kind=CaseKind(bodies[i]);string eff=CaseEffects(bodies[i]);string disp=LeftoverDisp(bodies[i]);
            string stripped=Regex.Replace(StripCase(bodies[i]),@"\s+"," ").Trim();
            if(kind=="other" && stripped.IndexOf("break")<0 && stripped.IndexOf("return")<0 && stripped.IndexOf("throw")<0 && stripped.IndexOf("(")<0){
                for(int j=i+1;j<ids.Count;j++){
                    string nextKind=CaseKind(bodies[j]);string nextEff=CaseEffects(bodies[j]);string nextDisp=LeftoverDisp(bodies[j]);
                    if(nextKind!="other" || bodies[j].IndexOf("(")>=0){kind=nextKind;eff=nextEff;disp=nextDisp;break;}
                }
            }
            derived[ids[i]]=kind;effects[ids[i]]=eff;leftover[ids[i]]=disp;
        }
        var auditRows=Regex.Matches(audit,@"(?m)^\| (\d+) \| ([a-z-]+) \| ([^|]*)\|");
        Check(auditRows.Count==518,"compound audit enumerates the exact 518 case IDs");
        int live=0,owned=0,single=0,other=0;
        for(int i=0;i<auditRows.Count;i++){
            uint id=uint.Parse(auditRows[i].Groups[1].Value);
            string kind=auditRows[i].Groups[2].Value;string aeff=auditRows[i].Groups[3].Value.Trim();
            if(kind=="live-split-write")live++;else if(kind=="owned-fail-closed")owned++;else if(kind=="single-consume")single++;else other++;
            Check(derived[id]==kind,"derived kind matches audit for "+id);
            if(kind!="owned-fail-closed")Check(effects[id]==aeff,"derived effects match audit for "+id);
        }
        Check(live==145 && owned==275 && single==55 && other==43,"census is 145 live-split-write / 275 owned-fail-closed / 55 single-consume / 43 other");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0
            && audit.IndexOf("| single-consume | 55 |")>=0 && audit.IndexOf("| other | 43 |")>=0,
            "audit table totals parse all four census categories");
        foreach(uint id in new uint[]{721623u,721624u,721627u,721628u,721672u,721673u})
            Check(derived[id]=="owned-fail-closed" && leftover[id]=="fail-closed" && audit.IndexOf("| "+id+" | owned-fail-closed | source+timer |")>=0,
                id+" is owned fail-closed source+timer");
        foreach(uint id in new uint[]{3004230u,3004182u,3004231u,3004232u,3004233u,3004234u,3004235u,3004236u,3004237u,3004238u})
            Check(derived[id]=="owned-fail-closed" && leftover[id]=="fail-closed","Bloodshed Sea "+id+" is owned fail-closed");
        foreach(uint id in new uint[]{3004121u,3004122u,3004123u,3004124u,3000794u,723724u})
            Check(derived[id]=="live-split-write" && effects[id]=="source+timer","VIP-day/disguise "+id+" is source+timer");
        foreach(uint id in new uint[]{1000000u,1000010u,1000020u,1000030u,1000040u,1002000u,1002010u,1002020u,1002050u,725065u,1001000u,1001010u,1001020u,1001030u,1001040u,1002030u,1002040u,725066u,723726u})
            Check(derived[id]=="owned-fail-closed" && leftover[id]=="fail-closed" && audit.IndexOf("| "+id+" | owned-fail-closed | source+timer |")>=0,
                id+" leftover NoDrugsTime cooldown is owned fail-closed");
        Check(derived[3000788u]=="other" && audit.IndexOf("| 3000788 | other |")>=0,"3000788 VIP8 WhiteDragon summon is other; leftover Remove is commented");
        Check(effects[3003650u]!="source+timer" && derived[3003650u]=="single-consume","JiangHu 3003650 packet-copy FreeTimeTodeyUsed is not a timer write");
        var interceptNames=new List<string>();
        foreach(Match im in Regex.Matches(handler.Substring(0,switchStart),@"if \(Revora\.Game\.Features\.(\w+)\.TryUse\(item, client\)\) return;"))
            interceptNames.Add(im.Groups[1].Value);
        Check(interceptNames.Count==67,"UseItem still has 67 pre-switch TryUse intercepts");
        string featDir=Path.GetFullPath(Path.Combine(runtime,"../../Game/Features"));
        string configText=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../System/Config.cs")));
        int catAt=audit.IndexOf("## TryUse handled IDs"),dispAt=audit.IndexOf("## Leftover TryUse disposition");
        Check(catAt>=0 && dispAt>catAt,"audit has the handled-ID catalog and leftover disposition tables");
        var catalogRows=Regex.Matches(audit.Substring(catAt,dispAt-catAt),@"(?m)^\| ([A-Za-z][A-Za-z0-9]+) \| ([0-9][0-9,/\-]*) \|");
        Check(catalogRows.Count==67,"handled-ID catalog lists every intercept family");
        var handledCatalog=new Dictionary<string,HashSet<uint>>();
        for(int i=0;i<catalogRows.Count;i++){
            string fam=catalogRows[i].Groups[1].Value;
            Check(!handledCatalog.ContainsKey(fam),"handled-ID catalog has one row per family "+fam);
            handledCatalog[fam]=ExpandNotes(catalogRows[i].Groups[2].Value);
        }
        var switchSet=new HashSet<uint>(ids);
        var matrix=Regex.Matches(audit.Substring(dispAt),@"(?m)^\| ([A-Za-z][A-Za-z0-9]+) \| (\d+|-) \| ([a-z-]+) \|");
        var matrixFams=new HashSet<string>();
        var matrixPairs=new Dictionary<string,HashSet<uint>>();
        var seenPair=new HashSet<string>();
        int mxFail=0,mxInd=0,mxInit=0,mxNone=0,mxInert=0;
        for(int i=0;i<matrix.Count;i++){
            string fam=matrix[i].Groups[1].Value;string idTok=matrix[i].Groups[2].Value;string disp=matrix[i].Groups[3].Value;
            matrixFams.Add(fam);
            if(!matrixPairs.ContainsKey(fam))matrixPairs[fam]=new HashSet<uint>();
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
            var inter=new HashSet<uint>(src);inter.IntersectWith(switchSet);
            HashSet<uint> listed=matrixPairs.ContainsKey(fam)?matrixPairs[fam]:new HashSet<uint>();
            Check(inter.SetEquals(listed),fam+" switch intersection equals leftover numeric rows with no omitted or extra IDs");
            if(inter.Count==0)Check(listed.Count==0,fam+" no-legacy-case has zero switch IDs");
        }
        Check(matrixFams.SetEquals(interceptNames) && matrixFams.SetEquals(handledCatalog.Keys)
            && matrixFams.Count==interceptNames.Count,"every pre-switch TryUse family is catalogued exactly once");
        Check(mxFail==275 && mxInd==6 && mxInit==0 && mxInert==2 && mxNone==33,"leftover matrix is 275 fail-closed / 6 independently-live / 0 init-absent / 2 leftover-live / 33 no-legacy-case");
        Check(leftover[3004247u]=="independently-live:P7SoulPacks" && leftover[3004248u]=="independently-live:P7SoulPacks"
            && leftover[3004243u]=="independently-live:ArenaPrizeConversions" && leftover[3009002u]=="independently-live:ArenaPrizeConversions",
            "P7SoulPacks and ArenaPrizeConversions leftover cases still delegate to TryUse");
        Check(handler.IndexOf("Power Arena pack must use the owned service.")>=0 && leftover[723130u]=="fail-closed" && derived[723130u]=="owned-fail-closed",
            "Power Arena leftover packs are fail-closed on the live handler");
        Check(derived[720598u]=="other" && leftover[720598u]=="leftover-live" && derived[3004259u]=="other" && leftover[3004259u]=="leftover-live",
            "Terato 720598 and PurpleBanshee 3004259 leftover cases are inert messages");
        Check(handler.IndexOf("Star stone combine must use the owned service.")>=0 && handler.IndexOf("StarStoneCombine.TryUse")>=0,
            "3009001 is fail-closed on the live handler");
        Check(handler.IndexOf("StarStoneCombine.TryUse")>handler.IndexOf("AncientStoneCombine.TryUse")
            && usage.IndexOf("StarStoneCombine.TryHandle")>usage.IndexOf("AncientStoneCombine.TryHandle")
            && thread.IndexOf("StarStoneCombine.Initialize")>thread.IndexOf("AncientStoneCombine.Initialize"),
            "StarStoneCombine intercepts sit after AncientStoneCombine");
        Check(handler.IndexOf("P7SoulPacks.TryUse")>=0 && handler.IndexOf("case 3004247:")>=0 && handler.IndexOf("P7 soul pack must use")<0,
            "3004247 leftover case delegates to P7SoulPacks.TryUse instead of throwing");
        Check(handler.IndexOf("ArenaPrizeConversions.TryUse")>=0 && handler.IndexOf("case 3009002:")>=0,
            "3009002 leftover case delegates to ArenaPrizeConversions.TryUse");
        Check(catalog.IndexOf("3009001@@BrightStarStone")>=0 && catalog.IndexOf("3009002@@RadiantStarStone")>=0
            && service.IndexOf("Cost=10")>=0 && service.IndexOf("You do not have 10 stones, or you lack 5 inventory spaces.")>=0,
            "catalog identities are Bright/Radiant Star Stone; leftover 10-unit Count<=31 contract is owned");
        Check(shop.IndexOf("NpcId = 900000014")>=0 && shop.IndexOf("3009001, 400")>=0 && thread.IndexOf("ParkNpc(900000014, 1002, 396, 340)")>=0,
            "startup Vesper 900000014 Twin City stall sells 3009001 for 400 Revora Points");
        Check(monsters.IndexOf("ItemID = 3009001")>=0 && monsters.IndexOf("Name == \"FuriousBear\"")>=0
            && PublicScalar("SELECT count(*) FROM public.monsterspawns s JOIN public.monsterinfos m ON m.id=s.npctype WHERE m.name='FuriousBear'")>=1,
            "spawned SuperBoss FuriousBear loot can floor-drop 3009001");
        string heard="";StarStone.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        ushort dur=ConquerItemInformation.BaseInformations[3009002u].Durability;
        heard="";var one=WelcomePlayer(town,1,0,15);var oneSrc=Pack(one,3009001,10);OpenStarStone(one,oneSrc);
        var gift=one.Inventory.Objects.Single(i=>i.ID==3009002);
        Check(!Fenced(one) && StarStoneUses(one)==1 && !one.Inventory.ContainsUID(oneSrc.UID) && CountId(one,3009002)==1
            && !gift.Bound && gift.Plus==0 && gift.StackSize==1 && gift.Days==0 && gift.Durability==dur && gift.MaximDurability==dur
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid AND bound=0 AND plus=0 AND stacksize=1 AND durability="+dur+" AND maximdurability="+dur,gift.UID)==1
            && heard=="","10-stack combine pays one permanent unbound plus-0 3009002 silently");
        foreach(bool boundSrc in new[]{false,true})foreach(ushort stack in new ushort[]{10,11,12}){
            var p=WelcomePlayer(town,1,0,15);var src=NestedPack(p,3009001,stack,boundSrc);
            Parallel.For(0,24,i=>{if(i%3==0)EntityTable.SaveEntity(p);else OpenStarStone(p,src);});
            Check(!Fenced(p) && StarStoneUses(p)==1 && CountId(p,3009002)==1 && (stack==10?!p.Inventory.ContainsUID(src.UID):src.StackSize==(ushort)(stack-10)),
                "3009001 pays with leftover stack remainder: "+boundSrc+"/"+stack);
        }
        var singles=WelcomePlayer(town,1,0,15);for(int n=0;n<10;n++)Pack(singles,3009001,1);OpenStarStone(singles,singles.Inventory.Objects.First(i=>i.ID==3009001));
        Check(!Fenced(singles) && StarStoneUses(singles)==1 && BrightUnits(singles)==0 && CountId(singles,3009002)==1,"10 singleton objects combine exactly 10 units");
        var mixed=WelcomePlayer(town,1,0,15);var small=Pack(mixed,3009001,6);var large=Pack(mixed,3009001,8);OpenStarStone(mixed,small);
        Check(!Fenced(mixed) && StarStoneUses(mixed)==1 && !mixed.Inventory.ContainsUID(small.UID) && large.StackSize==4 && CountId(mixed,3009002)==1,
            "6+8 stacks consume exactly 10 and keep a remainder of 4");
        var zeros=WelcomePlayer(town,1,0,15);for(int n=0;n<10;n++)NestedPack(zeros,3009001,0,false);OpenStarStone(zeros,zeros.Inventory.Objects.First(i=>i.ID==3009001));
        Check(!Fenced(zeros) && StarStoneUses(zeros)==1 && CountId(zeros,3009002)==1 && BrightUnits(zeros)==0,"10 stack-0 objects still combine as 10 leftover units");
        var skip=WelcomePlayer(town,1,0,15);var lone=Pack(skip,3009001,1);var pile=Pack(skip,3009001,12);OpenStarStone(skip,lone);
        Check(!Fenced(skip) && StarStoneUses(skip)==1 && skip.Inventory.ContainsUID(lone.UID) && pile.StackSize==2 && CountId(skip,3009002)==1,
            "a later stack of 10+ supplies the whole combine and leaves earlier small stacks");
        var boundMix=WelcomePlayer(town,1,0,15);for(int n=0;n<5;n++)NestedPack(boundMix,3009001,1,true);for(int n=0;n<5;n++)NestedPack(boundMix,3009001,1,false);
        OpenStarStone(boundMix,boundMix.Inventory.Objects.First(i=>i.ID==3009001));
        Check(!Fenced(boundMix) && StarStoneUses(boundMix)==1 && BrightUnits(boundMix)==0 && CountId(boundMix,3009002)==1
            && boundMix.Inventory.Objects.Single(i=>i.ID==3009002).Bound==false,"mixed source bindings still yield unbound 3009002");
        heard="";var fit=WelcomePlayer(town,1,0,15);var fitSrc=Pack(fit,3009001,10);while(fit.Inventory.Count<31)Pack(fit,1000000,1);OpenStarStone(fit,fitSrc);
        Check(!Fenced(fit) && StarStoneUses(fit)==1 && CountId(fit,3009002)==1,"Count 31 still accepts leftover Count<=31");
        heard="";var full=WelcomePlayer(town,1,0,15);var fullSrc=Pack(full,3009001,10);while(full.Inventory.Count<32)Pack(full,1000000,1);OpenStarStone(full,fullSrc);
        Check(!Fenced(full) && StarStoneUses(full)==0 && heard.IndexOf("You do not have 10 stones, or you lack 5 inventory spaces.")>=0,"Count 32 refuses with leftover 10-stone text");
        heard="";var shortStack=WelcomePlayer(town,1,0,15);var shortSrc=Pack(shortStack,3009001,9);OpenStarStone(shortStack,shortSrc);
        Check(!Fenced(shortStack) && StarStoneUses(shortStack)==0 && heard.IndexOf("You do not have 10 stones, or you lack 5 inventory spaces.")>=0,"9 units refuse with leftover 10-stone text");
        foreach(string fault in new[]{"source","output","receipt"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,3009001,10);
            string table=fault=="receipt"?"revora_star_stone_combines":"items";
            string verb=fault=="source"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_star_stone() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_star_stone BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_star_stone()");OpenStarStone(p,item);
            Check(Fenced(p) && StarStoneUses(p)==0 && p.Inventory.ContainsUID(item.UID) && CountId(p,3009002)==0,"suppressed "+fault+" preserves 3009001");
            Query("DROP TRIGGER suppress_star_stone ON "+table+";DROP FUNCTION suppress_star_stone()");p=ReloadBall(p);OpenStarStone(p,p.Inventory.Objects.Single(i=>i.ID==3009001));
            Check(!Fenced(p) && StarStoneUses(p)==1 && CountId(p,3009002)==1,"production reload retries 3009001 after "+fault+" rollback");
        }
        var replaced=WelcomePlayer(town,1,0,15);var replacedPack=Pack(replaced,3009001,10);
        Query("UPDATE items SET id=1000000 WHERE uid=@uid","uid",(long)replacedPack.UID);OpenStarStone(replaced,replacedPack);
        Check(Fenced(replaced) && StarStoneUses(replaced)==0,"same-UID durable source replacement cannot combine Bright Star Stones");
        Query("UPDATE items SET id=3009001 WHERE uid=@uid","uid",(long)replacedPack.UID);replaced=ReloadBall(replaced);
        OpenStarStone(replaced,replaced.Inventory.Objects.Single(i=>i.ID==3009001));
        Check(!Fenced(replaced) && StarStoneUses(replaced)==1,"restored source replacement retries once after production reload");
        var extraBag=WelcomePlayer(town,1,0,15);var extraSrc=Pack(extraBag,3009001,10);uint extraUid=ConquerItem.ItemUID.Next;
        Query("INSERT INTO items(uid,id,entityid,stacksize) VALUES(@uid,@id,@owner,1)","uid",(long)extraUid,"id",1000000,"owner",(long)extraBag.Entity.UID);OpenStarStone(extraBag,extraSrc);
        Check(Fenced(extraBag) && StarStoneUses(extraBag)==0,"an extra durable bag UID cannot combine Bright Star Stones");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)extraUid);extraBag=ReloadBall(extraBag);OpenStarStone(extraBag,extraBag.Inventory.Objects.Single(i=>i.ID==3009001));
        Check(!Fenced(extraBag) && StarStoneUses(extraBag)==1,"production reload retries after the extra durable bag UID is removed");
        var missingBag=WelcomePlayer(town,1,0,15);var missingSrc=Pack(missingBag,3009001,10);var cacheOnly=Pack(missingBag,1000000,1);
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)cacheOnly.UID);OpenStarStone(missingBag,missingSrc);
        Check(Fenced(missingBag) && StarStoneUses(missingBag)==0,"a cache-only bag UID cannot combine Bright Star Stones");
        missingBag=ReloadBall(missingBag);OpenStarStone(missingBag,missingBag.Inventory.Objects.Single(i=>i.ID==3009001));
        Check(!Fenced(missingBag) && StarStoneUses(missingBag)==1,"production reload retries after the cache-only bag UID is dropped");
        var frames=WelcomePlayer(town,1,0,15);var wire=Pack(frames,3009001,10);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && StarStoneUses(frames)==0,"bounded Star Stone combine rejects "+bad+" frame");
        }OpenStarStone(frames,wire);Check(!Fenced(frames) && StarStoneUses(frames)==1,"ordinary Star Stone combine works after malformed frames");
        var replay=WelcomePlayer(town,1,0,15);var once=Pack(replay,3009001,10);uint uid=once.UID;OpenStarStone(replay,once);
        ItemHandler.UseItem(once,replay);Check(StarStoneUses(replay)==1 && !replay.Inventory.ContainsUID(uid),"consumed Bright Star Stone UID cannot replay");
        var direct=WelcomePlayer(town,1,0,15);var directPack=Pack(direct,3009001,10);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && StarStoneUses(direct)==1 && CountId(direct,3009002)==1,"direct UseItem combines 3009001");
        var race=WelcomePlayer(town,1,0,15);var racePack=Pack(race,3009001,10);
        Parallel.For(0,16,n=>OpenStarStone(race,racePack));
        Check(!Fenced(race) && StarStoneUses(race)==1 && CountId(race,3009002)==1 && BrightUnits(race)==0,"parallel packets cannot duplicate Star Stone output");
        var buyer=WelcomePlayer(town,1,0,15);
        buyer.Entity.MapID=1002;buyer.Entity.X=396;buyer.Entity.Y=340;buyer.Entity.RevoraPoint=4000;
        if(!EntityTable.SaveEntity(buyer))throw new Exception("Vesper RVP save failed");
        buyer.ActiveNpc=900000014;
        town.AddNpc(new NpcSpawn{UID=900000014,MapID=1002,X=396,Y=340,Mesh=50110,Name="Vesper",Type=Enums.NpcType.Talker});
        Npcs.GetDialog(new NpcRequest(true){NpcID=900000014,OptionID=11},buyer);
        Check(CountId(buyer,3009001)==1 && buyer.Entity.RevoraPoint==3600
            && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",buyer.Entity.UID)==3600,
            "ordinary Npcs.GetDialog Vesper option 11 sells 3009001 for exactly 400 Revora Points");
        uint boughtUid=buyer.Inventory.Objects.Single(i=>i.ID==3009001).UID;
        buyer=ReloadBall(buyer);
        Check(CountId(buyer,3009001)==1 && buyer.Entity.RevoraPoint==3600 && buyer.Inventory.Objects.Single(i=>i.ID==3009001).UID==boughtUid,
            "Vesper 400-RVP debit and purchased 3009001 survive production reload");
        while(BrightUnits(buyer)<10)Pack(buyer,3009001,1);
        OpenStarStone(buyer,buyer.Inventory.Objects.First(i=>i.ID==3009001));
        Check(!Fenced(buyer) && StarStoneUses(buyer)==1 && CountId(buyer,3009002)==1,"packet-issued Bright Star Stones combine through the owned service");
        StarStone.GetField("Spoken",Hidden).SetValue(null,null);
    }
}
