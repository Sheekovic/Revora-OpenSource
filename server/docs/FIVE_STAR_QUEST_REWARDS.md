# Five-star quest reward catalog evidence

Read-only audit on 2026-09-21 of the actual runtime `database/coat_storage_type.txt`
and `database/itemtype-new.txt`, compared with the installed 6711 client
`ini/coat_storage_type.dat` and `ini/itemtype.dat`. The top-level runtime coat
file is not the loader's source. `StorageItem.Load` reads star column 7 and
category column 2; category 1 is garments, category 2 is mount armor here.

Run `python tools/audit_five_star_rewards.py --output docs/research/FIVE_STAR_REWARD_CANDIDATES.json`.
The audit decodes client catalogs in memory, verifies cipher round trips, rejects
duplicate identities, and records SHA-256 provenance. It writes only derived
IDs, classifications and exclusion reasons, never decoded client catalogs.

The server rarity list contains 86 five-star entries. Only **32** pass all four
catalog checks: 12 garments and 20 mounts. Every accepted entry has matching
five-star rarity and category in both rarity catalogs, plus matching item ID,
name, profession, proficiency, level and gender in both item catalogs. The
remaining 54 are excluded for missing items or disagreements. A rarity-only list
would therefore admit unusable or mismatched rewards.

See [derived results](research/FIVE_STAR_REWARD_CANDIDATES.json) for every ID,
exclusion and input hash. These are candidate identities, not new reward grants.

## Next implementation

Use a fixed verified identity or a versioned verified pool in the quest's actual
completion transaction. ArmorofHolyLight 187405 and ButterflyRose 193625 are
verified five-star garments with no catalog class/level/gender restriction.
SomersaultCloud 200449 is a verified five-star mount armor; its proficiency field
is 1, so its ordinary equipment/wardrobe route needs explicit acceptance.

First rebirth now awards ArmorofHolyLight through its owned rite transaction
([evidence](FIRST_REBIRTH_COMPLETION.md)). Power Arena now awards SomersaultCloud
through seventh-floor completion, including equipment/item reload and wardrobe
checks ([evidence](POWER_ARENA.md)). These assignments do not validate every
candidate or complete genuine-client acceptance.
Retain existing paid/completed receipts, permanent unbound item metadata, and
ordinary-character ownership checks. Verify immediate updates, wardrobe storage,
reload, rollback, replay and title eligibility as applicable. Catalog identity
alone does not prove any of those behaviors or rendered five-star presentation.
No active client, server configuration, production database or live process was
changed by this audit. Native review and deployment remain open.
