# Chosen One: verified server earning route

2026-09-16 UTC, server `b722bd1`. The ordinary-player integration fixture now
connects existing boss rewards, Vesper purchases, rune composition, equipment,
wardrobe selection and uncached persistence reload. No production prices,
rewards, schedules, client files or live player data were changed in this batch.

## Route and exact cost

The fixture starts a qualified second-rebirth Trojan with zero CPs, RVPs, gold,
runes and materials. Eighty-three distinct scheduled daily bosses spawn on their
real maps. Their production combat damage receiver and death/payout path award
83 durable, single-payment bounties. The resulting purse is 83,000,000 CPs,
4,150,000 RVPs and 830,000,000 gold.

At Twin City (396,340), the ordinary NPC dispatcher buys Vesper's matching
Comprehension and Rampage plus three different yellow runes. All start at level
one. Existing action 61 transfers the five targets into the rune bag. Further
paid level-one runes of the same color supply the existing composition path.

| Purchase | Count | RVP unit price | RVP total |
| --- | ---: | ---: | ---: |
| Initial red rune | 1 | 1,000 | 1,000 |
| Initial blue rune | 1 | 750 | 750 |
| Initial yellow runes | 3 | 500 | 1,500 |
| Blue material runes | 3,414 | 750 | 2,560,500 |
| Yellow material runes | 3,135 | 500 | 1,567,500 |
| Total | 6,554 | | **4,131,250** |

Installed cumulative EXP is 100 at level one, 341,500 at blue level 27, and
104,600 at yellow level 9. A level-one material contributes 100. Consequently,
the blue needs 341,400 added EXP and each yellow needs 104,500. The fixture buys
and consumes every material through the production handlers in batches of at
most 40 inventory slots: 167 compositions and 167 consumed-material replays.
All replays preserve item progress and currency. Only five owned runes remain;
remaining RVPs are exactly **18,750**, in memory and durable storage.

Bagged maxima grant no equipped title or class receipt. Equipping the five
legal, distinct runes earns 11310, 11311 and Trojan 11312, and Chosen One. The
ordinary wardrobe request equips key 90001 with 500 points. Full character save,
production character/item reload, clearing the wardrobe cache and loading its
database records restore the equipped title, earned receipt and exact purse.
Repeated equip/login does not add points or charge again.

## Evidence and limits

`tests/PotencyHunt/Verify-PotencyHunt.ps1 -RuneAcquisitionOnly` passes **16 route
checks**, in addition to the unchanged build's 2,112 earlier gameplay checks and
three producer-audit tests. Its transaction-loop assertions also check every
purchase, composition and replay; these are not counted as thousands of
independent tests. The earlier rune suites cover class eligibility, rollback,
forged materials, duplicate requests and invalid-loadout recovery.

Server SHA-256:
`21ADC947B0C0819A97FBC1D56212945E2A2B7E9406AA21F4DE24B9C6E9D48085`.
The fixture uses a disposable database schema and a private status-file directory.
Time advances through 21 server dates; combat supplies scripted lethal damage
to the production receiver. It verifies source ownership and reward transitions,
not fight difficulty, player competition, travel duration or a native 6711
playthrough. Initial second rebirth is a stated prerequisite, not earned by this
fixture. All ten rune class mappings have separate admission/receipt tests.

This is Revora's existing owner-configured Vesper/boss route. Its cost is a
balance-review input: claiming all 83 bounties occupies 21 scheduled server dates
at four bosses per day. Price changes and alternative stone/essence earning
routes are separate work. Native rune-panel selection, title display and live
deployment remain pending.

## Fresh earned Warrior rune loadout (2026-10-03)

`CheckCandidateFreshRunes.cs` continues the fresh ordinary level133, twice-reborn
Warrior after all seven subclasses. It verifies the exact preceding checkpoint,
loads the five existing roster members and preserves their accumulated earning
history. No character level, rebirth, currency, rune item or achievement is seeded.

Vesper sells Pitching4010101, All-round4020201 and three distinct yellow runes
4030101/4030201/4030301 for 3,250 earned RVPs. Normal rune-bag transfer and equip
requests place the exact five purchased UIDs in slots101?105. The one-rune and
five-rune receipts11310/11311 are required once each.

Upgrades buy the highest +Stone stocked in this candidate's shop2888: +6
item730006. Each material must have its matching enhancement value, disappear
on composition, and advance the rune's type or experience. Replaying the same
composition packet must preserve the snapshot. The shop clock advances by two
seconds for intentional distinct purchases; this does not claim a natural-time
UI journey. Each successful material use saves the character/spells and records
a private checkpoint before the next purchase.

The final reload requires the five normal level caps (red1, blue27, yellow9),
one Warrior full-level receipt11313, exact loaded item type/position/experience/
binding matching the database, and the corresponding rune skill levels. It also
compares the nested career snapshot. These checks do not establish native rune
panel or Prestige display acceptance.

Candidate server SHA-256:
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
An initial fixture compilation error from ambiguous database type names was
corrected before execution. No live restart, deployment or client change.

Three earning batches and the separate fresh-process reload passed. Logged
composition material costs total 597,780 CPs:309,096 for All-round and96,228
for each yellow rune. Pitching is already at its red-rune cap on purchase.
Final saved types are4010101,4020227,4030109,4030209,4030309; each has zero
remaining rune experience. The reload confirms all five exact items and skills,
the11310/11311/11313 receipts, and unchanged nested career state.

Redacted private evidence under
`C:\Revora\DisposablePostgres\moon-box-candidate`:
`arena-process-career-fresh-rune-earn-1.log`,
`arena-process-career-fresh-rune-earn-2.log`,
`arena-process-career-fresh-rune-earn.log`, and
`arena-process-career-fresh-rune-reload.log`.
The first two earning batches were bounded to70seconds; the final batch used
180seconds. No refused upgrade or gameplay-state repair was needed.
