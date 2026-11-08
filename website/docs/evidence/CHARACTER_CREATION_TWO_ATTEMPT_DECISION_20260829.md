# Character creation corrected-run decision record — 2026-08-29

This record contains sanitized evidence only. It contains no owner address, credentials, raw packet, handoff, cipher key, or DH material.

## Ordered attempts

| Decision | Attempt 1 (launcher PID 1188) | Attempt 2 (launcher PID 27636) |
|---|---|---|
| Account 1086 validated | yes | yes |
| 1055 sent | yes | yes |
| Game connection | yes | yes |
| DH reply / Blowfish | necessarily complete before validated 1052 | necessarily complete before validated 1052 |
| 1052 validated | yes | yes |
| NEW_ROLE sent | yes | yes |
| 1001 frame observed | yes | yes |
| Encrypted wire / declared / ID | 112 / 104 / 1001 | 112 / 104 / 1001 |
| `TQClient` footer | valid | valid |
| Account decode | rejected exact empty field | rejected exact empty field |
| Account canonical match | not evaluated; exact-client canonical value is empty | not evaluated; exact-client canonical value is empty |
| Name decode/validation | not reached; owner-visible input was valid ASCII-alphanumeric | not reached; owner-visible `SHEEKO2` was valid ASCII-alphanumeric |
| Reserved bytes 36–51 | not reached by parser; exact caller supplies empty literal | not reached by parser; exact caller supplies empty literal |
| Reserved accepted | not reached | not reached |
| Mesh / allowlist | not reached/not logged | not reached; exact Small Male is 1003 and is allowed |
| Profession / allowlist | not reached/not logged | not reached; exact Male Taoist is 100 and is allowed |
| Account word / consumed-handoff match | not reached; actual value not retained | not reached; actual value not retained |
| Exact failing branch | `ReadFixedAscii`, `!allowEmpty && value.IsEmpty` for Account | same |
| Database transaction | not started | not started |
| UID allocated | no | no |
| Commit / rollback | neither | neither |
| ANSWER_OK encoded/sent | no/no | no/no |
| Socket close initiator | server after bounded `InvalidDataException` | same |
| Final category | protocol-validation | protocol-validation |

Database inspection found zero case-insensitive `SHEEKO`, zero case-insensitive `SHEEKO2`, and zero characters owned by `vpsowner`. Sequence `game.characters_legacy_uid_seq` remained `last_value=1, is_called=false`, proving that no legacy UID was allocated.

## Exact-client derivation

At `0x491732`, EBX is loaded with `0x68f370`. The final call pushes arguments at `0x49197c–0x491991`; EBX is pushed as both the first (Account) and third (reserved) string. The `.data` bytes at `0x68f370` are zero. The UI name pointer is pushed independently.

At `0x4918db–0x491928`, Small Male derives mesh 1003. The profession jump table at `0x4919eb` maps the Taoist selector pair to `0x491937`, which writes 100. The complete emitted profession set is 100, 10, 40, 20, and 50.
