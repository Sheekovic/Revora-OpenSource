# Native Login Owner Test 3

## Exact evidence inputs

- Approved patch-5165 `Conquer.exe`: SHA-256 `e3a4570314a0e9c06cee62e41662cd745da811cfdc8c28a85fb626920155db5c`, PE32 x86, 2,841,656 bytes.
- Approved `data/main/flash/loginFlash.swf`: SHA-256 `558f1f2ebb523f05676a1cbc5fe2e6c601e67b45622e855859f34b942d78c577`, compressed SWF version 6.
- Approved original `Server.dat`: SHA-256 `ada5f56dd8909a09b13a152df29295ba044e82135d75fa8a035c139f85935516`, 2,560 bytes.
- Signed Test 2c profile: 12,831 files and 1,450,176,502 bytes. Owner reconstruction independently reported the same totals and passed final signed-profile verification.

The broader canonical research tree has 12,849 files and 1,457,526,376 bytes. Its 18 additional entries are not silently promoted into Test 3; analysis and construction use only the signed Test 2c set.

## Reviewed static tools

| Tool | Version/revision | Artifact SHA-256 | License and use |
|---|---|---|---|
| GNU Binutils `objdump`/`strings` | Debian 13 `2.44-3` | Debian package-managed | GPL; static PE imports, strings, instruction references only. |
| `pev`/`readpe` | Debian 13 `0.84-2` | Debian package-managed | GPL-2.0-or-later; PE metadata only. |
| `icoutils`/`wrestool` | Debian 13 `0.32.3-6` | Debian package-managed | GPL; resource inventory only. |
| JPEXS FFDec | `26.2.1`, upstream tag commit `792d2c2` | ZIP `0333b56998a55bd83f4e0deb678a811fcdc45607582b4f5dd438309c8c3ad5ce` | GPL-3.0 application/LGPL-3.0 library; official release used as an external static SWF analyzer, not incorporated. |
| Ghidra | `12.1.3`, `Ghidra_12.1.3_build` | official ZIP `93a5d11a9ad510622acaaf908c556a7b9b764d338e78a7567f3689bf5081fd54` | Apache-2.0; official headless release used for static x86 analysis, not incorporated. |

`Cyano-CO/conquer-rsa-patcher` commit `2f46c5fc03343995e725b4bceedb7f09e078add5` was inspected as behavioral evidence only. GitHub reports no applicable repository license, so its code was neither executed nor copied. Revora's implementation is independently written against the CC0 Conquer Online Development Wiki RSA description and direct disassembly of the exact client.

## SWF and native boundary

FFDec exported exactly three ActionScript 1/2 scripts. They only toggle root visibility, disable a mask hand cursor, stop a sprite frame and resume root playback. There is no `FSCommand`, `ExternalInterface`, credential logic, server selector, network request or native callback in the approved SWF.

Ghidra and direct x86 cross-references place the credential/state boundary in the native function containing references to the source string `DlgLoginFlash.cpp`. Its address-independent behavior is:

- native dispatch compares commands including `CMD_SELECTGROUP`, `CMD_SELECTSERVER`, `CMD_GAMESTART`, `CMD_ENTERGAME`, `CMD_NAME` and `CMD_PASSWORD`;
- the native object owns group/server indices and account/password buffers;
- native keyboard processing updates those buffers and mirrors `LOGINUSER`, `LOGINPASS`, `STARTTOFLASH`, `ENDTOFLASH` and focus state into the Flash control;
- Account endpoint lookup occurs natively through `GetServerInfo(ACCOUNT_SERVER, ...)`;
- the historical client contains a `regsvr32 /s Flash.ocx` path, which Revora never invokes.

Therefore Flash is presentation, not the credential authority, in this exact executable. This static conclusion did **not** prove that the Test 3 helper could supply credentials through the top-level window.

## Corrected owner result

Test 3 is classified as follows:

- exact-client preparation: **passed**;
- client launch: **passed**;
- original patch-5165 login interface without a distributed `Flash.ocx`: **passed**;
- Revora server-selection display: **passed**;
- helper credential delivery: **failed**;
- helper login-action delivery: **failed**;
- automatic native login: **failed**;
- manual in-game login triggering an Account connection: **passed**.

