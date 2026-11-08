# Character Creation and First World Entry

## Proven VPS baseline

The single owner run on 2026-08-29 conclusively reached the genuine patch-5165 character-creation screen. The approved configuration-only client completed remote Account `276/1086`, encrypted success `32/1055`, Game DH, Blowfish activation, encrypted `28/1052` plus `TQClient`, atomic handoff consumption, and encrypted `1004/2101 NEW_ROLE`.

Two owner attempts were correlated separately. Attempt 1 (client PID 28064) reached `NEW_ROLE` at 11:57:40 UTC and stopped at the registration boundary at 11:57:55. Attempt 2 (client PID 3796) reached `NEW_ROLE` at 12:00:51 and stopped at the same boundary at 12:01:12. In both attempts the client displayed its normal loading surface followed by `Error: Disconnected with game server. Please login the game again!`.

The database contained neither a case-insensitive `SHEEKO` row nor any character owned by the throwaway account. No creation transaction reached commit or rollback. Both server sessions were classified `protocol-validation` before `CharacterRegistration1001Validated`, so the second `NEW_ROLE` was correct for the unchanged zero-character database state.

The first root cause was Revora's treatment of bytes 36–51 as a required second password. Corrected owner attempts then proved valid 112-byte encrypted frames with declared `104/1001` and `TQClient`, but still failed before `CharacterRegistration1001Validated`. Exact disassembly resolved the remaining predicate: at `0x49198e` and `0x491990`, the client pushes literal address `0x68f370` for both the Account and reserved arguments; that address begins with NUL. Revora's `ReadFixedAscii(..., "account")` rejected the exact empty Account field before decoding the name or inspecting any later field.

The corrected parser now treats both empty fields according to their exact wire roles. The Account field must be the exact empty representation emitted by this build. The reserved field is copied only into a zeroable sensitive buffer and is never authentication authority. Identity and ownership remain bound to the cryptographically random, digest-stored, atomically consumed 64-bit handoff and its Account word.

Sanitized ordered events were `Account1086Validated`, `Account1055Sent`, `GameConnectionAccepted`, `Game1052Validated`, atomic handoff consumption, `GameNewRoleSent`, pre-persistence protocol rejection, and connection disposal. No credentials, packet payload, ticket, owner address, DH private value, shared secret, or Blowfish key is retained.

## Exact registration evidence

The approved exact client disassembly at `0x56b403–0x56b550` constructs a 104-byte packet `1001`. Its exact caller at `0x491991` passes empty Account, UI character name, empty reserved field, mesh, profession, and the Account word in that order. The packet is followed by `TQClient`, producing 112 encrypted wire bytes. Comet independently reads account/name and skips bytes 36–51 entirely. COPS v7 and Redux corroborate the major offsets; Redux has no discoverable license and remains behavioral evidence only.

The exact mesh derivation produces `1003`, `1004`, `2001`, or `2002`. Small Male is `1003`. The exact class jump table produces `100`, `10`, `40`, `20`, or `50`; Male Taoist is `100`. The earlier allowlist incorrectly included 30 and omitted 50 and is corrected. Revora additionally restricts initial names to 3–15 ASCII alphanumeric characters, reserves GM/Admin-like names, enforces case-insensitive database uniqueness, requires the exact empty Account field and matching Account word, and zeroes the reserved field.

Registration success is packet `1004`, channel `2100`, word `ANSWER_OK`. The former reconnect requirement came from older emulator behavior, not the exact 5165 client, and is frozen as rejected. In the approved client hash, the dispatch path at `0x4bcbc1` compares `ANSWER_OK`; success falls through `0x4bcbce` and calls `0x4c423f`, which initializes the continuing Game/loading state. The branch then presents the loading surface and returns to the existing Game message loop. It contains no `closesocket`, Account reconnect, second DH, or second `1052` operation. The alternate `NEW_ROLE` comparison begins at `0x4bcbfa` and is not the success path.

