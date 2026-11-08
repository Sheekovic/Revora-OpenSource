# Research Source Matrix

Static retrieval date: 2026-08-28 UTC. Every repository is a bare, no-checkout clone under ignored `research/isolated/`; hooks are disabled and submodules were not initialized. No repository-controlled code, binary, archive, script, build, restore, SQL, client, or container was executed.

## Classification

| Source | Exact inspected revision | Activity/provenance | License evidence | Class | Permitted use in this milestone |
|---|---|---|---|---|---|
| Comet 5065 | `https://github.com/conquer-online/comet.git`, `cc9c788a27fbace85325955a67b96b4e8e43843e` | Commit dated 2022-10-21; repository history has 335 commits beginning 2018-08-25. Maintained by Spirited; no file-level conflicting notices found in inspected C# files. | Root `License`, SHA-256 `7d0fa47d4436f83d48290767d4211a262aa0e9cbbeeac66785bcc3e4faf44c7d`; BSD 3-Clause text. | **A — Reusable with attribution** | Architecture/protocol evidence only pending a separate reuse review. Preserve the BSD notice if code is later adapted. |
| Comet 5187 | Same URL, `18446a8a54b06e9f2c0d69d76f7fec46254910ab` | Same history and authorship; adjacent patch, not 5165. | Same root license and hash. | **A — Reusable with attribution** | Adjacent-patch comparison only. Never silently map 5187 behavior to 5165. |
| COPS protocol documentation | `https://github.com/conquer-online/cops-protocols-doc.git`, `d976e24e02d5d351634fcf30640d8a78e3ece2de` | Four-commit documentation mirror, 2026-04-11 through 2026-08-22. README says generated from COPS X and reverse-engineered using clients, leaked server binaries, and cross-reference material. | Root `LICENSE`, SHA-256 `a2010f343487d3f7618affe54f789f5487602331c0a8d03f49e9a7c547cf0499`; CC0 1.0. | **A — Reusable with attribution** | CC0 documentation facts are usable, but its latest documented patch is 5066 and provenance still requires corroboration for 5165. |
| Redux 5065 | `https://github.com/conquer-online/redux.git`, `383c0f136e80d38475aa757b2f8a1fc2c24c6831` | One commit dated 2023-08-23. README says it was previously maintained by Pro4Never and imports a forum release; original history/authorship chain is absent. README SHA-256 `b94b6c17016cd2dfa889a36897d0342b338b87bbccd55253f1134b872815c814`. | No root or file-level software license found. README availability is not a grant. | **D — Unknown provenance** | Behavior/evidence only; no code or expressive structure may be copied. |
| HeroesCoServer | `https://github.com/DuneGameStudio/HeroesCoServer.git`, `008b0e997d63778204bb6bb6c91b88908d2e4892` | Seven commits on 2021-05-12. Duplicate `Copy of` source files, bundled binaries/archives, and minimal README make provenance unclear. README SHA-256 `cc47989555d4140b66b8dfd3e64c740a7b10d01fab3c4e7a075124412a0b27a9`. | No license file or grant; AssemblyInfo copyright is not a reuse license. | **D — Unknown provenance** | Claimed-5165 evidence only; never copy, build, restore, load, or execute. |
| Dragon | `https://github.com/conquer-online/dragon.git`, `7594b699b08725daba78b60386a429da557b3a13` | Fourteen commits, 2022-01-07 through 2026-06-28; maintained by Spirited. README SHA-256 `fc1b847bbea247d749dbf0513148d1b92897318c8389e34a2a573c636567af44`. | Root `LICENSE`, SHA-256 `ce9697da9632a81d0acab22e08b445485cc6f9cb17d77171b844112e877a8a6f`; BSD 3-Clause; inspected C++ headers repeat compatible copyright notices. | **A — Reusable with attribution** | Architecture evidence only. Its suspended-process/APC DLL injection and elevation model is rejected for Revora. |
| HeroesCoClient | `https://github.com/DuneGameStudio/HeroesCoClient.git`, `97c16a6d4c39549ee9965f5a85dcc6ff9698a894` | README-only Unity claim for 5165; authorship and client provenance are unverified. | No applicable software license found. | **D — Unknown provenance** | Claim/evidence only. It is not an original 5165 client and was not executed or copied. |
| coemu | `https://github.com/shekohex/coemu.git`, `a9fde82733d792e05b1e207f451c7a6cbf416473` | C# source targeting patch 5017 with SQLite; materially older than 5165. | GPL-3.0-only. | **C — Incompatible or restricted** | Defect/architecture comparison only; no incorporation. |
| Throne | `https://github.com/mahmoudmostafa0/Throne.git`, `0ca014ffb9986a82f5b84991df98a8621f07fb2d` | Claimed patch 5997 implementation with bundled DLLs; materially newer than 5165. | GPLv3. | **C — Incompatible or restricted** | Defect/architecture comparison only; binaries were not loaded and code was not incorporated. |

No inspected source is class C at repository level. Individual unsafe operational patterns are rejected in `LEGACY_DEFECT_REGISTER.md`; rejection is architectural/security based and does not change the repository's license class.

## Static inventory notes

- Comet contains source and four SQL files at each pinned branch, with no bundled EXE/DLL/archive observed in the inspected tree.
- Redux contains four DLLs, at least six DAT files, and a SQL backup. These were inventoried by tree name only and never loaded.
- Heroes contains five DLLs, two OpenSSL executables, one RAR, one ZIP, and one SQL dump. Existing blob identifiers remain in `RESEARCH_AUDIT.md`.
- Dragon contains C++ source/project material and no bundled executable in the inspected revision.
- COPS contains Markdown protocol documents for 5016–5066 and no client/server executable.

## Relative strengths

- **Comet:** clearest licensed service separation, packet-class organization, repositories, and tests; intentionally incomplete and still uses global kernels and weak legacy password storage.
- **COPS docs:** best explicit older-patch field tables and padding notes; does not cover Account Server messages or patch 5165.
- **Redux:** broad playable 5065 behavior inventory and useful contradiction detector; unsafe networking, plaintext passwords, hidden GM-account creation path, and unknown licensing prevent reuse.
- **Heroes:** broad claimed-5165 behavior and exact-patch hypotheses; highest security, provenance, race, networking, and maintainability risk.
- **Dragon:** useful warning about how private-server redirectors work, but its injection/elevation model is explicitly outside Revora's client plan.

## Reuse proposal

No legacy code is proposed for copying in this milestone. Later review may approve narrowly adapted Comet concepts/code or CC0 tables only when (1) the exact file is mapped to its license, (2) attribution is preserved, (3) Revora tests define the behavior, and (4) 5165 applicability is independently verified. Redux and Heroes remain evidence-only.
