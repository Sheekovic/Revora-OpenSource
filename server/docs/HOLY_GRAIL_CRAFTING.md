# Earned Holy Grail crafting and wardrobe rewards (M7.3)

GuildDirector 10003 in Twin City (317,321) now offers **Forge a Holy Grail**
under **Guild War honors**. An ordinary player may exchange twelve permanent,
bound, unlocked Gold Trophies earned through recorded Guild War seasons for one
permanent bound Holy Grail, Invincible title and Supreme wings. There is no CP
charge. The trophies must be in the player's inventory.

The recipe follows the [installed and official research](HOLY_GRAIL_RESEARCH.md).
Invincible uses wardrobe key 20342034 and Supreme uses 60116011. Each contributes
500 points. These are permanent crafting awards; owning or equipping a purchased
Grail, or collecting loose legacy trophies, does not establish the requirement.

## Full earning path

1. An ordinary guild wins three recorded Sunday rounds in one five-week season.
2. Its current leader claims each round's Lord Token within the claim window.
3. The token owner exchanges three earned tokens for Gold Prize and 20,000 CPs.
4. At the season boundary the exact recorded prize becomes a permanent trophy.
5. After earning twelve such trophies, their owner confirms the Grail recipe.

Leadership may transfer after token claim or trophy acquisition. The earned
items and their owner receipts stay with the claimant; crafting does not require
current leadership, GM status or a special account. The Revora calendar retains
its existing Sunday hour and groups five rounds; it does not reproduce historic
official event dates. Twelve trophies require twelve successful seasons.

## Transaction and replay protection

The two-minute confirmation retains twelve specific runtime objects, item UIDs
and prize receipts. Confirmation rechecks proximity, the canonical player,
inventory preimages and the exact earned rollover provenance. Timed, locked,
unbound, suspicious, missing or already consumed materials cannot qualify.

One transaction deletes the exact twelve trophy rows, inserts the Grail,
records the craft and each consumed prize, and inserts both permanent wardrobe
receipts. Every required write must affect one row. Unique material ancestry
prevents the same earned trophy from entering another craft. Distinct `:title`
and `:wings` receipt keys avoid colliding with the award ledger's source-key
uniqueness constraint. A failure on the second grant rolls back the first too.

Consumption frees twelve slots before one item is delivered, so a full 40-slot
bag containing the materials can finish with 29 items. No material or CP balance
is lost to an unsuccessful confirmation. Persistence failures fence the session
for reload; committed item/award records remain the source for recovery.

After commit the ordinary inventory and wardrobe reconciliation paths publish
the result. Both designs can be equipped through the existing wardrobe handler.
The wardrobe clock defaults to UTC and is injectable for deterministic tests of
the long seasonal route; production timing is unchanged.

## Validation

Final staged build SHA-256:
`98E093B19F5A744EEC9DE55226556C73D26F7F78A95745E146D0F5EEAA19F08F`.

The guild-season suite passes **295 checks**, including 41 Grail checks. One
ordinary fixture earns all twelve trophies through **36 actual scheduled battle
captures/results**, 36 leader claims, twelve Gold exchanges and twelve rollovers,
then crafts through the NPC dispatcher. The test advances a controlled clock;
it does not wait through real calendar seasons or simulate a genuine-client war.

Coverage includes a former leader crafting, legacy materials/Grail rejection,
invalid twelfth materials, changed item UID, exact confirmation expiry, remote
interaction and six suppressed-write cases. Those cases include each wardrobe
grant independently. A late changed saved trophy rolls back earlier deletions.
Concurrent confirmations produce one Grail, twelve material receipts and two
award receipts. Ordinary save, character/item/wardrobe reload and later-season
reconciliation preserve the item, both equipped rewards and their point total.

Related suites pass: titles 91, Summer 65, progression 74, Elite PK 78 and
Potency/daily bosses/Extraction 216. Total: **819 gameplay checks**. The achievement
producer audit and its three tests also pass; its 157/337 source-producer count
is unchanged and is not proof of the 320-achievement per-character route.

## Remaining gates

The server earning/crafting route is implemented and tested. Native invitation,
combat, NPC dialogue, item presentation and title/wing visuals still need genuine
6711 acceptance. [M7.2e](GUILD_WAR_PURSES.md) now migrates the regular Sunday
rank purse to atomic delivery. M7's umbrella acceptance remains open.

No live restart, deployment, client patch or production-row mutation occurred.

## Retained full seasonal journey (2026-10-01)

The composed-process career now exercises the complete seasonal recipe using
the retained ordinary founder of CareerBanner. It begins from the saved CTF
checkpoint with owned equipment, currencies and prior achievement receipts.
There are no supplied victories, tokens, prizes or trophy receipts.

