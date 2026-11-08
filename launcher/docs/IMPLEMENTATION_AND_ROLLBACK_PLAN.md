# Single-entry implementation and rollback plan

1. Build the x86 DLL and fixed loader with `/W4 /WX`.
2. Run synthetic hook, credential lifecycle, endpoint, HTTPS gate, and UInt32
   bridge tests without launching proprietary code.
3. Build the launcher and full foundation tests.
4. Package only launcher, fixed loader, DLL, signed manifests, installer, and
   rollback script; exclude proprietary binaries and secrets.
5. In a future approved install, back up replaced launcher assets, verify the
   original client hash, install transactionally, and verify installed hashes.
6. Deploy only any reviewed Identity route/config required by the existing
   direct authorization API; AccountHost/GameHost protocol code remains intact.

Rollback restores the backed-up launcher assets and profile manifest. Server
rollback removes only the newly exposed direct authorization route/config if
that future deployment occurs. Current installed clients and services are not
changed by this implementation milestone.
