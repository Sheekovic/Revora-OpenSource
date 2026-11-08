# Launcher 1.0.7 first-install release gate

The owner accepted launcher 1.0.6 for repair, automatic launch, and in-client authentication.
Stable promotion was withheld because a clean-folder acceptance preflight found a deterministic
product gap: `ClientRootResolver.ResolvePortable` required `Conquer.exe`, `version.dat`, and a data
directory before the launcher could fetch the signed desired-state manifest. Consequently 1.0.6
could repair an existing client but could not create the complete client in a new writable folder.

Version 1.0.7 is a new test candidate rather than a silent stable repack. It accepts an empty root
only when that root is the directory containing the running `Revora.Launcher.exe`. Dangerous roots,
reparse-point escapes, blank folders selected elsewhere, and non-writable directories remain
rejected. A partial genuine-client fingerprint is repairable. All content still comes exclusively
from the signed desired-state manifest and passes the existing staged hash, atomic replacement,
rollback, final verification, and one-shot automatic-launch path.

Immutable test package:

- URL: `https://playrevora.com/downloads/Revora-Launcher-1.0.7-test.zip`
- Bytes: `4,257,846`
- SHA-256: `9f68f07b68f3fc47d45cdc596131ad9d4561df8e5aa9a0939456c7d8dc72dd62`
- Launcher test manifest SHA-256: `bf78a81735832cdabc253babf8313b3c14512107fe96849e8fd03c9247febb16`
- Launcher release ID: `51650107-0000-4000-8000-000000000001`

The accepted 1.0.6 package remains immutable at its original URL and hash. Stable manifests and the
public website download button remain unchanged until genuine Windows acceptance proves the new
empty-folder download, repair, final verification, automatic launch, interrupted-download recovery,
and existing-client cache reuse paths.
