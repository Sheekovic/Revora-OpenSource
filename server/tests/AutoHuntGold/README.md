# Auto-hunt gold pickup validation

Build Debug/x86 into `obj/chi-refill-validation/bin` using the isolated build
command in `tests/CompanionAI/README.md`, then run:

```powershell
& tests/AutoHuntGold/Verify-GoldPickup.ps1
```

The runner reuses the companion suite's in-memory map and disconnected-player
fixture construction, but runs only GoldPickupTests. It invokes the actual
compiled gold collector and manual pickup handler. It starts no server, timers,
database connection or sockets. The optional GeoIP missing-file message comes
from the legacy assembly initializer and does not affect these tests.

25 checks cover nearby collection, exact-tile manual pickup, map/screen removal,
replay, ownership timeout, public drops, range, terrain, auto-hunt state, VIP,
death, trade, map mismatch, non-gold exclusion, expiry, stale screen entries,
balance overflow, competing collectors, simultaneous manual/automatic pickups,
concurrent distinct piles, and team money-sharing rules.

The existing one-second screen item callback attempts at most eight successful
automatic gold pickups per tick. It remains subject to the existing map freeze
gate. Collection is within three tiles and does not alter the client's movement
or item-selection settings. Manual collection still requires the exact tile.

Persistence continues through Entity.Money and the existing character
heartbeat/logout save. Database restart persistence and genuine 6711 client
updates are not exercised by these fixtures. After approved activation, verify
gold disappears, the displayed balance increases once, stopping auto-hunt stops
collection, and the balance survives a normal logout/login. Use an ordinary
non-GM VIP 1+ character, matching existing auto-hunt eligibility.
