using System;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Preserve the existing fare, with a resumable passage to the actual training ground.
    internal static class TrainingGroundPassage
    {
        internal static void Initialize() {
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Q.Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_training_ground_passages(
uid bigint PRIMARY KEY CHECK(uid>0),pending boolean NOT NULL,paid_at timestamptz NOT NULL DEFAULT now(),
trips bigint NOT NULL CHECK(trips>0),silver_before bigint NOT NULL CHECK(silver_before>=1000),
silver_after bigint NOT NULL CHECK(silver_after=silver_before-1000));
CREATE OR REPLACE FUNCTION revora_training_ground_arrived() RETURNS trigger LANGUAGE plpgsql SET search_path FROM CURRENT AS $$
BEGIN
 IF NEW.mapid=1039 THEN UPDATE revora_training_ground_passages SET pending=false WHERE uid=NEW.uid AND pending; END IF;
 RETURN NEW;
END $$;
DO $revora_trigger$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_training_ground_arrived') THEN
CREATE TRIGGER revora_training_ground_arrived AFTER UPDATE OF mapid ON entities FOR EACH ROW EXECUTE FUNCTION revora_training_ground_arrived();
ELSIF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_training_ground_arrived' AND NOT tgisinternal
AND tgenabled='O' AND tgtype=17 AND tgfoid='revora_training_ground_arrived()'::regprocedure
AND tgnargs=0 AND tgqual IS NULL AND tgattr::text=((SELECT attnum::text FROM pg_attribute WHERE attrelid='entities'::regclass AND attname='mapid' AND NOT attisdropped))) THEN
RAISE EXCEPTION 'Revora trigger definition mismatch: revora_training_ground_arrived';
END IF; END $revora_trigger$;");
                t.Commit();
            }}
        }
        private static bool Near(GameState p) {
            if(p==null || p.Entity==null)return false;
            ushort origin=p.Entity.MapID;
            return (origin==1002 || origin==1011 || origin==1020 || origin==1000 || origin==1015)
                && p.ActiveNpc>=181 && p.ActiveNpc<=185 && Q.Near(p,p.ActiveNpc,origin)
                && p.Map.ID==origin && p.Map.BaseID==origin && p.Entity.PokerTable==0;
        }
        internal static bool HandleNpc(GameState p, NpcRequest request) {
            if (p == null || request == null || p.ActiveNpc < 181 || p.ActiveNpc > 185) return false;
            if (p.ItemSyncRoot == null) return true;
            lock (p.ItemSyncRoot) try {
                if (request.NpcID != 0 && request.NpcID != p.ActiveNpc || !Near(p)) return true;
                if(request.OptionID==0){var dialog=new Npcs(p);dialog.Text("The training camp is open from level 20. The fare is 1,000 silver. An interrupted paid journey can be resumed.");dialog.Option("Send me.",1);dialog.Option("Not now.",255);dialog.Avatar(67);dialog.Send();return true;}
                if(request.OptionID!=1)return true;
                if(p.Entity.Level<20){p.Send("Come back at level 20.");return true;}
                Map map; MapsTable.MapInformation info; ushort x = 216, y = 214;
                if (!Kernel.Maps.TryGetValue(1039, out map) || map.ID != 1039 || map.BaseID != 1039
                    || !MapsTable.MapInformations.TryGetValue(1039, out info) || info.ID != 1039 || info.BaseID != 1039 || info.Owner != 0
                    || !PotencyGuide.FindLanding(map, ref x, ref y)) { p.Send("The training road is closed for now."); return true; }
                bool paid = false; ulong after = p.Entity.Money;
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    SkillTable.LockOwner(c, t, p);
                    using (var q = Q.Command(c, t, "SELECT mapid FROM entities WHERE uid=@uid", "uid", (long)p.Entity.UID))
                        if (Convert.ToUInt32(q.ExecuteScalar()) != p.Entity.MapID) throw new InvalidOperationException("Training visitor location changed.");
                    bool pending;
                    using (var q = Q.Command(c, t, "SELECT pending FROM revora_training_ground_passages WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID))
                        pending = Equals(q.ExecuteScalar(), true);
                    if (!pending) {
                        ulong before = p.Entity.Money;
                        if (before < 1000) { p.Send("The fare is 1,000 silver."); return true; }
                        after = before - 1000; SilverBalance.Persist(c, t, p, after);
                        if (Q.Execute(c, t, @"INSERT INTO revora_training_ground_passages(uid,pending,trips,silver_before,silver_after)
VALUES(@uid,true,1,@before,@after) ON CONFLICT(uid) DO UPDATE SET pending=true,paid_at=now(),
trips=revora_training_ground_passages.trips+1,silver_before=EXCLUDED.silver_before,silver_after=EXCLUDED.silver_after",
                            "uid", (long)p.Entity.UID, "before", checked((long)before), "after", checked((long)after)) != 1)
                            throw new InvalidOperationException("Training passage was not saved.");
                        paid = true;
                    }
                    if (!Near(p) || p.Entity.Level < 20) throw new InvalidOperationException("Training visitor changed.");
                    t.Commit();
                }}
                if (paid) SilverBalance.ApplyCommitted(p, after);
                p.Entity.Teleport(1039, x, y);
                if (p.Entity.MapID != 1039 || p.Entity.X != x || p.Entity.Y != y) throw new InvalidOperationException("Training passage requires resuming.");
            } catch (Exception ex) { Q.Fail(p, "training-ground-passage", ex); }
            return true;
        }
    }
}
