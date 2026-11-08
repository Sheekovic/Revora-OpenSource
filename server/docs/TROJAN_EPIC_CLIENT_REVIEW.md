# Trojan journal review candidate

Prepared 2026-09-21 by `tools/ClientPatches/prepare_trojan_journal.py`.
This is an unsigned, uninstalled private review artifact. It is not a release,
verified native navigation or proof that the overall Trojan quest is available.

The generator checks the approved DX9 executable hash and exact installed
journal preimage, then composes over the existing Warrior maximum-reward journal.
It rewrites twelve Trojan mission sections, updating NPC metadata, navigation
text and the owner-defined new-saber reward. Existing objective tokens, mission
ordering, eligibility fields and unrelated sections are preserved byte-for-byte.
Seven synthetic tests cover preservation/idempotence and rejection of missing,
duplicate or ambiguous mission/field data and changed objective tokens.

| Input/output | SHA-256 |
| --- | --- |
| Installed Questinfo.ini | `f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14` |
| Composed Warrior source | `160a4d649f5120216cba33f813c5612f16c291715a296bb200f4795b4e2387de` |
| Trojan candidate | `c3d232473ff879c057fa3a5725ac013e84118f3fa68aa747201b5e7d7218f5d8` |

The ignored artifact is under `obj/chi-refill-validation/trojan-epic-journal/`:
`originals/ini/Questinfo.ini`, `staged/ini/Questinfo.ini` and `manifest.json`.
The generator verifies installed/source/executable hashes again after preparing
the candidate. No active client file is modified and no proprietary file enters Git.

## Native acceptance still required

- Resolve 3825 NPC links inside actual dynamic rooms. The server terrain and
  actor coordinates are verified; client interpretation of base-map links is not.
- Confirm admission/journal visibility for reborn Trojans below level 120.
  Existing Lv_min fields remain intact; do not infer native visibility from the
  server's eligible journal projection.
- Confirm both kill flags, the physical Solar Blade objective, room transitions
  and the final victory/claim distinction on the genuine DX9 client.
- Confirm boss rendering, earned skills, active alternate weapons and relearning
  after a rite through the native UI. Server fixtures do not prove rendering.
- Compose this with the complete pending client release, review exact preimages,
  sign offline and provide transactional backup/rollback before any authorized
  publication. Candidate quest initialization/timers are now connected; live Host
  restart acceptance remains pending.

If a private-map link does not resolve, this candidate must be revised from
native evidence; its existence is not a reason to mark navigation complete.
