# Mystic Ring implementation

Current status (2026-10-01): this chain is included in the completed private
[nineteen-chain earning/restart validation](EQUIPMENT_QUEST_AGGREGATE.md).
The approved Revora10931 evaluator is implemented and tested; older statements
below that it is disabled or that no process restart was tested are historical.
Native/authenticated-client acceptance and live deployment remain pending.

## Contract and catalog checkpoint ? 2026-09-26

The [official guide](https://co.99.com/guide/quests/mysticring.shtml) describes
medicine delivery, a Ratling/FireRatL38 hunt, a scroll for Shen Junior, and use
of the ring to become a blue mouse. It identifies the quest as repeatable after
rebirth. The installed 6711 journal is the selected route evidence; the older
Facebook edition's badges and extra EXP Ball are not imported.

`audit_mystic_ring.py` verifies missions 1738..1744, predecessor links, level
48..68/all-class eligibility, NPC routes, item requirements, hunt/use counters
and pack 728240. Three contract tests pass. The JSON is evidence, not a claim
of implemented earning or completed aggregate achievement 10931.

| Mission | Installed objective | Silver |
| --- | --- | --- |
| 1738 | Chang to Minstrel, 1011:184,282 | 100 |
| 1739 | Minstrel to Dealer Shen, 1011:779,477 | 100 |
| 1740 | Bring 12 Stanchers (1000000) and 12 Agrypnotics (1001000) | 100 |
| 1741 | Defeat 20 Ratlings | 100 |
| 1742 | Deliver the quest Castle Gate to Shen Junior, 1011:920,552; return to Shen | 100 |
| 1743 | Exchange Antique Ring, then use Mystic Ring | 1500 + 1/3 EXP Ball |
| 1744 | Return to Chang for Level52UniqueHeadgearPack | ? |

The client item descriptions resolve important identities: **721710** is the
quest CastleGate entrusted to Shen Junior; **1060023** is the ordinary Phoenix
teleport scroll and must not be silently substituted or consumed. Antique Ring
is 721707; its upgraded usable Mystic Ring is 721708. Medicine hand-in must
consume exactly twelve units of each across legitimate stack rows, atomically
with quest progress. Neither ring is an equippable ring reward.

The loaded catalog confirms Ratling family 8 and FireRatL38 family 67 spawn on
1011. FireRat 112 and FireRatA..D 9020..9023 are different families and are not
implicitly eligible. Dealer Shen already exists as NPC 4501 at 1011:779,477
with lookface 6767; its direction suffix must not be mistaken for another model.
Minstrel has no matching loaded row. Shen Junior 4502 exists at **5000:84,95**,
not at the journal position. The official guide and quest item description say
924,556, while the journal says 920,552. The inspected Portals.ini contains no
5000 section or destination; this alone is not proof that every other travel
mechanism is absent.

Proposed Revora integration: reuse Dealer Shen, add Minstrel, and provide a
quest-owned Shen Junior alias at the installed journal's 1011:920,552 location,
preserving the existing NPC on 5000. The private journal must name the alias.
Validate terrain before claiming native reachability. This choice avoids moving
an existing actor or routing the quest through an unproven mine entrance.

Client npc.ini identifies types 675/676/677 for Minstrel/Shen/Junior. BlueMouse
NPC type 689 references simple object **30040**, using part 202000000 with texture
402000000. Ordinary object 202 uses texture 202000000 instead. Consequently,
a plain Ratling body is not sufficient evidence for the blue appearance. The
catalog report records these links and hashes, but player-mesh rendering remains
unverified. The ring should use an owned cosmetic lease that preserves Body,
Face and HP and cannot fight with Spun Gold's appearance lease.

Revora policy: minimum level 48 with no upper level cap, once per rebirth 0/1/2;
bound tracked quest items and a final bound pack plus a frozen bound P7 soul.
Original total silver is 2,000, plus twenty-minute EXP. This remains a one-time
quest per rebirth, never a daily P7 source. Lease duration is unpublished; use
an explicit Revora five-minute cosmetic duration, subject to native acceptance.

## Implemented reward pack

FixedOutputPacks now atomically opens 728240 into one Unique (+0), level-52
headgear item, preserving the source pack's binding. Class is captured at use;
unsupported classes keep the unopened pack. These explicit Revora per-class
choices are checked against loaded level, required class and quality:

| Class | Item ID |
| --- | --- |
| Trojan | 118057 GoldCoronet |
| Warrior | 111057 PearlHelmet |
| Archer | 113027 JackalHat |
| Ninja | 112057 FlameVeil |
| Monk | 143057 PureHeadband |
| Pirate | 144057 TranquilHat |
| Dragon Warrior | 148057 MercyHood |
| Water/Fire Taoist | 114057 LotusCap |
| Windwalker | 170057 DarkCloudHat |

Build and **435 checks pass** (406 prerequisites + 29 pack checks), covering
bound/unbound stacks, concurrent opening, full bags, injected insert rollback,
reconnect retry, ten class cases and exact output persistence, and unsupported
classes. Logs: `obj/mystic-pack-build.log`, `obj/mystic-pack-tests.log`.

**Not implemented:** NPC/medicine/hunt/delivery/ring progression, appearance
lease, durable recovery, final quest claim, journal and private overlay. Native
and Host acceptance are pending. No production schema, live process or active
client changed. Pack support does not complete this quest.

## Saved referrals and medicine hand-in ? 2026-09-26

MysticRingStore now persists acceptance, Minstrel/Shen referrals and the medicine
hand-in, keyed by character and rebirth. Acceptance freezes the eventual P7
choice. Ordered advancement prevents duplicate referral payments or medicine
replay. This is a persistence component; the future NPC coordinator must hold
the session-install lease and item lock, provide the exact NPC context, and
fence a session if applying committed state fails. No production startup or
NPC route invokes the component yet.

Medicine selection requires exactly twelve Stanchers and twelve Agrypnotics.
It spends usable permanent bound stacks first, then unbound stacks, and preserves
remainders and their binding. Both medicines, source receipts, the 100-silver
payment and stage advancement commit together. The recipe excludes locked,
suspicious, wardrobe, equipped, stored and timed items. Persisted ownership,
stack size, binding and flags are checked again while debiting. The player's
saved level/rebirth and complete inventory UID set must match memory. A changed
final NPC/session context rolls everything back before any memory projection.

Build and **425 checks pass** (406 prerequisites + 19 medicine checks): ordered
referrals across rebirth 0/1/2, bound-first split-stack selection, concurrent
hand-ins, exact receipts, partial-stack/silver/stage reload, insufficient and
locked medicines, second-receipt fault rollback, changed final context, stale
persisted locks and retry after rollback. The first fixture compile referenced
a nonexistent helper; the corrected fixture changes a test row and reloads it.
Logs: `obj/mystic-medicine-build.log`, `obj/mystic-medicine-tests.log`.

Still required: Ratling credits, owned scroll/Antique/Mystic Ring transitions,
cosmetic lease and final reward persistence; NPC/item/death wiring, recovery,
journal/overlay and full ordinary-player route validation. Native appearance,
terrain and Host acceptance remain pending. No production schema or live
process changed. This checkpoint does not complete Mystic Ring.

## Ratling hunt and owned item progression ? 2026-09-26

The persistence component now credits genuine Ratling/FireRatL38 deaths on map
1011 after medicine delivery, caps the saved count at 20, and rejects wrong
families, pre-stage deaths and duplicate callbacks. The future death route must
call Hunt under the owner/session context and Revived on genuine respawn.

After 20 kills, Shen's hand-in issues bound quest CastleGate 721710 and pays
100 silver. Junior's exchange consumes that exact scroll and issues bound
AntiqueRing 721707. Returning to Shen retains the ring and pays another 100;
Minstrel then exchanges it for bound MysticRing 721708. Each exchange is atomic
with saved stage/UID/type. The ordinary teleport scroll 1060023 is never accepted
as the quest scroll. Full bags preserve earned hunt credit; one-for-one exchanges
can replace their source in a full bag. Current payments total 500 silver; the
remaining 1,500 and EXP belong to the pending ring-use stage.

Lost scrolls and rings can be replaced through a durable old/new UID receipt.
Recovery refuses any source row still present in any owner/storage location,
preserves stage and kills, and cannot manufacture an item before it was earned.
The final ring remains unusable through this component: cosmetic use and final
claim have not been implemented and cannot be bypassed by Progress action 8.

Build and **441 checks pass** (406 prerequisites + 35 progression checks), across
rebirth 0/1/2: actual 19/20 kills and replay, wrong family and pre-stage rejection,
reload/cap, full-bag scroll retry, exact quest-scroll identity, scroll loss,
Antique Ring storage, ordered exchanges, failed Mystic Ring insertion rollback,
full-bag replacement, exact ring persistence and lost-ring recovery. These tests
invoke the store after actual deaths; NPC/death wiring is not yet enabled.
Logs: `obj/mystic-progression-build.log`, `obj/mystic-progression-tests.log`.

Next: owned mouse cosmetic use and final claim, then NPC/item/death integration,
saved journal/recovery dialogue and full ordinary-player route validation.
Native/Host acceptance and deployment remain gated; no live schema changed.

## Owned cosmetic use and final claim ? 2026-09-26

QuestAppearance now owns the shared bounded cosmetic registry for both Spun Gold
and Mystic Ring. SpunGoldAppearance remains the compatibility wrapper, including
the existing test clock. A character may have one active quest appearance;
another kind returns busy without replacing it. Tokens include owner/session
and kind checks. Same-kind application does not renew a live lease. Expiry,
map departure, death, session replacement and real combat transformation retire
leases. Body, Face, class, HP and transformation identity are not rewritten.
The existing Entity spawn/update mesh projection is reused; no packet layout
changed. Stale or wrong-kind cleanup cannot clear another lease.

Mystic Ring reserves a five-minute mouse lease using simple-object candidate
30040, then atomically consumes its exact bound ring, saves stage 9, pays 1,500
silver and twenty-minute EXP. A failed transaction clears the provisional lease;
a conflicting Spun Gold disguise leaves ring and progress untouched. Together
with earlier stages, silver totals 2,000. The appearance reservation's context
callback must only check in-memory world/NPC eligibility, since the transaction
already owns the character's saved rows. Post-commit application failures remain
the future coordinator's responsibility to fence for reload.

Final claim requires stage 9, a supported class and two bag slots. It inserts
one bound headgear pack and the bound P7 frozen at acceptance, saves exact output
UIDs and completes stage 10 in the same transaction. Ring-use/claim replays cannot
pay again. Expiry of the cosmetic does not undo completion eligibility.

Build and **484 checks pass**: 406 prerequisites, twelve existing Spun Gold
appearance regressions, 35 hunt/item progression checks and 31 reward/appearance
checks across rebirth 0/1/2. Added evidence covers untouched identity/HP under
cosmetic projection, wrong-kind cleanup, both directions of disguise conflict,
ring transaction rollback, silver/EXP, expiry, full bags, failed P7 insertion,
concurrent claims, reward reload and ordinary earned-pack opening. Logs:
`obj/mystic-rewards-build.log`, `obj/mystic-rewards-tests.log`.

**Rendering limit:** the client NPC/simple-object asset chain supports 30040 as
a blue-mouse candidate, but an NPC asset mapping does not itself prove that the
native player-mesh renderer accepts it. Tests verify the shared spawn/update
field, not blue color, animation or native appearance. That acceptance remains
open. No active client files or live processes changed.

Next: ordinary Minstrel/Shen/Junior routes, normal item/death dispatch, recovery
and journal, then full ordinary-player earning tests. Production startup does
not yet initialize MysticRingStore or call its use/claim methods. The source
components alone do not complete Mystic Ring or achievement 10931.


## 2026-09-26 ordinary route integration

The source startup now initializes Mystic Ring, and ordinary NPC, item-use,
monster death/revival and saved-journal handlers are connected. Chang offers the
errand at level 48, once per rebirth (0/1/2). Existing Dealer Shen is reused with
his original orientation. Outdoor Shen Junior uses ID 900071742 at 1011 920,552;
the existing mine NPC 4502 is preserved. The coordinator fences persistence
failures for reload. Journal Begin/abandon requests cannot forge saved progress.

Build and **493 checks pass** (406 shared prerequisites + 87 route checks),
using normal dialogue, medicine, combat, item-use and reward handlers for all
three rebirth states. Coverage includes remote/forged requests, duplicate kills,
19/20 hunt progress, reload, lost-scroll recovery, return-order enforcement,
ring-use location, claim rollback/fencing, reward replay, completed recovery,
bound P7/headgear delivery and normal pack opening. Logs are
`obj/mystic-quest-build.log` and `obj/mystic-quest-tests.log`.

The private journal overlay updates missions 1738?1744 over the Artisan bundle,
including the outdoor Junior and Revora reward rules. Its preparation verifies
input hashes, idempotence and unchanged unrelated sections. Output SHA-256:
`ff5096676959b02251eb8ab84870bfcd3e8bd48bf0942d8bd69c32d309f4561f`.
It is unsigned, uninstalled and unpublished. Existing task-status projection is
reused; original per-objective counters are not claimed as implemented.

`tools/audit_mystic_terrain.py` verifies shared cardinal walking routes between
all three Phoenix NPC landmarks against the hash-bound installed client/server
terrain. See `research/MYSTIC_RING_TERRAIN.json`. Terrain is not identical;
this does not establish live occupancy or native NPC interaction.

- [x] Ordinary server earning, recovery, persistence and rewards.
- [x] Saved journal projection and private client directions.
- [x] Shared terrain routes between quest NPC landmarks.
- [ ] Native mouse rendering, NPC/journal acceptance and actual Host restart.
- [ ] Deployment and signed client publication.

Achievement 10931 remains disabled. Next independent equipment quest is
Frantic Monkeys (1745); outstanding native gates remain visible above.


## Purchased medicine and earned ring through full restart (2026-10-01)

Three private process phases pass: `career-mystic-ring`, `career-mystic-claim`,
`career-mystic-reload`. Ordinary actor4294908401 buys twelve Stanchers and twelve
Agrypnotics from an installed silver shop through decoded ItemUsage purchases.
Every purchase checks the exact catalog silver debit and final quantities. Chang
accepts the errand; the installed Phoenix portal leads to Minstrel and Shen.
Shen consumes all24 owned medicines, preserving their transaction receipts.

Twenty loaded FireRatL38 deaths through production ReceiveAttack earn the hunt.
Shen issues the Castle Gate, Junior exchanges it for the Antique Ring, Shen
acknowledges the return, and Minstrel exchanges that owned item for Mystic Ring.
Each ordered stage and item identity is checked. The actor saves at stage8 and
exits abruptly while owning the ring; no final reward has been given.

Cold game/database startup preserves the exact quest, medicine receipts, profile,
owned items and all prior quest/event snapshots. Ordinary ItemHandler use beside
Minstrel consumes the saved ring and advances to9. The installed return portal
allows Chang's claim: exactly two new bound items, headgear pack728240 and the
saved P7 soul. The exact silver balance is the starting purse minus all24 shop
purchases plus2,000 from the quest. Normal EXP effects are in the saved profile.
Another cold restart retains stage10 and both recorded reward UIDs. Completion
and lost-item recovery requests after completion preserve the entire snapshot.
Checkpoint: `revora_mystic_process_probe`, done=true with snapshot and purchase cost.

No stages, medicine, kill credit, rings or final rewards are fixture-seeded.
NPC approaches and lethal damage are controlled. This does not establish native
mouse rendering, the composed journal, competitive combat or natural navigation.
The earned headgear pack remains owned/unopened; earlier pack tests retain their
separate scope. No production source/build changed, and the main retained career
is untouched. Candidate SHA256:
`05F7A80A0EC5F74ED699076019CE7FFD9F2B5B2646F7C69562FEC1A4E43BD9A6`.
Native acceptance and live deployment remain pending; no live files were changed.

## Retained medicine and reward follow-up (2026-10-02)

The retained career now completes Mystic Ring using purchased medicine, twenty
calculated kills, ordinary deliveries and item use. Four combined Artisan/Mystic
reward UIDs persist across full restart and repeat claims are unchanged. See
[the retained batch](ARTISAN_FEATHER_IMPLEMENTATION.md#retained-artisan-feather-and-mystic-ring-batch-2026-10-02)
for controls, provenance and native limitations.
