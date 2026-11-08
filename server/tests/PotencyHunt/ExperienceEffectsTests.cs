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
    static long SkillRows(GameState p)
    { return Scalar("SELECT count(*) FROM skills WHERE entityid=@uid", p.Entity.UID); }
    static GameState ArenaEntryPlayer(Map map, bool existing)
    {
        var p = StoredProgressionPlayer(map); p.Entity.Level = 69; p.Entity.Experience = DataHolder.LevelExperience(69) - 1;
        p.ArenaStatistic = new QualifierStatistic(true) { EntityID = existing ? p.Entity.UID : 0,
            Level = 69, Name = p.Entity.Name, Class = p.Entity.Class, ArenaPoints = 0,
            CurrentHonor = existing ? 77u : 0, TotalWin = existing ? 12u : 0 };
        if (existing) Query("INSERT INTO arena(entityid,entityname,level,arenapoints,currenthonor,totalwin) VALUES(@uid,@name,15,0,77,12)",
            "uid", (long)p.Entity.UID, "name", p.Entity.Name);
        if (!EntityTable.SaveEntity(p)) throw new Exception("Arena entry fixture save failed.");
        return p;
    }
    static GameState HonorBuyer(Map map,uint honor) {
        var p=ArenaEntryPlayer(map,true);
        p.TeamArenaStatistic=new TeamQualifierStatistic(true){EntityID=p.Entity.UID,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class};
        Query("INSERT INTO teamarena(entityid,entityname,level,currenthonor) VALUES(@uid,@name,69,77)","uid",(long)p.Entity.UID,"name",p.Entity.Name);
        p.CurrentHonor=honor;return p;
    }
    static void HonorBuy(GameState p,uint item=720842) {
        ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=HonorShop.UID,dwParam=item},p);
    }
    static void VerifyHonorPurchases(Map map) {
        Query("CREATE TABLE IF NOT EXISTS teamarena (LIKE public.teamarena INCLUDING ALL);CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.HonorPurchases");
        Call(service,null,"Initialize");Call(service,null,"Initialize");
        var now=DateTime.UtcNow;service.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        HonorShop.Load();uint price=HonorShop.Shop.Items[720842];
        Check(price==8000,"Sword Soul uses the real loaded 8000-Honor price");
        var p=HonorBuyer(map,price*2);
        Parallel.Invoke(()=>Parallel.For(0,8,n=>HonorBuy(p)),()=>{for(int n=0;n<8;n++){ArenaTable.SaveArenaStatistics(p.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(p.TeamArenaStatistic);}});
        Check(!Fenced(p) && p.CurrentHonor==price && p.Inventory.Objects.Count(i=>i.ID==720842)==1
            && Scalar("SELECT count(*) FROM revora_honor_purchases WHERE uid=@uid",p.Entity.UID)==1,
            "rapid duplicate Honor requests commit one item, one receipt and one charge");
        now=now.AddSeconds(2);HonorBuy(p);
        Check(!Fenced(p) && p.CurrentHonor==0 && p.TeamArenaStatistic.CurrentHonor==0
            && p.Inventory.Objects.Count(i=>i.ID==720842 && !i.Bound)==2
            && Scalar("SELECT currenthonor FROM arena WHERE entityid=@uid",p.Entity.UID)==0
            && Scalar("SELECT currenthonor FROM teamarena WHERE entityid=@uid",p.Entity.UID)==0,
            "concurrent Honor purchases and both Arena saves cannot overspend or restore deducted points");
        var item=p.Inventory.Objects.First(i=>i.ID==720842);
        var reloaded=(QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",p.Entity.UID);
        Check(reloaded.CurrentHonor==0 && ConquerItemTable.LoadItem(item.UID).ID==720842 && !ConquerItemTable.LoadItem(item.UID).Bound,
            "purchased Sword Soul and spent Honor survive production recovery readers");
        TeamArenaTable.SaveArenaStatistics(new TeamQualifierStatistic(true){EntityID=p.Entity.UID,CurrentHonor=999999});
        Check(Scalar("SELECT currenthonor FROM teamarena WHERE entityid=@uid",p.Entity.UID)==0,
            "detached Team Arena snapshot cannot overwrite the canonical buyer's balance");
        foreach(string table in new[]{"items","arena","teamarena","revora_honor_purchases"}) {
            var failed=HonorBuyer(map,price);
            // Suppress only the debit, leaving the legitimate pre-purchase save intact.
            string body=table=="items" || table=="revora_honor_purchases"?"RETURN NULL;":"IF NEW.currenthonor=0 THEN RETURN NULL; END IF; RETURN NEW;";
            string verb=table=="items" || table=="revora_honor_purchases"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION suppress_honor_purchase() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+body+" END';CREATE TRIGGER suppress_honor_purchase BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_honor_purchase()");
            try{HonorBuy(failed);}finally{Query("DROP TRIGGER suppress_honor_purchase ON "+table+";DROP FUNCTION suppress_honor_purchase()");}
            Check(Fenced(failed) && failed.Inventory.Count==0 && failed.CurrentHonor==price
                && Scalar("SELECT currenthonor FROM arena WHERE entityid=@uid",failed.Entity.UID)==price
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=720842",failed.Entity.UID)==0,
                "failed Honor purchase preserves earned funds and rolls back item/debits: "+table);
            TeamArenaTable.SaveArenaStatistics(failed.TeamArenaStatistic);
            DemonReset(failed);failed.ArenaStatistic=(QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",failed.Entity.UID);
            failed.CurrentHonor=failed.ArenaStatistic.CurrentHonor;HonorBuy(failed);
            Check(!Fenced(failed) && failed.CurrentHonor==0 && failed.Inventory.Objects.Count(i=>i.ID==720842)==1,
                "recovered owner can buy once after failed Honor transaction: "+table);
        }
        var full=HonorBuyer(map,price);while(full.Inventory.Count<40)Pack(full,1088001,1);HonorBuy(full);
        Check(!Fenced(full) && full.CurrentHonor==price && full.Inventory.Count==40,"full bag leaves Honor untouched");
        var stale=HonorBuyer(map,price);var extra=Pack(stale,1088001,1);stale.Inventory.Remove(extra.UID,Revora.Game.Enums.ItemUse.None,false);HonorBuy(stale);
        Check(Fenced(stale) && Scalar("SELECT count(*) FROM revora_honor_purchases WHERE uid=@uid",stale.Entity.UID)==0
            && Scalar("SELECT currenthonor FROM arena WHERE entityid=@uid",stale.Entity.UID)==price,
            "stale bag capacity cannot authorize an Honor purchase or lose earned funds");
        var unknown=HonorBuyer(map,price);HonorBuy(unknown,0);
        Check(!Fenced(unknown) && unknown.CurrentHonor==price && unknown.Inventory.Count==0,"unlisted Honor item cannot spend or grant");
        service.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));
    }
    static void VerifyExperienceEffects(Map map)
    {
        VerifyHonorPurchases(map);
        var entry = ArenaEntryPlayer(map, false);
        Query(@"CREATE FUNCTION reject_arena_entry() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
RAISE EXCEPTION 'fixture arena failure'; END $$;
CREATE TRIGGER reject_arena_entry BEFORE INSERT ON arena FOR EACH ROW EXECUTE FUNCTION reject_arena_entry()");
        bool rejected = false;
        try { entry.IncreaseExperience(1, false); } catch (Npgsql.PostgresException) { rejected = true; }
        finally { Query("DROP TRIGGER reject_arena_entry ON arena; DROP FUNCTION reject_arena_entry()"); }
        Check(rejected && Fenced(entry) && entry.Entity.Level == 69 && entry.Entity.Experience == DataHolder.LevelExperience(69) - 1
            && !entry.Spells.ContainsKey(1110) && SkillRows(entry) == 0
            && Scalar("SELECT level FROM entities WHERE uid=@uid", entry.Entity.UID) == 69,
            "Arena failure rolls back the level profile and class skill before any cache grant");
        DemonReset(entry); LoadProgressionImage(entry); entry.IncreaseExperience(1, false);
        Check(entry.Entity.Level == 70 && entry.Entity.Experience == 0 && entry.ArenaStatistic.Level == 70
            && entry.ArenaStatistic.EntityID == entry.Entity.UID && entry.ArenaStatistic.ArenaPoints == 1000
            && entry.Spells[1110].Available && SkillRows(entry) == 1
            && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid", entry.Entity.UID) == 1000
            && entry.TeamArenaStatistic.EntityID==entry.Entity.UID && entry.TeamArenaStatistic.ArenaPoints==1000
            && Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",entry.Entity.UID)==1000,
            "loaded ordinary character commits level 70, its class skill and initial Arena enrollment together");
        var seeded = ArenaEntryPlayer(map, true);
        seeded.TeamArenaStatistic = new TeamQualifierStatistic(true) { EntityID=seeded.Entity.UID, ArenaPoints=0, CurrentHonor=77, TotalWin=9 };
        Query("INSERT INTO teamarena(entityid,entityname,level,arenapoints,currenthonor,totalwin) VALUES(@uid,@name,69,0,77,9)","uid",(long)seeded.Entity.UID,"name",seeded.Entity.Name);
        seeded.IncreaseExperience(1, false);
        Check(seeded.Entity.Level == 70 && seeded.ArenaStatistic.ArenaPoints == 1000 && seeded.TeamArenaStatistic.ArenaPoints == 1000
            && Scalar("SELECT count(*) FROM arena WHERE entityid=@uid AND currenthonor=77 AND totalwin=12 AND arenapoints=1000", seeded.Entity.UID) == 1
            && Scalar("SELECT count(*) FROM teamarena WHERE entityid=@uid AND currenthonor=77 AND totalwin=9 AND arenapoints=1000", seeded.Entity.UID) == 1,
            "existing below-level Arena rows receive matching points without changing either mode's honors or wins");
        Check(((QualifierStatistic)Call(typeof(ArenaTable),null,"ReadExisting",seeded.Entity.UID)).ArenaPoints==1000
            && ((TeamQualifierStatistic)Call(typeof(TeamArenaTable),null,"ReadExisting",seeded.Entity.UID)).ArenaPoints==1000,
            "production readers reload matching initial Arena point balances");
        var conflict=ArenaEntryPlayer(map,true);conflict.TeamArenaStatistic=new TeamQualifierStatistic(true){EntityID=conflict.Entity.UID,ArenaPoints=4321};
        Query("INSERT INTO teamarena(entityid,entityname,level,arenapoints) VALUES(@uid,@name,69,4321)","uid",(long)conflict.Entity.UID,"name",conflict.Entity.Name);
        bool conflictRejected=false;try{conflict.IncreaseExperience(1,false);}catch(InvalidOperationException){conflictRejected=true;}
        Check(conflictRejected && Fenced(conflict) && conflict.Entity.Level==69 && conflict.TeamArenaStatistic.ArenaPoints==4321
            && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",conflict.Entity.UID)==0
            && Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",conflict.Entity.UID)==4321,
            "conflicting earned Team Arena points reject initial fill without erasing either saved balance");
        DemonReset(conflict);LoadProgressionImage(conflict);conflict.TeamArenaStatistic.ArenaPoints=0;
        conflictRejected=false;try{conflict.IncreaseExperience(1,false);}catch(InvalidOperationException){conflictRejected=true;}
        Check(conflictRejected && Fenced(conflict) && conflict.Entity.Level==69 && SkillRows(conflict)==0
            && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",conflict.Entity.UID)==0
            && Scalar("SELECT arenapoints FROM teamarena WHERE entityid=@uid",conflict.Entity.UID)==4321,
            "stale zero Team Arena cache cannot replace a nonzero saved point balance");
        foreach(bool existingTeam in new[]{false,true}) {
            var failed=ArenaEntryPlayer(map,true);
            if(existingTeam) {
                failed.TeamArenaStatistic=new TeamQualifierStatistic(true){EntityID=failed.Entity.UID};
                Query("INSERT INTO teamarena(entityid,entityname,level,arenapoints) VALUES(@uid,@name,69,0)","uid",(long)failed.Entity.UID,"name",failed.Entity.Name);
            }
            Query("CREATE FUNCTION suppress_initial_team_points() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_initial_team_points BEFORE INSERT OR UPDATE ON teamarena FOR EACH ROW EXECUTE FUNCTION suppress_initial_team_points()");
            bool suppressed=false;
            try{failed.IncreaseExperience(1,false);}catch(InvalidOperationException){suppressed=true;}
            finally{Query("DROP TRIGGER suppress_initial_team_points ON teamarena;DROP FUNCTION suppress_initial_team_points()");}
            Check(suppressed && Fenced(failed) && failed.Entity.Level==69 && failed.ArenaStatistic.ArenaPoints==0 && SkillRows(failed)==0
                && Scalar("SELECT level FROM entities WHERE uid=@uid",failed.Entity.UID)==69
                && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",failed.Entity.UID)==0,
                "suppressed Team Arena mirror rolls back profile, solo points and skill: existing="+existingTeam);
            DemonReset(failed);LoadProgressionImage(failed);failed.IncreaseExperience(1,false);
            Check(!Fenced(failed) && failed.Entity.Level==70 && failed.ArenaPoints==1000 && failed.TeamArenaStatistic.ArenaPoints==1000
                && Scalar("SELECT count(*) FROM teamarena WHERE entityid=@uid AND arenapoints=1000",failed.Entity.UID)==1,
                "initial shared Arena fill retries once after mirror failure: existing="+existingTeam);
        }
        var reborn = ArenaEntryPlayer(map, true); reborn.Entity.Reborn = 2; EntityTable.SaveEntity(reborn);
        reborn.IncreaseExperience(1, false);
        Check(reborn.Entity.Level == 70 && reborn.ArenaStatistic.ArenaPoints == 0
            && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid", reborn.Entity.UID) == 0,
            "retraining after rebirth cannot reclaim initial Arena points from an existing record");
        var arenaRace = ArenaEntryPlayer(map, true);
        Parallel.Invoke(() => arenaRace.IncreaseExperience(1, false),
            () => { for (int i = 0; i < 20; i++) { ArenaTable.SaveArenaStatistics(arenaRace.ArenaStatistic); TeamArenaTable.SaveArenaStatistics(arenaRace.TeamArenaStatistic); } });
        ArenaTable.SaveArenaStatistics(arenaRace.ArenaStatistic);
        Check(!Fenced(arenaRace) && arenaRace.Entity.Level == 70 && arenaRace.ArenaStatistic.ArenaPoints == 1000
            && Scalar("SELECT count(*) FROM arena WHERE entityid=@uid AND level=70 AND arenapoints=1000 AND currenthonor=77 AND totalwin=12", arenaRace.Entity.UID) == 1
            && Scalar("SELECT count(*) FROM teamarena WHERE entityid=@uid AND arenapoints=1000",arenaRace.Entity.UID)==1,
            "concurrent Arena saves cannot overwrite newly committed initial points with the earlier zero balance");
        ArenaTable.SaveArenaStatistics(new QualifierStatistic(true) { EntityID = arenaRace.Entity.UID, ArenaPoints = 0 });
        Check(Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid", arenaRace.Entity.UID) == 1000,
            "a detached Arena statistics object cannot overwrite the canonical player's saved balance");
        ushort[] ids = { 1110, 1020, 8002, 6011, 10490 }; byte[] classes = { 15, 25, 45, 55, 65 };
        for (int i = 0; i < ids.Length; i++)
        {
            var p = StoredProgressionPlayer(map); p.Entity.Class = classes[i];
            p.Entity.Experience = DataHolder.LevelExperience(135) - 1; p.ArenaStatistic.ArenaPoints = 9876;
            if (!EntityTable.SaveEntity(p)) throw new Exception("Class effect fixture save failed.");
            p.IncreaseExperience(1, false);
            Check(p.Entity.Level == 136 && p.Spells.ContainsKey(ids[i]) && p.Spells[ids[i]].Available
                && p.ArenaStatistic.ArenaPoints == 9876 && SkillRows(p) == 1
                && Scalar("SELECT level FROM entities WHERE uid=@uid", p.Entity.UID) == 136,
                "class " + classes[i] + " learns its durable level skill without resetting earned Arena points");
            SkillTable.LoadSpells(p); SkillTable.SaveSpells(p);
            Check(!Fenced(p) && p.Spells.ContainsKey(ids[i]) && SkillRows(p) == 1,
                "class " + classes[i] + " skill survives the production loader and subsequent save without duplicate insertion");
        }
        var known = StoredProgressionPlayer(map); var skill = new Spell(true) { ID = 1110, Level = 3, PreviousLevel = 2, Experience = 123 };
        known.AddSpell(skill); known.Entity.Experience = DataHolder.LevelExperience(135) - 1; EntityTable.SaveEntity(known);
        known.IncreaseExperience(1, false);
        Check(ReferenceEquals(known.Spells[1110], skill) && skill.Available && skill.Level == 3 && skill.Experience == 123
            && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=1110 AND level=3 AND previouslevel=2 AND experience=123", known.Entity.UID) == 1,
            "pending learned skill is persisted with its real level, previous level and EXP instead of a fresh replacement");
        VerifySkillSaveTransactions(map);
        VerifyComposableExperienceEffects(map);
    }
    static void VerifySkillSaveTransactions(Map map)
    {
        var p = StoredProgressionPlayer(map);
        var first = new Spell(true) { ID = 1110, Level = 2, PreviousLevel = 7, Experience = 10 };
        var second = new Spell(true) { ID = 1020, Level = 1, PreviousLevel = 4, Experience = 20 };
        p.AddSpell(first); p.AddSpell(second);
        Query(@"CREATE FUNCTION reject_second_skill() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
IF NEW.id=1020 THEN RAISE EXCEPTION 'fixture skill failure'; END IF; RETURN NEW; END $$;
CREATE TRIGGER reject_second_skill BEFORE INSERT ON skills FOR EACH ROW EXECUTE FUNCTION reject_second_skill()");
        try { SkillTable.SaveSpells(p); }
        finally { Query("DROP TRIGGER reject_second_skill ON skills; DROP FUNCTION reject_second_skill()"); }
        Check(Fenced(p) && !first.Available && !second.Available && SkillRows(p) == 0,
            "failed skill batch rolls back every row and does not mark unsaved skills as durable");
        DemonReset(p); SkillTable.SaveSpells(p);
        Check(!Fenced(p) && first.Available && second.Available && SkillRows(p) == 2
            && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=1110 AND previouslevel=7", p.Entity.UID) == 1,
            "recovered skill batch preserves actual previous levels and marks rows saved only after commit");
        p.AddSpell(new Spell(true) { ID = 40101, Level = 2 }); SkillTable.SaveSpells(p);
        Check(!Fenced(p) && SkillRows(p) == 2, "derived rune abilities are not duplicated into ordinary skill persistence");
        Query("INSERT INTO skills(entityid,id,level,previouslevel,experience,levelhu,levelhu2) VALUES(@uid,40101,2,0,0,1,0)", "uid", (long)p.Entity.UID);
        SkillTable.LoadSpells(p);
        Check(p.Spells.ContainsKey(1110) && !p.Spells.ContainsKey(40101), "ordinary skill loading ignores historical rune rows whose source is equipped rune items");
        Query(@"CREATE FUNCTION reject_skill_delete() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
RAISE EXCEPTION 'fixture delete failure'; END $$;
CREATE TRIGGER reject_skill_delete BEFORE DELETE ON skills FOR EACH ROW EXECUTE FUNCTION reject_skill_delete()");
        bool rejected = false;
        try { p.RemoveSpell((ushort)1110); } catch (Npgsql.PostgresException) { rejected = true; }
        finally { Query("DROP TRIGGER reject_skill_delete ON skills; DROP FUNCTION reject_skill_delete()"); }
        Check(rejected && Fenced(p) && p.Spells.ContainsKey(1110)
            && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=1110", p.Entity.UID) == 1,
            "failed skill deletion preserves the cached and saved skill and fences the character");
        DemonReset(p);
        Check(p.RemoveSpell((ushort)1110) && !p.Spells.ContainsKey(1110)
            && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=1110", p.Entity.UID) == 0,
            "successful owned skill deletion removes the durable row before projecting removal");
        p.Spells[1020].Available = false; SkillTable.SaveSpells(p);
        Check(Fenced(p) && !p.Spells[1020].Available && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=1020", p.Entity.UID) == 1,
            "inconsistent unsaved skill cannot create a duplicate or overwrite an existing durable row");
        var race = StoredProgressionPlayer(map); race.AddSpell(new Spell(true) { ID = 1110 }); SkillTable.SaveSpells(race);
        Parallel.Invoke(() => race.RemoveSpell((ushort)1110), () => { for (int i = 0; i < 20; i++) SkillTable.SaveSpells(race); });
        Check(!Fenced(race) && !race.Spells.ContainsKey(1110) && SkillRows(race) == 0,
            "concurrent skill saves cannot recreate a skill removed under the same character owner lock");
        var runeOwner = StoredProgressionPlayer(map); runeOwner.Entity.Reborn = 2; var rune = RuneItem(runeOwner, 4020101, 102);
        uint subtype = ConquerItemInformation.BaseInformations[rune.ID].subtype; ushort runeSkill = (ushort)(subtype / 100);
        Query("INSERT INTO skills(entityid,id,level,previouslevel,experience,levelhu,levelhu2) VALUES(@uid,@id,9,0,0,1,0)",
            "uid", (long)runeOwner.Entity.UID, "id", (int)runeSkill);
        var runeService = typeof(Entity).Assembly.GetType("Revora.Network.GamePackets.RuneProgress");
        SkillTable.LoadSpells(runeOwner); Call(runeService, null, "RefreshSkills", runeOwner); SkillTable.SaveSpells(runeOwner);
        Check(!Fenced(runeOwner) && runeOwner.Spells.ContainsKey(runeSkill) && runeOwner.Spells[runeSkill].Level == subtype % 100
            && Scalar("SELECT level FROM skills WHERE entityid=@uid", runeOwner.Entity.UID) == 9,
            "login-order skill loading and rune projection use equipped item level while ignoring a stale ordinary skill row");
        rune.Position = 211; Query("UPDATE items SET position=211 WHERE uid=@uid", "uid", (long)rune.UID);
        SkillTable.LoadSpells(runeOwner); Call(runeService, null, "RefreshSkills", runeOwner);
        Check(!runeOwner.Spells.ContainsKey(runeSkill), "unequipped rune cannot regain its ability from the stale skill table on reload");
        VerifyReportedRuneSkillCollisions(map, runeService);
        VerifyPkMutationOwnership(map);
    }
    static void VerifyPkMutationOwnership(Map map)
    {
        var p = StoredProgressionPlayer(map);
        p.Entity.PKPoints = 10000;
        Parallel.Invoke(
            () => { for (int n = 0; n < 1000; n++) Call(typeof(Entity), p.Entity, "ChangePkPoints", 10); },
            () => { for (int n = 0; n < 1000; n++) Call(typeof(Entity), p.Entity, "ChangePkPoints", -1); },
            () => { for (int n = 0; n < 8; n++) if (!EntityTable.SaveEntity(p)) throw new Exception("Concurrent PK save failed"); });
        Check(p.Entity.PKPoints == 19000 && !Fenced(p),
            "concurrent kill increments, decay and character saves conserve the complete PK balance");
        Check(EntityTable.SaveEntity(p) && Scalar("SELECT pkpoints FROM entities WHERE uid=@uid", p.Entity.UID) == 19000,
            "the final PK owner snapshot persists without restoring an earlier save value");
        p.Entity.PKPoints = 65530; Call(typeof(Entity), p.Entity, "ChangePkPoints", 10);
        Check(p.Entity.PKPoints == ushort.MaxValue,
            "kill penalties saturate at the protocol maximum instead of wrapping a black-name player to low PK");
        p.Entity.PKPoints = 0; Call(typeof(Entity), p.Entity, "ChangePkPoints", -1);
        Check(p.Entity.PKPoints == 0, "PK decay cannot wrap zero to maximum");
        p.Entity.PKPoints = 100; Call(typeof(Entity), p.Entity, "ChangePkPoints", -1);
        Check(p.Entity.PKPoints == 99 && p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.RedName)
            && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.BlackName),
            "atomic decay across one hundred preserves the black-to-red name projection");
        p.Entity.PKPoints = 30; Call(typeof(Entity), p.Entity, "ChangePkPoints", -1);
        Check(p.Entity.PKPoints == 29 && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.RedName)
            && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.BlackName),
            "atomic decay below thirty clears both criminal name flags");
        VerifyPkDurableBalance(map);
    }
    static void LoadPkImage(GameState p)
    {
        using (var query = new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid", p.Entity.UID))
        using (var reader = new MySqlReader(query)) {
            if (!reader.Read()) throw new Exception("PK fixture owner missing");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.PkBalance"), null, "Loaded", p, reader);
        }
    }
    static void VerifyPkDurableBalance(Map map)
    {
        var service = typeof(Entity).Assembly.GetType("Revora.Game.Features.PkBalance");
        var p = StoredProgressionPlayer(map);
        p.Entity.PKPoints = 100; Check(EntityTable.SaveEntity(p), "PK fixture saves its initial criminal balance");
        Call(typeof(Entity), p.Entity, "ChangePkPoints", 10);
        var gate = typeof(GameState).GetProperty("PkSyncRoot", Instance).GetValue(p, null);
        lock (p.ItemSyncRoot) lock (gate) {
            using (var c = DataHolder.MySqlConnection) {
                c.Open(); using (var t = c.BeginTransaction()) {
                    Call(service, null, "Persist", c, t, p, (ushort)80);
                    t.Rollback();
                }
            }
        }
        Check(p.Entity.PKPoints == 110 && Scalar("SELECT pkpoints FROM entities WHERE uid=@uid", p.Entity.UID) == 100,
            "rolled-back PK reduction preserves both the durable preimage and pending kill penalty");
        lock (p.ItemSyncRoot) lock (gate) {
            using (var c = DataHolder.MySqlConnection) {
                c.Open(); using (var t = c.BeginTransaction()) {
                    Call(service, null, "Persist", c, t, p, (ushort)80); t.Commit();
                }
            }
            Call(service, null, "ApplyCommitted", p, (ushort)80);
        }
        Check(EntityTable.SaveEntity(p) && p.Entity.PKPoints == 80 && Scalar("SELECT pkpoints FROM entities WHERE uid=@uid", p.Entity.UID) == 80,
            "committed reduction includes pending penalty and advances autosave preimage");
        Query(@"CREATE FUNCTION suppress_pk_write() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
IF NEW.pkpoints<>OLD.pkpoints THEN RETURN NULL; END IF; RETURN NEW; END $$;
CREATE TRIGGER suppress_pk_write BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_pk_write()");
        bool suppressed = false;
        try {
            lock (p.ItemSyncRoot) lock (gate) {
                using (var c = DataHolder.MySqlConnection) {
                    c.Open(); using (var t = c.BeginTransaction()) {
                        try { Call(service, null, "Persist", c, t, p, (ushort)50); }
                        catch (InvalidOperationException) { suppressed = true; }
                    }
                }
            }
        } finally { Query("DROP TRIGGER suppress_pk_write ON entities; DROP FUNCTION suppress_pk_write()"); }
        Check(suppressed && p.Entity.PKPoints == 80 && Scalar("SELECT pkpoints FROM entities WHERE uid=@uid", p.Entity.UID) == 80
            && EntityTable.SaveEntity(p), "suppressed PK write cannot succeed or advance the saved image");
        Query("UPDATE entities SET pkpoints=91 WHERE uid=@uid", "uid", (long)p.Entity.UID);
        bool stale = false;
        lock (p.ItemSyncRoot) lock (gate) {
            using (var c = DataHolder.MySqlConnection) {
                c.Open(); using (var t = c.BeginTransaction()) {
                    try { Call(service, null, "Persist", c, t, p, (ushort)50); }
                    catch (InvalidOperationException) { stale = true; }
                }
            }
        }
        Check(stale && p.Entity.PKPoints == 80 && Scalar("SELECT pkpoints FROM entities WHERE uid=@uid", p.Entity.UID) == 91,
            "PK transaction refuses to overwrite a newer durable value");
        Check(!EntityTable.SaveEntity(p) && Fenced(p) && Scalar("SELECT pkpoints FROM entities WHERE uid=@uid", p.Entity.UID) == 91,
            "ordinary autosave also rejects the stale PK image and requires reload");
        var nullable = StoredProgressionPlayer(map);
        Query("UPDATE entities SET pkpoints=NULL WHERE uid=@uid", "uid", (long)nullable.Entity.UID); LoadPkImage(nullable);
        Call(typeof(Entity), nullable.Entity, "ChangePkPoints", 10);
        Check(EntityTable.SaveEntity(nullable) && Scalar("SELECT pkpoints FROM entities WHERE uid=@uid", nullable.Entity.UID) == 10,
            "legacy NULL PK image uses loader zero semantics without losing pending penalties");
        var unknown = StoredProgressionPlayer(map); Call(service, null, "Forget", unknown); unknown.Entity.PKPoints = 10;
        Check(!EntityTable.SaveEntity(unknown) && Fenced(unknown) && Scalar("SELECT COALESCE(pkpoints,0) FROM entities WHERE uid=@uid", unknown.Entity.UID) == 0,
            "missing PK preimage cannot be invented from an unflushed cache value");
        foreach (int invalid in new[] { -1, 65536 }) {
            var broken = StoredProgressionPlayer(map);
            Query("UPDATE entities SET pkpoints=@points WHERE uid=@uid", "points", invalid, "uid", (long)broken.Entity.UID);
            bool rejected = false; try { LoadPkImage(broken); } catch (OverflowException) { rejected = true; }
            Check(rejected && Fenced(broken), "out-of-range saved PK points are rejected rather than wrapped: " + invalid);
        }
    }
    static void VerifyReportedRuneSkillCollisions(Map map, Type runeService)
    {
        // Exact identities in the owner's 2026-09-20 live SaveSpells errors.
        // Historical rows must not collide with item-derived cached abilities,
        // overwrite their history, or prevent an ordinary learned skill saving.
        ushort[] ids = { 40305, 40310, 40304, 40309, 40303, 40302 };
        var p = StoredProgressionPlayer(map);
        foreach (ushort id in ids)
        {
            Query("INSERT INTO skills(entityid,id,level,previouslevel,experience,levelhu,levelhu2) VALUES(@uid,@id,1,2,37,0,0)",
                "uid", (long)p.Entity.UID, "id", (int)id);
            p.AddSpell(new Spell(true) { ID = id, Available = false });
        }
        var learned = new Spell(true) { ID = 1110, Level = 2, PreviousLevel = 3, Experience = 41 };
        p.AddSpell(learned);
        SkillTable.SaveSpells(p); SkillTable.SaveSpells(p);
        Check(!Fenced(p) && learned.Available && SkillRows(p) == 7,
            "six reported rune collisions do not duplicate rows or block repeat ordinary skill saves");
        Check(Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id BETWEEN 40300 AND 40399 AND level=1 AND previouslevel=2 AND experience=37", p.Entity.UID) == 6,
            "reported historical rune rows are preserved without being overwritten by derived cached skills");
        SkillTable.LoadSpells(p); Call(runeService, null, "RefreshSkills", p);
        Check(ids.All(id => !p.Spells.ContainsKey(id)) && p.Spells.ContainsKey(1110)
            && p.Spells[1110].Level == 2 && p.Spells[1110].PreviousLevel == 3 && p.Spells[1110].Experience == 41,
            "cold skill reload restores learned progress but cannot grant six unequipped rune abilities from stale rows");
        SkillTable.SaveSpells(p);
        Check(!Fenced(p) && SkillRows(p) == 7,
            "post-reload save remains healthy with all six historical rune rows retained");
    }
    static void VerifyComposableExperienceEffects(Map map)
    {
        var p = ArenaEntryPlayer(map, false); var plan = RewardPlan(p, 1);
        var profile = plan.GetType().GetField("After", Instance).GetValue(plan);
        var service = typeof(Entity).Assembly.GetType("Revora.Game.Features.ExperienceRewards");
        bool rejected = false;
        lock (p.ItemSyncRoot)
        lock (typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorExperience").GetField("Sync", Hidden).GetValue(null))
        {
            try
            {
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        Call(ProgressionStore, null, "Persist", c, t, p, profile);
                        Call(service, null, "Persist", c, t, p, plan);
                        using (var q = new Npgsql.NpgsqlCommand("INSERT INTO progression_failure_fixture(uid,receipt) VALUES(@uid,0)", c, t))
                        { q.Parameters.AddWithValue("uid", (long)p.Entity.UID); q.ExecuteNonQuery(); }
                        t.Commit();
                    }
                }
            }
            catch (Npgsql.PostgresException) { rejected = true; }
        }
        Check(rejected && SkillRows(p) == 0 && Scalar("SELECT count(*) FROM arena WHERE entityid=@uid", p.Entity.UID) == 0
            && Scalar("SELECT count(*) FROM teamarena WHERE entityid=@uid",p.Entity.UID)==0
            && Scalar("SELECT level FROM entities WHERE uid=@uid", p.Entity.UID) == 69 && !p.Spells.ContainsKey(1110),
            "failed outer receipt rolls back profile, class skill and Arena enrollment in one caller-owned transaction");
        lock (p.ItemSyncRoot)
        lock (typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorExperience").GetField("Sync", Hidden).GetValue(null))
        {
            object effects;
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    Call(ProgressionStore, null, "Persist", c, t, p, profile);
                    effects = Call(service, null, "Persist", c, t, p, plan); t.Commit();
                }
            }
            p.Spells = null; rejected = false;
            try { Call(service, null, "ApplyCommitted", p, effects); } catch (InvalidOperationException) { rejected = true; }
        }
        Check(rejected && Fenced(p) && SkillRows(p) == 1 && Scalar("SELECT level FROM entities WHERE uid=@uid", p.Entity.UID) == 70,
            "post-commit effect projection failure fences stale cache and retains the complete durable reward");
        var recovered = (QualifierStatistic)Call(typeof(ArenaTable), null, "ReadExisting", p.Entity.UID);
        SkillTable.LoadSpells(p);
        Check(recovered != null && recovered.EntityID == p.Entity.UID && recovered.Level == 70 && recovered.ArenaPoints == 1000
            && p.Spells.ContainsKey(1110) && p.Spells[1110].Available && SkillRows(p) == 1,
            "production recovery readers find committed Arena and skill rows even when cache projection never completed");
    }
}
