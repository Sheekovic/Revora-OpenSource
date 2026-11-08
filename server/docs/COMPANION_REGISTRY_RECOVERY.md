# Companion registry startup repair

The private Virtue reload encountered `Dictionary.Insert` / `Map.AddEntity` /
`CityGuardSystem.Materialize` during startup. Inspection found that the
high-UID actor branch used `SafeDictionary`, which inherits an ordinary
`Dictionary` and provides no concurrency protection. Guard and other world
actor registration can share that collection with readers and writers.
This identifies an unsafe path consistent with the exception; the original
interleaving was not captured and is not claimed to be deterministically replayed.

`Map.Companions` now uses the existing `SafeConcurrentDictionary` type in all
three map initialization paths. Registration uses atomic `TryAdd`, so concurrent
UID contention retains one owner. Add/remove floor updates share a writer lock.
Removal matches both UID and entity reference, preventing a late cleanup from
removing a replacement actor. Concurrent readers can enumerate the registry.
This does not make every map collection or all map/floor operations thread-safe.
The separate legacy `SecoundEntities` registry remains outside this scoped fix.

Validation on 2026-10-01:

- Isolated x86 server build passed using the committed-source overlay for unrelated
  owner work in progress.
- In-memory regression: 2,048 parallel actor additions and removals with concurrent
  enumeration; 64 contenders for one UID; stale loser cleanup and replaced-owner
  cleanup. All pass without database access or listeners.
- Existing 37 guard checks pass, including real pursuit, death/respawn behavior,
  defensive stats and combat ownership. The fixture reports missing optional
  GeoIP.dat but completes successfully.
- The updated private candidate starts, restores the saved Exorcism career,
  verifies exact reward state and replay refusal, then shuts down successfully.
  SHA-256: `F5683ADB48B1D7015F0B998AA48F569431955CAA298FD506FE0BA78F35F93B96`.

Run the isolated registry regression with
`powershell -NoProfile -File tests/CompanionAI/Verify-CompanionRegistry.ps1`.
The Team PK and Quarantine in-memory map constructors were updated for the field
type; their broader suites were not rerun. No live restart, deployment or client
release occurred. Long-running production stress and unrelated startup races
remain outside this evidence.
