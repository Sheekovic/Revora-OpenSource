# HorrifyingSpecter boss HP display

## Finding (2026-09-13)

The owner reports no HP bar when attacking HorrifyingSpecter. Commit 2b522f8
sets the server boss flag, but this alone does not satisfy the client HUD.
The deployed assembly was checked in an offline, read-only database harness:
HorrifyingSpecter family 2719, mesh 976, level 140, real HP 5,000,000,
template Boss=true, spawn Boss=1, normalized spawn HP=10,000.

The local `6711/ini/monster.dat` has no exact-name HorrifyingSpecter section.
It has `[CreepySpook]`, TypeID=2719, Boss=1, MaxLife=55,000,000. The server's
custom name therefore has no matching client profile. SilverHawk has a
matching exact-name profile, explaining why simply copying its server flag
was insufficient.

## Native evidence and confidence

Approved DX9 executable: `6711/Env_DX9/Conquer.exe`, SHA256
`3728BC3FD4E584D85301EDD2B1887A176267CDAA6356038F9951EF23E4FDED60`.
Existing Ghidra program `Conquer.6711.original.exe` independently reports that
same hash. Analysis was static and read-only; no executable changes or hooks.

- Spawn receiver 0x0093CB6E assigns monster actor category 111 versus 11 using
  the received boss boolean at the parsed message's +0x12D. It installs the
  received actor name. This evidence supports the existing server flag; no
  packet layout was changed for this correction.
- 0x007B2129 chooses nearby boss-bar candidates through 0x0077B401.
- 0x0077B401 queries RoleDataQuery vtable +0x2C using actor name at +0x1AC.
  Missing lookup returns false; a successful lookup returns profile byte
  +0x10C. Thus an absent exact-name client profile cannot pass this gate.
- 0x00778B8B selects the candidate, 0x007784E3 updates selection, and
  0x00777515 also queries the selected actor's profile. 0x00778CB9 reads the
  selected actor's health for the boss effect. 0x00448D49 handles the boss
  life information display and likewise queries the selected actor by name.

Confidence: high for the missing-profile visibility rejection, from the
approved executable and actual client data. The full native visual result
after patching has not yet been observed. Decompiler output remains ignored
under `research/isolated/ghidra_scripts/boss6711-*.txt`; it is research
evidence, not copied implementation code.

## Prepared correction

`tools/ClientPatches/prepare_boss_hud_patch.py` stages patch 1.0.11 containing
only `ini/monster.dat`. It appends `[HorrifyingSpecter]` using CreepySpook's
existing profile and changes MaxLife to the server's current 5,000,000.
All other template values, including Boss=1 and TypeID=2719, remain intact.
The existing CreepySpook entry and every pre-existing plaintext byte remain
unchanged. The existing model resources are reused. Server combat stats and
drop rates are unaffected. A full client exit and relaunch is required.

Validation: approved executable and input hashes; reversible encryption;
unique exact-name alias; preserved original entries and original prefix;
expected Boss, TypeID and MaxLife; archive path, CRC and extracted bytes.

- Original monster.dat SHA256:
  `c9e7616d4b3888d508f15c41972c084d0403db558145ad5b95d0d59079f60178`.
- Staged monster.dat SHA256:
  `0740673b772cc70c8466e5776c14153fb7f06a751a0b2a4f00644136d3bd50bb`.
- Staged 1.0.11.zip SHA256:
  `b2c5ca8ee6d3bfcf77bfc498e286fde62c2c635e24c1c116c05799ccf7742232`,
  252,827 bytes.

Staging, exact preimage backup, manifest and publication script are under
`obj/chi-refill-validation/boss-hud`. After the owner explicitly requested retry, local application and public publication succeeded at 2026-09-14T02:30:35.0461923Z. The launcher catalog selects 1.0.11 and the public download hash matches the staged archive. A client restart and visual check remain required.

