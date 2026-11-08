# Owner Test 4c

## Returned Windows result

Owner Test 4c completed as a partial failure. Small delivery, reusable workspace creation,
12,829 unchanged-file verification, and exact client launch passed. No Account connection was
observed, the ticket was revoked, and the test timed out. Transition entry, state writes,
compatibility setters, dispatcher execution, Account builder, and socket creation are unproven.
Test 4d therefore diagnoses fixed stages rather than introducing another guessed transition.

## Purpose

Test 4c is a bounded loopback Account-bootstrap test, not playable login. It must prove that
one launcher authentication produces a PID-bound genuine encrypted length-276 packet 1086,
one ticket consumption and controlled response 1055.

## Corrected internal path

Confidence is tied to the exact RSA-intermediate input SHA-256
`902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`.

| Finding | Confidence |
|---|---|
| VA `0x44abad` initializes the login surface, loads group/server and account state, then calls UI completion at VA `0x44afee` once | Strongly supported |
| `CMD_SELECTGROUP` stores `this+0xfc`; `CMD_SELECTSERVER` stores `this+0x100` | Confirmed statically |
| Password storage is the existing object at `this+0x160`, set by VA `0x55de4f` | Confirmed statically |
| Account name is the existing string at `this+0x104`, assigned by VA `0x4029b2` | Confirmed statically |
| `CMD_GAMESTART` calls VA `0x50ee04(0x480,0)` | Confirmed statically |
| VA `0x50ee04` posts Win32 `0x464` to internal HWND `0x691c94` with command `0x480` | Confirmed statically |
| The internal handler sets Account-start state; later dispatch calls VA `0x44cda9`, endpoint resolution and VA `0x522ea2` | Strongly supported |
| The exact transition runs automatically on Windows and emits 276/1086 | Owner Test 4c gate; not yet confirmed |

The fixed compatibility strings `revora_bridge` and `bridge5165` are non-secret and have no
authentication authority. They are deliberately not a database account. Only the 60-second,
single-use, digest-stored launcher ticket authorizes the bridge transaction.

## Exact changes

Input: `902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`
Output: `8040023d599116c5db3d91eb3eb97065ec15521ed8b6a3a7ac6c3a28247ed310`

- `0x230`: `.text` virtual size `d2b52000` to `00c02000`, within existing raw data.
- `0x4afee`: call `e8c1080000` to `e80d161c00`, targeting the reviewed stub.
- `0x20c600`: 113 asserted zero bytes replaced by the documented one-time transition and fixed non-secret strings.

The patch disables no integrity or OS security control and uses no runtime injection, input
automation, Flash, elevation, service, persistence or public listener.

## Delivery redesign

- Framework-dependent application files: about 0.4 MB before signed profiles.
- Owner ZIP: 3,044,523 bytes; SHA-256
  `78e9e863aa3cc89554a399566be41d7ce8bcf3ed823b6db21a3a9463e4b603e0`.
- One-time prerequisite: official Microsoft .NET 10 Desktop Runtime, Windows x64.
- Test 3/4/4b remain preserved under an archived-evidence disclosure in Admin.
- No runtime, proprietary client, executable client delta or private key is in the ZIP.

The owner selects Test 2c only to establish the workspace. Future owner tests reuse it and
replace only reviewed controlled files with rollback.
