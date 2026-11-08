# Complete installment material journey

The level-five installment contract requires five Dragon Balls, twenty Lucky
Amulets, forty Euxenite Ores, twenty Meteor Scrolls and thirty eligible Super Gems.
The combined fixture in `HouseInstallmentTests.VerifyInstallmentEarning` follows
one ordinary character through existing production acquisition and delivery
services. It does not inject any of those 115 final materials into the bag.

## Route under validation

The character builds the level-four prerequisite using the ordinary certificate
shop and construction services. Starting currency is fixture-provided. The
character then visits Magnolia through Daily Envoy, takes the free seed, pays
for a seed change, walks to the appropriate bed and plants it. Controlled draws
select existing grade-four Dragon Ball/Meteor Scroll rewards and grade-three
standard Super Gems. One additional day selects the no-bonus boundary. This
exercises 56 different daily contracts, including all seven eligible gem families.

Each earned gift travels through the garden exit, production character/item
reload, House Admin showroom entry and Senior House Manager delivery. Delivery
receipts must reference the earned item UID, and the saved item must be gone.
Repeated confirmation cannot add a second delivery. The daily Chi Tokens are
redeemed in eight groups of seven through the existing cash service.

The twenty Lucky Amulets are purchased from the installed subclass shop for
100 CPs using the actual catalog and stock. They are reloaded before delivery.
The character buys and equips a pickaxe, buys two return scrolls, walks through
the Phoenix Castle mine entrance and mines forty Euxenite Ores over two trips.
Every trip uses the purchased return scroll and reloads before its ore delivery.

Final construction is attempted concurrently, followed by production reload,
achievement projection, private-house entry and another reload/projection.
The final database join links all 55 delivered garden gifts to their original
bonus item UIDs and verifies the separate no-bonus receipt. Saved and runtime
CPs, RVPs, Study Points and silver must reconcile after the final reconnect.
Accounting covers seed changes, daily rewards, Chi redemption, amulets,
the tool and return travel. The separate ordinary housing regression retains
the transaction-failure, capacity, ownership and replay checks. Both suites
share fixture initialization; the long earning run can be executed independently.

## Limits

Dates and random draws are controlled in the isolated fixture. This does not
change the 20% production bonus chance, claim 55 successes in 56 normal days,
or measure expected collection time. It is one supported acquisition route,
not a claim that Magnolia is the only source of these materials. Initial
character prerequisites and currency are fixture inputs; the test does not
prove a fresh level-one, zero-currency journey.

The journey reload includes derived-HP calculation from restored attributes.
The general item-reload fixture omits this login step; reusing it unchanged
made the fourth garden reward's level-up refill HP to zero. The initial combined
run consequently failed the guarded garden return while retaining the earned
gift. That fixture defect is corrected here without altering production travel
or granting an artificial revive during the journey.
The next run completed all materials and saved construction, but its final
reconnect assertion omitted title/achievement login projection. The completed
fixture includes that normal projection after both final reloads; the initial
failed run is not counted as a passing complete journey.

The fixture uses server walking and ordinary NPC/item handlers. It does not
exercise the rendered client, a complete Host restart or network transport.
No production database, installed client, server executable or shop price is
changed. Native acceptance and deployment remain separate open gates.

Run from the repository with:

```powershell
& tests/PotencyHunt/Verify-PotencyHunt.ps1 -InstallmentEarningOnly
```

Validation: **533 earning checks pass**, including the final private-house
reconnect, bonus-to-delivery provenance join and all four saved balance totals.
The fixture compiles with warnings as errors against the existing isolated
production build. Three achievement-audit tests also pass. No production source
change or new achievement producer was needed to connect this supported route.
Log: `obj/chi-refill-validation/installment-earning.log` (successful exit 0).
The standard housing/storage suite's existing 444-check result remains the
preceding service regression; it is not included in the 533-check count.
