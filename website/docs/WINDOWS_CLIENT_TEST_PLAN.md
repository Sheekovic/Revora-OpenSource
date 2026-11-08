# Disposable Windows Client Test Plan

No Windows compatibility or 5165 interoperability is currently claimed.

## Gate before VM use

1. Obtain two independently sourced trees whose critical core hashes agree, or an authenticated original distribution plus historical official patch chain.
2. Preserve downloads read-only and record SHA-256/SHA-512, source, date, size, signatures, and redistribution terms.
3. Perform complete static extraction and per-file Defender plus another reputable scanner check. A no-detection result alone is insufficient.
4. Approve the minimum required-file inventory. Never treat deletion as malware cleaning.
5. Establish an offline launcher-manifest signing ceremony and embed only its public key. Obtain a legitimate Authenticode certificate separately if desired.

## VM matrix

Use clean, disposable, fully patched Windows 10 and Windows 11 VMs with current Microsoft Defender and SmartScreen. Test as a standard non-admin user. Snapshot before copying. Initially disconnect at the virtual switch; use no exclusions, saved credentials, host credentials, or shared writable folders. Copy through one-way/read-only media and verify hashes inside the guest.

1. Scan the candidate and launcher independently; preserve non-sensitive Defender result identifiers without uploading samples.
2. Confirm no UAC prompt, service, task, autorun, shell child, or persistence; verify refusal of wrong client/profile hashes.
3. Exercise signed staging, interruption, checksum failure, activation, repair, and rollback.
4. Determine whether the client starts without auxiliary launchers and without registering/bundling Flash. Capture Process Monitor module loads; use no real credentials.
5. In a separate snapshot, test the SWFs with a source-built pinned Ruffle scanner/player only as a compatibility experiment. This does not prove in-client ActiveX compatibility.
6. If native login replacement is feasible, validate it without injection. Otherwise stop for a compatibility-module design review.
7. After server fixtures and an isolated route are approved, use throwaway credentials for login, handoff, character create/select, map entry, movement, chat, disconnect, and reconnect. Capture only redacted deterministic traffic.
8. Repeat update/rollback, clean shutdown, removal, Defender full scan, SmartScreen result, and persistence inspection.

Public ports and website downloads remain disabled until both OS versions pass, real-client protocol evidence is reconciled, redistribution review is complete, and signed artifacts are available.
