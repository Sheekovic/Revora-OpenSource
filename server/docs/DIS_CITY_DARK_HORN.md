# Dis City Dark Horn exchange

Baseline: `4f3a096`, 2026-09-15. M6.22 completes the server earning and direct
EXP exchange path for the selected Revora Dis City edition. Genuine 6711
presentation and deployment remain pending.

## Player path

Defeat your summoned Ultimate Pluto and claim the weekly victory from Solar
Saint. The final spoils now include one bound Dark Horn (`790001`) alongside
the existing Sacred Refinery Pack, Yin Jade, treasury key and possible Star
Sword. Return to Solar Saint in Twin City `(327,250)`, choose **Exchange a Dark
Horn**, then **Take the experience**. Right-clicking the horn explains the
destination and leaves it in the inventory.

The [official Dis City guide](https://co.99.com/guide/quests/monstercity.shtml)
identifies the horn's 300-minute EXP exchange. The official
[Everything Has A Price guide](https://co.99.com/guide/quests/2012/Everything_Has_A_Price.shtml)
equates 60 minutes to an EXP Ball. Revora awards five times the character's
current EXP Ball value as direct EXP, using the reviewed wide arithmetic and
level/restoration planner. It does not issue five inventory items or apply
kill multipliers. The server EXP curve is not certified as original CO data.

One guaranteed horn per weekly victory is Revora tuning; the guide does not
publish its precise drop probability. The existing weekly receipt prevents
repeat victories from minting more horns. Earlier completed victories are not
automatically backfilled. Already-earned, unclaimed victories use the new final
reward list when claimed. Full bags retain the complete victory until space is
available; they do not discard the horn.

## Ownership and transaction

Each issued horn is linked to the character's completed run through
`revora_dis_relics`, with a unique `(run,item_type)` and deferred completion
foreign key. Its bag row, final victory, stage achievement and issuance commit
together. A lookalike horn without that receipt cannot be exchanged.

Solar Saint offers one exact eligible item UID for one minute. The claim
requires the ordinary canonical loaded player, proximity, no trade/booth,
an unlocked, permanent, single bound bag item, and its unused owned issuance.
Maximum-level and cursed characters keep the horn. The confirmation is consumed
after commit; replay cannot silently spend the next earned horn.

Under the character owner lock and mentor lock, one database transaction:

1. Locks the character row and rechecks the exact item/issuance ownership.
2. Plans the complete five-ball-equivalent EXP reward.
3. Consumes the horn and marks its issuance used.
4. Saves the complete progression, class skill, initial Arena enrollment,
   applicable mentor contribution and reincarnation snapshot consumption.
5. Inserts the unique `revora_dis_horn_claims` receipt with awarded EXP and
   before/after levels, then commits.

Client inventory, skill, level/stat and EXP projections follow the commit.
Transaction failure preserves the horn and reward entitlement. Post-commit
projection failure fences the character for reload instead of attempting a
second reward. The claim fits a full bag because it removes one item and adds
direct EXP. Normal saves share the same owner lock and saved-profile comparison.

## Validation and limits

The isolated EXP/progression suite passes 228 checks, including 15 Dark Horn
exchange checks: ordinary full-bag use, failed final receipt rolling back all
effects, mentor credit, class skill, production persistence readers, concurrent
saves/claims, forged/expired offers, replay against the next horn, unissued and
locked items, maximum level, curse, distance, changed item/character ownership,
and a level-110 reincarnation restoration within the exchange.

The actual four-stage dungeon suite passes 142 checks. It verifies the horn's
issuance from the owned summoned Pluto victory and full-bag/failure behavior.
The focused exchange scenarios seed completed historical runs through the
production issuer; they do not each represent another full dungeon traversal.
The genuine-client panel, full login/reconnect session and native EXP display
still need acceptance. The historical mentor duplicates documented in
[M6.21](MENTOR_EXPERIENCE_TRANSACTIONS.md) remain a deployment prerequisite.

All 1,408 other quest/event regression checks pass. The isolated Debug/x86 build
passes with assembly SHA-256
`027679F06DE246F949BD36ABA9210ACE404DD7C1879C1F65E1F47F62379EEAF2`.

No new protocol fields are inferred. The exchange uses existing NPC options,
item removal and EXP/stat packets. Prior roadmap protocol confidence and
client gates remain in force. Difficulty variants and next-day ranking rewards
are separate unfinished Dis City scope.

## Private content stage

`tools/stage_dis_city_overlay.py` now includes the Dark Horn tooltip, final
mission reward and quest explanation. It preserves the previous Summer,
Labyrinth and Dis City content, changes only the 11 reviewed item tooltips and
four quest records, verifies the source hashes and refuses overlapping or
existing destinations. Twelve overlay and three producer-audit tests pass.

Stage: `obj/chi-refill-validation/dis-city-horn-overlay`.

| Output | SHA-256 |
| --- | --- |
| `ini/itemtype.dat` | `96f0596b854f2e3ff9df6007317c5272fb9d08329995c6dc3357d33addc86f1d` |
| `ini/Questinfo.ini` | `be814ad8256a582350ddde925b35bae05f3b2bff0a649f9783ac86ff418b5b9c` |

This is an unsigned private review artifact. No installed client, live server
binary, public patch or website has been changed. The source earning inventory
remains 81/337; this closes the horn reward path, not another achievement ID.

## Fresh earned Horn exchange and cold persistence (2026-10-03)

The fresh ordinary career exchanges the actual Dark Horn790001 earned from its
four-stage Pluto victory. Solar Saint's offer/confirmation consumes that exact
item and marks its issued relic receipt used. The saved claim records precisely
five times the character's pre-exchange EXP-ball amount:3,049,385,790 EXP.
The character remains level130; this is not a claim of a level increase.
Repeated confirmation adds nothing, and a separate process retains the exact
resulting profile, consumed item, claim and nested prior career.

Both phases in the [fresh Treasury batch](DIS_CITY_TREASURY.md#fresh-earned-key-and-reward-opening-2026-10-03)
pass. This supersedes the earlier retained max-level refusal only for this
below-cap exchange path. Clock/NPC approach are controlled. Native EXP display,
live acceptance and deployment remain pending.
