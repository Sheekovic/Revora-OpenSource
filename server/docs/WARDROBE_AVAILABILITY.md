# Current 6711 wardrobe earning audit

Current inventory reconciled 2026-10-01 against the 72-key
[reward matrix](research/WARDROBE_REWARD_MATRIX.csv), including privately verified
Kingdom earning, succession, revocation and full-server authenticated recovery.
Dated sections below retain historical scopes; they are not current counts.
See [current status](QUEST_EVENT_STATUS.md).

## What the counts mean

| Current state | Entries | Meaning |
| --- | ---: | --- |
| `code-validated` | 67 | A complete server earning route has ordinary-player automated evidence; native acceptance remains pending |
| `predicate-only` | 1 | The requirement/reconciliation exists; earning evidence or documented rule/runtime gates remain unresolved |
| `unimplemented` | 4 | No active production earning source for this reward; a definition or partial prerequisite is insufficient |
| Total | 72 | Every installed key appears exactly once |

`eligibility_expression=yes` totals 68; the four Poker rewards remain unimplemented and intentionally unavailable by owner choice.
The remaining predicate-only entry is Rising Star. The two Prestige titles and
three Perfection rewards now have code-validated rows under the approved Revora
rules. Rising Star still needs the complete compatible-career evidence review;
predicate-only does not mean its eligibility calculation is missing.
Overlord and Solar Dragon are now code-validated but staged and disabled by default.
No entry has completed genuine-client acceptance in this audit. Code validation
is not deployment or a claim that every route is enabled for live players.

The matrix adds `earning_route`, `server_reference`, `validation_reference` and
`live_acceptance`. References point to repository files, optionally qualified by
a symbol. Historical `evidence` labels remain linked to the original research;
new milestone/document references identify later work. `phase` retains the
original P0-P7 planning scheme, not the newer M0-M10 milestone numbering.

## Validated server earning routes

| Rewards | Count | Reviewed production route and evidence |
| --- | ---: | --- |
| Victor | 1 | Scheduled [local CTF](LOCAL_CTF.md), accepted objectives, committed champion guild and current guild leader reconciliation |
| Wings of Infernal Beast | 1 | [Local Elite PK](LOCAL_ELITE_PK.md), highest-bracket result, seven-day durable award and nonrenewing claims |
| Emerald / Orange / Flame / Bright Glow, Siren Song | 5 | [Summer hunt](SUMMER_WINGS.md), earned materials, exact owned crafting receipts, dye/unbind/trade and permanent activation |
| Chosen One | 1 | [Boss bounties to Vesper purchases and maximum runes](RUNE_ACQUISITION.md), actual owned-material composition, equip and uncached wardrobe reload |
| Invincible, Supreme | 2 | [Guild season to Grail](HOLY_GRAIL_CRAFTING.md), 36 actual fixture battles, 36 leader token claims, twelve earned trophies and one atomic craft |
| Wings of Planet | 1 | [Revora weekend expeditions](PLANET_WEEKEND.md), four earned hunts/owned encounters/cache deliveries, atomic 30-day award, ordinary wardrobe reload/equip and expiry |
| Mr./Ms. 2017 titles and Romance wings | 5 | [Magnolia-funded contest](MR_MS_2017_IMPLEMENTATION.md), free seed to earned upgrade CPs and orchid bonus; ordinary NPC donation; frozen category winners and durable wardrobe prizes |
| Wings of Heart | 1 | [Recurring Revora campaign](HEART_CAMPAIGN_REVORA.md), three NPC-admitted normal-combat sorties and cache deliveries; closed-weekend atomic prizes; wardrobe reload/equip and fixed expiry |

Event/crafting producers call the central award writer with a fixed source.
Victor uses champion-guild state committed with the CTF result; Chosen One uses
the current legal equipped loadout. `ProgressionEvidence`
also recognizes `team-pk-2018` as a persistence-fixture source. Origin-qualified
CS Team PK grants use `cs-tpk-revora-v1` ([scope](ORIGIN_TEAM_PK.md)).

## Remaining earning work

