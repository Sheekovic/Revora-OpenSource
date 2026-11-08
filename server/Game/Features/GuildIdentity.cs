using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using Revora.Database;

namespace Revora.Game.Features
{
    // Disconnected saved-image read. Startup and reconnect do not call it.
    // Inscribed items are the stored items columns from this transaction. Clock-derived
    // time-left, expiry deletion, refinery clearing, and lock clearing are not applied.
    // Item additions are stored rows from this transaction. Their ids and timestamps are not
    // validated as active effects. Union lifetime is the direct union token.
    // Saved nobility, face, and body stay raw. Donation rank is not derived: the live sort
    // needs the whole board and its tie order, and it mutates that board.
    // BlackList has no durable source. An empty list is not a recovered blacklist.
    // ConquerItem is not constructed. UnionId selects the saved union; zero means none.
    // None is an identity classification, not permission to attach a session or publish a guild.
    // A null guilds.incarnation is legacy. A non-null value is that row's identity, not its id,
    // name, founder, or enrollment day. Member, association, and item-binding rows are not cascaded.
    // Saved ranks, donations, advertisement expiry, and CTF amounts are returned as stored.
    // Project copies those saved fields, the incarnation, and relation identities.
    // It does not apply FromBinary, build a Guild, or publish a registry.
    // Derive evaluates item time, lock, and addition activity from a supplied clock
    // and catalog. It does not delete rows or replace saved refinery fields.
    // CreateMembersRank, spouse-rank derivation, CharacterActivity.Decode,
    // Nobility.Board, Arsenal.GetDonation, and ConquerItemTable.LoadItem are not applied.
    // items.uid is only the current row key. A deleted uid can be inserted again, so a matching
    // uid is not a lifetime. revora_item_token plus revora_guild_item_binding is the direct binding.
    // A null or mismatched token is a contradiction. There is no historical backfill.
    internal static class GuildIdentity
    {
        internal enum Kind { None, Consistent, Contradiction, ReadFailure }

        internal sealed class Member
        {
            public uint Uid;
            public string Name;
            public int Rank;
            public int Level;
            public int Class;
            public string Spouse;
            // Saved entities.lastlogin. Materialize's decoded ticks are derived and are not used here.
            public long LastLoginRaw;
            public ulong SilverDonation;
            public ulong ConquerPointDonation;
            public ulong ArsenalDonation;
            public ulong PkPoints;
            public uint Exploits;
            public uint CtfCpsReward;
            public uint CtfSilverReward;
            public uint Rouses;
            public uint Lilies;
            public uint Tulips;
            public uint Orchids;
            // Stored entity columns. They do not prove this guild's union lifetime.
            public uint? StoredUnionId;
            public ulong? UnionExploits;
            public int? UnionOfficial;
            public int? UnionHarem;
            public int? UnionGuards;
            public Guid EntityToken;
            // Saved entities.face and entities.body. Null is an absent column, not a stored zero.
            public int? Face;
            public int? Body;
            // entities.body % 10, the value ApplyDonation writes into nobility.gender. Not that saved column.
            public int? BodyDigit;
            // Session fallback used when the live board has no row: body % 10 >= 3 ? 0 : 1.
            public int? LoginFallbackGender;
            // UnionTable concatenates Face.ToString() and Body.ToString(). Null when that text is not a uint.
            public uint? ConcatenatedMesh;
            // Null means no nobility row. A row with donation zero is present.
            public SavedNobility Nobility;
        }

        internal sealed class ArsenalRow
        {
            public int Id;
            public bool Unlocked;
            public uint Enhancement;
            public uint BattlePower;
            // Zero denotes an undated legacy purchase; otherwise UTC DateTime ticks.
            public long EnhancementExpiresUtcTicks;
        }

        internal sealed class Inscription
        {
            public int ArsenalId;
            public uint ItemUid;
            public uint OwnerUid;
            public string OwnerName;
            public uint Donation;
            // items.id. Same value as Item.Id. ConquerItem is not constructed.
            public uint ItemId;
            public SavedItem Item;
        }

        // Stored item columns used by ConquerItemTable.deserialzeItem and LoadItem.
        // TimeLeftInMinutes, expired-day deletion, refinery clearing, and lock clearing are derived from the clock and are not applied.
        // ItemAddingTable is a separate loader and stays unresolved. ConquerItem is not constructed, so setters do not run.
        internal sealed class SavedItem
        {
            public uint Id;
            public uint Uid;
            public int Durability;
            public int MaxDurability;
            public int Position;
            public string Agate;
            public uint SocketProgress;
            public uint PlusProgress;
            public int SocketOne;
            public int SocketTwo;
            public int Effect;
            public int Plus;
            public int Bless;
            public bool Bound;
            public int Enchant;
            public int Lock;
            public long UnlockEndRaw;
            public bool Suspicious;
            public long SuspiciousStartRaw;
            public uint Color;
            public uint Warehouse;
            public int StackSize;
            public uint RefineItem;
            public long RefineryTimeRaw;
            public bool InWardrobe;
            public long DayStampRaw;
            public int Days;
            public uint PerfectionLevel;
            public string PerfectionOwner;
            public uint PerfectionOwnerGuid;
            public uint PerfectionProgress;
            public string Signature;
            public uint RunesExp;
            public uint Relic1;
            public uint Relic2;
            public uint Relic3;
            public uint Relic4;
            public uint Relic5;
            public bool Inscribed;
            public uint NextSteedColorRaw;
            public Guid Token;
            // Null means no saved row. It is not a zero-valued active effect.
            public SavedAddition Purification;
            public SavedAddition PermanentRefinery;
            public SavedAddition TemporaryRefinery;
            // Null when no refinery row stored stabilization credit. Not Cost(level).
            public int? StabilizationCredit;
        }

        internal enum DerivedStatus
        {
            Absent,
            StoredZero,
            Active,
            Expired,
            Permanent,
            Malformed,
            Overflow,
            UnknownCatalog,
            MismatchedCatalog,
            NotTimed,
            Unresolved
        }

        // Pure clock/catalog reading of one saved item. Raw columns stay on SavedItem.
        internal sealed class DerivedImage
        {
            public bool Ok;
            public string Detail;
            public string ClockKind;
            // ConquerItemTable and ItemAddingTable decode with FromBinary. The insert paths store Ticks.
            public bool LoaderDecodesFromBinary;
            public bool WritersStoreTicks;
            // deserialzeItem assigns that second count to TimeLeftInMinutes.
            public bool TimeLeftUsesSeconds;
            // ItemAdding.Append stores the truncated whole seconds, not the fractional remainder.
            public bool PacketSecondsTruncate;
            public DerivedItem[] Items = new DerivedItem[0];
        }

        internal sealed class DerivedItem
        {
            public uint ItemUid;
            public Guid Token;
            public int Days;
            public long DayStampRaw;
            public bool RawDayStampIsZero;
            public DerivedStatus DayStatus;
            public bool LoaderWouldDelete;
            public uint? TimeLeftSeconds;
            public int Lock;
            public long UnlockEndRaw;
            public bool RawUnlockIsZero;
            public DerivedStatus LockStatus;
            public bool LoaderWouldClearLock;
            public uint RefineItem;
            public long RefineryTimeRaw;
            public bool RawRefineryTimeIsZero;
            public DerivedStatus SavedRefineryStatus;
            public bool LoaderWouldClearSavedRefinery;
            public bool LoaderWouldReplaceRefineItem;
            public bool LoaderWouldProjectRefinery;
            public uint? CurrentRefineryId;
            // Stored stabilization max, then Cost(level) for a recognized permanent row.
            public int? DerivedCredit;
            public DerivedEffect Purification;
            public DerivedEffect PermanentRefinery;
            public DerivedEffect TemporaryRefinery;
            // Arsenal.GetDonation from quality, sockets, and plus. The inscribed donation stays raw.
            public uint RawDonation;
            public uint DerivedDonation;
            public bool DonationMatches;
            // ConquerItem.BattlePower. Family 421 and any id whose decimal text starts with 5 are doubled.
            // ItemHandler.IsTwoHander is a different predicate and is not used. This is not an arsenal packet.
            public int DerivedBattlePower;
            public DerivedStatus PrestigeStatus;
            public int? PrestigeScore;
            // ItemAdding.Append rewrites 724440-724444 and 3004140 to 301. The saved id stays.
            public uint? PacketEffectId;
            public int? PacketSeconds;
            public bool PacketWouldIncludeRefinery;
            public bool PacketWouldIncludePurification;
            public uint? RefineryPacketSeconds;
            public DerivedStatus RefineryPacketStatus;
            // SendExtras emits ItemSoul independently of ItemAdding, without the 301 rewrite.
            public DerivedStatus SoulCatalogStatus;
            public uint? SoulEffectId;
            public uint? SoulSeconds;
            public bool SoulWouldInclude;
        }

        internal sealed class DerivedEffect
        {
            public DerivedStatus Status;
            public long AddedOnRaw;
            public uint Duration;
            public uint AddingId;
            public Guid Token;
            public long? SecondsRemaining;
            public bool LoaderWouldDelete;
        }

        // One itemadding row. AddedOnRaw is the stored integer, not DateTime.FromBinary.
        internal sealed class SavedAddition
        {
            public long RowId;
            public Guid Token;
            public int AddingType;
            public uint AddingId;
            public uint Level;
            public uint Percent;
            public uint Duration;
            public long AddedOnRaw;
            public bool StabilizationStored;
            public int StabilizationPoints;
        }

        // Saved union row. Role numbers are stored assignments, not mesh, nobility, or packet authority.
        internal sealed class SavedUnion
        {
            public uint Id;
            public Guid Token;
            public uint GoldBricks;
            public ulong Fund;
            public string Name;
            public string Title;
            public string LeaderName;
            public uint LeaderUid;
            public string Bulletin;
            public string Declaration;
            public string OfficialsRaw;
            public string HaremRaw;
            public string GuardsRaw;
            public uint[] Officials = new uint[0];
            public uint[] Harem = new uint[0];
            public uint[] Guards = new uint[0];
            public UnionMember[] Members = new UnionMember[0];
            public GuildUnionLink[] Guilds = new GuildUnionLink[0];
        }

        internal sealed class UnionMember
        {
            public uint Uid;
            public string Name;
            public int Level;
            public int Class;
            public ulong Exploits;
            public int Official;
            public int Harem;
            public int Guards;
            // Null when entities.guildid is null. Zero is a stored guild id.
            public uint? GuildId;
            public Guid EntityToken;
            // Null means the array and leader did not derive a role. The stored column is separate.
            public int? DerivedOfficial;
            public int? DerivedHarem;
            public int? DerivedGuards;
            public int? Face;
            public int? Body;
            public int? BodyDigit;
            public int? LoginFallbackGender;
            public uint? ConcatenatedMesh;
            public SavedNobility Nobility;
        }

        // One nobility row. entityuid is not a character lifetime. DerivedRank stays null:
        // King, Prince, and Duke depend on global place, and live sorting has side effects.
        internal sealed class SavedNobility
        {
            public uint EntityUid;
            public Guid EntityToken;
            public string Name;
            public ulong? Donation;
            // Null when the column is null. Zero is a stored zero, not "no row".
            public int? Gender;
            public uint? Mesh;
            public int? Rey;
            // Saved duracionrey text. Load and the live sort never read it, so it is not an expiry.
            public string DurationRaw;
            public int? DerivedRank;
            // SendMembers writes gender 3 as 1 and every other saved gender as gender + 1.
            // Null when saved gender is null. A missing row does not become packet gender 1.
            public int? PacketGender;
        }

        internal sealed class Relation
        {
            public uint GuildId;
            public Guid Incarnation;
            public bool LegacyPeer;
        }

        internal sealed class Snapshot
        {
            public Kind Kind;
            public string Detail;
            public Guid Incarnation;
            public uint GuildId;
            public string Name;
            public string LeaderName;
            public uint LeaderUid;
            public ulong SilverFund;
            public uint ConquerPointFund;
            public string Bulletin;
            public uint EnrollmentDate;
            public uint BulletinDate;
            public uint LevelRequirement;
            public uint RebornRequirement;
            public uint ClassRequirement;
            public uint AdLevelRequirement;
            public uint AdRebornRequirement;
            public uint AdClassRequirement;
            public uint AutoRecruit;
            // Saved guilds.malefemale. Advertisement requirement, not a member's body or nobility gender.
            public uint MaleFemale;
            public uint UnionId;
            public uint Wins;
            public uint Losses;
            public uint CtfPoints;
            public uint CtfReward;
            public uint CtfDonationCps;
            public uint CtfDonationSilver;
            public uint CtfDonationCpsOld;
            public uint CtfDonationSilverOld;
            public uint MaxEnchant;
            public string Advertisement;
            public uint AdvertisementRate;
            // Saved guilds.adend. Guild.adend applies DateTime.FromBinary and is derived.
            public long AdvertisementEndsRaw;
            public Member[] Members = new Member[0];
            public ArsenalRow[] Arsenal = new ArsenalRow[0];
            // Null means this guild row stores no union. It is not a zero-valued union.
            public SavedUnion Union;
            public SavedUnion[] OtherUnions = new SavedUnion[0];
            public Inscription[] Inscriptions = new Inscription[0];
            public Relation[] Allies = new Relation[0];
            public Relation[] Enemies = new Relation[0];
            // Null means the blacklist was not recovered. Guild.BlackList is process memory only.
            public uint[] BlackList;
            public string BlackListState;
        }

        // Saved-image projection. It copies the snapshot's durable fields and does not
        // open a connection, construct a Guild, or publish a registry. Active effects,
        // packet behavior, and clock interpretation stay unresolved.
        internal sealed class Projection
        {
            public bool Ok;
            public string Detail;
            public Guid Incarnation;
            public uint GuildId;
            public string Name;
            public string LeaderName;
            public uint LeaderUid;
            public ulong SilverFund;
            public uint ConquerPointFund;
            public string Bulletin;
            public uint EnrollmentDate;
            public uint BulletinDate;
            public uint LevelRequirement;
            public uint RebornRequirement;
            public uint ClassRequirement;
            public uint AdLevelRequirement;
            public uint AdRebornRequirement;
            public uint AdClassRequirement;
            public uint AutoRecruit;
            public uint MaleFemale;
            public uint UnionId;
            public uint Wins;
            public uint Losses;
            public uint CtfPoints;
            public uint CtfReward;
            public uint CtfDonationCps;
            public uint CtfDonationSilver;
            public uint CtfDonationCpsOld;
            public uint CtfDonationSilverOld;
            public uint MaxEnchant;
            public string Advertisement;
            public uint AdvertisementRate;
            // Saved guilds.adend. Guild.adend reads it with FromBinary and stores Ticks on write.
            public long AdvertisementEndsRaw;
            public Member[] Members = new Member[0];
            public ArsenalRow[] Arsenal = new ArsenalRow[0];
            public Inscription[] Inscriptions = new Inscription[0];
            public SavedUnion Union;
            public SavedUnion[] OtherUnions = new SavedUnion[0];
            public Relation[] Allies = new Relation[0];
            public Relation[] Enemies = new Relation[0];
            // Null means unknown. It is not an empty recovered blacklist.
            public uint[] BlackList;
            public string BlackListState;
        }

        // Test seam. It runs after the guild row share lock and before the roster read.
        // It must not be used to publish Kernel.Guilds or a session.
        internal static Action AfterGuildLock = null;
        // Test seam for the pure projection. It does not open a connection.
        internal static Action BeforeProject = null;
        // Test seams for the pure effect derivation. They do not open a connection.
        internal static Action BeforeDerive = null;
        internal static Action AfterDeriveItem = null;

