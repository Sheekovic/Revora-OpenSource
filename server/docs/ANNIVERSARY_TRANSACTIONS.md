# Anniversary reward isolation

Current follow-up: the owner selected a recurring weekend event. The candidate
now has a public envoy and Pacific Saturday/Sunday admission, with the full
server earning path validated ([current scope](ANNIVERSARY_WEEKENDS.md)).
Historical statements below about absent source admission are superseded;
live/native acceptance remains open. Newly granted P7 souls are now bound.

Implemented against `82dc201` on 2026-09-16. This repairs a **legacy custom
Revora service**, not an evidence-backed recreation of an official Conquer
Anniversary campaign. No deployment, public NPC placement or client patch is
included. A complete new-player earning route is still pending.

## Catalog identity correction candidate (2026-09-21)

Current read-only inspection again finds no public NPC 900028. The previously
reviewed `InspectAnniversary.exe` still matches its recorded SHA-256 and returned
no rows successfully. Fresh event admission remains unavailable. The owner has
been asked whether to use a recurring weekend or seasonal calendar; that decision
is pending. No admission/calendar behavior is changed by this batch.

Both the loaded server catalog and approved client's item 3001172 identify the
custom bead as `VirtuePack(A)` with a Father?s Day/Thales description. The new
`tools/ClientPatches/prepare_anniversary_catalog.py` prepares matching private
server/client text: `AnniversaryBead`, category `RevoraEvent`, and the current
15,000-spirit eligibility/reward contract. Newly issued event beads describe their
**bound** P7 soul. Legacy reward receipts and existing items are not rewritten.

The tool validates the loaded catalog path, exact server/installed-client/composed
item hashes and approved DX9 executable hash. It changes only fields 1, 52 and 53
of that single record: name, category and description. All prices, icon/mesh data,
item flags, unrelated rows and line endings are retained. The client source is
the combined Dis City v2 candidate, preserving its Magnolia/Summer/Labyrinth edits.
The journal from that candidate is unchanged. This fixes display identity only;
it does not restore the original Father?s Day quest or certify native rendering.

Private output: `obj/chi-refill-validation/anniversary-bead-catalog`.

| Artifact | SHA-256 |
| --- | --- |
| Loaded server input | `1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef` |
| Composed client input | `799b083e2effd8c80d99f07f22bd70091d3ce29319a3770d1258b70987434164` |
| Staged server output | `9cb94ddc59c9fa557ecc69e623f78ad2c770fb355cc4d2551f98cab6212f9816` |
| Staged client output | `2b633bc5a137be8067d57e41fa3ffd07a73f64765b68bf736fd92b7cd933a465` |

Three synthetic tests pass: exact field/unrelated-byte preservation, malformed or
ambiguous catalog rejection, and encrypted-client/plain-server parity. A separate
actual-file audit confirms exactly one record/three fields change in each output;
all four input hashes remain unchanged after staging. Output uses a temporary
directory and refuses replacement of an existing candidate. Generated catalogs
remain ignored. No gameplay code changed, so no server rebuild was required.
Future installation must validate and back up both live catalog preimages and
provide transactional rollback. This candidate is unsigned, uninstalled and
unpublished; NPC placement, event calendar and genuine-client acceptance remain
open. Historical unbound-P7 wording below predates the owner's binding correction.

## Current availability and evidence

The installed public NPC table has **no row for 900028**. The former handler
advertised level 140, two rebirths and VIP 6+, but its direct Start continuation
ignored those conditions. Hidden choices 9 and 31 also exposed old Soul Ball
issuance/payment code. The item-use handler independently paid the prize in
several writes and used `Inventory.Count != 21` as its capacity test.

The installed server catalog identifies item 3001172 as `VirtuePack(A)`, despite
the server's custom Anniversary-bead treatment. Item 3006048 is
`HappyAnniversaryCard`. This does not establish native client display or an
original event calendar. Server catalog inspected:
`bin/Debug/database/itemtype-new.txt`, SHA-256
`1ED7A23B7A34B5E3706277F97E421E3CDF0B269EEB5BE7367A4EBC0E4F22B3EF`.

Read-only NPC inspection used the local Revora-owned x64 helper
`obj/chi-refill-validation/bin/InspectAnniversary.exe`, SHA-256
`211CB758520D80B48AA3438F4363FB1885F7127E6D2D71063922CC9D74E049A1`.
It queried only public NPC metadata in a read-only transaction. Protected
connection settings and proprietary catalogs remain outside Git.

Eligible ordinary players who already own a legacy bead have a protected
progress/redemption path in this build. Fresh issuance and lost-bead replacement
require a real nearby NPC 900028; none is created by this change. Their tests use
an explicitly synthetic NPC fixture. This is not a claim that players can find
an Anniversary NPC in the current world, or that the historical event should be
opened permanently. Admission placement/calendar, intended bead presentation,
native acceptance and any release remain separate unresolved work.

## Preserved rules and repaired behavior

The handler checks level 140+, exactly two rebirths and VIP 6+ on admission,
hunting and claim, including the durable owner row. No GM or owner allowlist is
used. All former hidden paid continuations are removed. NPC frames use existing
bounded 2032 decoding; no new wire format is inferred.

