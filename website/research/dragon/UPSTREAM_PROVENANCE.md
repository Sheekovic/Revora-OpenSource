# Dragon Upstream Provenance

## Reviewed source

- Project: `https://gitlab.com/spirited/dragon`
- Owner-provided archive: `https://gitlab.com/spirited/dragon/-/archive/main/dragon-main.zip?ref_type=heads`
- Inspected commit: `7594b699b08725daba78b60386a429da557b3a13`
- Retrieval date: 2026-08-29 UTC
- Archive size: 49,699 bytes
- Archive SHA-256: `8655484e5d2a612f96c6239311a3ec07785da919b5a2d1421d23f3055b100424`
- Archive SHA-512: `1acf9742612bba922499238f9db101c9559e3308189f8da0324606254e0e36f722d8d128485c8e9f8759bb7d9e8d939e77e8f9aec275f4738767872b52a2e399`
- License: BSD 3-Clause, copyright 2022 Spirited
- License-file SHA-256: `ce9697da9632a81d0acab22e08b445485cc6f9cb17d77171b844112e877a8a6f`
- Classification: reusable with attribution at the repository level, subject to separately
  satisfying the licenses of Microsoft Detours 4.0.1 and nlohmann/json 3.10.4 dependencies.

The GitLab archive comment identifies the inspected commit. The archive contained 63 relative
source/project/text entries and no compiled PE files. It passed path/link/encryption review and a
local ClamAV scan. It was inspected only beneath ignored `research/isolated/`; it was not built,
restored, loaded, injected, or executed. No Dragon source or binary is vendored into Revora.

If Dragon code is reused later, Revora must reproduce the BSD copyright notice, conditions, and
disclaimer in source and binary distributions. Dependency notices remain independently required.
