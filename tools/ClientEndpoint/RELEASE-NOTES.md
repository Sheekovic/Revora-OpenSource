Version 1.1 adds a **Server.dat editor to the same portable tool**.

- Load and verify a matching 6711 executable and `Server.dat` / `RevoraServer.dat`.
- Choose a server and edit its name, account IPv4 and port while preserving other rows.
- Sign the edited list with a new/reused local operator RSA-2048 key; update only the reviewed public-key constants in the executable. RSA validation remains enabled.
- Switch to signed-list routing by removing the earlier endpoint override when saving a pair.
- Back up both files and restore them together, including recovery from an interrupted replacement.
- Keep private keys, backups and journals local. New Windows keys use an owner-only ACL.

Portable Windows x64 GUI with .NET 10 included: extract the ZIP and open `Revora.ClientEndpoint.exe`, then use **Server.dat editor**. The earlier executable endpoint tab remains available for unrotated clients.

**[Instructions in English and Arabic](https://github.com/Sheekovic/Revora-OpenSource/blob/main/docs/CLIENT_ENDPOINT_TOOL.md)** · **[Source and CLI](https://github.com/Sheekovic/Revora-OpenSource/tree/main/tools/ClientEndpoint)**

89 managed checks and the synthetic x86 ABI harness passed. Private copies of both approved real client/list pairs passed independent signature decoding, editing, preservation of unselected rows and exact paired restore. No proprietary clients, real server lists or private keys are included. Real game login/playability remains unverified.

The executable is unsigned. Verify the downloaded ZIP with `SHA256SUMS.txt`; `BUILD-INFO.json` inside records its source commit and executable hash. Re-hash both changed files and re-sign your operator-owned launcher profile before deployment.
