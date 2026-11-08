# Clan War territory earning

## Revora rules and evidence

The installed hash-bound 6711 catalog identifies five occupation achievements:
10305 Twin City, 10306 Desert City, 10307 Bird Island, 10308 Ape City and 10309
Phoenix City. Legacy city arena indices 0/3/4/2/1 and maps
1505/1508/1507/1506/1509 provide the corresponding existing event locations.
The five wilderness arenas use the same atomic currency/P7 purse without
inventing additional city achievement identities.

This is the documented Revora event implementation, not a claim of original
Conquer rule or native-packet parity. A daily start occurs during 20:33 in the
server's existing local timezone; opening is unique per arena and local date.
The scheduler accepts the whole minute instead of relying on observing second 0.
Entry closes twenty minutes after opening. After that boundary, one surviving
admitted clan wins; no surviving clan cancels the run. Multiple surviving clans
receive ten minutes of overtime, then the run cancels without prizes. An unopposed
clan can win, preserving the legacy result rule. Registered survivors of the
winning clan receive the territory achievement and configured currency purse;
absent, dead, unadmitted and departed members do not receive retrospective credit.

The default leader purse remains 5,000 CPs and 8 RVPs for Twin City, half elsewhere.
Member CPs are divided by three; member RVPs are divided by three when above one,
as in the existing economy. Amounts are captured when the run opens and the saved
member rank determines the recipient tier at settlement. The later valuable-purse
migration adds a bound P7 to new entitlements; no entry fee is introduced. Historical text-file ownership does not create achievements or
prizes. Native presentation and original eligibility parity remain acceptance work.

## Authoritative path

ClanWar.Process retains existing 1313 query/enter actions with exact framing and
footer validation. It requires a known envoy physically present within six cells,
a healthy authenticated player, no trade/booth and current clan membership. It
does not depend on an earlier generic dialogue click: the native clan window has
its own packet route. Saved membership is checked before admission; registration
and successful arrival are separate records. A failed teleport cannot create an
eligible entrant. Subsequent clan changes invalidate the original entry.

Settlement coordinates session installation, ordered participant owner locks,
the shared combat gate and clan relationship changes. It rechecks the field and
durable memberships/admissions. One transaction records the result, territory
occupation, participant prize entitlements and all matching achievement evidence.
A failed write rolls everything back; the unchanged open field can retry. Main
player-damage processing acquires owner locks before the combat gate, rejects
busy locks promptly, and blocks unadmitted, closed-event and same-clan attacks.

Each earned purse is delivered separately with an atomic CP/RVP update and payment
receipt under the character owner lock/session lease. The complete result already
owns the entitlement, so reconnecting or leaving the clan does not destroy it.
A full purse defers both currencies without marking the prize paid. Immediate
postcommit balance projection uses the existing committed-currency APIs. Normal
player ticks probe pending prizes at most every thirty seconds using a partial
unpaid index; sessions without prizes do not acquire a database character-row lock.

Startup cancels interrupted open runs without reopening their date or manufacturing
winners. Completed occupation comes from PostgreSQL after the informational legacy
text file loads. Completed unpaid prizes remain available. A healthy reconnected
character stranded in a closed arena is returned to Twin City by the normal tick.
Occupation days count successive successful defences while the same clan remains
owner; they are not an assertion of consecutive calendar-day attendance.

## Validation and remaining gates

The isolated build and 186 focused clan checks pass. All 41 player-defeat
regression checks also pass. The territory additions cover
all five ordinary envoy-entry/win/reload journeys, city identity mapping, configured
leader/member payments, database occupation reload, duplicate timers and daily
reopening, actual lethal combat, friendly-fire rejection and busy-owner handling.
The installed public NPC catalog contains all ten expected type-31 envoys; journey
fixtures place corresponding envoy actors on an isolated map and submit actual
1313 handler frames, rather than driving the genuine client UI.

Negative cases cover remote/clanless/malformed/closed entry, unadmitted/dead/departed
characters, empty and tied fields, the exact twenty/thirty-minute boundaries,
write suppression in every result family, failed payment rollback, overflow,
concurrent prize delivery after character reload, closed-arena recovery and startup
cancellation. Injected failures are expected log entries. Database and registry
reload tests run in-process against the disposable validation schema.

