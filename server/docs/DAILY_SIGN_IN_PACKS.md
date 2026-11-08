# Leftover Daily Sign-In packs 3100011 / 3100013–3100017

M6.56b9m, 2026-09-23. Server implementation; not deployed.

Ordinary 1009/action-4 and direct `UseItem` of leftover **DailySign-inPack
3100011**, **2-daySign-inPack 3100013**, **7-daySign-inPack 3100014**,
**14-daySign-inPack 3100015**, **21-daySign-inPack 3100016** and
**28-daySign-inPack 3100017** go through `DailySignInPacks`. All six legacy
cases fail closed. Catalog-only **SeniorSign-inBox 3100012** has no UseItem
case and is not owned.

## Current Revora correction ? 2026-10-05

Owner-approved CP rewards: daily 10,000; day 2 20,000; day 21 500,000.
Days 7, 14 and 30 have no unbound CP reward. The historical amounts below
are superseded. P7 packs 3004247/3004248/3004249 are removed from day-7 and
day-14 rewards under the owner's no-daily-P7 rule. Day 7 retains its cosmetic
pack; day 14 retains five RadiantStarStones, the cosmetic pack and HousePermit.
Silver, RVP and Bound CP amounts are unchanged. Existing balances and historical
receipts are preserved; no retroactive deduction is authorized by this change.
The existing transactional pack consumption and daily claim uniqueness remain.

## Historical leftover terms (superseded)

Leftover `Add(id,0,n)` created one unbound plus-0 stack-1 catalog-durability
slot per unit. There was no leftover capacity text; a partial grant could
overfill. Owned service refuses with `Constants.FullInventory` from the actual
number of new objects and credits the source slot only when it is deleted.

| ID | Items | ConquerPoints | Silver | RevoraPoint | BoundCps | Success text |
| ---: | --- | ---: | ---: | ---: | ---: | --- |
| 3100011 | 2×723727 PenitenceAmulet, 1×3009002 RadiantStarStone | 10,000,000 | 50,000,000 | 2,000 | 0 | daily pack … everything |
| 3100013 | 1×3009003 SplendidStarStone | 20,000,000 | 100,000,000 | 0 | 0 | 2-day daily pack … everything |
| 3100014 | 720121, 3004247, 3004248, 3004249 | 0 | 0 | 0 | 500,000 | 7-day daily pack … everything |
| 3100015 | 5×3009002, 720121, 2×3004247, 2×3004248, 2×3004249, 721170 | 0 | 0 | 0 | 0 | 14-day daily pack … everything |
| 3100016 | 5×3009003 | 500,000,000 | 1,000,000,000 | 20,000 | 1,500,000 | 21-day daily pack … everything |
| 3100017 | 2100085 GoldTrophy, 5×3009003 | 0 | 0 | 0 | 0 | 30-day daily pack … the cup |

3100014 and 3100016 pay **BoundCps**, not ConquerPoints. The compound audit
previously omitted BoundCps on both IDs.

Overflow of CP, silver, RVP or BoundCps refuses without consuming. Receipt
table `revora_daily_sign_in_pack_uses`. Direct daily P7 is unchanged.

## Ordinary issuance

Startup loads `database\slayer\DayliSignIn\signin.ini` through
`SignInTable.Load`. Prizes are keyed by **needDays**: 1→3100011, 2→3100013,
7→3100014, 14→3100015, 21→3100016, **30→3100017**. There is no key 28.

`MsgSignIn` packet 3200:

- **Sign** needs level 140, second rebirth, VIP ≥5 and 4,000 Revora Points.
  Always issues `AddTime1` 3100011 (2-day duration). Milestone packs issue
  when signed-day count reaches 2/7/14/21/30. Same-day replay is gated by
  `DailySignDays[today]`.
- **LateSignIn** needs VIP 7 (VIP 6 returns immediately) and
  `DailySignVIPChances>0` plus 10,000 Revora Points. Milestones 2/7/14/21
  share the loaded prizes. After 21 days leftover LateSignIn looks up **28**,
  which is missing, so 3100017 is ordinary only on **normal Sign at 30 days**.
- **Reward** is unused.

No gold, emoney, exchange, StageGoal, shop or DropItemTable producer. 3004246
HolyStone combine is owned separately by [ANCIENT_STONE_COMBINE.md](ANCIENT_STONE_COMBINE.md).

## Remaining work

Catalog-only Chi pills 3000051/729476–729479,
catalog-only Surprise box 722178, nested/gear leftover 721222–721245,
classified silver leftovers, leftover medicine bundles 720010–720017,
classified 720654, other leftover packs, Host restart, native acceptance and
deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.


## Milestone and supplier follow-up (2026-09-30)

The sign-in request handler now shares the loaded catalog's 2/7/14/21/30
milestone ladder between normal and late sign-in. Previously late sign-in looked
up absent key 28, and normal sign-in left `next=0` after day 30, allowing another
final pack on day 31. Completed or unknown saved states now have no next reward.
The display recognizes saved day 30 as the fifth reward while retaining legacy
28-state display compatibility. Legacy day 28 is terminal, not converted into a
second final claim. This supersedes the late-sign lookup limitation above.

Paid sign-in now refuses before payment or chance mutation if the catalog lacks
any required daily/milestone prize. The current private candidate is missing
`database/slayer/DayliSignIn/signin.ini`; source presence alone therefore does
not establish a usable runtime supplier. The local `bin/Debug/signin.ini` has
all six expected keys and SHA-256
`6A70EE5CB0A84B41821523490012086EC2448348F8B86757F6B78ADC15F49341`.
A separate ignored catalog harness copied that file to the loader's expected
relative path; it did not modify the game package or live server.

Build passed with existing legacy warnings. `tools/CheckSignInMilestones.cs`
compiled with warnings as errors and checked 28/29/30/31-day month sequences,
completed/legacy/invalid saved states, every missing required prize, an unloaded
catalog and the two Easter supplier identities against compiled code. Executable
SHA-256: `BADFCC12574DB394968E16EFF25181EFCC88F06B0F5F485311C60ACF44516309`.
These checks do not exercise complete player issuance or persistence.

Remaining prerequisite: one saved-owner transaction for payment, daily state,
VIP chance, delivered packs and receipt, with full-bag/rollback/replay/restart
coverage. Existing `TrySpendRevoraPoints` and `AddTime1` results are ignored and
state is saved separately; the milestone fix does not make that path atomic.
Month rollover and concurrent save ownership must be covered by that repair.
Do not claim the combined 50-mount journey until actual issuance passes.


### Issuance transaction completed privately

The [atomic issuance follow-up](SIGN_IN_ISSUANCE.md) supersedes the split-write
and missing private-catalog prerequisites above. Seven boss-funded sign-ins,
an earned Easter mount, rollback, payment/capacity refusal and month/restart
persistence pass. This is separate from native and live deployment acceptance.
