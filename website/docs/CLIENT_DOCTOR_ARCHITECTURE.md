# Revora Client Doctor

## Trust boundary

Client Doctor is a non-admin Windows x64 application. A selected legacy folder is read-only, untrusted input. The tool inventories with bounded traversal, rejects reparse points and unsafe roots, hashes files, detects `MZ` content disguised under passive extensions, and never executes, repairs, uploads, or modifies a donor file.

Only an exact signed-profile match may make a passive file reusable. Active extensions, SWFs, archives containing active material, launchers, patchers, Flash components, scripts, links, and executable formats always require a content-addressed Revora repository copy. Construction occurs in a new sibling or other safe local destination through a staging directory. Final verification precedes atomic activation.

Test build 1 intentionally exposes inventory and destination planning only. Its signed profile contains a zero-length, zero-hash `Conquer.exe` sentinel and build ID `doctor-inventory-1`; it is not an approved client profile and cannot build or launch a client.

Test build 2 performs recognized-donor reconstruction. Recognition requires `version.dat=5165` and at least 95% exact matches across the signed passive inventory. Doctor bundles and unrelated archives in the donor are explicitly excluded, and active donor files are never reused. Every `doctor-*` build remains launch-disabled. See `CLIENT_DOCTOR_TEST_2.md`.

Test build 2b corrects Windows verification-handle lifetime, adds guarded marker-based retry cleanup, cancellation/progress, and a second sanitized build report. See `CLIENT_DOCTOR_TEST_2B.md`.

Test build 2c captures every WPF-derived build value on the STA thread before scheduling construction, passes only immutable/plain request data to the worker, throttles UI progress, handles ordinary unexpected failures without terminating the application, and records last-resort sanitized dispatcher crash evidence before shutdown. See `CLIENT_DOCTOR_TEST_2C.md`.

## Repository and signing

The VPS repository is `/srv/revora-client-repository`. Testing and production channels, SHA-256 objects, manifests, owner tests, backups, and root-only keys are separate. Objects will use `objects/sha256/<first-two>/<full-hash>`. Directory listing and upload are not exposed.

ECDSA P-256 testing and production identities are separate. Private PEM files and encrypted backups are root-owned mode `0600`; only SubjectPublicKeyInfo public keys are embedded. Signing is performed manually by the reviewed `Revora.ProfileSigner` tool. Rotation creates a new key ID and application release; compromise revokes the old profile channel and removes affected manifests/objects from publication.

Profiles bind patch 5165, channel, build ID, fixed hostname and ports, exact file inventory, roles, sizes, hashes, same-origin HTTPS objects, one fixed `blacknull` argument, minimum Windows build, and compatibility version. Profiles cannot contain scripts or arbitrary hosts.

## Compatibility decision

Three mechanisms were compared:

1. Controlled APC loading into the launcher-created suspended child: compatible with Dragon's historical approach, but commonly scanner-sensitive and not justified until a clean executable and module exist.
2. Static import patching: deterministic but modifies proprietary executable bytes, invalidates signatures/hashes, and creates a new binary provenance burden.
3. A fixed, private child-process compatibility boundary: preferred. The current first-party `Revora.Compatibility` is deliberately disabled. A later reviewed revision may create only the exact verified child and privately load fixed hash-bound modules if Windows evidence proves this necessary.

No Dragon code was copied. If APC loading ultimately becomes necessary, its implementation will be independent or retain the complete BSD-3-Clause attribution. No generic injector, plugin system, arbitrary PID/path, system-wide OCX registration, service, elevation, or security exclusion is permitted.

## Flash decision

The quarantined `Flash.ocx` and SWFs are not approved. A private local OCX route remains technically plausible but requires an independently recovered exact component, two-source provenance where possible, complete static review, current Defender and Malwarebytes results, redistribution review, and sandboxed Windows testing. Ruffle is useful only for offline SWF analysis and is not an ActiveX drop-in. Native login replacement remains the preferred long-term design.