The five remaining collection conditions now have a bounded
[supplier audit](COLLECTION_SUPPLY_GAPS.md). It identifies an active EShopV2 lead
for Big Fan, an unproven legacy RVP shop for all six Euro designs, and missing
variant/exchange prerequisites for the other sets. Catalog presence alone does
not raise their completion status.

The [bounty-funded Vesper collection journey](COLLECTION_ACQUISITION.md) now proves
Fashionist, Swift Chaser, Monkey Rider, Solar Rider and Lunar Rider, plus all three
Fashion placements. Its 55 checks cover real purchases, wardrobe admission,
production reload, equip and Fashion expiry. This adds seven classifications:
five collection titles and Fashion Idol/Master; Fashion Icon was already validated.

Starlight Wings now has a same-character hunted-material first-rebirth route
through the earned garment, wardrobe admission and production reload/equip
([406 checks](FIRST_REBIRTH_COMPLETION.md)). Moonlight Wings and Fashion Icon
have an earned Power Arena mount to wardrobe and monthly judging route, including
production reload and title expiry ([111 checks](FASHION_COMPETITION.md)). These
three entries are now code-validated; native acceptance remains pending.

The collection journey's later 65-check extension also proves Love Joker,
Sweet Fairy, Love's Madness and Successor to Legend through paid permanent
designs, sex-specific set eligibility, production reload and ordinary equip.
Fantasy fox ownership does not grant the base fox title.

Big Fan now has an [earned CP mall jersey plus Vesper mount route](EURO_JERSEY_PURCHASES.md),
with atomic purchase, wardrobe admission and production reload/equip. The expanded
collection suite passes 79 checks; Euro Collector remains unproven.

The 34 predicate-only entries comprise four collection rewards, seven progression
rewards, two kingdom-role rewards, Rising Star, four Homecoming titles,
Honored Hero, five local Mr./Ms. 2016 awards, two origin-qualified
CS-CTF awards, four origin-qualified CS Elite PK awards and four origin-qualified
CS Team PK awards. Collection fixtures supply owned designs to
test their predicates; that is not proof of legal acquisition of every design.
Hair/face cosmetic purchases are a separate service and do not close garment or
mount-armor supply gaps. Rune acquisition now has the complete server route above. Other maximum
progression journeys still need explicit verification; cultivation formulas
alone do not prove their earning paths.
Kingdom tests assign a role; ordinary kingdom formation and succession need audit.
Homecoming, Fashion and Honored Hero have server grant/reconciliation paths;
socket login, Host restart and native acceptance remain pending.

Rising Star now uses trusted server receipts, not client-authored bits. The source
inventory contains **217 producer identities and 120 missing identities**. At
least 103 additional producers are needed to reach 320 even before proving
compatibility, character eligibility and playable acquisition. Terato Dragon's
10932 receipt, ten class-specific full-level rune receipts and fifty trainer
promotion identities account for the change from the earlier 156-count checkpoint.
No current fixture supplies a genuine ordinary-character route to 320.

The six unimplemented earning sources remain the historical families
without a production producer: Mr./Ms. 2016 Beauty and Wise (2),
and Poker (4). Heart now has the owner-approved recurring Revora earning route;
its original historical rules remain unverified.
Later cash shops offering similarly named wings do not establish the original
campaign earning rules. Homecoming, Fashion, Planet, Honored Hero and the five
implemented local Mr./Ms. 2016 awards are no longer in this absent set.
The owner-approved recurring Mr./Ms. 2017 edition now produces four titles and
Romance wings through the NPC flower contest. Its 379-check title suite covers
store, ranking, recovery, expiry and NPC interactions, including pagination and
wardrobe reconstruction. The later 879-check Magnolia suite adds a zero-CP donor
earning upgrade funds and a bonus orchid, donating through the NPC and recovering
both category winners' prizes. These five entries are now `code-validated`.
Random outcomes and time are controlled; level and inter-map positioning are
fixture-provided. Full socket/process restart and native acceptance remain open.
See [the implementation record](MR_MS_2017_IMPLEMENTATION.md).

## Verification and limits

