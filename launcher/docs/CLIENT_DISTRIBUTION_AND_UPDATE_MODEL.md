# Client distribution and update model

The full private release candidate is produced once from the canonical assembled
tree. Routine launcher releases are framework-dependent and contain only the
launcher and its genuine managed/assets dependencies. Client updates are signed
incremental archives: unchanged files remain in place and are verified against
the full-state signed client manifest.

Each changed-file operation binds source and destination SHA-256. Installation
uses a same-volume staging file, a rollback copy, post-write verification, and
an atomic replace. Downloads use the existing resumable `.partial` update path.
Tampered metadata, archives, source files, or destinations fail closed. The
client doctor uses the signed full-state catalog to detect and repair damage.

`Revora-Conquer.exe` is never redistributed as a full proprietary executable;
the update contains the deterministic patch manifest and Revora-owned patcher.
`Revora-Server.dat` at the client root is the only generated data payload: it is created from the
documented mandatory hierarchy with one playable row and an offline private RSA key whose corresponding
public modulus is hash-bound in the executable patch manifest.
