using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Attacking;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Healing=typeof(Entity).Assembly.GetType("Revora.Game.Features.HealingMedicine");
    static DateTime healNow=DateTime.UtcNow;
    static long HealUses(GameState p){return Scalar("SELECT count(*) FROM revora_healing_medicine_uses WHERE uid=@uid",p.Entity.UID);}
    static long CoolSeconds(GameState p){return Scalar("SELECT COALESCE((SELECT seconds FROM revora_no_drugs_state WHERE uid=@uid),0)",p.Entity.UID);}
    static bool CoolPoison(GameState p){return Scalar("SELECT COALESCE((SELECT CASE WHEN poison_star THEN 1 ELSE 0 END FROM revora_no_drugs_state WHERE uid=@uid),0)",p.Entity.UID)==1;}
    static void DrinkHeal(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static int StarSeconds(){
        if(!SpellTable.SpellInformations.ContainsKey(6002))SpellTable.Load();
        return SpellTable.SpellInformations[6002][0].Duration;
    }
    static void StarPoison(GameState hunter,GameState victim){
        if(!SpellTable.SpellInformations.ContainsKey(6002))SpellTable.Load();
        if(!hunter.Spells.ContainsKey(6002))hunter.Spells[6002]=new Spell(true){ID=6002,Level=0,Available=true};
        hunter.Entity.Stamina=100;hunter.Entity.PKMode=Enums.PKMode.PK;
        hunter.Entity.Teleport(1217,40,40);victim.Entity.Teleport(1217,40,40);
        hunter.Attackable=true;victim.Attackable=true;
        hunter.Screen.Add(victim.Entity);victim.Screen.Add(hunter.Entity);
        hunter.Entity.AttackStamp=default(Time32);
        new Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=6002,Attacker=hunter.Entity.UID,Attacked=victim.Entity.UID,X=victim.Entity.X,Y=victim.Entity.Y},hunter.Entity,victim.Entity);
    }
    static void CastCleanse(GameState caster,GameState target,ushort id){
        if(!SpellTable.SpellInformations.ContainsKey(id))SpellTable.Load();
        if(!caster.Spells.ContainsKey(id))caster.Spells[id]=new Spell(true){ID=id,Level=0,Available=true};
        caster.Entity.Stamina=100;caster.Entity.Mana=500;caster.Entity.PKMode=Enums.PKMode.PK;
        caster.Entity.Teleport(1217,40,40);target.Entity.Teleport(1217,40,40);
        caster.Attackable=true;target.Attackable=true;
        caster.Screen.Add(target.Entity);target.Screen.Add(caster.Entity);
        caster.Entity.AttackStamp=default(Time32);caster.Entity.Tranquility=default(Time32);
        new Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=id,Attacker=caster.Entity.UID,Attacked=target.Entity.UID,X=target.Entity.X,Y=target.Entity.Y},caster.Entity,target.Entity);
    }
    static void VerifyHealingMedicine(string runtime,Map town){
        Call(Healing,null,"Initialize");Call(Healing,null,"Initialize");Call(Welcome,null,"Initialize");
        Healing.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>healNow));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string attacking=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Attacking/Handle.cs")));
        string qualifier=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/Tournaments/Qualifier.cs")));
        string teamQualifier=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/Tournaments/TeamQualifier.cs")));
        Check(handler.IndexOf("Healing medicine must use the owned service.")>=0 && handler.IndexOf("HealingMedicine.TryUse")>handler.IndexOf("PowerArenaPacks.TryUse")
            && usage.IndexOf("HealingMedicine.TryHandle")>usage.IndexOf("PowerArenaPacks.TryHandle")
            && thread.IndexOf("HealingMedicine.Initialize")>thread.IndexOf("AttackDefensePotions.Initialize"),
            "HealingMedicine intercepts sit after Power Arena packs and leftover cases fail closed");
        Check(Regex.Matches(attacking,@"HealingMedicine\.TryClearNoDrugs").Count==3 && attacking.IndexOf("HealingMedicine.TryApplyPoisonStar")>=0
            && qualifier.IndexOf("HealingMedicine.TryClearNoDrugs")>=0 && teamQualifier.IndexOf("HealingMedicine.TryResetNoDrugs")>=0,
            "Serenity, Tranquility and Qualifier NoDrugsTime writers use the owned durable API");
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
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0,"audit table totals parse census categories");
        foreach(uint id in new uint[]{1000000u,1000010u,1000020u,1000030u,1000040u,1001000u,1001010u,1001020u,1001030u,1001040u,1002000u,1002010u,1002020u,1002030u,1002040u,1002050u,725065u,725066u,723726u})
            Check(derived[id]=="owned-fail-closed" && leftover[id]=="fail-closed",id+" leftover medicine is fail-closed");
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
        Check(Scalar("SELECT count(*) FROM pg_index i JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=ANY(i.indkey) WHERE i.indrelid='revora_healing_medicine_uses'::regclass AND i.indisprimary AND a.attname='id'",0)==1,
            "healing medicine receipt ledger primary key is id");
        ushort hpHeal=ConquerItemInformation.BaseInformations[1000000u].ItemHP;
        ushort mpHeal=ConquerItemInformation.BaseInformations[1001000u].ItemMP;
        ushort fruitHeal=ConquerItemInformation.BaseInformations[723726u].ItemHP;
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1}){
            var hpPlayer=BallPlayer(town);hpPlayer.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(hpPlayer))throw new Exception("heal HP fixture save failed");
            var hpItem=NestedPack(hpPlayer,1000000,stack,bound);
            DrinkHeal(hpPlayer,hpItem);
            uint hpExpect=Math.Min(10u+hpHeal,hpPlayer.Entity.MaxHitpoints);
            Check(!Fenced(hpPlayer) && HealUses(hpPlayer)==1 && hpPlayer.Entity.Hitpoints==hpExpect && !hpPlayer.Inventory.ContainsUID(hpItem.UID),
                "ordinary HP medicine heals leftover ItemHP: "+bound+"/"+stack);
            if(!EntityTable.SaveEntity(hpPlayer))throw new Exception("heal HP autosave failed");
            hpPlayer=ReloadBall(hpPlayer);Check(hpPlayer.Entity.Hitpoints==hpExpect && Scalar("SELECT hitpoints FROM entities WHERE uid=@uid",hpPlayer.Entity.UID)==hpExpect,"HP medicine survives production reload");
        }
        foreach(bool remBound in new[]{false,true}){
            var remPlayer=BallPlayer(town);remPlayer.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(remPlayer))throw new Exception("heal remainder fixture save failed");
            var remItem=NestedPack(remPlayer,1000000,2,remBound);DrinkHeal(remPlayer,remItem);
            uint remOnce=Math.Min(10u+hpHeal,remPlayer.Entity.MaxHitpoints);
            Check(!Fenced(remPlayer) && HealUses(remPlayer)==1 && remItem.StackSize==1 && remPlayer.Inventory.ContainsUID(remItem.UID) && remPlayer.Entity.Hitpoints==remOnce,
                "stacked HP medicine keeps the leftover remainder: "+remBound);
            remPlayer=ReloadBall(remPlayer);remItem=remPlayer.Inventory.Objects.Single(i=>i.ID==1000000);DrinkHeal(remPlayer,remItem);
            uint remTwice=Math.Min(remOnce+hpHeal,remPlayer.Entity.MaxHitpoints);
            Check(!Fenced(remPlayer) && HealUses(remPlayer)==2 && !remPlayer.Inventory.ContainsUID(remItem.UID) && remPlayer.Entity.Hitpoints==remTwice,
                "remaining HP medicine unit heals leftover ItemHP: "+remBound);
        }
        var mpPlayer=BallPlayer(town);mpPlayer.Entity.Mana=1;if(!EntityTable.SaveEntity(mpPlayer))throw new Exception("heal MP fixture save failed");
        var mpItem=Pack(mpPlayer,1001000,1);ushort mpExpect=(ushort)Math.Min(1+mpHeal,mpPlayer.Entity.MaxMana);DrinkHeal(mpPlayer,mpItem);
        Check(!Fenced(mpPlayer) && HealUses(mpPlayer)==1 && mpPlayer.Entity.Mana==mpExpect,"ordinary MP medicine heals leftover ItemMP");
        if(!EntityTable.SaveEntity(mpPlayer))throw new Exception("heal MP autosave failed");
        mpPlayer=ReloadBall(mpPlayer);Check(mpPlayer.Entity.Mana==mpExpect && Scalar("SELECT mana FROM entities WHERE uid=@uid",mpPlayer.Entity.UID)==mpExpect,"ordinary MP medicine persists leftover ItemMP");
        var fruitPlayer=BallPlayer(town);fruitPlayer.Entity.Hitpoints=10;fruitPlayer.Entity.Mana=1;if(!EntityTable.SaveEntity(fruitPlayer))throw new Exception("fruit fixture save failed");
        var fruitItem=Pack(fruitPlayer,723726,1);uint fruitHp=Math.Min(10u+fruitHeal,fruitPlayer.Entity.MaxHitpoints);ushort fruitMp=fruitPlayer.Entity.MaxMana;DrinkHeal(fruitPlayer,fruitItem);
        Check(!Fenced(fruitPlayer) && HealUses(fruitPlayer)==1 && fruitPlayer.Entity.Hitpoints==fruitHp && fruitPlayer.Entity.Mana==fruitMp,"LifeFruit heals leftover HP and fills mana");
        if(!EntityTable.SaveEntity(fruitPlayer))throw new Exception("fruit autosave failed");
        fruitPlayer=ReloadBall(fruitPlayer);Check(fruitPlayer.Entity.Hitpoints==fruitHp && fruitPlayer.Entity.Mana==fruitMp && Scalar("SELECT hitpoints FROM entities WHERE uid=@uid",fruitPlayer.Entity.UID)==fruitHp && Scalar("SELECT mana FROM entities WHERE uid=@uid",fruitPlayer.Entity.UID)==fruitMp,"LifeFruit persists leftover HP heal and full mana");
        var blockedPlayer=BallPlayer(town);blockedPlayer.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(blockedPlayer))throw new Exception("blocked fixture save failed");
        blockedPlayer.Entity.NoDrugsTime=10;blockedPlayer.Entity.NoDrugsStamp=Time32.Now;var blockedItem=Pack(blockedPlayer,1000000,1);DrinkHeal(blockedPlayer,blockedItem);
        Check(HealUses(blockedPlayer)==0 && blockedPlayer.Entity.Hitpoints==10 && blockedPlayer.Inventory.ContainsUID(blockedItem.UID),"active leftover NoDrugsTime refuses HP medicine");
        blockedPlayer.Entity.NoDrugsStamp=Time32.Now.AddSeconds(-20);DrinkHeal(blockedPlayer,blockedItem);
        Check(!Fenced(blockedPlayer) && HealUses(blockedPlayer)==1 && blockedPlayer.Entity.NoDrugsTime==0 && blockedPlayer.Entity.Hitpoints==Math.Min(10u+hpHeal,blockedPlayer.Entity.MaxHitpoints),"expired leftover NoDrugsTime clears and heals");
        int starSecs=StarSeconds();
        var starInfo=SpellTable.SpellInformations[6002][0];ulong starPercent=starInfo.Percent;starInfo.Percent=100;
        try{
        var starHunter=BallPlayer(town);var starVictim=BallPlayer(town);starVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(starVictim))throw new Exception("poison fixture save failed");
        var starItem=Pack(starVictim,1000000,1);StarPoison(starHunter,starVictim);
        Check(!Fenced(starVictim) && starVictim.Entity.NoDrugsTime==starSecs && starVictim.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar) && CoolSeconds(starVictim)==starSecs && CoolPoison(starVictim),
            "ordinary Poison Star persists leftover duration before live flag and UI");
        uint starMax=starVictim.Entity.MaxHitpoints;starVictim=ReloadBall(starVictim);starVictim.Entity.MaxHitpoints=starMax;starItem=starVictim.Inventory.Objects.Single(i=>i.ID==1000000);DrinkHeal(starVictim,starItem);
        Check(HealUses(starVictim)==0 && starVictim.Inventory.ContainsUID(starItem.UID) && starVictim.Entity.NoDrugsTime>0 && CoolSeconds(starVictim)==starSecs
            && starVictim.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar) && CoolPoison(starVictim),
            "ordinary Poison Star remaining refuses medicine after production reload");
        healNow=healNow.AddSeconds(starSecs);starVictim=ReloadBall(starVictim);starVictim.Entity.MaxHitpoints=starMax;starVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(starVictim))throw new Exception("expired poison save failed");
        DrinkHeal(starVictim,starVictim.Inventory.Objects.Single(i=>i.ID==1000000));
        Check(!Fenced(starVictim) && HealUses(starVictim)==1 && starVictim.Entity.NoDrugsTime==0 && CoolSeconds(starVictim)==0 && starVictim.Entity.Hitpoints==Math.Min(10u+hpHeal,starMax),
            "expired Poison Star remaining clears with one atomic heal");
        healNow=healNow.AddSeconds(-starSecs);
        var applyHunter=BallPlayer(town);var applyVictim=BallPlayer(town);applyVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(applyVictim))throw new Exception("poison fail fixture save failed");
        var applyItem=Pack(applyVictim,1000000,1);
        Query("CREATE FUNCTION suppress_poison() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_poison BEFORE INSERT ON revora_no_drugs_state FOR EACH ROW EXECUTE FUNCTION suppress_poison()");
        try{StarPoison(applyHunter,applyVictim);
        Check(Fenced(applyVictim) && applyVictim.Entity.NoDrugsTime==0 && !applyVictim.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar) && CoolSeconds(applyVictim)==0 && applyVictim.Inventory.ContainsUID(applyItem.UID),
            "suppressed Poison Star persist fences without a live/durable mismatch");}
        finally{Query("DROP TRIGGER IF EXISTS suppress_poison ON revora_no_drugs_state;DROP FUNCTION IF EXISTS suppress_poison()");}
        uint applyMax=applyVictim.Entity.MaxHitpoints;applyVictim=ReloadBall(applyVictim);applyVictim.Entity.MaxHitpoints=applyMax;applyVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(applyVictim))throw new Exception("poison retry save failed");
        StarPoison(applyHunter,applyVictim);
        Check(!Fenced(applyVictim) && applyVictim.Entity.NoDrugsTime==starSecs && CoolSeconds(applyVictim)==starSecs,"production reload retries Poison Star after durable write failure");
        var raceHunter=BallPlayer(town);var raceVictim=BallPlayer(town);raceVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(raceVictim))throw new Exception("poison race fixture save failed");
        var racePack=Pack(raceVictim,1000000,1);
        Parallel.For(0,16,n=>{if(n%2==0)StarPoison(raceHunter,raceVictim);else DrinkHeal(raceVictim,racePack);});
        Check(!Fenced(raceVictim) && HealUses(raceVictim)<=1 && !(raceVictim.Entity.NoDrugsTime>0 && CoolSeconds(raceVictim)==0)
            && (HealUses(raceVictim)==1?!raceVictim.Inventory.ContainsUID(racePack.UID):raceVictim.Inventory.ContainsUID(racePack.UID)),
            "Poison Star application and medicine use cannot leave a live/durable mismatch");
        }finally{starInfo.Percent=starPercent;}
        var calmHunter=BallPlayer(town);var calmVictim=BallPlayer(town);calmVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(calmVictim))throw new Exception("serenity fixture save failed");
        var calmItem=Pack(calmVictim,1000000,1);starInfo.Percent=100;StarPoison(calmHunter,calmVictim);starInfo.Percent=starPercent;
        var calmCaster=BallPlayer(town);CastCleanse(calmCaster,calmVictim,10400);
        Check(!Fenced(calmVictim) && CoolSeconds(calmVictim)==0 && calmVictim.Entity.NoDrugsTime==0 && !calmVictim.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar),
            "ordinary Serenity clears durable NoDrugsTime and PoisonStar");
        uint calmMax=calmVictim.Entity.MaxHitpoints;calmVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(calmVictim))throw new Exception("serenity heal save failed");
        DrinkHeal(calmVictim,calmItem);calmVictim=ReloadBall(calmVictim);
        Check(!Fenced(calmVictim) && HealUses(calmVictim)==1 && CoolSeconds(calmVictim)==0 && calmVictim.Entity.Hitpoints==Math.Min(10u+hpHeal,calmMax),
            "medicine use after Serenity persists through production reload");
        var stillHunter=BallPlayer(town);var stillVictim=BallPlayer(town);stillVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(stillVictim))throw new Exception("tranquility fixture save failed");
        var stillItem=Pack(stillVictim,1000000,1);starInfo.Percent=100;StarPoison(stillHunter,stillVictim);starInfo.Percent=starPercent;
        var stillCaster=BallPlayer(town);CastCleanse(stillCaster,stillVictim,10425);
        Check(!Fenced(stillVictim) && CoolSeconds(stillVictim)==0 && stillVictim.Entity.NoDrugsTime==0 && !stillVictim.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar),
            "ordinary Tranquility clears durable NoDrugsTime");
        stillVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(stillVictim))throw new Exception("tranquility heal save failed");
        DrinkHeal(stillVictim,stillItem);stillVictim=ReloadBall(stillVictim);
        Check(!Fenced(stillVictim) && HealUses(stillVictim)==1 && CoolSeconds(stillVictim)==0,"medicine use after Tranquility survives production reload");
        var epicHunter=BallPlayer(town);var epicVictim=BallPlayer(town);epicVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(epicVictim))throw new Exception("epic tranquility fixture save failed");
        var epicItem=Pack(epicVictim,1000000,1);starInfo.Percent=100;StarPoison(epicHunter,epicVictim);starInfo.Percent=starPercent;
        var epicCaster=BallPlayer(town);epicCaster.Weapons=Tuple.Create(new ConquerItem(true){ID=622007},(ConquerItem)null);
        Check(epicCaster.Entity.EpicMonje(),"epic Tranquility branch uses leftover 622 weapons");
        CastCleanse(epicCaster,epicVictim,10425);
        Check(!Fenced(epicVictim) && CoolSeconds(epicVictim)==0 && epicVictim.Entity.NoDrugsTime==0 && !epicVictim.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar),
            "epic Tranquility branch clears durable NoDrugsTime");
        epicVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(epicVictim))throw new Exception("epic heal save failed");
        DrinkHeal(epicVictim,epicItem);epicVictim=ReloadBall(epicVictim);
        Check(!Fenced(epicVictim) && HealUses(epicVictim)==1 && CoolSeconds(epicVictim)==0,"medicine use after epic Tranquility survives production reload");
        var deadHunter=BallPlayer(town);var deadVictim=BallPlayer(town);deadVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(deadVictim))throw new Exception("dead tranquility fixture save failed");
        var deadItem=Pack(deadVictim,1000000,1);starInfo.Percent=100;StarPoison(deadHunter,deadVictim);starInfo.Percent=starPercent;
        deadVictim.Entity.Hitpoints=0;var deadCaster=BallPlayer(town);CastCleanse(deadCaster,deadVictim,10425);
        Check(!Fenced(deadVictim) && deadVictim.Entity.Dead && CoolSeconds(deadVictim)==0 && deadVictim.Entity.NoDrugsTime==0 && !deadVictim.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar),
            "Tranquility clears durable NoDrugsTime on a dead player target");
        uint deadMax=deadVictim.Entity.MaxHitpoints;deadVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(deadVictim))throw new Exception("dead heal save failed");
        DrinkHeal(deadVictim,deadItem);deadVictim=ReloadBall(deadVictim);
        Check(!Fenced(deadVictim) && HealUses(deadVictim)==1 && CoolSeconds(deadVictim)==0 && deadVictim.Entity.Hitpoints==Math.Min(10u+hpHeal,deadMax),
            "medicine use after dead-target Tranquility survives production reload");
        var winHunter=BallPlayer(town);var winVictim=BallPlayer(town);var winMate=BallPlayer(town);winVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(winVictim))throw new Exception("qualifier fixture save failed");
        var winItem=Pack(winVictim,1000000,1);starInfo.Percent=100;StarPoison(winHunter,winVictim);starInfo.Percent=starPercent;
        winVictim.TeamArenaStatistic=new TeamQualifierStatistic(true){EntityID=winVictim.Entity.UID,Name=winVictim.Entity.Name};winMate.TeamArenaStatistic=new TeamQualifierStatistic(true){EntityID=winMate.Entity.UID,Name=winMate.Entity.Name};
        winVictim.ArenaStatistic.EntityID=winVictim.Entity.UID;winVictim.ArenaStatistic.Name=winVictim.Entity.Name;winVictim.ArenaStatistic.Level=winVictim.Entity.Level;winVictim.ArenaStatistic.Class=winVictim.Entity.Class;
        winMate.ArenaStatistic.EntityID=winMate.Entity.UID;winMate.ArenaStatistic.Name=winMate.Entity.Name;winMate.ArenaStatistic.Level=winMate.Entity.Level;winMate.ArenaStatistic.Class=winMate.Entity.Class;
        winVictim.ArenaPoints=1000;winMate.ArenaPoints=1000;
        winVictim.ArenaStatistic.PlayWith=winMate;winMate.ArenaStatistic.PlayWith=winVictim;Qualifier.Win(winMate,winVictim);
        Check(!Fenced(winVictim) && CoolSeconds(winVictim)==0 && winVictim.Entity.NoDrugsTime==0,
            "Solo Qualifier departure clears durable NoDrugsTime without resurrecting PoisonStar");
        winVictim=ReloadBall(winVictim);
        Check(!Fenced(winVictim) && CoolSeconds(winVictim)==0 && winVictim.Entity.NoDrugsTime==0 && !winVictim.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar) && !CoolPoison(winVictim),
            "reload after Qualifier departure does not resurrect PoisonStar");
        winItem=winVictim.Inventory.Objects.Single(i=>i.ID==1000000);winVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(winVictim))throw new Exception("qualifier heal save failed");
        DrinkHeal(winVictim,winItem);winVictim=ReloadBall(winVictim);
        Check(!Fenced(winVictim) && HealUses(winVictim)==1 && CoolSeconds(winVictim)==0 && !winVictim.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar),
            "medicine use after Qualifier reset survives production reload");
        var resetHunter=BallPlayer(town);var resetVictim=BallPlayer(town);resetVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(resetVictim))throw new Exception("team reset fixture save failed");
        var resetItem=Pack(resetVictim,1000000,1);starInfo.Percent=100;StarPoison(resetHunter,resetVictim);starInfo.Percent=starPercent;
        var groupType=typeof(TeamQualifier).GetNestedType("QualifierList").GetNestedType("QualifierGroup");
        Call(groupType,null,"RestoreFighter",resetVictim);
        Check(!Fenced(resetVictim) && CoolSeconds(resetVictim)==1 && resetVictim.Entity.NoDrugsTime==1 && !resetVictim.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar) && !CoolPoison(resetVictim),
            "Team Qualifier restore persists leftover one-second NoDrugsTime without PoisonStar");
        resetVictim=ReloadBall(resetVictim);
        Check(!Fenced(resetVictim) && CoolSeconds(resetVictim)==1 && resetVictim.Entity.NoDrugsTime==1 && !resetVictim.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar) && !CoolPoison(resetVictim),
            "reload of Team Qualifier one-second remaining does not resurrect PoisonStar");
        resetVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(resetVictim))throw new Exception("team reset heal save failed");
        DrinkHeal(resetVictim,resetVictim.Inventory.Objects.Single(i=>i.ID==1000000));
        Check(HealUses(resetVictim)==0 && resetVictim.Inventory.ContainsUID(resetItem.UID) && resetVictim.Entity.NoDrugsTime>0,
            "one-second Team Qualifier remaining refuses medicine after reload");
        healNow=healNow.AddSeconds(1);resetVictim=ReloadBall(resetVictim);resetVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(resetVictim))throw new Exception("team reset expiry save failed");
        DrinkHeal(resetVictim,resetVictim.Inventory.Objects.Single(i=>i.ID==1000000));
        Check(!Fenced(resetVictim) && HealUses(resetVictim)==1 && CoolSeconds(resetVictim)==0,"expired Team Qualifier remaining clears with one atomic heal");
        healNow=healNow.AddSeconds(-1);
        var faultHunter=BallPlayer(town);var faultVictim=BallPlayer(town);faultVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(faultVictim))throw new Exception("clear fault fixture save failed");
        var faultItem=Pack(faultVictim,1000000,1);starInfo.Percent=100;StarPoison(faultHunter,faultVictim);starInfo.Percent=starPercent;
        var faultCaster=BallPlayer(town);
        Query("CREATE FUNCTION suppress_clear() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_clear BEFORE UPDATE ON revora_no_drugs_state FOR EACH ROW EXECUTE FUNCTION suppress_clear()");
        try{CastCleanse(faultCaster,faultVictim,10400);
        Check(Fenced(faultVictim) && faultVictim.Entity.NoDrugsTime>0 && CoolSeconds(faultVictim)==StarSeconds() && faultVictim.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar),
            "suppressed Serenity persist fences without clearing live or durable Poison Star");}
        finally{Query("DROP TRIGGER IF EXISTS suppress_clear ON revora_no_drugs_state;DROP FUNCTION IF EXISTS suppress_clear()");}
        uint faultMax=faultVictim.Entity.MaxHitpoints;faultVictim=ReloadBall(faultVictim);faultVictim.Entity.MaxHitpoints=faultMax;faultVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(faultVictim))throw new Exception("clear retry save failed");
        var retryCaster=BallPlayer(town);CastCleanse(retryCaster,faultVictim,10400);
        Check(!Fenced(faultVictim) && CoolSeconds(faultVictim)==0 && faultVictim.Entity.NoDrugsTime==0 && !faultVictim.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar),
            "production reload retries Serenity after durable clear failure");
        var mixHunter=BallPlayer(town);var mixVictim=BallPlayer(town);mixVictim.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(mixVictim))throw new Exception("clear race fixture save failed");
        var mixItem=Pack(mixVictim,1000000,1);var mixCaster=BallPlayer(town);starInfo.Percent=100;
        try{Parallel.For(0,16,n=>{if(n%3==0)StarPoison(mixHunter,mixVictim);else if(n%3==1)CastCleanse(mixCaster,mixVictim,10400);else DrinkHeal(mixVictim,mixItem);});}
        finally{starInfo.Percent=starPercent;}
        Check(!Fenced(mixVictim) && HealUses(mixVictim)<=1 && !(mixVictim.Entity.NoDrugsTime>0 && CoolSeconds(mixVictim)==0),
            "Poison Star, Serenity and medicine use cannot leave a live/durable mismatch");
        var mapPlayer=BallPlayer(town);mapPlayer.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(mapPlayer))throw new Exception("map fixture save failed");
        mapPlayer.Entity.MapID=1707;var mapItem=Pack(mapPlayer,1000000,1);DrinkHeal(mapPlayer,mapItem);
        Check(HealUses(mapPlayer)==0 && mapPlayer.Inventory.ContainsUID(mapItem.UID) && mapPlayer.Entity.Hitpoints==10,"map 1707 leftover healing refusal consumes nothing");
        foreach(string fault in new[]{"source","timer","hp","receipt"}){
            var failPlayer=BallPlayer(town);failPlayer.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(failPlayer))throw new Exception("fault fixture save failed");
            var failItem=Pack(failPlayer,1000000,1);
            string table=fault=="timer"?"revora_no_drugs_state":fault=="receipt"?"revora_healing_medicine_uses":fault=="hp"?"entities":"items";
            string verb=fault=="source"?"DELETE":fault=="hp"?"UPDATE":"INSERT";
            Query("CREATE FUNCTION suppress_heal() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_heal BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_heal()");DrinkHeal(failPlayer,failItem);
            Check(Fenced(failPlayer) && HealUses(failPlayer)==0 && failPlayer.Inventory.ContainsUID(failItem.UID) && failPlayer.Entity.Hitpoints==10,"suppressed "+fault+" preserves medicine, HP and cooldown together");
            Query("DROP TRIGGER suppress_heal ON "+table+";DROP FUNCTION suppress_heal()");
            uint failMax=failPlayer.Entity.MaxHitpoints;failPlayer=ReloadBall(failPlayer);failPlayer.Entity.MaxHitpoints=failMax;failPlayer.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(failPlayer))throw new Exception("fault retry save failed");
            DrinkHeal(failPlayer,failPlayer.Inventory.Objects.Single(i=>i.ID==1000000));
            Check(!Fenced(failPlayer) && HealUses(failPlayer)==1 && failPlayer.Entity.Hitpoints==Math.Min(10u+hpHeal,failMax),"production reload retries healing medicine after "+fault+" rollback");
        }
        var stalePlayer=BallPlayer(town);stalePlayer.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(stalePlayer))throw new Exception("stale HP fixture save failed");
        var staleItem=Pack(stalePlayer,1000000,1);
        Query("UPDATE entities SET hitpoints=hitpoints+1 WHERE uid=@uid","uid",(long)stalePlayer.Entity.UID);DrinkHeal(stalePlayer,staleItem);
        Check(Fenced(stalePlayer) && HealUses(stalePlayer)==0 && stalePlayer.Inventory.ContainsUID(staleItem.UID) && stalePlayer.Entity.Hitpoints==10
            && Scalar("SELECT hitpoints FROM entities WHERE uid=@uid",stalePlayer.Entity.UID)==11,"stale HP preimage cannot overwrite a newer durable hitpoints row");
        uint staleMax=stalePlayer.Entity.MaxHitpoints;stalePlayer=ReloadBall(stalePlayer);stalePlayer.Entity.MaxHitpoints=staleMax;stalePlayer.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(stalePlayer))throw new Exception("stale HP retry save failed");
        DrinkHeal(stalePlayer,stalePlayer.Inventory.Objects.Single(i=>i.ID==1000000));
        Check(!Fenced(stalePlayer) && HealUses(stalePlayer)==1 && stalePlayer.Entity.Hitpoints==Math.Min(10u+hpHeal,staleMax),"reload exposes durable HP and retries leftover medicine");
        var welcomePlayer=WelcomePlayer(town,1,0,15);var welcomePack=Pack(welcomePlayer,723753,1);OpenWelcome(welcomePlayer,welcomePack);
        var issued=welcomePlayer.Inventory.Objects.First(i=>i.ID==1000000);welcomePlayer.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(welcomePlayer))throw new Exception("welcome heal save failed");
        uint welcomeExpect=Math.Min(10u+hpHeal,welcomePlayer.Entity.MaxHitpoints);DrinkHeal(welcomePlayer,issued);
        Check(!Fenced(welcomePlayer) && HealUses(welcomePlayer)==1 && welcomePlayer.Entity.Hitpoints==welcomeExpect,"ordinary WelcomePack 1000000 issuance is usable through HealingMedicine");
        if(!EntityTable.SaveEntity(welcomePlayer))throw new Exception("welcome heal autosave failed");
        welcomePlayer=ReloadBall(welcomePlayer);Check(welcomePlayer.Entity.Hitpoints==welcomeExpect && Scalar("SELECT hitpoints FROM entities WHERE uid=@uid",welcomePlayer.Entity.UID)==welcomeExpect,"WelcomePack 1000000 heal survives production reload");
        var framesPlayer=BallPlayer(town);framesPlayer.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(framesPlayer))throw new Exception("frame fixture save failed");
        var wire=Pack(framesPlayer,1000000,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,framesPlayer);Check(!Fenced(framesPlayer) && HealUses(framesPlayer)==0,"bounded healing medicine rejects "+bad+" frame");
        }DrinkHeal(framesPlayer,wire);Check(!Fenced(framesPlayer) && HealUses(framesPlayer)==1,"ordinary healing medicine works after malformed frames");
        var racePlayer=BallPlayer(town);racePlayer.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(racePlayer))throw new Exception("race fixture save failed");
        var raceItem=Pack(racePlayer,1000000,1);Parallel.For(0,16,n=>DrinkHeal(racePlayer,raceItem));
        Check(!Fenced(racePlayer) && HealUses(racePlayer)==1 && !racePlayer.Inventory.ContainsUID(raceItem.UID),"parallel packets cannot duplicate HP medicine");
        var directPlayer=BallPlayer(town);directPlayer.Entity.Hitpoints=10;if(!EntityTable.SaveEntity(directPlayer))throw new Exception("direct fixture save failed");
        var directItem=Pack(directPlayer,1000000,1);ItemHandler.UseItem(directItem,directPlayer);
        Check(!Fenced(directPlayer) && HealUses(directPlayer)==1 && directPlayer.Entity.Hitpoints==Math.Min(10u+hpHeal,directPlayer.Entity.MaxHitpoints),"direct UseItem heals leftover HP medicine");
    }
}
