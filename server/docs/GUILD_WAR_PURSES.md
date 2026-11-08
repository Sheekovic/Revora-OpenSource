# Recoverable regular Guild War purses (M7.2e)

The regular Sunday Guild War records each eligible player's purse in the same
transaction as the battle result. Delivery runs separately: online players are
polled automatically, and GuildDirector in Twin City offers **Collect my war
purse**. Full bags, trading and currency limits defer delivery without losing
any part of the reward. Offline recipients can collect after reconnecting.

## Eligibility and preserved rewards

The canonical winning guild's ordinary authenticated members on the real map
1038 at finalization qualify, including fallen members. Fake players, stale
sessions, absent players, other guilds and unrecognized ranks do not qualify.
This preserves the existing support-member presence rule; individual damage is
not a new requirement. Eligibility is the finalization snapshot, not reconstructed
historical presence at an exact clock instant. Interrupted active battles remain
cancelled under M7.1b; no purses are invented for them.

Rank and configured amounts are frozen at that snapshot. Later promotions or
configuration changes cannot increase an earned purse. Leadership transfers
serialize against snapshot collection. Default configured rewards are:

| Rank | CPs | RVPs | Gold | Bound items | Six-day honor |
| --- | ---: | ---: | ---: | --- | --- |
| Leader | 8,000 | 100 | 2,000,000 | One pack + four Lord Tokens | Guild leader |
| Leader spouse | 8,000 | 50 | 2,000,000 | One pack + two Lord Tokens | Guild leader |
| Deputy / deputy spouse | 8,000 | 50 | 2,000,000 | One pack + two Lord Tokens | Deputy |
| Recognized member ranks | 2,000 | 20 | 250,000 | One pack | Deputy |

Pack 3302411 and loose token 723467 retain their existing one-day lifetime,
starting at delivery. These loose tokens **never** establish seasonal earning
proof and cannot replace the recorded weekly token for cup exchanges. Honor
expires six days after the battle deadline; collecting late does not renew it.
A newer guild honor is preserved. Native status keys 9/10 and existing flag
serializers are reused. Reconciliation projects the saved, unexpired honor.

## Persistence and recovery

`revora_guild_purses` is keyed by battle day and character UID, with the winning
guild, rank, amounts, token count, honor expiry, claim time and delivered item
UIDs. The battle's `purses_frozen` marker commits with all recipient rows and the
result. Entity rows lock in UID order before the battle row. A recovered capture
forces a fresh winner/recipient snapshot before finalization, even when the
previous runtime winner had no recipients.

Delivery takes the character inventory lock and saved entity row. Exact saved
CP/RVP/silver balances and bag count must agree with memory. All currencies,
items, honor and the claimed receipt commit together. Failed writes roll back
and fence an uncertain session for reload; normal capacity limits simply wait.
Duplicate NPC calls and automatic delivery share the same claim path. The poll
visits at most 32 owners per ten-second pass with a rotating cursor, claiming
one pending purse per owner. NPC collection likewise takes the oldest purse.

Migration seals pre-existing terminal battle rows without backfilling speculative
payouts. Existing completed purse rows remain intact across initialization.
The old regular-war payment loop is bypassed after managed finalization. The
separate Wednesday Super Guild War still uses its existing implementation and
is not certified by this migration.

## Validation and limits

Staged build SHA-256:
`E470F0FDBB3BD3B28CB88792F50FB55DD9A23A39B423FA2A278B8C2AE7373FB4`.

The focused guild-season suite passes **323 checks**, including 28 new purse
checks. Ordinary non-GM fixtures exercise the real capture/finalization and NPC
paths, rank payouts, dead/offline eligibility, exclusions, frozen configuration,
later promotion, full bags, CP/RVP overflow, stale RVP, concurrent claims,
late delivery, newer honor preservation, expiry and character/item reload.
Suppressed recipient/seal writes roll back finalization; suppressed entity,
item, status and claim writes roll back the entire delivery. Lost capture
projection recovery is tested with an initially empty recipient snapshot.

Related suites pass: titles 91, progression 74, weekly/monthly PK 60, CTF 48,
Elite PK 78 and Potency/daily bosses/Extraction 216. Total: **890 checks**.
All database fixtures use a disposable schema. These tests prove saved timed-item
metadata and pre-expiry reload, not a real 24-hour client session or process-kill
recovery. No production data was changed.

