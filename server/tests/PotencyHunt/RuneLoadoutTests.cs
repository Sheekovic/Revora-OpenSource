using System;
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
    static readonly Type Loadout=typeof(Entity).Assembly.GetType("Revora.Game.Features.RuneLoadout");
    static GameState LoadoutPlayer(Map map) {
        var p=MoonPlayer(map);p.Entity.FullyLoaded=false;p.Entity.Reborn=2;p.Entity.Vitality=100;p.Entity.Strength=100;p.Entity.FullyLoaded=true;return p;
    }
    static byte[] RuneMove(uint uid,ushort action,uint position=0) {
        var packet=new byte[112];Writer.WriteUInt16(104,0,packet);Writer.WriteUInt16(1009,2,packet);
        Writer.WriteUInt32(uid,8,packet);Writer.WriteUInt32(position,12,packet);Writer.WriteUInt16(action,20,packet);return packet;
    }
    static void RuneMove(GameState p,ConquerItem item,ushort action,uint position=0){RunesSystem.HandlePacket1009(p,RuneMove(item.UID,action,position));}
    static void VerifyRuneLoadout(Map map) {
        Query("ALTER TABLE entities ADD COLUMN level integer DEFAULT 140,ADD COLUMN my_title integer DEFAULT 0,ADD COLUMN class integer DEFAULT 15,ADD COLUMN reborn integer DEFAULT 2");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);ConquerItem.ItemUID=new Counter(7800000);
        var now=new DateTime(2026,9,16,1,0,0,DateTimeKind.Utc);Loadout.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        var p=LoadoutPlayer(map);var red=RuneItem(p,4010001,0);var blue=RuneItem(p,4020101);
        Check(p.Account.State==AccountTable.AccountState.Player && p.Entity.FullyLoaded,"loadout fixtures use a loaded ordinary player");
        RuneMove(p,red,57,101);Check(red.Position==0 && !HasAchievement(p,11310),"inventory rune cannot bypass rune-bag transfer");
        Parallel.For(0,12,i=>RuneMove(p,red,61));
        Check(red.Position==211 && p.Inventory.Count==0 && p.RunesItem.ContainsKey(red.UID) && SavedRune(red,"position")==211,"concurrent bag toggle commits once without immediate reversal");
        RuneMove(p,red,61);Check(red.Position==211,"duplicate transfer remains suppressed within one second");
        now=now.AddSeconds(1);RuneMove(p,red,61);Check(red.Position==0 && p.Inventory.Count==1 && !p.RunesItem.ContainsKey(red.UID),"deliberate reverse transfer works at the exact cooldown boundary");
        now=now.AddSeconds(1);RuneMove(p,red,61);
        foreach(uint position in new uint[]{0,1,102,103,108,122,125,211,357,uint.MaxValue})RuneMove(p,red,57,position);
        foreach(ushort action in new ushort[]{313,314,317,65535})RuneMove(p,red,action,101);
        var broken=RuneMove(red.UID,57,101);broken[0]--;RunesSystem.HandlePacket1009(p,broken);
        broken=new byte[129];Writer.WriteUInt16(121,0,broken);Writer.WriteUInt16(1009,2,broken);Writer.WriteUInt32(red.UID,8,broken);Writer.WriteUInt32(101,12,broken);Writer.WriteUInt16(57,20,broken);RunesSystem.HandlePacket1009(p,broken);
        Check(red.Position==211 && !HasAchievement(p,11310),"invalid color slots, high action bits, lengths and oversized packets cannot equip or grant evidence");
        p.Fake=true;RuneMove(p,red,57,101);p.Fake=false;p.Entity.FullyLoaded=false;RuneMove(p,red,57,101);p.Entity.FullyLoaded=true;
        Kernel.GamePool.Remove(p.Entity.UID);RuneMove(p,red,57,101);Kernel.GamePool.Add(p.Entity.UID,p);
        Check(red.Position==211,"fake, partially loaded and noncanonical sessions cannot equip");
        Parallel.For(0,12,i=>RuneMove(p,red,57,101));
        Check(!Fenced(p) && red.Position==101 && SavedRune(red,"position")==101 && HasAchievement(p,11310) && !HasAchievement(p,11311),"first genuine equip commits one-rune achievement with its position");
        uint subtype=ConquerItemInformation.BaseInformations[red.ID].subtype;
        Check(p.Spells.ContainsKey((ushort)(subtype/100)) && PrestigeRanking.Update(p).Abilities.Abilities.Any(a=>(int)a.Sort==21 && a.Value==3000),"equip immediately projects rune skills and prestige");
        RuneMove(p,blue,57,102);
        var yellows=new[]{RuneItem(p,4030301),RuneItem(p,4030201),RuneItem(p,4030101)};
        RuneMove(p,yellows[0],57,103);RuneMove(p,yellows[1],57,104);
        Check(!HasAchievement(p,11311),"four actual slots remain below the five-rune threshold");
        Query("ALTER TABLE revora_achievement_evidence ADD CONSTRAINT loadout_failure CHECK(achievement_id<>11311) NOT VALID");
        RuneMove(p,yellows[2],57,105);
        Check(Fenced(p) && yellows[2].Position==211 && SavedRune(yellows[2],"position")==211 && !HasAchievement(p,11311),"late achievement failure rolls back the fifth equip and keeps cache unchanged");
        Query("ALTER TABLE revora_achievement_evidence DROP CONSTRAINT loadout_failure");DemonReset(p);RuneMove(p,yellows[2],57,105);
        Check(!Fenced(p) && HasAchievement(p,11311) && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND source='rune-loadout-v1'",p.Entity.UID)==2,"successful retry persists exactly the two generic rune achievements");
        var free=RuneItem(p,4031201);RuneMove(p,free,57,105);
        for(int repeat=0;repeat<3;repeat++)p.LoadItemStats();
        Check(!Fenced(p) && p.Entity.DrainingTouch==1 && p.Entity.BloodSpawn==1 && p.Entity.FreeSoul==1 && p.Entity.Perfection==null,"yellow boosters recalculate once without requiring refined equipment");
        var plain=LoadoutPlayer(map);var baseline=new PerfectionManager.RefineEffect{Id=1,DrainingTouch=3,BloodSpawn=4,FreeSoul=2,PhysicalAttack=17};
        PerfectionManager.ItemsRefineEffects[1]=baseline;
        foreach(var owner in new[]{p,plain})owner.Equipment.Objects[0]=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=130009,Position=1,PerfectionLevel=1};
        var boosted=p.Entity.Perfection;var ordinary=plain.Entity.Perfection;
        Check(boosted.DrainingTouch==4 && boosted.BloodSpawn==5 && boosted.FreeSoul==3 && boosted.PhysicalAttack==17
            && ordinary.DrainingTouch==3 && ordinary.BloodSpawn==4 && ordinary.FreeSoul==2
            && baseline.DrainingTouch==3 && baseline.BloodSpawn==4 && baseline.FreeSoul==2,"rune boosts belong to one character and never modify another player's shared Perfection level");
        Check(!ReferenceEquals(boosted,baseline) && p.Entity.Perfection.DrainingTouch==4,"repeated Perfection reads cannot accumulate rune bonuses");
        foreach(var owner in new[]{p,plain})Array.Clear(owner.Equipment.Objects,0,owner.Equipment.Objects.Length);
        RuneMove(p,free,58);Check(p.Entity.FreeSoul==0,"unequipping a yellow booster immediately clears its derived bonus");
        var replacement=RuneItem(p,4030401);Query("ALTER TABLE items ADD CONSTRAINT rune_swap_failure CHECK(id<>4030401 OR position<>103) NOT VALID");
        RuneMove(p,replacement,57,103);
        Check(Fenced(p) && replacement.Position==211 && yellows[0].Position==103 && SavedRune(replacement,"position")==211 && SavedRune(yellows[0],"position")==103,"failed swap restores both incoming and displaced rune positions");
        Query("ALTER TABLE items DROP CONSTRAINT rune_swap_failure");DemonReset(p);RuneMove(p,replacement,57,103);
        Check(replacement.Position==103 && yellows[0].Position==211 && SavedRune(yellows[0],"position")==211,"successful swap atomically returns the displaced rune to its bag");
        subtype=ConquerItemInformation.BaseInformations[yellows[0].ID].subtype;
        Check(!p.Spells.ContainsKey((ushort)(subtype/100)),"displaced rune skill is removed from the live loadout");
        RuneMove(p,replacement,58);RuneMove(p,replacement,58);Check(replacement.Position==211 && SavedRune(replacement,"position")==211,"unequip persists and duplicate unequip is idempotent");
        RuneMove(p,red,61);Check(red.Position==101,"equipped rune cannot transfer directly to inventory");
        var full=LoadoutPlayer(map);var held=RuneItem(full,4020101);for(int i=0;i<40;i++)Pack(full,1088001,1);RuneMove(full,held,61);
        Check(held.Position==211 && SavedRune(held,"position")==211 && full.Inventory.Count==40,"full inventory preserves the rune bag and storage");
        var slot=full.Inventory.Objects[0];Query("DELETE FROM items WHERE uid=@uid","uid",(long)slot.UID);full.Inventory.Remove(slot.UID,Enums.ItemUse.None,false);RuneMove(full,held,61);
        Check(held.Position==0 && full.Inventory.Count==40 && SavedRune(held,"position")==0,"freeing one place permits exact rune withdrawal");
        now=now.AddSeconds(1);Query("ALTER TABLE items ADD CONSTRAINT rune_bag_failure CHECK(position<>211) NOT VALID");RuneMove(full,held,61);
        Check(Fenced(full) && held.Position==0 && full.Inventory.Count==40 && SavedRune(held,"position")==0,"failed bag deposit preserves inventory membership and the persisted position");
        Query("ALTER TABLE items DROP CONSTRAINT rune_bag_failure");DemonReset(full);RuneMove(full,held,61);now=now.AddSeconds(1);
        Query("ALTER TABLE items ADD CONSTRAINT rune_bag_failure CHECK(position<>0) NOT VALID");RuneMove(full,held,61);
        Check(Fenced(full) && held.Position==211 && full.RunesItem.ContainsKey(held.UID) && SavedRune(held,"position")==211,"failed bag withdrawal preserves rune membership and the persisted position");
        Query("ALTER TABLE items DROP CONSTRAINT rune_bag_failure");DemonReset(full);RuneMove(full,held,61);Check(held.Position==0,"failed transfers can retry after storage recovery without false duplicate suppression");
        var foreign=RuneItem(full,4030101);RuneMove(p,foreign,57,103);Check(foreign.Position==211,"foreign rune UID is rejected");
        var stale=RuneItem(p,4030501);Query("UPDATE items SET entityid=@owner WHERE uid=@uid","owner",(long)full.Entity.UID,"uid",(long)stale.UID);RuneMove(p,stale,57,103);
        Check(Fenced(p) && stale.Position==211 && SavedRune(stale,"position")==211,"stale cached ownership cannot equip a transferred rune");DemonReset(p);p.RunesItem.Remove(stale.UID);
        var mismatch=RuneItem(p,4030501);Query("UPDATE items SET runesexp=1 WHERE uid=@uid","uid",(long)mismatch.UID);RuneMove(p,mismatch,57,103);
        Check(Fenced(p) && mismatch.Position==211 && SavedRune(mismatch,"position")==211,"changed persisted rune progress is rejected without cache mutation");DemonReset(p);mismatch.RunesEXP=1;
        RuneMove(p,mismatch,57,103);Check(mismatch.Position==103,"reloaded exact preimage can be equipped");
        var divided=LoadoutPlayer(map);var dividedItems=new[]{RuneItem(divided,4010001),RuneItem(divided,4020101),RuneItem(divided,4030101),RuneItem(divided,4030201),RuneItem(divided,4030301)};
        byte[] mixed={101,102,123,124,125};for(int i=0;i<5;i++)RuneMove(divided,dividedItems[i],57,mixed[i]);
        Check(HasAchievement(divided,11310) && !HasAchievement(divided,11311),"partial primary and alternate sets do not fabricate five equipped runes in one loadout");
        RuneMove(divided,dividedItems[0],57,121);RuneMove(divided,dividedItems[1],57,122);
        Check(HasAchievement(divided,11311),"five valid alternate slots qualify independently");
        foreach(var i in dividedItems)RuneMove(divided,i,58);
        divided.RunesItem.Clear();ConquerItemTable.LoadItems(divided);Call(typeof(Entity).Assembly.GetType("Revora.Network.GamePackets.RuneProgress"),null,"RefreshSkills",divided);
        divided.Entity.MyAchievement=new Achievement(divided.Entity);divided.Entity.MyAchievement.Load("");EvidenceRefresh(divided);
        Check(divided.RunesItem.Count==5 && divided.RunesItem.Values.All(i=>i.Position==211) && HasAchievement(divided,11311),"production loader restores bag positions and historical achievements survive unequip and reload");
        p.RunesItem.Clear();foreach(var i in p.Inventory.Objects)p.Inventory.Remove(i.UID,Enums.ItemUse.None,false);ConquerItemTable.LoadItems(p);p.LoadItemStats();
        Check(p.RunesItem[red.UID].Position==101 && p.RunesItem[mismatch.UID].Position==103 && !Fenced(p),"production item reload restores the committed primary loadout");
        var duplicate=LoadoutPlayer(map);RuneItem(duplicate,4030101,103);RuneItem(duplicate,4030201,103);duplicate.RunesItem.Clear();bool rejected=false;
        try{ConquerItemTable.LoadItems(duplicate);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && Fenced(duplicate) && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=103",duplicate.Entity.UID)==2,"corrupt duplicate saved slots fail closed without silently rewriting either item");
        VerifyFullRuneAchievements(map);
        VerifyRuneAdmission(map);
    }
}