A new admission starts with the existing three spirits. The goal is 15,000.
Registered ordinary wilderness monsters give one spirit through level 70, two
at 71-99, three at 100-119 and four from 120. The legacy level-70 boundary is
retained; extending the last bracket past 140 avoids a silent zero at higher
levels. Registered public hunting maps use the existing Soul Ball source guard.
Guards, revivers, bosses, adventurers, companions, private instances and forged
or unregistered entities do not count. Each actual monster life credits once;
changing its death timestamp cannot replay it. Generic boss bonuses are not
invented. Special-encounter spirits belong to their separately verified paths.

The full legacy Anniversary bundle is preserved:

- 50,000 RVPs and 50,000,000 silver;
- twenty item-3006048 Anniversary Cards;
- 100 Study Points and 172,800 seconds of blessing.

No CP or EXP prize is added. Cards inherit the bead's binding. Twenty individual
owned cards need twenty bag slots after consuming the bead: a 21-item bag fits
exactly, while a 22-item bag keeps the bead and receives no partial prize.
Overflow, stale balances and invalid inventory ownership also retain the reward.

## Durable ownership and shared daily allowance

`revora_anniversary_runs` records the owner, acceptance/completion days, exact
bead UID/binding, capped spirits, replacement history and complete reward UID
receipt. One active commission is permitted per character. An unfinished bead
can cross midnight; claiming it consumes the completion day's allowance. Clock
rollback and the old daily-reset flag cannot reopen a committed reward.

Soul Ball and Anniversary retain their shared daily allowance using durable
records. An active commission or completed day blocks the other service, even
if the bead is stored or lost. Concurrent paid Soul Ball completion cannot charge
around an Anniversary claim. The shared menu recognizes Anniversary completion
even when older Soul Ball history exists. It does not offer a paid finish during
an active Anniversary commission.

A sole owned, usable legacy bead may import its persisted counter once. Mixed
beads do not assign an ambiguous shared counter to either quest. A same-day
legacy completion imports closed, without another payout. Lost-bead replacement
requires proof that the original UID no longer exists anywhere; it retains
progress and recorded binding. This replacement remains dependent on the absent
admission NPC in the current installation.

The owner row and inventory lock serialize all mutations. The bead deletion,
RVP/Study Points, silver/blessing balances, all twenty cards and final receipt
commit in one PostgreSQL transaction. Cache and client updates follow commit.
Suppressed required writes roll back the entire prize and fence the session for
production reload. Monster progress also commits before its legacy display is
updated. Neither legacy `SpiritBeads` nor the shared RAM counter is reward
authority after import.

## Validation

The isolated x86 build passes. The final Anniversary suite passes **486 checks**
including shared progression prerequisites, synthetic ordinary-player admission,
legacy redemption, exact/full bag boundaries, all eligibility guards, concurrent
replays, shared-calendar claims, malformed dialogue, ownership/binding changes,
late twentieth-card failure, wallet/blessing failures, reload and retry.

The full numerical hunt fixture begins with a fresh three-spirit bead and credits
3,750 registered level-120+ monster deaths, including the production death
dispatch, then reloads and redeems all prizes. Its admission is synthetic and is
not evidence of a deployed new-player route. Production reload also verifies
currency, cards, blessing, Study Points and completion independently.

Final affected Soul Ball suite: **559 checks**. Before the final shared-menu
correction, Exorcism passed **557** and quest packets **9**. Total: **1,611 gameplay
checks**, with shared prerequisites repeated. Three producer-audit tests pass;
the source inventory remains 219 present and 118 absent. No achievement producer
or wardrobe earning route is added by this repair.

Final harness SHA-256:
`8EF36CBF0177983C539E462303978D39189F072008677AA4734BE9F281108A95`.
Ignored logs: `anniversary-final.log`, `anniv-regression-soul-final.log`,
`anniv-regression-exorcism.log`, `anniv-regression-packets.log`, under
`obj/chi-refill-validation/`.

## Valuable completion reward (2026-09-20)

New Anniversary beads freeze `valuable_reward=true` and add one unbound P7 soul
to the twenty cards, 50,000 RVPs, 50 million silver, 100 Study Points and two days
of blessing. Existing/imported runs remain false and preserve their original
rewards. Replacement beads keep the saved contract. The extra soul is selected
from the loaded P7 catalog and saved in `reward_uids` with all other completion
writes. Cards continue to inherit bead binding; the added soul is unbound.
The shared Soul Ball daily exclusion remains in effect. Capacity now requires
room for 21 gifts after consuming a new-contract bead. Native/live acceptance
remains pending.

The targeted rollback fixture uses a saved full spirit counter. The existing
full route also fills a freshly issued bead through 3,750 registered wilderness
deaths, then reloads and claims the upgraded prize. This is server integration
evidence; no native-client journey is claimed. Reward checks cover
full bags, suppressed soul insertion, rollback of cards/currencies/completion,
concurrent use and production reload. Log:
`obj/chi-refill-validation/anniversary-p7.log` (ignored).

Isolated build and 521 Anniversary checks pass for this upgrade.
