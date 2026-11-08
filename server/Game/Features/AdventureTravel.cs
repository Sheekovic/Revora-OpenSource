using System;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class AdventureTravel
    {
        private static bool enabled;
        internal static void Initialize() {
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Q.Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_adventure_passages(
uid bigint PRIMARY KEY CHECK(uid>0),pending boolean NOT NULL,paid_at timestamptz NOT NULL DEFAULT now(),
trips bigint NOT NULL CHECK(trips>0),silver_before bigint NOT NULL CHECK(silver_before>=1000),
silver_after bigint NOT NULL CHECK(silver_after=silver_before-1000));
ALTER TABLE revora_adventure_passages ADD COLUMN IF NOT EXISTS npc_id integer NOT NULL DEFAULT 9884 CHECK(npc_id BETWEEN 9884 AND 9887);
ALTER TABLE revora_adventure_passages ADD COLUMN IF NOT EXISTS destination integer NOT NULL DEFAULT 1216 CHECK(destination IN(1216,1015,1217,1001));
CREATE OR REPLACE FUNCTION revora_adventure_arrived() RETURNS trigger LANGUAGE plpgsql SET search_path FROM CURRENT AS $$
DECLARE touched integer;
BEGIN
 IF EXISTS(SELECT 1 FROM revora_adventure_passages WHERE uid=NEW.uid AND pending AND destination=NEW.mapid) THEN
  UPDATE revora_adventure_passages SET pending=false WHERE uid=NEW.uid AND pending;
  GET DIAGNOSTICS touched=ROW_COUNT;
  IF touched<>1 THEN RAISE EXCEPTION 'Adventure arrival was not saved'; END IF;
 END IF;
 RETURN NEW;
END $$;
DO $revora_trigger$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_adventure_arrived') THEN
CREATE TRIGGER revora_adventure_arrived AFTER UPDATE OF mapid ON entities FOR EACH ROW EXECUTE FUNCTION revora_adventure_arrived();
ELSIF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_adventure_arrived' AND NOT tgisinternal
AND tgenabled='O' AND tgtype=17 AND tgfoid='revora_adventure_arrived()'::regprocedure
AND tgnargs=0 AND tgqual IS NULL AND tgattr::text=((SELECT attnum::text FROM pg_attribute WHERE attrelid='entities'::regclass AND attname='mapid' AND NOT attisdropped))) THEN
RAISE EXCEPTION 'Revora trigger definition mismatch: revora_adventure_arrived';
END IF; END $revora_trigger$;");
                t.Commit();
            }}
            enabled=true;
        }
        private static bool Landing(ushort id, ref ushort x, ref ushort y) {
            Map map; MapsTable.MapInformation info; ushort profile = id == 1217 ? (ushort)1075 : id == 1219 ? (ushort)1212 : id;
            return Kernel.Maps.TryGetValue(id, out map) && map.ID == id && map.BaseID == profile && map.Floor != null
                && MapsTable.MapInformations.TryGetValue(id, out info) && info.ID == id && info.BaseID == profile && info.Owner == 0
                && PotencyGuide.FindLanding(map, ref x, ref y);
        }
        private static bool Near(GameState p, uint npc, ushort origin) {
            return Q.Near(p, npc, origin) && p.Map.ID == origin && p.Map.BaseID == (origin == 1219 ? 1212 : origin)
                && p.Entity.PokerTable == 0;
        }
        internal static bool PreparePortal(GameState p, Portal portal, ref ushort x, ref ushort y) {
            if (p == null || p.Entity == null || p.Entity.MapID < 1210 || p.Entity.MapID > 1212) return true;
            if (!Q.Player(p) || p.Map.ID != p.Entity.MapID || p.Map.BaseID != p.Map.ID || portal == null
                || portal.CurrentMapID != p.Map.ID || p.Trade == null || p.Trade.InTrade || p.Booth != null || p.Entity.PokerTable != 0
                || !Landing(portal.DestinationMapID, ref x, ref y)) {
                p.Send("The crossing is closed for now."); return false;
            }
            return true;
        }
        private static void Paid(GameState p, uint npc, ushort target, ushort x, ushort y) {
            bool paid = false; ulong after = p.Entity.Money;
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                SkillTable.LockOwner(c, t, p);
                using (var q = Q.Command(c, t, "SELECT mapid FROM entities WHERE uid=@uid", "uid", (long)p.Entity.UID))
                    if (Convert.ToUInt32(q.ExecuteScalar()) != 1038) throw new InvalidOperationException("Adventure visitor location changed.");
                bool pending=false;
                using (var q = Q.Command(c, t, "SELECT pending,npc_id,destination FROM revora_adventure_passages WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID))
                using(var r=q.ExecuteReader())if(r.Read()) {
                    pending=r.GetBoolean(0);
                    if(pending && (r.GetInt32(1)!=npc || r.GetInt32(2)!=target)) {
                        p.Send("Another conductor still holds your paid passage. Return there to finish your journey.");return;
                    }
                }
                if (!pending) {
                    ulong before = p.Entity.Money;
                    if (before < 1000) { p.Send("The fare is 1,000 silver."); return; }
                    after = before - 1000; SilverBalance.Persist(c, t, p, after);
                    if (Q.Execute(c, t, @"INSERT INTO revora_adventure_passages(uid,pending,trips,silver_before,silver_after,npc_id,destination)
VALUES(@uid,true,1,@before,@after,@npc,@target) ON CONFLICT(uid) DO UPDATE SET pending=true,paid_at=now(),
trips=revora_adventure_passages.trips+1,silver_before=EXCLUDED.silver_before,silver_after=EXCLUDED.silver_after,npc_id=EXCLUDED.npc_id,destination=EXCLUDED.destination",
                        "uid", (long)p.Entity.UID, "before", checked((long)before), "after", checked((long)after),"npc",(int)npc,"target",(int)target) != 1)
                        throw new InvalidOperationException("Adventure fare was not saved.");
                    paid = true;
                }
                if (!Near(p, npc, 1038)) throw new InvalidOperationException("Adventure visitor changed.");
                t.Commit();
            }}
            if (paid) SilverBalance.ApplyCommitted(p, after);
            p.Entity.Teleport(target, x, y);
            if (p.Entity.MapID != target || p.Entity.X != x || p.Entity.Y != y) throw new InvalidOperationException("Adventure passage requires resuming.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT EXISTS(
SELECT 1 FROM revora_adventure_passages r JOIN entities e ON e.uid=r.uid
WHERE r.uid=@uid AND NOT r.pending AND r.npc_id=@npc AND r.destination=@target AND e.mapid=@target)",
                "uid",(long)p.Entity.UID,"npc",(int)npc,"target",(int)target))
                if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Guild passage arrival requires reload.");}
            RecoverAchievement(p);
        }
        internal static void RecoverAchievement(GameState p) {
            if(!enabled || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try {
                if(!Q.Player(p,false))return;bool added;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);int npc,target;long trips;
                    using(var q=Q.Command(c,t,"SELECT npc_id,destination,trips FROM revora_adventure_passages WHERE uid=@uid AND NOT pending FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader()){if(!r.Read())return;npc=r.GetInt32(0);target=r.GetInt32(1);trips=r.GetInt64(2);}
                    if(trips<1 || (npc==9884?target!=1216:npc==9885?target!=1015:npc==9886?target!=1217:npc==9887?target!=1001:true))
                        throw new InvalidOperationException("Guild passage source is invalid.");
                    added=ProgressionEvidence.Complete(c,t,p.Entity.UID,10117,"guild-transporter-v1",npc+":"+trips,1);
                    t.Commit();
                }}
                if(added)Q.Refresh(p);
            }catch(Exception ex){Q.Fail(p,"guild-passage-achievement",ex);}
        }
        internal static bool HandleNpc(GameState p, NpcRequest request) {
            if (p == null || request == null) return false;
            uint id = p.ActiveNpc; ushort origin, target, x, y; string words;
            switch (id) {
                case 9884: origin = 1038; target = 1216; x = 12; y = 481; words = "For 1,000 silver, I can take you to Old Explorer and the Adventure Islands."; break;
                case 9885: origin = 1038; target = 1015; x = 792; y = 573; words = "For 1,000 silver, I can carry you to Bird Island."; break;
                case 9886: origin = 1038; target = 1217; x = 536; y = 558; words = "For 1,000 silver, I can carry you to the distant hunting grounds."; break;
                case 9887: origin = 1038; target = 1001; x = 337; y = 325; words = "For 1,000 silver, I can carry you to Mystic Castle."; break;
                case 300655: origin = 1216; target = 1210; x = 1029; y = 714; words = "The island portals lead deeper into wild country. Waterlord waits beyond them. Shall we go?"; break;
                case 300653: origin = 1212; target = 1219; x = 448; y = 272; words = "The boatman waits beyond this crossing. He can take you home."; break;
                case 300654: origin = 1219; target = 1002; x = 408; y = 330; words = "Ready to return to Twin City? Step aboard."; break;
                default: return false;
            }
            if (p.ItemSyncRoot == null) return true;
            lock (p.ItemSyncRoot) try {
                if (request.NpcID != 0 && request.NpcID != id || !Near(p, id, origin)) return true;
                if (request.OptionID == 0) {
                    var dialog = new Npcs(p); dialog.Text(words); dialog.Option("Take me there.", 1); dialog.Option("Not yet.", 255); dialog.Send();
                } else if (request.OptionID == 1) {
                    if (!Landing(target, ref x, ref y)) { p.Send("The crossing is closed for now."); return true; }
                    if (id >= 9884 && id <= 9887) Paid(p,id,target,x,y); else p.Entity.Teleport(target, x, y);
                }
            } catch (Exception ex) { Q.Fail(p, "adventure-travel", ex); }
            return true;
        }
    }
}
