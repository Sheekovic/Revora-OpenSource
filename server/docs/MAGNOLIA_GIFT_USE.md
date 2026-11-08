# Magnolia gift catalog and owned pack prerequisite

Current follow-up: [Magnolia bonuses](MAGNOLIA_BONUSES.md) now issue 38
catalog-confirmed outcomes at the owner-selected 20% chance. Version three now
adds the restored Lottery Ticket route for 39 outcomes. The tables below
record the earlier prerequisite audit; their issuance-pending notes are
superseded for those included outcomes. Three ambiguous mappings and several
downstream use chains remain open.

The current earning fixture also follows actual Magnolia rewards through EXP
Potion, Meteor Scroll, Money Bag and Praying Stone use. Each route reloads the
earned item, suppresses its use receipt to prove rollback, reloads and retries,
then verifies exact rewards after autosave/reload and consumed-UID replay.
Celestial Stone earning-to-first-rebirth validation is recorded in the bonus
document. The historical prerequisite results below are retained as such.

Date: 2026-09-16. M6.56b2b2a. Server implementation, not deployed.

The extra-gift audit found two concrete use-path defects. Meteor Scroll 720027
used separate removal and ten-item additions without a shared transaction or
binding propagation. Class2MoneyBag 723714 had catalog/reward references but no
owned use handler. `QuestGiftPacks` now makes both ordinary-player services
atomic. This is a prerequisite; Magnolia does not issue these extra gifts yet.

## Confirmed pack contracts

| Item | Owned use result | Evidence |
| --- | --- | --- |
| 720027 Meteor Scroll | Ten separate Meteors 1088001, preserving binding | Loaded server and active 6711 catalog; prior ten-Meteor handler |
| 723714 Class 2 Money Bag | 800,000 silver | Loaded server and active 6711 catalog |

The active `6711/ini/itemtype.dat` was decoded read-only with Revora's existing
transform and verified by exact encrypted round trip. SHA-256:
`D49F0658BF01387B99C284DDEC81713838F2EAE3ADB8E63666E5C906F63CF87A`.
No proprietary files were edited, copied into Git or packaged.

These contracts apply to every eligible ordinary player holding the items,
regardless of their earning source. They grant no quest completion, title,
achievement, CPs or RVPs. Supplied fixture packs are not represented as earned
Magnolia rewards.

## Transaction and item-use boundary

Existing 1009 action 4 dispatch now recognizes both IDs before legacy routing.
The bounded handler checks 32..128 total bytes, declared body plus eight-byte
trailer, packet ID, action and seal. Direct item-use calls share the same owned
service. No new native packet family or captured-client evidence is claimed.

A healthy canonical loaded player must be alive, outside trade/booth, and own
the exact unlocked, non-suspicious bag item. Warehouse/equipment/wardrobe items
are rejected. Legacy zero-stack singleton and unexpired timed packs are
supported; saved binding, stack, expiry and source identity must match.

Under the owner lock, the character row is locked and the whole bag UID set is
checked. One transaction then consumes exactly one source unit, inserts all ten
Meteors or persists the silver balance, and saves an exact
`revora_quest_gift_openings` receipt. All required writes check affected counts.
A failed sixth Meteor insert, source debit, balance write or receipt insertion
rolls everything back. Silver uses `SilverBalance` so pending owner gains are
preserved and stale durable credits cannot be overwritten. Database signed
64-bit overflow is rejected before consumption. Nullable historical silver
keeps the production loader's zero semantics.

A scroll requires ten output slots, crediting its own slot only when it is
actually consumed. A money bag needs no extra slot. Client item/silver updates
follow commit, and autosave's silver preimage advances with it. Uncertain
failures fence the session for reload. A bounded one-second per-item debounce
prevents simultaneous stacked requests from consuming multiple intended units;
this is not a claim of a durable native request identifier. Reconnecting may
use remaining owned stack units, never recreate an already-consumed unit.

## Remaining Magnolia gift catalog work

