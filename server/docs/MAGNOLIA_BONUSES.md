# Magnolia bonus issuance

Current source status: new version-four seeds add owner-defined Meteor Pack
721269 to Elite and Dragon Soul Pack 720957 to Super, for 41 outcomes. Each custom
pack opens for **one random P7 soul, 1,000 RVPs and 5,000 CPs**. The overall bonus
chance remains 20%; selection within each grade is uniform. Versions zero through
three retain their previous pools. No live deployment.

2026-09-20, baseline `dbc99cb`. Source implementation; not deployed.

The owner selected **20%** for one bonus gift when planting Refined-or-better
seeds. The [official guide](https://co.99.com/guide/quest/2012/Magnolias_All_Around.shtml)
provides grade-dependent examples but no complete pool or odds. This chance
and equal selection among the implemented entries are explicit Revora policy.
Normal seeds receive only their existing base reward. Every seed still grants
its frozen base EXP, Study Points, Chi Token and Revora currency terms.

## Initial catalog-confirmed pools

| Grade | Entries, equally likely after the successful 20% draw |
| --- | --- |
| Refined | Meteor; normal Phoenix, Dragon, Fury, Rainbow, Kylin, Violet, Moon, Tortoise, Thunder or Glory Gem |
| Unique | EXP Potion; 3 Orchids; 1 Tulip; 1 Love Letter; five Saddles |
| Elite | Penitence Amulet; Class 2 Money Bag; 9 Orchids; 3 Tulips; 3 Love Letters; super versions of the ten listed gems |
| Super | Dragon Ball; Meteor Scroll; Praying Stone (M); Celestial Stone; 9 Tulips; 9 Love Letters |

The original version-one pool has 37 outcome entries. New version-two contracts
add canonical Moon Box (721090) to Elite, for 38 entries. Five Saddles are five separate owned items; flower
and letter quantities use their existing quantity-specific item identities.
Binding follows the planted seed. The item identities and requirements match
both reviewed client/server catalogs, checked by
`python tools/audit_magnolia_bonuses.py` with exact file hashes and encrypted
round-trip validation. The audit does not certify downstream item-use behavior.

**Incomplete pool restoration:** Elite Meteor Pack, and Super
Lottery Ticket and Dragon Soul Bag remain excluded pending exact historical
mapping and usable reward contracts. No similarly named event pack is silently
substituted. Downstream flower/letter delivery, saddle use and all remaining
gift-use journeys still need their separate ownership/transaction acceptance.
This initial pool is not a declaration of complete retail Magnolia parity.

## Ownership, versioning and draw behavior

New daily seed records freeze `bonus_version=2`; version-one records retain
their original pool. Pre-bonus records migrate to zero and keep their promised
pre-bonus terms through completion. The version
survives grade changes. No retroactive draw is added to an already issued old
contract. New Normal contracts record an explicit no-bonus result without RNG.

At planting, available space is checked before any random draw. Unique seeds
reserve room for the maximum five-item gift plus the Chi Token; other bonus
grades reserve one gift slot plus the token. Consuming the seed releases its
slot only when its last unit is used. Insufficient capacity preserves the seed
and cannot be used to reroll a revealed result. The existing cryptographic
rejection-sampled RNG supplies a 0..99 chance roll, with 0..19 winning, followed
by an unbiased pool index. Out-of-range draws fail the transaction.

The whole planting transaction includes source consumption, base currencies,
progression/recovery/mentor effects, Chi Token, every bonus item, the durable
`revora_magnolia_bonuses` receipt and daily completion. Both successful and
no-bonus outcomes are saved. Required writes check affected row counts; the
receipt references the existing daily contract. Rollback preserves the entire
reward and source. Client inventory projection occurs only after commit.
Repeat planting and cold reload cannot grant a second outcome.

Seed rerolls remain **CP-only**. The older guide's Study payment was removed
in the [September 2016 notice](https://co.99.com/news/2016-09-09/studypoints.shtml),
before 6711. A briefly drafted Study option was discarded before committing;
the final regression explicitly rejects that obsolete option. No 25-Study
payment is enabled or deployed.

## Validation and remaining work

Isolated x86 build; **680 Magnolia checks**, **579 gift/amulet checks** and the
37-identity catalog audit pass. Each pool entry is earned through ordinary
seed issuance, a paid grade change, walking to a real flower bed, planting and
production item/character reload, with both bindings represented. The tests
control RNG to cover outcomes; they do not bypass payment or eligibility.

The amulet outcome additionally follows the actual earned item through ordinary
use and another cold reload, preserving the 30-PK reduction and its use receipt.
Other checks cover the winning roll 19, losing rolls 20/99, exact worst-case
capacity, no draw when full, atomic rollback on a suppressed bonus receipt,
legacy contract preservation, Normal no-draw behavior and existing quest faults.
Logs: `obj/chi-refill-validation/magnolia-bonuses-final.log` and
`obj/chi-refill-validation/magnolia-bonus-gifts.log`.

Remaining: the three excluded reward mappings, complete downstream gift-use
journeys, native dialogue/inventory/effect/reconnect acceptance, historical
selection parity and authorized deployment. Preserve daily/bonus receipts on
rollback. No live binaries, clients or production player data changed.

## Canonical Moon Box extension

Follow-up to `42e1638`: new version-two Elite contracts use the same 721090
Moon Box already earned by Revora's Moon Box quest and accepted for promotion.
This is an explicit Revora alias choice, not a verified historical Magnolia
item ID. Random gift opening remains unfinished. Version-one Elite contracts
retain their fifteen-entry pool; version two has sixteen. The overall bonus
chance remains 20%.

The isolated build, **685 Magnolia checks** and **38-identity catalog audit**
pass. The earned box funds an ordinary character's promotion, is consumed once,
and survives production reload without granting unrelated quest achievement
11022. The fixture uses production teleport to the trainer; this does not prove
the native travel or dialogue experience. Log:
`obj/chi-refill-validation/magnolia-moonbox.log`.

Remaining candidate evidence is insufficient to invent pack contents:
720957 does not specify a Dragon Soul phase/pool; Meteor Pack 721269 lacks a
quantity and 721510 is a Spring Festival reward. Lottery Ticket 710212's
exchange for three Small Lottery Tickets is now confirmed and implemented at
Lady Luck ([contract and tests](LOTTERY_TICKET_RESTORATION.md)); its downstream
draw/reroll/prize transactions remain unfinished. These items stay excluded
pending their complete reward contracts at that checkpoint. The Lottery Ticket
route is now completed in source as described below; Meteor Pack and Dragon
Soul Bag remain excluded.

## Version-three earned lottery route

New Super contracts add LotteryTicket 710212. Version-two Super contracts keep
their exact six-entry pool; version-one Elite and pre-bonus contracts also keep
their earlier terms. There is no retroactive extra chance on an existing seed.
The catalog audit now checks 39 identities against both exact client/server
catalog hashes. Equal slot selection remains explicit Revora policy.

The actual earned ticket is cold-reloaded, exchanged at the installed Lady Luck
for three matching-bound Small Lottery Tickets, and spent at an installed map
700 box. A suppressed draw receipt preserves all three tickets and creates no
pending result. Reconnect/retry commits one draw; another reconnect restores
that result for an exactly-once claim. Autosave/item reload retain the prize and
all planting, exchange and draw receipts. The test uses production teleport
from the garden to a legal Twin City landing, walking beside Lady Luck, her real
lottery travel option and walking to the box. It does not prove a native journey.

Validation: isolated build, 39-identity audit and 812 Magnolia checks plus three
fresh-process flower assertions pass. Log:
`obj/chi-refill-validation/magnolia-earned-lottery.log`.
The draw/claim ownership and remaining native/parity limitations are detailed
in [Lottery restoration](LOTTERY_TICKET_RESTORATION.md).

## Earned consumable and rebirth journeys

Follow-up to `bbe6f42`: the Magnolia fixture now follows the actual earned
EXP Potion (723017), Meteor Scroll (720027), Class 2 Money Bag (723714) and
Praying Stone (720578) through use. Each starts with NPC seed issuance, paid
grade change, controlled bonus RNG, ordinary walking, planting and production
item reload. Suppressing the use receipt must roll back consumption and all
effects while retaining the completed planting record. Reload then permits one
successful retry. Autosave, another reload and consumed-UID replay retain
exactly one hour of double EXP, ten binding-preserving Meteors, 800,000 silver,
or seven days of blessing respectively. No replacement fixture gift is supplied.

The earned Celestial Stone (721259) also funds one first rebirth, retaining
its binding on the gem and weapon gifts and recording achievements 11030 and
10315 without second-rebirth claims. Trainer travel uses production teleport;
native travel and dialogue are not proven. The fixture establishes the required
class/level before seed issuance rather than claiming Magnolia earns promotion
eligibility. Achievement assertions now run the production TitleStorage login
step after character reload, including the Moon Box negative assertion.

These checks validate existing services across their earning boundaries; this
batch changes tests and documentation, not production reward rules. Remaining
flower/letter, saddle, gem/material use chains and native acceptance stay open.

Validation: **702 Magnolia checks pass**, with the fixture compiled against the
existing isolated server build. Log:
`obj/chi-refill-validation/magnolia-earned-gifts.log`. No live changes.

## Earned Wrangler enrollment

The saddle fixture spans eight separate daily contracts with controlled RNG
selecting a five-saddle gift each time. This is eight forced winning test days,
not a promise of eight-day completion or a change to the 20% bonus odds.
No saddles are supplied directly. Ordinary warehouse dispatch stores the Chi
Tokens and the first five saddles, making room for later daily rewards. After
each day, production item reload retains both stored and carried materials.
The final withdrawal fills the bag with forty earned saddles for Wrangler P1.

The fixture completes login stat calculations before quest EXP can level the
character; it loads subclass membership and achievement evidence separately
after the final character reload, as production login does. Travel between
the garden and warehouse is a production teleport fixture, not native travel
acceptance. Warehouse request security is not exhaustively audited here.
Required-write fixes and fault validation are documented in
[subclass progression](SUBCLASS_PROGRESSION.md).

Validation: isolated server build, **729 Magnolia checks** and **71 subclass
checks** pass. The final enrollment consumes the forty earned saddles once,
persists Wrangler level/phase one and P1 achievement 11201, and does not grant
P9 achievement 11202. Log: `obj/chi-refill-validation/magnolia-wrangler.log`.
No live deployment; remaining flower/letter and gem/material journeys stay open.


## Owner-defined custom packs, version four

The owner chose custom contents for the two unresolved pack families. This is a
Revora economy decision, not recovered retail contents. Both item identities now
use the ordinary gift-opening transaction. The random soul pool is the loaded
800000?899999 item family with PurificationLevel 7, sorted by identity. Binding
follows the source pack. Exactly one source unit, one soul, 5,000 ordinary CPs,
1,000 RVPs and an opening receipt commit together. Immediate currency projection
uses the established CP/RVP update paths. Full bags, unsigned balance overflow,
stale ownership or stale balances cannot partially award or consume the pack.
A stacked pack retains its remaining units and uses the existing one-second
request debounce; later intentional use is allowed.

Seed bonus version four extends Elite to 17 choices and Super to eight. Existing
versions remain unchanged. Normal/Refined/Unique bonus behavior is unchanged.
The client identity audit now uses the actual loaded `itemtype-new.txt`, rather
than the unused older catalog. It verifies 41 identities and requirements; it
makes no claim that the historical descriptions match these new custom contents.

Validation: isolated build, 850 Magnolia checks and 1,341 gift-service checks pass.
The earned planting-to-pack routes cover both binding states, late receipt rollback,
concurrent opening with autosave, exact CP/RVP amounts and cold reload. Gift checks
also cover zero-count/single-unit and stacked sources, full bags and both currency
overflow boundaries. Existing scroll, silver bag and lottery routes remain covered.
Four tooltip tests prove only description field 53 changes and cipher round trips.

`tools/stage_magnolia_custom_packs.py` stages matching descriptions for both IDs
in a new private directory. The ignored output is
`obj/chi-refill-validation/magnolia-custom-overlay`; encrypted catalog SHA-256
`c4b8a6503cc35e7f16d105796990a8d9859a32d3921757cc88d3b80eb0b8bd96`.
The active client is untouched. This is unsigned review content, not a released
patch; merge with other accepted overlays before signing/release.

The owner's wider [valuable quest reward standard](QUEST_REWARD_STANDARD.md)
remains a separate incomplete milestone. These occasional bonus packs alone do
not establish compliance of all Magnolia base rewards or all server quests.

## Guaranteed harvest upgrade (2026-09-20)

New Magnolia contracts now freeze `valuable_reward=true` separately from their
bonus-pool version. Every successful harvest adds one unbound P7 soul, selected
uniformly from the loaded catalog, even when the bonus roll fails. Existing
contracts default false. Chi Token binding still follows the seed; the guaranteed
soul is unbound. The separate 20% bonus chance and version-four custom packs
remain unchanged, so a custom pack can grant an additional P7 soul plus its
1,000 RVPs and 5,000 CPs when opened.

The guaranteed soul UID commits in `soul_uid` with seed consumption, currencies,
EXP, Chi Token, bonus and completion. Capacity reserves both guaranteed gifts
and the maximum grade bonus before drawing, preventing inventory-based rerolls.
The fixtures distinguish both souls and warehouse the guaranteed souls during
the eight-day saddle earning route. Native/client release remains pending.
Log: `obj/chi-refill-validation/magnolia-p7-harvest.log` (ignored).

Isolated build and 853 Magnolia checks pass for the guaranteed-harvest upgrade.
