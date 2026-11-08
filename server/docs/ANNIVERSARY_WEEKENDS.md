# Recurring Anniversary weekend admission

The matching [private server/client catalog candidate](ANNIVERSARY_WEEKEND_CLIENT_STAGE.md)
now includes the calendar, envoy location and weekday completion rule. It passes
three tests and an independent whole-file audit; it is not installed or published.

Owner decision, 2026-09-21: restore Anniversary as a **recurring weekend event**.
The source candidate now installs Anniversary Envoy 900028 in Twin City 1002 at
**(323,307)** and opens new commissions every Saturday and Sunday, Pacific time.
The event clock follows the same daylight-saving-aware Pacific calendar as the
existing daily allowance; it does not use the host's local timezone.

Composed startup correction (2026-09-21): the source catalog also contains the
retired SOB NPC `AdmAniversario`, ID 900028, mesh 34240, Talker type, on map 6000.
The plaza sweep used to move it before Anniversary placement, causing a fatal
identity collision that the original isolated placement fixture missed.
Placement now recognizes only that exact retired profile at the observed jail
coordinates (102,78) or (90,78). It checks the Twin City destination first, then
replaces the retired actor in the world/secondary registries with the restored
envoy. Unrelated profiles, duplicate identities and occupied destinations still
fail closed. The original database row is retained, so restart repeats this
bounded runtime conversion rather than depending on a production data migration.
The plaza sweep skips Anniversary while preserving the slot spacing of other
retired desks; Anniversary is no longer a closed desk.

The isolated build and 551 Anniversary checks pass, including both retired
positions, unrelated-profile rejection, destination occupancy, registry replacement
and idempotent placement, plus the existing full 15,000-spirit earning route.
The count includes prerequisites; it does not establish native acceptance.

Eligibility remains level 140, twice reborn and VIP 6+. Every eligible ordinary
authenticated player can use the NPC; there is no GM or owner-name restriction.
The existing one-completion-per-day exclusion shared with Spirit Ball remains.

## Availability and preserved progress

New beads are issued from Saturday 00:00 inclusive until Monday 00:00 exclusive.
A request crossing the closing boundary or daily rollover rolls back its new
bead and receipt, leaves the player connected and asks them to speak again.
Forged direct options cannot bypass the closed calendar.

Beads already entrusted to a player remain huntable and redeemable afterward.
Lost-bead replacement also remains available for an existing commission, with
the same original-item absence checks and saved progress. This avoids trapping
unfinished players behind the shared Spirit Ball allowance until another
weekend. Existing/imported items and completed receipts are preserved.

NPC dialogue is brief and separates the full reward list into a menu option.
New-contract rewards remain twenty cards, 50,000 RVPs, 50 million silver, 100
Study Points, two days of blessing and one **bound P7 soul**. Imported older
contracts retain their recorded reward terms. This is the recurring event;
direct daily quest completion still grants no P7.

## Placement and validation

Placement checks the reviewed Twin City terrain hash, canonical public world,
walkability and NPC occupancy. Repeated initialization recognizes its already
installed actor before checking the occupied tile. Conflicting NPC identities
fail closed. Admission also checks the exact actor location and presentation;
another map or private copy cannot reuse the NPC ID for admission.

Read-only inspection found Daily Envoy 79882 at (319,307), mesh 19936, and NPC
9450 at (319,317), with no other catalog NPC within twelve tiles of the Daily
Envoy. Twin City's stored mapdoc is 1002. The new envoy reuses the loaded 19936
presentation and sits four tiles east of the Daily Envoy. Inspector SHA-256:
`3047E8E5B2716A7C9600AC32ACE4440409C5A8B4580436DCD4D887AA82FDF0E5`.
The bounded inspector and its output remain outside Git under ignored `obj/`.

The isolated build, **546 Anniversary checks** and **15 quest-packet regression
checks** pass. The suite now uses production NPC placement and native dialogue
continuations for fresh admission instead of placing a synthetic Anniversary
actor on the Daily Envoy's tile. Added checks cover exact opening/closing
boundaries in normal, spring-forward and fall-back weekends, ambiguous timestamps,
Friday/Monday direct requests, weekday progress/replacement/redemption, midnight
rollback, public ownership and idempotent placement.

The full ordinary server earning fixture starts from a newly issued three-spirit
bead, earns the full 15,000 spirits through 3,750 registered qualifying monster
deaths, reloads the player and redeems all 21 reward items plus balances. The
suite retains eligibility, shared-allowance races, duplicate claims, failure
rollback and persistence checks. This is server integration evidence, not a
genuine client playthrough. Only a disposable database schema was written.

Logs: `obj/chi-refill-validation/anniversary-weekend-tests.log` and
`anniversary-weekend-packet-tests.log` (ignored). No production process was
restarted and no installed client was changed. Coordinated catalog installation,
genuine NPC appearance/dialogue/client reward acceptance and live Host
restart/rollback acceptance remain open. Earlier documentation describing the
calendar decision and source NPC as absent is superseded by this document;
historical test counts describe their own batches.

