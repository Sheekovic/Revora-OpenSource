using System;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Preserve the existing fare, with a resumable passage to the Moon Platform.
    internal static class MoonPlatformPassage
    {
        internal static void Initialize() {
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Q.Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_moon_platform_passages(
uid bigint PRIMARY KEY CHECK(uid>0),pending boolean NOT NULL,paid_at timestamptz NOT NULL DEFAULT now(),
trips bigint NOT NULL CHECK(trips>0),fare bigint NOT NULL CHECK(fare>=0),silver_before bigint NOT NULL CHECK(silver_before>=fare),
silver_after bigint NOT NULL CHECK(silver_after=silver_before-fare));
CREATE OR REPLACE FUNCTION revora_moon_platform_arrived() RETURNS trigger LANGUAGE plpgsql SET search_path FROM CURRENT AS $$
BEGIN
 IF NEW.mapid=1103 THEN UPDATE revora_moon_platform_passages SET pending=false WHERE uid=NEW.uid AND pending; END IF;
 RETURN NEW;
END $$;
DO $revora_trigger$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_moon_platform_arrived') THEN
CREATE TRIGGER revora_moon_platform_arrived AFTER UPDATE OF mapid ON entities FOR EACH ROW EXECUTE FUNCTION revora_moon_platform_arrived();
ELSIF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_moon_platform_arrived' AND NOT tgisinternal
AND tgenabled='O' AND tgtype=17 AND tgfoid='revora_moon_platform_arrived()'::regprocedure
AND tgnargs=0 AND tgqual IS NULL AND tgattr::text=((SELECT attnum::text FROM pg_attribute WHERE attrelid='entities'::regclass AND attname='mapid' AND NOT attisdropped))) THEN
RAISE EXCEPTION 'Revora trigger definition mismatch: revora_moon_platform_arrived';
END IF; END $revora_trigger$;");
                t.Commit();
            }}
        }
        private static bool Near(GameState p) {
            if(p==null || p.Entity==null)return false;
            ushort origin=p.Entity.MapID;
            return origin==1002
                && p.ActiveNpc==4178 && Q.Near(p,p.ActiveNpc,origin)
                && p.Map.ID==origin && p.Map.BaseID==origin && p.Entity.PokerTable==0;
        }
        internal static bool HandleNpc(GameState p, NpcRequest request) {
            if (p == null || request == null || p.ActiveNpc != 4178) return false;
            if (p.ItemSyncRoot == null) return true;
            lock (p.ItemSyncRoot) try {
                if (request.NpcID != 0 && request.NpcID != p.ActiveNpc || !Near(p)) return true;
                ulong fare=Config.LunaTeleportGold;
                if(fare>long.MaxValue){p.Send("The passage is closed for now.");return true;}
                if(request.OptionID==0 || request.OptionID==1){var dialog=new Npcs(p);dialog.Text("The moon shines brightest above the clouds. Passage costs "+fare.ToString("N0")+" silver. I will honor a fare you have already paid.");dialog.Option("Take me there.",2);dialog.Option("Not now.",255);dialog.Avatar(114);dialog.Send();return true;}
                if(request.OptionID!=2)return true;
                Map map; MapsTable.MapInformation info; ushort x = 188, y = 165;
                if (!Kernel.Maps.TryGetValue(1103, out map) || map.ID != 1103 || map.BaseID != 1103
                    || !MapsTable.MapInformations.TryGetValue(1103, out info) || info.ID != 1103 || info.BaseID != 1103 || info.Owner != 0
                    || !PotencyGuide.FindLanding(map, ref x, ref y)) { p.Send("The moon road is closed for now."); return true; }
                bool paid = false; ulong after = p.Entity.Money;
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    SkillTable.LockOwner(c, t, p);
                    using (var q = Q.Command(c, t, "SELECT mapid FROM entities WHERE uid=@uid", "uid", (long)p.Entity.UID))
                        if (Convert.ToUInt32(q.ExecuteScalar()) != p.Entity.MapID) throw new InvalidOperationException("Moon Platform visitor location changed.");
                    bool pending;
                    using (var q = Q.Command(c, t, "SELECT pending FROM revora_moon_platform_passages WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID))
                        pending = Equals(q.ExecuteScalar(), true);
                    if (!pending) {
                        ulong before = p.Entity.Money;
                        if (before < fare) { p.Send("The fare is "+fare.ToString("N0")+" silver."); return true; }
                        after = before - fare; SilverBalance.Persist(c, t, p, after);
                        if (Q.Execute(c, t, @"INSERT INTO revora_moon_platform_passages(uid,pending,trips,fare,silver_before,silver_after)
VALUES(@uid,true,1,@fare,@before,@after) ON CONFLICT(uid) DO UPDATE SET pending=true,paid_at=now(),
trips=revora_moon_platform_passages.trips+1,fare=EXCLUDED.fare,silver_before=EXCLUDED.silver_before,silver_after=EXCLUDED.silver_after",
                            "uid", (long)p.Entity.UID,"fare",checked((long)fare), "before", checked((long)before), "after", checked((long)after)) != 1)
                            throw new InvalidOperationException("Moon Platform passage was not saved.");
                        paid = true;
                    }
                    if (!Near(p)) throw new InvalidOperationException("Moon Platform visitor changed.");
                    t.Commit();
                }}
                if (paid) SilverBalance.ApplyCommitted(p, after);
                p.Entity.Teleport(1103, x, y);
                if (p.Entity.MapID != 1103 || p.Entity.X != x || p.Entity.Y != y) throw new InvalidOperationException("Moon Platform passage requires resuming.");
            } catch (Exception ex) { Q.Fail(p, "moon-platform-passage", ex); }
            return true;
        }
    }
}
