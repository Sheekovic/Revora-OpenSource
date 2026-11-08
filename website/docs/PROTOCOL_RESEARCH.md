# Patch 5165 Protocol Research

## Native Ninja evidence (2026-08-30)

The approved executable is `Conquer.exe` SHA-256
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`.
All inspection in this section was static. No client, donor binary, DLL, or
legacy tool was executed.

Official TQ material corrects the commonly repeated release boundary: the
Ninja class was released in patch **5089**, not 5095. The official patch note
at `https://co.99.com/content/2008-12-19/20081219015941741.shtml` names Ninja,
Veils, Hoods, Vests, and Katanas. The official update chronology at
`https://co.99.com/guide/info/coupdates.shtml` places Ninja in December 2008
and Monk in December 2010. These dates are historical corroboration; the
approved executable and its data remain the compatibility authority.

### Approved-client records

| Evidence | SHA-256 / location | Confirmed fact |
|---|---|---|
| `ini/ProfessionalName.ini` | `4fd34e660cdc2a15f466758fc2b910db8aa17aa9e81d283c33b898ffb80548a8` | Professions 50–55 are `InternNinja`, `Ninja`, `MiddleNinja`, `DarkNinja`, `MysticNinja`, `NinjaMaster`. |
| `ini/selectCharacter.ini` | approved client tree | `vocation8` and `vocation9` are male and female Ninja creation choices; `info4`/`infoII4` contain the Ninja descriptions. |
| `ini/itemtype.dat` | `29c9fc7ba5da97fa6d7c77921107b0b981725ee375e48d216b662b4b32424353` | 273 Katana definitions in the sparse quality-coded range 601000–601339; all require profession 50. Ninja Veil 112000–112109, Hood 123000–123109, and Vest 135000–135109 records also require profession 50. |
| `ini/MagicType.dat` | `a839525c9c4842c5bddb1b5d2b4536c8ffda164150093783df08df6d5bf28642` | Native records exist for 6000 Twofold Blades, 6001 Toxic Fog, 6002 Poison Star, 6003 Counter-Kill, 6004 Archer Bane, 6010 Shuriken Vortex, and 6011 Fatal Strike. |
| `ini/3dmotion.ini` and `c3/0002/601/*` | approved client tree | Katana motion families 904, 905, 909, 910, 912, 913, 915, and 916 exist for the approved body meshes. |
| `sound/Ninjamagic-*`, `c3/effect/other/Ninja120/*`, `pic/Ninja/*` | approved client tree | Skill-specific sound, effect, and icon resources exist. Filenames alone are not used as protocol evidence. |

The encrypted TQ data files were decoded using the documented seed-9527
128-byte Microsoft-LCG XOR/rotate transform implemented in the bounded
`Revora.ClientEvidence tq-file-decrypt` command. The decoder has a 16 MiB
input limit and zeroes plaintext memory after writing. The ignored outputs are
`itemtype.txt` SHA-256
`e15b066b324cf9eac62a7137f96026ec1e12b12aea516b1605fce99fafd69cc4`
and `MagicType.txt` SHA-256
`ad1de31c1de6d0b2ca14e6b5c9f0f0513a57d6d11a738905494c99ef30009560`.
No decoded client data is committed.

### Skill packet instructions

The exact client constructs `32/1022` at VA `0x54a5f6`: timestamp `UInt32`
at `+4`, sender UID at `+8`, encoded target at `+12`, encoded X/Y at
`+16/+18`, action `UInt32` at `+20`, and encoded skill/level at `+24/+26`.
Action 24 selects magic/skill use. The authorized owner 5165 source independently
uses the same fields and reversible transforms. Revora treats the authenticated
session UID, not the packet UID, as authority.

The exact `1105` receive handler at VA `0x58e91c` consumes actor UID at `+4`,
X/Y at `+8/+10`, skill ID/level at `+12/+14`, one-byte target count at `+16`,
and 12-byte target rows starting at `+20` (`UInt32` target, `UInt32` damage,
four reserved zero bytes). It has explicit behavior branches for 6001, 6002,
6004, 6010, and 6011. Packet `1103` carries skill experience at `+4`, skill ID
at `+8`, and level at `+10` in a 12-byte declared packet. These layouts are
**Confirmed static client evidence**; genuine encrypted Ninja traffic remains
required before production activation.

The same exact-client `32/1022` constructor carries ordinary interaction
actions without the skill obfuscation. For normal melee action `2` and ranged
action `28`, sender UID is `+8`, target UID is `+12`, claimed target X/Y are
`+16/+18`, and the `UInt32` action is `+20`. The authorized owner 5165 source's
reviewed `AttackPacket` response independently emits declared `32/1022` with
timestamp `+4`, authoritative attacker `+8`, target `+12`, target X/Y
`+16/+18`, action `+20`, server-calculated damage `+24`, and reserved zero
`+28`. This ordinary-combat layout is **Strong static support**: the exact
client instructions establish the common fields, while a genuine owner-client
normal-attack exchange is still required before global activation.

### Skill data boundary

