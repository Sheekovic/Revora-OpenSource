# Release Souls inspection handoff

## Correction and evidence (2026-09-28)

The timer removed an expired inspection from the pending dictionary before
acquiring the character's item lock. A second remains dialogue could then start
a new inspection while the old callback waited. The old callback subsequently
cleared `OnCheckGuard`, leaving the new inspection pending but cancelled.

`ReleaseSoulsQuest.Tick` now removes the exact dictionary entry while holding
the same lock used by inspection admission. It rereads the clock before deciding
whether the prayer is complete and belongs to the current Pacific day. A stale
snapshot cannot remove a replacement entry. Ineligible sessions still retire
only their exact pending entry.

The actual-process fixture `tools/Verify-ArenaProcess.ps1 -SoulInspectionOnly`
uses an ordinary non-GM character and installed Desert Guardian/remains NPCs.
It accepts the quest, starts a prayer, holds the item lock, and waits until a
timer thread blocks on that lock. A second remains request is dispatched before
releasing the lock. The old game fails the pending/active consistency assertion;
the fixed game cancels the interrupted prayer without credit and permits a new
three-second prayer. Only that soldier is saved. The character then releases
the other two souls, claims the bound combat rune, and repeats the claim with
an unchanged persisted item fingerprint. The restart extension below adds
currency, complete receipt and achievement comparisons.

Evidence:

- Old gameplay SHA256 `C5791856C7459DF2F3AE5D96CE699A99AC554C853F8D28A47EA48E152155B0A2`:
  `obj/soul-inspection-before.log`; assertion32 failed in the first version of
  `CheckCandidateSoulInspection.cs`, after the blocked timer resumed.
- Fixed gameplay SHA256 `BC7CEA41EA4692DC93684297EE742C230773D6EDBA16E806AFE85AF75E7652D8`:
  `obj/soul-inspection-build.log`, `obj/soul-inspection-after.log` and the private
  `arena-process-soul-inspection.log` report the successful build and fixture.
- The successful driver removed its owned clone and stopped private PostgreSQL.

The fixture supplies character setup, NPC proximity, a controlled UTC clock and
the lock interleaving. It exercises production NPC dispatch and timer settlement,
not authenticated TCP navigation or native DX9. It does not prove every timing
race, process-restart recovery or real elapsed prayer animation. No reward terms,
live deployment or client files changed.

## Earned reward restart extension (2026-09-28)

The same driver now runs `soul-inspection-reload` after stopping/restarting the
private database and starting a fresh game process. Before shutdown it records
the earned item identities/types/binding/quantities/positions, complete daily
quest rows, CP/RVP/Study balances and achievement11012 evidence. The new process
compares those exact saved values, loads the ordinary character through the
production loader and checks the inventory and balances against durable state.

Normal login recovery, repeated reward collection, reacceptance and another
remains dialogue leave the completed daily contract unchanged. The character
retains exactly one bound combat rune and cannot start another same-day prayer.
The first phase's repeat-claim assertion now uses this expanded snapshot too.

Both phases pass in `obj/soul-inspection-restart.log` on the unchanged fixed
gameplay hash above. The driver cleaned its clone and stopped PostgreSQL. This
closes the specific graceful earned-reward restart check; it does not test an
in-flight crash, ambiguous commit, authenticated TCP or native-client display.