Still pending: fresh-Host crash/restart acceptance, genuine 6711 UI/packet acceptance,
native wilderness navigation and extended disconnect/movement/reflection/summon
combat races, title/wardrobe visual acceptance and deployment. The core lethal
player-damage path is tested; this does not certify every secondary damage source.
This batch does not deploy, restart the live server or publish a client patch.

## Valuable Clan War purses (2026-09-27)

New winning entitlements freeze one random **bound permanent P7 soul** beside the
existing configured CP/RVP amounts. Every eligible surviving admitted winner gets
one, including leaders and members; currency rank ratios remain unchanged. This
is a scheduled PvP event reward, not a daily quest P7 reward.

`revora_clan_war_awards.soul_type` records the selected catalog identity;
`soul_uid` links the delivered item to its paid receipt. Existing rows migrate to
type0 and keep their original currency-only terms, whether pending or paid. Old
claims are never reopened. New item insertion, both wallet updates and the paid
receipt commit together under the existing session/inventory/owner locks. A full
bag or currency overflow defers the entire purse. Inventory and wallet preimages
must agree with storage; the session is rechecked before commit. Postcommit
installation updates the ordinary inventory, currencies and progression view.

Isolated build passed with existing legacy warnings; **203 focused clan checks
passed**. All five city envoy/win routes deliver and reload the exact saved P7 UID,
type and binding for ordinary leaders/members. Additional checks cover full bags,
failed P7 insertion, failed paid-receipt rollback of the preceding item, concurrent
reconnect delivery, one remaining bag slot, and old pending/paid currency-only
terms after repeated initialization. Existing eligibility/result/combat checks are
included in this overlapping count.

The committed focused selector is `Verify-PotencyHunt.ps1 -ClanMembershipOnly`.
This worktree used the existing isolated build and ignored wrapper
`obj/verify-clan-war-purse.ps1 -ClanWarOnly` to avoid unrelated owner changes.
An initial incorrect wrapper switch ran the broader legacy harness and failed
before Clan War; that run is not included in the focused acceptance evidence.
Logs: `obj/clan-war-soul-build.log` and `obj/clan-war-soul-tests.log`.

Private Host migration/storage restart is verified below. Authenticated-player
crash recovery, native wilderness navigation and genuine-client presentation remain pending. No live deployment,
production schema mutation, server restart or client installation occurred.

### Private Host migration acceptance

