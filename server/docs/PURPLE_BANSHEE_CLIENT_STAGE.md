# Purple Banshee private catalog candidate

The candidate gives Banshee Spirit 3004259 the implemented house contract:
once per Pacific day, thirty-minute hunt, departure/disconnect/team-defeat
forfeit, 200 summoner and 50 teammate Study Points, two 60% P5/P6 or 40%
level-5/6 refinery rolls, one extra bound P7, and the 35,000-Honor purchase price.
Odds and encounter rules are explicit Revora choices; the original client only
supplied the own-house/daily summon and 200/50 Study/P5/P6/refinery contract.

The reviewed server preimage had 67 fields: an extra `@@` split its description
between fields 53 and 54. The matching decoded client preimage has 66 fields
and a literal space at that location. Preparation repairs only that exact server
split before replacing the description. All following logical fields are retained
without changing their values, including the trailing 500 field. The client
changes field 53 only. Unrelated rows and line endings are preserved byte-for-byte.
This scope does not claim that the old separator caused a particular runtime stat
failure; it establishes matching catalog structure and text.

The preparation tool composes from the reviewed Anniversary-weekend candidate.
All earlier Sword Master, Anniversary, Magnolia, Summer, Labyrinth, Dis City and
other quest content survives. Questinfo.ini is unchanged. Inputs are hash-gated:
installed server/client catalogs, composed server/client/journal and approved DX9
executable. Sources are rechecked before the temporary directory is renamed to
the private destination; an existing candidate is never overwritten.

Four synthetic tests pass: exact server repair with logical-field preservation,
client-only description scope and idempotence, encryption round trip/player
contract, and missing/duplicate/changed/malformed-input rejection. An independent
whole-file audit compared all 31,399 server rows and 31,371 decoded client rows;
only item 3004259 differed. It separately checked the unchanged journal and every
source/output hash. Audit output remains ignored under
`obj/chi-refill-validation/purple-banshee-catalog-audit.json`.

Private directory: `obj/chi-refill-validation/purple-banshee-catalog/`.

| Output | SHA-256 |
| --- | --- |
| server/database/itemtype-new.txt | `adf4c9375285b15018f8d814afde65fd827f59cd7d3dae892d2c6f32f25cdc2f` |
| client/ini/itemtype.dat | `64178c832d91f65baf7c0ce6ffd4d40632abe68324fc6aaca4e92854d6488f6e` |
| client/ini/Questinfo.ini | `09dd2f2d01d5b775696f1d532eaeb0a2854aeb3adc024ec6a00e216e2b8aaf82` |

The candidate is **unsigned, uninstalled and unpublished**. The installed server,
client and production database were unchanged. Genuine-client tooltip length,
summoning, combat, long HP bar and reward acceptance remain open. Installation
requires exact reviewed preimages, backups and transactional rollback. This
private preparation is not a live availability or native-compatibility claim.

See [server encounter and remaining gates](PURPLE_BANSHEE_ENCOUNTER.md).