The CSV was checked for 72 unique keys, exact type/id composition, installed
point values, complete state classification, existing code/document references
and agreement with the 28 direct eligibility keys plus eight reviewed production
award keys. Production grant callers were inspected; tests and unused award
definitions were not counted as earning producers. The achievement inventory
check is now 217/337 and its three audit tests pass. Promotion validation and
its remaining original-edition/native gates are recorded in
[CLASS_PROMOTION_EARNING](CLASS_PROMOTION_EARNING.md).

M10.1 audit build `FDC7A129FCDBAA4E1C4A13BE22E189609488F1DE3220F5E0C21B167FB21A7FE5`
already passed 344 guild/Grail/activity checks and 91 title checks in M8.1b.
This audit additionally passed Summer 65, CTF 48 and Elite PK 78 against that
same build: 191 refreshed checks, or 626 relevant checks including the preceding
guild/title results.
These are bounded automated checks, not a new deployment or native playtest.
The current-state matrix does not revoke or invent any reward and does not
certify the unreviewed portions of event economies.

M6.46b follow-up: ten class-specific rune receipts raise the source count to
167. Its build passes 589 gameplay checks and three producer-audit tests; see
[FULL_LEVEL_RUNE_ACHIEVEMENTS](FULL_LEVEL_RUNE_ACHIEVEMENTS.md). That checkpoint did not establish rune acquisition. M6.50c subsequently
validated the complete server route, moving Chosen One to `code-validated`: ten
validated, 26 partial and 36 absent sources. Its 16 route checks pass on the
unchanged M6.50b build; native acceptance remains pending.


2026-09-22: Honored Hero moved from absent to `predicate-only`: the approved
Revora career rule now grants a permanent title from five final receipts. Build,
105 title checks and 122 progression checks pass. The new integration uses seeded
receipts, so the combined five-route journey and process/native acceptance remain
open ([scope](HONORED_HERO_REWARDS.md)). Current counts: 10 validated, 27 partial,
35 absent. Earlier count summaries above are dated evidence.


2026-09-22 Homecoming follow-up: Sky Knight, Glory Knight, Paladin and Earth
Knight now have return-snapshot consumers and ordinary wardrobe grants, moving
to `predicate-only`. Frozen raw VIP 0 selects Earth Knight before the VIP-4
login floor; VIP 1-7 still use that floor. Interrupted-session recovery is
implemented in source. Combined socket login, Host restart and native acceptance
remain open ([scope](HOMECOMING_RESEARCH.md)).

2026-09-22 Mr./Ms. 2016 follow-up: local orchid/tulip voting now freezes
Fairy, Scholar, Goddess, Handsome and Fairy Wings. Beauty and Wise stay
`unimplemented`. Current totals: 11 code-validated, 39 predicate-only, 22
unimplemented; 72 keys; 50 eligibility expressions ([scope](MR_MS_2016.md)).

2026-09-22 origin-qualified CS-CTF follow-up: Conqueror and Radiant Wings stay
`predicate-only` until live inter-realm battle and native acceptance exist.
Placement 1 of a two-origin `cs-ctf-v1` run stores both keys; production login
binds this server's origin for wardrobe refresh. Local CTF still grants only
Victor. Current totals: 11 code-validated, 41 predicate-only, 20 unimplemented;
72 keys; 52 eligibility expressions ([scope](ORIGIN_CTF.md)).

2026-09-22 origin-qualified CS Elite PK follow-up: Legendary, Peerless,
Outstanding and Expert stay `predicate-only` until live inter-realm battle and
native acceptance exist. A two-origin `cs-epk-v1` run stores the four placement
titles for origin-authenticated 14-day delivery. Local Elite PK still grants
only Infernal Beast. Current totals: 11 code-validated, 45 predicate-only, 16
unimplemented; 72 keys; 56 eligibility expressions ([scope](ORIGIN_ELITE_PK.md)).

2026-09-22 origin-qualified CS Team PK follow-up: King, Dominator, Power and
Elite Team stay `predicate-only` until live inter-realm battle and native
acceptance exist. A two-origin `cs-team-pk-v1` run stores the four roster
titles for origin-authenticated 14-day delivery. Current totals: 11
code-validated, 49 predicate-only, 12 unimplemented; 72 keys; 60 eligibility
expressions ([scope](ORIGIN_TEAM_PK.md)).