Build df725485 passes two real isolated Host/database cycles. The first Host adds
the previously absent soul columns while preserving old pending/paid currency-only
receipts. The second retains exact synthetic old and new pending/paid terms. All 26
checked table identities survive; fixtures are removed and the candidate is
cleanly stopped. [Evidence and scope](ROADMAP_STARTUP_ACCEPTANCE.md#clan-war-soul-migration-and-mixed-claim-retention-2026-09-27).
This does not close authenticated earned-player crash or native acceptance.

## Wilderness envoy-to-reward journeys (2026-09-27)

All five wilderness component journeys now pass: Wind Plain/envoy101897/arena1555,
Maple Forest/101902/1590, Love Canyon/101906/1560, Desert/101914/1580 and
Bird Island/101910/1570. The fixture reads each installed envoy's actual type, map,
coordinates and mesh from the existing catalog, loads real terrain, and requires
a nearby walkable interaction tile. It uses each arena's configured `mapdoc`
alias; the initial direct1555.map lookup failed because that arena uses an alias.
No replacement map or alternate NPC placement was introduced to make it pass.

Ordinary leader/member sessions send the existing1313 query/entry frames, arrive
in the correct arena, remain unopposed through the20-minute boundary, receive the
configured currencies and exact frozen bound P7, then reload character/items and
occupation without repeat payment. Wilderness runs earn no invented10305-10309
city achievement. The five existing city journeys remain in the same suite.

**273 focused clan checks pass** (`obj/clan-wilderness-tests.log`). This is a
fixture/source-validation expansion against the already built gameplaydf725485;
no production source changed and no new build, Host refresh or restart was needed.
The fixtures begin at an accessible envoy tile; they do not establish a full
native walking/portal journey from character creation, click/render acceptance,
or wilderness PvP/secondary-hit parity. Those gates remain open separately.

## Restored public envoys and earned process recovery (2026-09-27)

The first actual-game earning run exposed an access defect hidden by the city
component fixtures: TwinCityPlaza still parked101895 and101897 on6000 and kept
their desks closed. ParkNpc also saved those retired positions to the database,
so merely removing the parking calls would not recover previously booted worlds.

TwinCityPlaza now restores those two existing ClanInfo actors near297,279 and
293,279 on canonical map1002. It preserves their mesh/type, rejects missing,
duplicate or incompatible identities, and searches at most six tiles around each
anchor for free terrain with an adjacent approach. Already-restored actors retain
their positions, accounting for AddNpc marking an actor's own tile invalid.
Other retired desks stay closed. The current private catalog uses the exact two
anchor positions. Native appearance and live installation remain unverified.

The new `tools/Verify-ArenaProcess.ps1 -ClanWarOnly` route passes:

- [x] Real startup restores both catalog envoys from retired rows; both are public,
  unique, open and approachable, without nearby NPC overlap.
- [x] An ordinary leader/member query and enter the Twin City arena through the
  production1313 handler and actual envoy. An absent clan member stays outside.
  The arena cannot settle early; remaining unopposed until20 minutes earns the
  territory, configured leader/member CPs/RVPs, one bound permanent P7 each and
  achievement10305. The absent member earns none of these rewards.
- [x] Force game exit after saving the earned result, restart PostgreSQL and the
  game, and reload the three profiles. Exact item UID/type/binding, currencies,
  paid timestamps, achievement receipts and one-day occupation survive. Repeated
  payout calls do not pay again; both public envoy positions remain unchanged.
- [x] All ten installed arenas now have actual-process earning/recovery evidence
  in the extension below.
- [ ] Interrupted active combat/unpaid prizes, authenticated/native entry/combat,
  natural calendar and deployment remain open.

The helper supplies clan affiliation/ranks, eligibility, NPC proximity and UTC
time. It does not supply arena admission, occupation, rewards or achievement rows.
It uses Begin/Work with a controlled deadline and does not claim contested PvP
or an ordinary clan-founding journey. The private build passes with existing
legacy warnings; both process phases pass in `obj/clan-earned-process.log` on
SHA256 `D57AE461F6A7BB17C76AAFA75ACD0713C78B13CDD9AD80E19CD5C8F26F9DAC8A`.
Build log: `obj/clan-envoy-build.log`. Private helper compilation treats warnings
as errors. The owned clone is removed and private processes stop on success.
No live service, database, installed client or public patch was changed.

## All ten installed arenas (2026-09-27)

The same two-phase ClanWarOnly fixture now exercises every city and wilderness
arena on ten separate controlled UTC days. Each entry queries and uses its unique
installed ClanInfo envoy through production1313 handling, from adjacent walkable
terrain. It uses startup-loaded arenas; no synthetic map or NPC is supplied.

- [x] All ten ordinary leader/member admissions and unopposed20-minute captures.
- [x] Exact per-round configured CP/RVP deltas, including half purses outside
  Twin City and the member share calculated after that reduction.
- [x] Ten permanent bound P7 items per participant, each matched to its own paid
  receipt; no items or currencies for the absent member.
- [x] Correct individual city achievements10305?10309 as their respective arenas
  settle; wilderness rounds do not create another city achievement.
- [x] All ten one-day territories reload after the game exits and PostgreSQL
  restarts. Exact inventory fingerprints, wallets, achievement counts and all20
  full award-row fingerprints survive repeated payout attempts.

Both phases pass in `obj/clan-all-arenas-process.log` on the same gameplay SHA256
`D57AE461F6A7BB17C76AAFA75ACD0713C78B13CDD9AD80E19CD5C8F26F9DAC8A`.
Only the private fixture changed; no gameplay rebuild was needed. Clan affiliation,
proximity and time remain supplied, and entry packets call the handler directly.
This does not prove contested combat, transport/native behavior, natural calendar
opening, interrupted unpaid delivery or deployment. The isolated clone and private
processes were cleaned up by the successful verifier.

## Interrupted admitted match (2026-09-27)

The all-arena fixture now opens an eleventh match on a separate controlled day,
admits the ordinary leader/member through the installed Twin City envoy, saves
both inside the arena, and exits without calling settlement or graceful shutdown.
It verifies two durable admitted entries and no awards for this still-open run
before exit. PostgreSQL and the game then restart.

- [x] Startup changes that run to cancelled with no winner and an end timestamp.
- [x] A same-day Begin attempt cannot reopen the cancelled match.
- [x] Both saved participants load inside the arena; production RecoverLocation
  returns them to1002,301,279, and a production save preserves that return.
- [x] No interrupted-run prize exists; prior inventories, wallets,20 paid award
  fingerprints, five city achievements and ten one-day territories stay intact.

Both phases pass in `obj/clan-interrupted-process.log` on gameplay SHA256
`D57AE461F6A7BB17C76AAFA75ACD0713C78B13CDD9AD80E19CD5C8F26F9DAC8A`.
This is an abrupt process exit after admission and explicit character saving,
not an OS kill during contested combat or a socket login. Recovery is invoked
through its production method, and the returned coordinates are checked in the
database after saving. Unpaid-prize delivery across interruption, contested
combat, authenticated/native acceptance and deployment remain open.

## Deferred full-bag purses (2026-09-27)

The current ClanWarOnly suite has three game launches and two PostgreSQL restarts.
After the ten arena captures, the leader/member fill their durable bags with
supplied ordinary fixture items and earn another Bird Island capture through its
installed envoy. Production settlement saves two entitlements while full bags
prevent all currency/item delivery. These fixture items test capacity; their
acquisition is not claimed as earned gameplay.

- [x] Both bags stay at40, both CP/RVP balances stay unchanged, and each winner has
  one unpaid receipt with a frozen P7 type and no soul UID.
- [x] After abrupt exit and game/database restart, full bags still prevent payment.
  Inventory, wallet and complete receipt fingerprints match their pre-exit values.
- [x] Removing the supplied fillers allows each production Pay call to deliver
  exactly the frozen soul type, permanently bound, and the saved CP/RVP amounts.
  A second call pays nothing. Each soul UID matches its paid receipt.
- [x] Another game/database restart preserves all22 awards, eleven earned P7s per
  participant, exact wallets and receipt fingerprints. Repeated payout attempts
  add nothing. The absent member still receives nothing.
- [x] Bird Island's second earned occupation day survives; the other nine remain
  at one. The later interrupted Twin City match still cancels without rewards.

All three phases pass in `obj/clan-deferred-process.log` on gameplay SHA256
`D57AE461F6A7BB17C76AAFA75ACD0713C78B13CDD9AD80E19CD5C8F26F9DAC8A`.
The later interrupted test is now January13 in the controlled clock, following
the January12 deferred capture. This supersedes the earlier two-phase fixture
counts. This verifies durable deferred delivery through direct production methods,
not the timed delivery pulse over an authenticated connection or loss of the
payment COMMIT reply. Contested combat, native acceptance, natural scheduling and
live deployment remain open. No production gameplay change or rebuild was needed.


## Contested server-process capture (2026-09-27)

- [x] Two ordinary clans use the existing envoy packet handler to admit three
  players to Twin City Clan War. With both clans alive, the twenty-minute pulse
  leaves the war open without prizes. At thirty minutes it cancels without
  territory ownership or awards.
- [x] A new day's contest again reaches overtime with both clans present.
  Production ReceiveAttack refuses damage to an admitted ally and an unadmitted
  rival placed on the arena map. Controlled lethal damage through that same
  production damage/death path defeats the admitted rival. The next settlement
  grants the territory to the surviving admitted clan.
- [x] Only the winning leader and surviving admitted member receive the configured
  full/member CP and RVP amounts, one bound P7 each and achievement10305.
  The defeated leader and unadmitted clan member receive nothing.
- [x] After game/database restart, all four ordinary profiles retain exact item
  UID/type/binding/quantity, currency and paid-receipt fingerprints. Repeated
  production Pay calls change neither items, receipts nor wallets.

Evidence: `tools/Verify-ArenaProcess.ps1 -ClanContestOnly`,
`obj/clan-contest-process.log`, private `arena-process-clan-contest.log` and
`arena-process-clan-contest-reload.log`. Gameplay SHA256 remains
`D84835DF41CADB027A5928118BAD0DA8E357150D62A6B05D7FE4BC13E22C514E`.
Existing membership, ordinary profile eligibility, proximity, clock and lethal
attack damage are supplied by the fixture; admission, death, winner selection,
prize creation and persistence use production code. This is not TCP combat,
combat balance, native UI or naturally observed calendar evidence. Authenticated
entry/delivery and native acceptance remain pending. No gameplay rebuild or live
deployment occurred; the owned test clone was removed.


## Authenticated entry and immediate reward packets (2026-09-27)

- [x] Two ordinary accounts authenticate through Account/Game TCP and load saved
  clan leader/member identities. Both request the live Twin City envoy's1313
  entry offer, validate its enabled entry flag, send their own entry action,
  and receive the1505 map response. Two admitted records belong to that clan.
- [x] The twenty-minute unopposed result uses production settlement. The host
  verifies configured full/member CP and RVP purses and exactly one bound P7
  soul per participant. Each socket independently decodes the CP/RVP attribute
  updates and1008 item update, checking exact item UID/type and binding, and
  verifies its return to Twin City without relogging.
- [x] Repeating the production payment attempt leaves its saved receipt unchanged;
  another wire read barrier confirms the same currencies and single bound item.

Evidence: `tools/Verify-ArenaProcess.ps1 -ClanWireOnly`,
`obj/clan-wire-process.log`, and private `arena-process-clan-wire.log`
(`clan-wire-passed`). Gameplay SHA256 remains
`D84835DF41CADB027A5928118BAD0DA8E357150D62A6B05D7FE4BC13E22C514E`.
Membership, initial envoy proximity and the event clock are fixture-supplied;
entry and immediate reward updates travel through authenticated sockets. This
unopposed transport test complements the contested process test; it does not
claim opposing-player TCP combat, native1313 UI rendering or natural scheduling.
The owned clone was removed. No gameplay rebuild or live deployment occurred.


## Opposing-player TCP combat (2026-09-27)

- [x] Three authenticated ordinary players enter through the envoy: two members
  of one clan and the opposing clan's leader. With both clans still alive, the
  twenty-minute settlement pulse stays open and creates no prize entitlements.
- [x] The attacking leader switches to PK mode over TCP. Repeated ordinary melee
  packets, paced beyond the attack cooldown, defeat the opposing leader. Neither
  damage, HP, death, score nor the winning clan is supplied by the fixture.
- [x] Settlement records clan50000 as the winner. The defeated player's socket
  receives its death and Twin City return, with zero CPs, RVPs and inventory
  items; the database contains no award for that player. Both surviving members
  receive their configured currencies and one bound P7 over TCP, preserving the
  previous immediate-update and duplicate-payment checks.

The fixture supplies existing membership, event clock and proximity on adjacent
walkable arena cells selected through the map's spawn-coordinate provider. The
first attempt stopped before combat because the earlier fixed coordinate range
had no walkable pair; the corrected fixture passed without gameplay changes.
Evidence: `tools/Verify-ArenaProcess.ps1 -ClanWireOnly -ResetOwnedFixture`,
`obj/clan-combat-wire.log` and private `arena-process-clan-wire.log`.
Gameplay SHA256 remains
`D84835DF41CADB027A5928118BAD0DA8E357150D62A6B05D7FE4BC13E22C514E`.
The owned clone was removed. This is synthetic authenticated protocol evidence,
not native UI, class-balance testing, natural calendar observation or deployment.


## Retained founder's five city territories (2026-10-01)

The `career-clan-territory-*` journey uses the same ordinary retained character
and normally founded CareerKeep clan. Existing territory, clan membership,
achievement receipts and prizes are not inserted to satisfy this journey.

- [x] The city envoys admit the founder through sealed1313 offer/entry actions.
  Controlled daily runs on2027-02-08 through02-12 settle Twin City, Phoenix,
  Ape City, Desert City and Bird City after twenty unopposed minutes. Production
  settlement records each occupation and achievements10305?10309.
- [x] Twin City and Phoenix finish with a full bag. Their awards remain pending;
  no CPs, RVPs or soul items are lost or prematurely granted.
- [x] Ordinary trades and warehouse packets make room for city scrolls while
  retaining earned belongings. Paid stock and normal scroll use reach the
  remaining cities. Three queued awards deliver during later settlements;
  wallet deltas match the newly paid records each time.
- [x] A fresh process restores two remaining awards and all three earlier paid
  results. Both pending claims deliver their saved CP/RVP contracts and bound P7
  souls, matched by item UID. A further payment call is a no-op. The total is five
  territory awards, five bound P7s and the five territory achievement receipts.

Capture and claim phases pass against private game SHA-256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.
The final cold-replay phase also passes: no extra payment or soul item. Snapshots cover saved
items, currencies, guild and clan, occupations, result/payment rows, achievement
receipts and the trade recipient's items. No gameplay rebuild or live deployment.

### Travel evidence and fixture corrections

The loaded Twin City portal to Ape City is at350,845. No walkable two-cell approach
was found within its accepted four-tile radius. The earlier portal fixture failed
before changing the retained checkpoint; completed Twin City/Phoenix results were
not replayed. The working ordinary alternative buys city scrolls from an installed
silver shop. The portal's terrain/client placement remains a separate unresolved
travel issue; this test does not repair it or prove native navigation.

The initial Desert scroll assumption was wrong:1060023 goes to Phoenix, while
1060021 goes to Desert. Its recorded200-silver purchase and consumption were
preserved. Resume required an exact saved-state comparison allowing only that
recorded debit, then used the ordinary Phoenix return portal and correct scroll.
No refund, result, membership or inventory item was injected to hide the error.

Clock and proximity are controlled. The event's production begin/entry/settlement
and payment paths run with an unopposed clan; this is not proof of rival-player
combat, native UI, natural calendar observation or uninterrupted walking routes.
The independent earlier contested/wire evidence remains scoped as documented.


## Fresh founded clan five-city settlement (2026-10-03)

The ordinary fresh career UID1096322 uses its previously founded FreshKeep601
and retained FreshBanner51102 membership. A normal Phoenix warehouse deposit
moves one owned item out of the bag, leaving35 slots occupied. No clan, entry,
result, reward or balance is supplied by the fixture.

Each city's actual envoy receives sealed1313 registration/entry actions6 and8.
The production event begins under a controlled clock on five separate private
cycles20410301-20410305; after twenty simulated minutes the sole admitted clan
wins through normal settlement. Twin City awards5,000 CPs and8 RVPs; Phoenix,
Ape, Desert and Bird each award2,500 CPs and4 RVPs. Each also pays one bound
catalog P7 soul. Every wallet delta is matched to its saved city/run contract,
and all previously owned bag UIDs remain present after each reward.

Total:15,000 CPs,24 RVPs, five bound P7s and five clan-territory-v1 receipts
10305-10309. All five territory rows and in-memory occupation views belong to
FreshKeep. Final balances are343,905 CPs and829 RVPs, with40 bag items. The
private game/database restart preserves exact item UIDs, purses, occupations,
admissions, settlements, rewards, clan/guild and the complete prior career.
No pending award remains; repeated Pay and attempts to begin the already
completed five cycles return without new payments, entries or ownership changes.

`tools/CheckCandidateFreshTerritories.cs` phases
`career-fresh-territories-capture` and `career-fresh-territories-reload` pass on
their first run. Latest checkpoint `revora_fresh_territories_probe` is `paid`.
Game SHA-256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.

This proves ordinary unopposed entry/settlement and paid recovery on the fresh
career. Map/NPC approach and calendar are controlled; there is no rival-player
combat, natural calendar observation or native navigation claim. The earlier
retained full-bag/pending-prize and contested-wire checks retain their separate
scope and were not repeated. No game-source change, live restart or deployment.
The post-Ancient56-receipt audit remains historical; these five territory
receipts and the intervening clan-founder receipt occurred afterward.
