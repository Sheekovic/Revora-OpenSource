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
    static readonly Type FirstRite = typeof(Entity).Assembly.GetType("Revora.Game.Features.FirstRebirth");
    static DateTime rebirthNow = new DateTime(2032, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    static readonly byte[] rebirthClasses = { 11,21,41,51,61,71,81,132,142,161 };
    static GameState RebirthPlayer(Map town, byte profession = 15, byte level = 130) {
        var p = AppearancePlayer(town); p.Entity.Class = profession; p.Entity.Level = level;
        p.Entity.X = (ushort)(town.Npcs[59558].X - 2); p.Entity.Y = (ushort)(town.Npcs[59558].Y - 2); p.ActiveNpc = 59558;
        Collection(p); p.Entity.MyAchievement = new Achievement(p.Entity); p.Entity.MyAchievement.Load("");
        if (!EntityTable.SaveEntity(p)) throw new Exception("Rebirth fixture save failed.");
        Pack(p, 721259, 1); return p;
    }
    static void RebirthOffer(GameState p, byte destination = 11, bool bless = false, bool sturdy = false) {
        StoneNpc(p, 1); StoneNpc(p, (byte)(200 + Array.IndexOf(rebirthClasses, destination)));
        StoneNpc(p, sturdy ? (byte)211 : (byte)210); StoneNpc(p, bless ? (byte)213 : (byte)212);
    }
    static long Rebirths(GameState p) { return Scalar("SELECT count(*) FROM revora_first_rebirths WHERE uid=@uid", p.Entity.UID); }
    static void RebirthSkill(GameState p, ushort id) {
        var s = new Spell(true) { ID = id, Level = 4, PreviousLevel = 2, Experience = 90, Available = true };
        p.Spells.Add(id, s); Query("INSERT INTO skills(entityid,id,level,previouslevel,experience,levelhu,levelhu2) VALUES(@uid,@id,4,2,90,1,0)", "uid", (long)p.Entity.UID, "id", (int)id);
    }
    static void RebirthProf(GameState p) {
        p.Proficiencies.Add(410, new Proficiency(true) { ID = 410, Level = 12, PreviousLevel = 8, Experience = 90, Available = true });
        Query("INSERT INTO profs(entityid,id,level,previouslevel,experience) VALUES(@uid,410,12,8,90)", "uid", (long)p.Entity.UID);
    }
    static ConquerItem RebirthGear(GameState p, byte slot, uint type = 410339) {
        var i = Pack(p, type, 1); p.Inventory.Remove(i.UID, Enums.ItemUse.None, false); i.Position = slot;
        i.Durability = i.MaximDurability = 1000;
        Query("UPDATE items SET position=@slot,durability=1000,maximdurability=1000 WHERE uid=@uid", "slot", (int)slot, "uid", (long)i.UID);
        if (!p.Equipment.Add(i, Enums.ItemUse.None)) throw new Exception("Rebirth gear fixture failed."); return i;
    }
    static void VerifyFirstGarment(GameState p){
        var item=p.Inventory.Objects.Single(i=>i.ID==187405);
        Check(!item.Bound && item.Days==0 && item.TimeLeftInMinutes==0 && item.StackSize==1
            && Scalar("SELECT count(*) FROM revora_first_rebirths r JOIN items i ON i.uid=r.garment_uid WHERE r.uid=@uid AND i.entityid=@uid AND i.id=187405 AND i.bound=0",p.Entity.UID)==1,
            "first rite grants a permanent unbound Armor of Holy Light with an owned receipt");
    }
    static DateTime RiteReceiptUtc(GameState p,bool second) {
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand(second
            ? "SELECT created_at FROM revora_second_rebirths WHERE uid=@uid"
            : "SELECT created_at FROM revora_first_rebirths WHERE uid=@uid",c)){
            q.Parameters.AddWithValue("uid",(long)p.Entity.UID);
            using(var r=q.ExecuteReader()){if(!r.Read())throw new Exception("Missing rite receipt");return r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;}
        }}
    }
    static void VerifyFirstRebirth(string runtime, Map town) {
        var journey = VerifyRebirthMaterials(runtime, town);
        Query("CREATE TABLE profs AS TABLE public.profs WITH NO DATA");
        Call(FirstRite, null, "Initialize"); Call(FirstRite, null, "Initialize");
        FirstRite.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => rebirthNow));
        FirstRite.GetField("Roll", Hidden).SetValue(null, (Func<int, int>)(max => 0));
        DataHolder.ReadStats(); SpellTable.Load(); RunesSystem.LoadRunesEXP();
        journey.Entity.Class = 15; journey.Entity.Level = 130; journey.Entity.Body = 1003;
        if (!EntityTable.SaveEntity(journey)) throw new Exception("Journey rebirth fixture save failed."); LoadAppearance(journey);
        journey.ActiveNpc = 59558; RebirthOffer(journey); StoneNpc(journey, 214);
        Check(!Fenced(journey) && journey.Entity.Reborn == 1 && Rebirths(journey) == 1 && Stones(journey) == 1 && Waters(journey) == 1,
            "ordinary hunted Clean Water and crafted Heavenly Stone complete actual first rebirth");
        Check(RiteReceiptUtc(journey,false)==rebirthNow,"first-rite receipt uses its authoritative game UTC clock, independent of database wall time");
        Check(HasAchievement(journey, 11030) && HasAchievement(journey, 10315) && !HasAchievement(journey, 11031) && !HasAchievement(journey, 10316),
            "committed first rite grants its two distinct achievements without second-rebirth claims");
        VerifyFirstGarment(journey);
        var loaded = ReloadGold(journey); ConquerItemTable.LoadItems(loaded); new TitleStorage().Login(loaded); SkillTable.LoadSpells(loaded); SkillTable.LoadProficiencies(loaded);
        VerifyFirstGarment(loaded);
        Check(loaded.Entity.Reborn == 1 && loaded.Entity.FirstRebornClass == 15 && loaded.Entity.FirstRebornLevel == 130 && loaded.Entity.Level == 15
            && loaded.Inventory.Contains(700003, 1) && loaded.Inventory.Contains(410077, 1) && !loaded.Inventory.Contains(721259, 1)
            && HasAchievement(loaded, 11030) && HasAchievement(loaded, 10315), "production character/item/skill reload preserves complete rebirth and earned gifts");
        var garment=loaded.Inventory.Objects.Single(i=>i.ID==187405);
        ItemHandler.EquipItem(new ItemUsage(true){UID=garment.UID,dwParam=ConquerItem.Garment,Action=ItemUsage.Mode.EquipItem},loaded);
        Check(loaded.Equipment.TryGetItem(ConquerItem.Garment)!=null && loaded.Equipment.TryGetItem(ConquerItem.Garment).UID==garment.UID,"ordinary reborn player equips the earned five-star garment");
        loaded=ReloadBall(loaded);
        Check(loaded.Equipment.TryGetItem(ConquerItem.Garment)!=null && loaded.Equipment.TryGetItem(ConquerItem.Garment).UID==garment.UID && !loaded.Equipment.TryGetItem(ConquerItem.Garment).Bound,
            "earned garment stays equipped and unbound after reconnect");
        new TitleStorage().Login(loaded);
        ItemHandler.UnequipItem(new ItemUsage(true){UID=garment.UID,dwParam=ConquerItem.Garment},loaded);
        Check(loaded.Inventory.ContainsUID(garment.UID),"ordinary journey unequips its earned garment before wardrobe admission");
        new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=garment.UID,ItemId=(int)garment.ID}}.Handle(loaded);
        Check(loaded.Entity.StorageItems.ContainsKey(garment.UID) && OwnedTitles(loaded).ContainsKey(60030022),
            "the same hunted-material rebirth journey unlocks Starlight Wings from its earned garment");
        loaded=ReloadBall(loaded);new TitleStorage().Login(loaded);TitleRequest(60030022,TitleStorage.Action.Equip).Handle(loaded);
        Check(loaded.Entity.EquippedWing==60030022 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=187405",loaded.Entity.UID)==1,
            "full hunted-material journey retains one garment and equips Starlight Wings after production reload");
        // The wardrobe request also accepts an already stored design; use a second
        // actually earned garment in the bag to exercise first-time admission.
        var wardrobeOwner=RebirthPlayer(town);RebirthOffer(wardrobeOwner);StoneNpc(wardrobeOwner,214);
        new TitleStorage().Login(wardrobeOwner);
        var wardrobeGift=wardrobeOwner.Inventory.Objects.Single(i=>i.ID==187405);
        var coat=new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=wardrobeGift.UID,ItemId=(int)wardrobeGift.ID}};
        coat.Handle(wardrobeOwner);coat.Handle(wardrobeOwner);
        Check(StorageItem.Garments[187405].Stars==5 && wardrobeOwner.Entity.StorageItems.ContainsKey(wardrobeGift.UID)
            && !wardrobeOwner.Inventory.ContainsUID(wardrobeGift.UID) && SavedRune(wardrobeGift,"inwardrobe")==1
            && OwnedTitles(wardrobeOwner).ContainsKey(60030022),"earned five-star garment enters wardrobe once and unlocks Starlight Wings");
        wardrobeOwner=ReloadBall(wardrobeOwner);new TitleStorage().Login(wardrobeOwner);
        Check(wardrobeOwner.Entity.StorageItems.ContainsKey(wardrobeGift.UID) && !wardrobeOwner.Entity.StorageItems[wardrobeGift.UID].Bound
            && OwnedTitles(wardrobeOwner).ContainsKey(60030022)
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=187405",wardrobeOwner.Entity.UID)==1,
            "earned wardrobe ownership and five-star title persist without duplicating the garment");
        var garmentFault=RebirthPlayer(town);RebirthOffer(garmentFault);
        Query("CREATE FUNCTION fail_rebirth_garment() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.id=187405 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER fail_rebirth_garment BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION fail_rebirth_garment()");
        try{StoneNpc(garmentFault,214);}finally{Query("DROP TRIGGER fail_rebirth_garment ON items;DROP FUNCTION fail_rebirth_garment()");}
        Check(Fenced(garmentFault) && Rebirths(garmentFault)==0 && garmentFault.Entity.Reborn==0 && garmentFault.Inventory.Contains(721259,1)
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=187405",garmentFault.Entity.UID)==0,"garment insertion failure rolls back first rebirth and its other gifts");
        garmentFault=ReloadBall(garmentFault);garmentFault.ActiveNpc=59558;RebirthOffer(garmentFault);StoneNpc(garmentFault,214);VerifyFirstGarment(garmentFault);
        foreach (byte source in new byte[] { 15,25,45,55,65,75,85,135,145,165 }) foreach (byte destination in rebirthClasses) {
            var p = RebirthPlayer(town, source); RebirthSkill(p, 1045); RebirthProf(p);
            RebirthOffer(p, destination, false, true); StoneNpc(p, 214);
            int total = p.Entity.Strength + p.Entity.Agility + p.Entity.Vitality + p.Entity.Spirit + p.Entity.Atributes;
            Check(!Fenced(p) && p.Entity.Reborn == 1 && p.Entity.Class == destination && p.Entity.Body == 1004 && total == 127
                && p.Entity.Experience == 0 && p.Entity.FirstRebornClass == source && p.Entity.FirstRebornLevel == 130,
                "all ordinary source/destination pairs have correct profile, size and total attributes: " + source + " to " + destination);
            Check(p.Spells[1045].Level == 0 && p.Spells[1045].PreviousLevel == 4 && p.Spells[1045].Experience == 0
                && p.Proficiencies[410].Level == 0 && p.Proficiencies[410].PreviousLevel == 12 && p.Proficiencies[410].Experience == 0
                && Scalar("SELECT previouslevel FROM profs WHERE entityid=@uid AND id=410", p.Entity.UID) == 12,
                "first rebirth resets learned weapon skill and proficiency while retaining recovery levels: " + source + " to " + destination);
        }
        // Independent expected bonus table, including odd Water levels that formerly fell into the maximum.
        int[] water = { 0,0,1,1,3,3,6,6,10,10,15,15,21,21,28,28,36,36,45,45,55 };
        int[] other = { 0,1,3,6,10,15,21,28,36,45,55 };
        for (int i = 0; i < water.Length; i++) Check(journey.ExtraAtributePoints((byte)(110 + i), 135) == water[i], "Water bonus at level " + (110 + i));
        for (int i = 0; i < other.Length; i++) Check(journey.ExtraAtributePoints((byte)(120 + i), 15) == other[i], "ordinary bonus at level " + (120 + i));
        VerifyRebirthFailures(town);
        VerifyRebirthEquipment(town);
        VerifyRebirthSkills(town);
    }
    static void VerifyRebirthFailures(Map town) {
        foreach (string mode in new[] { "no-offer","wrong-order","cancel","expire","low-level","wrong-rank","reborn","missing-stone","locked","remote","trade","dead","fake","unloaded","replaced","changed-bag","changed-skill" }) {
            var p = RebirthPlayer(town); var stone = p.Inventory.Objects.Single();
            if (mode == "low-level") p.Entity.Level = 119;
            if (mode == "wrong-rank") p.Entity.Class = 14;
            if (mode == "reborn") p.Entity.Reborn = 1;
            if (mode == "missing-stone") DisRemove(p, stone);
            if (mode == "locked") stone.Lock = 1;
            if (mode != "no-offer") RebirthOffer(p);
            if (mode == "wrong-order") StoneNpc(p, 210);
            if (mode == "cancel") StoneNpc(p, 255);
            if (mode == "expire") rebirthNow = rebirthNow.AddMinutes(2);
            if (mode == "remote") p.Entity.X += 20;
            if (mode == "trade") p.Trade.InTrade = true;
            if (mode == "dead") p.Entity.Hitpoints = 0;
            if (mode == "fake") p.Fake = true;
            if (mode == "unloaded") p.Entity.FullyLoaded = false;
            if (mode == "replaced") Kernel.GamePool.Remove(p.Entity.UID);
            if (mode == "changed-bag") stone.StackSize = 2;
            if (mode == "changed-skill") RebirthSkill(p, 1045);
            StoneNpc(p, 214);
            Check(Rebirths(p) == 0 && !HasAchievement(p, 11030) && (mode == "missing-stone" || SavedRune(stone, "id") == 721259), "rejected first rite preserves cost and achievements: " + mode);
        }
        foreach (string table in new[] { "entities", "items", "skills", "profs", "revora_first_rebirths", "revora_achievement_evidence" }) {
            var p = RebirthPlayer(town); RebirthSkill(p, 1045); RebirthProf(p); var stone = p.Inventory.Objects.Single(); RebirthOffer(p);
            Query("CREATE FUNCTION reject_rebirth_write() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER reject_rebirth_write BEFORE INSERT OR UPDATE OR DELETE ON " + table + " FOR EACH ROW EXECUTE FUNCTION reject_rebirth_write()");
            try {
                StoneNpc(p, 214);
                Check(Fenced(p) && p.Entity.Reborn == 0 && Rebirths(p) == 0 && p.Inventory.ContainsUID(stone.UID) && SavedRune(stone, "id") == 721259
                    && Scalar("SELECT COALESCE(reborn,0) FROM entities WHERE uid=@uid", p.Entity.UID) == 0 && p.Spells[1045].Level == 4 && p.Proficiencies[410].Level == 12,
                    "silent write failure rolls back the entire rite without projecting partial state: " + table);
            } finally { Query("DROP TRIGGER reject_rebirth_write ON " + table + ";DROP FUNCTION reject_rebirth_write()"); }
        }
        foreach (string part in new[] { "profile", "item", "skill", "prof", "appearance" }) {
            var p = RebirthPlayer(town); RebirthSkill(p, 1045); RebirthProf(p); RebirthOffer(p);
            string sql = part == "profile" ? "UPDATE entities SET experience=12345 WHERE uid=@uid"
                : part == "appearance" ? "UPDATE entities SET body=1004 WHERE uid=@uid"
                : part == "item" ? "UPDATE items SET bound=1 WHERE entityid=@uid"
                : part == "skill" ? "UPDATE skills SET level=5 WHERE entityid=@uid" : "UPDATE profs SET level=13 WHERE entityid=@uid";
            Query(sql, "uid", (long)p.Entity.UID); StoneNpc(p, 214);
            Check(Fenced(p) && Rebirths(p) == 0 && p.Entity.Reborn == 0 && p.Inventory.Contains(721259, 1), "external durable edit fences stale rebirth: " + part);
        }
        var parallel = RebirthPlayer(town); RebirthOffer(parallel); Parallel.For(0, 12, i => StoneNpc(parallel, 214));
        Check(!Fenced(parallel) && Rebirths(parallel) == 1 && parallel.Inventory.Objects.Count(i => i.ID == 700003) == 1, "concurrent final clicks consume one stone and award one gem");
        Check(!RebirthPlayer(town).Reborn(11), "legacy direct rebirth cannot bypass the first-rite transaction");
        var oldMenu = RebirthPlayer(town); foreach (byte option in new byte[] { 12,13,23,33,53,63,73,83,93,145,155,174,233,243,253,254 }) StoneNpc(oldMenu, option);
        Check(oldMenu.Entity.Reborn == 0 && Rebirths(oldMenu) == 0 && oldMenu.Inventory.Contains(721259, 1), "old arithmetic and overlapping first-rebirth choices cannot mutate a character");
        var saving = RebirthPlayer(town); RebirthProf(saving); RebirthOffer(saving);
        Parallel.Invoke(() => StoneNpc(saving, 214), () => SkillTable.SaveProficiencies(saving));
        Check(!Fenced(saving) && saving.Entity.Reborn == 1 && saving.Proficiencies[410].Level == 0
            && Scalar("SELECT level FROM profs WHERE entityid=@uid AND id=410", saving.Entity.UID) == 0,
            "concurrent proficiency autosave cannot overwrite the committed rebirth reset");
        foreach (string column in new[] { "locked", "suspicious", "days" }) {
            var p = RebirthPlayer(town);
            Query("UPDATE items SET " + column + "=" + (column == "days" ? "'1'" : "1") + " WHERE entityid=@uid", "uid", (long)p.Entity.UID);
            RebirthOffer(p); StoneNpc(p, 214);
            Check(Fenced(p) && Rebirths(p) == 0 && p.Inventory.Contains(721259, 1), "durable material restrictions are checked even if the cache predates them: " + column);
        }
        foreach (string mode in new[] { "gift-insert", "equipment-update", "skill-delete", "extra-skill", "extra-prof", "extra-gear", "extra-bag" }) {
            var p = RebirthPlayer(town, 45); var gear = RebirthGear(p, 4); RebirthSkill(p, 8001); RebirthProf(p);
            if (mode == "extra-skill") Query("INSERT INTO skills(entityid,id,level) VALUES(@uid,1045,4)", "uid", (long)p.Entity.UID);
            if (mode == "extra-prof") Query("INSERT INTO profs(entityid,id,level) VALUES(@uid,420,12)", "uid", (long)p.Entity.UID);
            if (mode == "extra-gear") { var orphan = Pack(p, 410009, 1); p.Inventory.Remove(orphan.UID, Enums.ItemUse.None, false); Query("UPDATE items SET position=5 WHERE uid=@uid", "uid", (long)orphan.UID); }
            if (mode == "extra-bag") { var orphan = Pack(p, 1088001, 1); p.Inventory.Remove(orphan.UID, Enums.ItemUse.None, false); }
            RebirthOffer(p, 21);
            string target = mode == "skill-delete" ? "skills" : "items";
            bool fault = mode == "gift-insert" || mode == "equipment-update" || mode == "skill-delete";
            if (fault) {
                Query("CREATE FUNCTION reject_rebirth_write() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                Query("CREATE TRIGGER reject_rebirth_write BEFORE " + (mode == "gift-insert" ? "INSERT" : mode == "equipment-update" ? "UPDATE" : "DELETE")
                    + " ON " + target + " FOR EACH ROW EXECUTE FUNCTION reject_rebirth_write()");
            }
            try { StoneNpc(p, 214); Check(Fenced(p) && Rebirths(p) == 0 && p.Inventory.Contains(721259, 1) && gear.ID == 410339,
                "late mutation failure or incomplete loaded ownership cannot produce a partial rite: " + mode); }
            finally { if (fault) Query("DROP TRIGGER reject_rebirth_write ON " + target + ";DROP FUNCTION reject_rebirth_write()"); }
        }
    }
    static void VerifyRebirthEquipment(Map town) {
        var p = RebirthPlayer(town); var right = RebirthGear(p, 4); var left = RebirthGear(p, 5); var alt = RebirthGear(p, 25);
        right.Plus = 6; Query("UPDATE items SET plus=6 WHERE uid=@uid", "uid", (long)right.UID);
        RebirthOffer(p, 11, true); StoneNpc(p, 214);
        Check(!Fenced(p) && right.Bless == 1 && right.Plus == 6 && right.ID % 10 == 9 && ConquerItemInformation.BaseInformations[right.ID].Level <= 20
            && p.Inventory.ContainsUID(left.UID) && p.Inventory.ContainsUID(alt.UID) && left.Position == 0 && alt.Position == 0
            && SavedRune(right, "bless") == 1 && SavedRune(left, "position") == 0 && SavedRune(alt, "position") == 0,
            "blessed rite preserves quality/plus, lowers both sets and atomically returns both off-hands");
        var noBless = RebirthPlayer(town); RebirthOffer(noBless, 11, true); StoneNpc(noBless, 214);
        Check(!Fenced(noBless) && noBless.Entity.Reborn == 0 && noBless.Inventory.Contains(721259, 1), "blessing without eligible gear spends no stone");
        var female = RebirthPlayer(town); female.Entity.Body = 2001; EntityTable.SaveEntity(female); RebirthOffer(female, 161, false, true); StoneNpc(female, 214);
        Check(!Fenced(female) && female.Entity.Class == 161 && female.Entity.Body == 2002, "Windwalker first rebirth preserves gender and offers the selected body size");
        var bound = RebirthPlayer(town); var stone = bound.Inventory.Objects.Single(); stone.Bound = true; stone.StackSize = 2;
        Query("UPDATE items SET bound=1,stacksize=2 WHERE uid=@uid", "uid", (long)stone.UID); RebirthOffer(bound); StoneNpc(bound, 214);
        Check(!Fenced(bound) && stone.StackSize == 1 && bound.Inventory.Objects.Where(i => i.UID != stone.UID && i.ID!=187405).All(i => i.Bound)
            && SavedRune(stone, "stacksize") == 1, "stacked bound stone consumes one and binds its original rewards");VerifyFirstGarment(bound);
        var full = RebirthPlayer(town); for (int i = 0; i < 39; i++) Pack(full, 1088001, 1);
        RebirthOffer(full, 11); StoneNpc(full, 214);
        Check(!Fenced(full) && full.Entity.Reborn == 0 && full.Inventory.Count == 40, "full bag cannot lose its stone when two gifts require space");
        var exact = RebirthPlayer(town); for (int i = 0; i < 37; i++) Pack(exact, 1088001, 1);
        RebirthOffer(exact, 11); StoneNpc(exact, 214);
        Check(!Fenced(exact) && exact.Entity.Reborn == 1 && exact.Inventory.Count == 40, "capacity includes the consumed stone's released slot");
        var zero = RebirthPlayer(town); var single = zero.Inventory.Objects.Single(); single.StackSize = 0;
        Query("UPDATE items SET stacksize=0 WHERE uid=@uid", "uid", (long)single.UID); RebirthOffer(zero, 61); StoneNpc(zero, 214);
        Check(!Fenced(zero) && zero.Entity.Reborn == 1 && zero.Inventory.Count == 2 && !zero.Inventory.ContainsUID(single.UID), "legacy zero-stack singleton is consumed once and Monk receives no obsolete blade");
        var boundBless = RebirthPlayer(town); var boundStone = boundBless.Inventory.Objects.Single(); boundStone.Bound = true;
        Query("UPDATE items SET bound=1 WHERE uid=@uid", "uid", (long)boundStone.UID); var blessed = RebirthGear(boundBless, 4);
        RebirthOffer(boundBless, 11, true); StoneNpc(boundBless, 214);
        Check(!Fenced(boundBless) && blessed.Bless == 1 && blessed.Bound && SavedRune(blessed, "bound") == 1, "bound material cannot become an unbound equipment blessing");
        var dragon = RebirthPlayer(town); RebirthOffer(dragon, 81); StoneNpc(dragon, 214);
        Check(!Fenced(dragon) && dragon.Entity.Strength == 19 && dragon.Entity.Vitality == 15 && dragon.Entity.Agility == 5 && dragon.Entity.Spirit == 13,
            "Dragon Warrior uses its installed BruceLee profile instead of the Taoist fallback");
        var collector = RebirthPlayer(town);
        uint garment = ConquerItemInformation.BaseInformations.Values.First(i => ItemHandler.ItemPosition(i.ID) == 7).ID;
        Query("INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,inwardrobe) SELECT 60000000+s,@type,@uid,0,0,1,1,1 FROM generate_series(1,513) s",
            "type", (int)garment, "uid", (long)collector.Entity.UID);
        RebirthOffer(collector, 61); StoneNpc(collector, 214);
        Check(!Fenced(collector) && collector.Entity.Reborn == 1 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND inwardrobe=1", collector.Entity.UID) == 513,
            "unrelated wardrobe storage does not enlarge the bounded rebirth snapshot or consume bag capacity");
        var equippedReload = ReloadGold(p); ConquerItemTable.LoadItems(equippedReload); new TitleStorage().Login(equippedReload);
        Check(equippedReload.Equipment.TryGetItem(4).Bless == 1 && equippedReload.Equipment.TryGetItem(4).Plus == 6
            && equippedReload.Inventory.ContainsUID(left.UID) && equippedReload.Inventory.ContainsUID(alt.UID), "production reload preserves lowered blessed gear and both returned off-hands");
    }
    static void VerifyRebirthSkills(Map town) {
        int[][] transitions = {
            new[]{85,11,12280,12140}, new[]{165,11,12830,12840}, new[]{65,11,10400,10420}, new[]{75,11,11070,11040},
            new[]{55,11,6001,6000}, new[]{145,11,1000,1002}, new[]{145,132,1120,1165}, new[]{135,142,1050,1100},
            new[]{135,41,1075,1085}, new[]{25,132,1025,1051}, new[]{25,11,1320,1025}, new[]{15,161,1110,1115}
        };
        foreach (var row in transitions) {
            var p = RebirthPlayer(town, (byte)row[0]); RebirthSkill(p, (ushort)row[2]); RebirthSkill(p, (ushort)row[3]);
            RebirthOffer(p, (byte)row[1]); StoneNpc(p, 214);
            Check(!Fenced(p) && p.Spells.ContainsKey((ushort)row[2]) && !p.Spells.ContainsKey((ushort)row[3])
                && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=" + row[3], p.Entity.UID) == 0,
                "reviewed cross-class skill retention and removal: " + row[0] + " to " + row[1]);
            var loaded = ReloadGold(p); SkillTable.LoadSpells(loaded);
            Check(loaded.Spells.ContainsKey((ushort)row[2]) && !loaded.Spells.ContainsKey((ushort)row[3]), "skill transition survives the production loader: " + row[0] + " to " + row[1]);
        }
        foreach (var pair in new[] { new[]{15,11,3050},new[]{25,21,3060},new[]{45,41,5000},new[]{45,11,5002},new[]{55,51,6002},new[]{85,81,12280},new[]{135,132,3090},new[]{145,142,3080} }) {
            var p = RebirthPlayer(town, (byte)pair[0]); RebirthOffer(p, (byte)pair[1]); StoneNpc(p, 214);
            Check(!Fenced(p) && p.Spells.ContainsKey((ushort)pair[2]) && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=" + pair[2], p.Entity.UID) == 1,
                "same-class first-rebirth art is granted atomically: " + pair[0]);
        }
    }
}