Preparation uses ordinary warehouse packets and accepted trades: eleven stored
unbound weapons move to the existing ordinary peer, and eleven existing bound
bag items move into those warehouse spaces. The retained bag falls from37 to26
items. The third-season purse described below requires six further ordinary
trades/storage moves before the fourth season. No possession is discarded,
duplicated or injected; peer-owned items remain in subsequent snapshots.

Each bounded season phase:

1. Starts three Sunday rounds in one five-week season and enters through the
   ordinary Guild Controller. Normal melee packets and damage calculation
   capture the pole. Controlled approach positions and cooldowns avoid waiting
   through a real-time hour; no damage amount or winner is supplied.
2. Settles each battle and returns through the installed battlefield exit.
   Monday GuildDirector claims produce one recorded bound Lord Token per win.
   Repeated claims produce no second token.
3. Exchanges the three exact earned tokens through the Gold Prize confirmation,
   asserting one prize, three spent receipts and exactly20,000 CPs. Replaying
   confirmation changes nothing.
4. Advances to the saved season boundary and invokes ordinary owned rollover
   twice, retaining one permanent bound Gold Trophy and its original item UID.
   Scheduled battle polling is held while testing that boundary so it does not
   accidentally open the next season before its intended process.
5. Saves the checkpoint for comparison in the next fresh process.

- [x] Twelve recorded seasons produce36 wins,36 spent seasonal tokens,
  twelve Gold Prizes, exactly240,000 exchange CPs and twelve recorded rollovers.
- [x] One ordinary craft consumes all twelve earned trophies and creates one
  permanent bound Grail plus the Invincible/Supreme permanent award receipts.
- [x] Both wardrobe rewards equip through their normal handler. A further fresh
  process verifies they are already equipped before another request, preserves
  the exact saved checkpoint and rejects repeated crafting without extra items.

Eleven season phases pass directly. The third season is verified by the guarded
recovery below after its successful earning/rollover; its original assertion
failure is not relabeled a passing phase. Craft recovery and final cold replay
pass. The regular purse that arrived during season three separately paid8,000
CPs,100 RVPs and2,000,000 silver with its six saved reward item UIDs. That is in
addition to the seasonal240,000 CPs, not part of the Gold Prize exchange price.

Snapshots cover prior career state, owned and traded items, guild state, battle
results, token claims, prizes/rollovers, crafting materials and wardrobe awards.
The separate regular Sunday purse timer delivered one earned purse during the
third season. That legitimate payout is retained. Subsequent private season
phases hold only that independent timer; their purse entitlements remain saved
and unclaimed. This isolates seasonal material accounting without changing the
production automatic-purse service.
The battles are unopposed; this does not prove competitive PvP, continuous
navigation, naturally elapsed seasons or genuine-client presentation.

The first snapshot query used an ambiguous joined item_uid reference and failed
before any earning/preparation operation. Qualifying that column fixed the test;
no player purchase or event reward was repeated to recover from it.

The third season completed its wins, claims, Gold exchange and rollover, then
failed an overly strict bag-count assertion: the independent purse timer had
legitimately added six reward items. Its pre-rollover checkpoint already saved
that payout. Guarded recovery required exact snapshot equality allowing only
the one Gold Prize's type transition and its two rollover receipt fields. It
preserved all payments and possessions and advanced to the next season without
replaying a battle, claim or exchange. The original failed phase is not reported
as passed. Subsequent snapshots also include the regular-purse rows.

The extra storage preparation initially selected an unbound quest token whose
catalog forbids trading. The trade was refused. A separate guarded recovery
verified that the only change since its checkpoint was that token's warehouse
withdrawal, then returned it through the ordinary warehouse packet and required
exact checkpoint equality. Item selection now uses the production trade rules;
the remaining space comes from allowed trades, not deletion of the refused item.
After the remaining tradable bag item moved, Twin City had no eligible stored
trade material. That attempt stopped at its saved checkpoint. The final space
used an ordinary warehouse in another installed main city. City/proximity
placement is controlled in this fixture; storage and ownership transfer still
use sealed warehouse packets and normal trade settlement.

The ordinary craft committed correctly, but its first equip check used the
wardrobe's real current date while Guild War's simulated seasons had advanced
to2028. The evidence loader correctly excludes future-earned awards. Both
fixture clocks now agree. Craft recovery required exact prior snapshot equality
allowing only the twelve recorded trophy removals, one recorded bound Grail,
craft/material rows and the two permanent awards. It then equipped those saved
rewards without repeating the craft. The failed equip phase is not reported as
a successful complete phase; the subsequent recovery and cold replay are.

Private game SHA-256:
07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2.
Fixture: GrailCareer in tools/CheckCandidateCareerRites.cs. Private logs retain
one retained-grail-season-N.log per season and the craft/reload phase logs.
No production gameplay change, rebuild, live restart or deployment.
