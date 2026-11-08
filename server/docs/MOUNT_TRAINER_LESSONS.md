# Mount Trainer: atomic paid lessons

2026-10-01. Steed Race prerequisite repair; source and private process validation,
not live deployment or native-client acceptance.

## Problem and behavior

Installed Mount Trainer9881 is on Twin City1002 at430,403. The legacy lesson path
added a spell to the session and then independently deducted silver. It did not
persist the learned spell in the payment transaction, so interrupted save paths
could leave payment and learning inconsistent. Its War Cry menu advertised
1,000,000 silver while the actual eligibility check and charge were100,000.

SteedRaceSystem's existing early NPC dispatcher now owns this trainer before the
legacy switch. It preserves the four existing skill identities and actual server
prices, with concise confirmation text:

| Lesson | Skill | Silver |
| --- | --- | --- |
| Riding | 7001 | 50,000 |
| Spook | 7002 | 100,000 |
| War Cry | 7003 | 100,000 |
| Vortex | 11190 | 500,000 |

A canonical ordinary live player must stand near the installed trainer, outside
trade/booth/Poker. Forged NPC identities and remote requests are refused. The
character item lock and database owner-row lock serialize payment and learning.
The loaded silver preimage is checked; the debit, new skill and immutable
`revora_mount_lessons` receipt commit together. Cache and immediate client updates
follow commit. A failed or uncertain mutation fences the session for reconnect.
Missing or conflicting persisted skill state cannot charge a second lesson.
Already-known skills retain their level and do not charge or add a receipt.
Historical learned skills and old payments are not rewritten or charged again.
If a later legitimate character operation removes a skill, a new paid lesson can
be learned and gets a new receipt; receipts are not retroactive permanent unlocks.

## Validation

The existing ordinary event character4294908401 learns all four lessons through
Npcs.GetDialog on the loaded trainer. The three phases are
`career-mount-lessons-fault`, `career-mount-lessons-learn`, and
`career-mount-lessons-reload`:

- Remote and mismatched-NPC requests preserve the exact saved state.
- A private receipt constraint forces failure after the transaction has attempted
  both writes. Silver, skills and receipt state all roll back; the player is fenced.
  The constraint is removed, and the actual game exits abruptly.
- Fresh startup reloads the original state. Eight concurrent Riding requests grant
  one skill for50,000 silver. All four lessons cost750,000 total and create exactly
  four receipts. Repeated options charge nothing further.
- An abrupt exit immediately after purchases, without ordinary character/spell
  saving, followed by full game/database restart reloads all four available skills
  and the exact balance. Repeated NPC confirmations preserve prior event rewards,
  items, skills and receipts.

Checkpoint: `revora_mount_lessons_probe` done=true / `MountLessonSnapshot`.
The initial probe compile used an internal field directly; it was changed to
reflection before execution. The server build passes with235 existing warning
lines, not a warning-free result. Only the stopped private candidate was refreshed;
its prior executable and manifest were preserved. Tested executable SHA256:
`3875BAE7BD0161A2F384B24296CF577647CDC660A18B50BC05A868D9F145E110`.
Logs: ignored `obj/mount-lessons-build.log` and private
`arena-process-career-mount-lessons-*.log`.

The retained main career stays at212 stored identities with mixed provenance.
This batch establishes durable Riding acquisition for an ordinary entrant; the
separate [full-process Steed Race journey](STEED_RACE_CLAIMS.md#full-process-ordinary-course-and-unpaid-prize-recovery-2026-10-01) now passes on course2063 with first/second prizes. Native
lesson/riding UI, actual socket replacement and loss of a COMMIT reply are not
proved by these checks. No live server or client release was changed.
