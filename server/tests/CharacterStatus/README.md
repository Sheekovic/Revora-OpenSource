# Character status regression checks

Build Revora.csproj into `obj/chi-refill-validation/bin`, then run:

```powershell
.\tests\CharacterStatus\Verify-CharacterStatus.ps1
```

The harness checks the actual server's CharacterInfo serializer against the
confirmed modern Revora 6711 receiver layout. Its 29 checks cover HP/mana
boundaries, adjacent fields, currency widths, stable positive display identity,
string preservation, and agreement with subsequent HP/mana updates.

It reuses the offline CompanionTests world/player fixture, with no GM or account
allowlist. CP values are initialized through the backing field because the normal
setter persists immediately. No database, real client, listeners, or server
timers are used. A missing optional GeoIP.dat notice is expected in this output.
These checks do not establish live client rendering or durable player saves.

Protocol evidence and confidence are recorded in `docs/PROTOCOL_RESEARCH.md`.

## Wall of Prestige

Run `Verify-Prestige.ps1` against the same isolated build for 55 checks covering
shared category totals, real rebirth counts, currency independence, stable ties,
top-100 and ten-row pagination, all ten class categories, wall winners, central
appearance, offline inspection responses, bounded requests, query rate limits,
ordinary-player eligibility and concurrent snapshot reads. Ability table data is
read from `bin/Debug/ability_score.txt`; these fixtures do not connect to a database.

An additional ignored `obj/chi-refill-validation/PrestigeSavedDataCheck.cs` harness
loads the local saved data without running server/login routines. The production
offline loader enforces a read-only, repeatable-read transaction. Its two saved
characters produced identical totals/order on reload. This verifies reconstruction
from persistence, not a new authenticated client save or visual rendering test.
