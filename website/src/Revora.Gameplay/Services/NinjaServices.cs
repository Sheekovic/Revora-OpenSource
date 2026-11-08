using System.Globalization;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Revora.Gameplay.Models;

namespace Revora.Gameplay.Services;

/// <summary>
/// Session-scoped, server-authoritative Ninja skill validation. This service
/// deliberately does not resolve entities or persist state; callers must bind
/// the supplied combatants to authenticated, authoritative world entities.
/// </summary>
public sealed class NinjaSkillEngine
{
    private const int MaximumReceiptsPerCharacter = 256;
    private readonly object gate = new();
    private readonly Dictionary<(CharacterUid Character, NinjaSkillId Skill), long> cooldowns = [];
    private readonly Dictionary<(CharacterUid Character, NinjaStatusKind Status), ActiveStatus> statuses = [];
    private readonly Dictionary<(CharacterUid Character, uint Sequence), (string Fingerprint, NinjaSkillResult Result)> receipts = [];
    private readonly Dictionary<CharacterUid, Queue<uint>> receiptOrder = [];

    public NinjaSkillResult Use(
        uint sequence,
        NinjaCombatant actor,
        NinjaCombatant? target,
        ushort requestedSkillId,
        ushort requestedSkillLevel,
        long monotonicMilliseconds)
    {
        lock (gate)
        {
        if (sequence == 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (monotonicMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(monotonicMilliseconds));
        string fingerprint = Fingerprint(actor, target, requestedSkillId, requestedSkillLevel);
        var receiptKey = (actor.CharacterUid, sequence);
        if (receipts.TryGetValue(receiptKey, out var receipt))
            return receipt.Fingerprint == fingerprint
                ? receipt.Result with { Code = NinjaSkillResultCode.Replay, Replayed = true }
                : Failure(NinjaSkillResultCode.CommandConflict, actor.Stamina);

        NinjaSkillResult result = ValidateAndApply(
            actor, target, requestedSkillId, requestedSkillLevel, monotonicMilliseconds);
        receipts.Add(receiptKey, (fingerprint, result));
        if (!receiptOrder.TryGetValue(actor.CharacterUid, out Queue<uint>? order))
            receiptOrder.Add(actor.CharacterUid, order = new());
        order.Enqueue(sequence);
        while (order.Count > MaximumReceiptsPerCharacter)
            receipts.Remove((actor.CharacterUid, order.Dequeue()));
        return result;
        }
    }

    public bool HasStatus(CharacterUid character, NinjaStatusKind status, long monotonicMilliseconds)
    {
        lock (gate)
        {
        if (!statuses.TryGetValue((character, status), out ActiveStatus active)) return false;
        if (active.ExpiresAt > monotonicMilliseconds) return true;
        statuses.Remove((character, status));
        return false;
        }
    }

    public bool TryGetActiveStatus(CharacterUid character, NinjaStatusKind status,
        long monotonicMilliseconds, out byte level, out long expiresAt)
    {
        lock (gate)
        {
            if (!statuses.TryGetValue((character, status), out ActiveStatus active) ||
                active.ExpiresAt <= monotonicMilliseconds)
            {
                statuses.Remove((character, status));
                level = 0;
                expiresAt = 0;
                return false;
            }
            level = active.Level;
            expiresAt = active.ExpiresAt;
            return true;
        }
    }

    public bool IsItemUseSuppressed(CharacterUid character, long monotonicMilliseconds) =>
        HasStatus(character, NinjaStatusKind.PoisonStar, monotonicMilliseconds);

    public bool TryTriggerCounterKill(CharacterUid defender, uint attackerUid, uint requestSequence,
        long monotonicMilliseconds, out byte skillLevel)
    {
        lock (gate)
        {
            if (!TryGetActiveStatus(defender, NinjaStatusKind.CounterKill, monotonicMilliseconds,
                    out skillLevel, out _))
                return false;
            // Exact MagicType data proves a 30% activation chance. A stable
            // server-owned roll makes replay and concurrency deterministic.
            Span<byte> material = stackalloc byte[12];
            BinaryPrimitives.WriteUInt32LittleEndian(material, defender.Value);
            BinaryPrimitives.WriteUInt32LittleEndian(material[4..], attackerUid);
            BinaryPrimitives.WriteUInt32LittleEndian(material[8..], requestSequence);
            byte[] digest = SHA256.HashData(material);
            try { return BinaryPrimitives.ReadUInt32LittleEndian(digest) % 100 < 30; }
            finally { CryptographicOperations.ZeroMemory(digest); }
        }
    }

    public void OnDeath(CharacterUid character) => ClearStatuses(character);
    public void OnRevive(CharacterUid character) => ClearStatuses(character);
    public void OnLogout(CharacterUid character) => ClearStatuses(character);
    public void OnDisconnect(CharacterUid character) => ClearStatuses(character);
    public void OnMapTransition(CharacterUid character) => ClearStatuses(character);

    public static uint CalculateTwofoldDamage(uint normalDamage, byte skillLevel)
    {
        int percent = skillLevel switch { 0 => 110, 1 => 115, 2 => 120, 3 => 125, 4 => 130, _ => -1 };
        if (percent < 0) throw new ArgumentOutOfRangeException(nameof(skillLevel));
        return checked((uint)Math.Min(uint.MaxValue, (ulong)normalDamage * (uint)percent / 100));
    }

    public static uint CalculateToxicFogCurrentHealth(uint currentHealth, byte skillLevel)
    {
        if (currentHealth == 0 || skillLevel > 4) throw new ArgumentOutOfRangeException(nameof(currentHealth));
        uint percent = checked((uint)(10 * (skillLevel + 1)));
        uint damage = checked((uint)((ulong)currentHealth * percent / 100));
        return Math.Min(Math.Max(1, damage), currentHealth - 1);
    }

    public static uint CalculateCounterKillDamage(uint normalDamage, byte skillLevel)
    {
        int percent = skillLevel switch { 0 => 20, 1 => 28, 2 => 36, 3 => 42, 4 => 50, _ => -1 };
        if (percent < 0) throw new ArgumentOutOfRangeException(nameof(skillLevel));
        return checked((uint)Math.Min(uint.MaxValue, (ulong)normalDamage * (uint)percent / 100));
    }

    public static uint CalculateFatalStrikeDamage(uint normalDamage) =>
        checked((uint)Math.Min(uint.MaxValue, (ulong)normalDamage * 5));

    public static uint CalculateArcherBaneCurrentHealth(uint currentHealth)
    {
        if (currentHealth == 0) throw new ArgumentOutOfRangeException(nameof(currentHealth));
        return Math.Min(Math.Max(1, currentHealth / 10), currentHealth - 1);
    }

    public static uint ApplyShurikenVortexIncomingDamage(uint requestedDamage) => requestedDamage == 0 ? 0u : 1u;

    private NinjaSkillResult ValidateAndApply(
        NinjaCombatant actor,
        NinjaCombatant? target,
        ushort requestedSkillId,
        ushort requestedSkillLevel,
        long now)
    {
        if (!actor.Alive) return Failure(NinjaSkillResultCode.InvalidState, actor.Stamina);
        if (!NinjaProfessionRules.IsNinja(actor.Profession))
            return Failure(NinjaSkillResultCode.WrongProfession, actor.Stamina);
        if (!NativeNinjaSkillCatalog5165.TryGet(requestedSkillId, out NinjaSkillDefinition definition))
            return Failure(NinjaSkillResultCode.SkillNotLearned, actor.Stamina);
        if (!actor.LearnedSkills.TryGetValue(definition.Id, out byte learnedLevel))
            return Failure(NinjaSkillResultCode.SkillNotLearned, actor.Stamina);
        if (requestedSkillLevel > definition.MaximumLevel || learnedLevel != requestedSkillLevel)
            return Failure(NinjaSkillResultCode.InvalidSkillLevel, actor.Stamina);
        if (actor.Level < definition.RequiredCharacterLevel)
            return Failure(NinjaSkillResultCode.InvalidState, actor.Stamina);
        if (definition.RequiresDualKatana &&
            (!actor.RightHandItemType.HasValue || !actor.LeftHandItemType.HasValue ||
             !NinjaItemRules.IsKatana(actor.RightHandItemType.Value) ||
             !NinjaItemRules.IsKatana(actor.LeftHandItemType.Value)))
            return Failure(NinjaSkillResultCode.InvalidEquipment, actor.Stamina);
        if (!ValidateTarget(definition, actor, target, out NinjaSkillResultCode targetFailure))
            return Failure(targetFailure, actor.Stamina);
        if (actor.Stamina < definition.StaminaCost)
            return Failure(NinjaSkillResultCode.InsufficientStamina, actor.Stamina);

        var cooldownKey = (actor.CharacterUid, definition.Id);
        if (cooldowns.TryGetValue(cooldownKey, out long availableAt) && now < availableAt)
            return Failure(NinjaSkillResultCode.Cooldown, actor.Stamina);

        CharacterUid statusRecipient = definition.TargetMode == NinjaTargetMode.Self
            ? actor.CharacterUid
            : target!.CharacterUid;
        if (definition.Status.HasValue && definition.DurationMilliseconds > 0 &&
            HasStatus(statusRecipient, definition.Status.Value, now))
            return Failure(NinjaSkillResultCode.InvalidState, actor.Stamina);

        ushort remaining = checked((ushort)(actor.Stamina - definition.StaminaCost));
        cooldowns[cooldownKey] = checked(now + definition.CooldownMilliseconds);
        long? expires = null;
        if (definition.Status.HasValue)
        {
            int duration = definition.Id == NinjaSkillId.PoisonStar
                ? checked((requestedSkillLevel + 1) * 5_000)
                : definition.DurationMilliseconds;
            if (duration > 0)
            {
                expires = checked(now + duration);
                statuses[(statusRecipient, definition.Status.Value)] =
                    new ActiveStatus(expires.Value, checked((byte)requestedSkillLevel));
            }
        }
        return new NinjaSkillResult(NinjaSkillResultCode.Applied, remaining, definition.Status, expires);
    }

    private static bool ValidateTarget(
        NinjaSkillDefinition definition,
        NinjaCombatant actor,
        NinjaCombatant? target,
        out NinjaSkillResultCode failure)
    {
        failure = NinjaSkillResultCode.InvalidTarget;
        if (definition.TargetMode == NinjaTargetMode.Self)
            return target is null || target.CharacterUid == actor.CharacterUid;
        if (target is null || target.CharacterUid == actor.CharacterUid) return false;
        if (!target.Alive) { failure = NinjaSkillResultCode.TargetDead; return false; }
        if (target.Position.MapId != actor.Position.MapId) { failure = NinjaSkillResultCode.WrongMap; return false; }
        if (actor.Position.ChebyshevDistance(target.Position) > definition.Range)
        {
            failure = NinjaSkillResultCode.OutOfRange;
            return false;
        }
        if (definition.TargetMode == NinjaTargetMode.Player && target.IsMonster) return false;
        if (definition.TargetMode == NinjaTargetMode.Creature && !target.IsMonster) return false;
        if (definition.MonstersOnly && !target.IsMonster) return false;
        if (definition.Id == NinjaSkillId.ArcherBane && !target.IsFlying) return false;
        return true;
    }

    private void ClearStatuses(CharacterUid character)
    {
        lock (gate)
        {
            foreach (var key in statuses.Keys.Where(key => key.Character == character).ToArray()) statuses.Remove(key);
            foreach (var key in cooldowns.Keys.Where(key => key.Character == character).ToArray()) cooldowns.Remove(key);
            foreach (var key in receipts.Keys.Where(key => key.Character == character).ToArray()) receipts.Remove(key);
            receiptOrder.Remove(character);
        }
    }

    private static NinjaSkillResult Failure(NinjaSkillResultCode code, ushort stamina) => new(code, stamina);

    private static string Fingerprint(NinjaCombatant actor, NinjaCombatant? target, ushort skill, ushort level) =>
        string.Join('|',
            actor.CharacterUid.Value.ToString(CultureInfo.InvariantCulture),
            target?.CharacterUid.Value.ToString(CultureInfo.InvariantCulture) ?? "self",
            skill.ToString(CultureInfo.InvariantCulture),
            level.ToString(CultureInfo.InvariantCulture),
            actor.Position.MapId.ToString(CultureInfo.InvariantCulture),
            actor.Position.X.ToString(CultureInfo.InvariantCulture),
            actor.Position.Y.ToString(CultureInfo.InvariantCulture));

    private readonly record struct ActiveStatus(long ExpiresAt, byte Level);
}