The observed post-registration disconnect was server-authored: `RunCharacterRegistrationAsync` returned immediately after sending `ANSWER_OK`, after which `RunGameAsync` disposed the socket. Revora now keeps the same authenticated socket and DH/Blowfish stream, binds the newly committed numeric Character UID to the already authenticated immutable AccountId, and promotes `RegisteringCharacter → CharacterCommitted → EnteringWorld → InWorld`. It sends `1006` directly after the registration acknowledgement and completes exact-build action `74/10010` plus `1110` on that connection. A post-commit world failure preserves the character for safe later recovery; it never repeats the commit or UID allocation.

## First entry sequence

For an existing character, Revora sends `1004/2101 ANSWER_OK` followed by character information `1006`. For a just-committed character, channel `2100` has already advanced the exact client, so Revora sends the same `1006` without a redundant entrance acknowledgement and without another authentication or cryptographic transition. The 2026-08-29 genuine reconnect proved character lookup but exposed that the first encoder used the older 5065 string offset. It declared only 80 bytes for `SHEEKO` and placed its packed strings at offset 71. The exact 5165 constructor at `0x592056` registers its string pack at packet offset `0x57` (87), while the handler at `0x592135` reads fixed fields through offset 86. The corrected encoder emits a 100-byte packet for `SHEEKO`, with two packed strings at offset 87 and spouse `None`. A byte-independent exact-client model rejects the former 80-byte layout.

Only after consuming the complete `1006`, exact-client code at `0x5925c2` calls builder `0x54d9f8`, which constructs the 32-byte general-action packet `10010` with action `74`. The genuine production client emitted that exact declared length and ID. For action `74` (enter map), Revora returns the same exact-build packet generation with authoritative starter position and map document, followed by the 16-byte map-info packet `1110`. Exact receive branch `0x54e127` consumes those values and begins actions `75`, `76`, `77`, and `78`; COPS v7 independently corroborates the semantic action chain and response-before-map order, although its older packet generation differs. The bounded starter state is map `1002`, document `1002`, coordinates `430,378`, direction `0`, and map flags `0`.

The latest genuine run is recorded separately in [the world-initialization evidence](evidence/CHARACTER_WORLD_INITIALIZING_20260829.md). It committed `SHEEKO` (not the historical `SHEEKO2`) for `vpsowner`, UID 1, and proved that the reconnect path did not send `NEW_ROLE`.

## Identity and ownership invariants

Authentication resolves a normalized unique username to an immutable internal
`AccountId`. The consumed handoff carries that `AccountId`; the wire Account
word binds the Game connection to the handoff. Existing-character lookup is
only `game.characters.account_id = authenticated AccountId`. It never joins on
username or character name.

Each character has its immutable UUID persistence identity and numeric legacy
UID. Packet 1006, general actions, entities, and world state use the numeric
legacy UID. The display name is validated and case-insensitively unique, but it
is neither a credential nor an ownership key. The exact packet-1001 Account
field remains zero and is not replaced with a username or character name.

Disposable integration coverage creates and enters characters named `SHEEKO`,
`SHEEKOVIC`, and `Hero7x`. It proves that name variation does not alter
AccountId ownership, handoff validation, Account-word validation, UID
allocation, reconnect lookup, 1006 identity, or map entry. It also rejects
cross-account duplicate names, foreign character UIDs, duplicate Create
allocation, and proves account/display-name changes preserve immutable
ownership and UID.

This milestone stops after the first visible world frame. Combat, inventory, NPC, guild, trade, and unrelated systems remain out of scope.

## Source provenance

| Source | Revision | License/use |
|---|---|---|
| `conquer-online/comet`, branch 5065 | `cc9c788a27fbace85325955a67b96b4e8e43843e` | BSD 3-Clause; protocol corroboration |
| `conquer-online/servers/cops-v7-emulator` | `84919e25e79eded39314ea50bfd787427eadd59f` | BSD-style 4-clause; protocol corroboration with required acknowledgement |
| `conquer-online/redux` | `383c0f136e80d38475aa757b2f8a1fc2c24c6831` | no license found; behavioral evidence only |
| Approved configuration-only client | `902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f` | static and genuine owner-runtime evidence; never committed |
