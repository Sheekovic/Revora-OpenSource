# Local Elite PK: Revora v1

M3 code and automated validation, 2026-09-15 UTC. This restores an ordinary-player
earning route for the highest-bracket champion and Infernal Beast wings. Live
6711 validation and deployment remain pending. No GM status or account allowlist
is required.

## Entry and rules

The Elite PK Envoy (448582) is restored near Twin City **438,270**, using the
installed NPC mesh and the nearest free walkable tile within eight tiles. The
previous plaza cleanup parked this NPC on map 6000 and blocked its dialogue.
Startup now places it in town without changing the stored NPC record.

Registration opens each Friday at **19:55 America/Los_Angeles**, with daylight
saving handled by Windows' `Pacific Standard Time` zone. Admission closes when
the entrant's bracket starts. Each bracket accepts at most 32 characters; one
character can enter only one bracket per dated event.

| Level at registration | Start | Latest finish |
| --- | --- | --- |
| 130+ | 20:00 | 20:30 |
| 120–129 | 20:02 | 20:30 |
| 100–119 | 20:04 | 20:30 |
| 1–99 | 20:06 | 20:30 |

These are the legacy level groups already compiled in this server and the times
in the [official level-bracket table](https://co.99.com/guide/quests/elite_pk.shtml).
That page also contains newer rebirth rules; Revora v1 retains the level edition.
The [older prize guide](https://co.99.com/guide/prize_upgrade/elite_pk.shtml)
corroborates the level table. This is a scoped Revora edition, not a claim that
every historical prize or newer official rule has been reproduced.

Entrants wait on an isolated copy of map 2068. Leaving or disconnecting forfeits
participation. Persisted random seeds fix the initial order. Pairings give enough
first-round byes to form a power-of-two bracket, without omitting an entrant.
Each pair fights in its own copy of map 700; up to 64 arenas are cached and reused.

Rounds last three minutes above the top eight and five minutes thereafter,
including ten seconds of preparation with combat blocked. A defeat wins the
duel. At timeout, accepted damage to the opponent determines the winner, capped
by HP actually available before each hit. Reflection counts for its source.
Equal positive scores use signup battle power, level, then the stored random
seed. Two players with no damage are both eliminated. No-shows advance a present
opponent; two absent players advance nobody. These attendance, cap and final
tie rules are Revora decisions. The [official scoring guide](https://co.99.com/guide/event/elite_pk_tournament.shtml)
supports damage scoring and BP/level tie criteria, but describes a newer edition.

The Envoy also admits up to 20 spectators per duel. Spectators cannot attack,
cast spells or receive fighter damage. Combat is restricted to the assigned
pair, including checks in the damage-receive path. Event deaths do not drop
equipment or create normal PK crime. The Elite PK Clerk provides an exit;
eliminated players and spectators return to town, revived, with their original
PK mode restored. Dialogue and normal chat provide instructions and results.

## Champion honors

The final winner must have at least one won, contested match with positive
damage and the run must contain at least two registered characters. A lone
entrant or a winner who advanced entirely through byes/no-shows cannot create a
champion award. This prevents an empty tournament from granting wings; it does
not establish that opponents belong to different real-world people.

The **130+ champion** receives wing key **60010020** and champion status **16**.
Both expire exactly seven days after the committed result. This follows the
local Elite PK earning requirement for Infernal Beast in the
[official wardrobe guide](https://co.99.com/guide/quest/2016/wardrobe.shtml).
The seven-day duration is the existing server reward contract. Lower brackets
save their contested champion result but do not grant the highest-bracket wing
or status. Cross-server Elite PK rewards remain separate.

The award is automatic. Repeated NPC requests, duplicate finalization and login
never renew it. Wardrobe ownership and champion status are reconciled from the
same durable award; expiry also clears an equipped wing or selected champion
status. Legacy champion flags alone are no longer accepted as wing evidence.
Original legacy status records are preserved, not promoted into new results.

This milestone does not award Top-8 placement prizes, CPs, gold, EXP, honor or
items. Native ranking, bracket and wagering requests remain globally disabled;
the old claim and tournament timers cannot grant competing rewards. No Elite PK
achievement ID was confirmed in the installed catalog, so none is invented.
See [protocol evidence and remaining client gates](PROTOCOL_RESEARCH.md).

## Persistence and recovery

Additive tables `revora_epk_runs`, `revora_epk_entries` and `revora_epk_matches`
save dated runs, frozen entrants, pairings, damage totals and outcomes. A single
event lock owns gameplay transitions. Run-row locks serialize database changes;
saved pairings/results reject changed replays. Champion finalization and the M1
award receipt commit in the same transaction. Notification follows commit.

Failed writes retain retryable state with settled combat frozen. A partially
completed server transfer can retry without awarding a false no-show. Startup
cancels unfinished runs and never reopens those dated slots; it preserves
completed results and awards. Existing login handling returns characters saved
in these dynamic maps to Twin City. Restart recovery cancels interrupted play;
it does not resume a partially played bracket.

## Validation and rollout

The isolated build passed **78 Elite PK, 48 CTF, 63 progression and 91 wardrobe
checks (280 total)**. Fixtures use actual installed map floors and the Envoy
record, ordinary non-GM synthetic characters, the NPC dispatcher, combat receive
and death handlers. Database writes belong to the disposable
`revora_potency_validation` schema, removed after each run. No game listeners or
live server entry point are started.

Coverage includes every population from 2–32, a full five-player highest bracket,
byes, warmup, spectators, a large-hit scoring regression, partial transfer retry,
match and championship transaction rollback, concurrent replay, lower-bracket
timeout, no-show, idle elimination, equipment/reload, shared expiry, disabled
native requests and cancellation of an actual in-progress fixture duel.

After the isolated build, run `tests/PotencyHunt/Verify-PotencyHunt.ps1` with
`-ElitePkOnly`, `-CtfOnly`, `-ProgressionOnly` and `-TitlesOnly` separately and
sequentially. They share a disposable schema. Logs and the staged executable
remain ignored under `obj/chi-refill-validation/`.

Validated staged executable SHA-256:
`225c9da214293da47847ee1adcd51be8f950283e5f42ac47b95b3db0d44b8f83`.

No runtime binary, live database migration or client patch was deployed. Before
activation, preserve the existing binary and database backup and review the M1
legacy-progress migration. Validate a fresh ordinary-player tournament on genuine
6711 clients: Envoy access, map entry, combat, watching, return, champion status,
immediate wardrobe ownership, equipment and nearby-player wing appearance.
Native title packet 1130 is reused; its restored champion display still needs
that client test. Do not mark the live milestone complete from fixture results.

Rollback restores the prior binary with preserved evidence. Do not delete award
receipts or overwrite later player progress to revert tournament scheduling.

2026-09-16 follow-up: [owned prize pack consumption](ELITE_PRIZE_PACK_TRANSACTIONS.md)
is atomic. The current tournament grants honors but does not issue those packs;
placement-prize claims and their complete earning journeys remain M6.56c.

2026-09-16 placement follow-up: [owned bronze bouts and verified entitlements](ELITE_PLACEMENT_ENTITLEMENTS.md) now persist earned top-eight results with
the champion transaction. Native acceptance and the Envoy's actual prize payout
remain open. The expanded isolated tournament suite passes 124 checks.

2026-09-16 claim follow-up: [Earned Envoy claims](ELITE_PRIZE_CLAIMS.md) now connect the verified placements to complete transactional prizes and a battle-to-nested-item-to-refinery journey. This closes the server earning-path gap described above; live deployment and native acceptance remain open.