        // Explicit additive setup, disconnected from production and from Read. No backfill.
        // Deliberately no FK/cascade: orphan marks remain evidence of a contradiction.
        // Future lifecycle writers must maintain the owner/mark/entity invariant atomically.
        internal static void EnsureSchema()
        {
            GuildArsenalPurchases.Initialize();
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                using (var q = new NpgsqlCommand(@"
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = current_schema() AND table_name = 'guilds' AND column_name = 'incarnation')
  THEN
    ALTER TABLE guilds ADD COLUMN incarnation uuid;
  END IF;
END $$;
CREATE UNIQUE INDEX IF NOT EXISTS revora_guilds_incarnation_key
  ON guilds (incarnation) WHERE incarnation IS NOT NULL;
CREATE TABLE IF NOT EXISTS revora_guild_member_identity (
  entity_uid bigint PRIMARY KEY,
  guild_id integer NOT NULL CHECK (guild_id > 0 AND guild_id <= 65535),
  incarnation uuid NOT NULL);
CREATE TABLE IF NOT EXISTS revora_guild_association (
  incarnation uuid NOT NULL,
  guild_id integer NOT NULL CHECK (guild_id > 0 AND guild_id <= 65535),
  kind text NOT NULL CHECK (kind IN ('arsenal','inscribed','ally','enemy')),
  subject_id bigint NOT NULL,
  peer_incarnation uuid NULL,
  PRIMARY KEY (incarnation, kind, subject_id));
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = current_schema() AND table_name = 'items' AND column_name = 'revora_item_token')
  THEN
    ALTER TABLE items ADD COLUMN revora_item_token uuid;
  END IF;
END $$;
CREATE TABLE IF NOT EXISTS revora_guild_item_binding (
  item_uid bigint PRIMARY KEY,
  incarnation uuid NOT NULL,
  owner_uid bigint NOT NULL,
  item_token uuid NOT NULL);
CREATE TABLE IF NOT EXISTS revora_item_addition_binding (
  addition_id bigint PRIMARY KEY,
  item_uid bigint NOT NULL,
  item_token uuid NOT NULL,
  incarnation uuid NOT NULL);
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = current_schema() AND table_name = 'itemadding' AND column_name = 'revora_addition_token')
  THEN
    ALTER TABLE itemadding ADD COLUMN revora_addition_token uuid;
  END IF;
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = current_schema() AND table_name = 'revora_item_addition_binding' AND column_name = 'addition_token')
  THEN
    ALTER TABLE revora_item_addition_binding ADD COLUMN addition_token uuid;
  END IF;
END $$;
CREATE UNIQUE INDEX IF NOT EXISTS revora_itemadding_token_key
  ON itemadding (revora_addition_token) WHERE revora_addition_token IS NOT NULL;
-- No FK, default token, or backfill. A future inscription writer must maintain item,
-- inscription, association, and item binding atomically. An addition writer must mint
-- a fresh nonempty token on every inserted/replacement row, and atomically maintain its
-- binding to that token, item token, and guild incarnation. Never reuse an old row token.
-- Deletion/replacement must reconcile bindings in that transaction. Leftover marks
-- remain contradictions. Numeric row ids and item tokens cannot prove addition lifetime.
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema = current_schema() AND table_name = 'unions')
  THEN
    IF NOT EXISTS (
      SELECT 1 FROM information_schema.columns
      WHERE table_schema = current_schema() AND table_name = 'unions' AND column_name = 'revora_union_token')
    THEN
      ALTER TABLE unions ADD COLUMN revora_union_token uuid;
    END IF;
    EXECUTE 'CREATE UNIQUE INDEX IF NOT EXISTS revora_unions_token_key ON unions (revora_union_token) WHERE revora_union_token IS NOT NULL';
  END IF;
END $$;
CREATE TABLE IF NOT EXISTS revora_guild_union_binding (
  incarnation uuid PRIMARY KEY,
  guild_id integer NOT NULL,
  union_id bigint NOT NULL,
  union_token uuid NOT NULL);
CREATE TABLE IF NOT EXISTS revora_union_member_binding (
  entity_uid bigint PRIMARY KEY,
  union_id bigint NOT NULL,
  union_token uuid NOT NULL,
  entity_token uuid NULL);
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = current_schema() AND table_name = 'entities' AND column_name = 'revora_entity_token')
  THEN
    ALTER TABLE entities ADD COLUMN revora_entity_token uuid;
  END IF;
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = current_schema() AND table_name = 'revora_guild_member_identity' AND column_name = 'entity_token')
  THEN
    ALTER TABLE revora_guild_member_identity ADD COLUMN entity_token uuid;
  END IF;
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = current_schema() AND table_name = 'revora_union_member_binding' AND column_name = 'entity_token')
  THEN
    ALTER TABLE revora_union_member_binding ADD COLUMN entity_token uuid;
  END IF;
END $$;
CREATE UNIQUE INDEX IF NOT EXISTS revora_entities_token_key
  ON entities (revora_entity_token) WHERE revora_entity_token IS NOT NULL;
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema = current_schema() AND table_name = 'nobility')
  THEN
    IF NOT EXISTS (
      SELECT 1 FROM information_schema.columns
      WHERE table_schema = current_schema() AND table_name = 'nobility' AND column_name = 'revora_entity_token')
    THEN
      ALTER TABLE nobility ADD COLUMN revora_entity_token uuid;
    END IF;
    EXECUTE 'CREATE UNIQUE INDEX IF NOT EXISTS revora_nobility_entity_token_key ON nobility (revora_entity_token) WHERE revora_entity_token IS NOT NULL';
  END IF;
