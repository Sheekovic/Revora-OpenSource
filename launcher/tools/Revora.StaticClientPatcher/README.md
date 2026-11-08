# Revora static client patch

`Revora.StaticClientPatcher` accepts only the approved Patch-7630 DX9 executable
(`4889B6...A65CE`) and deterministically creates `Revora-Conquer.exe`. The patch
manifest records every file offset, image RVA, preimage, postimage, and purpose.

```powershell
dotnet run --project tools/Revora.StaticClientPatcher -c Release -- apply tools/Revora.StaticClientPatcher/revora-conquer-patch.json <Conquer.exe> <Revora-Conquer.exe>
dotnet run --project tools/Revora.StaticClientPatcher -c Release -- reverse tools/Revora.StaticClientPatcher/revora-conquer-patch.json <Revora-Conquer.exe> <Conquer.restored.exe>
```

The derivative redirects the genuine login constructor's `.\\Server` base name
to `.\\Revora-Server` (the generic loader appends `.dat`), replaces the embedded RSA public modulus used to verify
that file, and points the two confirmed English exit paths at
`https://playrevora.com/en/features`. The generated data file contains the
single `Revora` Account endpoint (`79.110.49.34:9958`); Game routing remains
the opcode-1637 handoff.
