# Owned Elite PK prize pack transactions

M6.56b1, 2026-09-16, baseline `0919408`. This completes consumption of existing
owned packs 720714 through 720729. It does not create tournament placement,
claim or honor eligibility.

## Preserved bundle contract

The baseline `Network/GamePackets/ItemHandler.cs` is the evidence for these
Revora bundles. No exact official 6711 economy parity is claimed.

| Pack family | IDs, ascending by placement reward | EXP | CPs |
| --- | --- | --- | --- |
| First lower family | 720714, 720715, 720716, 720717 | 15%, 30%, 50%, 100% | None |
| Second lower family | 720718, 720719, 720720, 720721 | 15%, 30%, 50%, 100% | None |
| Third lower family | 720722, 720723, 720724, 720725 | 15%, 30%, 50%, 100% | None |
| Highest family | 720726, 720727, 720728, 720729 | No direct EXP | 3M, 5M, 5M, 5M |

EXP uses the current level curve and preserves the old integer rounding:
`curve / 100 * 15`, `curve / 10 * 3`, `curve / 2` or `curve`.
At the configured level cap, the character can still open an EXP prize for its
item bundle; new EXP is zero, preserving existing maximum-level entitlement.
Standard EXP Ball daily uses are not charged for these direct rewards.

| Lower-family placement | Exact items |
| --- | --- |
| Fourth through eighth | 723341 x1, 729626 x1 |
| Third | 723341 x2, 729626 x1, 720731 x1 |
| Second | 723341 x3, 729626 x1, 720731 x2 |
| Champion | 1088000 x1, 723341 x5, 729626 x1, 720731 x3 |

| Highest-family placement | Exact items |
| --- | --- |
| Fourth through eighth | Bound 723700 x3, 720731 x1, 729626 x1, 720598 x1, 723341 x1 |
| Third | Bound 723700 x5, 720731 x2, 729626 x2, 720598 x1, 723342 x1 |
| Second | Bound 720730 x1, 720731 x3, 729626 x3, 1088000 x1, 720598 x2, 723342 x1 |
| Champion | 720049 x1, bound 720730 x3, 720731 x5, 729626 x5, 1088000 x3, 720598 x3, 723342 x3 |

A bound source pack now preserves binding on every resulting item. The existing
forced-bound outputs remain bound even from an unbound source pack. CP rewards
and the legacy direct-EXP mentor source rule remain unchanged. No pack can
produce a title or achievement merely by being possessed or opened.

## Complete transaction

`ElitePkPacks.TryUse` replaces the sixteen split-write switch branches. Ordinary
canonical, fully loaded players can use the service. Owner and mentor locks
serialize item use with progression and autosaves. Character/profile or CP purse
preimages, the complete bag UID set and the selected pack's durable ownership,
stack, bound, lock, suspicion, wardrobe and expiry metadata are checked.

One transaction consumes one exact pack unit, writes every reward item, persists
the progression/CP change, commits eligible skills/Arena/mentor effects and
consumes any pending rebirth recovery receipt. A durable opening receipt records
source UID/type/stack, character, awarded EXP/CP and every new item UID.
Silent failed inserts are checked individually, including failures partway
through the bundle. Failure rolls the entire transaction back and fences the
session for a normal reload; successful cache changes and existing client
updates happen after commit. Stack zero retains the legacy singleton meaning.

Capacity reflects the actual number of reward items and a singleton's freed
slot. For example, the 23-item champion bundle fits a bag with 18 entries,
including its source pack. A stacked source needs all 23 empty slots because
its source entry remains. Insufficient space or CP overflow preserves the pack.
Blocked EXP conditions preserve an EXP prize; dead or trading players cannot
spend any prize. No new item UIDs are allocated for normal capacity/CP denials.

## Earning-path audit and outstanding work

`ElitePkResults.Finish` verifies contested results and grants the highest-bracket
honor. It does not issue the historical prize packs. `ElitePkEvent` currently has
no placement-prize claim route. Completing owned-pack use must not be checked off
as a complete battle-to-prize journey. A saved placement contract, including
third-place handling, eligible lower-bracket results and claim replay protection,
is still needed before connecting these bundles to tournament results.

The later [nested pack milestone](ELITE_NESTED_PACK_TRANSACTIONS.md) replaces
legacy openings for 720049, 720731 and 729626 and validates the supplied
champion bundle through all eleven nested gifts, reload and refinery use.
It also corrects class-based refinery selection and excludes a client-undefined
accessory. Tournament placement-to-prize admission remains unfinished.

The same source audit found active Magnolia seed cases 729307/729310. Their
planting writes split the daily flag, seed removal, Chi token, CP/RVP, EXP and
study points. `DailyQuestEnvoySystem.PayDaily` similarly splits those reward
components for Price Hunt, rare-material and Soul Ball paths; paid skips add
separate cost writes. Release Souls has its own earlier interception and is not
mistaken for these legacy handlers. M6.56b2 retains the remaining daily work.

## Validation

The focused suite includes 392 first-rebirth prerequisite checks and 106 pack
checks: all 16 identities bound/unbound; exact amounts and item multisets;
production reconnect; stack/singleton replay; concurrent autosaves; exact bag
capacity; overflow/stale purse; hidden/missing inventory rows; item metadata;
partial reward insert and source/receipt/profile/skill/recovery failures;
maximum-level entitlement; normal restrictions; mentor credit rollback/retry.
Supplies and mentor relationships are fixture inputs. No tournament prize earning
journey, native 6711 display acceptance or live deployment is claimed.

Final validation: Debug x86 build; prize packs 498, experience 452, EXP items
456, Elite PK 78, progression 74, titles 91 and default 216 (1,865 gameplay
checks), plus three achievement audits. Producer inventory stays 219 present /
118 absent. Local logs: `obj/chi-refill-validation/prize-packs*.log`. Server
SHA-256: `EEE6FF53295F776F85653416A07026948915162B7735C9406C5102816DE2F07A`.
No live server restart, patch publication or website update occurred.

2026-09-16 claim follow-up: [Earned Elite prize claims](ELITE_PRIZE_CLAIMS.md) now deliver the existing bundle contents directly and apply signup-level EXP at the Envoy. Independently owned outer packs retain the behavior tested above.
