using System;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Preserve the existing fare, with a resumable passage to the actual mine.
    internal static class MiningPassage
    {
        internal static void Initialize() {
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Q.Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_plain_mine_passages(
uid bigint PRIMARY KEY CHECK(uid>0),pending boolean NOT NULL,paid_at timestamptz NOT NULL DEFAULT now(),
trips bigint NOT NULL CHECK(trips>0),silver_before bigint NOT NULL CHECK(silver_before>=100),
silver_after bigint NOT NULL CHECK(silver_after=silver_before-100));
CREATE OR REPLACE FUNCTION revora_plain_mine_arrived() RETURNS trigger LANGUAGE plpgsql SET search_path FROM CURRENT AS $$
BEGIN
 IF NEW.mapid=1028 THEN UPDATE revora_plain_mine_passages SET pending=false WHERE uid=NEW.uid AND pending; END IF;
 RETURN NEW;
END $$;
DO $revora_trigger$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_plain_mine_arrived') THEN
CREATE TRIGGER revora_plain_mine_arrived AFTER UPDATE OF mapid ON entities FOR EACH ROW EXECUTE FUNCTION revora_plain_mine_arrived();
ELSIF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_plain_mine_arrived' AND NOT tgisinternal
AND tgenabled='O' AND tgtype=17 AND tgfoid='revora_plain_mine_arrived()'::regprocedure
AND tgnargs=0 AND tgqual IS NULL AND tgattr::text=((SELECT attnum::text FROM pg_attribute WHERE attrelid='entities'::regclass AND attname='mapid' AND NOT attisdropped))) THEN
RAISE EXCEPTION 'Revora trigger definition mismatch: revora_plain_mine_arrived';
END IF; END $revora_trigger$;");
                t.Commit();
            }}
        }
        private static bool Near(GameState p) {
            return Q.Near(p, 10080, 1002) && p.Map.ID == 1002 && p.Map.BaseID == 1002 && p.Entity.PokerTable == 0;
        }
        internal static bool HandleNpc(GameState p, NpcRequest request) {
            if (p == null || request == null || p.ActiveNpc != 10080 || request.OptionID != 5) return false;
            if (p.ItemSyncRoot == null) return true;
            lock (p.ItemSyncRoot) try {
                if (request.NpcID != 0 && request.NpcID != 10080 || !Near(p)) return true;
                if (p.Entity.PKPoints >= 29) { p.Send("I cannot take you while the guards are after you."); return true; }
                if (p.Entity.Level < 70) { p.Send("The mine is dangerous. Return at level 70 with a Pickaxe."); return true; }
                Map map; MapsTable.MapInformation info; ushort x = 155, y = 95;
                if (!Kernel.Maps.TryGetValue(1028, out map) || map.ID != 1028 || map.BaseID != 1028
                    || !MapsTable.MapInformations.TryGetValue(1028, out info) || info.ID != 1028 || info.BaseID != 1028 || info.Owner != 0
                    || !PotencyGuide.FindLanding(map, ref x, ref y)) { p.Send("The mine road is closed for now."); return true; }
                bool paid = false; ulong after = p.Entity.Money;
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    SkillTable.LockOwner(c, t, p);
                    using (var q = Q.Command(c, t, "SELECT mapid FROM entities WHERE uid=@uid", "uid", (long)p.Entity.UID))
                        if (Convert.ToUInt32(q.ExecuteScalar()) != 1002) throw new InvalidOperationException("Mine visitor location changed.");
                    bool pending;
                    using (var q = Q.Command(c, t, "SELECT pending FROM revora_plain_mine_passages WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID))
                        pending = Equals(q.ExecuteScalar(), true);
                    if (!pending) {
                        ulong before = p.Entity.Money;
                        if (before < 100) { p.Send("The fare is 100 silver."); return true; }
                        after = before - 100; SilverBalance.Persist(c, t, p, after);
                        if (Q.Execute(c, t, @"INSERT INTO revora_plain_mine_passages(uid,pending,trips,silver_before,silver_after)
VALUES(@uid,true,1,@before,@after) ON CONFLICT(uid) DO UPDATE SET pending=true,paid_at=now(),
trips=revora_plain_mine_passages.trips+1,silver_before=EXCLUDED.silver_before,silver_after=EXCLUDED.silver_after",
                            "uid", (long)p.Entity.UID, "before", checked((long)before), "after", checked((long)after)) != 1)
                            throw new InvalidOperationException("Mine passage was not saved.");
                        paid = true;
                    }
                    if (!Near(p) || p.Entity.PKPoints >= 29 || p.Entity.Level < 70) throw new InvalidOperationException("Mine visitor changed.");
                    t.Commit();
                }}
                if (paid) SilverBalance.ApplyCommitted(p, after);
                p.Entity.Teleport(1028, x, y);
                if (p.Entity.MapID != 1028 || p.Entity.X != x || p.Entity.Y != y) throw new InvalidOperationException("Mine passage requires resuming.");
            } catch (Exception ex) { Q.Fail(p, "mine-passage", ex); }
            return true;
        }
    }
}
