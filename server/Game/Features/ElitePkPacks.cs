using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Existing owned-pack rewards. Issuing these from a tournament requires a
    // separate verified placement/claim contract; possession alone grants no honor.
    internal static class ElitePkPacks
    {
        internal static void Initialize() {
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_epk_pack_openings(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),pack_uid bigint NOT NULL,pack_type integer NOT NULL CHECK(pack_type BETWEEN 720714 AND 720729),
before_stack integer NOT NULL CHECK(before_stack>=0),experience bigint NOT NULL CHECK(experience>=0),cp bigint NOT NULL CHECK(cp>=0),
rewards bigint[] NOT NULL,opened_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);"); }
        }
        internal static Tuple<uint, bool>[] Rewards(uint id, bool bound) {
            int rank = (int)(id - 720714) % 4; var items = new List<Tuple<uint, bool>>();
            Action<uint, int, bool> add = (type, count, forced) => { for (int i = 0; i < count; i++) items.Add(Tuple.Create(type, bound || forced)); };
            if (id < 720726) {
                if (rank == 3) add(1088000, 1, false);
                add(723341, new[] {1,2,3,5}[rank], false); add(729626, 1, false); add(720731, rank, false);
            } else {
                if (rank == 3) add(720049, 1, false);
                if (rank < 2) add(723700, rank == 0 ? 3 : 5, true); else add(720730, rank == 2 ? 1 : 3, true);
                add(720731, new[] {1,2,3,5}[rank], false); add(729626, new[] {1,2,3,5}[rank], false);
                if (rank >= 2) add(1088000, rank == 2 ? 1 : 3, false);
                add(720598, new[] {1,1,2,3}[rank], false);
                add(rank == 0 ? 723341u : 723342u, rank == 3 ? 3 : 1, false);
            }
            return items.ToArray();
        }
        private static bool Ready(GameState p) {
            return Q.Player(p) && CharacterProgression.Managed(p) && p.Spells != null && p.Booth == null && p.Trade != null && !p.Trade.InTrade;
        }
        internal static bool TryUse(ConquerItem item, GameState p) {
            if (item == null || item.ID < 720714 || item.ID > 720729) return false;
            if (!Ready(p)) return true;
            lock (p.ItemSyncRoot) lock (MentorExperience.Sync) try {
                ConquerItem owned;
                if (!Ready(p) || !p.Inventory.TryGetItem(item.UID, out owned) || !ReferenceEquals(owned, item)
                    || item.Position != 0 || item.Warehouse != 0 || item.Lock != 0 || item.Suspicious || item.InWardrobe
                    || item.Days != 0 && DateTime.Now >= item.DayStamp.AddDays(item.Days)) return true;
                bool experience = item.ID < 720726;
                if (experience && (p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Cursed)
                    || Config.TrainingGroundBlocksExperience && p.Map.BaseID == 1039)) {
                    p.Send("Keep this prize until you can receive its experience."); return true;
                }
                var contents = Rewards(item.ID, item.Bound);
                int spaces = contents.Length - (item.StackSize <= 1 ? 1 : 0);
                if (p.Inventory.Count + spaces > 40) { p.Send("Make " + spaces + " spaces in your bag for this prize."); return true; }
                uint cp = experience ? 0 : item.ID == 720726 ? 3000000u : 5000000u;
                ulong nextCp = (ulong)p.Entity.ConquerPoints + cp;
                if (nextCp > uint.MaxValue) { p.Send("Spend some CPs before opening this prize."); return true; }
                ulong award = 0;
                if (experience && p.Entity.Level < Config.LevelCap) {
                    ulong curve = DataHolder.LevelExperience(p.Entity.Level);
                    switch ((item.ID - 720714) % 4) { case 0: award = curve / 100 * 15; break; case 1: award = curve / 10 * 3; break; case 2: award = curve / 2; break; default: award = curve; break; }
                }
                var plan = experience ? new ExperienceRewardPlan(CharacterProgression.Capture(p), award, Config.LevelCap,
                    Config.AttributePointsPerLevel, ReincarnationRecovery.Snapshot(p)) : null;
                ulong autoHunt = p.Entity.InAutoHunt ? checked(p.Entity.AutoHuntEXP + award / Math.Max(1u, Config.AutoHuntExperienceDivisor)) : p.Entity.AutoHuntEXP;
                var rewards = contents.Select(r => Q.New(r.Item1, r.Item2)).ToArray();
                ExperienceRewards.Effects effects = null;
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    if (experience) CharacterProgression.Persist(c, t, p, plan.After);
                    else if (Q.Execute(c, t, "UPDATE entities SET conquerpoints=@next WHERE uid=@uid AND conquerpoints=@old",
                        "next", (long)nextCp, "uid", (long)p.Entity.UID, "old", (long)p.Entity.ConquerPoints) != 1)
                        throw new InvalidOperationException("Prize CP balance changed.");
                    var ids = new List<uint>();
                    using (var q = Q.Command(c, t, "SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid", "uid", (long)p.Entity.UID))
                    using (var r = q.ExecuteReader()) while (r.Read()) { if (ids.Count >= 40) throw new InvalidOperationException("Prize inventory is over capacity."); ids.Add(checked((uint)Convert.ToInt64(r[0]))); }
                    if (!ids.SequenceEqual(p.Inventory.Objects.Select(i => i.UID).OrderBy(i => i))) throw new InvalidOperationException("Prize inventory requires reload.");
                    string debit = item.StackSize <= 1 ? "DELETE FROM items" : "UPDATE items SET stacksize=stacksize-1";
                    if (Q.Execute(c, t, debit + @" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item", (long)item.UID, "uid", (long)p.Entity.UID, "type", (int)item.ID, "stack", (int)item.StackSize,
                        "bound", item.Bound ? 1 : 0, "days", item.Days.ToString(CultureInfo.InvariantCulture),
                        "permanent", item.Days == 0, "stamp", item.DayStamp.ToBinary()) != 1)
                        throw new InvalidOperationException("Prize pack ownership changed.");
                    foreach (var reward in rewards) {
                        if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max)", "item", (long)reward.UID, "type", (int)reward.ID, "uid", (long)p.Entity.UID,
                            "bound", reward.Bound ? 1 : 0, "dur", (int)reward.Durability, "max", (int)reward.MaximDurability) != 1)
                            throw new InvalidOperationException("Prize item was not saved.");
                    }
                    if (experience) { effects = ExperienceRewards.Persist(c, t, p, plan); ReincarnationRecovery.Consume(c, t, p, plan.Restored); }
                    if (Q.Execute(c, t, @"INSERT INTO revora_epk_pack_openings(id,uid,pack_uid,pack_type,before_stack,experience,cp,rewards)
VALUES(@id,@uid,@pack,@type,@stack,@exp,@cp,@rewards)", "id", Guid.NewGuid(), "uid", (long)p.Entity.UID,
                        "pack", (long)item.UID, "type", (int)item.ID, "stack", (int)item.StackSize, "exp", checked((long)award),
                        "cp", (long)cp, "rewards", rewards.Select(r => (long)r.UID).ToArray()) != 1)
                        throw new InvalidOperationException("Prize opening receipt was not saved.");
                    t.Commit();
                }}
                Q.Install(p, item.StackSize <= 1 ? new[] { item.UID } : new uint[0], rewards);
                if (item.StackSize > 1) { item.StackSize--; item.Mode = Enums.ItemMode.Update; item.Send(p); item.Mode = Enums.ItemMode.Default; }
                if (experience) { ReincarnationRecovery.Committed(p, plan.Restored); MentorExperience.Apply(p, effects.MentorCredit); p.ApplyExperienceReward(plan, effects, autoHunt, true); }
                else p.Entity.ApplyCommittedConquerPoints((uint)nextCp);
            } catch (Exception ex) { Q.Fail(p, "elite-prize-pack", ex); }
            return true;
        }
    }
}
