# Nobility NPC earning route

## Boss-funded sign-in donation journey (2026-10-02)

The focused ordinary-character journey starts with zero CPs, RVPs, bound CPs and
silver. Two scheduled daily-world-boss victories through the normal damage handler
earn100,000 RVPs. Twenty-one sealed3200 normal sign-in requests spend84,000 RVPs;
each same-day replay is refused. Only the issued7-day and21-day milestone packs
are opened, through ItemUsage: they award500,000 and1,500,000 bound CPs. The21-day
pack also pays its existing20,000 RVPs, leaving36,000 RVPs after sign-in costs.

The public donor spends the earned2,000,000 bound CPs for20,000,000 donation.
Concurrent confirmation pays once without changing regular CPs or silver. A
mid-calendar character reload retains the first award and exact remaining RVPs;
final board/character reload retains donation and purses, both pack-use receipts,
one payment receipt and replay refusal. The focused suite now passes82 checks,
including the prior50 access/payment checks. The server assembly is unchanged
from the hash below; the fixture compiles with warnings as errors.

The initial test reused an older sign-in helper without the required client seal;
the server correctly rejected it. This journey now explicitly seals its request.
No game validation rule was relaxed and no sign-in reward was manually inserted.

Starting level140, second rebirth and VIP5 are supplied eligibility. Boss lethal
damage, travel/proximity and21 calendar dates are controlled. This is a composed
private funding/earning/reload check, not natural combat balance, elapsed real-time
calendar/expiration testing, a full Host restart, authenticated transport or native
client acceptance. It adds no retained-career receipts and performs no deployment.
The earlier supplied-purse fixture below retains its narrower original scope.

## Bound-CP service at the public donor (2026-10-02)

A read-only query of both `npcs` and `sobnpcs` in the private installed catalog
found no actor761124, despite its existing bound-CP handler. The already restored
public donor900042 at Twin City1002(394,350) now offers both currencies. No new
actor, client asset, placement or currency rate is introduced. CP offerings keep
the100,000 minimum and50,000 donation per CP; bound offerings keep the2,000,000
minimum and10 donation per bound CP. The legacy761124 handler remains supported.

Each pending confirmation now stores its selected currency together with NPC,
map, amount and expiry. Changing the currency menu clears the previous offer.
The confirmation uses the stored currency, and concurrent/repeated confirmations
consume the offer once. The existing atomic payment service and packet currency
restrictions remain unchanged.

The isolated server build passes with existing legacy warnings; the focused
fixture compiles with warnings as errors and passes50 checks. Tests exercise
ordinary decoded NPC requests for both currencies, minimum refusal, cancellation,
forged and remote confirmations, currency switching, concurrent replay, exact
payment receipts and full character/board reload. Existing atomic service tests
cover all three currencies and injected purse/donation/receipt write failures.
The private test schema is removed and its PostgreSQL process stopped afterward.

Reproduce the fixture compilation with
`python tests/EquipmentAchievements/compile_daily_unarmed.py --nobility` after
building the validation assembly. `NobilityAccess.Tests.exe` requires the existing
loopback25439 `revora_candidate_game` fixture environment and runtime catalogs.
Assembly SHA256:
`D7C63E73179960619F761096BE73F2A66AB9CE07B9C0F371B292EC10559F2D29`.

This fixture supplies funds and constructs the public donor at its configured
location. It proves service access and persistence, not newly earned bound CPs,
new terrain/navigation evidence, authenticated transport, a full Host restart,
or native dialog rendering. The separate public-placement process evidence below
still applies. No live deployment, restart or retained-career receipt changed.

## Public donor placement and process recovery (2026-10-01)

TwinCityPlaza now restores existing CP donor900042 from map5000(69,94) to
Twin City1002(394,350), beside the Chi/JiangHu clerk. It preserves the catalog
actor, mesh46420, NPC type and existing prices. Missing/ambiguous identity,
occupied placement or an inaccessible approach fail validation. An already
correct placement is retained on subsequent startup.

The private ordinary character4294908402 used its previously earned event CPs:
23 accepted Entity.Move steps reached the donor, decoded2032 requests through
the normal NPC dispatcher paid100,000 CPs for5,000,000,000 donation, and exactly
one payment receipt was saved. A remote confirmation and paid confirmation
replay made no changes. Full game/database restart preserved the donor's saved
location, exact CP/donation/evidence and the nested prior quest/event rewards.
The cold character walked away and back through the loaded town floor; replay
without a new offer could not charge again.

The first restart exposed a placement-check defect: Map.AddNpc reserves all
eight neighboring cells for ordinary talkers, so an adjacent-cell approach
check failed after the relocated actor loaded. The corrected check inspects the
two-tile ring within dialog range and avoids re-parking an unchanged actor.
Build and cold recovery, including the added walking regression, pass.
The original pay phase used SHA256
D7A8467476FF3A4E820BCE8A43010AA1D55E2D2C0020B64E52DC2E9FFAADC627;
the corrected candidate and passing reload used
CE1B425A68EB9082F15EF2C39FD2F0B0EC6D21980360CB1B5EA3FFB708522F10.
The paid transaction was retained, not reset or charged again to rerun the test.

Evidence: CheckCandidateCareerRites.PublicDonorProcess,
career-donor-pay and career-donor-reload. These are server handler/process
checks, not authenticated socket or genuine6711 navigation/dialog acceptance.
No live deployment occurred. The older map5000 fixture below is historical;
its supplied admission is superseded by this public placement/server walk.
Bound-CP donor placement remains outside this check. A separate retained
2064 donation proves10318/11120 and private-board King recovery; see
CAREER_REBIRTH_JOURNEY.md.

## Earlier isolated donor transaction fixture

The ordinary-player Grotto integration route now continues from a recorded
daily-world-boss victory through the installed CP donor 900042. The donor is
loaded from public.sobnpcs, including its map, position, mesh and NPC type;
it is not in the regular npcs catalog. The test spends 100,000 of the earned
CPs for 5,000,000,000 donation value, records 10318, and verifies exact purses,
donation and achievement after a full character reload and login achievement
refresh. Confirmation replay cannot charge again.

This exposed a legacy confirmation defect: both donors reused NpcCpsInput,
which was not reliably cleared by cancellation or tied to a particular donor.
NobilityDonations.HandleNpc now owns a session-local, one-minute offer containing
NPC, map and amount. It checks canonical ordinary-player eligibility and physical
proximity for every step, binds currency to the donor, consumes the offer before
payment, and discards it for cancellation, invalid input and other NPC requests.
The two legacy branches are removed. Existing minimums/rates remain unchanged:
100,000 CPs at 50,000 donation each; 2,000,000 bound CPs at ten donation each.

Validation: isolated Debug/x86 build and 329 Grotto checks pass. Nine added checks
cover the boss-earned funding precondition, forged/cancelled/expired/remote/
switched-NPC/invalid-input confirmations, concurrent confirmation, and production
character reload plus replay. Earlier service tests cover atomic debits for all
three currencies. This batch exercises the CP donor dialog, not the bound-CP
NPC's installed placement or an earned bound-CP route.

The fixture places the player at the catalog donor location and supplies combat
damage/time controls. It does not prove native navigation to that map, encounter
balance, or genuine-client dialogs/icons. Those gates and live deployment remain
pending. Unrelated owner edits were present in the isolated build. No live
database or process changed.
