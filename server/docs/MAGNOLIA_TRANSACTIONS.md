# Magnolia daily quest transactions

2026-09-21: new daily harvest contracts additionally award one bound level-1
Tortoise Breaker rune, with exact `rune_uid` persisted in the same transaction.
Old contracts retain their original terms. The 20% bonus chance and custom
pack contents are unchanged; see [current daily policy](DAILY_REWARD_POLICY.md).
Build and 867 Magnolia checks including shared prerequisites pass. Coverage
includes rune-insert rollback, capacity before bonus selection, receipt/binding
reload and ordinary warehouse handling during the eight-day saddle journey.
Log: `obj/chi-refill-validation/magnolia-rune-tests.log` (ignored).
Native acceptance and deployment remain pending.

M6.56b2a, 2026-09-16, baseline `3cf820a`. This completes the existing Revora
Magnolia seed-to-plant-to-reward route with durable daily eligibility and atomic
payments/rewards. It does not certify the complete official five-grade quest.

## Evidence and edition

The [official Magnolia guide](https://co.99.com/guide/quest/2012/Magnolias_All_Around.shtml)
describes a daily level-80 quest, garden seed collection and planting. It also
has five grades, paid seed changes and grade-dependent rewards. Revora's prior
code instead has a free Refined seed and a 500-CP Super seed with the same daily
bundle. That existing custom economy is preserved here and stated plainly in
the seed-choice dialogue. Official grade upgrades and reward parity remain open;
unknown grade probabilities are not invented.

Local evidence is the baseline NPC 44100 handler, Magnolia item cases
729307/729310, `System/Config.cs`, `ExplorationWorld` and the installed map/NPC
catalog. Current configured rewards are 2,500 CPs, 15 RVPs, one Chi Token 729304,
one existing EXP Ball equivalent and 10 study points. The Super seed fee is
500 CPs. These are Revora settings, not official prices or EXP conversion claims.

Garden entry retains Daily Quest Envoy choice 5 and the reviewed physical map
1806. Magnolia keeper 44100 stands at installed (102,105). Planting remains
within six tiles of either legacy bed anchor (98,111) or (106,98). The two bed
centers are not valid walking tiles. Actual terrain checks and production walks
reach (95,108) and (104,96), respectively. The existing keeper return reaches
Twin City through `ExplorationWorld`.

## Seed issue and daily record

`revora_magnolia_runs` stores one record per character and Pacific calendar day.
A fresh seed-choice offer expires after two minutes and freezes the displayed
Super seed fee. Both free and paid choices require the ordinary authenticated,
canonical player, the physical keeper, minimum level, alive/non-trading state,
capacity and current durable eligibility. Item creation, any CP payment and the
daily seed record commit together. Duplicate confirmations, reconnects and
concurrent requests cannot issue or charge twice.

The record freezes CP/RVP/study/Chi reward terms when a seed is issued. Changing
those settings afterward does not change the promised bundle. The old
`CliamSeed` field was RAM-only; it is now a projection of the durable record.
A trustworthy current legacy completion can be imported as completed without
fabricating a historical seed, currency award or achievement. The old claimed
flag alone cannot reconstruct historical issuance.

Owned historical seeds remain usable, including bound items and legacy
zero-stack singletons. Planting such a seed creates today's record in the same
transaction. A seed carried into a later day can complete that day's quest;
carrying a seed prevents collecting another from the keeper. Existing older
records do not prevent next-day play. Future-dated records fence daily eligibility
against backward clock movement without consuming the item.

## Atomic planting reward

Planting checks the physical garden, bed proximity, loaded profile, item
reference and durable ownership, stack, binding, lock, suspicion, wardrobe and
expiry. Cursed or otherwise ineligible characters keep the seed. Insufficient
space or currency/study overflow preserves the whole reward for a later attempt.
A singleton frees the space needed by its token; a remaining stack needs one
empty slot. Legacy nullable balances retain their loader's zero semantics.

One transaction includes the complete progression profile, all three point
balances, exact seed debit, Chi Token insertion, level/skill/Arena/mentor
effects, any recorded rebirth recovery and the daily completion receipt.
Changed preimages or failed/suppressed writes roll everything back and fence the
session for production reload. The recorded reward survives a retry. A bound
source cannot become a tradable Chi Token. At maximum level, the non-EXP reward
remains available while new EXP is zero.

Committed state uses the existing inventory, CP/RVP, progression and study-point
updates. The legacy flower-effect builder now declares its actual one-string
payload rather than 22 strings. Native rendering still needs genuine 6711
acceptance. Diary flags and the keeper/Envoy menu are projected from the record;
resetting an old flag cannot reopen a completed day. No achievement identity is
invented for planting. The already verified garden travel discovery remains
part of the complete travel route.

## Validation and limits

The focused suite uses installed terrain and keeper placement. An ordinary
player walks to Daily Quest Envoy, enters the garden, walks to the keeper,
claims a free seed, walks to a bed, plants, receives the bundle, returns and
reloads through production loaders. Both seed choices also survive reconnect,
walk to their bed and plant through the same ordinary handlers.

Further cases exercise daily rollover and clock rollback, legacy completion
import, fixed reward terms, concurrent planting/autosaves, minimum level,
remote/dead/fake/trading/incomplete/replaced actors, item restrictions, full
bags, capped balances, maximum level, nullable historical balances, selected
item ownership and suppressed seed/payment/reward/profile/skill/receipt/mentor/
recovery writes. Reconnect retries preserve both daily eligibility and payment.

The official five-grade upgrade and reward system remains a separate research
and implementation task. Other Daily Envoy bundles and the Chi Token's own
later exchange are not certified by this quest transaction. Genuine client
visuals and live deployment remain open; no restart, patch or site publication
occurred.

Final validation: Debug x86 build; focused Magnolia 466 (392 first-rebirth
prerequisites plus 74 garden checks), experience 452, travel 105, Release Souls
35, quest packets 9, progression 74, EXP items 456, prize packs 498 and default
216 (2,311 gameplay checks), plus three achievement audits. The inventory stays
219 present / 118 absent; this quest does not invent an achievement mapping.
Local logs: `obj/chi-refill-validation/magnolia*.log`. Final server SHA-256:
`B7C6929C6C219A3ACAFC724FC3054C8FEA9B57D0FF3E0EED35E9FAC03D28F26A`.

2026-09-16 grade follow-up: [Magnolia grade changes](MAGNOLIA_GRADES.md) now add five-grade base rewards, CP-only random changes and the 100-CP Super choice. New daily contracts use the revised terms; existing stored contracts retain the historical promises described above. Exact lottery parity, extra gifts and genuine-client acceptance remain open.

## Fresh production-created career (2026-10-03)

The fresh ordinary career buys a Super seed for100 CPs, preserves it across a
process restart, and plants through the normal item handler. Exact non-P7 base
rewards and the unforced roll31/no-bonus receipt survive cold reload. Duplicate
seed purchases and consumed-seed use do not charge or reward again. No clock or
RNG override is used. This verifies one outcome, not bonus frequency or native
acceptance. [Evidence](CAREER_STARTING_BOUNDARY.md#fresh-super-magnolia-with-unforced-bonus-draw-2026-10-03).
