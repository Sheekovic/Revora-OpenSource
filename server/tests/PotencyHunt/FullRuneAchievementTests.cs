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
    static GameState FullRunePlayer(Map map,byte profession) {
        var p=LoadoutPlayer(map);p.Entity.FullyLoaded=false;p.Entity.Class=profession;p.Entity.Reborn=2;p.Entity.FullyLoaded=true;
        Query("UPDATE entities SET class=@class WHERE uid=@uid","class",(int)profession,"uid",(long)p.Entity.UID);return p;
    }
    static ConquerItem[] FullRuneSet(GameState p,bool alternate,bool upgrade) {
        int family=p.Entity.Class/10;
        int[] families={1,2,4,5,6,7,8,13,14,16};int index=Array.IndexOf(families,family);if(index<0)index=0;
        uint red=(uint)(4010001+100*index),blue=(uint)(4020100+100*index)+(upgrade?26u:27u);
        var items=new[]{RuneItem(p,red),RuneItem(p,blue),RuneItem(p,4030109),RuneItem(p,4030209),RuneItem(p,4030309)};
        if(upgrade){items[1].RunesEXP=29999;Query("UPDATE items SET runesexp=29999 WHERE uid=@uid","uid",(long)items[1].UID);}
        // Leave blue last, to exercise either the fifth equip or its final stone upgrade.
        foreach(int i in new[]{0,2,3,4})RuneMove(p,items[i],57,(uint)(101+i+(alternate?20:0)));
        return items;
    }
    static long FullRuneReceipts(GameState p) {
        return Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id BETWEEN 11312 AND 11321",p.Entity.UID);
    }
    static void FullRuneComposeFailure(GameState p,ConquerItem rune,ConquerItem stone) {
        // The synthetic Screen has no timer-disposal lock; this is outside the transaction under test.
        try {RunesSystem.HandlePacket1009(p,ComposePacket(rune.UID,stone.UID));}
        catch(ArgumentNullException) {if(!Fenced(p))throw;}
    }
    static void VerifyFullRuneAchievements(Map map) {
        RunesSystem.LoadRunesEXP();SpellTable.Load();
        byte[] classes={10,20,142,132,40,50,60,80,70,160};
        for(int n=0;n<classes.Length;n++)foreach(bool alternate in new[]{false,true}) {
            var p=FullRunePlayer(map,(byte)(classes[n]+(alternate?(classes[n]>=130 && classes[n]<150?3:5):0)));var items=FullRuneSet(p,alternate,alternate);
            Check(FullRuneReceipts(p)==0 && !EligibleTitles(p).Contains(90001),"four maxima do not qualify, class "+classes[n]+" alternate "+alternate);
            RuneMove(p,items[1],57,alternate?122u:102u);
            if(alternate) {
                Check(FullRuneReceipts(p)==0 && !EligibleTitles(p).Contains(90001),"fifth rune below maximum cannot qualify, class "+classes[n]);
                var stone=RuneItem(p,730001,0);var packet=ComposePacket(items[1].UID,stone.UID);
                Parallel.For(0,8,i=>RunesSystem.HandlePacket1009(p,packet));
                Check(items[1].ID%100==27 && items[1].RunesEXP==0 && !p.Inventory.ContainsUID(stone.UID),"final stone upgrades the equipped rune once, class "+classes[n]);
            }else Parallel.For(0,8,i=>RuneMove(p,items[1],57,102));
            Check(!Fenced(p) && HasAchievement(p,11312+n) && FullRuneReceipts(p)==1 && EligibleTitles(p).Contains(90001),"exact class receipt and immediate Chosen One eligibility, class "+classes[n]+" alternate "+alternate);
            foreach(var item in items)RuneMove(p,item,58);
            p.RunesItem.Clear();ConquerItemTable.LoadItems(p);p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.MyAchievement.Load("");EvidenceRefresh(p);
            Check(HasAchievement(p,11312+n) && FullRuneReceipts(p)==1 && !EligibleTitles(p).Contains(90001),"historical achievement survives unequip and production reload, class "+classes[n]+" alternate "+alternate);
        }
        var split=FullRunePlayer(map,15);var divided=FullRuneSet(split,false,false);RuneMove(split,divided[1],57,122);
        Check(FullRuneReceipts(split)==0,"maximum runes split across sets cannot create a class receipt");
        var unknown=FullRunePlayer(map,100);var unknownSet=FullRuneSet(unknown,false,false);RuneMove(unknown,unknownSet[1],57,102);
        Check(!Fenced(unknown) && FullRuneReceipts(unknown)==0,"unspecialized Taoist cannot inherit Fire or Water achievement");
        var stale=FullRunePlayer(map,15);var staleSet=FullRuneSet(stale,false,false);
        Query("UPDATE entities SET class=25 WHERE uid=@uid","uid",(long)stale.Entity.UID);RuneMove(stale,staleSet[1],57,102);
        Check(Fenced(stale) && staleSet[1].Position==211 && SavedRune(staleSet[1],"position")==211 && FullRuneReceipts(stale)==0,"stale persisted class rolls back fifth equip");
        var session=FullRunePlayer(map,15);var sessionSet=FullRuneSet(session,false,true);RuneMove(session,sessionSet[1],57,102);
        var sessionStone=RuneItem(session,730001,0);var sessionPacket=ComposePacket(sessionSet[1].UID,sessionStone.UID);
        session.Fake=true;RunesSystem.HandlePacket1009(session,sessionPacket);session.Fake=false;
        session.Entity.FullyLoaded=false;RunesSystem.HandlePacket1009(session,sessionPacket);session.Entity.FullyLoaded=true;
        Kernel.GamePool.Remove(session.Entity.UID);RunesSystem.HandlePacket1009(session,sessionPacket);Kernel.GamePool.Add(session.Entity.UID,session);
        Check(!Fenced(session) && FullRuneReceipts(session)==0 && SavedRune(sessionStone,"id")==730001 && SavedRune(sessionSet[1],"id")==4020126,"fake, unloaded and noncanonical upgrades consume nothing");
        Query("UPDATE entities SET class=25 WHERE uid=@uid","uid",(long)session.Entity.UID);FullRuneComposeFailure(session,sessionSet[1],sessionStone);
        Check(Fenced(session) && FullRuneReceipts(session)==0 && SavedRune(sessionStone,"id")==730001 && SavedRune(sessionSet[1],"id")==4020126,"stale profession also rolls back the final stone upgrade");
        Query("UPDATE entities SET class=15 WHERE uid=@uid","uid",(long)session.Entity.UID);DemonReset(session);
        Query("UPDATE items SET runesexp=1 WHERE uid=@uid","uid",(long)sessionSet[2].UID);FullRuneComposeFailure(session,sessionSet[1],sessionStone);
        Check(Fenced(session) && FullRuneReceipts(session)==0 && SavedRune(sessionStone,"id")==730001 && SavedRune(sessionSet[1],"id")==4020126,"another equipped rune's stale preimage rolls back target and materials");
        Query("UPDATE items SET runesexp=0 WHERE uid=@uid","uid",(long)sessionSet[2].UID);DemonReset(session);
        RunesSystem.HandlePacket1009(session,sessionPacket);
        Check(!Fenced(session) && HasAchievement(session,11312),"exact reload preimages permit a successful retry without false replay suppression");
        foreach(bool upgrade in new[]{false,true}) {
            var p=FullRunePlayer(map,15);var items=FullRuneSet(p,false,upgrade);ConquerItem stone=null;
            if(upgrade){RuneMove(p,items[1],57,102);stone=RuneItem(p,730001,0);}
            Query(@"CREATE FUNCTION suppress_full_rune() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.achievement_id=11312 THEN RETURN NULL; END IF; RETURN NEW; END $$;
CREATE TRIGGER suppress_full_rune BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE PROCEDURE suppress_full_rune()");
            if(upgrade)FullRuneComposeFailure(p,items[1],stone);else RuneMove(p,items[1],57,102);
            Check(Fenced(p) && FullRuneReceipts(p)==0 && (upgrade?items[1].ID==4020126 && SavedRune(items[1],"id")==4020126 && SavedRune(stone,"id")==730001 && p.Inventory.ContainsUID(stone.UID):items[1].Position==211 && SavedRune(items[1],"position")==211),"silently suppressed class receipt rolls back item and material changes, upgrade "+upgrade);
            Query("DROP TRIGGER suppress_full_rune ON revora_achievement_evidence;DROP FUNCTION suppress_full_rune()");DemonReset(p);
            if(upgrade)RunesSystem.HandlePacket1009(p,ComposePacket(items[1].UID,stone.UID));else RuneMove(p,items[1],57,102);
            Check(!Fenced(p) && HasAchievement(p,11312) && FullRuneReceipts(p)==1,"retry after receipt recovery commits exactly once, upgrade "+upgrade);
            RuneMove(p,items[1],58);RuneMove(p,items[1],57,102);
            Check(!Fenced(p) && FullRuneReceipts(p)==1,"already-earned receipt permits later legitimate equip, upgrade "+upgrade);
        }
    }
}
