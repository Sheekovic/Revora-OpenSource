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
    static readonly Type AttackDefense=typeof(Entity).Assembly.GetType("Revora.Game.Features.AttackDefensePotions");
    static DateTime attackDefNow=DateTime.UtcNow;
    static long AttackDefenseUses(GameState p){return Scalar("SELECT count(*) FROM revora_attack_defense_potion_uses WHERE uid=@uid",p.Entity.UID);}
    static void OpenAttackDefense(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static bool HasStigma(GameState p){return p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Stigma);}
    static bool HasShield(GameState p){return p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.MagicShield);}
    static int FlagSeconds(GameState p,FlagsUpdate.Flags flag){return p.Entity.BitVector.ArrayFlags[(int)flag].Secounds;}
    static int PotionSeconds(uint id){return id==721627||id==721628?45:id==721672||id==721673?30:15;}
    static bool PotionStigma(uint id){return id==721623||id==721672||id==721627;}
    static void VerifyAttackDefensePotions(string runtime,Map town){
        Call(AttackDefense,null,"Initialize");Call(AttackDefense,null,"Initialize");
        Call(Starter,null,"Initialize");
        AttackDefense.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>attackDefNow));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/AttackDefensePotions.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        Check(handler.IndexOf("AttackDefensePotions.TryUse")>handler.IndexOf("StarStoneCombine.TryUse")
            && usage.IndexOf("AttackDefensePotions.TryHandle")>usage.IndexOf("StarStoneCombine.TryHandle")
            && thread.IndexOf("AttackDefensePotions.Initialize")>thread.IndexOf("StarStoneCombine.Initialize"),
            "AttackDefensePotions intercepts sit after StarStoneCombine");
        Check(handler.IndexOf("Attack defense potion must use the owned service.")>=0
            && service.IndexOf("Your attack will increase during the next 15 minutes")>=0
            && service.IndexOf("Your Defense will increase during the next 45 minutes")>=0,
            "leftover Attack/Defense cases are fail-closed and leftover texts are owned");
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
        foreach(uint id in new uint[]{721623u,721624u,721627u,721628u,721672u,721673u})
            Check(derived[id]=="owned-fail-closed" && leftover[id]=="fail-closed" && audit.IndexOf("| "+id+" | owned-fail-closed | source+timer |")>=0,
                id+" is owned fail-closed source+timer");
        var interceptNames=new System.Collections.Generic.List<string>();
        foreach(Match im in Regex.Matches(handler.Substring(0,switchStart),@"if \(Revora\.Game\.Features\.(\w+)\.TryUse\(item, client\)\) return;"))
            interceptNames.Add(im.Groups[1].Value);
        Check(interceptNames.Count==67,"UseItem has 67 pre-switch TryUse intercepts");
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
        string heard="";AttackDefense.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        foreach(uint type in new uint[]{721623u,721624u,721672u,721673u,721627u,721628u})foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1}){
            var p=BallPlayer(town);var item=NestedPack(p,type,stack,bound);int seconds=PotionSeconds(type);bool stigma=PotionStigma(type);
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else OpenAttackDefense(p,item);});
            Check(!Fenced(p) && AttackDefenseUses(p)==1 && !p.Inventory.ContainsUID(item.UID)
                && (stigma?p.Entity.StigmaTime==seconds && p.Entity.StigmaIncrease==seconds && HasStigma(p):p.Entity.MagicShieldTime==seconds && p.Entity.MagicShieldIncrease==seconds && HasShield(p)),
                "ordinary Attack/Defense packet/autosave commits leftover duration and one source unit: "+type+"/"+bound+"/"+stack);
            ItemHandler.UseItem(item,p);p=ReloadBall(p);
            Check(!Fenced(p) && AttackDefenseUses(p)==1
                && (stigma?p.Entity.StigmaTime==seconds && HasStigma(p):p.Entity.MagicShieldTime==seconds && HasShield(p))
                && Scalar("SELECT "+(stigma?"stigma_seconds":"magic_seconds")+" FROM revora_attack_defense_state WHERE uid=@uid",p.Entity.UID)==seconds,
                "production reload retains the leftover Attack/Defense grant: "+type);
        }
        foreach(uint type in new uint[]{721623u,721624u,721672u,721673u,721627u,721628u})foreach(bool bound in new[]{false,true}){
            var p=BallPlayer(town);var item=NestedPack(p,type,2,bound);int seconds=PotionSeconds(type);bool stigma=PotionStigma(type);
            OpenAttackDefense(p,item);
            Check(!Fenced(p) && AttackDefenseUses(p)==1 && item.StackSize==1 && p.Inventory.ContainsUID(item.UID)
                && (stigma?p.Entity.StigmaTime==seconds && HasStigma(p):p.Entity.MagicShieldTime==seconds && HasShield(p)),
                "stacked Attack/Defense potion keeps the leftover remainder: "+type+"/"+bound);
            p=ReloadBall(p);item=p.Inventory.Objects.Single(i=>i.ID==type);OpenAttackDefense(p,item);
            Check(!Fenced(p) && AttackDefenseUses(p)==2 && !p.Inventory.ContainsUID(item.UID)
                && (stigma?p.Entity.StigmaTime==seconds && p.Entity.StigmaIncrease==seconds && HasStigma(p):p.Entity.MagicShieldTime==seconds && p.Entity.MagicShieldIncrease==seconds && HasShield(p)),
                "remaining Attack/Defense unit overwrites the leftover grant: "+type+"/"+bound);
        }
        heard="";var atk=BallPlayer(town);var atkItem=Pack(atk,721623,1);OpenAttackDefense(atk,atkItem);
        Check(!Fenced(atk) && heard.IndexOf("Your attack will increase during the next 15 minutes")>=0 && atk.Entity.StigmaTime==15 && atk.Entity.StigmaIncrease==15 && HasStigma(atk),
            "721623 applies leftover 15-second Stigma Time/Increase/flag and leftover minutes text");
        heard="";var def=BallPlayer(town);var defItem=Pack(def,721624,1);OpenAttackDefense(def,defItem);
        Check(!Fenced(def) && heard.IndexOf("Your Defense will increase during the next 15 minutes")>=0 && def.Entity.MagicShieldTime==15 && def.Entity.MagicShieldIncrease==15 && HasShield(def),
            "721624 applies leftover 15-second MagicShield Time/Increase/flag and leftover Defense text");
        var both=BallPlayer(town);var bothAtk=Pack(both,721623,1);var bothDef=Pack(both,721624,1);OpenAttackDefense(both,bothAtk);OpenAttackDefense(both,bothDef);
        Check(!Fenced(both) && AttackDefenseUses(both)==2 && both.Entity.StigmaTime==15 && both.Entity.MagicShieldTime==15 && HasStigma(both) && HasShield(both),
            "attack and defense potions persist independently");
        both=ReloadBall(both);
        Check(both.Entity.StigmaTime==15 && both.Entity.MagicShieldTime==15 && HasStigma(both) && HasShield(both),"independent Attack/Defense grants survive production reload");
        var over=BallPlayer(town);var overFirst=Pack(over,721623,1);var overNext=Pack(over,721627,1);OpenAttackDefense(over,overFirst);OpenAttackDefense(over,overNext);
        Check(!Fenced(over) && AttackDefenseUses(over)==2 && over.Entity.StigmaTime==45 && over.Entity.StigmaIncrease==45 && HasStigma(over)
            && FlagSeconds(over,FlagsUpdate.Flags.Stigma)==45,
            "a later Attack potion overwrites the entity and live flag Stigma duration");
        var overDefense=BallPlayer(town);var overDefenseFirst=Pack(overDefense,721624,1);var overDefenseNext=Pack(overDefense,721628,1);
        OpenAttackDefense(overDefense,overDefenseFirst);OpenAttackDefense(overDefense,overDefenseNext);
        Check(!Fenced(overDefense) && AttackDefenseUses(overDefense)==2 && overDefense.Entity.MagicShieldTime==45 && overDefense.Entity.MagicShieldIncrease==45
            && HasShield(overDefense) && FlagSeconds(overDefense,FlagsUpdate.Flags.MagicShield)==45,
            "a later Defense potion overwrites the entity and live flag MagicShield duration");
        var full=BallPlayer(town);var fullItem=Pack(full,721623,1);while(full.Inventory.Count<40)Pack(full,1000000,1);OpenAttackDefense(full,fullItem);
        Check(!Fenced(full) && AttackDefenseUses(full)==1 && full.Inventory.Count==39,"Attack/Defense use needs no empty inventory slot");
        foreach(string fault in new[]{"source","stack","timer","receipt"}){
            var p=BallPlayer(town);var item=Pack(p,721623,(ushort)(fault=="stack"?2:1));
            string table=fault=="timer"?"revora_attack_defense_state":fault=="receipt"?"revora_attack_defense_potion_uses":"items";
            string verb=fault=="source"?"DELETE":fault=="receipt"||fault=="timer"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION suppress_attack_defense() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_attack_defense BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_attack_defense()");OpenAttackDefense(p,item);
            Check(Fenced(p) && AttackDefenseUses(p)==0 && p.Entity.StigmaTime==0 && !HasStigma(p) && p.Inventory.ContainsUID(item.UID) && SavedRune(item,"stacksize")==item.StackSize
                && Scalar("SELECT count(*) FROM revora_attack_defense_state WHERE uid=@uid",p.Entity.UID)==0,"suppressed "+fault+" preserves Attack potion, timer and receipt together");
            Query("DROP TRIGGER suppress_attack_defense ON "+table+";DROP FUNCTION suppress_attack_defense()");p=ReloadBall(p);OpenAttackDefense(p,p.Inventory.Objects.Single(i=>i.ID==721623));
            Check(!Fenced(p) && AttackDefenseUses(p)==1 && p.Entity.StigmaTime==15 && HasStigma(p),"production reload retries the Attack potion after "+fault+" rollback");
        }
        var stale=BallPlayer(town);var staleItem=Pack(stale,721623,2);OpenAttackDefense(stale,staleItem);
        Query("UPDATE revora_attack_defense_state SET stigma_seconds=45,stigma_increase=45 WHERE uid=@uid","uid",(long)stale.Entity.UID);OpenAttackDefense(stale,staleItem);
        Check(Fenced(stale) && AttackDefenseUses(stale)==1 && stale.Inventory.ContainsUID(staleItem.UID) && Scalar("SELECT stigma_seconds FROM revora_attack_defense_state WHERE uid=@uid",stale.Entity.UID)==45,
            "stale Attack/Defense preimage cannot overwrite a newer durable grant");
        stale=ReloadBall(stale);staleItem=stale.Inventory.Objects.Single(i=>i.ID==721623);OpenAttackDefense(stale,staleItem);
        Check(!Fenced(stale) && AttackDefenseUses(stale)==2 && stale.Entity.StigmaTime==15,"reload exposes the durable grant and retries leftover overwrite");
        var expired=BallPlayer(town);var expiredItem=Pack(expired,721623,1);OpenAttackDefense(expired,expiredItem);
        attackDefNow=attackDefNow.AddSeconds(15);expired=ReloadBall(expired);
        Check(!Fenced(expired) && AttackDefenseUses(expired)==1 && expired.Entity.StigmaTime==0 && !HasStigma(expired),"expired Attack grant does not restore a Stigma flag");
        attackDefNow=attackDefNow.AddSeconds(-15);
        foreach(string guard in new[]{"dead","fake","trade","locked","suspicious","wardrobe","expired","substituted"}){
            var p=BallPlayer(town);var item=Pack(p,721623,1);
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;if(guard=="locked")item.Lock=1;
            if(guard=="suspicious")item.Suspicious=true;if(guard=="wardrobe")item.InWardrobe=true;if(guard=="expired"){item.Days=1;item.DayStamp=attackDefNow.ToLocalTime().AddDays(-2);}
            if(guard=="substituted")ItemHandler.UseItem(new ConquerItem(true){ID=item.ID,UID=item.UID},p);else OpenAttackDefense(p,item);
            Check(AttackDefenseUses(p)==0 && p.Entity.StigmaTime==0 && p.Inventory.ContainsUID(item.UID),"ineligible Attack potion leaves source and timer unchanged: "+guard);
        }
        var frames=BallPlayer(town);var wire=Pack(frames,721623,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && AttackDefenseUses(frames)==0 && frames.Entity.StigmaTime==0,"bounded Attack potion rejects "+bad+" frame");
        }OpenAttackDefense(frames,wire);Check(!Fenced(frames) && AttackDefenseUses(frames)==1 && frames.Entity.StigmaTime==15,"ordinary Attack potion works after malformed frames");
        var replay=BallPlayer(town);var once=Pack(replay,721623,1);uint uid=once.UID;OpenAttackDefense(replay,once);
        ItemHandler.UseItem(once,replay);Check(AttackDefenseUses(replay)==1 && !replay.Inventory.ContainsUID(uid),"consumed Attack potion UID cannot replay");
        var direct=BallPlayer(town);var directPack=Pack(direct,721624,1);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && AttackDefenseUses(direct)==1 && direct.Entity.MagicShieldTime==15 && HasShield(direct),"direct UseItem applies leftover MagicShield");
        var race=BallPlayer(town);var racePack=Pack(race,721623,1);
        Parallel.For(0,16,n=>OpenAttackDefense(race,racePack));
        Check(!Fenced(race) && AttackDefenseUses(race)==1 && race.Entity.StigmaTime==15 && !race.Inventory.ContainsUID(racePack.UID),"parallel packets cannot duplicate Attack potion consumption");
        var l10=WelcomePlayer(town,10,0,15);var ten=Pack(l10,727028,1);OpenStarter(l10,ten);
        var issuedAtk=l10.Inventory.Objects.First(i=>i.ID==721623);var issuedDef=l10.Inventory.Objects.First(i=>i.ID==721624);
        OpenAttackDefense(l10,issuedAtk);OpenAttackDefense(l10,issuedDef);
        Check(!Fenced(l10) && AttackDefenseUses(l10)==2 && l10.Entity.StigmaTime==15 && l10.Entity.MagicShieldTime==15 && HasStigma(l10) && HasShield(l10)
            && !l10.Inventory.ContainsUID(issuedAtk.UID) && !l10.Inventory.ContainsUID(issuedDef.UID),
            "ordinary L10 StarterPack 721623/721624 issuance is usable through AttackDefensePotions");
        l10=ReloadBall(l10);Check(l10.Entity.StigmaTime==15 && l10.Entity.MagicShieldTime==15 && HasStigma(l10) && HasShield(l10),"StarterPack-issued 15-second buffs survive production reload");
        var l75=WelcomePlayer(town,75,0,15);var seventyFive=Pack(l75,727041,1);OpenStarter(l75,seventyFive);
        var cAtk=l75.Inventory.Objects.First(i=>i.ID==721672);OpenAttackDefense(l75,cAtk);
        Check(!Fenced(l75) && AttackDefenseUses(l75)==1 && l75.Entity.StigmaTime==30 && l75.Entity.StigmaIncrease==30 && HasStigma(l75),"ordinary L75 StarterPack 721672 applies leftover 30-second Stigma");
        var l120=WelcomePlayer(town,120,0,15);var terminal=Pack(l120,727050,1);OpenStarter(l120,terminal);
        var dAtk=l120.Inventory.Objects.First(i=>i.ID==721627);OpenAttackDefense(l120,dAtk);
        Check(!Fenced(l120) && AttackDefenseUses(l120)==1 && l120.Entity.StigmaTime==45 && l120.Entity.StigmaIncrease==45 && HasStigma(l120),"ordinary L120 StarterPack 721627 applies leftover 45-second Stigma");
        AttackDefense.GetField("Spoken",Hidden).SetValue(null,null);
    }
}
