using Revora.Gameplay.Features;

var disabled = GameplayFeatureGates.KnownKeys.Select(key =>
    new GameplayGateEvidence(key, GameplayGateState.Disabled, 0, false, false, false)).ToArray();
Assert(GameplayFeatureGates.Validate(disabled).Valid, "Disabled future gates must preserve the proven baseline.");

GameplayGateEvidence[] itemsWithoutWorld = disabled.Select(gate => gate.Key switch
{
    "items.definitions" => gate with
    {
        State = GameplayGateState.Enabled, AppliedMigration = 13, CatalogReleaseActive = true,
        ReferenceClosureValid = true, ExactCodecAvailable = true
    },
    _ => gate
}).ToArray();
GameplayGateValidation dependencyFailure = GameplayFeatureGates.Validate(itemsWithoutWorld);
Assert(!dependencyFailure.Valid && dependencyFailure.Failures.Contains(
    "dependency-disabled:items.definitions:world.catalog.complete"), "An enabled gate bypassed its dependency.");

GameplayGateEvidence[] worldAndItems = itemsWithoutWorld.Select(gate => gate.Key == "world.catalog.complete"
    ? gate with
    {
        State = GameplayGateState.Enabled, AppliedMigration = 12, CatalogReleaseActive = true,
        ReferenceClosureValid = true, ExactCodecAvailable = true
    }
    : gate).ToArray();
Assert(GameplayFeatureGates.Validate(worldAndItems).Valid, "A fully evidenced dependency chain was rejected.");

GameplayGateEvidence[] missingCodec = worldAndItems.Select(gate => gate.Key == "items.definitions"
    ? gate with { ExactCodecAvailable = false }
    : gate).ToArray();
Assert(GameplayFeatureGates.Validate(missingCodec).Failures.Contains("codec-unavailable:items.definitions"),
    "An enabled gate without an exact codec did not fail closed.");

GameplayGateValidation unknown = GameplayFeatureGates.Validate(disabled.Append(
    new GameplayGateEvidence("plugin.arbitrary", GameplayGateState.Enabled, 99, true, true, true)));
Assert(unknown.Failures.Contains("unknown-gate:plugin.arbitrary"), "An arbitrary database feature key was accepted.");

Console.WriteLine("Revora.FeatureGateTests: 5 passed");

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
