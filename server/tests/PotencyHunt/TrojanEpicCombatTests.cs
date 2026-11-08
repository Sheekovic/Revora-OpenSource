using System;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type TrojanCombat = typeof(Entity).Assembly.GetType("Revora.Game.Features.TrojanEpicCombat");
    static bool trojanBattleNegatives, trojanUnarmedChecked;
    static void TrojanCombatCatalog() {
        using (var c = DataHolder.MySqlConnection) { c.Open(); using (var q = new Npgsql.NpgsqlCommand(
            "SELECT id,name,lookface FROM public.monsterinfos WHERE id IN(7483,7484,7487)", c))
        using (var r = q.ExecuteReader()) while (r.Read()) {
            uint id = Convert.ToUInt32(r[0]);
            MonsterInformation.MonsterInformations[id] = new MonsterInformation { ID = id, Name = Convert.ToString(r[1]), Mesh = Convert.ToUInt16(r[2]) };
        }}
        Call(TrojanCombat, null, "ValidateCatalog");
        MonsterInformation.MonsterInformations[9916801] = new MonsterInformation { ID = 9916801 };
        bool denied = false;
        try { Call(TrojanCombat, null, "ValidateCatalog"); } catch (InvalidOperationException) { denied = true; }
        finally { MonsterInformation.MonsterInformations.Remove(9916801); }
        Check(denied, "Trojan private monster families cannot collide with loaded catalog entries");
    }
    static Entity TrojanSpawn(GameState p, object scene) {
        lock (p.ItemSyncRoot) return (Entity)Call(TrojanCombat, null, "Spawn", scene, p);
    }
    static bool TrojanCombatAdvance(GameState p) {
        int before = TrojanValue(p, "Revision"), stage = TrojanValue(p, "Stage");
        var scene = TrojanScene(p); var map = (Map)Field(scene, "Map");
        TrojanTravel(p, scene, "Enter");
        try {
            var body = TrojanSpawn(p, scene);
            if (body == null) return false;
            AdventureWalk(p, 45, 42);
            Check(ReferenceEquals(body, TrojanSpawn(p, scene)) && body.MaxHitpoints == (stage == 6 ? 150000u : stage == 8 ? 350000u : 1000000u)
                && body.Body == (stage == 6 ? 979 : stage == 8 ? 981 : 978) && body.MonsterInfo.Boss == (stage != 6),
                "owned Trojan encounter uses reviewed model and explicit combat profile: " + stage);
            if (!trojanBattleNegatives) {
                var outsider = TrojanPlayer(Revora.Kernel.Maps[1004]); outsider.Entity.MapID = map.ID;
                outsider.Entity.X = p.Entity.X; outsider.Entity.Y = p.Entity.Y;
                try {
                bool ownerAllowed = Revora.Game.Attacking.Handle.CanAttack(p.Entity, body, null, true);
                bool foreignAllowed = Revora.Game.Attacking.Handle.CanAttack(outsider.Entity, body, null, true);
                Check(ownerAllowed && !foreignAllowed,
                    "only registered owner can attack the Trojan encounter (owner=" + ownerAllowed + ", foreign=" + foreignAllowed
                    + ", guard=" + Call(TrojanCombat, null, "BlockAttack", p.Entity, body) + ", owns=" + Call(TrojanScenes, null, "Owns", scene, p)
                    + ", dead=" + body.Dead + ", delay=" + (Time32.Now < p.CantAttack) + ")");
                uint damage = body.Hitpoints;
                Revora.Game.Attacking.Handle.ReceiveAttack(outsider.Entity, body,
                    new Attack(true) { AttackType = Attack.Melee, Attacker = outsider.Entity.UID, Attacked = body.UID }, ref damage, null);
                body.Die(outsider.Entity);
                Check(damage == 0 && body.Hitpoints == 150000 && TrojanValue(p, "Revision") == before,
                    "foreign damage and direct death callback cannot award Trojan credit");
                } finally { outsider.Entity.MapID = 1004; }
                uint partial = 100;
                Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity, body,
                    new Attack(true) { AttackType = Attack.Melee, Attacker = p.Entity.UID, Attacked = body.UID }, ref partial, null);
                Check(body.Hitpoints == 149900 && TrojanValue(p, "Revision") == before,
                    "nonlethal owned damage changes HP without recording victory");
                p.Trade.InTrade = true; body.Die(p.Entity); p.Trade.InTrade = false;
                Check(body.Hitpoints == 149900 && TrojanValue(p, "Revision") == before,
                    "trading suspends Trojan victory credit");
                Query("CREATE FUNCTION trojan_cancel_fail() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER trojan_cancel_fail BEFORE UPDATE ON revora_trojan_epic_encounters FOR EACH ROW EXECUTE FUNCTION trojan_cancel_fail()");
                bool denied = false;
                try { lock (p.ItemSyncRoot) Call(TrojanCombat, null, "Cancel", scene); } catch (InvalidOperationException) { denied = true; }
                finally { Query("DROP TRIGGER trojan_cancel_fail ON revora_trojan_epic_encounters;DROP FUNCTION trojan_cancel_fail()"); }
                Check(denied && map.Entities.ContainsKey(body.UID), "failed durable cancellation retains actor ownership for retry");
                lock (p.ItemSyncRoot) Call(TrojanCombat, null, "Cancel", scene);
                body.Die(p.Entity);
                Check(!map.Entities.ContainsKey(body.UID) && TrojanValue(p, "Revision") == before
                    && Scalar("SELECT count(*) FROM revora_trojan_epic_encounters WHERE uid=@uid AND state='active'", p.Entity.UID) == 0,
                    "cancellation retires receipt and rejects late death without spending energy");
                body = TrojanSpawn(p, scene);
                TrojanTravel(p, scene, "Exit");
                body.Die(p.Entity);
                Check(!Revora.Kernel.Maps.ContainsKey(map.ID) && TrojanValue(p, "Revision") == before
                    && Scalar("SELECT count(*) FROM revora_trojan_epic_encounters WHERE uid=@uid AND state='active'", p.Entity.UID) == 0,
                    "leaving a live Trojan battle retires its receipt and removes the private room");
                scene = TrojanScene(p); map = (Map)Field(scene, "Map");
                TrojanTravel(p, scene, "Enter");
                AdventureWalk(p, 45, 42);
                body = TrojanSpawn(p, scene); trojanBattleNegatives = true;
            }
            uint cp = p.Entity.ConquerPoints; ulong silver = p.Entity.Money; int bag = p.Inventory.Count, drops = map.FloorItems.Count;
            bool checkUnarmed = !trojanUnarmedChecked;
            if (checkUnarmed) {
                long victories=Scalar("SELECT count(*) FROM revora_trojan_epic_encounters WHERE uid=@uid AND state='victory'",p.Entity.UID);
                int energy=TrojanValue(p,"Energy");
                Query("CREATE FUNCTION reject_trojan_unarmed() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_trojan_unarmed BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN(NEW.achievement_id=10525) EXECUTE FUNCTION reject_trojan_unarmed()");
                try{body.Die(p.Entity);}finally{Query("DROP TRIGGER reject_trojan_unarmed ON revora_achievement_evidence;DROP FUNCTION reject_trojan_unarmed()");}
                Check(Fenced(p) && !body.Dead && Scalar("SELECT revision FROM revora_trojan_epic WHERE uid=@uid",p.Entity.UID)==before
                    && Scalar("SELECT energy FROM revora_trojan_epic WHERE uid=@uid",p.Entity.UID)==energy
                    && Scalar("SELECT count(*) FROM revora_trojan_epic_encounters WHERE uid=@uid AND state='victory'",p.Entity.UID)==victories
                    && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525",p.Entity.UID)==0,"failed Trojan unarmed evidence preserves living encounter, story revision and daily energy");
                DemonReset(p);trojanUnarmedChecked=true;
            }
            SpookKill(p, body);
            if(checkUnarmed)Check(HasAchievement(p,10525),"retried real Trojan lethal hit immediately projects unarmed credit");
            int after = TrojanValue(p, "Revision");
            Check(!Fenced(p) && after == before + 1 && !map.Entities.ContainsKey(body.UID)
                && p.Entity.ConquerPoints == cp && p.Entity.Money == silver && p.Inventory.Count == bag && map.FloorItems.Count == drops,
                "real lethal attack commits one Trojan victory without legacy loot or currency: " + stage);
            body.Die(p.Entity);
            Check(TrojanValue(p, "Revision") == after, "replayed Trojan corpse cannot award another step");
            return true;
        } finally { TrojanTravel(p, scene, "Exit"); }
    }
}
