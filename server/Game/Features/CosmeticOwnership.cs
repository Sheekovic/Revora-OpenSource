using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Payment, durable ownership, appearance and achievement evidence share one
    // owner transaction. Client requests never supply entitlements or prices.
    internal static class CosmeticOwnership
    {
        internal sealed class Owned
        {
            internal uint Kind, Id, Colour, Colours;
            internal bool Active;
        }
        internal sealed class Result
        {
            internal Owned Entry;
            internal bool Changed, AchievementChanged;
            internal ulong Charged;
        }
        // Numeric facts from the hash-recorded installed hair_color_type.dat.
        // Style 42 occurs there but has no storage catalog row: it is not buyable.
        private static readonly HashSet<uint> colourStyles = new HashSet<uint>(new uint[] {
            1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,
            39,40,41,42,45,46,47,48,49,50,51,52,53,54,55,60,61,62
        });
        internal static bool HasColour(uint style, uint colour)
        { return colour >= 1 && colour <= 6 && colourStyles.Contains(style); }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection) {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_cosmetic_ownership(
uid bigint NOT NULL CHECK(uid>0),kind integer NOT NULL CHECK(kind IN(0,1)),
id integer NOT NULL CHECK(id BETWEEN 1 AND 65535),colour integer NOT NULL DEFAULT 0 CHECK(colour BETWEEN 0 AND 6),
colours integer NOT NULL DEFAULT 1 CHECK(colours BETWEEN 1 AND 127 AND (colours & 1)=1),
acquired_at timestamptz NOT NULL DEFAULT now(),PRIMARY KEY(uid,kind,id),
CHECK((kind=0 AND id<1000 AND (colours & (1<<colour))<>0) OR (kind=1 AND colour=0 AND colours=1)));
CREATE TABLE IF NOT EXISTS revora_cosmetic_receipts(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),action integer NOT NULL CHECK(action IN(3,4,6,7)),
kind integer NOT NULL CHECK(kind IN(0,1)),cosmetic_id integer NOT NULL CHECK(cosmetic_id BETWEEN 1 AND 65535),
colour integer NOT NULL CHECK(colour BETWEEN 0 AND 6),charged bigint NOT NULL CHECK(charged>=0),
body_before integer NOT NULL,face_before integer NOT NULL,hair_before integer NOT NULL,
body_after integer NOT NULL,face_after integer NOT NULL,hair_after integer NOT NULL,
created_at timestamptz NOT NULL DEFAULT now());");
            }
        }
        private static bool Ready(GameState p)
        { return Q.Player(p) && p.Booth == null && p.Trade != null && !p.Trade.InTrade; }
        private static AvaterFaces.HairFace_Type Catalog(uint kind, uint id)
        { return AvaterFaces.HairFace.SingleOrDefault(x => x.ISHair == (kind == 0) && x.ID == id); }
        private static bool Fits(GameState p, AvaterFaces.HairFace_Type item)
        {
            int sex = p.Entity.Body == 1003 || p.Entity.Body == 1004 ? 1 :
                p.Entity.Body == 2001 || p.Entity.Body == 2002 ? 2 : 0;
            return sex != 0 && (item.Type == 0 || item.Type == sex)
                && (item.Class.Count == 0 || item.Class.Contains((byte)(p.Entity.Class / 10)));
        }
        private static bool Purchasable(GameState p, AvaterFaces.HairFace_Type item)
        {
            return item.CostKind == 0 && item.Extra == 0
                && (item.RuleKind == 0 && item.RuleValue == 0 || item.RuleKind == 5 && item.RuleValue == 4 && p.Entity.VIPLevel >= 4);
        }
        private static Owned Read(NpgsqlConnection c, NpgsqlTransaction t, GameState p, uint kind, uint id)
        {
            using (var q = Q.Command(c, t, @"SELECT colour,colours FROM revora_cosmetic_ownership
WHERE uid=@uid AND kind=@kind AND id=@id FOR UPDATE", "uid", (long)p.Entity.UID, "kind", (int)kind, "id", (int)id))
            using (var r = q.ExecuteReader()) {
                if (!r.Read()) return null;
                var owned = new Owned { Kind = kind, Id = id, Colour = checked((uint)r.GetInt32(0)), Colours = checked((uint)r.GetInt32(1)) };
                Validate(owned); return owned;
            }
        }
        private static void Validate(Owned owned)
        {
            if (owned.Kind > 1 || owned.Id == 0 || owned.Id > ushort.MaxValue || owned.Colour > 6
                || owned.Colours > 127 || (owned.Colours & 1) == 0 || (owned.Colours & (1u << (int)owned.Colour)) == 0
                || (owned.Kind == 1 && (owned.Colour != 0 || owned.Colours != 1)) || (owned.Kind == 0 && owned.Id >= 1000))
                throw new InvalidOperationException("Invalid stored cosmetic ownership.");
            if (owned.Kind == 0) for (uint colour = 1; colour <= 6; colour++)
                if ((owned.Colours & (1u << (int)colour)) != 0 && !HasColour(owned.Id, colour))
                    throw new InvalidOperationException("Stored cosmetic colour is outside the installed catalog.");
        }
        private static void ProjectActive(GameState p, Owned owned)
        { owned.Active = owned.Kind == 0 ? p.Entity.HairStyle % 1000 == owned.Id : p.Entity.Face == owned.Id; }

        // No imported legacy ownership or inferred colour entitlement. Existing
        // raw appearance remains untouched until an explicitly owned selection.
        internal static Result Operate(GameState p, uint action, uint first, uint second)
        {
            if (!Ready(p) || (action != 3 && action != 4 && action != 6 && action != 7)) return null;
            bool choice = action == 3 || action == 4;
            uint kind = choice ? first : 0, id = choice ? second : first, colour = choice ? 0 : second;
            if (kind > 1 || id == 0 || id > ushort.MaxValue || kind == 0 && id >= 1000 || colour > 6
                || action == 7 && !HasColour(id, colour) || action == 6 && colour != 0 && !HasColour(id, colour)) return null;
            lock (p.ItemSyncRoot) {
                if (!Ready(p)) return null;
                var item = Catalog(kind, id);
                if (item == null || !Fits(p, item)) return null;
                ushort body = p.Entity.Body, face = p.Entity.Face, hair = p.Entity.HairStyle;
                ushort nextFace = face, nextHair = hair;
                ulong charged = 0, nextMoney = p.Entity.Money;
                bool changed = false, achievement = false;
                Owned owned;
                using (var c = DataHolder.MySqlConnection) {
                    c.Open(); using (var t = c.BeginTransaction()) {
                        // One durable entity row orders competing sessions and links
                        // wallet/appearance preimages to the entitlement mutation.
                        AppearanceState.AssertSaved(c, t, p);
                        SilverBalance.AssertSaved(c, t, p);
                        owned = Read(c, t, p, kind, id);
                        if (action == 4) {
                            if (owned == null) {
                                if (!Purchasable(p, item) || nextMoney < item.Cost) return null;
                                charged = item.Cost; nextMoney -= charged;
                                if (Q.Execute(c, t, @"INSERT INTO revora_cosmetic_ownership(uid,kind,id) VALUES(@uid,@kind,@id)",
                                    "uid", (long)p.Entity.UID, "kind", (int)kind, "id", (int)id) != 1)
                                    throw new InvalidOperationException("Cosmetic purchase was not saved.");
                                owned = new Owned { Kind = kind, Id = id, Colours = 1 }; changed = true;
                            }
                        } else {
                            if (owned == null) return null;
                            if (action == 7) {
                                uint bit = 1u << (int)colour;
                                if ((owned.Colours & bit) == 0) {
                                    if (nextMoney < 1000) return null;
                                    charged = 1000; nextMoney -= charged; owned.Colours |= bit;
                                    SaveOwned(c, t, p, owned); changed = true;
                                }
                            } else if (action == 6) {
                                if ((owned.Colours & (1u << (int)colour)) == 0) return null;
                                if (owned.Colour != colour) { owned.Colour = colour; SaveOwned(c, t, p, owned); changed = true; }
                                // The colour dialog may edit an unworn stored style.
                                if (hair % 1000 == id) nextHair = checked((ushort)(colour * 1000 + id));
                            } else if (kind == 1) nextFace = checked((ushort)id);
                            else nextHair = checked((ushort)(owned.Colour * 1000 + id));
                        }
                        changed |= nextFace != face || nextHair != hair;
                        if (changed) {
                            Guid receipt = Guid.NewGuid();
                            if (charged != 0) SilverBalance.Persist(c, t, p, nextMoney);
                            if (nextFace != face || nextHair != hair) AppearanceState.Persist(c, t, p, body, nextFace, nextHair);
                            if (Q.Execute(c, t, @"INSERT INTO revora_cosmetic_receipts
(id,uid,action,kind,cosmetic_id,colour,charged,body_before,face_before,hair_before,body_after,face_after,hair_after)
VALUES(@receipt,@uid,@action,@kind,@id,@colour,@charged,@body,@face,@hair,@body,@nextface,@nexthair)",
                                "receipt", receipt, "uid", (long)p.Entity.UID, "action", (int)action, "kind", (int)kind,
                                "id", (int)id, "colour", (int)(choice ? owned.Colour : colour), "charged", checked((long)charged),
                                "body", (int)body, "face", (int)face, "hair", (int)hair, "nextface", (int)nextFace, "nexthair", (int)nextHair) != 1)
                                throw new InvalidOperationException("Cosmetic receipt was not saved.");
                            string key = receipt.ToString("N");
                            if (action == 3 && kind == 0 && nextHair % 1000 != hair % 1000)
                                achievement |= ProgressionEvidence.Complete(c, t, p.Entity.UID, 10114, "cosmetic-style-v1", key, 1);
                            if (action == 3 && kind == 1 && nextFace != face)
                                achievement |= ProgressionEvidence.Complete(c, t, p.Entity.UID, 10116, "cosmetic-avatar-v1", key, 1);
                            if (action == 6 && nextHair != hair)
                                achievement |= ProgressionEvidence.Complete(c, t, p.Entity.UID, 10113, "cosmetic-colour-v1", key, 1);
                            int expected = action == 3 && kind == 0 && nextHair % 1000 != hair % 1000 ? 10114
                                : action == 3 && kind == 1 && nextFace != face ? 10116 : action == 6 && nextHair != hair ? 10113 : 0;
                            if (expected != 0) using (var q = Q.Command(c, t, @"SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence
WHERE uid=@uid AND achievement_id=@id)", "uid", (long)p.Entity.UID, "id", expected))
                                if (!Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Cosmetic achievement was not saved.");
                        }
                        t.Commit();
                    }
                }
                if (charged != 0) SilverBalance.ApplyCommitted(p, nextMoney);
                if (nextFace != face || nextHair != hair) AppearanceState.ApplyCommitted(p, body, nextFace, nextHair);
                ProjectActive(p, owned);
                return new Result { Entry = owned, Changed = changed, AchievementChanged = achievement, Charged = charged };
            }
        }
        private static void SaveOwned(NpgsqlConnection c, NpgsqlTransaction t, GameState p, Owned owned)
        {
            Validate(owned);
            if (Q.Execute(c, t, @"UPDATE revora_cosmetic_ownership SET colour=@colour,colours=@colours
WHERE uid=@uid AND kind=@kind AND id=@id", "colour", (int)owned.Colour, "colours", (int)owned.Colours,
                "uid", (long)p.Entity.UID, "kind", (int)owned.Kind, "id", (int)owned.Id) != 1)
                throw new InvalidOperationException("Cosmetic colour ownership was not saved.");
        }
        internal static Owned[] Snapshot(GameState p)
        {
            if (!Ready(p)) return new Owned[0];
            lock (p.ItemSyncRoot) {
                if (!Ready(p)) return new Owned[0];
                var entries = new List<Owned>();
                using (var c = DataHolder.MySqlConnection) {
                    c.Open(); using (var t = c.BeginTransaction()) {
                        AppearanceState.AssertSaved(c, t, p);
                        using (var q = Q.Command(c, t, @"SELECT kind,id,colour,colours FROM revora_cosmetic_ownership
WHERE uid=@uid ORDER BY kind,id LIMIT 1025", "uid", (long)p.Entity.UID))
                        using (var r = q.ExecuteReader()) while (r.Read()) {
                            var owned = new Owned { Kind = checked((uint)r.GetInt32(0)), Id = checked((uint)r.GetInt32(1)),
                                Colour = checked((uint)r.GetInt32(2)), Colours = checked((uint)r.GetInt32(3)) };
                            Validate(owned);
                            if (Catalog(owned.Kind, owned.Id) == null) throw new InvalidOperationException("Stored cosmetic is outside the installed catalog.");
                            ProjectActive(p, owned); entries.Add(owned);
                            if (entries.Count > 1024) throw new InvalidOperationException("Stored cosmetic list is too large.");
                        }
                        t.Commit();
                    }
                }
                return entries.ToArray();
            }
        }
    }
}
