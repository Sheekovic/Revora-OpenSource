# Horn of Summer: Revora earning contract

Checkpoint: 2026-09-15 UTC. The earning route, crafting, dye, unbinding, direct
trading and permanent activation are implemented. Sixty-two isolated Summer
checks pass. Real-client acceptance and deployment remain pending.

The isolated Debug/x86 build passes; staged executable SHA-256 is
`AB091802CD8B2A25C7AE98290F44EDCF1EB38FEE805E2331665AB36BED886181`.
An additional 550 quest, event, progression and wardrobe regressions pass.

## Installed identities

The approved 6711 `ini/itemtype.dat` and the server's active
`bin/Debug/database/itemtype.txt` contain all sixteen identities below. The client
descriptions establish the recipe, binding and wardrobe activation functions;
the server parser supplies duration and stacking fields. Client wardrobe keys are
from the previously audited title/wing catalog.

| Function | Bound item | Unbound item | Wardrobe key |
| --- | --- | --- | --- |
| Glow fragment | 3303704 | Not used | — |
| Siren fragment | 3303705 | Not used | — |
| Rainbow Wand fragment | 3303706 | Not used | — |
| Wings Unbind Ingredient | 3303707 | Not used | — |
| Magic Rainbow Wand | 3303764 | Not used | — |
| Wings Unbind Solution | 3303765 | Not used | — |
| Emerald Glow Wings | 3303772 | 3303797 | 60136013 |
| Orange Glow Wings | 3303773 | 3303798 | 60146014 |
| Flame Glow Wings | 3303774 | 3303799 | 60156015 |
| Bright Glow Wings | 3303775 | 3303800 | 60166016 |
| Siren Song | 3303776 | 3303801 | 60176017 |

Hashes:

- Client itemtype: `d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`.
- Server itemtype: `594ffe5f252c9445a6087004372519a6f150f089c7bff42d4ec30400911a0e88`.

All sixteen installed server rows have duration zero and a stack field of 10,000.
Wing packages require individual identity through crafting, dye, unbinding,
transfer and activation; do not let generic stacking erase their receipts.
The installed tooltips contain an August 3 conversion deadline, whereas the
historical web guide names August 2. Neither old date defines Revora's season.
A private client overlay updates all sixteen descriptions and sets the twelve
package/tool stack limits to one. Material stacks remain 10,000. The reviewed
stage is unsigned and not installed; originals and public downloads stay intact.

## Recipe evidence