Recovery: after closing the client, restore the saved original only if the
current client file still matches the staged postimage above. Do not replace
newer client updates. Public releases are immutable; do not overwrite a
published 1.0.11 archive to undo it. Use a separately reviewed newer patch.

### Owner visual confirmation

Follow-up: the owner reported `10000/5000000`. Visibility was fixed, but
server normalization was wrong for the numeric HUD. The new server sends
actual HP for spawn and damage updates: 5,000,000 full, 2,500,000 half,
and 1 near death for HorrifyingSpecter. Packet round trips cover those
values and zero, plus the five Boss Rush HP totals and a 100M daily boss.
The client profile remains MaxLife=5,000,000. Corrected in-game numbers
have not yet been visually confirmed. Patch 1.0.12 also provides the nine
daily/Boss Rush aliases; see DAILY_WORLD_BOSSES.md.

At 2026-09-14T02:33:24.6555207Z, the owner confirmed seeing an HP bar for HorrifyingSpecter after patch 1.0.11. This verifies in-game HP-bar visibility. The confirmation did not separately establish damage scaling, depleted-health display, or respawn behavior.

## 2026-09-14: native section-name parsing correction

The owner reported no Sunscar Tyrant bar despite installing 1.0.12 and fully
restarting. The profile headers added in 1.0.12 contained literal spaces.
Static inspection of the approved DX9 dependency `6711/Env_DX9/GameData.dll`
(x86, image base 0x10000000, SHA256
`8f5687dd11a17e7950358c19aefbb197e220f8f1fc24442a826ea0e28a2e409c`)
identified the actual behavior:

- `RoleDataQuery` RVA 0x2341D returns the role-data object. Its virtual method
  at offset 0x2C, RVA 0x10CC5, performs exact-name monster-profile lookup.
- The monster loader at RVA 0x173B9 parses section headers as a whitespace
  delimited token, then removes the closing bracket. A literal-space header
  therefore produces a truncated key rather than the displayed monster name.
- Before insertion, non-Chinese profiles decode `~` into a space via RVA
  0x2378D. `[Sunscar~Tyrant]` therefore resolves as `Sunscar Tyrant`.

`tools/ClientPatches/Verify-BossProfiles.ps1` compiles an x86 offline fixture
against disposable, hash-gated copies of GameData and its approved dependencies.
It calls the actual loader and exact-name lookup, without starting Conquer,
connecting to a server, or patching an executable. All nine 1.0.12 aliases fail
lookup; all nine corrected 1.0.13 profiles resolve with Boss=1 and their expected
MaxLife. HorrifyingSpecter resolves as Boss=1 / 5,000,000 in both variants.
This is direct native runtime evidence for the correction, not a simulated
INI parser. It does not replace a rendered in-game visual check.

Patch 1.0.13 changes only those nine section headers, encoding spaces as `~`.
Server/display names, profile values and other plaintext bytes are preserved.
All four daily bosses resolve with MaxLife=100,000,000. The five Boss Rush
profiles also receive this correction, retaining their individual HP totals.

- Preimage monster.dat: `bc40ae4d515ffc3475da17a6630e1155ef59736116cb6bc40185178e5c2f68c0`.
- Postimage monster.dat: `99fa2c8f14c3851d797a0b20fbd325053f9acee35c7dab4c5e73edc2f5792585`.
- Archive: `ec63d85096e3f2144f624ee69777153c84b659f62a965f5c2f4b19b852851776`, 254,599 bytes.

Published at 2026-09-14T23:34:30Z; HTTPS latest catalog and downloaded archive
hash both verified. The local client catalog was applied transactionally.
Close the game completely and relaunch through the launcher to receive it.
Rollback uses the exact saved preimage under the ignored
`obj/chi-refill-validation/boss-hud-space/originals` directory, only while the
current file matches the above postimage. Published archives remain immutable.
Decompilation evidence stays outside Git under `research/isolated/ghidra_scripts/gamedata-*.txt`.