The helper's top-level `PostMessage(WM_CHAR/WM_KEYDOWN)` attempt neither populated Account/Password nor initiated login. Two reports remained `listening` with no frames and are failed input attempts, not connections. Only the owner's manual entry into the in-game controls followed by the in-game Enter button produced Account traffic. The repeated initial ciphertext bytes `15 94 56 65` therefore have provenance `manual-in-game-login`. They are not a plaintext length/type pair.

## Deterministic Test 3 file patch

`Server.dat` is gzip-compressed and RSA PKCS#1 type-1 transformed in 256-byte blocks. Direct client disassembly reconstructs a 2,048-bit public modulus from 64 immediate 32-bit words using a rolling adjacent XOR starting at index 17 and exponent 65,537. Revora's evidence tool successfully decrypted the original XML and independently round-tripped a loopback-only replacement.

Test 3 changes exactly the 64 four-byte modulus immediates. Every original byte is checked before replacement. The complete byte-level offset/original/replacement list is `conquer-test3-patch.json` inside the authenticated package; its SHA-256 is `036069e65095ee0d884aae5f974609e9273ea82a17436d39b904a9bd89d02e0a`. Unknown inputs are rejected.

- Input executable SHA-256: `e3a4570314a0e9c06cee62e41662cd745da811cfdc8c28a85fb626920155db5c`.
- Deterministic output SHA-256: `902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`.
- Loopback `Server.dat`: 512 bytes, SHA-256 `b0ec86d2450256894e6c348e424410adcf47a89decf7f6d1efe7a05ce049de52`.
- Signed Test 3 profile SHA-256: `489f31a76f51fc9fd20894480c29db2ff342293659282992d9011ecae257e3ad`.

The patch is applied only to a separate Test 3 copy and is reversible by deleting that copy. The signed Test 2c original remains unchanged. A root-only RSA key used to construct the loopback DAT is stored outside Git and outside served directories at `/srv/revora-client-repository/keys/native-login-test3/serverdat-private.pem`, mode `0600`; it is not shipped.

One other approved file, `ini/suittype.dat`, uses the same historical RSA key. It remains original in Test 3 and may fail to load under the testing key. It is not required to identify the first Account connection, but it is an explicit compatibility risk and Test 3 must not be described as a gameplay-ready client.

## Threat model and probe

The UI has bounded 16-character username/password inputs, a masked `PasswordBox`, no credential persistence and no credential logs. Password characters are read through `SecureString`, passed to the exact child window and immediately zero-freed; the UI password is cleared. A malicious process already running as the same Windows user could observe UI input, which is an operating-system session boundary this test cannot eliminate. The owner must use throwaway game credentials.

The Test 3 probe bound exactly `127.0.0.1:9958`, backlog one. Its immediate plaintext framing assumption was wrong for the Account connection: interpreting `15 94 56 65` as plaintext produced the impossible length 37,909 and type 25,942. Those numbers are retained only as rejected intermediate interpretations. It implemented no response and cannot be mistaken for authentication or automatic input success.

## Owner artifact and rollback

- Authenticated route: `/owner-downloads/client-native-login-test-3`.
- ZIP: 152,785,428 bytes.
- ZIP SHA-256: `63fb078e2f50eaa460d4ea9c7a4c704b5ab74dac973d601b2ba6892c1486490e`.
- First-party executable: 139,798,756 bytes.
- Executable SHA-256: `12a10077fdaf6ec29bb4d05d9b468c79016555d02e5019307b5ad0a297035ee0`.

Two independent Windows publishes produced the same executable hash. ClamAV `1.4.3/28106` reported no detection for the store-only ZIP and every member. A maximally compressed packaging experiment triggered `Win.Malware.Aotera-10060486-0` on the archive only; that experimental ZIP was rejected and was never staged for owner download.

Rollback is deletion of the separate `Revora Client Test 3` directory after the helper and child are stopped. The helper never alters Test 2c. An unmarked staging directory is preserved for manual review; only the exact tool marker for the signed Test 3 build permits automatic cleanup.