END $$;
-- No FK, default, or backfill. A future union writer must mint a new token when the
-- numeric union id is inserted or reused, and bind the guild incarnation and each
-- member in that same transaction. Deleting the union leaves the marks, and the next
-- read is a contradiction. A guild member may store a different UnionID.
-- Server.EntityUID is the counter saved as configuration.EntityID. entities.uid is only the
-- current row key, so a deleted uid can be inserted again. revora_entity_token is that row's
-- lifetime. Guild and union member marks must carry the same nonempty token. A future writer
-- mints a new token on every inserted or replacement character and reconciles those marks
-- in the same transaction. There is no backfill.
-- nobility.entityuid is the current row key, not a character lifetime. A future nobility
-- writer must store that character's revora_entity_token on the nobility row in the same
-- transaction as the character insert or replacement. NobilityTable insert, donation update,
-- paid-king updates, and donation reset do not write this token. EnsureSchema does not enroll
-- an existing row. There is no default, backfill, foreign key, or cascade.", c))
                    q.ExecuteNonQuery();
            }
        }

        // None classifies identity. It does not authorize attaching a session or publishing the registry.
        internal static Snapshot Read(uint guildId, uint entityUid)
        {
            return ReadCore(guildId, entityUid, true);
        }

        // Disconnected startup shape: the saved leader is taken from the roster, not invented for a caller.
        internal static Snapshot ReadOwner(uint guildId)
        {
            return ReadCore(guildId, 0, false);
        }

        // Copies a consistent snapshot. It does not read the database or publish Kernel state.
        // A fault, including BeforeProject, leaves the caller with an unsuccessful projection.
        internal static Projection Project(Snapshot snapshot)
        {
            try
            {
                if (BeforeProject != null) BeforeProject();
                if (snapshot == null || snapshot.Kind != Kind.Consistent)
                    return EmptyProjection(snapshot == null ? "missing snapshot" : "not consistent");
                var copy = new Projection();
                copy.Ok = true;
                copy.Detail = "projected";
                copy.Incarnation = snapshot.Incarnation;
                copy.GuildId = snapshot.GuildId;
                copy.Name = snapshot.Name;
                copy.LeaderName = snapshot.LeaderName;
                copy.LeaderUid = snapshot.LeaderUid;
                copy.SilverFund = snapshot.SilverFund;
                copy.ConquerPointFund = snapshot.ConquerPointFund;
                copy.Bulletin = snapshot.Bulletin;
                copy.EnrollmentDate = snapshot.EnrollmentDate;
                copy.BulletinDate = snapshot.BulletinDate;
                copy.LevelRequirement = snapshot.LevelRequirement;
                copy.RebornRequirement = snapshot.RebornRequirement;
                copy.ClassRequirement = snapshot.ClassRequirement;
                copy.AdLevelRequirement = snapshot.AdLevelRequirement;
                copy.AdRebornRequirement = snapshot.AdRebornRequirement;
                copy.AdClassRequirement = snapshot.AdClassRequirement;
                copy.AutoRecruit = snapshot.AutoRecruit;
                copy.MaleFemale = snapshot.MaleFemale;
                copy.UnionId = snapshot.UnionId;
                copy.Wins = snapshot.Wins;
                copy.Losses = snapshot.Losses;
                copy.CtfPoints = snapshot.CtfPoints;
                copy.CtfReward = snapshot.CtfReward;
                copy.CtfDonationCps = snapshot.CtfDonationCps;
                copy.CtfDonationSilver = snapshot.CtfDonationSilver;
                copy.CtfDonationCpsOld = snapshot.CtfDonationCpsOld;
                copy.CtfDonationSilverOld = snapshot.CtfDonationSilverOld;
                copy.MaxEnchant = snapshot.MaxEnchant;
                copy.Advertisement = snapshot.Advertisement;
                copy.AdvertisementRate = snapshot.AdvertisementRate;
                copy.AdvertisementEndsRaw = snapshot.AdvertisementEndsRaw;
                copy.BlackList = snapshot.BlackList == null ? null : (uint[])snapshot.BlackList.Clone();
                copy.BlackListState = snapshot.BlackListState;
                copy.Members = CopyMembers(snapshot.Members);
                copy.Arsenal = CopyArsenal(snapshot.Arsenal);
                copy.Inscriptions = CopyInscriptions(snapshot.Inscriptions);
                copy.Union = CopyUnion(snapshot.Union);
                copy.OtherUnions = CopyUnions(snapshot.OtherUnions);
                copy.Allies = CopyRelations(snapshot.Allies);
                copy.Enemies = CopyRelations(snapshot.Enemies);
                return copy;
            }
            catch (Exception ex)
            {
                return EmptyProjection(ex.GetType().Name);
            }
        }

        private static Projection EmptyProjection(string detail)
        {
            var copy = new Projection();
            copy.Ok = false;
            copy.Detail = detail;
            copy.Members = new Member[0];
            copy.Arsenal = new ArsenalRow[0];
            copy.Inscriptions = new Inscription[0];
            copy.OtherUnions = new SavedUnion[0];
            copy.Allies = new Relation[0];
            copy.Enemies = new Relation[0];
            return copy;
        }

        private static Relation[] CopyRelations(Relation[] source)
        {
            if (source == null) throw new InvalidOperationException("incomplete relation");
            var copy = new Relation[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                Relation row = source[i];
                if (row == null) throw new InvalidOperationException("incomplete relation");
                copy[i] = new Relation { GuildId = row.GuildId, Incarnation = row.Incarnation, LegacyPeer = row.LegacyPeer };
            }
            return copy;
        }

        // Captured clock and catalog only. DateTime.Now, DatabaseRefinery, and row deletion are not used.
        // Item and purification loaders compare DateTime.Now. Refinery progress compares Clock().ToLocalTime().
        // This method compares the supplied clock directly and does not convert time zones.
        internal static DerivedImage Derive(Snapshot snapshot, DateTime clock, uint[] catalogIds, uint[] catalogLevels, uint[] catalogPercents)
        {
            try
            {
                if (BeforeDerive != null) BeforeDerive();
                if (snapshot == null || snapshot.Kind != Kind.Consistent)
                    return EmptyDerived(snapshot == null ? "missing snapshot" : "not consistent");
                if (catalogIds == null || catalogLevels == null || catalogPercents == null)
                    return EmptyDerived("missing catalog");
                if (catalogIds.Length != catalogLevels.Length || catalogIds.Length != catalogPercents.Length)
                    return EmptyDerived("malformed catalog");
                var catalogKeys = new HashSet<uint>();
                foreach (uint id in catalogIds)
                    if (id == 0 || !catalogKeys.Add(id)) return EmptyDerived("malformed catalog");
                var image = new DerivedImage();
                image.Ok = true;
                image.Detail = "derived";
                image.ClockKind = clock.Kind.ToString();
                image.LoaderDecodesFromBinary = true;
                image.WritersStoreTicks = true;
                image.TimeLeftUsesSeconds = true;
                var items = new List<DerivedItem>();
                foreach (var inscription in snapshot.Inscriptions)
                {
                    if (inscription == null || inscription.Item == null) return EmptyDerived("incomplete item");
                    DerivedItem derived = DeriveItem(inscription.Item, clock, catalogIds, catalogLevels, catalogPercents);
                    if (AfterDeriveItem != null) AfterDeriveItem();
                    items.Add(derived);
                }
                image.Items = items.ToArray();
                return image;
            }
            catch (Exception ex)
            {
                return EmptyDerived(ex.GetType().Name);
            }
        }

        // Donation, battle power, prestige, and packet scalars. Catalogs are caller input.
        // A contradictory catalog returns no items. This does not construct a ConquerItem or send a packet.
        internal static DerivedImage DeriveScores(Snapshot snapshot, DateTime clock, uint[] catalogIds, uint[] catalogLevels, uint[] catalogPercents,
            uint[] itemIds, int[] itemLevels, int[] scoreSorts, int[] scoreMins, int[] scoreMaxes, int[] scoreValues)
        {
            try
            {
                if (BeforeDerive != null) BeforeDerive();
                if (snapshot == null || snapshot.Kind != Kind.Consistent)
                    return EmptyDerived(snapshot == null ? "missing snapshot" : "not consistent");
                if (catalogIds == null || catalogLevels == null || catalogPercents == null || itemIds == null || itemLevels == null
                    || scoreSorts == null || scoreMins == null || scoreMaxes == null || scoreValues == null)
                    return EmptyDerived("missing catalog");
                if (catalogIds.Length != catalogLevels.Length || catalogIds.Length != catalogPercents.Length
                    || itemIds.Length != itemLevels.Length
                    || scoreSorts.Length != scoreMins.Length || scoreSorts.Length != scoreMaxes.Length || scoreSorts.Length != scoreValues.Length)
                    return EmptyDerived("malformed catalog");
                var catalogKeys = new HashSet<uint>();
                foreach (uint id in catalogIds)
                    if (id == 0 || !catalogKeys.Add(id)) return EmptyDerived("malformed catalog");
                foreach (uint level in catalogLevels)
                    if (level > int.MaxValue) return EmptyDerived("malformed catalog");
                var itemKeys = new Dictionary<uint, int>();
                for (int i = 0; i < itemIds.Length; i++)
                {
                    if (itemIds[i] == 0 || itemIds[i] > int.MaxValue || itemLevels[i] <= 0) return EmptyDerived("malformed catalog");
                    int prior;
                    if (itemKeys.TryGetValue(itemIds[i], out prior) && prior != itemLevels[i]) return EmptyDerived("ambiguous catalog");
                    itemKeys[itemIds[i]] = itemLevels[i];
                }
                for (int i = 0; i < scoreSorts.Length; i++)
                    if (!Enum.IsDefined(typeof(Revora.AbilitiesSort), scoreSorts[i]) || scoreMins[i] < 0
                        || scoreMaxes[i] < scoreMins[i] || scoreValues[i] < 0) return EmptyDerived("malformed catalog");
                for (int i = 0; i < scoreSorts.Length; i++)
                    for (int j = i + 1; j < scoreSorts.Length; j++)
                        if (scoreSorts[i] == scoreSorts[j] && scoreMins[i] <= scoreMaxes[j] && scoreMins[j] <= scoreMaxes[i])
                            return EmptyDerived("ambiguous catalog");
                var image = new DerivedImage();
                image.Ok = true;
                image.Detail = "scored";
                image.ClockKind = clock.Kind.ToString();
                image.LoaderDecodesFromBinary = true;
                image.WritersStoreTicks = true;
                image.TimeLeftUsesSeconds = true;
                image.PacketSecondsTruncate = true;
                var items = new List<DerivedItem>();
                foreach (var inscription in snapshot.Inscriptions)
                {
                    if (inscription == null || inscription.Item == null) return EmptyDerived("incomplete item");
                    DerivedItem derived = DeriveItem(inscription.Item, clock, catalogIds, catalogLevels, catalogPercents);
                    SavedItem raw = inscription.Item;
                    if (raw.Id == 0 || raw.Id > int.MaxValue || !ByteValue(raw.Plus) || !ByteValue(raw.SocketOne) || !ByteValue(raw.SocketTwo)
                        || !ByteValue(raw.Bless) || !ByteValue(raw.Enchant)) return EmptyDerived("malformed item score input");
                    ApplyScore(derived, inscription, clock, itemKeys, scoreSorts, scoreMins, scoreMaxes, scoreValues, catalogIds, catalogLevels, catalogPercents);
                    if (AfterDeriveItem != null) AfterDeriveItem();
                    items.Add(derived);
                }
                image.Items = items.ToArray();
                return image;
            }
            catch (Exception ex)
            {
                return EmptyDerived(ex.GetType().Name);
            }
        }

        private static bool ByteValue(int value) { return value >= 0 && value <= byte.MaxValue; }

        private static void ApplyScore(DerivedItem derived, Inscription inscription, DateTime clock, Dictionary<uint, int> itemLevels,
            int[] scoreSorts, int[] scoreMins, int[] scoreMaxes, int[] scoreValues, uint[] catalogIds, uint[] catalogLevels, uint[] catalogPercents)
        {
            SavedItem item = inscription.Item;
            derived.RawDonation = inscription.Donation;
            derived.DerivedDonation = DonationOf(item.Id, item.SocketOne, item.SocketTwo, item.Plus);
            derived.DonationMatches = derived.DerivedDonation == inscription.Donation;
            derived.DerivedBattlePower = BattlePowerOf(item.Id, item.SocketOne, item.SocketTwo, item.Plus);
            ApplyPacket(derived, item, clock);
            ApplySoul(derived, item, clock, catalogIds);
            var position = Network.GamePackets.ItemHandler.GetPositionFromID(item.Id);
            if (position == Network.GamePackets.ItemHandler.Positions.SteedArmor || position == Network.GamePackets.ItemHandler.Positions.Garment
                || position == Network.GamePackets.ItemHandler.Positions.Bottle || position == Network.GamePackets.ItemHandler.Positions.RightAccessory
                || position == Network.GamePackets.ItemHandler.Positions.LeftAccessory)
            {
                derived.PrestigeStatus = DerivedStatus.StoredZero;
                derived.PrestigeScore = 0;
                return;
            }
            int level;
            if (!itemLevels.TryGetValue(item.Id, out level))
            {
                derived.PrestigeStatus = DerivedStatus.UnknownCatalog;
                return;
            }
            if (level <= 0)
            {
                derived.PrestigeStatus = DerivedStatus.Malformed;
                return;
            }
            int score = 0;
            if (!TakePrestige(derived, scoreSorts, scoreMins, scoreMaxes, scoreValues, (int)Revora.AbilitiesSort.ItemLevel, level, true, level, ref score)) return;
            if (item.Bless > 0 && item.Bless < 8)
            {
                if (!TakePrestige(derived, scoreSorts, scoreMins, scoreMaxes, scoreValues, (int)Revora.AbilitiesSort.Protection, item.Bless, false, 0, ref score)) return;
            }
            if (item.RefineItem > 0)
            {
                int material = -1;
                for (int i = 0; i < catalogIds.Length; i++)
                    if (catalogIds[i] == item.RefineItem) material = (int)catalogLevels[i];
                // A missing saved refinery id is unknown. The live getter treats that miss as level 0.
                if (material < 0) { derived.PrestigeStatus = DerivedStatus.UnknownCatalog; return; }
                if (material > 0)
                {
                    if (!TakePrestige(derived, scoreSorts, scoreMins, scoreMaxes, scoreValues, (int)Revora.AbilitiesSort.MaterialPhase, material, false, 0, ref score)) return;
                }
            }
            if (derived.Purification.Status != DerivedStatus.Absent && derived.Purification.Status != DerivedStatus.Expired
                && derived.Purification.Status != DerivedStatus.Active && derived.Purification.Status != DerivedStatus.Permanent)
            {
                derived.PrestigeStatus = derived.Purification.Status;
                return;
            }
            if ((derived.Purification.Status == DerivedStatus.Active || derived.Purification.Status == DerivedStatus.Permanent)
                && item.Purification.Level > int.MaxValue)
            {
                derived.PrestigeStatus = DerivedStatus.Overflow;
                return;
            }
            if (derived.Purification != null && (derived.Purification.Status == DerivedStatus.Active || derived.Purification.Status == DerivedStatus.Permanent)
                && item.Purification != null && item.Purification.Level > 0)
            {
                if (!TakePrestige(derived, scoreSorts, scoreMins, scoreMaxes, scoreValues, (int)Revora.AbilitiesSort.SoulPhase, (int)item.Purification.Level, false, 0, ref score)) return;
            }
            if (item.Plus > 0)
            {
                if (!TakePrestige(derived, scoreSorts, scoreMins, scoreMaxes, scoreValues, (int)Revora.AbilitiesSort.Plus, item.Plus, false, 0, ref score)) return;
            }
            int sockets = (item.SocketOne != 0 ? 1 : 0) + (item.SocketTwo != 0 ? 1 : 0);
            if (sockets != 0)
            {
                if (!TakePrestige(derived, scoreSorts, scoreMins, scoreMaxes, scoreValues, (int)Revora.AbilitiesSort.Sockets, sockets, false, 0, ref score)) return;
                if (item.SocketOne > 0 && item.SocketOne < 255 && item.SocketOne % 10 != 0)
                {
                    if (!TakePrestige(derived, scoreSorts, scoreMins, scoreMaxes, scoreValues, (int)Revora.AbilitiesSort.GemQuality, item.SocketOne % 10, false, 0, ref score)) return;
                }
                if (item.SocketTwo > 0 && item.SocketTwo < 255 && item.SocketTwo % 10 != 0)
                {
                    if (!TakePrestige(derived, scoreSorts, scoreMins, scoreMaxes, scoreValues, (int)Revora.AbilitiesSort.GemQuality, item.SocketTwo % 10, false, 0, ref score)) return;
                }
            }
            if (item.Id % 10 > 0)
            {
                if (!TakePrestige(derived, scoreSorts, scoreMins, scoreMaxes, scoreValues, (int)Revora.AbilitiesSort.Quality, (int)(item.Id % 10), false, 0, ref score)) return;
            }
            if (item.PerfectionLevel > 0)
            {
                int val = item.PerfectionLevel >= 28 ? 27 : (int)item.PerfectionLevel;
                if (!TakePrestige(derived, scoreSorts, scoreMins, scoreMaxes, scoreValues, (int)Revora.AbilitiesSort.PerfectionLevel, val, true, val, ref score)) return;
                if (item.PerfectionLevel >= 28)
                {
                    long extra = ((long)item.PerfectionLevel - 27L) * 100L;
                    long next = (long)score + extra;
                    if (next > int.MaxValue || next < int.MinValue) { derived.PrestigeStatus = DerivedStatus.Overflow; derived.PrestigeScore = null; return; }
                    score = (int)next;
                }
            }
            if (item.Enchant > 0)
            {
                if (!TakePrestige(derived, scoreSorts, scoreMins, scoreMaxes, scoreValues, (int)Revora.AbilitiesSort.Enchant, item.Enchant, true, item.Enchant, ref score)) return;
            }
            derived.PrestigeStatus = DerivedStatus.Active;
            derived.PrestigeScore = score;
        }

        private static bool TakePrestige(DerivedItem derived, int[] sorts, int[] mins, int[] maxes, int[] values, int sort, int key, bool scale, int scaleBy, ref int score)
        {
            int found = -1;
            for (int i = 0; i < sorts.Length; i++)
            {
                if (sorts[i] != sort || key < mins[i] || key > maxes[i]) continue;
                if (found >= 0) throw new InvalidOperationException("ambiguous catalog");
                found = i;
            }
            if (found < 0)
            {
                derived.PrestigeStatus = DerivedStatus.UnknownCatalog;
                derived.PrestigeScore = null;
                return false;
            }
            int part = values[found];
            if (scale)
            {
                decimal product = ((decimal)scaleBy / 100m) * values[found];
                if (product > int.MaxValue || product < int.MinValue)
                {
                    derived.PrestigeStatus = DerivedStatus.Overflow;
                    derived.PrestigeScore = null;
                    return false;
                }
                part = (int)product;
            }
            long next = (long)score + part;
            if (next > int.MaxValue || next < int.MinValue)
            {
                derived.PrestigeStatus = DerivedStatus.Overflow;
                derived.PrestigeScore = null;
                return false;
            }
            score = (int)next;
            return true;
        }

        private static void ApplyPacket(DerivedItem derived, SavedItem item, DateTime clock)
        {
            if (derived.Purification.Status == DerivedStatus.Active || derived.Purification.Status == DerivedStatus.Permanent)
            {
                derived.PacketWouldIncludePurification = true;
                if (derived.Purification.Status == DerivedStatus.Active) derived.PacketSeconds = (int)derived.Purification.SecondsRemaining.Value;
            }
            if (!derived.CurrentRefineryId.HasValue)
            {
                derived.RefineryPacketStatus = DerivedStatus.Unresolved;
                return;
            }
            uint current = derived.CurrentRefineryId.Value;
            if (current == 0) return;
            SavedAddition selected = derived.TemporaryRefinery.Status == DerivedStatus.Active ? item.TemporaryRefinery : item.PermanentRefinery;
            if (selected == null) return;
            derived.PacketEffectId = current >= 724440 && current <= 724444 || current == 3004140 ? 301 : current;
            if (selected.Percent == 0)
            {
                derived.RefineryPacketStatus = DerivedStatus.Active;
                derived.PacketWouldIncludeRefinery = true;
                return;
            }
            DateTime added;
            long seconds;
            if (!TryBinary(selected.AddedOnRaw, out added) || !TrySecondDelta(added, selected.Duration, clock, out seconds)
                || seconds < int.MinValue || seconds > int.MaxValue)
            {
                derived.RefineryPacketStatus = DerivedStatus.Overflow;
                return;
            }
            if (seconds <= 0 && selected.Duration != 0)
            {
                derived.RefineryPacketStatus = DerivedStatus.Expired;
                return;
            }
            // Append casts negative permanent seconds to uint. Keep that unsafe wrap unresolved.
            if (seconds < 0)
            {
                derived.RefineryPacketStatus = DerivedStatus.Unresolved;
                return;
            }
            derived.RefineryPacketStatus = DerivedStatus.Active;
            derived.PacketWouldIncludeRefinery = true;
            derived.RefineryPacketSeconds = (uint)seconds;
        }

        private static void ApplySoul(DerivedItem derived, SavedItem item, DateTime clock, uint[] catalogIds)
        {
            // Model the loader guard and the separate RefineStats/RefinerySeconds inputs.
            if (derived.SavedRefineryStatus == DerivedStatus.Malformed)
            {
                derived.SoulCatalogStatus = DerivedStatus.Malformed;
                return;
            }
            bool additions = item.PermanentRefinery != null || item.TemporaryRefinery != null;
            if (additions && !derived.CurrentRefineryId.HasValue)
            {
                derived.SoulCatalogStatus = DerivedStatus.Unresolved;
                return;
            }
            uint id = additions ? derived.CurrentRefineryId.Value : derived.LoaderWouldClearSavedRefinery ? 0 : item.RefineItem;
            if (id == 0) return;
            if (Array.IndexOf(catalogIds, id) < 0)
            {
                derived.SoulCatalogStatus = DerivedStatus.UnknownCatalog;
                return;
            }
            long seconds = 0;
            if (additions)
            {
                if (derived.TemporaryRefinery.Status == DerivedStatus.Active)
                {
                    DateTime added;
                    if (!TryBinary(item.TemporaryRefinery.AddedOnRaw, out added)) return;
                    seconds = (long)(added.AddSeconds(item.TemporaryRefinery.Duration) - clock).TotalSeconds;
                }
            }
            else if (item.RefineryTimeRaw != 0)
            {
                DateTime until;
                if (!TryBinary(item.RefineryTimeRaw, out until))
                {
                    derived.SoulCatalogStatus = DerivedStatus.Malformed;
                    return;
                }
                seconds = (long)(until - clock).TotalSeconds;
            }
            if (seconds < 0 || seconds > uint.MaxValue)
            {
                derived.SoulCatalogStatus = DerivedStatus.Overflow;
                return;
            }
            derived.SoulCatalogStatus = DerivedStatus.Active;
            derived.SoulEffectId = id;
            derived.SoulSeconds = (uint)seconds;
            derived.SoulWouldInclude = true;
        }

        private static uint DonationOf(uint id, int socketOne, int socketTwo, int plus)
        {
            uint value = 0;
            int quality = (int)(id % 10);
            if (quality == 8) value = 1000;
            else if (quality == 9) value = 16660;
            if (socketOne > 0 && socketTwo == 0) value += 33330;
            if (socketOne > 0 && socketTwo > 0) value += 133330;
            if (plus == 1) value += 90;
            else if (plus == 2) value += 490;
            else if (plus == 3) value += 1350;
            else if (plus == 4) value += 4070;
            else if (plus == 5) value += 12340;
            else if (plus == 6) value += 37030;
            else if (plus == 7) value += 111110;
            else if (plus == 8) value += 333330;
            else if (plus == 9) value += 1000000;
            else if (plus == 10) value += 1033330;
            else if (plus == 11) value += 1101230;
            else if (plus == 12) value += 1212340;
            return value;
        }

        private static int BattlePowerOf(uint id, int socketOne, int socketTwo, int plus)
        {
            int value = 0;
            int quality = (int)(id % 10);
            if (quality >= 5) value += quality - 5;
            value += plus;
            if (socketOne != 0) value++;
            if (socketTwo != 0) value++;
            if (socketOne % 10 == 3) value++;
            if (socketTwo % 10 == 3) value++;
            if (id / 1000 == 421 || id.ToString()[0] == '5') value *= 2;
            return value;
        }

        private static DerivedImage EmptyDerived(string detail)
        {
            var image = new DerivedImage();
            image.Ok = false;
            image.Detail = detail;
            image.Items = new DerivedItem[0];
            return image;
        }

        private static DerivedItem DeriveItem(SavedItem item, DateTime clock, uint[] catalogIds, uint[] catalogLevels, uint[] catalogPercents)
        {
            var derived = new DerivedItem();
            derived.ItemUid = item.Uid;
            derived.Token = item.Token;
            derived.Days = item.Days;
            derived.DayStampRaw = item.DayStampRaw;
            derived.RawDayStampIsZero = item.DayStampRaw == 0;
            derived.Lock = item.Lock;
            derived.UnlockEndRaw = item.UnlockEndRaw;
            derived.RawUnlockIsZero = item.UnlockEndRaw == 0;
            derived.RefineItem = item.RefineItem;
            derived.RefineryTimeRaw = item.RefineryTimeRaw;
            derived.RawRefineryTimeIsZero = item.RefineryTimeRaw == 0;
            derived.Purification = DerivePurification(item.Purification, clock);
            derived.PermanentRefinery = DeriveRefinery(item.PermanentRefinery, clock, true, catalogIds, catalogLevels, catalogPercents);
            derived.TemporaryRefinery = DeriveRefinery(item.TemporaryRefinery, clock, false, catalogIds, catalogLevels, catalogPercents);
            ApplyDay(derived, item, clock);
            ApplyLock(derived, item, clock);
            ApplySavedRefinery(derived, item, clock);
            ApplyCurrentRefinery(derived, item, clock);
            return derived;
        }

        private static void ApplyDay(DerivedItem derived, SavedItem item, DateTime clock)
        {
            DateTime stamp;
            if (item.Days < 0 || item.Days > byte.MaxValue || !TryBinary(item.DayStampRaw, out stamp))
            {
                derived.DayStatus = DerivedStatus.Malformed;
                return;
            }
            if (item.Days == 0)
            {
                derived.DayStatus = DerivedStatus.Absent;
                return;
            }
            DateTime expiry;
            try { expiry = stamp.AddDays(item.Days); }
            catch (ArgumentOutOfRangeException)
            {
                derived.DayStatus = DerivedStatus.Overflow;
                return;
            }
            if (clock >= expiry)
            {
                derived.DayStatus = DerivedStatus.Expired;
                derived.LoaderWouldDelete = true;
                return;
            }
            long seconds;
            if (!TrySecondDelta(stamp, (uint)(item.Days * 24 * 60 * 60), clock, out seconds) || seconds < 0 || seconds > uint.MaxValue)
            {
                derived.DayStatus = DerivedStatus.Overflow;
                return;
            }
            derived.DayStatus = DerivedStatus.Active;
            derived.TimeLeftSeconds = (uint)seconds;
        }

        private static void ApplyLock(DerivedItem derived, SavedItem item, DateTime clock)
        {
            DateTime unlock;
            if (!TryBinary(item.UnlockEndRaw, out unlock))
            {
                derived.LockStatus = DerivedStatus.Malformed;
                return;
            }
            if (item.Lock != 2)
            {
                derived.LockStatus = DerivedStatus.NotTimed;
                return;
            }
            if (clock >= unlock)
            {
                derived.LockStatus = DerivedStatus.Expired;
                derived.LoaderWouldClearLock = true;
                return;
            }
            derived.LockStatus = DerivedStatus.Active;
        }

        private static void ApplySavedRefinery(DerivedItem derived, SavedItem item, DateTime clock)
        {
            if (item.RefineItem == 0)
            {
                derived.SavedRefineryStatus = item.RefineryTimeRaw == 0 ? DerivedStatus.StoredZero : DerivedStatus.Absent;
                return;
            }
            if (item.RefineryTimeRaw == 0)
            {
                derived.SavedRefineryStatus = DerivedStatus.StoredZero;
                return;
            }
            DateTime when;
            if (!TryBinary(item.RefineryTimeRaw, out when))
            {
                derived.SavedRefineryStatus = DerivedStatus.Malformed;
                return;
            }
            if (clock > when)
            {
                derived.SavedRefineryStatus = DerivedStatus.Expired;
                derived.LoaderWouldClearSavedRefinery = true;
                return;
            }
            derived.SavedRefineryStatus = DerivedStatus.Active;
        }

        private static void ApplyCurrentRefinery(DerivedItem derived, SavedItem item, DateTime clock)
        {
            int? credit = null;
            if (item.PermanentRefinery != null && item.PermanentRefinery.StabilizationStored)
                credit = item.PermanentRefinery.StabilizationPoints;
            if (item.TemporaryRefinery != null && item.TemporaryRefinery.StabilizationStored)
                credit = credit.HasValue ? Math.Max(credit.Value, item.TemporaryRefinery.StabilizationPoints) : item.TemporaryRefinery.StabilizationPoints;
            bool permanentReady = item.PermanentRefinery == null || derived.PermanentRefinery.Status == DerivedStatus.Permanent;
            bool temporaryReady = item.TemporaryRefinery == null || derived.TemporaryRefinery.Status == DerivedStatus.Active || derived.TemporaryRefinery.Status == DerivedStatus.Expired;
            if (!permanentReady || !temporaryReady) return;
            if (item.PermanentRefinery != null)
                credit = credit.HasValue ? Math.Max(credit.Value, RefineryProgress.Cost(item.PermanentRefinery.Level)) : RefineryProgress.Cost(item.PermanentRefinery.Level);
            derived.DerivedCredit = credit;
            var state = new RefineryProgress.State();
            if (item.PermanentRefinery != null)
            {
                DateTime added;
                if (!TryBinary(item.PermanentRefinery.AddedOnRaw, out added)) return;
                state.Permanent = new Network.GamePackets.ItemAdding.Refinery_();
                state.Permanent.Available = true;
                state.Permanent.ItemUID = item.Uid;
                state.Permanent.EffectID = item.PermanentRefinery.AddingId;
                state.Permanent.EffectLevel = item.PermanentRefinery.Level;
                state.Permanent.EffectPercent = item.PermanentRefinery.Percent;
                state.Permanent.EffectDuration = 0;
                state.Permanent.AddedOn = added;
            }
            if (item.TemporaryRefinery != null)
            {
                DateTime added;
                if (!TryBinary(item.TemporaryRefinery.AddedOnRaw, out added)) return;
                state.Temporary = new Network.GamePackets.ItemAdding.Refinery_();
                state.Temporary.Available = true;
                state.Temporary.ItemUID = item.Uid;
                state.Temporary.EffectID = item.TemporaryRefinery.AddingId;
                state.Temporary.EffectLevel = item.TemporaryRefinery.Level;
                state.Temporary.EffectPercent = item.TemporaryRefinery.Percent;
                state.Temporary.EffectDuration = item.TemporaryRefinery.Duration;
                state.Temporary.AddedOn = added;
            }
            Network.GamePackets.ItemAdding.Refinery_ current = state.Current(clock);
            derived.CurrentRefineryId = current.EffectID;
            // Load preserves a legacy saved id without addition rows, unless the
            // preceding saved-refinery expiry step already cleared that id.
            derived.LoaderWouldProjectRefinery = item.PermanentRefinery != null || item.TemporaryRefinery != null
                || item.RefineItem == 0 || derived.LoaderWouldClearSavedRefinery;
            derived.LoaderWouldReplaceRefineItem = derived.LoaderWouldProjectRefinery && current.EffectID != item.RefineItem;
        }

        private static DerivedEffect DerivePurification(SavedAddition row, DateTime clock)
        {
            var effect = new DerivedEffect();
            if (row == null)
            {
                effect.Status = DerivedStatus.Absent;
                return effect;
            }
            effect.AddedOnRaw = row.AddedOnRaw;
            effect.Duration = row.Duration;
            effect.AddingId = row.AddingId;
            effect.Token = row.Token;
            DateTime added;
            if (!TryBinary(row.AddedOnRaw, out added))
            {
                effect.Status = DerivedStatus.Malformed;
                return effect;
            }
            if (row.Duration == 0)
            {
                effect.Status = DerivedStatus.Permanent;
                return effect;
            }
            long seconds;
            if (!TrySecondDelta(added, row.Duration, clock, out seconds) || seconds < int.MinValue || seconds > int.MaxValue)
            {
                effect.Status = DerivedStatus.Overflow;
                return effect;
            }
            effect.SecondsRemaining = seconds;
            if (seconds <= 0)
            {
                effect.Status = DerivedStatus.Expired;
                effect.LoaderWouldDelete = true;
                return effect;
            }
            effect.Status = DerivedStatus.Active;
            return effect;
        }

        private static DerivedEffect DeriveRefinery(SavedAddition row, DateTime clock, bool permanent, uint[] catalogIds, uint[] catalogLevels, uint[] catalogPercents)
        {
            var effect = new DerivedEffect();
            if (row == null)
            {
                effect.Status = DerivedStatus.Absent;
                return effect;
            }
            effect.AddedOnRaw = row.AddedOnRaw;
            effect.Duration = row.Duration;
            effect.AddingId = row.AddingId;
            effect.Token = row.Token;
            if (permanent != (row.Duration == 0))
            {
                effect.Status = DerivedStatus.Malformed;
                return effect;
            }
            DerivedStatus catalog = CatalogStatus(row, catalogIds, catalogLevels, catalogPercents);
            if (catalog != DerivedStatus.Active)
            {
                effect.Status = catalog;
                return effect;
            }
            DateTime added;
            if (!TryBinary(row.AddedOnRaw, out added))
            {
                effect.Status = DerivedStatus.Malformed;
                return effect;
            }
            if (permanent || row.Duration == 0)
            {
                effect.Status = DerivedStatus.Permanent;
                return effect;
            }
            long seconds;
            if (!TrySecondDelta(added, row.Duration, clock, out seconds))
            {
                effect.Status = DerivedStatus.Overflow;
                return effect;
            }
            effect.SecondsRemaining = seconds;
            effect.Status = clock < added.AddSeconds(row.Duration) ? DerivedStatus.Active : DerivedStatus.Expired;
            return effect;
        }

        private static DerivedStatus CatalogStatus(SavedAddition row, uint[] catalogIds, uint[] catalogLevels, uint[] catalogPercents)
        {
            for (int i = 0; i < catalogIds.Length; i++)
            {
                if (catalogIds[i] != row.AddingId) continue;
                if (catalogLevels[i] != row.Level || catalogPercents[i] != row.Percent) return DerivedStatus.MismatchedCatalog;
                if (RefineryProgress.Cost(row.Level) == 0) return DerivedStatus.UnknownCatalog;
                return DerivedStatus.Active;
            }
            return DerivedStatus.UnknownCatalog;
        }

        private static bool TryBinary(long raw, out DateTime value)
        {
            try
            {
                value = DateTime.FromBinary(raw);
                return true;
            }
            catch (ArgumentException)
            {
                value = new DateTime(0);
                return false;
            }
        }

        private static bool TrySecondDelta(DateTime start, uint duration, DateTime clock, out long seconds)
        {
            seconds = 0;
            try
            {
                double delta = new TimeSpan(start.AddSeconds(duration).Ticks).TotalSeconds - new TimeSpan(clock.Ticks).TotalSeconds;
                // Item time casts to uint; purification casts to int; State.Current
                // compares dates without either limit. Apply bounds per consumer.
                seconds = (long)delta;
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static Member[] CopyMembers(Member[] source)
        {
            if (source == null) throw new InvalidOperationException("incomplete roster");
            var copy = new Member[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                Member row = source[i];
                if (row == null) throw new InvalidOperationException("incomplete roster");
                var member = new Member();
                member.Uid = row.Uid;
                member.Name = row.Name;
                member.Rank = row.Rank;
                member.Level = row.Level;
                member.Class = row.Class;
                member.Spouse = row.Spouse;
                member.LastLoginRaw = row.LastLoginRaw;
                member.SilverDonation = row.SilverDonation;
                member.ConquerPointDonation = row.ConquerPointDonation;
                member.ArsenalDonation = row.ArsenalDonation;
                member.PkPoints = row.PkPoints;
                member.Exploits = row.Exploits;
                member.CtfCpsReward = row.CtfCpsReward;
                member.CtfSilverReward = row.CtfSilverReward;
                member.Rouses = row.Rouses;
                member.Lilies = row.Lilies;
                member.Tulips = row.Tulips;
                member.Orchids = row.Orchids;
                member.StoredUnionId = row.StoredUnionId;
                member.UnionExploits = row.UnionExploits;
                member.UnionOfficial = row.UnionOfficial;
                member.UnionHarem = row.UnionHarem;
                member.UnionGuards = row.UnionGuards;
                member.EntityToken = row.EntityToken;
                member.Face = row.Face;
                member.Body = row.Body;
                member.BodyDigit = row.BodyDigit;
                member.LoginFallbackGender = row.LoginFallbackGender;
                member.ConcatenatedMesh = row.ConcatenatedMesh;
                member.Nobility = CopyNobility(row.Nobility);
                copy[i] = member;
            }
            return copy;
        }

        private static ArsenalRow[] CopyArsenal(ArsenalRow[] source)
        {
            if (source == null) throw new InvalidOperationException("incomplete arsenal");
            var copy = new ArsenalRow[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                ArsenalRow row = source[i];
                if (row == null) throw new InvalidOperationException("incomplete arsenal");
                var arsenal = new ArsenalRow();
                arsenal.Id = row.Id;
                arsenal.Unlocked = row.Unlocked;
                arsenal.Enhancement = row.Enhancement;
                arsenal.BattlePower = row.BattlePower;
                if (row.EnhancementExpiresUtcTicks < 0 || row.EnhancementExpiresUtcTicks > DateTime.MaxValue.Ticks)
                    throw new InvalidOperationException("invalid arsenal deadline");
                arsenal.EnhancementExpiresUtcTicks = row.EnhancementExpiresUtcTicks;
                copy[i] = arsenal;
            }
            return copy;
        }

        private static Inscription[] CopyInscriptions(Inscription[] source)
        {
            if (source == null) throw new InvalidOperationException("incomplete inscription");
            var copy = new Inscription[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                Inscription row = source[i];
                if (row == null || row.Item == null) throw new InvalidOperationException("incomplete inscribed item");
                var inscription = new Inscription();
                inscription.ArsenalId = row.ArsenalId;
                inscription.ItemUid = row.ItemUid;
                inscription.OwnerUid = row.OwnerUid;
                inscription.OwnerName = row.OwnerName;
                inscription.Donation = row.Donation;
                inscription.ItemId = row.ItemId;
                inscription.Item = CopyItem(row.Item);
                copy[i] = inscription;
            }
            return copy;
        }

        private static SavedItem CopyItem(SavedItem row)
        {
            var copy = new SavedItem();
            copy.Id = row.Id;
            copy.Uid = row.Uid;
            copy.Durability = row.Durability;
            copy.MaxDurability = row.MaxDurability;
            copy.Position = row.Position;
            copy.Agate = row.Agate;
            copy.SocketProgress = row.SocketProgress;
            copy.PlusProgress = row.PlusProgress;
            copy.SocketOne = row.SocketOne;
            copy.SocketTwo = row.SocketTwo;
            copy.Effect = row.Effect;
            copy.Plus = row.Plus;
            copy.Bless = row.Bless;
            copy.Bound = row.Bound;
            copy.Enchant = row.Enchant;
            copy.Lock = row.Lock;
            copy.UnlockEndRaw = row.UnlockEndRaw;
            copy.Suspicious = row.Suspicious;
            copy.SuspiciousStartRaw = row.SuspiciousStartRaw;
            copy.Color = row.Color;
            copy.Warehouse = row.Warehouse;
            copy.StackSize = row.StackSize;
            copy.RefineItem = row.RefineItem;
            copy.RefineryTimeRaw = row.RefineryTimeRaw;
            copy.InWardrobe = row.InWardrobe;
            copy.DayStampRaw = row.DayStampRaw;
            copy.Days = row.Days;
            copy.PerfectionLevel = row.PerfectionLevel;
            copy.PerfectionOwner = row.PerfectionOwner;
            copy.PerfectionOwnerGuid = row.PerfectionOwnerGuid;
            copy.PerfectionProgress = row.PerfectionProgress;
            copy.Signature = row.Signature;
            copy.RunesExp = row.RunesExp;
            copy.Relic1 = row.Relic1;
            copy.Relic2 = row.Relic2;
            copy.Relic3 = row.Relic3;
            copy.Relic4 = row.Relic4;
            copy.Relic5 = row.Relic5;
            copy.Inscribed = row.Inscribed;
            copy.NextSteedColorRaw = row.NextSteedColorRaw;
            copy.Token = row.Token;
            copy.Purification = CopyAddition(row.Purification);
            copy.PermanentRefinery = CopyAddition(row.PermanentRefinery);
            copy.TemporaryRefinery = CopyAddition(row.TemporaryRefinery);
            copy.StabilizationCredit = row.StabilizationCredit;
            return copy;
        }

        private static SavedNobility CopyNobility(SavedNobility row)
        {
            if (row == null) return null;
            if (row.Name == null) throw new InvalidOperationException("incomplete nobility");
            var copy = new SavedNobility();
            copy.EntityUid = row.EntityUid;
            copy.EntityToken = row.EntityToken;
            copy.Name = row.Name;
            copy.Donation = row.Donation;
            copy.Gender = row.Gender;
            copy.Mesh = row.Mesh;
            copy.Rey = row.Rey;
            copy.DurationRaw = row.DurationRaw;
            copy.DerivedRank = row.DerivedRank;
            copy.PacketGender = row.PacketGender;
            return copy;
        }

        private static bool TryAppearance(object faceValue, object bodyValue, out int? face, out int? body, out int? digit, out int? login, out uint? mesh)
        {
            face = null;
            body = null;
            digit = null;
            login = null;
            mesh = null;
            if (faceValue != null && faceValue != DBNull.Value)
            {
                if (!StoredInteger(faceValue, 0, 65535)) return false;
                face = (int)AsInt64(faceValue);
            }
            if (bodyValue != null && bodyValue != DBNull.Value)
            {
                if (!StoredInteger(bodyValue, 0, 65535)) return false;
                body = (int)AsInt64(bodyValue);
                digit = body.Value % 10;
                login = digit.Value >= 3 ? 0 : 1;
            }
            if (face.HasValue && body.HasValue)
            {
                uint parsed;
                if (uint.TryParse(face.Value.ToString() + body.Value.ToString(), out parsed)) mesh = parsed;
            }
            return true;
        }

        // Locks only the nobility rows of characters already in this snapshot. Rank stays unresolved.
        private static Snapshot AttachProvenance(NpgsqlConnection c, NpgsqlTransaction t, Snapshot snapshot)
        {
            snapshot.BlackList = null;
            snapshot.BlackListState = "unsupported";
            var expected = new Dictionary<uint, Guid>();
            foreach (var member in snapshot.Members) expected[member.Uid] = member.EntityToken;
            if (snapshot.Union != null)
                foreach (var member in snapshot.Union.Members) expected[member.Uid] = member.EntityToken;
            foreach (var union in snapshot.OtherUnions)
                foreach (var member in union.Members) expected[member.Uid] = member.EntityToken;
            var uids = new List<string>();
            var tokens = new List<string>();
            foreach (var pair in expected)
            {
                uids.Add(pair.Key.ToString());
                tokens.Add(pair.Value.ToString());
            }
            if (uids.Count == 0) return null;
            var found = new Dictionary<uint, SavedNobility>();
            using (var q = Command(c, t, @"SELECT entityuid, entityname, donation, gender, mesh, rey, duracionrey, revora_entity_token
FROM nobility WHERE entityuid::text = ANY(@uids) OR revora_entity_token::text = ANY(@tokens)
ORDER BY entityuid FOR SHARE", "uids", uids.ToArray(), "tokens", tokens.ToArray()))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (!StoredInteger(reader.GetValue(0), 1, uint.MaxValue)) return Conflict("unsupported saved nobility");
                    uint id = AsUInt32(reader.GetValue(0));
                    ulong? donation = null;
                    int? gender = null;
                    uint? mesh = null;
                    int? rey = null;
                    if (!reader.IsDBNull(2))
                    {
                        if (!StoredInteger(reader.GetValue(2), 0, ulong.MaxValue)) return Conflict("unsupported saved nobility");
                        donation = AsUInt64(reader.GetValue(2));
                    }
                    if (!reader.IsDBNull(3))
                    {
                        if (!StoredInteger(reader.GetValue(3), 0, 255)) return Conflict("unsupported saved nobility");
                        gender = (int)AsInt64(reader.GetValue(3));
                    }
                    if (!reader.IsDBNull(4))
                    {
                        if (!StoredInteger(reader.GetValue(4), 0, uint.MaxValue)) return Conflict("unsupported saved nobility");
                        mesh = AsUInt32(reader.GetValue(4));
                    }
                    if (!reader.IsDBNull(5))
                    {
                        if (!StoredInteger(reader.GetValue(5), 0, 255)) return Conflict("unsupported saved nobility");
                        rey = (int)AsInt64(reader.GetValue(5));
                    }
                    if (reader.IsDBNull(7) || reader.GetGuid(7) == Guid.Empty)
                        return Conflict("nobility row has no character lifetime");
                    Guid life = reader.GetGuid(7);
                    Guid marked;
                    if (!expected.TryGetValue(id, out marked))
                        return Conflict("nobility identity does not match the character row");
                    if (marked != life) return Conflict("nobility row does not match the character lifetime");
                    if (found.ContainsKey(id)) return Conflict("duplicate nobility row");
                    var row = new SavedNobility();
                    row.EntityUid = id;
                    row.EntityToken = life;
                    row.Name = AsText(reader.GetValue(1));
                    row.Donation = donation;
                    row.Gender = gender;
                    row.Mesh = mesh;
                    row.Rey = rey;
                    row.DurationRaw = reader.IsDBNull(6) ? null : AsText(reader.GetValue(6));
                    row.DerivedRank = null;
                    if (gender.HasValue) row.PacketGender = gender.Value == 3 ? gender.Value - 2 : gender.Value + 1;
                    found[id] = row;
                }
            }
            foreach (var member in snapshot.Members) AssignMemberNobility(found, member);
            if (snapshot.Union != null)
                foreach (var member in snapshot.Union.Members) AssignUnionNobility(found, member);
            foreach (var union in snapshot.OtherUnions)
                foreach (var member in union.Members) AssignUnionNobility(found, member);
            return null;
        }

        private static void AssignMemberNobility(Dictionary<uint, SavedNobility> found, Member member)
        {
            SavedNobility row;
            if (found.TryGetValue(member.Uid, out row)) member.Nobility = CopyNobility(row);
        }

        private static void AssignUnionNobility(Dictionary<uint, SavedNobility> found, UnionMember member)
        {
            SavedNobility row;
            if (found.TryGetValue(member.Uid, out row)) member.Nobility = CopyNobility(row);
        }

        private static SavedAddition CopyAddition(SavedAddition row)
        {
            if (row == null) return null;
            var copy = new SavedAddition();
            copy.RowId = row.RowId;
            copy.Token = row.Token;
            copy.AddingType = row.AddingType;
            copy.AddingId = row.AddingId;
            copy.Level = row.Level;
            copy.Percent = row.Percent;
            copy.Duration = row.Duration;
            copy.AddedOnRaw = row.AddedOnRaw;
            copy.StabilizationStored = row.StabilizationStored;
            copy.StabilizationPoints = row.StabilizationPoints;
            return copy;
        }

        private static Snapshot ReadCore(uint guildId, uint entityUid, bool requireRequester)
        {
            try
            {
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open();
                    using (var t = c.BeginTransaction(IsolationLevel.RepeatableRead))
                    {
                        Snapshot snapshot = ReadLocked(c, t, guildId, entityUid, requireRequester);
                        t.Commit();
                        return snapshot;
                    }
                }
            }
            catch (Exception ex)
            {
                var failed = new Snapshot();
                failed.Kind = Kind.ReadFailure;
                failed.Detail = FailureDetail(ex);
                failed.Members = new Member[0];
                failed.Arsenal = new ArsenalRow[0];
                failed.Inscriptions = new Inscription[0];
                failed.Allies = new Relation[0];
                failed.Enemies = new Relation[0];
                return failed;
            }
        }

        private static Snapshot ReadLocked(NpgsqlConnection c, NpgsqlTransaction t, uint guildId, uint entityUid, bool requireRequester)
        {
            // Repeatable read supplies a single MVCC snapshot across all statements, including
            // absent rows. Locks are guild, ordered identity rows, then ordered entity rows.
            // Nobility rows for those characters are share-locked after the unions that name them.
            // The whole nobility table is not locked, so place-dependent rank stays unresolved.
            // A concurrent change before a later lock may raise 40001; return ReadFailure.
            // Locks end at return: this read does not authorize a later mutation/publication.
            bool found = false;
            bool managed = false;
            Guid incarnation = Guid.Empty;
            var snapshot = new Snapshot();
            using (var q = Command(c, t, @"SELECT incarnation, name, leadername, silverfund, conquerpointfund, bulletin,
enrollmentdate, bulletindate, levelrequirement, rebornrequirement, classrequirement, autorecruit, unionid, wins, losts,
ctfpoints, ctfreward, maxenchant, adv, ad_rate, adlevelrequirement, adrebornrequirement, adclassrequirement,
malefemale, adend, ctfdonationcps, ctfdonationsilver, ctfdonationcpsold, ctfdonationsilverold
FROM guilds WHERE id::text=@id FOR SHARE", "id", guildId.ToString()))
            using (var reader = q.ExecuteReader())
            {
                if (!reader.Read()) found = false;
                else
                {
                    found = true;
                    managed = !reader.IsDBNull(0);
                    incarnation = managed ? reader.GetGuid(0) : Guid.Empty;
                    snapshot.GuildId = guildId;
                    snapshot.Incarnation = incarnation;
                    snapshot.Name = AsText(reader.GetValue(1));
                    snapshot.LeaderName = AsText(reader.GetValue(2));
                    snapshot.SilverFund = AsUInt64(reader.GetValue(3));
                    snapshot.ConquerPointFund = AsUInt32(reader.GetValue(4));
                    snapshot.Bulletin = AsText(reader.GetValue(5));
                    snapshot.EnrollmentDate = AsUInt32(reader.GetValue(6));
                    snapshot.BulletinDate = AsUInt32(reader.GetValue(7));
                    snapshot.LevelRequirement = AsUInt32(reader.GetValue(8));
                    snapshot.RebornRequirement = AsUInt32(reader.GetValue(9));
                    snapshot.ClassRequirement = AsUInt32(reader.GetValue(10));
                    snapshot.AutoRecruit = AsUInt32(reader.GetValue(11));
                    snapshot.UnionId = AsUInt32(reader.GetValue(12));
                    snapshot.Wins = AsUInt32(reader.GetValue(13));
                    snapshot.Losses = AsUInt32(reader.GetValue(14));
                    snapshot.CtfPoints = AsUInt32(reader.GetValue(15));
                    snapshot.CtfReward = AsUInt32(reader.GetValue(16));
                    snapshot.MaxEnchant = AsUInt32(reader.GetValue(17));
                    snapshot.Advertisement = AsText(reader.GetValue(18));
                    snapshot.AdvertisementRate = AsUInt32(reader.GetValue(19));
                    snapshot.AdLevelRequirement = AsUInt32(reader.GetValue(20));
                    snapshot.AdRebornRequirement = AsUInt32(reader.GetValue(21));
                    snapshot.AdClassRequirement = AsUInt32(reader.GetValue(22));
                    snapshot.MaleFemale = AsUInt32(reader.GetValue(23));
                    snapshot.AdvertisementEndsRaw = AsInt64(reader.GetValue(24));
                    snapshot.CtfDonationCps = AsUInt32(reader.GetValue(25));
                    snapshot.CtfDonationSilver = AsUInt32(reader.GetValue(26));
                    snapshot.CtfDonationCpsOld = AsUInt32(reader.GetValue(27));
                    snapshot.CtfDonationSilverOld = AsUInt32(reader.GetValue(28));
                    snapshot.Members = new Member[0];
                }
            }
            if (!found) return MissingGuild(c, t, guildId, entityUid, requireRequester);
            Action held = AfterGuildLock;
            if (held != null) held();
            if (!managed) return LegacyRow(c, t, snapshot, guildId, entityUid, requireRequester);
            if (incarnation == Guid.Empty) return Conflict("empty managed incarnation");
            return ManagedRow(c, t, snapshot, guildId, entityUid, incarnation, requireRequester);
        }

        private static Snapshot MissingGuild(NpgsqlConnection c, NpgsqlTransaction t, uint guildId, uint entityUid, bool requireRequester)
        {
            if (HasUnionBinding(c, t, guildId)) return Conflict("union binding owner missing");
            if (HasAssociations(c, t, guildId)) return Conflict("association owner missing");
            object owner = Scalar(c, t, "SELECT incarnation FROM revora_guild_member_identity WHERE entity_uid::text=@uid OR guild_id::text=@id ORDER BY entity_uid FOR SHARE",
                "uid", entityUid.ToString(), "id", guildId.ToString());
            if (owner == null || owner == DBNull.Value)
            {
                Snapshot other = OtherManaged(c, t, guildId, entityUid, requireRequester);
                if (other != null) return other;
                return None();
            }
            return Conflict("managed owner missing");
        }

        private static Snapshot LegacyRow(NpgsqlConnection c, NpgsqlTransaction t, Snapshot snapshot, uint guildId, uint entityUid, bool requireRequester)
        {
            if (HasUnionBinding(c, t, guildId)) return Conflict("union binding on a legacy guild");
            if (HasAssociations(c, t, guildId)) return Conflict("association identity on a legacy guild");
            object mark = Scalar(c, t, "SELECT incarnation FROM revora_guild_member_identity WHERE entity_uid::text=@uid OR guild_id::text=@id ORDER BY entity_uid FOR SHARE",
                "uid", entityUid.ToString(), "id", guildId.ToString());
            if (mark != null && mark != DBNull.Value) return Conflict("member identity on a legacy guild");
            Snapshot other = OtherManaged(c, t, guildId, entityUid, requireRequester);
            if (other != null) return other;
            snapshot.Kind = Kind.None;
            snapshot.Detail = "no managed identity";
            return snapshot;
        }

        // A saved membership in another managed guild is not a legacy answer for the requested id.
        private static Snapshot OtherManaged(NpgsqlConnection c, NpgsqlTransaction t, uint guildId, uint entityUid, bool requireRequester)
        {
            if (!requireRequester) return null;
            object saved = Scalar(c, t, "SELECT COALESCE(guildid,0) FROM entities WHERE uid::text=@uid FOR SHARE", "uid", entityUid.ToString());
            if (saved == null || saved == DBNull.Value) return Conflict("saved entity is missing");
            uint savedGuild = AsUInt32(saved);
            if (savedGuild == 0 || savedGuild == guildId) return null;
            object life = Scalar(c, t, "SELECT incarnation FROM guilds WHERE id::text=@id FOR SHARE", "id", savedGuild.ToString());
            if (life == null) return null;
            if (life == DBNull.Value) return null;
            if ((Guid)life == Guid.Empty) return Conflict("saved entity belongs to another managed guild");
            return Conflict("saved entity belongs to another managed guild");
        }

        private static Snapshot ManagedRow(NpgsqlConnection c, NpgsqlTransaction t, Snapshot snapshot, uint guildId, uint entityUid, Guid incarnation, bool requireRequester)
        {
            if (requireRequester)
            {
                object exists = Scalar(c, t, "SELECT uid FROM entities WHERE uid::text=@uid FOR SHARE", "uid", entityUid.ToString());
                if (exists == null || exists == DBNull.Value) return Conflict("saved entity is missing");
            }
            var rows = new HashSet<long>();
            var identityTokens = new Dictionary<long, Guid>();
            bool requester = false;
            using (var q = Command(c, t, @"SELECT entity_uid, guild_id, incarnation, entity_token FROM revora_guild_member_identity
WHERE incarnation=@life OR guild_id::text=@id OR entity_uid::text=@uid ORDER BY entity_uid FOR SHARE",
                "life", incarnation, "id", guildId.ToString(), "uid", entityUid.ToString()))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                {
                    long uid = Convert.ToInt64(reader.GetValue(0));
                    int rowGuild = Convert.ToInt32(reader.GetValue(1));
                    Guid rowLife = reader.GetGuid(2);
                    if (rowLife != incarnation || rowGuild != (int)guildId || uid <= 0 || uid > uint.MaxValue)
                        return Conflict("member incarnation does not match the guild row");
                    if (reader.IsDBNull(3) || reader.GetGuid(3) == Guid.Empty)
                        return Conflict("membership mark has no character lifetime");
                    if (uid == entityUid) requester = true;
                    rows.Add(uid);
                    identityTokens[uid] = reader.GetGuid(3);
                }
            }
            if (rows.Count == 0 || (requireRequester && !requester)) return Conflict("managed guild has no matching member");
            var members = new List<Member>();
            var keys = new List<string>();
            foreach (long uid in rows) keys.Add(uid.ToString());
            using (var q = Command(c, t, @"SELECT uid, name, guildid, guildrank, level, class, spouse, lastlogin,
guildsilverdonation, guildconquerpointdonation, arsenaldonation, pkpoints, exploits, ctfcpsreward, ctfsilverreward,
guildrouses, guildlilies, guildtulips, guildorchids, unionid, unionexploits, offical, harem, guards, revora_entity_token,
face, body
FROM entities WHERE guildid::text=@id OR uid::text = ANY(@uids) ORDER BY uid FOR SHARE",
                "id", guildId.ToString(), "uids", keys.ToArray()))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                {
                    var member = new Member();
                    member.Uid = AsUInt32(reader.GetValue(0));
                    member.Name = AsText(reader.GetValue(1));
                    int entityGuild = (int)AsUInt32(reader.GetValue(2));
                    member.Rank = (int)AsUInt32(reader.GetValue(3));
                    member.Level = (int)AsUInt32(reader.GetValue(4));
                    member.Class = (int)AsUInt32(reader.GetValue(5));
                    member.Spouse = AsText(reader.GetValue(6));
                    member.LastLoginRaw = AsInt64(reader.GetValue(7));
                    member.SilverDonation = AsUInt64(reader.GetValue(8));
                    member.ConquerPointDonation = AsUInt64(reader.GetValue(9));
                    member.ArsenalDonation = AsUInt64(reader.GetValue(10));
                    member.PkPoints = AsUInt64(reader.GetValue(11));
                    member.Exploits = AsUInt32(reader.GetValue(12));
                    member.CtfCpsReward = AsUInt32(reader.GetValue(13));
                    member.CtfSilverReward = AsUInt32(reader.GetValue(14));
                    member.Rouses = AsUInt32(reader.GetValue(15));
                    member.Lilies = AsUInt32(reader.GetValue(16));
                    member.Tulips = AsUInt32(reader.GetValue(17));
                    member.Orchids = AsUInt32(reader.GetValue(18));
                    member.StoredUnionId = OptionalUInt(reader.GetValue(19));
                    member.UnionExploits = OptionalULong(reader.GetValue(20));
                    member.UnionOfficial = OptionalInt(reader.GetValue(21), 0, 65535);
                    member.UnionHarem = OptionalInt(reader.GetValue(22), 0, 65535);
                    member.UnionGuards = OptionalInt(reader.GetValue(23), 0, 65535);
                    if ((!reader.IsDBNull(19) && member.StoredUnionId == null) || (!reader.IsDBNull(20) && member.UnionExploits == null)
                        || (!reader.IsDBNull(21) && member.UnionOfficial == null) || (!reader.IsDBNull(22) && member.UnionHarem == null)
                        || (!reader.IsDBNull(23) && member.UnionGuards == null))
                        return Conflict("unsupported saved union value");
                    if (reader.IsDBNull(24) || reader.GetGuid(24) == Guid.Empty)
                        return Conflict("character row has no direct lifetime");
                    member.EntityToken = reader.GetGuid(24);
                    if (!TryAppearance(reader.GetValue(25), reader.GetValue(26), out member.Face, out member.Body, out member.BodyDigit, out member.LoginFallbackGender, out member.ConcatenatedMesh))
                        return Conflict("unsupported saved appearance");
                    Guid marked;
                    if (!identityTokens.TryGetValue(member.Uid, out marked) || marked != member.EntityToken)
                        return Conflict("character lifetime does not match the membership mark");
                    if (!rows.Contains(member.Uid)) return Conflict("saved member has no matching identity");
                    if (entityGuild != (int)guildId) return Conflict("entity membership does not match the guild row");
                    members.Add(member);
                }
            }
            if (members.Count != rows.Count) return Conflict("managed member entity is missing");
            uint leaders = 0;
            foreach (var member in members)
            {
                if (member.Rank == (int)Enums.GuildMemberRank.GuildLeader)
                {
                    if (string.IsNullOrEmpty(member.Name) || member.Name != snapshot.LeaderName)
                        return Conflict("saved leader does not match the guild row");
                    leaders++;
                    snapshot.LeaderUid = member.Uid;
                }
            }
            if (leaders != 1) return Conflict("saved leader does not match the guild row");
            snapshot.Members = members.ToArray();
            Snapshot image = SavedImage(c, t, snapshot, guildId, incarnation);
            if (image != null) return image;
            Snapshot union = ReadUnion(c, t, snapshot, guildId, incarnation);
            if (union != null) return union;
            Snapshot others = ReadRosterUnions(c, t, snapshot);
            if (others != null) return others;
            Snapshot provenance = AttachProvenance(c, t, snapshot);
            if (provenance != null) return provenance;
            snapshot.Kind = Kind.Consistent;
            snapshot.Detail = "consistent";
            return snapshot;
        }

        private static SavedUnion CopyUnion(SavedUnion row)
        {
            if (row == null) return null;
            var copy = new SavedUnion();
            copy.Id = row.Id;
            copy.Token = row.Token;
            copy.GoldBricks = row.GoldBricks;
            copy.Fund = row.Fund;
            copy.Name = row.Name;
            copy.Title = row.Title;
            copy.LeaderName = row.LeaderName;
            copy.LeaderUid = row.LeaderUid;
            copy.Bulletin = row.Bulletin;
            copy.Declaration = row.Declaration;
            copy.OfficialsRaw = row.OfficialsRaw;
            copy.HaremRaw = row.HaremRaw;
            copy.GuardsRaw = row.GuardsRaw;
            copy.Officials = (uint[])row.Officials.Clone();
            copy.Harem = (uint[])row.Harem.Clone();
            copy.Guards = (uint[])row.Guards.Clone();
            copy.Guilds = new GuildUnionLink[row.Guilds.Length];
            for (int i = 0; i < row.Guilds.Length; i++)
            {
                GuildUnionLink link = row.Guilds[i];
                if (link == null) throw new InvalidOperationException("incomplete union guild");
                copy.Guilds[i] = new GuildUnionLink { GuildId = link.GuildId, Incarnation = link.Incarnation };
            }
            copy.Members = new UnionMember[row.Members.Length];
            for (int i = 0; i < row.Members.Length; i++)
            {
                UnionMember member = row.Members[i];
                if (member == null) throw new InvalidOperationException("incomplete union member");
                var twin = new UnionMember();
                twin.Uid = member.Uid;
                twin.Name = member.Name;
                twin.Level = member.Level;
                twin.Class = member.Class;
                twin.Exploits = member.Exploits;
                twin.Official = member.Official;
                twin.Harem = member.Harem;
                twin.Guards = member.Guards;
                twin.GuildId = member.GuildId;
                twin.EntityToken = member.EntityToken;
                twin.DerivedOfficial = member.DerivedOfficial;
                twin.DerivedHarem = member.DerivedHarem;
                twin.DerivedGuards = member.DerivedGuards;
                twin.Face = member.Face;
                twin.Body = member.Body;
                twin.BodyDigit = member.BodyDigit;
                twin.LoginFallbackGender = member.LoginFallbackGender;
                twin.ConcatenatedMesh = member.ConcatenatedMesh;
                twin.Nobility = CopyNobility(member.Nobility);
                copy.Members[i] = twin;
            }
            return copy;
        }

        // guilds.unionid is a number. The union token is the lifetime. Guild members may store another UnionID.
        private static Snapshot ReadUnion(NpgsqlConnection c, NpgsqlTransaction t, Snapshot snapshot, uint guildId, Guid incarnation)
        {
            // Sweep both keys: an old incarnation under another union must not be hidden.
            Guid? boundToken = null;
            using (var q = Command(c, t, @"SELECT incarnation, guild_id, union_id, union_token FROM revora_guild_union_binding
WHERE incarnation=@life OR guild_id::text=@id ORDER BY incarnation FOR SHARE", "life", incarnation, "id", guildId.ToString()))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (snapshot.UnionId == 0 || boundToken.HasValue || reader.IsDBNull(0) || reader.GetGuid(0) != incarnation
                        || !StoredInteger(reader.GetValue(1), 1, ushort.MaxValue) || AsUInt32(reader.GetValue(1)) != guildId
                        || !StoredInteger(reader.GetValue(2), 1, uint.MaxValue) || AsUInt32(reader.GetValue(2)) != snapshot.UnionId
                        || reader.IsDBNull(3) || reader.GetGuid(3) == Guid.Empty)
                        return Conflict("guild union binding does not match this incarnation");
                    boundToken = reader.GetGuid(3);
                }
            }
            if (snapshot.UnionId == 0)
            {
                snapshot.Union = null;
                return null;
            }
            if (!boundToken.HasValue) return Conflict("guild union has no direct lifetime");
            SavedUnion loaded;
            Snapshot loadedResult = LoadUnion(c, t, snapshot.UnionId, out loaded);
            if (loadedResult != null) return loadedResult;
            if (loaded.Token != boundToken.Value) return Conflict("guild union binding does not match this incarnation");
            snapshot.Union = loaded;
            return null;
        }

        private static Snapshot LoadUnion(NpgsqlConnection c, NpgsqlTransaction t, uint unionId, out SavedUnion union)
        {
            union = null;
            using (var q = Command(c, t, @"SELECT id, goldbricks, unionfund, name, title, leadername, leaderuid, bulletin, declaration,
coreofficals, harem, guards, revora_union_token FROM unions WHERE id::text=@id FOR SHARE", "id", unionId.ToString()))
            using (var reader = q.ExecuteReader())
            {
                if (!reader.Read()) return Conflict("union row is missing");
                if (reader.IsDBNull(12) || reader.GetGuid(12) == Guid.Empty) return Conflict("union has no direct lifetime");
                if (!StoredInteger(reader.GetValue(0), 1, uint.MaxValue)
                    || !StoredInteger(reader.GetValue(1), 0, uint.MaxValue) || !StoredInteger(reader.GetValue(2), 0, ulong.MaxValue)
                    || !StoredInteger(reader.GetValue(6), 1, uint.MaxValue))
                    return Conflict("unsupported saved union value");
                union = new SavedUnion();
                union.Id = AsUInt32(reader.GetValue(0));
                union.GoldBricks = AsUInt32(reader.GetValue(1));
                union.Fund = AsUInt64(reader.GetValue(2));
                union.Name = AsText(reader.GetValue(3));
                union.Title = AsText(reader.GetValue(4));
                union.LeaderName = AsText(reader.GetValue(5));
                union.LeaderUid = AsUInt32(reader.GetValue(6));
                union.Bulletin = AsText(reader.GetValue(7));
                union.Declaration = AsText(reader.GetValue(8));
                union.Token = reader.GetGuid(12);
                if (union.Id != unionId || union.Name.Length == 0 || union.LeaderName.Length == 0)
                    return Conflict("unsupported saved union value");
                if (reader.IsDBNull(9) || reader.IsDBNull(10) || reader.IsDBNull(11)) return Conflict("union role list is missing");
                union.OfficialsRaw = AsText(reader.GetValue(9));
                union.HaremRaw = AsText(reader.GetValue(10));
                union.GuardsRaw = AsText(reader.GetValue(11));
                uint[] officials;
                uint[] harem;
                uint[] guards;
                Snapshot bad = ParseRoleList(union.OfficialsRaw, 8, out officials);
                if (bad != null) return bad;
                bad = ParseRoleList(union.HaremRaw, 10, out harem);
                if (bad != null) return bad;
                bad = ParseRoleList(union.GuardsRaw, 20, out guards);
                if (bad != null) return bad;
                union.Officials = officials;
                union.Harem = harem;
                union.Guards = guards;
            }
            var members = new List<UnionMember>();
            var memberIds = new HashSet<uint>();
            using (var q = Command(c, t, @"SELECT uid, name, level, class, unionexploits, offical, harem, guards, guildid, revora_entity_token,
face, body
FROM entities WHERE unionid::text=@id ORDER BY uid FOR SHARE", "id", union.Id.ToString()))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (!StoredInteger(reader.GetValue(0), 1, uint.MaxValue) || !StoredInteger(reader.GetValue(2), 0, 65535)
                        || !StoredInteger(reader.GetValue(3), 0, 65535) || !StoredInteger(reader.GetValue(4), 0, ulong.MaxValue)
                        || !StoredInteger(reader.GetValue(5), 0, 65535) || !StoredInteger(reader.GetValue(6), 0, 65535)
                        || !StoredInteger(reader.GetValue(7), 0, 65535))
                        return Conflict("unsupported saved union member");
                    var member = new UnionMember();
                    member.Uid = AsUInt32(reader.GetValue(0));
                    member.Name = AsText(reader.GetValue(1));
                    member.Level = (int)AsUInt32(reader.GetValue(2));
                    member.Class = (int)AsUInt32(reader.GetValue(3));
                    member.Exploits = AsUInt64(reader.GetValue(4));
                    member.Official = (int)AsUInt32(reader.GetValue(5));
                    member.Harem = (int)AsUInt32(reader.GetValue(6));
                    member.Guards = (int)AsUInt32(reader.GetValue(7));
                    if (reader.IsDBNull(8)) member.GuildId = null;
                    else if (!StoredInteger(reader.GetValue(8), 0, uint.MaxValue)) return Conflict("unsupported saved union member");
                    else member.GuildId = AsUInt32(reader.GetValue(8));
                    if (reader.IsDBNull(9) || reader.GetGuid(9) == Guid.Empty) return Conflict("character row has no direct lifetime");
                    member.EntityToken = reader.GetGuid(9);
                    if (!TryAppearance(reader.GetValue(10), reader.GetValue(11), out member.Face, out member.Body, out member.BodyDigit, out member.LoginFallbackGender, out member.ConcatenatedMesh))
                        return Conflict("unsupported saved appearance");
                    member.DerivedOfficial = DerivedOfficial(union, member.Uid);
                    member.DerivedHarem = DerivedHarem(union, member.Uid);
                    member.DerivedGuards = DerivedGuards(union, member.Uid);
                    if (member.Name.Length == 0 || !memberIds.Add(member.Uid)) return Conflict("unsupported saved union member");
                    members.Add(member);
                }
            }
            bool leader = false;
            foreach (var member in members)
                if (member.Uid == union.LeaderUid && member.Name == union.LeaderName) leader = true;
            if (!leader) return Conflict("union leader is not a saved member");
            foreach (uint id in union.Officials) if (id != 0 && !memberIds.Contains(id)) return Conflict("union role has no saved member");
            foreach (uint id in union.Harem) if (id != 0 && !memberIds.Contains(id)) return Conflict("union role has no saved member");
            foreach (uint id in union.Guards) if (id != 0 && !memberIds.Contains(id)) return Conflict("union role has no saved member");
            var boundMembers = new HashSet<uint>();
            using (var q = Command(c, t, @"SELECT entity_uid, union_id, union_token, entity_token FROM revora_union_member_binding
WHERE union_token=@token OR union_id::text=@id ORDER BY entity_uid FOR SHARE", "token", union.Token, "id", union.Id.ToString()))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (!StoredInteger(reader.GetValue(0), 1, uint.MaxValue) || !StoredInteger(reader.GetValue(1), 1, uint.MaxValue)
                        || reader.IsDBNull(2) || reader.GetGuid(2) != union.Token || AsUInt32(reader.GetValue(1)) != union.Id
                        || reader.IsDBNull(3) || reader.GetGuid(3) == Guid.Empty)
                        return Conflict("union member binding does not match this lifetime");
                    uint uid = AsUInt32(reader.GetValue(0));
                    UnionMember saved = null;
                    foreach (var candidate in members) if (candidate.Uid == uid) saved = candidate;
                    if (saved == null || saved.EntityToken != reader.GetGuid(3) || !boundMembers.Add(uid))
                        return Conflict("union member binding has no saved member");
                }
            }
            if (boundMembers.Count != members.Count) return Conflict("union member has no direct lifetime");
            var links = new List<GuildUnionLink>();
            var linkedGuilds = new HashSet<uint>();
            using (var q = Command(c, t, @"SELECT incarnation, guild_id, union_id, union_token FROM revora_guild_union_binding
WHERE union_token=@token OR union_id::text=@id
OR guild_id::text IN (SELECT id::text FROM guilds WHERE unionid::text=@id)
ORDER BY guild_id, incarnation FOR SHARE", "token", union.Token, "id", union.Id.ToString()))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (reader.IsDBNull(0) || reader.GetGuid(0) == Guid.Empty
                        || !StoredInteger(reader.GetValue(1), 1, ushort.MaxValue) || !StoredInteger(reader.GetValue(2), 1, uint.MaxValue)
                        || reader.IsDBNull(3) || reader.GetGuid(3) != union.Token || AsUInt32(reader.GetValue(2)) != union.Id)
                        return Conflict("guild union binding does not match this lifetime");
                    var link = new GuildUnionLink();
                    link.Incarnation = reader.GetGuid(0);
                    link.GuildId = AsUInt32(reader.GetValue(1));
                    if (!linkedGuilds.Add(link.GuildId)) return Conflict("duplicate guild union binding");
                    links.Add(link);
                }
            }
            var guildKeys = new List<string>();
            foreach (var link in links) guildKeys.Add(link.GuildId.ToString());
            using (var q = Command(c, t, @"SELECT id, incarnation, unionid FROM guilds
WHERE unionid::text=@id OR id::text=ANY(@guilds) ORDER BY id FOR SHARE", "id", union.Id.ToString(), "guilds", guildKeys.ToArray()))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (!StoredInteger(reader.GetValue(0), 1, ushort.MaxValue) || reader.IsDBNull(1) || reader.GetGuid(1) == Guid.Empty
                        || !StoredInteger(reader.GetValue(2), 1, uint.MaxValue) || AsUInt32(reader.GetValue(2)) != union.Id)
                        return Conflict("union binding has no saved guild");
                    uint id = AsUInt32(reader.GetValue(0));
                    GuildUnionLink link = links.Find(delegate(GuildUnionLink candidate) { return candidate.GuildId == id; });
                    if (link == null || link.Incarnation != reader.GetGuid(1) || !linkedGuilds.Remove(id))
                        return Conflict("union binding has no saved guild");
                }
            }
            if (linkedGuilds.Count != 0) return Conflict("union binding has no saved guild");
            union.Members = members.ToArray();
            union.Guilds = links.ToArray();
            return null;
        }

        private static int? DerivedOfficial(SavedUnion union, uint uid)
        {
            if (uid == union.LeaderUid) return 1;
            for (int i = 0; i < union.Officials.Length; i++)
            {
                if (union.Officials[i] != uid) continue;
                if (i == 0) return 2;
                if (i == 1) return 4;
                if (i == 2) return 8;
                if (i == 3) return 16;
                if (i == 4) return 32;
                if (i == 5) return 64;
                if (i == 6) return 128;
                return 256;
            }
            return null;
        }

        private static int? DerivedHarem(SavedUnion union, uint uid)
        {
            for (int i = 0; i < union.Harem.Length; i++)
            {
                if (union.Harem[i] != uid) continue;
                if (i == 0) return 1024;
                if (i == 1 || i == 2) return 2048;
                return 4096;
            }
            return null;
        }

        private static int? DerivedGuards(SavedUnion union, uint uid)
        {
            for (int i = 0; i < union.Guards.Length; i++)
                if (union.Guards[i] == uid) return 512;
            return null;
        }

        private static SavedUnion[] CopyUnions(SavedUnion[] source)
        {
            if (source == null) throw new InvalidOperationException("incomplete union");
            var copy = new SavedUnion[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null) throw new InvalidOperationException("incomplete union");
                copy[i] = CopyUnion(source[i]);
            }
            return copy;
        }

        // Roster UnionID values are stored metadata. A different nonzero id is another union lifetime.
        private static Snapshot ReadRosterUnions(NpgsqlConnection c, NpgsqlTransaction t, Snapshot snapshot)
        {
            var keys = new List<string>();
            foreach (var member in snapshot.Members) keys.Add(member.Uid.ToString());
            if (keys.Count == 0) keys.Add("0");
            var marks = new Dictionary<uint, Guid>();
            var markUnions = new Dictionary<uint, uint>();
            using (var q = Command(c, t, @"SELECT entity_uid, union_id, union_token, entity_token FROM revora_union_member_binding
WHERE entity_uid::text = ANY(@uids) ORDER BY entity_uid FOR SHARE", "uids", keys.ToArray()))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (!StoredInteger(reader.GetValue(0), 1, uint.MaxValue) || !StoredInteger(reader.GetValue(1), 1, uint.MaxValue)
                        || reader.IsDBNull(2) || reader.GetGuid(2) == Guid.Empty || reader.IsDBNull(3) || reader.GetGuid(3) == Guid.Empty)
                        return Conflict("union member binding does not match the character lifetime");
                    uint uid = AsUInt32(reader.GetValue(0));
                    Member roster = null;
                    foreach (var candidate in snapshot.Members) if (candidate.Uid == uid) roster = candidate;
                    if (roster == null || roster.EntityToken != reader.GetGuid(3))
                        return Conflict("union member binding does not match the character lifetime");
                    marks[uid] = reader.GetGuid(2);
                    markUnions[uid] = AsUInt32(reader.GetValue(1));
                }
            }
            var otherIds = new List<uint>();
            foreach (var member in snapshot.Members)
            {
                uint stored = member.StoredUnionId ?? 0;
                Guid marked;
                bool hasMark = marks.TryGetValue(member.Uid, out marked);
                if (stored == 0)
                {
                    if (hasMark) return Conflict("stale union mark on a member with no union");
                    continue;
                }
                if (snapshot.Union != null && stored == snapshot.Union.Id)
                {
                    if (!hasMark || marked != snapshot.Union.Token || markUnions[member.Uid] != stored)
                        return Conflict("guild member union mark does not match the guild union");
                    continue;
                }
                if (!otherIds.Contains(stored)) otherIds.Add(stored);
            }
            otherIds.Sort();
            var loaded = new List<SavedUnion>();
            foreach (uint id in otherIds)
            {
                SavedUnion union;
                Snapshot bad = LoadUnion(c, t, id, out union);
                if (bad != null) return bad;
                foreach (var member in snapshot.Members)
                {
                    if ((member.StoredUnionId ?? 0) != id) continue;
                    Guid marked;
                    if (!marks.TryGetValue(member.Uid, out marked) || marked != union.Token || markUnions[member.Uid] != id)
                        return Conflict("roster union mark does not match that union lifetime");
                }
                loaded.Add(union);
            }
            snapshot.OtherUnions = loaded.ToArray();
            return null;
        }

        private static Snapshot ParseRoleList(string raw, int count, out uint[] values)
        {
            values = new uint[0];
            string[] parts = raw.Split('#');
            if (parts.Length != count) return Conflict("union role list has the wrong length");
            var parsed = new uint[count];
            var seen = new HashSet<uint>();
            for (int i = 0; i < count; i++)
            {
                uint id;
                if (!uint.TryParse(parts[i], out id)) return Conflict("union role list is not numeric");
                if (id != 0 && !seen.Add(id)) return Conflict("duplicate union role");
                parsed[i] = id;
            }
            values = parsed;
            return null;
        }

        // Numeric arsenal and relationship rows stay unbound until an association names this incarnation.
        // A future writer must insert those marks in the same transaction as the guild row. No cascade.
        private static Snapshot SavedImage(NpgsqlConnection c, NpgsqlTransaction t, Snapshot snapshot, uint guildId, Guid incarnation)
        {
            var marks = new List<string>();
            using (var q = Command(c, t, @"SELECT kind, subject_id, peer_incarnation, guild_id, incarnation FROM revora_guild_association
WHERE incarnation=@life OR guild_id::text=@id ORDER BY incarnation, kind, subject_id FOR SHARE",
                "life", incarnation, "id", guildId.ToString()))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (AsUInt32(reader.GetValue(3)) != guildId || reader.GetGuid(4) != incarnation)
                        return Conflict("association identity does not match the guild row");
                    long subject = AsInt64(reader.GetValue(1));
                    if (subject <= 0 || subject > uint.MaxValue) return Conflict("invalid association subject");
                    marks.Add(AsText(reader.GetValue(0)) + ":" + subject.ToString() + ":" + (reader.IsDBNull(2) ? "" : reader.GetGuid(2).ToString()));
                }
            }
            Snapshot arsenal = ReadArsenal(c, t, snapshot, guildId, marks);
            if (arsenal != null) return arsenal;
            Snapshot relations = ReadRelations(c, t, snapshot, guildId, marks);
            if (relations != null) return relations;
            if (marks.Count != 0) return Conflict("association has no saved row");
            return null;
        }

        private static Snapshot ReadArsenal(NpgsqlConnection c, NpgsqlTransaction t, Snapshot snapshot, uint guildId, List<string> marks)
        {
            var rows = new List<ArsenalRow>();
            var seen = new HashSet<int>();
            using (var q = Command(c, t, @"SELECT arsenal_id, arsenal_unlocked, arsenal_enhance, syn_id_bp, enhancement_expires_utc_ticks
FROM pt_arsenal WHERE syn_id::text=@id ORDER BY arsenal_id FOR SHARE", "id", guildId.ToString()))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                {
                    var row = new ArsenalRow();
                    row.Id = (int)AsUInt32(reader.GetValue(0));
                    row.Unlocked = AsUInt32(reader.GetValue(1)) == 1;
                    row.Enhancement = AsUInt32(reader.GetValue(2));
                    row.BattlePower = AsUInt32(reader.GetValue(3));
                    if (reader.IsDBNull(4)) return Conflict("arsenal deadline is missing");
                    row.EnhancementExpiresUtcTicks = AsInt64(reader.GetValue(4));
                    if (row.EnhancementExpiresUtcTicks < 0 || row.EnhancementExpiresUtcTicks > DateTime.MaxValue.Ticks)
                        return Conflict("invalid arsenal deadline");
                    if (!Consume(marks, "arsenal", row.Id, "")) return Conflict("arsenal row has no matching incarnation");
                    seen.Add(row.Id);
                    rows.Add(row);
                }
            }
            var inscribed = new List<Inscription>();
            var seenItems = new HashSet<uint>();
            using (var q = Command(c, t, @"SELECT uid, name, iten_id, iten_atype, arsenal_donation, syn_id
FROM pt_arsenal_inscribed WHERE syn_id::text=@id OR iten_id::text IN
  (SELECT item_uid::text FROM revora_guild_item_binding WHERE incarnation=@life)
ORDER BY iten_id, syn_id FOR SHARE", "id", guildId.ToString(), "life", snapshot.Incarnation))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (AsUInt32(reader.GetValue(5)) != guildId)
                        return Conflict("bound item is inscribed in another guild");
                    var row = new Inscription();
                    row.OwnerUid = AsUInt32(reader.GetValue(0));
                    row.OwnerName = AsText(reader.GetValue(1));
                    row.ItemUid = AsUInt32(reader.GetValue(2));
                    row.ArsenalId = (int)AsUInt32(reader.GetValue(3));
                    row.Donation = AsUInt32(reader.GetValue(4));
                    if (!seenItems.Add(row.ItemUid)) return Conflict("duplicate inscribed item");
                    inscribed.Add(row);
                }
            }
            foreach (var row in inscribed)
            {
                if (!seen.Contains(row.ArsenalId)) return Conflict("inscription has no saved arsenal");
                if (!Consume(marks, "inscribed", row.ItemUid, "")) return Conflict("inscription has no matching incarnation");
                bool owner = false;
                foreach (var member in snapshot.Members)
                    if (member.Uid == row.OwnerUid && member.Name == row.OwnerName) owner = true;
                if (!owner) return Conflict("inscription owner does not match the saved roster");
                SavedItem item;
                Snapshot bad = ReadSavedItem(c, t, row, snapshot.Incarnation, out item);
                if (bad != null) return bad;
                row.Item = item;
                row.ItemId = item.Id;
            }
            // Reconcile every direct binding for this lifetime, including rows whose
            // inscription/association has vanished. No cascade may hide that evidence.
            using (var q = Command(c, t, @"SELECT item_uid, owner_uid, item_token FROM revora_guild_item_binding
WHERE incarnation=@life ORDER BY item_uid FOR SHARE", "life", snapshot.Incarnation))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                {
                    uint uid = AsUInt32(reader.GetValue(0));
                    if (!seenItems.Contains(uid)) return Conflict("item binding has no saved inscription");
                    bool matched = false;
                    foreach (var row in inscribed)
                        if (row.ItemUid == uid && row.OwnerUid == AsUInt32(reader.GetValue(1))
                            && !reader.IsDBNull(2) && row.Item.Token == reader.GetGuid(2)) matched = true;
                    if (!matched) return Conflict("item binding does not match the saved inscription");
                }
            }
            using (var q = Command(c, t, @"SELECT item_uid FROM revora_item_addition_binding
WHERE incarnation=@life ORDER BY addition_id FOR SHARE", "life", snapshot.Incarnation))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                    if (!seenItems.Contains(AsUInt32(reader.GetValue(0))))
                        return Conflict("addition binding has no saved inscription");
            }
            foreach (string mark in marks)
            {
                if (mark.StartsWith("arsenal:") || mark.StartsWith("inscribed:"))
                    return Conflict("association has no saved arsenal row");
            }
            snapshot.Arsenal = rows.ToArray();
            snapshot.Inscriptions = inscribed.ToArray();
            return null;
        }

        // Stored columns only. A future writer owns the token and binding; this read does not mint one.
        private static Snapshot ReadSavedItem(NpgsqlConnection c, NpgsqlTransaction t, Inscription row, Guid incarnation, out SavedItem item)
        {
            item = null;
            using (var q = Command(c, t, @"SELECT id, uid, durability, maximdurability, position, agate, socketprogress, plusprogress,
socketone, sockettwo, effect, plus, bless, bound, enchant, locked, unlockend, suspicious, suspiciousstart,
color, warehouse, stacksize, refineryitem, refinerytime, inwardrobe, daystamp, perfectionlevel, perfectionowner,
perfectionownerguid, perfectionprogress, signature, runesexp, relicattrib1, relicattrib2, relicattrib3, relicattrib4,
relicattrib5, days, inscribed, nextsteedcolor, entityid, revora_item_token
FROM items WHERE uid::text=@uid FOR SHARE", "uid", row.ItemUid.ToString()))
            using (var reader = q.ExecuteReader())
            {
                if (!reader.Read()) return Conflict("inscribed item row is missing");
                // Reject NULL, fractional and out-of-range values before conversions can
                // round, wrap or supply plausible zero/default gear. Clock fields remain raw.
                for (int column = 0; column < 41; column++)
                {
                    if (column == 5 || column == 27 || column == 30) continue; // stored text
                    decimal min = 0, max = uint.MaxValue;
                    if (column == 16 || column == 18 || column == 23 || column == 25)
                    { min = long.MinValue; max = long.MaxValue; }
                    else if (column == 2 || column == 3 || column == 21) max = ushort.MaxValue;
                    else if (column == 4 || column == 8 || column == 9 || column == 10
                        || column == 14 || column == 37) max = byte.MaxValue;
                    else if (column == 11) max = 12;
                    else if (column == 12) max = 7;
                    else if (column == 15) max = 2;
                    else if (column == 13 || column == 17 || column == 24 || column == 38) max = 1;
                    if (column == 0 || column == 1 || column == 21 || column == 40) min = 1;
                    if (!StoredInteger(reader.GetValue(column), min, max))
                        return Conflict("unsupported saved item value: " + reader.GetName(column));
                }
                if (!Enum.IsDefined(typeof(Enums.ItemEffect), (byte)AsInt64(reader.GetValue(10)))
                    || AsUInt32(reader.GetValue(19)) > byte.MaxValue
                    || !Enum.IsDefined(typeof(Enums.Color), (byte)AsUInt32(reader.GetValue(19))))
                    return Conflict("unsupported saved item enum");
                if (AsUInt32(reader.GetValue(40)) != row.OwnerUid) return Conflict("inscribed item ownership changed");
                if (reader.IsDBNull(41)) return Conflict("inscribed item has no direct binding");
                Guid token = reader.GetGuid(41);
                if (token == Guid.Empty) return Conflict("inscribed item has no direct binding");
                long plus = AsInt64(reader.GetValue(11));
                if (plus < 0 || plus > 12) return Conflict("unsupported saved plus");
                if (!DefinedGem(AsInt64(reader.GetValue(8))) || !DefinedGem(AsInt64(reader.GetValue(9))))
                    return Conflict("unsupported saved socket");
                int bound = AsFlag(reader.GetValue(13));
                if (bound < 0) return Conflict("unsupported saved bound");
                if (AsFlag(reader.GetValue(38)) != 1) return Conflict("inscribed item is not marked inscribed");
                uint id = AsUInt32(reader.GetValue(0));
                if (Arsenal.MetoEditionars.ArsenalPosition(id) != row.ArsenalId)
                    return Conflict("item category does not match the inscription");
                item = new SavedItem();
                item.Id = id;
                item.Uid = AsUInt32(reader.GetValue(1));
                item.Durability = (int)AsInt64(reader.GetValue(2));
                item.MaxDurability = (int)AsInt64(reader.GetValue(3));
                item.Position = (int)AsInt64(reader.GetValue(4));
                item.Agate = AsText(reader.GetValue(5));
                item.SocketProgress = AsUInt32(reader.GetValue(6));
                item.PlusProgress = AsUInt32(reader.GetValue(7));
                item.SocketOne = (int)AsInt64(reader.GetValue(8));
                item.SocketTwo = (int)AsInt64(reader.GetValue(9));
                item.Effect = (int)AsInt64(reader.GetValue(10));
                item.Plus = (int)plus;
                item.Bless = (int)AsInt64(reader.GetValue(12));
                item.Bound = bound == 1;
                item.Enchant = (int)AsInt64(reader.GetValue(14));
                item.Lock = (int)AsInt64(reader.GetValue(15));
                item.UnlockEndRaw = AsInt64(reader.GetValue(16));
                item.Suspicious = AsFlag(reader.GetValue(17)) == 1;
                item.SuspiciousStartRaw = AsInt64(reader.GetValue(18));
                item.Color = AsUInt32(reader.GetValue(19));
                item.Warehouse = AsUInt32(reader.GetValue(20));
                item.StackSize = (int)AsInt64(reader.GetValue(21));
                item.RefineItem = AsUInt32(reader.GetValue(22));
                item.RefineryTimeRaw = AsInt64(reader.GetValue(23));
                item.InWardrobe = AsFlag(reader.GetValue(24)) == 1;
                item.DayStampRaw = AsInt64(reader.GetValue(25));
                item.PerfectionLevel = AsUInt32(reader.GetValue(26));
                item.PerfectionOwner = AsText(reader.GetValue(27));
                item.PerfectionOwnerGuid = AsUInt32(reader.GetValue(28));
                item.PerfectionProgress = AsUInt32(reader.GetValue(29));
                item.Signature = AsText(reader.GetValue(30));
                item.RunesExp = AsUInt32(reader.GetValue(31));
                item.Relic1 = AsUInt32(reader.GetValue(32));
                item.Relic2 = AsUInt32(reader.GetValue(33));
                item.Relic3 = AsUInt32(reader.GetValue(34));
                item.Relic4 = AsUInt32(reader.GetValue(35));
                item.Relic5 = AsUInt32(reader.GetValue(36));
                item.Days = (int)AsInt64(reader.GetValue(37));
                item.Inscribed = true;
                item.NextSteedColorRaw = AsUInt32(reader.GetValue(39));
                item.Token = token;
            }
            using (var q = Command(c, t, @"SELECT incarnation, owner_uid, item_token FROM revora_guild_item_binding
WHERE item_uid::text=@uid FOR SHARE", "uid", row.ItemUid.ToString()))
            using (var reader = q.ExecuteReader())
            {
                if (!reader.Read() || reader.IsDBNull(0) || reader.GetGuid(0) == Guid.Empty || reader.GetGuid(0) != incarnation)
                    return Conflict("inscribed item binding does not match this guild");
                if (AsUInt32(reader.GetValue(1)) != row.OwnerUid) return Conflict("inscribed item binding owner does not match the roster");
                if (reader.IsDBNull(2) || reader.GetGuid(2) != item.Token) return Conflict("inscribed item binding does not match the saved token");
            }
            return ReadAdditions(c, t, item, incarnation);
        }

        // Numeric addition ids can be reused. A direct token on both row and binding
        // identifies the addition lifetime within the item lifetime. No expiry deletion and no refinery-catalog lookup.
        private static Snapshot ReadAdditions(NpgsqlConnection c, NpgsqlTransaction t, SavedItem item, Guid incarnation)
        {
            var rows = new List<SavedAddition>();
            using (var q = Command(c, t, @"SELECT iditemadding, uid, addingtype, addingid, addinglevel, addingpercent, duration, addedon, stabilizationpoints, revora_addition_token
FROM itemadding WHERE uid::text=@uid ORDER BY iditemadding FOR SHARE", "uid", item.Uid.ToString()))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (!StoredInteger(reader.GetValue(0), 1, long.MaxValue)
                        || !StoredInteger(reader.GetValue(1), 1, uint.MaxValue)
                        || !StoredInteger(reader.GetValue(2), 0, 1)
                        || !StoredInteger(reader.GetValue(3), 1, uint.MaxValue)
                        || !StoredInteger(reader.GetValue(4), 0, uint.MaxValue)
                        || !StoredInteger(reader.GetValue(5), 0, uint.MaxValue)
                        || !StoredInteger(reader.GetValue(6), 0, uint.MaxValue)
                        || !StoredInteger(reader.GetValue(7), long.MinValue, long.MaxValue))
                        return Conflict("unsupported saved addition value");
                    if (AsUInt32(reader.GetValue(1)) != item.Uid) return Conflict("addition row does not match the item");
                    int type = (int)AsInt64(reader.GetValue(2));
                    bool creditStored = !reader.IsDBNull(8);
                    if (type == 1 && (!creditStored || !StoredInteger(reader.GetValue(8), 0, int.MaxValue)))
                        return Conflict("unsupported saved stabilization credit");
                    if (type == 0 && creditStored && !StoredInteger(reader.GetValue(8), 0, int.MaxValue))
                        return Conflict("unsupported saved stabilization credit");
                    if (reader.IsDBNull(9) || reader.GetGuid(9) == Guid.Empty)
                        return Conflict("addition row has no direct lifetime token");
                    var addition = new SavedAddition();
                    addition.Token = reader.GetGuid(9);
                    addition.RowId = AsInt64(reader.GetValue(0));
                    addition.AddingType = type;
                    addition.AddingId = AsUInt32(reader.GetValue(3));
                    addition.Level = AsUInt32(reader.GetValue(4));
                    addition.Percent = AsUInt32(reader.GetValue(5));
                    addition.Duration = AsUInt32(reader.GetValue(6));
                    addition.AddedOnRaw = AsInt64(reader.GetValue(7));
                    addition.StabilizationStored = creditStored;
                    addition.StabilizationPoints = creditStored ? (int)AsInt64(reader.GetValue(8)) : 0;
                    rows.Add(addition);
                }
            }
            var marks = new List<AdditionMark>();
            using (var q = Command(c, t, @"SELECT addition_id, item_uid, item_token, incarnation, addition_token FROM revora_item_addition_binding
WHERE item_uid::text=@uid OR item_token=@token ORDER BY addition_id FOR SHARE",
                "uid", item.Uid.ToString(), "token", item.Token))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (!StoredInteger(reader.GetValue(0), 1, long.MaxValue)
                        || !StoredInteger(reader.GetValue(1), 1, uint.MaxValue))
                        return Conflict("unsupported saved addition binding value");
                    if (reader.IsDBNull(2) || reader.IsDBNull(3) || reader.IsDBNull(4) || reader.GetGuid(4) == Guid.Empty)
                        return Conflict("addition binding has no direct lifetime");
                    var mark = new AdditionMark();
                    mark.RowId = AsInt64(reader.GetValue(0));
                    mark.ItemUid = AsUInt32(reader.GetValue(1));
                    mark.Token = reader.GetGuid(2);
                    mark.Incarnation = reader.GetGuid(3);
                    mark.AdditionToken = reader.GetGuid(4);
                    marks.Add(mark);
                }
            }
            if (rows.Count == 0 && marks.Count == 0)
            {
                item.Purification = null;
                item.PermanentRefinery = null;
                item.TemporaryRefinery = null;
                item.StabilizationCredit = null;
                return null;
            }
            foreach (var row in rows)
            {
                AdditionMark mark = null;
                foreach (var candidate in marks)
                    if (!candidate.Used && candidate.RowId == row.RowId) mark = candidate;
                if (mark == null) return Conflict("addition row has no direct lifetime binding");
                if (mark.ItemUid != item.Uid || mark.Token != item.Token || mark.Incarnation != incarnation
                    || mark.Incarnation == Guid.Empty || mark.AdditionToken != row.Token)
                    return Conflict("addition binding does not match this item lifetime");
                mark.Used = true;
            }
            foreach (var mark in marks)
                if (!mark.Used) return Conflict("addition binding has no saved row");
            SavedAddition purification = null;
            SavedAddition permanent = null;
            SavedAddition temporary = null;
            int? credit = null;
            foreach (var row in rows)
            {
                if (row.AddingType == 0)
                {
                    if (row.Percent != 0) return Conflict("unsupported purification shape");
                    if (purification != null) return Conflict("duplicate purification");
                    purification = row;
                }
                else if (row.Duration == 0)
                {
                    if (permanent != null) return Conflict("duplicate permanent refinery");
                    permanent = row;
                }
                else
                {
                    if (temporary != null) return Conflict("duplicate temporary refinery");
                    temporary = row;
                }
                if (row.AddingType == 1)
                    credit = credit.HasValue ? Math.Max(credit.Value, row.StabilizationPoints) : row.StabilizationPoints;
            }
            item.Purification = purification;
            item.PermanentRefinery = permanent;
            item.TemporaryRefinery = temporary;
            item.StabilizationCredit = credit;
            return null;
        }

        private static bool StoredInteger(object value, decimal min, decimal max)
        {
            if (value == null || value == DBNull.Value) return false;
            decimal number = value is bool ? ((bool)value ? 1 : 0) : Convert.ToDecimal(value);
            return number == decimal.Truncate(number) && number >= min && number <= max;
        }

        private static bool HasUnionBinding(NpgsqlConnection c, NpgsqlTransaction t, uint guildId)
        {
            return Scalar(c, t, "SELECT incarnation FROM revora_guild_union_binding WHERE guild_id::text=@id ORDER BY incarnation FOR SHARE",
                "id", guildId.ToString()) != null;
        }

        internal sealed class GuildUnionLink
        {
            public Guid Incarnation;
            public uint GuildId;
        }

        private static uint? OptionalUInt(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            if (!StoredInteger(value, 0, uint.MaxValue)) return null;
            return AsUInt32(value);
        }

        private static ulong? OptionalULong(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            if (!StoredInteger(value, 0, ulong.MaxValue)) return null;
            return AsUInt64(value);
        }

        private static int? OptionalInt(object value, long min, long max)
        {
            if (value == null || value == DBNull.Value) return null;
            if (!StoredInteger(value, min, max)) return null;
            return (int)AsInt64(value);
        }

        private sealed class AdditionMark
        {
            public long RowId;
            public uint ItemUid;
            public Guid Token;
            public Guid Incarnation;
            public Guid AdditionToken;
            public bool Used;
        }

        private static bool DefinedGem(long value)
        {
            if (value < 0 || value > 255) return false;
            return Enum.IsDefined(typeof(Enums.Gem), (byte)value);
        }

        private static Snapshot ReadRelations(NpgsqlConnection c, NpgsqlTransaction t, Snapshot snapshot, uint guildId, List<string> marks)
        {
            Relation[] allies = ReadEdges(c, t, guildId, "SELECT allyid FROM guildally WHERE guildid::text=@id ORDER BY allyid FOR SHARE", "ally", marks);
            if (allies == null) return Conflict("ally row does not match its incarnation");
            Relation[] enemies = ReadEdges(c, t, guildId, "SELECT enemyid FROM guildenemy WHERE guildid::text=@id ORDER BY enemyid FOR SHARE", "enemy", marks);
            if (enemies == null) return Conflict("enemy row does not match its incarnation");
            snapshot.Allies = allies;
            snapshot.Enemies = enemies;
            return null;
        }

        private static Relation[] ReadEdges(NpgsqlConnection c, NpgsqlTransaction t, uint guildId, string query, string kind, List<string> marks)
        {
            var peers = new List<uint>();
            using (var q = Command(c, t, query,
                "id", guildId.ToString()))
            using (var reader = q.ExecuteReader())
            {
                while (reader.Read()) peers.Add(AsUInt32(reader.GetValue(0)));
            }
            var rows = new List<Relation>();
            foreach (uint peer in peers)
            {
                object life = Scalar(c, t, "SELECT incarnation FROM guilds WHERE id::text=@id FOR SHARE", "id", peer.ToString());
                // A nullable peer cannot bind a reusable numeric id to a durable guild.
                // Missing, legacy and empty-UUID peers all stay unavailable to managed recovery.
                if (life == null || life == DBNull.Value || (Guid)life == Guid.Empty) return null;
                Guid peerLife = (Guid)life;
                if (!Consume(marks, kind, peer, peerLife.ToString())) return null;
                rows.Add(new Relation { GuildId = peer, Incarnation = peerLife, LegacyPeer = false });
            }
            return rows.ToArray();
        }

        private static bool HasAssociations(NpgsqlConnection c, NpgsqlTransaction t, uint guildId)
        {
            return Scalar(c, t, "SELECT incarnation FROM revora_guild_association WHERE guild_id::text=@id ORDER BY incarnation, kind, subject_id FOR SHARE",
                "id", guildId.ToString()) != null;
        }

        private static bool Consume(List<string> marks, string kind, long subject, string peer)
        {
            string key = kind + ":" + subject.ToString() + ":" + (peer ?? "");
            return marks.Remove(key);
        }

        private static Snapshot None()
        {
            var snapshot = new Snapshot();
            snapshot.Kind = Kind.None;
            snapshot.Detail = "no managed identity";
            return snapshot;
        }

        private static Snapshot Conflict(string detail)
        {
            var snapshot = new Snapshot();
            snapshot.Kind = Kind.Contradiction;
            snapshot.Detail = detail;
            return snapshot;
        }

        private static int AsFlag(object value)
        {
            if (value == null || value == DBNull.Value) return -1;
            if (value is bool) return (bool)value ? 1 : 0;
            decimal number = Convert.ToDecimal(value);
            if (number == 0 || number == 1) return (int)number;
            return -1;
        }

        private static string AsText(object value)
        {
            if (value == null || value == DBNull.Value) return "";
            return Convert.ToString(value);
        }

        private static uint AsUInt32(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            return Convert.ToUInt32(Convert.ToDecimal(value));
        }

        private static ulong AsUInt64(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            return Convert.ToUInt64(Convert.ToDecimal(value));
        }

        private static long AsInt64(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            return Convert.ToInt64(Convert.ToDecimal(value));
        }

        private static string FailureDetail(Exception ex)
        {
            PostgresException pg = ex as PostgresException;
            if (pg == null && ex.InnerException != null) pg = ex.InnerException as PostgresException;
            if (pg != null) return ex.GetType().Name + " " + pg.SqlState;
            return ex.GetType().Name;
        }

        private static NpgsqlCommand Command(NpgsqlConnection c, NpgsqlTransaction t, string sql, params object[] values)
        {
            var q = new NpgsqlCommand(sql, c, t);
            for (int i = 0; i < values.Length; i += 2)
                q.Parameters.AddWithValue((string)values[i], values[i + 1]);
            return q;
        }

        private static object Scalar(NpgsqlConnection c, NpgsqlTransaction t, string sql, params object[] values)
        {
            using (var q = Command(c, t, sql, values))
                return q.ExecuteScalar();
        }
    }
}
