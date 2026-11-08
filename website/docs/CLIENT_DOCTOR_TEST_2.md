# Client Doctor Test 2 evidence

## Result and safety boundary

Test 2 recognizes a signed patch-5165 donor, reads it without modification, reuses only exact-hash passive assets, downloads other profile files from the fixed HTTPS testing origin, and atomically creates `Revora Client Clean`. It is a deterministic constructor, not a malware cleaner. Every `doctor-*` profile is launch-disabled in Client Doctor and Launcher.

The reconstructed historical tree is **not approved for execution or public distribution**. ClamAV 1.4.3 detected `Win.Virus.Sality-6761375-0` in `TimeDelay.exe`, `Win.Trojan.Fakeav-88125` in `zftqat/TQAT.exe`, and `Win.Malware.Generic-6879689-0` in `zftqat/updata.dll`. Test 2 excludes those files, the entire `zftqat` directory, `Flash.ocx`, historical launchers/patchers, URL shortcuts, and the credit helper. Remaining active components require owner-side Defender and Malwarebytes results before approval. Test 2 never launches them.

## Static extraction provenance

All extraction ran on a temporary `noexec,nosuid,nodev` filesystem; no Windows file was executed.

| Tool | Pinned source | License | Built binary SHA-256 |
|---|---|---|---|
| `iss_extract` | `https://github.com/hifi/iss_extract`, `01232146c9284a86958817817849f320e77f5fb7` | ISC notice in exact source; original `iss_extract.c` SHA-256 `362f06408d646e079c5ce462be39a20542f01249d92e353a9998d96d3be96871` | `c90ec77f246a5d58647d1d54d1de3dd65f6634725e4d3be77e1eaa1e6d2d7784` |
| `unshield` | `https://github.com/twogood/unshield`, `cbed3d19ef9cdf4e5fb86b9d65e48f1bf8d539db` | MIT; `LICENSE` SHA-256 `3aaecd6f4440a1456c7d6fee4fe82a4d47af55646ae3daa1c806c717a9385bba` | `14df0aabe9165ab757c57c19ff912a8f31fac51fc4b2c9a2ddcb20417b9884` |

`lifenjoiner/ISx` was unnecessary and unused. The preserved 5156 installer remained unchanged (SHA-256 `d0536933cdefb785271c760f476fc8daa399e1f0b3462e5b41da76b72267a025`). A local research-only `iss_extract` hardening patch corrected a string bound and numeric parsing before use.

## Reconstruction

The 5156 base has 12,695 files, 1,451,516,136 bytes, and no case collision. Sequential overlays produced:

| Patch | Added | Replaced | Deleted | Case-normalized |
|---:|---:|---:|---:|---:|
| 5157 | 0 | 5 | 0 | 0 |
| 5158 | 106 | 10 | 0 | 1 |
| 5159 | 0 | 4 | 0 | 0 |
| 5160 | 31 | 12 | 0 | 2 |
| 5161 | 0 | 3 | 0 | 0 |
| 5162 | 17 | 8 | 0 | 1 |
| 5163 | 0 | 4 | 0 | 0 |
| 5165 | 0 | 3 | 0 | 0 |

No transition deleted a file. The result has 12,849 files, 1,457,526,376 bytes, zero case collisions, and `version.dat` exactly `5165` (SHA-256 `44898b43f6d5ea1c2b39daad4f4b043830318ecd3b54a348063f9c48cf99f0c4`). Root-only manifests and transition records remain outside Git under `/root/revora-research/clients/reports/`.

Patch 5164 is absent. Patch 5165 contains a 5165 marker and applies cleanly after 5163; the [official August 31, 2009 notice](https://co.99.com/content/2009-08-31/20090831170505699.shtml) calls it Patch 5165 without a 5164 prerequisite. Together these strongly corroborate that the publisher skipped 5164. Contradictory primary evidence would trigger rejection and review; no patch was invented or renamed.

## Profile and delivery

Build `doctor-reconstruction-5165-test-2` contains 12,831 entries totaling 1,450,176,502 bytes. It is signed with ECDSA P-256/SHA-256 under key ID `revora-client-testing-2026-01`. The private key stays root-only at `/srv/revora-client-repository/keys/testing-profile.pem` (mode `0600`), outside Git and web roots; only its public key is embedded. Signed-profile SHA-256: `bf4679cbd371b61ba8f80e3d609f2f473b64d98b1d52bef0974eeb6fcaf7117c`.

Objects are immutable and content-addressed by SHA-256 beneath an unguessable testing path on the pinned `admin.playrevora.com` HTTPS origin. Listing, uploads, inline executable rendering, and indexing are disabled. The authenticated Admin download is `/owner-downloads/client-doctor-test-2`. The store-only ZIP is 146,276,033 bytes with SHA-256 `1a448bbf4bee6248f49ee8db9713153a2a7eae8fa1cf2cdf40e959cb786b16d3`. It contains only first-party Doctor, signed profile, and instructions; Doctor SHA-256 is `71a3d3c51e6109d821484beff77abccbd5b4d0dad5cd790c1e89b4aeb706b4d0`. Two publishes matched and ClamAV reported no detection for the executable or final archive.

An earlier deflated ZIP triggered ClamAV `Win.Malware.Aotera-10060486-0` despite its extracted Doctor scanning clean. It was immediately removed from delivery and retained root-only as forensic evidence; the finding is not dismissed as a false positive. The final package uses store-only ZIP entries and passed archive scanning. Defender and Malwarebytes owner scans remain mandatory.

## Flash/native-login boundary

Static strings in `Conquer.exe` (SHA-256 `e3a4570314a0e9c06cee62e41662cd745da811cfdc8c28a85fb626920155db5c`) include `DlgLoginFlash.cpp`, `CShockwaveFlash`, `STARTTOFLASH`, `LOGINUSER`, `LOGINPASS`, and `regsvr32 /s Flash.ocx`. `data/main/flash/loginFlash.swf` is Flash version 6 (SHA-256 `558f1f2ebb523f05676a1cbc5fe2e6c601e67b45622e855859f34b942d78c577`). This proves an ActiveX-hosted login UI and native/Flash message boundary, not a safe replacement implementation.

`Flash.ocx` is excluded and is neither bundled nor registered. No injection, APC, remote thread, elevation, service, persistence, or antivirus exclusion is used. A native replacement needs Windows-side evidence of the precise credential and server-selection exchange at this boundary. Genuine login remains blocked pending that evidence and approval of the active executable set.
