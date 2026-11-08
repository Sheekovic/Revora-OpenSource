# Revora new-character entry

Current implementation: see [starter integration](REVORA_STARTER_INTEGRATION.md).
The September25 undefined-contents notes below describe the earlier retirement
batch; the October3 integration supplies the reviewed rewards and claim handlers.
Client/native acceptance and live deployment remain pending.

Owner policy, 2026-09-25: stop granting legacy packs 727026 and 723753; new
characters start directly in Twin City, map 1002 at (327,303). The owner will
specify RevoraStarterPack contents later.

Character creation writes map and coordinates explicitly in the same transaction
as the character, account pointer and creation receipt, rather than inheriting
the database's old spawn defaults. The creation projection uses the same values.
Existing characters keep their saved positions; no shared map files are deleted.

New creation receipts have revora_starter_pending=true. This is a durable future
entitlement, not a usable item or a claim that rewards have been delivered. No
item ID, client name/icon, rewards, claim handler or patch is invented before
contents are defined. Existing receipts default to false; a retroactive giveaway
requires a separate policy. Repeated login does not consume this entitlement.

The legacy packs_issued field now also means that legacy grants have been retired.
Its owner/session-locked transaction creates no items and does not require catalog
entries or bag space. It retains the existing class-equipment prerequisite and
failure/reconnect safeguards. Basic class equipment, starting currency and VIP
policy remain as before. Already-owned legacy packs and their chains remain usable;
no existing inventory is purged.

Source and isolated validation only. No live database mutation, deployment,
restart or client patch was performed for this batch. Native entry presentation
remains pending.

Validation: isolated Debug/x86 build and 407 purse/session/creation review checks
pass. Coverage includes exact saved and initial projected spawn, pending custom
entitlement, account/creation races, no legacy items after repeated login,
precommit rollback, postcommit reconnect and all nine supported class kits.
Unrelated owner edits were present in the build. The login fixtures use controlled
session/entity hooks; this is not genuine-client world-entry acceptance.