M7.2's server implementation is complete. M7's full-route acceptance remains
open for genuine 6711 invitation, combat, NPC/item/currency presentation and
honor/title/wing visuals. No live restart, deployment or client patch occurred.


## Valuable regular Guild War purse addition (2026-09-27)

Newly finalized regular Guild War purses include one random **bound permanent P7
soul** alongside the existing frozen currencies, pack, tokens and honor. This is
an event reward under the owner's valuable-reward policy; no daily quest changes.
Eligibility remains the same ordinary winning-guild finalization snapshot,
including qualifying support members. No new damage or privileged-account gate.

The `soul_type` column defaults to0 for existing purses. Finalization selects a
catalog-valid P7 type and saves it with each new entitlement and battle result.
Later claims use that frozen identity, so retry/reconnect cannot reroll it. Old
pending purses retain their original contents; paid purses are never reopened.

New complete bundles require6/4/2 slots for leader/officer/member respectively.
Full bags retain every component. Claim validates the saved soul against the P7
catalog and saves its bound item with the existing currencies, items, honor and
receipt in one transaction. A soul insert failure rolls back preceding legacy
items and currency updates. The soul does not inherit the legacy pack/token's
one-day expiry. All generated item UIDs remain in the existing rewards array.

Isolated build passed with existing legacy warnings and **991 guild-season checks
passed**. New assertions cover frozen catalog identities for all eligible ranks,
the extra inventory slot, soul-only insertion failure, exact permanent/bound
outputs, old pending terms and actual character/item loader recovery. Existing
concurrency, rank snapshot, offline delivery and duplicate-claim checks pass.
Logs: ignored `obj/guild-purse-soul-build.log` and
`obj/guild-purse-soul-tests.log`. Counts overlap previous guild suites.

This does not enable unresolved achievement10302. Its original occupation trigger
still lacks verified evidence; a separate owner question proposes a documented
Revora Guild War rule. Live deployment, actual Host migration/player recovery
and native acceptance remain pending. Super Guild War is unchanged.

Super Guild War follow-up: repeated runtime Start/End calls are now guarded;
its durable payout migration remains open. See `SUPER_GUILD_WAR_RECOVERY.md`.


## Earned pending purses across game processes (2026-09-27)

`tools/Verify-ArenaProcess.ps1 -GuildWarOnly` starts three private game processes
and stops/restarts PostgreSQL after forcibly exiting the first game. Two ordinary
non-GM profiles receive supplied guild affiliation and leader/member ranks. Guild
creation/admission is not claimed as earned by this fixture. Both enter the real
map 1038; the leader's controlled attack passes through `ReceiveAttack` against
the installed pole at full HP and earns one capture. A controlled Sunday clock
then reaches the existing battle deadline.

Normal finalization records a won battle, the matching keeper and two frozen,
unclaimed purses. No purse or result is inserted by the probe. Automatic purse
polling is held back only in the test so it can exit before delivery. After actual
database/game restart, both profiles use GuildDirector's existing collection
option twice. Each receives the frozen CP/RVP/silver amounts, one pack, the
rank-specific loose tokens and one bound catalog P7 soul. The regular member's
presence qualifies without inventing a personal-damage requirement.

A third game process loads both characters and retries collection. Exact saved
item UID/type fingerprints, purse claim timestamps and balances remain unchanged;
there is one purse achievement 10228 per owner. The battle stays won with the same
keeper and frozen recipients. Achievement 10302 is not granted: its proposed
Revora rule still awaits the owner's decision.

The initial probe exposed a missing warehouse collection in the offline player
fixture, causing the normal background arsenal callback to fail. The shared
fixture now constructs an empty warehouse collection before publishing the player;
no production arsenal or purse behavior was changed. The focused suite was rerun
with that setup correction. This test covers offline ordinary profiles, supplied
guild membership, controlled combat/time and real process persistence. It does
not establish authenticated client admission, native visuals or deployment.

Validation: all three phases passed on unchanged candidate SHA-256
`440D5CFD930640C83A457AE69D51BE17E2C2C15DB537A95EFCA080485C89F65E`.
The corrected phase logs contain no exception/error markers. Local redacted
summary: `obj/guild-process.log` (ignored). No production build or live restart.