The client records confirm the following bounded semantics: Twofold Blades
requires dual Katanas and uses 30 stamina; Toxic Fog uses 50 stamina and cannot
reduce the target below one HP; Poison Star uses 50 stamina and suppresses item
use; Archer Bane uses 50 stamina and cancels Fly; Shuriken Vortex and Fatal
Strike are XP skills, with Vortex lasting 20 seconds and Fatal Strike lasting
30 seconds and targeting monsters. Exact damage, proc, cooldown, and status
values remain data-driven from reviewed `MagicType` fields. A later-client or
donor formula is not promoted merely because its skill name matches.

### Reference pins and use boundary

| Source | Commit | License | Target/use |
|---|---|---|---|
| `https://github.com/conquer-online/comet` branch `5187` | `18446a8a54b06e9f2c0d69d76f7fec46254910ab` | BSD-3-Clause | Adjacent protocol/reference only; Ninja enums exist but no complete skill runtime. |
| `https://github.com/conquer-online/comet` branch `5065` | `cc9c788a27fbace85325955a67b96b4e8e43843e` | BSD-3-Clause | Earlier protocol architecture corroboration only. |
| `https://github.com/zinja-coder/OpenConquerEmulator5517` | `5bb7623a8ae2d2435537095822baaeae37ff2680` | README claims MIT; no LICENSE file found | 5517/5518 research only; licensing unresolved and no reusable Ninja combat implementation found. |
| owner-provided ReVora source | retained owner archive | Owner-authorized; bundled third-party rights not assumed | Primary 5165 behavior evidence only; unsafe infrastructure is not copied. |
| COPS v7 | `84919e25e79eded39314ea50bfd787427eadd59f` | BSD notice with advertising/attribution clauses | Secondary 5065 protocol corroboration only. |

World Conquer Source V2 targets 5517 but no independently verified reusable
license was found, so it was not downloaded or copied. HeroesCoServer and Redux
remain unlicensed research references only. Public-source expressions were not
copied into Revora.

## Exact-client clock, ping and FPS overlay

