# Client Doctor Milestone Record

Build ID `doctor-inventory-1` is the first owner test. It performs signed-profile verification and read-only inventory only. It cannot construct or launch a client because no active component is approved.

Artifacts outside Git:

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| `artifacts/client-doctor-test1-g/Revora.ClientDoctor.exe` | 139,782,383 | `d6aa84aa94a0a3811a2692533cc29a63e03c5b1acf2101e8dfca29576f2810c6` |
| `artifacts/launcher-doctor-a/Revora.Launcher.exe` | 139,749,075 | `e22af981f4867f07b9e9dfeb1dc64d49f309c40fd250b1dae324c24eca2beba0` |
| `artifacts/compatibility-disabled-a/Revora.Compatibility.exe` | 73,565,874 | `17165819e275b3b8a8a5836359fb91708d96bc1046cb5394fb5b04a3a18e9438` |
| owner Test 1 ZIP | 58,762,855 | `2acdfbbb8f8461a4bb05686dc08c8e1876175c833a9cd7825f72a2388ab8cc75` |

Each executable was independently published twice with identical bytes. Linux ClamAV reported no detection. That result is not a Windows safety verdict; current Defender and Malwarebytes scans are Test 1 acceptance gates.

The owner package is served only by authenticated Revora Admin at `/owner-downloads/client-doctor-test-1`. Unauthenticated access redirects to login. It contains exactly Client Doctor, the signed inventory profile, and instructions. It contains no client, Flash, compatibility module, launcher, signing key, or proprietary asset.

Signing keys are outside Git under `/srv/revora-client-repository/keys`; encrypted backups are under its root-only `key-backups`. The Admin runtime can read only the owner package and cannot read either private key.

The compatibility executable is intentionally disabled. Publishing a module that privately loads Flash or a child process would be unsafe before exact active-component provenance, Defender/Malwarebytes evidence, and real Windows testing.

Validation: complete Release build had zero warnings/errors; Client Doctor 16/16, Launcher 11/11, Unit 9/9, Integration 1/1, Web 32/32, Host 11/11, Admin 6/6, and durable synthetic playable 11/11 passed. Protocol has zero production fixtures because genuine 5165 layouts remain unconfirmed. Deployment/template validation passed. All three Windows executables and the ZIP had no ClamAV detection.

The authenticated Admin route was deployed from a single-file artifact after backing up the prior executable at `/srv/revora-admin/backups/client-doctor-route-20260828T000000Z/Revora.Admin`. An initial non-single-file deployment failed closed and was rolled back; the old Admin returned healthy before the corrected single-file deployment. Public website and AMP panel remained healthy. No nginx change was required.
