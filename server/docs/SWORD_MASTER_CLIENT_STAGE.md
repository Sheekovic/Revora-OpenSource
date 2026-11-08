# Sword Soul private client review candidate

Prepared 2026-09-21 for the [owned Sword Master encounter](SWORD_MASTER_ENCOUNTER.md).
The installed level-99 tooltip disagrees with the level-100 admission requirement
and omits the thirty-minute limit, departure/disconnect forfeiture and bound event
P7 reward. The candidate updates only field 53 of item 720842 in both catalogs.
Names, category, prices, stats, binding flags and every other item remain unchanged.

`tools/ClientPatches/prepare_sword_master_catalog.py` composes from the reviewed
Anniversary server/client candidate and carries the reviewed Dis City quest journal
byte-for-byte. This preserves the inherited Magnolia custom packs, Summer,
Labyrinth, Dis City, Anniversary and earlier quest-journal work. The installed
server/client and genuine DX9 executable are hash-checked but never modified.
Preparation uses a temporary directory, rechecks every source before publication
into the private workspace, and refuses to overwrite an existing candidate.

Private output: `obj/chi-refill-validation/sword-master-catalog/`.

| Output | SHA-256 |
| --- | --- |
| server/database/itemtype-new.txt | `d88ac85ad5d618373301223d27f1bba91ebf6ee1249c9ba8c4fef702e0f4b606` |
| client/ini/itemtype.dat | `a348140eb0b55876308948ddb63e30aae94b10ba7d2a013f5364003b893e7703` |
| client/ini/Questinfo.ini | `09dd2f2d01d5b775696f1d532eaeb0a2854aeb3adc024ec6a00e216e2b8aaf82` |

The ignored `review.json` records all installed and staged source hashes, output
hashes and pending gates. Three synthetic tests pass, covering both exact legacy
description encodings, field scope, encrypted round trip, idempotence and rejection
of unknown/missing/duplicate/malformed records. An independent whole-file audit
compared all 31,399 server records and 31,371 decoded client records: each changed
exactly `720842:[53]` relative to its composed input. The journal is byte-identical,
and every source/output hash was rechecked after preparation.

This candidate is unsigned, uninstalled and unpublished. Native tooltip length,
all seven skill visuals, the long HP bar, house movement and loot pickup still
require genuine 6711 acceptance. The current source roadmap does not imply live
availability. Future installation must verify/back up installed preimages and
perform transactional rollback; no live files were changed here.