The [official Magnolia guide](https://co.99.com/guide/quest/2012/Magnolias_All_Around.shtml)
lists grade-dependent bonus examples but no outcome odds or complete selection
contract. The current server catalog provides these candidate mappings:

| Gift family | Candidate IDs / status |
| --- | --- |
| EXP Potion | 723017; ordinary use and timer ownership repaired in [EXP potion transactions](EXP_POTION_TRANSACTIONS.md); Magnolia issuance pending |
| Orchids / Tulips / Love Letters | 753003/753009, 754001/754003/754009, 756001/756003/756009; earned recipient transactions, rollback and cleared-cache reload now validated ([durable delivery](FLOWER_GIFT_RESTORATION.md)); offline startup ranking and native acceptance remain open |
| Saddle | 723903; eight earned five-unit rewards now fund Wrangler P1 through warehouse storage and production reload ([fixture scope](MAGNOLIA_BONUSES.md)); native acceptance remains open |
| Moon Box | Many 7210xx aliases; existing promotion material rules provide one use, random opening needs separate review |
| Penitence Amulet | 720128/723727; atomic use and shared durable cooldown implemented; minimum VIP4 policy preserved; Magnolia issuance, exact exception precedence and native acceptance pending ([evidence](PENITENCE_AMULET_RESEARCH.md)) |
| Class 2 Money Bag / Meteor Scroll | Use repaired here; Magnolia issuance pending |
| Praying Stone (M) | 1200001 and exact catalog alias 720578 now share the validated seven-day service ([migration and use checks](PRAYING_STONE_EARNING.md)); historical Magnolia alias selection and issuance remain pending |
| Celestial Stone | 721259 has the validated first-rebirth material route; requires a new Magnolia-to-rebirth earning fixture |
| Lottery Ticket | 710212 describes a three-small-ticket exchange; no reference to that item was found in the current NPC handler |
| Dragon Soul Bag | Generic 720957 DragonSoulPack is a candidate, not a verified mapping of the guide's bag or its soul pool |
| Meteor Pack | 721269 and 721510 have ambiguous historical descriptions; neither proves the intended quest quantity |
| Gems / Dragon Ball / Meteor | Loaded families/materials exist; grade selection and complete earned-use fixtures remain |

Do not replace unresolved names with a similarly named event-specific pack or
invent original bonus odds. An explicit Revora selection policy must be stated
separately from published evidence. The five-grade base route is documented in
[Magnolia grades](MAGNOLIA_GRADES.md); bonus issuance and remaining use services
stay open under M6.56b2b2.

## Validation and release limits

The focused gift suite passes 490 checks, including first-rebirth prerequisites.
It covers ordinary bounded item-use dispatch, both bindings, zero/single/stacked
sources, concurrent autosave/use, production reload, intentional later uses,
exact/full bag capacity, pending and nullable silver, signed overflow,
suppressed source/stack/sixth-item/balance/receipt writes with reload retry,
stale durable ownership/bag/silver, dead/fake/trading/replaced actors, prohibited
item locations/metadata, valid and expired timed sources, and malformed frames.
Only the disposable validation schema is mutated.

Final validation: isolated Debug x86 build; gifts 490, Magnolia 552 and
Praying Stones 50 (1,092 gameplay checks), plus three achievement audits.
Producer inventory remains 219 present / 118 absent, not per-player proof.
Ignored logs: `obj/chi-refill-validation/quest-gifts-first.log`,
`quest-gifts-magnolia.log` and `quest-gifts-praying.log`.
Server SHA-256: `8B5A71BFE550E2BD6960B1C0866B568197E098C77F9634D70EA5F67EC6204E40`.
Harness SHA-256: `B4FF153E1E139C75E95D00409C926FA71FD842938D3BBB7DEDCA9F438CBFA624`.
Genuine 6711 immediate display/use/reconnect acceptance and live activation
remain open. No restart, live migration, client patch or website publication
occurred. Rollback preserves opening receipts and current player balances.