The approved executable SHA-256
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`
contains the overlay format at virtual address `0x67d95c`. Its renderer at
`0x4bb0c9` supplies the fields independently:

- `0x51cc53` returns a client-maintained epoch-seconds clock. It is initialized
  to zero at `0x512f02/0x512f08` and advances from `timeGetTime()` until packet
  `1033` initializes it. Revora previously sent no `1033`, so `localtime()`
  rendered Windows monotonic uptime as a date after the Unix epoch. This is the
  proven source of the observed 1970 date; no timezone conversion occurred.
- The `36/1033` receive branch at `0x590227` consumes action zero at offset `4`
  and seven little-endian `UInt32` calendar fields: year since 1900 at `+8`,
  zero-based month at `+12`, one-based day-of-year at `+16`, day at `+20`, hour
  at `+24`, minute at `+28`, and second at `+32`. It does not consume Unix
  seconds, Unix milliseconds, or .NET ticks. The owner-authorized 5165 donor's
  `Packets.Time()` independently emits the same layout.
  The deterministic 2026-01-15 10:20:30Z fixture, converted with the runtime
  Cairo rules to 12:20:30, serializes before footer/encryption as
  `24000904000000007E000000000000000F0000000F0000000C000000140000001E000000`.
  The independent exact-client parser reads that byte sequence as length 36,
  ID 1033, action zero, year 2026, month 1, day-of-year/day 15, and 12:20:30.
- The ping field at object offset `0x2f88` is assigned at `0x537da5` as unsigned
  `timeGetTime()` at response receipt minus the `UInt32` at packet-1009 offset
  `+16`. The client therefore starts and ends the RTT measurement on the same
  monotonic Windows clock. The server must echo the client send tick unchanged.
  Revora had copied the donor's flawed `+10..19` behavior as a fixed `+15` and,
  before reaching that branch, performed a PostgreSQL player-snapshot read.
  A deterministic pre-footer request/response echo is
  `1C00F10302000000000000001B000000F8FFFFFF0000000000000000`:
  length 28, ID 1009, action 27, and the unsigned client tick `0xfffffff8`
  unchanged at offset `+16`.
  A genuine session on 2026-08-30 produced 115 measured receive-to-send audit
  intervals: minimum `93.319 ms`, mean `119.674 ms`, maximum `331.490 ms`, with
  a keepalive interval mean of `10.042 s`. `TCP_NODELAY` was already enabled.
- FPS is neither a server tick nor an overlay refresh value. At `0x4bb0c9` the
  render path computes `1000 / unsigned(timeGetTime_now - previous_tick)` and
  averages it with the previous sample. The main-loop call site is `0x4bce4d`.
  It does not use the server clock, ping field, wall-clock time, or packet data.
  Its initial zero smoothing state can display `10` after a 50 ms first sample;
  whether a persistent value of 10 represents a genuine client rendering
  bottleneck requires a real Windows multi-sample observation and must not be
  inferred from a single overlay capture.

Revora's correction is initially bound to the existing immutable item/economy
canary identity. It sends the exact `1033` calendar fields using the runtime
`Africa/Cairo` timezone rules, echoes action-27 timestamps without database
work, and records monotonic server processing duration after the response send.
It does not change TCP settings, client bytes, renderer pacing, or stable patch
manifests. Confidence is **Confirmed static evidence** for field layouts and
**Confirmed genuine runtime evidence** for the pre-fix server processing path;
post-fix overlay acceptance remains gated on a genuine test-channel run.

The automatic fixed-address UI-bootstrap experiment is frozen after two reproducible Owner Test
4j failures. Both exact clients reached valid startup/trace stages and wrapper rejection, then
froze at the loading surface and closed without Account state or a socket. This is not an Account
server failure. The current gate is the manual playable baseline documented in
[MANUAL_PLAYABLE_BASELINE.md](MANUAL_PLAYABLE_BASELINE.md).

The exact reviewed client's static `CMD_GAMESTART` internal-message chain and Owner Test 4 transition hypothesis are documented in [AUTOMATIC_ACCOUNT_BOOTSTRAP.md](AUTOMATIC_ACCOUNT_BOOTSTRAP.md). The call chain remains static evidence; execution of the callback patch is **Unproven** after Test 4. Raw bytes `15 94 56 65` remain encrypted initial Account bytes; their direct little-endian interpretations are rejected.

Owner Test 4 did not produce an Account connection. Static callback/stub/message execution must not be promoted from that result. Owner Test 4b isolates timing by posting only the statically identified `0x480` once after the exact client HWND is continuously visible, responsive and PID-owned; its design and evidence classification are in [OWNER_TEST_4B_EVIDENCE.md](OWNER_TEST_4B_EVIDENCE.md).

Owner Test 4d R1 launched the exact diagnostic client but returned an all-false breadcrumb
sequence. Because that build lacked an independently proven client control breadcrumb, the
result is inconclusive about whether the candidate hook executed. Owner Test 4e first traces
startup and top-level-window creation, then progressively later fixed stages. See
[OWNER_TEST_4E.md](OWNER_TEST_4E.md). No new packet claim is derived from this instrumentation.

## Client static evidence (2026-08-28)

The quarantined evidence tree is strongly consistent with the 5165 era (`version.dat` equals `5165`, 2009 timestamps, and an official-signed core executable), but three malware detections disqualify it from execution or distribution. Its 128 SWFs and `Conquer.exe` ActiveX strings establish that the login presentation uses an embedded Flash control. They do not confirm packet layouts, password cryptography, endpoint configuration, or handoff behavior. No protocol claim was promoted to Confirmed from this evidence.

Test 3 static analysis of the clean signed reconstruction narrows the UI boundary. The exact login SWF contains animation only; native `DlgLoginFlash` control flow owns credential buffers, group/server selection, Account endpoint lookup and game-start dispatch. Test 3 helper input failed; only manual in-game input initiated the genuine Account connection.

The repeated manual-login ciphertext prefix `15 94 56 65`, exact-client cipher initializer/transform, and exact request builder corroborate the initial Account header. Initial transport decryption yields `14 01 3e 04`: length `276`, ID `1086`. No server seed precedes this frame. The client builder places username at offset 4, a 128-byte password area at 132 and realm at 260; its first 16 password bytes pass through a 12-round RC5 routine. Credential content was neither captured nor persisted. Owner Test M1 confirmed the response ID `1055` and 12-byte controlled-error form bidirectionally; the genuine client displayed `Failed to login: Invalid account or password!`.

Exact-client response handling at `0x58963f` reads successful-1055 words at offsets 4 and 8, address bytes at 12–27, and a 32-bit port at 28. Its Game-connect constructor at `0x588cc7` writes length 28, ID 1052, the two handoff words at 4/8, a 16-bit field at 12, ten bytes at 14, and a 32-bit value at 24.

M2-H1 triangulates the immediately preceding Game DH/Blowfish transition across pinned Comet 5065, COPS v7 5065, Redux 5065, and the exact client parser. The exact matrix is in `research/protocol/GAME_HANDSHAKE_SOURCE_MATRIX.md`. The only wire-critical disagreement was COPS' 56-byte key clamp; exact-client instructions at `0x5a0543–0x5a054d` cap at 72 bytes, agreeing with Comet and permitting the complete normalized 64-byte DH secret. The canonical candidate is now genuinely proven: the 2026-08-29 owner run completed remote Game connection, DH reply validation, shared-secret derivation, Blowfish activation, genuine encrypted `28/1052` plus `TQClient`, and atomic handoff consumption before displaying the genuine character-creation screen.

The same run proved encrypted `1004/2101 NEW_ROLE`. Its later visible disconnect was caused by Revora's deliberate five-second evidence stop; no post-`NEW_ROLE` packet was read. Exact-client static analysis now confirms the `104/1001` registration builder and its offsets. The former "protocol-required reconnect" assumption is rejected: in the approved client, the `1004` handler compares `ANSWER_OK` at `0x4bcbc1`, calls the registration-success transition at `0x4bcbd0`, and then continues its Game-session/loading path. There is no `closesocket`, Account reconnect, second DH, or second `1052` on that success branch. Revora's own registration method was the component that returned and disposed the Game socket. The production path therefore promotes the same authenticated cipher/session directly into `1006` and the exact-build action-74/`1110` world-entry exchange, as documented in [CHARACTER_CREATION_AND_FIRST_WORLD_ENTRY.md](CHARACTER_CREATION_AND_FIRST_WORLD_ENTRY.md).

The 2026-08-29 15:45 UTC genuine production run resolves the action-generation conflict. After accepting 1006, the exact approved client emitted declared `32/10010`, followed by a separately framed `304/1134`. Revora ignored the first frame because it still required the older `24/1010` shape, so it never sent action 74 or map 1110. Exact instructions in builder `0x54d9f8` write ID 10010 and the 32-byte layout: UID at 4, data at 8, timestamp at 12, action at 16, direction at 18, X/Y at 20/22, and zero reserved bytes at 24-31. The post-1006 caller at `0x5925c2` supplies action 74. The receive branch at `0x54e127` consumes authoritative data, direction and coordinates, initializes the map, and begins the 75/76/77/78 initialization chain. This is Confirmed static evidence corroborated by the genuine runtime length/ID; the former 24/1010 claim is rejected for this exact build. Packet 1134 is a quest-status family frame and is not reclassified as an action.

The later genuine SHEEKOVIC map-1002 session resolves the first gameplay frame. The exact client sent valid declared `20/10005` frames, accepted authoritative responses, persisted position, and restored it after reconnect. Exact builder instructions at `0x58198f–0x581a31` construct length 20, ID 10005, direction `UInt32` at 4, character UID `UInt32` at 8, movement mode `UInt32` at 12, and tick `UInt32` at 16; the receive handler at `0x581a57–0x581d5c` validates the UID and applies the direction/mode. COPS v7's licensed movement and view-set implementations independently corroborate the direction-delta, authenticated-UID, echo/broadcast, map-cell, and nearby-view semantics. Packet 10005 and this layout are Confirmed for the exact build. The first genuine jump then exposed the exact action-137 tail: constructor `0x54da8c`, called at `0x517202`, emits declared `32/10010` with packed target at 8/10, source at 20/22, zero at 24 and sentinel `0xffffffff` at 28. Revora's synthetic zero-tail fixture rejected that frame before semantic validation. The corrected codec preserves malformed/foreign-UID failure and applies non-disconnecting action-108 correction for invalid semantic destinations.

The portals/maps/NPC slice is based on exact-client constructors and handlers, not
on donor packet assumptions. Portal activation uses action 85; the authoritative
map transition uses action 86. NPC spawn is declared `28/2030`, activation is
declared `16/2031`, and dialog messages use packet `2032` with option/index at 10,
action at 11, and a string pack at 12. The exact client and the two reviewed sources
agree on these layouts. Runtime owner-client acceptance remains pending, so the
layouts are **Strong static support**, while map/proximity/security policy is a
Revora server rule rather than a client claim.

The genuine NPC Goodbye attempt on 2026-08-29 confirmed the first record as
declared `16/2032`, option `255`, action `101`; Revora accepted it and then
closed with `protocol-validation` before recording the close record. Complete
constructor analysis corrects the former 15-byte inference: the action-102 call
at `0x41b265` uses constructor `0x54497a`, whose packed-string serializer
`0x599659` always accounts for the one-byte collection count. An empty
collection therefore yields `1 + 15 = 16` declared bytes, with option `0`,
action `102`, and zero bytes 12–15. This is **Confirmed static evidence** and is
corroborated by the genuine immediately preceding frame and owner-source string
pack offsets. The 15-byte fixture is rejected as an incorrect synthetic model;
encrypted production-reader fragmentation/coalescing fixtures now exercise the
16-byte sequence end to end.

The subsequent production run with pre-parser auditing supersedes the assumed
action-102 runtime sequence. Twice, the genuine client emitted two identical
complete `16/2032` records with option `255`, action `101`, and an all-zero
packed/reserved tail. The first was accepted; the second was rejected only by
server dialog state after the first cleared `ActiveNpc`. No partial frame,
cipher/footer failure, or action-102 record was observed. Duplicate Goodbye is
therefore a **Confirmed genuine runtime behavior** for this exact client path.
Revora handles only that just-closed-dialog replay idempotently and retains
strict parsing and state rejection for unrelated dialog traffic.

The first 2026-08-30 genuine `@givemeall` attempt stopped while parsing inbound
`1004`, before command dispatch; no command audit or balance mutation occurred.
The authorized owner-source `Chat` reader consumes four length-prefixed strings
beginning at offset 25, while COPS independently confirms the four-string pack
and fixed header. Initial five-byte and four-byte reserved-tail interpretations
were disproved by later genuine attempts. The Whisper path first produced a valid
declared `13/1015`, then reached the same Talk boundary. Revora consequently
recorded only bounded `1004` structural metadata and a fixed failure category,
never chat text, while keeping a semantically rejected Talk frame from closing
the authenticated Game session. Two normal-Talk observations then resolved the
exact shape: declared `55/1004`, channel `2000`, string count four, field lengths
`9,4,0,10`, and an all-zero tail length of exactly three bytes. This is
**Confirmed genuine runtime evidence** for the approved client. Revora accepts
only no tail or this exact genuine form and restricts reviewed GM commands to
channel `2000`; channel `2001` cannot invoke them.

## Evidence method

Every claim is classified as:

- **Confirmed**: independently corroborated by a licensed source and exact-5165 evidence, or validated later against an untouched authentic 5165 client capture/behavior.
- **Probable**: consistent across adjacent-patch licensed sources and claimed-5165 text, but not client-validated.
- **Tentative**: found only in claimed-5165 material or inferred from adjacent patches.
- **Unknown**: insufficient evidence.

Unlicensed source is evidence only and is never incorporated code. Packet names are descriptive labels; numeric IDs and field offsets are facts only to the confidence stated.

## Pinned sources

| Source | Branch | Commit | License/use | Status |
|---|---|---|---|---|
| `https://github.com/conquer-online/comet` | `5065` | `cc9c788a27fbace85325955a67b96b4e8e43843e` | BSD 3-Clause; architecture/protocol reference with attribution | Pinned 2026-08-28 |
| `https://github.com/conquer-online/comet` | `5187` | `18446a8a54b06e9f2c0d69d76f7fec46254910ab` | BSD 3-Clause; architecture/protocol reference with attribution | Pinned 2026-08-28 |
| `https://github.com/DuneGameStudio/HeroesCoServer` | `master` | `008b0e997d63778204bb6bb6c91b88908d2e4892` | No license found; static research evidence only | Pinned 2026-08-28; untrusted |