2026-09-22 Fashion coordinator follow-up: Icon, Idol and Master move to
`predicate-only`. Their snapshot/rank producer, fixed-expiry award consumer and
monthly event-tick coordinator are connected in source. Build and 159 title
checks pass; ordinary standings/rules, notifications and process/native acceptance
remain pending ([scope](FASHION_COMPETITION.md)). Current totals: 10 validated,
33 partial, 29 absent; 43 eligibility expressions. Nothing deployed.


2026-09-22 Fashion guide follow-up: garment merchant 6002 (Market 202,179) now
exposes standings/rules and preserves access to shop 502; committed awards return
a brief notice. Build, 169 title and 122 progression checks pass. Process/native
acceptance remains pending, so the three Fashion rows stay `predicate-only` and
counts remain 10 validated / 33 partial / 29 absent ([scope](FASHION_COMPETITION.md)).


2026-09-22 Fashion process follow-up: a separate validation process recovers the
committed season/cursor and grants a saved winner without inherited caches.
170 parent title checks and five child checks pass. Actual game Host/native
acceptance remains pending; counts are unchanged ([scope](FASHION_COMPETITION.md)).

2026-09-22 Honored Hero follow-up: the same ordinary character now earns all five
final receipts through quest and rebirth producers, then equips and reloads the
permanent title (596 Path Hunt checks). Water is hunted, the stone is crafted,
and the token is bought with earned Courier CPs. Levels/promotions and normal
gems remain fixture-supplied; full acquisition and actual Host/native acceptance
remain open. Classification stays predicate-only; counts are unchanged.


2026-09-26 Honored Hero follow-up: 693 combined checks now earn the complete
scripted career from level one, including both master ranks and their hunted/Moon
quest materials, both rebirth levels, mined gems, Waterlord water, stone crafting
and token purchase. Equipped-title reload passes. This supersedes the supplied
prerequisites above; actual Host/native acceptance remains open and classification
counts remain unchanged. [Evidence](HONORED_HERO_REWARDS.md#integrated-promotion-career-2026-09-26).


2026-09-26 current-state reconciliation: direct matrix enumeration confirms43
code-validated,23 predicate-only and6 unimplemented entries, all72 native-pending.
Perfection216/324/648 now have complete current-rule twelve-slot earning, ordinary
shop/tempering, equip and reload evidence (1,058 combined checks). Their conservative
classification remains pending the original admission/shared-phase decision and
actual Host/native gates; it no longer means their current-rule source journey is
missing. See [scope](PERFECTION_TEMPERING.md#earned-648-wardrobe-threshold-2026-09-26).
The source producer audit independently confirms321/337, with16 absent identities;
it does not establish a compatible 320-achievement career or a completion percentage.


### Honored Hero actual career acceptance (2026-09-27)

Honored Hero moves to code-validated after the same ordinary character earned
all five prerequisites over Account/Game TCP, was refused premature equip, then
received/equipped the title and retained it across actual game/database restart.
The path uses existing shops for rebirth materials; native acceptance and the
alternative crafting/Abyss routes remain separate gates. Counts are44 validated,
22 predicate-only and6 unimplemented; all72 still have pending native acceptance.
[Evidence](ROADMAP_STARTUP_ACCEPTANCE.md#honored-hero-earned-and-equipped-through-actual-sockets-2026-09-27).


## Kingdom inventory reconciliation (2026-10-01)

Overlord11000 and Solar Dragon40011000 move from predicate-only to code-validated
following ordinary event earning/equip, atomic succession and old-ruler revocation,
cold successor recovery, and full server/database restart with authenticated
winning-leader login. Interrupted admission also returns safely through disabled
startup and authenticated login. [Exact evidence and limits](KINGDOM_COMPETITION.md).
Both retain pending live/native acceptance; activation remains off by default.
Direct matrix enumeration confirms 62/6/4 across72 unique keys, with all 72 native
acceptance fields pending. This changes the inventory classification, not any
runtime setting, achievement producer count or compatible320-career claim.
