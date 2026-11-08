using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class SecondRebirth
    {
        private sealed class Offer {
            internal byte Class, Stage; internal uint TokenUid;
            internal Guid? Quest; internal int QuestVersion; internal bool Bound;
            internal DateTime Until; internal string Cache, Items, Skills, Profs;
        }
        private sealed class Gear { internal ConquerItem Item; internal uint Type; internal byte Position; }
        private static readonly ConditionalWeakTable<GameState, Offer> offers = new ConditionalWeakTable<GameState, Offer>();
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        internal static void Initialize() {
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_second_rebirths(
uid bigint PRIMARY KEY,first_class integer NOT NULL,first_level integer NOT NULL,
old_class integer NOT NULL,old_level integer NOT NULL,old_experience bigint NOT NULL,new_class integer NOT NULL,
token_uid bigint NOT NULL,bound boolean NOT NULL,reward_item bigint NOT NULL,
created_at timestamptz NOT NULL DEFAULT now(),recovered_at timestamptz);
ALTER TABLE revora_second_rebirths ADD COLUMN IF NOT EXISTS soul_uid bigint UNIQUE;
ALTER TABLE revora_second_rebirths ADD COLUMN IF NOT EXISTS quest_run uuid;
ALTER TABLE revora_second_rebirths ADD COLUMN IF NOT EXISTS payment_kind varchar(16) NOT NULL DEFAULT 'exemption-token'
CHECK((payment_kind='exemption-token' AND token_uid>0 AND quest_run IS NULL) OR (payment_kind='abyss-quest' AND token_uid=0 AND quest_run IS NOT NULL));
CREATE UNIQUE INDEX IF NOT EXISTS revora_second_rebirth_quest ON revora_second_rebirths(quest_run) WHERE quest_run IS NOT NULL;"); }
        }
        private static bool Near(GameState p) {
            return Q.Near(p, HeavenlyStoneCrafting.Sereth, 1002) && p.Map.ID == 1002 && p.Map.BaseID == 1002
                && p.Entity.PokerTable == 0 && CharacterProgression.Managed(p);
        }
        private static bool Token(ConquerItem i) {
            return i.ID == 723701 && i.Position == 0 && i.Warehouse == 0 && i.Lock == 0
                && !i.Suspicious && !i.InWardrobe && i.Days == 0 && i.TimeLeftInMinutes == 0;
        }
        private static string Cache(GameState p) { return FirstRebirth.Cache(p); }
        private static string Rows(NpgsqlConnection c, NpgsqlTransaction t, GameState p, string table) { return FirstRebirth.Rows(c, t, p, table); }
        private static void Ready(GameState p, Offer offer, Npcs dialog) {
            var token = p.Inventory.Objects.Where(Token).OrderByDescending(i => i.Bound).ThenBy(i => i.UID).FirstOrDefault();
            offer.Cache = Cache(p);
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                SkillTable.LockOwner(c, t, p); offer.Items = Rows(c, t, p, "items"); offer.Skills = Rows(c, t, p, "skills"); offer.Profs = Rows(c, t, p, "profs");
                FirstRebirth.AssertLoaded(c, t, p);
                var run=AbyssRebirthPayment.Read(c,t,p);
                if(AbyssRebirthPayment.Pending(c,t,run)){dialog.Text("Finish the spirit you called and collect your Abyss spoils before leaving this life.");return;}
                if(AbyssRebirthPayment.Earned(c,t,run)){offer.Quest=run.Id;offer.QuestVersion=run.Version;offer.Bound=run.Bound;}
                else if(token!=null){offer.TokenUid=token.UID;offer.Bound=token.Bound;}
                else {dialog.Text(AbyssTrialQuest.Enabled?"Complete Arthur's Abyss trial, or bring an unlocked Exemption Token.":"Bring an unlocked Exemption Token in your bag.");return;}
                t.Commit();
            }}
            offer.Stage = 2;
            dialog.Text((offer.Quest.HasValue?"Your Abyss trial has earned a third life. Rise at level 15 as a ":"One Exemption Token begins your third life at level 15 as a ") + RebirthRules.Names[Array.IndexOf(RebirthRules.Classes, offer.Class)] + ". A bound P7 soul awaits you. Are you ready?");
            dialog.Option("Begin my third life.", 180);
        }
        private static void Complete(GameState p, Offer offer, Npcs dialog) {
            offers.Remove(p);
            if (offer.Stage != 2 || offer.Cache != Cache(p)) { dialog.Text("Your belongings have changed. Let us begin again."); return; }
            var token = offer.Quest.HasValue?null:p.Inventory.Objects.SingleOrDefault(i => i.UID == offer.TokenUid && Token(i));
            if (!offer.Quest.HasValue && token == null) return;
            var gear = p.Equipment.GetItems().Select(i => new Gear { Item = i, Type = FirstRebirth.Lower(i), Position = i.Position }).ToArray();
            var added = new List<ConquerItem>();
            // Pirate has no rebirth weapon; do not copy the obsolete legacy blade grant.
            uint weapon = offer.Class == 11 || offer.Class == 21 ? 410077u : offer.Class == 41 ? 500057u
                : offer.Class == 132 || offer.Class == 142 ? 421077u : 0;
            if (weapon != 0) { var item = Q.New(weapon, offer.Bound); item.Effect = offer.Class == 41 ? Enums.ItemEffect.Shield
                : offer.Class == 132 ? Enums.ItemEffect.MP : offer.Class == 142 ? Enums.ItemEffect.HP : Enums.ItemEffect.Poison; added.Add(item); }
            long weaponUid=added.Count==0?0L:(long)added[0].UID;
            var soul=QuestSoulReward.Create();added.Add(soul);
            var hands = gear.Where(g => g.Position == 5 || g.Position == 25).ToArray();
            if (p.Inventory.Count - (token!=null && token.StackSize <= 1 ? 1 : 0) + hands.Length + added.Count > 40) { dialog.Text("Make room in your bag for your new life's gifts and off-hand equipment."); return; }
            var profile = CharacterProgression.PreserveEarnedAttributes(SecondRebirthRules.Profile(p.Entity.FirstRebornClass, p.Entity.FirstRebornLevel, p.Entity.Class, p.Entity.Level, offer.Class), CharacterProgression.Capture(p));
            var skills = SecondRebirthSkills.Plan(p, offer.Class);
            var profs = p.Proficiencies.Values.Select(s => new Proficiency(true) { ID = s.ID, PreviousLevel = Math.Max(s.Level, s.PreviousLevel), Level = 0, Experience = 0, Available = s.Available }).ToArray();
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                SkillTable.LockOwner(c, t, p);
                if (Rows(c, t, p, "items") != offer.Items || Rows(c, t, p, "skills") != offer.Skills || Rows(c, t, p, "profs") != offer.Profs)
                    throw new InvalidOperationException("Durable rebirth belongings changed.");
                using (var q = Q.Command(c, t, "SELECT mapid FROM entities WHERE uid=@uid", "uid", (long)p.Entity.UID))
                    if (Convert.ToUInt32(q.ExecuteScalar()) != 1002) throw new InvalidOperationException("Rebirth owner moved.");
                AbyssRebirthPayment.Consume(c,t,p,offer.Quest,offer.QuestVersion,offer.Bound);
                AchievementCatalog.PreserveMasteries(c, t, p);
                CharacterProgression.Persist(c, t, p, profile);
                if(token!=null) {
                    string debit = token.StackSize <= 1 ? "DELETE FROM items" : "UPDATE items SET stacksize=stacksize-1";
                    if (Q.Execute(c, t, debit + @" WHERE uid=@item AND entityid=@uid AND id=723701 AND position=0 AND warehouse=0 AND COALESCE(stacksize,0)=@stack AND COALESCE(bound,0)=@bound AND COALESCE(locked,0)=0
AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                        "item", (long)token.UID, "uid", (long)p.Entity.UID, "stack", (int)token.StackSize, "bound", token.Bound ? 1 : 0) != 1)
                        throw new InvalidOperationException("Rebirth token changed.");
                }
                foreach (var g in gear)
                    if (Q.Execute(c, t, "UPDATE items SET id=@type,position=@next WHERE uid=@item AND entityid=@uid AND id=@old AND position=@slot AND warehouse=0",
                        "type", (int)g.Type, "next", g.Position == 5 || g.Position == 25 ? 0 : (int)g.Position,
                        "item", (long)g.Item.UID, "uid", (long)p.Entity.UID, "old", (int)g.Item.ID, "slot", (int)g.Position) != 1)
                        throw new InvalidOperationException("Rebirth equipment changed.");
                foreach (var item in added)
                    if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability,effect)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max,@effect)", "item", (long)item.UID, "type", (int)item.ID, "uid", (long)p.Entity.UID,
                        "bound", item.Bound ? 1 : 0, "dur", (int)item.Durability, "max", (int)item.MaximDurability, "effect", (int)item.Effect) != 1)
                        throw new InvalidOperationException("Rebirth gift was not saved.");
                foreach (var old in p.Spells.Values.Where(s => !SkillTable.DerivedRuneSkill(s.ID) && !skills.Any(n => n.ID == s.ID)))
                    if (old.Available && Q.Execute(c, t, "DELETE FROM skills WHERE entityid=@uid AND id=@id", "uid", (long)p.Entity.UID, "id", (int)old.ID) != 1)
                        throw new InvalidOperationException("Rebirth skill removal failed.");
                foreach (var skill in skills) SkillTable.Persist(c, t, p, skill);
                foreach (var prof in profs) {
                    string sql = prof.Available ? "UPDATE profs SET level=0,previouslevel=@previous,experience=0 WHERE entityid=@uid AND id=@id"
                        : "INSERT INTO profs(entityid,id,level,previouslevel,experience) VALUES(@uid,@id,0,@previous,0)";
                    if (Q.Execute(c, t, sql, "uid", (long)p.Entity.UID, "id", (int)prof.ID, "previous", (int)prof.PreviousLevel) != 1)
                        throw new InvalidOperationException("Rebirth proficiency reset failed.");
                }
                if (Q.Execute(c, t, @"INSERT INTO revora_second_rebirths(uid,first_class,first_level,old_class,old_level,old_experience,new_class,token_uid,bound,reward_item,quest_run,payment_kind,soul_uid,created_at)
VALUES(@uid,@first,@firstlevel,@old,@level,@exp,@new,@token,@bound,@reward,@quest,@payment,@soul,@created)", "created", Clock(), "uid", (long)p.Entity.UID,
                    "first", (int)p.Entity.FirstRebornClass, "firstlevel", (int)p.Entity.FirstRebornLevel,
                    "old", (int)p.Entity.Class, "level", (int)p.Entity.Level, "exp", checked((long)p.Entity.Experience), "new", (int)offer.Class,
                    "token", token==null?0L:(long)token.UID, "bound", offer.Bound, "reward", weaponUid, "soul", (long)soul.UID,
                    "quest",offer.Quest.HasValue?(object)offer.Quest.Value:DBNull.Value,"payment",offer.Quest.HasValue?"abyss-quest":"exemption-token") != 1)
                    throw new InvalidOperationException("Second-rebirth receipt was not saved.");
                // The actual rite earns 10316; only the owned final death earns 11031.
                if (!ProgressionEvidence.Complete(c, t, p.Entity.UID, 10316, "second-rebirth-v1", offer.Quest.HasValue?"abyss-quest":"exemption-token", 1))
                    throw new InvalidOperationException("Second-rebirth achievement already exists or was not saved.");
                var end = Clock();
                if (!Near(p) || end >= offer.Until || offer.Cache != Cache(p)) throw new InvalidOperationException("Rebirth session changed.");
                t.Commit();
            }}
            if(token!=null) {
                Q.Install(p, token.StackSize <= 1 ? new[] { token.UID } : new uint[0], new ConquerItem[0]);
                if (token.StackSize > 1) { token.StackSize--; token.Mode = Enums.ItemMode.Update; token.Send(p); token.Mode = Enums.ItemMode.Default; }
            }
            foreach (var g in gear) {
                g.Item.ID = g.Type;
                if (g.Position == 5 || g.Position == 25) {
                    if (!p.Equipment.Remove(g.Position, true) || !p.Inventory.Add(g.Item, Enums.ItemUse.None)) throw new InvalidOperationException("Committed off-hand requires reload.");
                }
                g.Item.Mode = Enums.ItemMode.Update; g.Item.Send(p); g.Item.Mode = Enums.ItemMode.Default;
            }
            Q.Install(p, new uint[0], added);
            foreach (var old in p.Spells.Values.ToArray()) if (!skills.Any(s => s.ID == old.ID)) {
                p.Spells.Remove(old.ID); p.Send(new Data(true) { UID = p.Entity.UID, dwParam = old.ID, ID = 0x6d });
            }
            foreach (var s in skills) { s.Available = true; p.Spells[s.ID] = s; if (s.ID != 3060) s.Send(p); }
            foreach (var s in profs) { s.Available = true; p.Proficiencies[s.ID] = s; s.Send(p); }
            CharacterProgression.ApplyCommitted(p, profile);
            RebirthSkillRules.ClearAssassinModes(p);RebirthSkillRules.ClearNinjaModes(p);RebirthSkillRules.ClearMonkModes(p);RebirthSkillRules.ClearPirateModes(p);WarriorModes.Clear(p);
            p.LoadItemStats(); p.CalculateStatBonus(); p.CalculateHPBonus(); p.GemAlgorithm(); p.Equipment.UpdateEntityPacket();
            PathHuntSystem.SyncQuestList(p); SoulTrialSystem.SyncQuestList(p); CourierLoopSystem.SyncQuestList(p); Q.Refresh(p);
            AbyssTrialJournal.Refresh(p);
            dialog.Text("Your former life is remembered. Walk your new path well.");
        }
        internal static bool HandleNpc(GameState p, NpcRequest request) {
            if (p == null || request == null || p.ItemSyncRoot == null) return false;
            bool handled = p.ActiveNpc == HeavenlyStoneCrafting.Sereth && (request.OptionID == 2 || request.OptionID == 9 || request.OptionID >= 170 && request.OptionID <= 180);
            lock (p.ItemSyncRoot) {
                if (!handled) { offers.Remove(p); return false; }
                if (request.NpcID != 0 && request.NpcID != HeavenlyStoneCrafting.Sereth || !Near(p)) { offers.Remove(p); return true; }
                try {
                    var dialog = new Npcs(p); dialog.Avatar(15); Offer offer;
                    if (!SecondRebirthRules.Eligible(p.Entity.Class, p.Entity.Level, p.Entity.Reborn)) {
                        offers.Remove(p); dialog.Text("Reach level 120 and your highest rank in your second life.");
                    } else if (request.OptionID == 2 || request.OptionID == 9) {
                        offers.Remove(p); offers.Add(p, new Offer { Stage = 1, Until = Clock().AddMinutes(2) });
                        dialog.Text(AbyssTrialQuest.Enabled?"Your Abyss trial or an Exemption Token can begin a third life. Which path calls to you?":"An Exemption Token begins your third life. Which path calls to you?");
                        for (int i = 0; i < RebirthRules.Classes.Length; i++) dialog.Option(RebirthRules.Names[i] + ".", (byte)(170 + i));
                    } else if (!offers.TryGetValue(p, out offer) || Clock() >= offer.Until) {
                        offers.Remove(p); dialog.Text("Let us speak of your second rebirth again.");
                    } else if (offer.Stage == 1 && request.OptionID >= 170 && request.OptionID <= 179) {
                        offer.Class = RebirthRules.Classes[request.OptionID - 170]; Ready(p, offer, dialog);
                    } else if (request.OptionID == 180) Complete(p, offer, dialog);
                    else { offers.Remove(p); dialog.Text("Let us begin again."); }
                    dialog.Option("Farewell.", 255); dialog.Send();
                } catch (Exception ex) { offers.Remove(p); Q.Fail(p, "second-rebirth", ex); }
            }
            return true;
        }
    }
}