Repositories were acquired as filtered bare repositories with hooks disabled and no checkout. Only selected text blobs were fetched. Bundled binary/archive blobs were neither fetched intentionally nor executed or extracted. Git blob object IDs for risky paths are recorded in `docs/RESEARCH_AUDIT.md`.

## Facts, assumptions, and unknowns

The following are facts about the pinned sources, not yet facts verified against an authentic client:

- Comet prepends a little-endian `UInt16` declared length and `UInt16` packet ID. For ordinary sends its declared length excludes an eight-byte server footer, which is appended on the wire.
- Comet 5065 decodes account request `1051` as 52 declared bytes: header, 16-byte username, 16-byte RC5-encrypted password, and 16-byte realm.
- Comet 5187 decodes account request `1086` as 276 declared bytes: username at offset 4, 16 password bytes at offset 132, and realm at offset 260. It first sends seed packet `1059` and applies seeded RC5 plus a username-derived scan-code substitution.
- HeroesCoServer accepts exactly 276 received bytes, checks packet `1086`, and uses the same three field offsets. It does not send the Comet 5187 seed packet and treats the password field as ASCII before comparing it to its database.
- Comet documents its seed packet as applying at and after patch 5174. Therefore patch 5165 should not be assumed to use the 5187 seed exchange even though HeroesCoServer claims the 5187-shaped request.
- Game connect is packet `1052` across all three pinned references. Its detailed fields are not sufficiently corroborated for 5165.
- Character creation is packet `1001` across the references. HeroesCoServer reads character name at offset 20, body at 52, and class byte at 54; Comet reads two fixed strings followed by 16 reserved bytes, mesh/class `UInt16`s, and a token. These expressions do not establish exact 5165 semantics.
- Movement changes from ID `1005` in Comet 5065 to `10005` in Comet 5187; HeroesCoServer claims `10005` with a 16-byte declared packet plus an eight-byte footer.
- Player spawn changes from ID `1014` in Comet 5065 to `10014` in Comet 5187; HeroesCoServer claims `10014` with a variable declared length of `138 + name/status length` plus an eight-byte footer.
- General action changes from ID `1010` and 24 declared bytes in Comet 5065 to ID `10010` in later families. Exact 5165 static instructions and the genuine client now prove ID `10010` with 32 declared bytes for this build; adjacent sources remain corroboration only.