## Full hunt across actual game/database restarts (2026-09-27)

`tools/Verify-ArenaProcess.ps1 -AnniversaryOnly` now passes eight bounded game
launches: admission, five750-kill hunt batches, redemption and final reload.
The verifier restarts PostgreSQL between phases, restores private configuration
and removes its owned clone after success.

- [x] The startup-installed envoy900028 at1002,323,307 admits an ordinary eligible
  non-GM profile through a sealed2032 continuation packet and the production packet
  dispatcher. Admission issues one bead with its normal three starting spirits.
- [x] All3,750 qualifying deaths run through ReceiveAttack and the production
  monster death dispatcher, using startup-loaded level120+ monsters on map1999.
  Every kill's exact four-spirit increment is checked, capped at15,000. Progress
  is never preloaded; the saved counter matches after every phase restart.
- [x] Using the earned bead delivers20 cards, one permanent bound P7 soul,
  50,000 RVPs,50 million silver,100 Study Points and172,800 blessing seconds.
  Using the stale consumed bead again delivers no additional prize.
- [x] The final game/database restart preserves the exact inventory fingerprint,
  complete completion-row fingerprint, all21 reward UIDs and each saved balance.
  Same-day NPC admission cannot grant another bead or change those items.

The fixture supplies level140, second rebirth and VIP6 eligibility, the Saturday
UTC clock, NPC/monster proximity and lethal damage; it does not prove progression
to eligibility, damage balance, navigation, TCP authentication or native UI.
When an installed population is exhausted, its existing map respawn callback is
advanced to a controlled deadline; no synthetic monster or quest credit is added.
Ordinary money/CP drops and random equipment drops are disabled in this isolated
fixture to separate hunt rewards. These controls are not deployed settings.

All eight phases passed in `obj/anniversary-process.log` on gameplay SHA256
`D57AE461F6A7BB17C76AAFA75ACD0713C78B13CDD9AD80E19CD5C8F26F9DAC8A`.
Helper compilation treats warnings as errors; gameplay source was unchanged and
no new gameplay build was required. Public calendar observation, authenticated
native-client acceptance, live installation and deployment remain pending.


## Authenticated bead issue and earned claim (2026-09-27)

- [x] An ordinary level140, twice-reborn VIP6 profile authenticates through real
  Account/Game TCP, opens envoy900028 and chooses the weekend bead option. Its
  socket receives exactly one3001172 bead; the normal three starting spirits
  are verified in the saved run.
- [x] Five existing hunt phases earn all3,750 qualifying monster deaths through
  the production damage/death path. Each exact spirit increment is checked;
  progress survives game/database restarts between phases. Lethal damage and
  respawn deadlines remain supplied by the process fixture, not TCP attacks.
- [x] The same character reauthenticates and uses that earned bead through1009.
  Its socket receives bead removal, twenty cards, one bound soul, +50,000 RVPs
  and +50,000,000 silver. Server checks confirm the soul is permanent P7,
  +100 Study Points and +172,800 blessing seconds. Reusing the stale bead over
  TCP leaves the same21 items and currency totals.
- [x] A final game/database restart preserves item UID/type/binding/quantity,
  the complete completion receipt and all21 reward UIDs, currencies, Study
  Points and blessing. Another same-day admission adds nothing.

Evidence: `tools/Verify-ArenaProcess.ps1 -AnniversaryOnly -ResetOwnedFixture`,
`obj/anniversary-wire-process.log` and private
`arena-process-anniv-wire-start.log` / `arena-process-anniv-wire-claim.log`.
Gameplay SHA256 remains
`D84835DF41CADB027A5928118BAD0DA8E357150D62A6B05D7FE4BC13E22C514E`.
The transport probe explicitly accepts the supplied twice-reborn level140
profile and checks currency deltas: this fixture starts with100,000 silver, so
its correct final balance is50,100,000. A prior assertion incorrectly expected
50,000,000 total; retained database evidence identified the test error before
rerunning the earned route. No production reward value was changed.

This verifies authenticated admission/claim and immediate card/soul/currency
packets alongside the full server-process hunt. It does not claim TCP attacks
for all kills, native UI, progression to eligibility, natural weekend observation
or live deployment. The owned clone was removed after all eight phases passed.


## Dialogue close correction (2026-10-05)

Normal dialogue option 255 is consumed before early NPC handlers and eligibility checks, clearing the active NPC instead of reopening a rejection menu. Message-box interaction 6 and journal interaction 102 retain their existing meanings; item-menu routing remains separate. The Anniversary handler also handles close before its site/eligibility checks. Shared dialogue replies validate their minimum frame and input bounds before dispatch. Build, 38 compiled close checks and four dispatch coverage checks passed. Native response/latency confirmation remains pending.
