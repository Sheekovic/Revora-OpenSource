# Administrator Arena balances and writer audit

2026-09-21. Existing `@arenapoints` and `@honorpoints` commands previously changed
RAM only; the first also changed only the solo mirror. Managed final saves omit
those economic columns, so the changes could disappear or conflict with results.

The legacy administrator dispatcher now calls a separate `ArenaAdminBalances`
service. Both dispatcher and service require `Administrador`; ordinary players
and the lesser GameMaster role cannot invoke it. `@arenapoints` sets shared points;
`@honorpoints` adds current Honor without changing historical Honor, preserving
the previous command meaning. Arguments use bounded unsigned parsing.

The service requires the original loaded session, matching statistic owners and
balances, and no match, spectator role, queue, trade, shutdown or pending stored
result. Under the actor/entity locks it checks saved preimages, changes both rows
and records operation, character UID, amount, before/after balances and UTC time
in one transaction. The audit has no credentials or player message text. Explicit
concurrent administrator requests each have their own audited mutation; this is
not a client request replay protocol. Overflow is rejected. Database/projection
failure invalidates Arena caches and fences the session for recovery. Existing
malformed historical mirrors are rejected rather than guessed or overwritten.

Build and 1,131 combined Arena checks pass, including service-level authorization,
managed result owners, both production readers, concurrent requests, overflow,
queue/active-match denial, stored pending outcomes with a missing RAM reference,
mirror/audit suppression rollback and production cache recovery. The actual
native command UI was not exercised. Logs:
`obj/chi-refill-validation/arena-admin-balances-build.log` and
`obj/chi-refill-validation/arena-admin-balances-tests.log`.
The build includes the owner's separate `System/IniFile.cs` change. No deployment.

## Bounded compiled writer audit

| Writer family | Integration disposition |
| --- | --- |
| Solo/team match results and ordinary Win/quit callbacks | Prepared matching attaches result owners; owned callbacks use coordinators. Prepared unowned invitations cannot reach legacy payouts. |
| Daily/custom/ranking rewards, point and Honor purchases | Transaction services own saved balance changes and cache projection. Ranking/history policies remain explicitly Revora-specific. |
| Initial level-70 progression | Both point mirrors participate in the profile/skill transaction; validated separately by 495 Experience checks. |
| Login/load, daily reset and ordinary/final saves | Login restores canonical statistics. Managed balance fields are protected from legacy reset/save; metadata remains saveable. |
| Administrator adjustments | The two direct command writes are replaced by the service above. |
| Legacy ElitePK Honor writer | Outside ordinary enrollment: Create installs an empty legacy tournament array and delegates to ElitePkEvent. |
| TeamQualifier1 and ElitePK original copies | Excluded from the project compilation list. |

This source audit covers the identified compiled writers, not arbitrary external
SQL tools, proprietary modules or a full live lifecycle. Startup activation,
actual process restart with fighters and native acceptance remain separate gates
in [the cutover checklist](ARENA_CUTOVER.md).
