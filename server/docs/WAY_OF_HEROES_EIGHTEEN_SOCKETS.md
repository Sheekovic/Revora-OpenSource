# Way of Heroes: eighteen sockets

Implemented and privately validated 2026-10-02; not deployed live.

Installed stage904 (task27, second rebirth level121 opening) requires eighteen
equipped sockets and awards three bound Superior Refinery Packs728919. It uses
the validated active-equipment predicate documented in WAY_OF_HEROES_EIGHT_GEMS.md;
empty sockets count, while inactive and unequipped gear do not.

The installed itemtype.txt explicitly describes728919 as70% level-four and30%
level-five refinery material. DisCityPacks already implements that distribution
for720399, so728919 now uses the same validated handler and receipt table. Draws
0-69 choose level4;70-99 choose level5. A second draw uniformly chooses from the
installed refinery/base-item intersection excluding intrinsically untradable
materials. That per-material pool remains Revora policy, not verified original
per-item odds. Bound inputs produce bound results. Existing expiry, ownership,
capacity, transaction and stack-replay protections are reused.

## Private process evidence

CheckCandidateSocketClaim.cs supplies an ordinary Warrior with level130, second
rebirth, stats, CPs/silver and eight double-socket equipment pieces. Those sixteen
sockets are fixture prerequisites. Boots start with zero sockets/progress.

- [x] Sixteen sockets refuse904 without changing the scoped snapshot.
- [x] Forty normally purchased Dragon Balls are consumed through quality forging.
  Controlled failed quality rolls earn both boot sockets through saved progress;
  ordinary paid repairs follow the failures. No boot sockets/progress are supplied.
- [x] Equipping the boots at seventeen sockets refuses. With eighteen sockets,
  selecting the empty alternate combat bank also refuses; shared talismans alone
  cannot satisfy the threshold.
- [x] Eight concurrent primary-set claims award exactly three bound728919 packs.
- [x] Controlled quality draws69,70,99 produce bound level4,5,5 materials from
  compatible installed pools. All three earned packs are consumed once; spent
  pack replay cannot duplicate output.
- [x] The three materials are applied through ordinary refinery packets. Application
  replay cannot consume twice. The final saved effect remains on the boots.
- [x] Full private game/database restart preserves exact scoped items, currencies,
  refinery state, all forty forging receipts, three opening receipts and the904
  milestone receipt. Repeated claims change nothing.

Game SHA256:
EEE2161711573E3C8B8C3501E09E15FC02B54C2B641DFF4A110F2A53866BA8C1.
Build completed with existing warnings using the committed-source overlay,
excluding unrelated owner WIP. The first fixture applied its identical third
refinery too quickly and correctly hit the existing one-second guard. The fixture
now advances its controlled refinery clock between applications. Recovery retained
the claimed/opened rewards and applied the remaining saved material without reset.
A final receipt assertion initially used operation0 instead of the defined
DragonQuality value3; correcting that fixture query then passed earning and cold
reload. No production cooldown, forging probability or refinery rule was weakened.

Native-client acceptance and live deployment remain pending. This process fixture
does not re-prove all original720399 fault/capacity cases or statistically measure
odds; the shared-service evidence is in SUPERIOR_REFINERY_PACK.md.