- Assumption: patch 5165 lies between Comet's 5065 and 5187 branches, but protocol evolution is not assumed monotonic.
- Confirmed for the initial manual-login Account connection: the unseeded initial counter cipher, request length `276`, request ID `1086`, and the three top-level field offsets above. Runtime credential semantics beyond the 16-byte RC5 input and all authentication decisions remain unconfirmed.
- Unknown: account-to-game handoff format and replay behavior.
- Confirmed: the initial Game handshake, 28/1052, character creation/entry, map-load action, 20/10005 movement, and the relevant `TQClient` footer/encryption boundaries described above.
- Still unproven against the genuine owner client: jump action 137, 10014 nearby-player spawn acceptance, dynamic despawn/enter-range transitions, and later gameplay packet families.

## Compatibility matrix

The expanded multi-source, symbol-level evidence ledger is maintained in `PROTOCOL_EVIDENCE_MATRIX.md`. This earlier compact table remains a summary; where wording differs, the expanded matrix's conservative confidence classification governs.

The evidence matrix will be populated after static inspection. Blank cells are not compatibility claims.

## Patch-5165 item and economy campaign

The approved client (`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`) and the authorized owner source establish the following bounded runtime layouts. Client disassembly is the wire authority; donor implementations are behavior corroboration only.

