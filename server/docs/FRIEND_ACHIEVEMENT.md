# Mutual friendship and achievement 10123

2026-09-15, M6.31, based on `6187b86`. Implemented and tested in an isolated
database schema; genuine 6711 acceptance and live deployment remain pending.

Accepting a valid friend request now saves both directions and achievement
10123 for both ordinary authenticated players in one database transaction.
The old handler allowed unsolicited acceptance and changed both live lists
before two independent inserts. Neither sending a request nor loading a legacy
friend row now establishes completion evidence.

## Evidence and rules

The installed achievement catalog identifies 10123 as adding a friend.
`6711/ini/Achievement.dat` SHA-256:
`DF78DD498353B0B089F9B4228CADA5D79F4547AAE7B705349816671B2C29EBFC`.
The [protocol record](PROTOCOL_RESEARCH.md) distinguishes inherited 1019/2071
source layouts from native capture evidence. Names and peer metadata are supplied
by server state. The requester/receiver information now identifies both players
correctly, and outgoing names are bounded to their existing sixteen-byte slot.

Requests belong to the exact sender and receiver sessions and entity objects.
Revora policy allows two minutes to accept, one outgoing request per second,
sixteen pending incoming requests and fifty durable friends per player. These
limits are implementation policy, not newly verified original Conquer rules.
Repeating a pending request does not extend its expiry. Both canonical sessions
are rechecked after acquiring database owner locks, including expiry after waits.

Both character locks and database rows are acquired in UID order. The transaction
inserts any missing direction, records one active consent per unordered pair,
and records both completion receipts. It checks affected rows and persisted
achievement evidence before commit. Only then does it replace the live lists
and refresh achievement/title state. Repeated and crossed accepts cannot double
award. There is no verified native acceptance nonce: a packet naming a peer can
only be matched to the current owned pending request, not distinguished from an
arbitrarily delayed identical packet against a later new invitation.

Removal transactionally deletes both directions and closes active consent,
including for an offline peer. Earned achievement evidence remains permanent.
It clears pending requests between those current sessions. The real database
loader rebuilds the list; a second load after login registration and post-commit
resolution of online peers cover removal overlapping entry into the game pool.
Loads never grant historical achievements. A newly accepted request may repair
a one-sided legacy pair while preserving its stored message. Duplicate legacy
directions or an inconsistent active receipt fail closed for reviewed repair.

## Validation and deployment

The read-only public database inspection found zero friend rows, duplicate
directions, missing reverse rows or self references. This is a snapshot, not a
promise about later live activity. The ignored x64 inspection executable was
hashed before execution:
`07DD38D6F4CDD176B14CC3398AAC8BDFE2667B04BF2EF98DF51BEA9EBEA397E1`.
It persisted aggregate/schema evidence only, with no player identities or secrets.

Validation uses ordinary non-GM synthetic players and isolated tables:

- 43 friend checks cover unsolicited/third-party/replayed/crossed accepts,
  immediate achievement/list state, reconnects, offline removal, malformed
  frames, identity fields, bounded names, expiry after database lock waits,
  duplicate-request expiry, capacity, throttling and legacy preservation.
- Injected failures on either friend insert, consent, achievement and deletion
  prove transaction rollback, no premature cache installation, stale-session
  fencing and recovery. Silently skipped inserts/deletes are also rejected.
- 452 EXP, 74 progression, 91 title and 89 auction checks plus 16 source/overlay
  audit checks cover related persistence and achievement behavior: 765 total.

Final isolated `Revora.exe` SHA-256:
`5526CC1E74A41B5F17EEB3670EF99BB08376FAB6C672E41C5A554D442C0802DA`.
The existing legacy build configuration is retained. Startup creates the consent
table and friend lookup index; no public schema or live binary was changed here.
Before deployment, retain the normal database/binary backup and verify two
ordinary genuine 6711 clients can request, accept, remove, reconnect and observe
the achievement. No proprietary client content is included in this commit.

Trusted producer coverage becomes **98/337**, with 239 producers still missing.
This is source coverage; it does not complete the per-character 320 route,
mentor/apprentice admission achievements or trade-partner consent.
