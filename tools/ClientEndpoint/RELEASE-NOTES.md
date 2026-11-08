Configure the account server IPv4 and port of two reviewed 6711 DX9 client builds.

- Portable Windows x64 GUI, including .NET 10: extract the ZIP and open `Revora.ClientEndpoint.exe`.
- Inspect, apply, automatic verified backups, retarget and restore.
- Strict whole-file hashes, PE checks and fixed-span verification; unknown clients are refused.
- English/Arabic instructions, CLI source and synthetic managed/native x86 tests in the repository.

See [the full guide](https://github.com/Sheekovic/Revora-OpenSource/blob/main/docs/CLIENT_ENDPOINT_TOOL.md) for supported hashes, launcher/server wiring and prerequisites. No game client or server list is bundled. RSA validation and launcher integrity checks remain separate requirements. Actual game login/playability has not been verified for this tool.

The executable is unsigned. Compare the ZIP with `SHA256SUMS.txt`; `BUILD-INFO.json` inside identifies the source commit and executable hash.