| Purpose | Direction | Declared bytes / ID | Reviewed fields |
|---|---|---:|---|
| Item information | S→C | `48 / 1008` | item UID `+4`, item type `+8`, durability `+12`, maximum durability `+14`, action `+16`, position `+18`, sockets `+24/+25`, plus/bless/bound/enchant `+28..+31`, color `+40` |
| Item action | C↔S | `28 / 1009` | subject `+4`, command/item `+8`, action `+12`, timestamp `+16`, argument `+20`, reserved zero `+24` |
| Status update | S→C | `36 / 10017` | character UID `+4`, count `+8`, status `+12`, 64-bit value `+16`; reviewed statuses: HP `0`, MaxHP `1`, MP `2`, MaxMP `3`, silver `4`, CP `29` |
| Warehouse action | C→S | `48 / 1102` | NPC UID `+4`, action byte `+8`, warehouse selector `10` at `+9`, reserved zero `+10..+11`, item UID `+12`, reserved zero `+16..+47` |
| Warehouse list | S→C | `16 + 32*n / 1102` | NPC UID `+4`, count `+12`; reviewed 32-byte item rows begin at `+16` |
| Warehouse live add | S→C | `48 / 1102` | Exact-client receive handler `0x587cfd` takes action `0`; count `1` at `+12`; the standard 32-byte item row begins at `+16`. The owner donor `AddWHItem` emits this one-row list form. Offset `+9` remains zero and is not read by the client handler. |
| Warehouse live remove | S→C | `48 / 1102` | Exact-client action `2`; immutable Item UID `+12`; reserved zero `+9..+11` and `+16..+47`. |

Production evidence on 2026-08-30 showed that Revora's former live-add packet used action `1`, Item UID at `+12`, and item type at `+16`. The exact approved client action-1 branch at `0x587d5c` instead reads the row identity/type from `+16/+20`; it therefore ignored that structurally mismatched update. Closing and reopening succeeded because the full-list path already used action `0`, count at `+12`, and its row at `+16`. The corrected live-add intentionally reuses that exact accepted one-row form. The request-only selector at byte `+9` was not the rejection cause.

Reviewed `1009` actions are buy `1`, sell `2`, inventory removal `3`, equip `4`, unequip `6`, warehouse open `9`, warehouse-silver deposit `10`, and warehouse-silver withdrawal `11`. The current bounded campaign activates buy, sell, equip, unequip, warehouse open, item deposit, and item withdrawal. Unsupported item actions remain gated rather than inferred.

The encrypted acceptance fixture performs genuine DH/Blowfish negotiation and sends fragmented and coalesced `1009`/`1102` frames through the production Game reader and dispatcher. It proves initial inventory/status delivery, stable Item UID persistence, equip/unequip and calculated MaxHP/MaxMP refresh, replay-safe purchase, authoritative silver updates, warehouse persistence across a fresh handoff/reconnect, withdrawal, sale, and movement after an economy command. This is deterministic integration evidence; genuine Windows acceptance remains required before global rollout.

| Packet/purpose | Direction | 5065 ID/length | Claimed 5165 ID/length | 5187 ID/length | Evidence | Confidence | Notes |
|---|---|---|---|---|---|---|---|
| Account login request | C→A | `1051` / 52 declared | `1086` / 276 received | `1086` / 276 declared | Exact 5165 client request builder; manual-login encrypted header; Comet `MsgAccount` | Confirmed initial header/offsets for 5165 | Initial cipher decrypts observed header to 276/1086; credential content was not recorded |
| Password seed | A→C | Absent | Absent in claimed source | `1059` / 8 declared | Comet `MsgEncryptCode`; Heroes connection path | Probable absence at 5165 | Comet says feature begins at 5174; authentic-client validation still required |
| Server selection/login response | A→C | `1055` / 34 declared success | `1055` / 32 received | `1055` / 34 declared success | Exact 5165 client response matcher/error branch; Comet `MsgConnectEx`; Heroes `SendAuthentication` | Confirmed ID and 12-byte controlled-error form; success unknown | Test 3b uses only zero success field plus error code 1; no success/handoff is guessed |
| Game connect/authenticate | C→G | `1052` / codec reads 28 bytes | `1052`; length not validated | `1052` / codec reads 28 bytes | Comet `MsgConnect`; Heroes `GameWorker` | Probable ID; tentative layout | Heroes reads only 8-byte token then trusts global table |
| Character creation | C→G | `1001` / exact client constructs declared length 104 and fields through offset 59 | `1001`; account/name then skips bytes 36–51 as reserved | `1001` / codec reads through offset 59 | Exact caller `0x49171e–0x4919a5`; constructor `0x56b403–0x56b550`; Comet `MsgRegister`; four genuine owner runs on 2026-08-29 | ID, 112 encrypted wire bytes, 104 declared bytes, footer, and primary offsets confirmed | Exact caller pushes literal empty string `0x68f370` for both Account and reserved fields. Identity authority is the consumed 64-bit handoff/Account word. Mesh set is 1003/1004/2001/2002; profession jump-table set is 10/20/40/50/100. |
| Character entry/info | G→C | `1006` / variable | `1006` / variable | `1006` / variable | Comet/Heroes character-info encoders | Probable ID; tentative layout | 5065 and 5187 payloads differ materially after base stats |
| Map load/spawn action | C↔G | `1010` / 24 declared | `10010` / 32 declared + 8 footer | `10010` / 36 declared | Exact builder/handler plus genuine VPS run; licensed COPS semantic corroboration | Confirmed ID/length/layout/action 74 for exact 5165 | Genuine client emitted 32/10010 after 1006; former server awaited the wrong generation |
| Player movement | C↔G | `1005`; layout absent | `10005` / 20 declared + 8 footer | `10005`; layout absent | Exact builder/handler; genuine VPS frames; licensed COPS movement semantics | Confirmed ID/length/fields | Direction at 4, UID at 8, mode at 12, tick at 16; genuine field values were not retained |
| Nearby player spawn/update | G→C | `1014`; layout absent | `10014` / `138 + name` declared + 8 footer | `10014`; layout absent | Exact handler `0x589c9d`; licensed COPS view semantics | Strong static support; genuine validation pending | Revora emits only exact fixed identity/look/position fields plus the one-name string pack |
| Nearby player removal | G→C | action `135` on `1010` | Likely action on `10010`; unverified | action `135` on `10010` | Comet `MsgAction`; claimed switch/constructors | Tentative | Exact claimed constructor and client behavior need fixtures |

