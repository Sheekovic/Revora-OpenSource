using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Interfaces;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static void BookProf(GameState p, ushort id, byte level, bool save = true)
    {
        p.Proficiencies = new SafeDictionary<ushort, IProf>();
        p.AddProficiency(new Proficiency(true) { ID = id, Level = level });
        if (save) SkillTable.SaveProficiencies(p);
    }
    static void VerifyWeaponBooks(Map map)
    {
        Query("CREATE TABLE IF NOT EXISTS profs (LIKE public.profs INCLUDING ALL)");
        uint[] books = {725005,725010,725029,725030,725014,725027,725012,725013,725026,725011,725031,725040,725041,725042,725043,725044};
        ushort[] skills = {1045,1046,5030,5040,1300,5020,1260,1290,5010,1250,5050,7000,7010,7020,7030,7040};
        ushort[] profs = {410,420,420,460,540,580,560,490,561,510,530,430,450,480,481,440};
        for (int i = 0; i < books.Length; i++)
        {
            var p = SkillPlayer(map); BookProf(p, profs[i], 5);
            var book = Pack(p, books[i], (ushort)(i % 2 + 1));
            if (i % 2 == 0) { book.Bound = true; Query("UPDATE items SET bound=1 WHERE uid=@uid", "uid", (long)book.UID); }
            var other = Pack(p, books[i], 1); ushort count = book.StackSize;
            ItemHandler.UseItem(book, p);
            Check(!Fenced(p) && p.Spells.ContainsKey(skills[i]) && p.Spells[skills[i]].Available && p.Spells[skills[i]].Level == 0
                && SkillRows(p) == 1 && Scalar("SELECT level FROM skills WHERE entityid=@uid", p.Entity.UID) == 0
                && (count == 1 ? !p.Inventory.ContainsUID(book.UID) && Scalar("SELECT count(*) FROM items WHERE uid=@uid", book.UID) == 0
                    : book.StackSize == 1 && SavedRune(book,"stacksize") == 1)
                && p.Inventory.ContainsUID(other.UID) && SavedRune(other,"stacksize") == 1 && FixedReceipts(p) == 0,
                "book learns its exact level-zero skill and consumes only the selected owned unit " + books[i]);
            ItemHandler.UseItem(book, p); ItemHandler.UseItem(other, p); SkillTable.SaveSpells(p); SkillTable.LoadSpells(p); ItemHandler.UseItem(other, p);
            Check(!Fenced(p) && SkillRows(p) == 1 && p.Spells[skills[i]].Level == 0 && p.Inventory.ContainsUID(other.UID)
                && SavedRune(other,"stacksize") == 1 && FixedReceipts(p) == 0,
                "replayed use and production skill reload preserve a second book and cannot grant mastery " + books[i]);
        }
        var low = SkillPlayer(map); BookProf(low, 410, 5); low.Entity.Level = 39; var blade = Pack(low,725005,1);
        ItemHandler.UseItem(blade,low);
        Check(!Fenced(low) && SkillRows(low)==0 && low.Inventory.ContainsUID(blade.UID), "Fast Blade level 39 cannot consume its book");
        low.Entity.Level=40; ItemHandler.UseItem(blade,low);
        Check(!Fenced(low) && low.Spells.ContainsKey(1045), "Fast Blade learns at level 40 with saved proficiency five");
        var phoenix=SkillPlayer(map); phoenix.Entity.Level=29; var fire=Pack(phoenix,725029,1); ItemHandler.UseItem(fire,phoenix);
        Check(!Fenced(phoenix) && SkillRows(phoenix)==0 && phoenix.Inventory.ContainsUID(fire.UID), "Phoenix cannot be learned below the published level thirty requirement");
        phoenix.Entity.Level=30; ItemHandler.UseItem(fire,phoenix);
        Check(!Fenced(phoenix) && phoenix.Spells.ContainsKey(5030), "Phoenix level thirty does not invent a weapon proficiency prerequisite");
        var required=SkillPlayer(map); BookProf(required,560,3); var spear=Pack(required,725012,1); ItemHandler.UseItem(spear,required);
        Check(!Fenced(required) && SkillRows(required)==0 && required.Inventory.ContainsUID(spear.UID), "passive spear skill requires proficiency four");
        required.Proficiencies[560].Level=4; ItemHandler.UseItem(spear,required);
        Check(!Fenced(required) && SkillRows(required)==0 && required.Inventory.ContainsUID(spear.UID), "unsaved proficiency cannot establish paid learning eligibility");
        SkillTable.SaveProficiencies(required); ItemHandler.UseItem(spear,required);
        Check(!Fenced(required) && required.Spells.ContainsKey(1260), "saving the earned proficiency enables book learning without extra payment");
        var wrong=SkillPlayer(map); BookProf(wrong,420,20); var wrongBook=Pack(wrong,725012,1); ItemHandler.UseItem(wrongBook,wrong);
        Check(!Fenced(wrong) && SkillRows(wrong)==0 && wrong.Inventory.ContainsUID(wrongBook.UID), "unrelated high weapon proficiency cannot learn Speed Gun");
        var epic=SkillPlayer(map); BookProf(epic,614,5); var epicBook=Pack(epic,725005,1); ItemHandler.UseItem(epicBook,epic);
        Check(!Fenced(epic) && epic.Spells.ContainsKey(1045), "installed 6711 epic weapon proficiency alternative can learn Fast Blade");
        var rollback=SkillPlayer(map); var kept=Pack(rollback,725029,2);
        Query("ALTER TABLE skills ADD CONSTRAINT fail_book_skill CHECK(id<>5030) NOT VALID"); ItemHandler.UseItem(kept,rollback);
        Check(Fenced(rollback) && SkillRows(rollback)==0 && !rollback.Spells.ContainsKey(5030) && kept.StackSize==2 && SavedRune(kept,"stacksize")==2,
            "failed skill insert preserves the book and suppresses learned cache state");
        Query("ALTER TABLE skills DROP CONSTRAINT fail_book_skill"); DemonReset(rollback); SkillTable.LoadSpells(rollback); ItemHandler.UseItem(kept,rollback);
        Check(!Fenced(rollback) && SkillRows(rollback)==1 && kept.StackSize==1, "recovery retries an uncommitted book use exactly once");
        var stale=SkillPlayer(map); var moved=Pack(stale,725029,1); Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)moved.UID);
        ItemHandler.UseItem(moved,stale);
        Check(Fenced(stale) && SkillRows(stale)==0 && !stale.Spells.ContainsKey(5030) && SavedRune(moved,"warehouse")==8,
            "changed durable item location rolls back the earlier skill insert");
        var existing=SkillPlayer(map); existing.AddSpell(new Spell(true){ID=5030,Level=5}); SkillTable.SaveSpells(existing);
        existing.Spells.Clear(); var duplicate=Pack(existing,725029,1); ItemHandler.UseItem(duplicate,existing);
        Check(Fenced(existing) && Scalar("SELECT level FROM skills WHERE entityid=@uid",existing.Entity.UID)==5 && existing.Inventory.ContainsUID(duplicate.UID),
            "missing cache cannot overwrite an existing durable skill or charge another book");
        var concurrent=SkillPlayer(map); var first=Pack(concurrent,725029,2); var second=Pack(concurrent,725029,1);
        Parallel.Invoke(()=>ItemHandler.UseItem(first,concurrent),()=>ItemHandler.UseItem(second,concurrent),()=>SkillTable.SaveSpells(concurrent));
        Check(!Fenced(concurrent) && SkillRows(concurrent)==1 && Scalar("SELECT SUM(stacksize) FROM items WHERE entityid=@uid",concurrent.Entity.UID)==2,
            "concurrent book use and skill saving learn once and consume one unit");
        var guarded=SkillPlayer(map); var untouched=Pack(guarded,725029,1);
        guarded.Trade.InTrade=true; ItemHandler.UseItem(untouched,guarded); guarded.Trade.InTrade=false;
        untouched.Lock=1; ItemHandler.UseItem(untouched,guarded); untouched.Lock=0;
        guarded.Entity.FullyLoaded=false; ItemHandler.UseItem(untouched,guarded); guarded.Entity.FullyLoaded=true;
        guarded.Fake=true; ItemHandler.UseItem(untouched,guarded); guarded.Fake=false;
        untouched.Days=1; untouched.DayStamp=DateTime.Now.AddDays(-2); ItemHandler.UseItem(untouched,guarded); untouched.Days=0;
        var impostor=new ConquerItem(true){UID=untouched.UID,ID=untouched.ID,StackSize=1}; ItemHandler.UseItem(impostor,guarded);
        Check(!Fenced(guarded) && SkillRows(guarded)==0 && guarded.Inventory.ContainsUID(untouched.UID),
            "trade, lock, incomplete login, fake actor, expiry and substituted item references cannot spend a book");
        ItemHandler.UseItem(untouched,guarded);
        Check(!Fenced(guarded) && guarded.Spells.ContainsKey(5030), "eligible owner can use the preserved book after temporary restrictions end");
        var timed=SkillPlayer(map); var timedBook=Pack(timed,725029,1); timedBook.Days=1; timedBook.DayStamp=DateTime.Now.AddHours(-1);
        Query("UPDATE items SET days='1',daystamp=@stamp WHERE uid=@uid","stamp",timedBook.DayStamp.ToBinary(),"uid",(long)timedBook.UID);
        ItemHandler.UseItem(timedBook,timed);
        Check(!Fenced(timed) && timed.Spells.ContainsKey(5030) && !timed.Inventory.ContainsUID(timedBook.UID),
            "unexpired timed book consumes only with its matching durable expiry metadata");
        var changedExpiry=SkillPlayer(map); var staleExpiry=Pack(changedExpiry,725029,1);
        Query("UPDATE items SET days='1',daystamp=@stamp WHERE uid=@uid","stamp",DateTime.Now.AddDays(-2).ToBinary(),"uid",(long)staleExpiry.UID);
        ItemHandler.UseItem(staleExpiry,changedExpiry);
        Check(Fenced(changedExpiry) && SkillRows(changedExpiry)==0 && changedExpiry.Inventory.ContainsUID(staleExpiry.UID),
            "stale permanent cache cannot spend an expired durable timed book");
        var legacy=SkillPlayer(map); var single=Pack(legacy,725029,0); ItemHandler.UseItem(single,legacy);
        Check(!Fenced(legacy) && legacy.Spells.ContainsKey(5030) && !legacy.Inventory.ContainsUID(single.UID),
            "legacy zero-stack single book convention remains usable exactly once");
        var full=SkillPlayer(map); var fullBook=Pack(full,725029,1);
        while(full.Inventory.Count<40)Pack(full,1000000,1);
        ItemHandler.UseItem(fullBook,full);
        Check(!Fenced(full) && full.Spells.ContainsKey(5030) && full.Inventory.Count==39,
            "learning needs no free inventory slot and consumes its book from a full inventory");
        var staleLevel=SkillPlayer(map); var levelBook=Pack(staleLevel,725029,1);
        Query("UPDATE entities SET level=29 WHERE uid=@uid","uid",(long)staleLevel.Entity.UID); ItemHandler.UseItem(levelBook,staleLevel);
        Check(!Fenced(staleLevel) && SkillRows(staleLevel)==0 && staleLevel.Inventory.ContainsUID(levelBook.UID),
            "stale high cached level cannot bypass the durable learning requirement");
        low.Entity.Level=70;
        for(int i=0;i<4;i++) low.IncreaseSpellExperience(uint.MaxValue,1045);
        Check(!Fenced(low) && low.Spells[1045].Level==4 && HasAchievement(low,10504) && FixedReceipts(low)==1,
            "ordinary book acquisition and production training reach durable Fast Blade mastery");
    }
}
