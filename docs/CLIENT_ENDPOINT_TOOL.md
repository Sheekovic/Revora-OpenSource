# Configure a 6711 client endpoint

**[Download Revora Client Endpoint for Windows x64](https://github.com/Sheekovic/Revora-OpenSource/releases/tag/client-endpoint-v1.0.0)**

The tool changes the account-server endpoint in two reviewed 6711 DX9
executables. Use it only with a client you may lawfully modify and a server you
control. No proprietary executable, server list, private key or game asset is
included in the release. Source: [`tools/ClientEndpoint/`](../tools/ClientEndpoint/).

## Supported files

Both files are x86 PE32, exactly **10,280,848 bytes**. Filenames are informative;
the **entire-file SHA-256**, PE mapping, original prologue and zero padding are
the actual acceptance gates.

| Profile | Original SHA-256 |
| --- | --- |
| Genuine 6711 DX9 `Conquer.exe` | `3728BC3FD4E584D85301EDD2B1887A176267CDAA6356038F9951EF23E4FDED60` |
| Revora 6711 DX9 `Conquer-Revora.exe` | `B6D6091CD026757BB9E6400557A44F72BA995D92586618D610861700F362D130` |

Outputs made by this tool are recognized only when undoing its two fixed spans
reconstructs one of those entire original hashes. Other changes cause refusal.
7630, DX8, DX12, arbitrary repacks and outputs from earlier 62/65-byte prototypes are
not supported. There is no force option or editable offset/profile manifest.

## Use and connect

1. Copy your client to a folder you own. Close the game and launcher. Extract
   the release ZIP and run `Revora.ClientEndpoint.exe` without elevation.
2. Browse to the executable under `Env_DX9` and choose **Inspect**. Pick the
   executable your launcher actually starts. Changing one does not patch the other.
3. Enter a canonical IPv4 address and the **Account Server** TCP port.
   `127.0.0.1` is for a server on the same computer; use a reachable LAN/public
   address for other players. DNS names and IPv6 are not supported by this hook.
4. In the server's private `Configuracion.ini`, match `IP` and `AuthPort`
   (repository example: `9958`). Configure the separate `GamePort` (`9959`) and
   advertised game address for the server handoff. See
   [component connections](CONNECT_COMPONENTS.md) and your supplied runtime.
5. Choose **Apply endpoint**. A content-addressed pre-operation backup is flushed
   to `.revora-endpoint/backups/<SHA256>.bin` beside the selected file before an
   atomic replacement. Inspect the new address/port and keep that folder.
6. Keep your valid matching server-list file and RSA pair. This tool does not
   produce/sign `Server.dat` or `RevoraServer.dat`, modify RSA validation, or
   repair an invalid list. The old Revora derivative was documented with
   `RevoraServer.dat` SHA-256
   `E08C555D34B7ABF7341EDE94EC8867EAAEBE5A6F0347A64ECEC1E0EEEE616E2F`;
   this is a historical pairing, not a cryptographic or gameplay qualification.
7. Recompute the changed executable's SHA-256 in your operator-owned **6711**
   launcher profile and re-sign its manifest with your private key. Preserve
   hash/signature gates. The imported 7630 profiles do not become compatible.
8. Verify native login, character entry, movement, save and relogin against your
   server. Account/Game listeners alone do not prove those steps work.

**Restore original** reconstructs the allowlisted source and checks its entire
hash; it does not trust a user-editable receipt. A backup is also kept before
restore or retargeting. Unknown modifications, damaged existing backups,
network/device paths and reparse-point paths are refused. Use a private local
folder; do not run the tool in a directory other users/processes can alter.
It never downloads files, starts the game or changes any other client file.

## Method and provenance

The method comes from the source repository's
[`prepare_moon_box_client.py`](../server/tools/prepare_moon_box_client.py) and
[`MOON_BOX_ACCEPTANCE.md`](../server/docs/MOON_BOX_ACCEPTANCE.md), imported from
`Revora6711-3D` commit `33bb6661291e72bb5ce51028c70226f45633fca9`.
That prototype was a private loopback-only preparation script; its client
outputs were never authorized for distribution. This release contains our
generalized tool source and our own executable, not those outputs.

`GetServerInfo(type=2, address, ..., port)` is redirected by a seven-byte jump
at raw offset `0x11CEB6`. An **80-byte** stub at `0x7662E4` writes a padded,
NUL-terminated 16-byte IPv4 buffer and a 32-bit port, returning true with the
original callee cleanup of four stack arguments (the original object pointer
in ECX is preserved on fallback). Other types, null pointers and address-buffer
capacities below 16 execute the displaced seven-byte prologue and jump back.
Relative branches use the checked PE section RVAs. The original 62-byte stub
supported a shorter fixed address; this version supports IPv4 lengths up to
15 characters and adds pointer/capacity guards.

Both local reviewed images had 80 zero bytes in executable section raw padding.
Only those spans change. No RSA/security code is patched. Supporting another
hash requires an explicit source review and new native evidence.

## Verification and limits

Before publication, the .NET 8 development build passed **43 managed checks**
covering range/input validation, PE permissions, unknown-image refusal,
outside/stub tampering, exact restoration, retargeting, backup integrity,
replacement and junction refusal. The release workflow repeats these checks on
.NET 10. A compiled **x86** harness executes only synthetic test-owned code,
covering four endpoint cases, six lookup types and six buffer capacities per
case, including null pointers. It verifies stack balance, fallback behavior,
exact address bytes, port width and buffer sentinels.
It does not load or execute the proprietary game.

On private copies of **both actual approved client files**, applying a 15-char
address with port 65535, inspection, tamper refusal, retargeting and restoration
passed. Only the reviewed spans changed, backups matched, and the installed
client files/server list remained unchanged. Those copies are ignored and are
not published. No real game session or live login has been verified for this
tool; runtime success still depends on the correct client/list/server pairing.

The portable executable is unsigned. The ZIP has a public SHA-256 checksum and
build metadata identifying its source commit. A checksum checks download
integrity; it is not a malware or gameplay guarantee.
