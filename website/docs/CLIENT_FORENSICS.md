# Client Forensics and Provenance

Status date: 2026-08-28. This document contains hashes and independently written findings only. No client file is tracked by Revora.

## Evidence controls

The original archive is `/root/revora-research/clients/incoming/5165.7z`, owned by `root:root`, mode `0400`, and protected with the filesystem immutable flag. It was extracted only into a dedicated ext4 image mounted `noexec,nosuid,nodev`. Nothing from the tree was executed or loaded as code. The forensic mount was unmounted after inspection.

| Evidence | Size | SHA-256 | SHA-512 |
|---|---:|---|---|
| Cooldown/MEGA `5165.7z` | 318,091,319 | `33ed8cadc18c78483d1eed0fad38b69dd6cdd721ff1b6e510310aad5ee76a212` | `979dde762bc412682c1f50d162f15f8edd635d7c6c6557cecedad9ef4add437c0a3041b8756d909e0e52efa77ae9feb6527b222c06cac392d47e7e60cbfd0c9b` |
| ModDB/GameWatcher `Conquer_v5118.exe` | 491,811,761 | `36a9db229e4a605c717f18bef4d25079c59427f8fb4ee1524be008bd82dfc78d` | `b41332ef6721069559a213694407ebf9c988edf9b071d4844063946bc67d0a082aa56ce430d61937dc16675c82e3b9de82bbc0bf39135019c05e55e616b96984` |

The 5118 installer MD5 is `75a3c2fda261153c6c7a9c9b0ccda903`, matching ModDB’s published value. This is useful provenance evidence, not a malware-clean or redistribution determination. It is an unsigned InstallShield container and cannot be safely transformed to 5165 until the historical official patch chain is independently acquired and inspected.

## Archive findings

The 7z archive tested clean structurally: 12,845 files, 878 directories, 1,456,532,694 uncompressed bytes, no encryption, absolute paths, traversal entries, or links. It contains 28 PE/EXE/DLL/OCX components and 128 SWFs. The complete per-component SHA-256/SHA-512 list is in `docs/evidence/QUARANTINED_5165_EXECUTABLE_HASHES.tsv`.

ClamAV 1.4.3 with definition set 28106 scanned 12,830 files and detected:

| File | Detection | SHA-256 |
|---|---|---|
| `TimeDelay.exe` | `Win.Virus.Sality-6761375-0` | `9b5dac858df162048a4ddccc204aa0331ac7a30e6afcd03bdcd079b1b81b2198` |
| `zftqat/TQAT.exe` | `Win.Trojan.Fakeav-88125` | `a352903faae599c60b2919f6b36dd91a9b7840b2a292d14ff1245438c7489c5d` |
| `zftqat/updata.dll` | `Win.Malware.Generic-6879689-0` | `17b640a7fbf05a3d61dec81677786751880a8e663ee115b37b78f55ea6463ee6` |

No reference to `TimeDelay.exe` was found in configuration, scripts, or strings in other files. That does not make deletion a remediation: two additional malicious components exist and the tree has no independently matching clean baseline. `Help/credit.exe` also has a 1,701,861-byte overlay. Most components are unsigned. `Conquer.exe`, `AutoPatch.exe`, and `play.exe` contain historical NetDragon signatures; their embedded Authenticode content digests match, but modern chain validation fails because the historical Thawte root is absent/expired. This is provenance evidence only.

## Version and Flash evidence

`version.dat` contains ASCII `5165`; most relevant timestamps are from 2009 and the file is dated 2009-09-01. The official NetDragon announcement states patch 5165 was released 2009-08-31. Expected data/map/WDF files are present. This is strong patch-era consistency, not proof of an untouched original distribution.

The tree contains an unsigned `Flash.ocx` and 128 SWFs (versions 6 and 8). `Conquer.exe` exposes strings for `CShockwaveFlash`, `DlgLoginFlash.cpp`, `LoginFlash`, `Flash.ocx`, and `regsvr32 /s Flash.ocx`. The old login is therefore an ActiveX host, not a generic standalone SWF player. The obsolete proprietary OCX must not be distributed or registered.

## Candidate decision

Neither candidate is approved. The MEGA candidate is infected. The 5118 preservation installer matches its published archival hash and had no direct ClamAV detection, but a packed-installer scan is not equivalent to scanning every payload and the authenticated historical 5118-to-5165 patch chain is still missing. A no-detection result from one engine is never a clean verdict.
