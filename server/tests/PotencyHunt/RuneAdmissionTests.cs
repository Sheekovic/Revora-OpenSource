using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static int RuneScore(GameState p) {return PrestigeRanking.Update(p).Abilities.Abilities.Where(a=>(int)a.Sort>=21).Sum(a=>a.Value);}
    static void VerifyRuneAdmission(Map map) {
        byte[] professions={10,20,40,50,60,70,80,132,142,160};
        for(int n=0;n<professions.Length;n++) {
            var p=FullRunePlayer(map,professions[n]);var red=RuneItem(p,(uint)(4010001+100*n));var blue=RuneItem(p,(uint)(4020101+100*n));
            var wrong=RuneItem(p,(uint)(4010001+100*((n+1)%10)));RuneMove(p,wrong,57,101);
            Check(wrong.Position==211 && !Fenced(p),"wrong-class red rune rejected for profession "+professions[n]);
            var wrongBlue=RuneItem(p,(uint)(4020101+100*((n+1)%10)));RuneMove(p,wrongBlue,57,102);
            Check(wrongBlue.Position==211 && !Fenced(p),"wrong-class blue rune rejected for profession "+professions[n]);
            RuneMove(p,red,57,101);RuneMove(p,blue,57,102);
            Check(red.Position==101 && blue.Position==102 && !Fenced(p),"installed minimum class accepts its red/blue pair "+professions[n]);
        }
        foreach(byte profession in new byte[]{130,131,140,141}) {
            var p=FullRunePlayer(map,profession);uint red=profession<140?4010701u:4010801u;
            var r=RuneItem(p,red);RuneMove(p,r,57,101);
            Check(r.Position==211 && !Fenced(p),"Taoist rune observes installed promotion minimum "+profession);
        }
        var locked=FullRunePlayer(map,15);var candidate=RuneItem(locked,4020101);var stone=RuneItem(locked,730001,0);
        locked.Entity.Reborn=1;RuneMove(locked,candidate,57,102);RunesSystem.HandlePacket1009(locked,ComposePacket(candidate.UID,stone.UID));
        Check(candidate.Position==211 && candidate.RunesEXP==0 && SavedRune(stone,"id")==730001,"first rebirth cannot equip or compose runes");
        locked.Entity.Reborn=2;locked.Entity.Level=14;RuneMove(locked,candidate,57,102);RunesSystem.HandlePacket1009(locked,ComposePacket(candidate.UID,stone.UID));
        Check(candidate.Position==211 && candidate.RunesEXP==0 && SavedRune(stone,"id")==730001,"second rebirth below level fifteen cannot equip or compose");
        locked.Entity.Level=15;RuneMove(locked,candidate,57,102);RunesSystem.HandlePacket1009(locked,ComposePacket(candidate.UID,stone.UID));
        Check(candidate.Position==102 && candidate.RunesEXP==10 && !Fenced(locked),"exact second-rebirth level-fifteen boundary permits both actions");
        var p2=FullRunePlayer(map,15);var a=RuneItem(p2,4030201);var b=RuneItem(p2,4030209);
        RuneMove(p2,a,57,103);RuneMove(p2,b,57,104);
        Check(a.Position==103 && b.Position==211 && p2.Entity.DrainingTouch==1,"same yellow type at different levels cannot fill two slots");
        RuneMove(p2,b,57,103);
        Check(a.Position==211 && b.Position==103 && p2.Entity.DrainingTouch==9,"same-type upgrade replacement in the same slot remains legal");
        RuneMove(p2,a,57,123);
        Check(a.Position==123 && p2.Entity.DrainingTouch==9,"same yellow type can occupy the other set without overriding active effects");
        bool switched=GeneralData.SwitchEquipment(p2,true);
        Check(switched && p2.Entity.DrainingTouch==1,"actual equipment switch installs only alternate rune effects: switched="+switched+" bonus="+p2.Entity.DrainingTouch+" alternate="+p2.AlternateEquipment);
        GeneralData.SwitchEquipment(p2,false);Check(p2.Entity.DrainingTouch==9,"switching back restores the primary level without stacking");
        var extra=RuneItem(p2,4030309);foreach(uint pos in new uint[]{106,107,108})RuneMove(p2,extra,57,pos);
        Check(extra.Position==211 && !Fenced(p2),"unsupported extra primary slots reject new equipment");
        var old=FullRunePlayer(map,15);var legacy=RuneItem(old,4030209,106);old.LoadItemStats();
        Check(old.Entity.DrainingTouch==0 && RuneScore(old)==0,"old extra-slot rune grants neither passive effect nor prestige");
        RuneMove(old,legacy,58);
        Check(legacy.Position==211 && SavedRune(legacy,"position")==211 && !Fenced(old),"old extra-slot rune remains recoverable by ordinary unequip");
        var duplicated=FullRunePlayer(map,15);var d1=RuneItem(duplicated,4030209,103);var d2=RuneItem(duplicated,4030201,104);
        duplicated.RunesItem.Clear();ConquerItemTable.LoadItems(duplicated);duplicated.LoadItemStats();
        Check(!Fenced(duplicated) && duplicated.Entity.DrainingTouch==0 && RuneScore(duplicated)==0,"loaded duplicate yellow types are retained but both effects and scores are excluded");
        RuneMove(duplicated,duplicated.RunesItem[d2.UID],58);
        Check(duplicated.Entity.DrainingTouch==9 && RuneScore(duplicated)==11000 && SavedRune(d1,"position")==103,"unequipping one duplicate restores the unambiguous remaining rune");
        var forged=FullRunePlayer(map,15);RuneItem(forged,4010001,101);RuneItem(forged,4020127,102);
        RuneItem(forged,4030209,103);RuneItem(forged,4030209,104);var fifth=RuneItem(forged,4030309);RuneMove(forged,fifth,57,105);
        Check(!Fenced(forged) && fifth.Position==105 && !HasAchievement(forged,11311) && FullRuneReceipts(forged)==0
            && !EligibleTitles(forged).Contains(90001),"five maximum items with repeated yellow types cannot mint five-rune or class receipts or Chosen One");
        var profile=FullRunePlayer(map,15);var full=FullRuneSet(profile,false,false);RuneMove(profile,full[1],57,102);
        Check(EligibleTitles(profile).Contains(90001) && OwnedTitles(profile).ContainsKey(90001) && HasAchievement(profile,11312),"legal full loadout earns title eligibility and the historical class receipt");
        ushort redSkill=(ushort)(ConquerItemInformation.BaseInformations[full[0].ID].subtype/100);
        profile.Entity.Class=25;
        Check(!profile.Spells.ContainsKey(redSkill) && !EligibleTitles(profile).Contains(90001) && !OwnedTitles(profile).ContainsKey(90001) && HasAchievement(profile,11312)
            && profile.Entity.DrainingTouch==9,"persisted class change removes incompatible skills and title while preserving universal runes and earned history");
        profile.Entity.Reborn=1;
        Check(profile.Entity.DrainingTouch==0 && RuneScore(profile)==0,"losing unlock eligibility immediately clears all rune effects and prestige");
        profile.Entity.Reborn=2;profile.Entity.Class=15;
        Check(profile.Entity.DrainingTouch==9 && EligibleTitles(profile).Contains(90001) && OwnedTitles(profile).ContainsKey(90001) && profile.Spells.ContainsKey(redSkill),"restoring a legal profile reinstates retained owned equipment without a new item grant");
        profile.Entity.Level=14;Check(profile.Entity.DrainingTouch==0 && !EligibleTitles(profile).Contains(90001) && !OwnedTitles(profile).ContainsKey(90001),"level decrease also invalidates effects and title immediately");
        foreach(var item in full)RuneMove(profile,item,58);
        Check(!Fenced(profile) && full.All(i=>i.Position==211),"locked character can recover every rune through unequip");
        var stale=FullRunePlayer(map,15);var entry=RuneItem(stale,4010001);
        Query("UPDATE entities SET reborn=1 WHERE uid=@uid","uid",(long)stale.Entity.UID);RuneMove(stale,entry,57,101);
        Check(Fenced(stale) && entry.Position==211 && SavedRune(entry,"position")==211 && !HasAchievement(stale,11310),"stale saved unlock profile rolls back equip and generic achievement");
        var staleBag=FullRunePlayer(map,15);var bagRune=RuneItem(staleBag,4020101);var bagStone=RuneItem(staleBag,730001,0);
        Query("UPDATE entities SET reborn=1 WHERE uid=@uid","uid",(long)staleBag.Entity.UID);FullRuneComposeFailure(staleBag,bagRune,bagStone);
        Check(Fenced(staleBag) && bagRune.RunesEXP==0 && SavedRune(bagRune,"runesexp")==0 && SavedRune(bagStone,"id")==730001,
            "stale saved unlock also prevents composition in the rune bag without consuming a stone");
    }
}