Lengths above distinguish the protocol's declared length from trailing footer bytes. “Reads through” is not a claim that the packet ends at that offset.

## NPC dialog replay and event-rate hardening

The genuine production Goodbye behavior remains the two complete identical
`16/2032`, option `255`, action `101` records documented below. Revora accepts
the first record as the dialog close and permits exactly one semantic duplicate
on that same Game session. The allowance is consumed by that duplicate and is
cleared whenever a dialog opens, NPC visibility is lost, or the player changes
maps. A third record, a different option/text payload, or a close without a
pending dialog is rejected. The independently evidenced option `0`, action
`102` close may consume the same single pending close confirmation, but does not
create an unbounded replay window.

NPC activation (`2031`) and dialog-answer (`2032`) handling share a per-session
rate limiter of 12 events per rolling two-second window. Parsing, framing,
footer checks, map/distance validation, and dialog ownership remain unchanged.
The limiter is independent of movement and jump limits and fails closed before
dialog state or server responses are changed.

## RevoraMaster progression services

The approved 5165 client hash remains
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`.
No client bytes or packet parser were changed for these services. RevoraMaster
uses the already accepted `16/2031` activation and bounded `2032` text/option,
face, finish, selection, cancel, and close flow.

The exact client `1006` handler consumes level as one byte at offset `66` and
profession as one byte at `67`. Its status-update handler accepts the owner
donor's patch-family status ordinals: experience `5`, class `7`, attribute
points `10`, level `12`, spirit `13`, vitality `14`, strength `15`, agility
`16`, rebirth count `22`, and CPs `29`. Revora uses the existing exact
`36/10017` encoder for these authoritative changes and continues to use
`48/1008` for every created item.

The authorized owner source treats 130 as the playable level ceiling: level-up
paths stop below 130 and its experience table has 129 transition entries. The
reviewed `ExpNeed.dat` has exactly those 129 little-endian thresholds. Level
130 therefore stores zero progress toward a nonexistent next level. Base
attributes are taken from the reviewed level-120 class row; the ten levels
above 120 produce 30 unallocated attribute points and do not replay per-level
rewards.

The reviewed first/second-rebirth targets are Trojan `11`, Warrior `21`, Archer
`41`, Water Taoist `132`, and Fire Taoist `142`. The owner source's first and
second rebirth methods reset the character to level 15, record prior class
lineage, and use `(level - 120) * 3 + rebirth * 10 + 45` bonus points when the
source level is at least 120. Revora adopts that bounded state transformation,
preserves existing skills/proficiencies rather than executing the donor's
destructive implicit reset, and transactionally moves only class-incompatible
equipment to reserved inventory slots.

Silver is capped at `2,000,000,000`: the owner source explicitly rejects larger
values and the genuine client represents the initial field as a 32-bit value.
CP uses the same signed-display-safe cap even though the transport status value
is 64-bit; this avoids treating transport width as proof of client UI support.
Both values remain within PostgreSQL, domain, `1006`, and `10017` bounds.

## Exact 5165 monster target identity and skill request fields

The approved client SHA-256 remains
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`.
Its entity-classification branches at `0x4c6dd0..0x4c6dd7` and
`0x4ca7df..0x4ca7f9` compare a candidate UID against hexadecimal `0x61a80`
(400000) and `0x927c0` (600000). The monster path is the strict interval
`400000 < UID < 600000`. This is static exact-client evidence. The authorized
Owner Source independently assigns monster UIDs with
`Program.Rnd.Next(400000,500000)` in `Game/Mob.cs`; it corroborates the client
classification without defining Revora's allocation mechanism.

The 2026-08-30 genuine canary session provided the runtime negative proof:
monsters sent with Revora's former `400000000+` UIDs were rendered, but the
client emitted no normal melee `1022`; skill `1022` frames reached the Host with
target UID zero. Revora now gives every reviewed generator member a durable,
unique UID in the exact-client interval and rejects outbound monster spawn
packets outside that interval.

