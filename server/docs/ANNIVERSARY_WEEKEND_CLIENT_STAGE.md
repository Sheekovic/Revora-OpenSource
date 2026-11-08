# Anniversary weekend catalog candidate

The private candidate updates item 3001172's description to match the approved
[weekend event](ANNIVERSARY_WEEKENDS.md): level 140, twice reborn, VIP6+, the
Anniversary Envoy at Twin City (323,307), Saturday/Sunday Pacific admission,
15,000 spirits, the complete prize, bound P7 for new event beads, continued
weekday completion and the shared daily Spirit Ball allowance.

`tools/ClientPatches/prepare_anniversary_weekend_catalog.py` composes from the
reviewed Sword Master candidate and preserves all inherited Anniversary,
Magnolia, Summer, Labyrinth, Dis City and other quest content. Only field 53 of
3001172 changes. Item identity, category, prices, statistics, binding fields and
all other records remain unchanged. The quest journal is carried byte-for-byte.

Inputs include exact hashes for the installed server/client catalogs, composed
Sword Master catalogs/journal and approved genuine DX9 executable. Preparation
rechecks every source before atomically moving its temporary tree to the private
destination and refuses to overwrite an existing candidate.

Private directory: `obj/chi-refill-validation/anniversary-weekend-catalog/`.

| Output | SHA-256 |
| --- | --- |
| server/database/itemtype-new.txt | `0319015be2ce7b8fbd88c0146022f3d9056e0945d9e45c2f37ad4a2aac2a232a` |
| client/ini/itemtype.dat | `6e2b38adbb5caed754a35d7e7c7ddea646d57ac17289ed7c7d73d485bc70f730` |
| client/ini/Questinfo.ini | `09dd2f2d01d5b775696f1d532eaeb0a2854aeb3adc024ec6a00e216e2b8aaf82` |

Three synthetic tests pass: field scope and unrelated bytes, encryption round
trip, idempotence, and missing/duplicate/changed/malformed input rejection. A
separate whole-file audit compared 31,399 server rows and 31,371 decoded client
rows against their composed preimages. Both differed only at `3001172:[53]`.
The journal and all source/output hashes were checked independently afterward.

The candidate is **unsigned, uninstalled and unpublished**. No installed client,
live server or production database was changed. Genuine-client tooltip length,
NPC presentation, dialogue and earned reward acceptance remain open. Installation
must use reviewed exact preimages, backups and transactional rollback; this
private catalog preparation is not proof of native or live availability.
