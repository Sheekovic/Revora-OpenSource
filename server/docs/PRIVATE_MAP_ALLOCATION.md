# Private map allocation

2026-09-20, baseline `906848a`. This is an event/quest instance repair and a
prerequisite for housing restoration. It does not replace legacy house IDs,
persist house ownership or enable any new housing reward.

## Problem and change

`Map.MakeDynamicMap` previously accepted the next wrapping counter value without
checking the world registry. Its constructor silently skipped registration if
that ID already existed, while returning a different object to the caller.
`Dispose` then removed the ID without checking which object owned it. After
counter wrap, an event could therefore hold an unregistered map and its cleanup
could remove another world. Dis City, Exorcism and Warrior Epic also maintained
separate allocation loops without a common creation lock.

`Map.CreateDynamic` now owns the shared allocation lock and checks runtime maps,
map metadata and terrain-index reservations throughout the 11000-59999 range.
It attempts at most 49,000 candidates and fails on exhaustion. Invalid counter
values fail before narrowing to a 16-bit map ID. Terrain construction occurs
before registration; missing or failed terrain leaves no partial registry entry.
Catalog loading retains the existing behavior after publication, with owned
cleanup on failure. Disposal checks object identity and uses the allocation
lock, so an old reference cannot delete a replacement instance.

`MakeDynamicMap`, Dis City, Exorcism and Warrior Epic now use this factory.
Direct legacy/static constructors retain their behavior. This change does not
make every global map/metadata dictionary access thread-safe or provide durable
identity across process restarts. Those are separate ownership concerns. In
particular, the legacy house loader still requires the replacement documented
in [housing research](HOUSING_EARNING_RESEARCH.md).

## Validation

`tests/PotencyHunt/Verify-DynamicMaps.ps1` compiles a separate process fixture
against the isolated server build and runs from `bin/Debug`. It opens no database
or listener and uses the installed level-1 house terrain read-only. Its 13 checks
cover loaded/static/metadata reservations, 32 concurrent allocations, wrap,
missing terrain, invalid counters, complete range exhaustion, stale-reference
disposal and concurrent cleanup. The fixture's process owns all synthetic
registry entries; it cannot mutate the running server.

The isolated server build and focused fixture pass, as do 176 Dis City,
453 Warrior Epic and 613 Exorcism checks. These exercise their production
instance factories, ordinary quest journeys and existing recovery/reward paths.
A missing optional GeoIP data notice from standalone fixture startup does not
affect its allocation checks. No full tournament lifecycle is newly certified.

No live restart, production mutation, client patch or housing migration is part
of this batch. Native journeys and the full housing milestone remain open.