The exact client attack constructors at `0x557b1a..0x557c40` and
`0x558c83..0x558d2d` encode target, coordinates, skill and sender/action but do
not encode a skill level. The authorized Owner Source `PacketHandling/Attack.cs`
likewise decrypts skill/target/coordinates and resolves the learned level from
authoritative character state. Genuine frames contained changing values in
bytes `26..27` for repeated skill 6001 attempts (`51489`, `59425`, `60705`,
`20257`), proving those bytes are not a stable client skill level. Revora treats
them as scratch/padding and obtains the level from PostgreSQL learned-skill
state. This change does not relax the exact declared length, packet ID, footer,
or reserved-tail validation.

## Genuine moving-monster combat and Stamina correction

The 2026-08-30 23:56–23:59 UTC `sheekoninja` run produced three accepted
Twofold Blades `32/1022` frames against monster UID `400006`, each audited with
damage `1`. Every later normal melee `32/1022`, including targets `400006`,
`400007`, and `400026`, was decoded but rejected as
`target-coordinate-mismatch`. The same run recorded 2,265 committed monster
attacks and one player death, proving that the inbound player-combat failure
was independent of monster AI scheduling.

The authorized Owner Source `PacketHandling/Attack.cs:47–130` reads the target
UID at offset 12, resolves that UID in the current map, and validates distance
using the server-owned `PossMob.Loc`; it does not use packet offsets 16–19 as
authoritative target state. Revora now treats those coordinates as a bounded
18-cell viewport claim, while current server UID, map/instance, alive state,
attackability, and range remain authoritative. Server `1022` results serialize
the current authoritative target coordinates.

Owner Source `Game/Character.cs:7879–7902` regenerates Stamina once per second
after a 500 ms movement quiet period: 3 standing or 15 sitting. Revora's former
enum state made `Moving` and `Attacking` permanent regeneration blockers. They
now decay to `Standing` after the evidenced quiet period. The encrypted runtime
fixture proves a Twofold transition from 100 to 70 Stamina followed by an
authoritative persisted `10017` update to 73. Twofold damage now applies its
evidenced level multiplier to the existing server-authoritative normal physical
damage formula rather than the former fixed base of one.

## Genuine dead-login and revive state

The 2026-08-31 00:31 UTC `sheekoninja` session sent multiple valid normal
`32/1022` action-2 requests for monster UIDs `400032`, `400037`, and `400138`.
Every request reached the combat pipeline and was safely rejected as
`attacker-dead`; skill 6000 was rejected by the same authoritative life-state
validation. PostgreSQL held current HP `0` and maximum HP `2265`, while the
client sent no `32/10010` action 94 revive request.

The discrepancy was server-created: `1006` offset 60 is the exact-client
current-health field, but the progression snapshot selected
`greatest(1, v.current_health)` and exposed it under the misleading name
`MaximumHealth`. The live combat registry independently loaded the real zero.
Current and maximum vitals are now separate snapshot fields; `1006` offsets
60/62 carry authoritative current HP/MP, while stat mutation packets continue
to use the calculated maximum values. Zero is preserved so the genuine client
can represent death and originate its evidenced action-94 revive flow rather
than appearing alive while the server rejects combat.

## Genuine 5165 ground-item pickup request

The 2026-08-31 00:48:37 UTC `sheekoninja` run ended with a server-side
`protocol-validation` close immediately when the owner selected a Sycee. No
pickup reached persistence. Exact-client constructor `0x581426`, called at
`0x5177e4`, constructs a 20-byte packet 1101 with the ground UID at offset 4,
claimed X/Y at offsets 12/14, zero at offsets 8..11 and 16..17, and the pickup
discriminator byte `3` at offset 18 (byte 19 remains zero). The authorized
Owner Source `PacketHandling/PickItemUp.cs` independently consumes only the
server-resolved UID at offset 4.

This request is deliberately asymmetric with server 1101 spawn/removal, which
uses category 3 at offset 16 and action 1/2 at offset 18. Revora's former
synthetic fixture incorrectly reused the outbound layout for inbound pickup,
causing the genuine request to fail before dispatch. The inbound parser now
accepts only the exact client constructor shape; item identity, map, instance,
position, ownership, capacity and resulting balance remain server-authoritative.

The later 2026-08-31 01:43–01:46 UTC production runs refined one constructor
detail: genuine pickup frames consistently carried the opaque client stamp
`0x7914A09A` at offset 8 rather than zero. The field is not an item, map,
coordinate, or ownership authority; accepting it while retaining all
server-owned validations enabled five durable pickups without a disconnect.

## Genuine 5165 recovery-item use

At 2026-08-31 01:48:03 UTC the genuine client sent a complete `28/1009`
frame with action 4, Item UID 50 at offset 4, and equipment position/command
zero at offset 8. UID 50 was authoritative inventory item type `1000000`
(Stancher). Revora decoded the frame but returned `1004/2005` with the generic
unsupported-action message, leaving both HP and the item unchanged.

The authorized Owner Source routes packet 1009 action 4 through
`Main/ItemPacket/Equip.cs`; position zero invokes `Items.Manager.ProcessItem`.
`Items/Potions/HP Pots/[1000000] Stancher.cs` adds 70 current HP, the
`Character.CurHP` setter caps it at maximum HP and emits status HP (status 0),
then `Character.RemoveItem` emits the item-removal action. Revora follows this
evidenced order while replacing reflection/dynamic item dispatch with an
explicit reviewed recovery-item allowlist and an atomic PostgreSQL mutation.