The [official July 2017 exchange](https://co.99.com/guide/event/2017/july/cross-server_team_pk_tournament/index.shtml),
read 2026-09-15 UTC, establishes: 30 Glow fragments for a random permanent bound
Glow color; 99 Siren fragments for permanent bound Siren Song; 10 Rainbow fragments
for a dye wand; five unbind ingredients for one solution. The wand changes a Glow
package's color. A solution unbinds Glow with certainty or Siren with a one-in-six
chance. The installed item descriptions agree on these functions. They identify
the wing packages as items activated into the wardrobe.

## Selected Revora edition

Liora, the summer artisan near Twin City **(460,281)**, offers a personal **60-Pacific-calendar-day** season
starting at the first daily hunt enrollment. This makes the whole collection
available to an eligible player who joins after other players' seasons began.
The immutable start/end and rule version belong to that character's season record.
An expired season can be renewed; previously earned materials, completed claims,
packages and wardrobe unlocks remain usable. No historical Chi conversion applies.

At level 120+, accept one daily hunt for 120 monsters from a published rotating
family: Birdmen on Bird Island, Tomb Bats, Bull Monsters or Red Devils in Mystic
Castle. Existing corpse ownership and duplicate-death protection must attest to
the kill. Only the canonical nearby player's direct kills count; ambient fake
adventurers and teammate-only credit do not. Existing owned-player attack paths
that name the real character as killer remain valid. This policy must be tested.

The completed hunt delivers six bound Glow fragments, three bound Siren fragments,
one bound Rainbow fragment and one bound unbind ingredient, once per Pacific day.
An optional rotating restored quest (Release the Souls, Demon Exterminators or a
Power Arena floor claim) may add two Glow and one Siren fragment once that day.
Its saved legitimate completion is required; paid soul assistance does not count.
No Potency pack, purchase, CP conversion or global monster-drop faucet supplies
these materials. No CP, RVP or gold purse is added by the summer route.

The base route supplies 99 Siren fragments in 33 completed days. In that time it
also supplies enough Glow fragments for at least four crafts and three dye wands,
so even repeated random colors can be changed before activation to complete all
four Glow designs. Bonus tasks only shorten the collection time. These are
material bounds were exercised through 3,960 actual owned-monster deaths and
33 completed daily claims, followed by all five wardrobe activations.

Crafting previews exact costs and the random-color outcome before confirmation.
Each craft records its random result and produced item UID atomically with the
material debit. Use a uniform four-color draw, preserving duplicates as real
packages. Dye targets a chosen unactivated Glow package and a chosen different
color. Unbinding consumes one solution, records its roll and updates that same
package only on success. No activated wardrobe design is destroyed by dyeing.

Activation consumes one owned, legitimately issued package and grants the matching
permanent wardrobe evidence. A legitimately traded unbound package may be activated
by its new owner; its crafting history travels with its item identity. Activating
an already-owned design must preserve the package and explain the available dye
or transfer options. Post-commit wardrobe refresh and ordinary reload are required.

## Persistence and delivery

`revora_summer_seasons` owns the personal calendar. `revora_summer_daily` owns
admission, direct kill count, base claim and optional claim. Its bonus checks
existing completed Demon/Souls receipts or a Power Arena floor receipt on the
matching Pacific day; the `paid` Souls state is excluded.

`revora_summer_crafts` records each physical package/tool UID, original and current
type, random result and consumed state. `revora_summer_operations` records dye,
unbind and activation results. Every material debit, output, operation and award
commits in its owning transaction. Confirmations last two minutes and belong to
the current character session. Storage failures fence further actions until reload.

Bound materials and tools remain with their owner. Unbound packages can be
exchanged through normal reciprocal player trades; their immutable craft history
continues with the same UID. The direct trade prerequisite now commits offered
items and both gold/CP purses with one `revora_trade_receipts` row under stable
ordered owner locks. Changed offers close both tables after acceptance.

Summer packages are excluded from the old booth sale/regeneration paths because
those paths debit and transfer independently. The NPC and booth message directs
players to direct trading. This milestone does not certify ordinary booth sales
or the entire legacy currency system.

## Automated evidence and client stage

`tests/PotencyHunt/Verify-PotencyHunt.ps1 -SummerOnly` passes 62 checks, including:

- The complete ordinary 33-day collection with the worst four identical color draws.
- Daily/season boundaries, counter caps, distant/fake/duplicate death rejection,
  reconnect, full bags, concurrent claims, stale quantities and transaction failure.
- All four colors through three earned dye wands, Siren, failed and successful
  unbinding, duplicate-preserving activation and permanent wardrobe reconciliation.
- Genuine Soul inspections earning a bonus, concurrent bonus replay protection
  and a genuinely paid Soul shortcut earning no Summer bonus.
- Ordinary direct trade through the existing packet entry point, opposite simultaneous
  accepts, both-way items, gold/CP persistence, stale offers, malformed buffers,
  failures before and after item movement, replay and buyer activation after reload.
- Rejection of both newly listed and stale/automated Summer booth offers.

The existing Demon and Power completion producers retain their own route suites;
the Summer end-to-end bonus fixture exercises the Souls branch.

`tools/stage_summer_overlay.py` reads only the reviewed client itemtype hash,
changes fields 53 (description) and 46 (identity stack limit), verifies encryption
round trips and writes a new separate private directory. Four Python checks verify
the transform, exact field scope, incomplete/duplicate catalogs and path/hash refusal.
The generated client file remains ignored and is not committed or distributed.

Private stage: `obj/chi-refill-validation/summer-overlay/ini/itemtype.dat`.
SHA-256: `c488ace14dffae62a6cebc95fcd48a9947a4accba5d129800d3e25c01cd6c210`.
Its unsigned `review.json` records pre/post row hashes and changed fields.

Real-client NPC text, trading UI, stack presentation, tooltips, package use and
wardrobe visuals still require acceptance in the genuine 6711 client. Manual
signing, live installation and public release are separate pending steps.

## Guaranteed daily combat rune (2026-09-21)

New daily hunt admissions freeze `valuable_reward=true` and award one unbound
level-one Tortoise Breaker yellow combat rune (4030101) alongside the existing
6/3/1/1 fragment bundles after 120 owned monster deaths. It is an existing Vesper
combat-rune identity, not a quest item whose name happens to contain Rune.
Optional-bonus fragments and wing crafting/activation rules are unchanged.
Existing daily rows migrate with false and retain their original fragment payout.

The rune UID joins `reward_ids` in the same transaction as stack changes and
claim completion. Exact capacity includes new material stacks plus the rune;
claim update must affect one row. The ordinary rune-bag handler accepts the
reward without changing its unbound state. No historic claim is reopened.

Read-only server/client item inspection confirms matching ID 4030101 and numeric
class/proficiency/level/gender fields (all zero); the installed client name is
TortoiseBreaker(+1). The server name contains an older encoding discrepancy, so
this is not claimed as byte-identical name evidence. The NPC uses explicit
Tortoise Breaker reward wording; no client catalog is rewritten by this batch.

The Summer fixture now gives its previously zero-vitality level-120 character
a viable vitality profile before rune admission, and uses the complete isolated
entity schema required by the ordinary Release Souls bonus fixture. These are
fixture setup changes, not health grants or production schema replacements.
An older-contract fixture earns and replays the original fragment-only claim.
The follow-up fixture earns a rune on an ordinary second-rebirth character,
equips it through action 57 at slot 103, then uses fresh character/item loaders
to restore its UID, position and unbound identity. Claim replay after reconnect
gives no second rune. Second rebirth is fixture eligibility, not an earned rite
within this Summer test. The isolated schema includes the normal login tables.
Native display and live deployment remain open.

The completed earning fixture stores each of 33 awarded runes through action 61,
checks their unbound identity against saved daily receipt UIDs, and retains the
original 3,960 owned-monster deaths, material quantities and five-design crafting
route. The exact-capacity fixture rejects four free slots for five outputs; the
old false-marker contract earns its original four bundles once. The isolated
full entity copy restores zero currency defaults for the existing trade fixture.
The initial 137 checks and subsequent 142-check equipment/reconnect suite pass. Log: `obj/chi-refill-validation/summer-rune.log` (ignored). Native rune display,
and live deployment remain separate gates.
Follow-up log: `obj/chi-refill-validation/summer-rune-reload.log` (ignored).
