# Second rebirth: requirements, receipt and transition contract

Date: 2026-09-16. M6.55c1 and M6.55c2. The ordinary token purchase, second-rebirth
rite and exact EXP recovery are implemented and fixture-validated. Not deployed;
genuine 6711 visual/combat acceptance and the separate Abyss quest remain open.

## Confirmed requirements and attribute budget

The official [requirements](https://co.99.com/guide/quests/2rebornrqm.shtml)
require level 120 and one prior rebirth. The HTML contains this rule even though
the browser text extractor omits its main table. Water's first-rebirth level-110
exception does not apply to second rebirth. Revora's existing highest-promotion
requirement remains part of its admission policy.

The official [advantages](https://co.99.com/guide/quests/2rebornadv.shtml)
define level 15, equipment level reduction, Bless, and restoration of former
level/EXP upon returning to level 130. Its attribute formula gives 82 base/system
points plus the first- and second-life bonuses at level 15. Each bonus caps at
55: a double-130 character therefore has 192 total points. Water's bonus advances
every two levels above 110; other classes advance above 120. The existing
interpolation for unlisted intervening Water levels is retained as an inference.

`SecondRebirthRules` now plans this complete total while preserving the installed
class distribution, both historical class/level pairs, and the level-15/zero-EXP
new profile. It rejects invalid histories, unpromoted/unknown current classes,
unsupported destinations and Water second rebirth below 120. It has no item,
database, packet or character mutation. The owned rite below now uses this
planner inside its complete payment/progression transaction.

The legacy administrative `RebornCore` adds its free points on top of a separately loaded class
distribution. It creates gifts and lowers equipment before recording the new
life and saves skills/proficiencies separately. The former ordinary NPC also
removed the token afterward. That ordinary branch has been removed: Sereth
now calls `SecondRebirth`, and administrative rebirth does not create its trusted
receipt or quest evidence. Paid reincarnation is now separately implemented and
fixture-validated under [M6.55c4](PAID_REINCARNATION.md).

## Token, achievement and skill identities

The [quest guide](https://co.99.com/guide/quests/2reborn.shtml) explicitly allows
an Exemption Token to bypass the three-stage quest. Installed 6711 facts:

| Identity | Meaning and implication |
| --- | --- |
| Item 723701 | Exemption Token; substitutes for quest completion, not level eligibility |
| Achievement 10316 | Second rebirth; earn from the committed rite |
| Achievement 11031 | Complete the Second Rebirth quest; a token must not grant it |
| Logical spell 9876 | Bless; catalog row ID is 10300, so matching only the first column misses it |
| Logical spells 5000 / 5002 | Freezing Arrow / Poisonous Arrows; both exist in active client and loaded server catalogs |
| Logical spell 12830 | Frost Gaze I; active Windwalker cross-class identity |

The installed CP mall lists 723701. Its primary/client catalog price is 999 CP,
while the loaded server `ItemtypeSub.txt` override is 888 CP. `SafeDictionary.Add`
replaces the earlier entry, so the active server price is 888. Preserve the
configured server economy in the protected purchase path. The native
price-display discrepancy remains a client acceptance/patch gate.
This is a Revora price, not a claim of original CO parity.

## Skill evidence must not be imported blindly

The official [second-rebirth matrix](https://co.99.com/guide/quests/2rebornskill1.shtml)
contains 81 two-life headings and **729 third-class rows / 728 unique triples**.
One combination has conflicting duplicate rows; one is absent:

- Archer / Pirate / Archer is listed twice with different retention.
- Archer / Pirate / Trojan is missing.

Correction to M6.55c1: Water / Ninja / Monk and / Pirate are present in a second
sibling DIV. The original extraction stopped too early. The generator now scopes
each heading to the next heading/table boundary and tests sibling blocks.

`tools/build_second_rebirth_matrix.py` is offline and hash-gated. Its committed
CSV contains numeric retention facts only: 727 published rows and two explicitly
labeled Revora inferences. Those two Archer/Pirate routes retain previously
learned Poisonous Arrows and Gale Bomb; they do not create either missing art.
The 271 Windwalker triples use an explicit composition policy from the older
matrix and separate Windwalker guide. This is **not verified retail parity**.

Other rows contain spelling and punctuation defects. These structural findings
are from the archived official HTML, not a claim that every skill outcome was
verified. The 271 triples involving Windwalker are outside that nine-class
matrix. The separate [Windwalker guide](https://co.99.com/guide/windwalker_rebirth_skills.shtml)
confirms Frost Gaze I inheritance and class-to-Windwalker retention, but is not a
complete three-life table. Resolve or explicitly identify inferred transitions;
do not convert duplicate keys into silently authoritative last-row wins.

The first-rebirth helper is not sufficient for second rebirth: a third life must
consider both historical schools, keep only earned eligible skills, reset their
levels while retaining recovery records, and grant valid new rebirth arts.
Pure-class skills require all three class identities to agree. Rune-derived
skills must be rebuilt through the existing loadout rules. Archer's two logical
spell IDs above are now established; their precise first-/second-life granting
granting now have planner and ordinary-rite fixtures. Native skill use, level/weapon
restrictions, instructor parity and any unclassified class arts still require a
separate combat/learning audit.

## Implemented ordinary path (M6.55c2)

`SecondRebirth` handles Sereth 59558 options 2/9, class choice 170..179, then
review/confirmation 180. Offers belong to one canonical authenticated session,
expire after two minutes, and are invalidated by cancellation or changed
belongings/context. The short game-world dialogue names the cost and new class.
Every eligible ordinary player can use it; there is no GM or owner allowlist.

One owner-locked database transaction verifies full owned bag/equipment,
skill and proficiency snapshots, consumes the selected unlocked permanent
Exemption Token, lowers both equipment sets, returns both off-hands, saves the
complete new profile and learned-skill reset, creates supported weapon gifts,
and inserts `revora_second_rebirths` plus achievement **10316**. Suppressed writes
and changed preimages roll back everything. Cache updates and normal skill,
item, attribute and achievement packets occur only after commit. Projection
failure fences the session for reconnect. No token route grants **11031**.

The receipt preserves both old class/level pairs and the second life's exact
EXP. `SecondRebirthRecovery` consumes it atomically with the first award that
reaches **130**, restores all intervening attribute points and full former EXP,
and disables the legacy level-only shortcut. Duplicate awards and reloads
cannot replay it. A consumed receipt remains valid after later reincarnation
changes class history. Pending second-life recovery must finish before the
existing paid-reincarnation path can replace that life. Paid recovery
retains its separate **110** threshold; the complete payment/profile transaction
and repeatable exact recovery are implemented under [M6.55c4](PAID_REINCARNATION.md).

`ExemptionTokenShop` protects the existing 1009 purchase at loaded CP-mall
stock. Shop 2888 retains its existing remote-mall behavior; other loaded stock
IDs require their nearby canonical NPC. It checks the selected balance and
exact bag UID set, inserts all tokens and a durable receipt, and debits once in
one transaction. Quantity 0 means one; 1..40 is bounded by capacity. Identical
purchases within one second are ignored even across reconnects. Later deliberate
purchases remain possible. The configured 888-CP price is read from the loaded
catalog. Bound-CP selection creates bound tokens, preserving binding through
the rite's gift. This also closes the legacy bound-CP-to-unbound-item behavior
for this item. No client price file was modified.

The skill planner resets retained learned levels/EXP and preserves previous
levels and soul metadata. Pure-class arts require all three schools to agree;
Pure Pirate receives catalog-backed Scurvy Bomb 11040. Kraken's Revenge 11100
has the other-school/Pirate/Pirate retention gate; its existing trainer remains
the learning path, outside this transaction's acceptance claim. Pirate, Ninja,
Monk, Dragon and Windwalker receive no unverified replacement weapon. Archer's
missing first-rite Freezing Arrow / Poisonous Arrows grants are persisted so
first-life ownership can feed the second rite. Their missing passive combat
path remains open in [the skill audit](REBIRTH_SKILL_AUDIT.md); official
[first-rebirth guidance](https://co.99.com/guide/event/2013/rebirth/rebirth-1-4.shtml)
and [Pirate skill requirements](https://co.99.com/guide/skills/m_pirate.shtml)
were rechecked. Pure Windwalker entitlement is not invented from the older
nine-school table. Existing rune eligibility/loadout rebuilding still applies.

## Remaining acceptance and next work

- The separate Abyss NPC/material/combat route and proof-funded rite are now
  implemented in source and isolated validation ([M6.55c3 details](ABYSS_TRIAL_RESTORATION.md)).
  PvP item protection and native boss-profile loading are validated. Rendered
  client acceptance and runtime activation remain open.
- Paid reincarnation now conserves its attribute budget and commits payment,
  skills, equipment and recovery together; native acceptance remains open.
- Resolve the two conflicting/missing Archer/Pirate source rows and full
  Windwalker three-life parity; review native skill use and instructors.
- Verify 6711 menu, skill, attribute/BP and shop-price display with the genuine
  client, then stage an approved deployment. The current live server is unchanged.

The quest route is substantial independent work, not equivalent to a token
purchase. The official guide describes three Pure Vigors, a 40,000-meter hunt,
ordered soul rounds and a chained Satan encounter. Installed actors, terrain,
materials, monster families and all ownership/payment boundaries must be
verified before restoring it. Do not infer quest completion from a reborn flag.

## Provenance and validation

Public HTML was retrieved read-only into ignored
`research/isolated/second-rebirth-6711/` under the Revora workspace. No raw client
catalog or third-party implementation is committed or executed.

| Evidence | SHA-256 |
| --- | --- |
| Official requirements HTML | `86ABF6C1BD85304BDB5CC8C22DB183B639CD7CAB4775584901F44DA14B9B7DC4` |
| Official nine-class skill matrix HTML | `8ED88353F6F06289465D3062A756A1139A944E15B728CE70C5EFC50A9FD466EB` |
| Active client itemtype.dat | `D49F0658BF01387B99C284DDEC81713838F2EAE3ADB8E63666E5C906F63CF87A` |
| Active client magictype.dat | `618431607E8DB81F5E8C6093D801329B73BD9A5AF4AA57FC8A38348A832173C2` |
| Loaded primary item catalog | `1ED7A23B7A34B5E3706277F97E421E3CDF0B269EEB5BE7367A4EBC0E4F22B3EF` |
| Loaded item override | `6E318B1E53B0FB28036BD547B5CB78914E3E66B1272D8054259B6B0BCAA66AEA` |
| Loaded spell catalog | `97ECB413566F5FEA541090D1A1C1498ED2ACF0874EFA6056984583C65C187A71` |
| Loaded CP mall stock | `3549E60A10E4391508F6731932850102565B8A3239EA60B241975394EBBDA3C9` |

The active catalogs were decoded read-only with a verified encode/decode round
trip, and only the selected numeric identities were inspected. Achievement
catalog hash remains `DF78DD498353B0B089F9B4228CADA5D79F4547AAE7B705349816671B2C29EBFC`.

The isolated build and final consolidated validation pass: All database
fixtures use the disposable `revora_potency_validation` schema, never live
character writes. Profile level boundaries and the return-scroll item are
fixture setup; the quest reward, actual token purchase, rite, recovery receipt
and production reloads execute the real ordinary-player paths.


| Fixture | Passed checks |
| --- | ---: |
| `-SecondRebirthOnly`: first-rite prerequisite, complete profile rules, all 1,000 skill-history gates, actual second rites, faults and recovery | 1,826 |
| `-SecondTokenShopOnly`: ordinary quest prerequisite, CP/bound-CP purchase, malformed/replayed requests and earned-CP-to-recovery journey | 535 |
| `-FirstRecoveryOnly`: first-life exact recovery regression | 444 |
| `-ExperienceOnly`: ordinary EXP, paid reincarnation and existing purchase regressions | 452 |

The 3,257 fixture checks include repeated prerequisite checks, not 3,257 unique
scenarios. Five offline matrix tests and three achievement-audit tests also
pass. The hash-gated matrix regeneration matches the committed numeric CSV.
The corrected producer inventory is **223/337 present, 114 missing**; this is
still an upper bound, with at least 97 additional identities needed for 320.

Ignored logs: `obj/chi-refill-validation/second-rebirth.log`,
`second-token-shop.log`, `first-recovery-second-regression.log`, and
`experience-second-regression.log` in the same directory.
Staged server SHA-256:
`6C836952BBF4F3243C9CC156584CBD2FC1B347414114E5C656F4F76FB812C3BB`.
Final fixture executable SHA-256:
`958720967EEDBE2E305B0E8655272BE54F69FDDCDB63CDC9C6A3B3CE24E49B5D`.
No live database, server process, client patch, website or release was changed.

## Valuable final-rite reward (2026-09-21)

Every newly confirmed second-rebirth rite now grants one unbound catalog P7 soul,
including earned Abyss completion and the existing Exemption Token alternative.
This is the final rite at Sereth, not a gift for intermediate summons, material
claims or an unowned final-monster death. Original class weapon gifts and their
binding remain unchanged. Classes without a rebirth weapon still receive the soul.

The nullable unique `revora_second_rebirths.soul_uid` stores the new item identity;
`reward_item` retains its original weapon-only meaning (zero for no weapon).
The soul, character transition, skills, proficiencies, equipment moves, consumed
Abyss entitlement/token and achievement evidence commit in the same transaction.
Capacity includes the soul and actual off-hand moves after consumed token space.
A failed soul write rolls back the entire rite. The existing character-primary
receipt and rebirth eligibility prevent repeat claims after reconnect.

Sereth's final confirmation announces the soul. Offers are transient and must be
confirmed in the same live session; historical completed receipts stay unchanged,
with null soul IDs and no retroactive award. This does not alter the earlier
Abyss stages or pay any completed historic rite again. Genuine-client reward
presentation and live deployment remain open.

Isolated build, 1,832 second-rebirth checks and 1,788 Abyss quest checks pass.
The latter uses the owned combat/NPC route and checks both unbound and bound
Abyss offerings. Coverage includes soul UID/binding after reload, concurrent
confirmations, exact capacity, soul-insert rollback/retry, and existing skill,
equipment and level-recovery regressions. Logs: `second-rebirth-p7.log` and
`abyss-p7-rite.log` under ignored `obj/chi-refill-validation/`.
