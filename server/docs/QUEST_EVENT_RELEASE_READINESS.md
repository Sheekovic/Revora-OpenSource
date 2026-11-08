# Quest/event release readiness

Source audit: 2026-09-21, baseline `88a9e9a`. This is a bounded release-triage
audit, not certification of every route or a completion percentage.

## Current position

Recent work is in Git. The latest Arena gameplay regression run passed 1,131 checks; those
checks cover Arena behavior and prerequisites only. The roadmap's release table
still marks native acceptance pending and these batches not deployed. No live
restart, installed-client edit or production schema change was performed here.

The owner reported 26% weekly allowance remaining. Reuse the existing evidence;
do not run another full suite merely to increase a check count. Consolidate the
next implementation and validation around a release candidate.

## Source findings

Latest private candidate check (2026-09-21): gameplay baseline `5da095a` includes
Wave Defense persistence, companion combat and autonomous monster pressure. Three
actual private game starts/normal closes across two database cycles verify Wave
synthetic reservation recovery without fabricated prizes. This supersedes the
candidate binary below, not its scoped Relic evidence. Full ordinary-player and
native acceptance remain open. The owner `System/IniFile.cs` change is recorded
in the manifest; no clean-source release is claimed. See the
[game hash and recovery scope](WAVE_DEFENSE_REWARDS.md#composed-startup-reservation-recovery).

Follow-up candidate check (2026-09-21): source baseline `344ea0e`, including the
recent LMS reward and Relic Rush persistence changes, passes three actual private
game startup/normal-close checks across two database cycles. Synthetic Relic
reservations recover without a fabricated winner. This does not close full
ordinary-player or native acceptance. The private manifest records the existing
owner `System/IniFile.cs` change; no clean-source release is claimed. See the
[exact scope and game hash](RELIC_RUSH_REWARDS.md#composed-startup-reservation-recovery).

| Route | Current source evidence | Release implication |
| --- | --- | --- |
| Moon Box | `Thread.cs` initializes/places it and schedules `Tick`; `Npcs.cs` dispatches it; `Entity.cs` handles deaths and travel. New runs set `valuable_reward=true`; successful endings call `QuestSoulReward.Create`, which creates a bound P7 soul. | Best first acceptance candidate among the routes inspected. Requires actual ordinary-player journey, client presentation and reconnect/claim acceptance. Existing saved contracts intentionally retain their old reward version. |
| Demon Exterminators | Startup, NPC and monster-death hooks exist. New contracts now save a bound level-1 yellow rune alongside five item 722136 rewards and achievement 11001 (`40b46db`). | The original audit's reward gap is resolved in source. Old contracts retain their original reward; ordinary-player native acceptance and deployment remain pending. |
| Trojan Epic | Startup calls `TrojanEpicQuest.Initialize`; its initializer validates terrain/catalog and connects store, entrance, training and journal. Timer hook exists. | Source integration is present. Full restart/native room navigation, equipment/skills and composed client-journal acceptance remain unchecked. |
| Solo/Team Arena | `Server.Run` now calls joint preparation after catalog loading and before workers/caches/listeners. Two actual isolated game start/normal-close cycles pass across database restarts. | Owned routes are connected in candidate source. Full HEAD includes this Arena cutover; do not describe another quest release as isolated from it. Fighter restart and native acceptance remain open. |

Sources: [roadmap/release table](QUEST_EVENT_ROADMAP.md#release-tracking),
[Moon Box](MOON_BOX.md), [Demon Exterminators](DEMON_EXTERMINATORS.md),
[Trojan native gates](TROJAN_EPIC_CLIENT_REVIEW.md),
[Arena startup](ARENA_STARTUP_PREPARATION.md),
[reward inventory](QUEST_REWARD_COVERAGE.md).

## Shortest next release sequence

Follow-up 2026-09-21: the Demon Exterminators gap in the original audit table is
now addressed in source by a saved bound level-1 yellow rune on new contracts.
Old contracts retain their original reward. See [reward update](DEMON_EXTERMINATORS.md).
Its native acceptance and deployment remain pending; step 5's implementation
work is now staged rather than an unimplemented reward design.

The [Moon Box acceptance record](MOON_BOX_ACCEPTANCE.md) now identifies the
hash-verified approved DX9 executable, the observed staged server binary, the
working-directory configuration dependency and the remaining setup/player
checks. Inspection did not launch either executable or establish an isolated
native client connection. All native checks remain pending.

1. Use Moon Box as the first acceptance candidate, retaining the full roadmap as
   the objective. Inspect the actual candidate/deployment mechanism before
   selecting binaries. Full HEAD initializes many other systems and cannot be
   advertised as a Moon-Box-only release.
2. Reuse the recorded Moon Box transaction/replay/reload evidence. Rebuild and run
   relevant checks once if the selected candidate changes; do not rerun Arena
   for a documentation-only or unrelated quest change.
3. In an isolated candidate with genuine 6711, use an ordinary eligible account:
   enter through Fortune Teller, earn the six tokens, obtain the jade, reach the
   correct spirit, finish, inspect the bound P7 reward and reconnect. Confirm bag
   capacity failure retains the claim and replay grants nothing extra. Record
   the actual candidate/client identities and observations.
4. Complete candidate restart and composed-client acceptance, then use the
   applicable signed publication/deployment and rollback process. Existing
   source checkmarks do not satisfy these gates. No production rollout is
   authorized or performed by this audit.
5. Include the completed daily reward upgrades in coherent release review;
   direct daily P7 remains prohibited. Continue remaining quests/events while
   keeping source implementation, native acceptance and deployment distinct.

## Remaining scope is substantial

Arena has tested Breaking Touch/shared flame ownership and operator draining,
and joint startup is connected and smoke-tested; full lifecycle/native acceptance
with ordinary fighters remains open.
See the [current cutover checklist](ARENA_CUTOVER.md) for concrete remaining work. Cross-server M9, campaign/historical evidence
and multiple native reward presentations remain unchecked. These are separate
from the first local release candidate; finishing Moon Box does not finish the
goal. Historical batch test counts and checkmarks must remain scoped to the work
they actually proved.

This audit reused source and existing results. No build or test suite was run.
