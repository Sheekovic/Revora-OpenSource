# Character-creation continuation boundary, 2026-09-05

## Direct evidence

Release-equivalent client PID 3440 injected the first 2429 identifier once at
12:31:22.638 UTC. GameHost consumed the grant and requested NEW_ROLE. The genuine
class screen was visually verified. Owner then selected the character and name.
Client emitted `world_handle_bridge_rejected/session_or_packet_invalid` at
12:34:48.153 UTC. No second grant-consumption event or persisted character file
was observed. The old event does NOT establish the suppressed opcode or length.

A read-only Windows process snapshot of hash-pinned GameHost PID 4188 inspected
only GameClient type metadata and categorical fields, never character names,
credentials, packets or grants. Its sole authenticated GameClient had:
`CreateCharacter=false`, `CreateCharacterSucces=true`, `AcceptLogin=false`,
`LoginFull=false`. By inspection time its socket was no longer alive. No dump
was written. Therefore MsgNewRole successfully ran through its genuine creation
handler; the owner input is not the first failure. No safe live resumption exists.

## Client correction

The production send seam serves all Game traffic. Its old non-2429 pass-through
incorrectly required length <=1024; larger unrelated messages fell through to
handoff rejection. Remove that condition from opcode classification only.
2429 retains its own strict size/session/single-use validation. Every unrelated
message passes to the unchanged native sender regardless of cleared handoff
state. This fixes a proven source defect, but is NOT proven to be the cause of
the 12:34:48 rejection because that report lacked header metadata.

Real-DLL tests cover creation opcode 2205 and normal/prelude opcodes, lengths
4/136/472/1024/1025/2048/65535, exact-byte transparency, missing/duplicate 2429
rejection and Win32/WSA error preservation. Existing cross-thread tests cover
initial substitution, all other bytes unchanged, replay, expiration and races.

## Separate blocker: server finalization contract

Current MaTriX MsgNewRole.CreateCharacter sends ANSWER_OK and sets
CreateCharacterSucces, but does not persist or invoke world entry. Persistence
is in MsgLoginClient.LoginHandler. That method unconditionally calls
TryEstablishValidatedIdentity / GameSessionValidation.Current.TryConsume BEFORE
checking CreateCharacterSucces. The initial grant has already been consumed.
Thus the current finalization path cannot succeed under atomic single-use
semantics, even if a continuation reaches it. Do not bypass grant validation,
reuse the identifier, or blanket-allow duplicate 2429 as a client-only fix.

An explicit, same-authenticated-Game-session creation-finalization transition
is required. It must not reauthenticate from a consumed grant and must finalize
the accepted owner creation once. The exact native continuation packet still
needs header-only evidence; no packet type is inferred from the old broad log.
The active task prohibits MaTriX changes, so this server boundary is not modified.
No new owner retry is justified by the transparency fix alone.
