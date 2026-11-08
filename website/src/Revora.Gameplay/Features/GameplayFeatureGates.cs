namespace Revora.Gameplay.Features;

public enum GameplayGateState : byte
{
    Disabled,
    Validated,
    Enabled
}

public sealed record GameplayGateEvidence(
    string Key,
    GameplayGateState State,
    int AppliedMigration,
    bool CatalogReleaseActive,
    bool ReferenceClosureValid,
    bool ExactCodecAvailable);

public sealed record GameplayGateValidation(bool Valid, IReadOnlyList<string> Failures);

public static class GameplayFeatureGates
{
    private static readonly IReadOnlyDictionary<string, GateDefinition> Definitions =
        new Dictionary<string, GateDefinition>(StringComparer.Ordinal)
        {
            ["world.catalog.complete"] = new(12, []),
            ["world.player.equipment.visibility"] = new(46, ["inventory", "equipment.stats"]),
            ["items.definitions"] = new(13, ["world.catalog.complete"]),
            ["inventory"] = new(14, ["items.definitions"]),
            ["currency.ledger"] = new(15, []),
            ["equipment.stats"] = new(16, ["inventory"]),
            ["shops"] = new(17, ["world.catalog.complete", "inventory", "currency.ledger"]),
            ["warehouse"] = new(18, ["inventory"]),
            ["items.enhancement"] = new(19, ["inventory", "equipment.stats"]),
            ["monsters"] = new(40, ["world.catalog.complete"]),
            ["combat.pk"] = new(21, ["equipment.stats", "monsters"]),
            ["drops"] = new(21, ["inventory", "combat.pk"]),
            ["skills"] = new(22, ["equipment.stats", "combat.pk"]),
            ["progression.rebirth"] = new(22, ["skills"]),
            ["social.basic"] = new(23, ["world.catalog.complete"]),
            ["trade.personalshops"] = new(24, ["inventory", "currency.ledger", "social.basic"]),
            ["guild.marriage"] = new(25, ["currency.ledger", "social.basic"]),
            ["npc.quests"] = new(26, ["world.catalog.complete", "inventory"]),
            ["events.arena.rankings"] = new(27, ["combat.pk", "npc.quests"]),
            ["housing.mining.weather"] = new(28, ["world.catalog.complete", "inventory"]),
            ["chat.gm"] = new(29, ["social.basic"]),
            ["progression.services"] = new(33, ["inventory", "currency.ledger", "equipment.stats"]),
            ["combat.runtime"] = new(40, ["combat.pk", "monsters"]),
            ["ninja.native"] = new(40, ["combat.runtime", "skills"]),
            ["guards.system"] = new(41, ["combat.runtime", "world.catalog.complete"]),
            ["guards.player.rendering"] = new(41, ["guards.system"]),
            ["guards.wanted.heat"] = new(41, ["guards.system"]),
            ["guards.blue.warning"] = new(41, ["guards.wanted.heat"]),
            ["guards.red.pursuit"] = new(41, ["guards.system"]),
            ["guards.black.announcements"] = new(41, ["guards.red.pursuit"]),
            ["guards.honor.earning"] = new(41, ["guards.black.announcements"]),
            ["guards.honor.redemption"] = new(41, ["guards.commander"]),
            ["guards.commander"] = new(41, ["guards.player.rendering"]),
            ["guards.arena"] = new(41, ["guards.system"]),
            ["guards.twin.city"] = new(41, ["guards.system"]),
            ["guards.ai"] = new(41, ["guards.player.rendering"]),
            ["guards.emergency.disable"] = new(41, []),
        };

    public static IReadOnlyCollection<string> KnownKeys => Definitions.Keys.ToArray();

    public static GameplayGateValidation Validate(IEnumerable<GameplayGateEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        GameplayGateEvidence[] supplied = evidence.ToArray();
        var failures = new List<string>();
        if (supplied.GroupBy(static gate => gate.Key, StringComparer.Ordinal).Any(static group => group.Count() != 1))
            failures.Add("duplicate-gate-evidence");
        var byKey = supplied.GroupBy(static gate => gate.Key, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        foreach (string unknown in byKey.Keys.Where(key => !Definitions.ContainsKey(key)).Order(StringComparer.Ordinal))
            failures.Add($"unknown-gate:{unknown}");

        foreach ((string key, GateDefinition definition) in Definitions)
        {
            if (!byKey.TryGetValue(key, out GameplayGateEvidence? gate) || gate.State == GameplayGateState.Disabled)
                continue;
            if (gate.AppliedMigration < definition.RequiredMigration) failures.Add($"migration-missing:{key}");
            if (!gate.CatalogReleaseActive) failures.Add($"catalog-inactive:{key}");
            if (!gate.ReferenceClosureValid) failures.Add($"reference-closure-invalid:{key}");
            if (!gate.ExactCodecAvailable) failures.Add($"codec-unavailable:{key}");
            if (gate.State != GameplayGateState.Enabled) continue;
            foreach (string dependency in definition.Dependencies)
                if (!byKey.TryGetValue(dependency, out GameplayGateEvidence? dependencyGate) ||
                    dependencyGate.State != GameplayGateState.Enabled)
                    failures.Add($"dependency-disabled:{key}:{dependency}");
        }
        return new(failures.Count == 0, failures.AsReadOnly());
    }

    private sealed record GateDefinition(int RequiredMigration, IReadOnlyList<string> Dependencies);
}
