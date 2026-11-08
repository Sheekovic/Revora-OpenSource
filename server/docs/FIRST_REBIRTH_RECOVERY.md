# First-life level and EXP recovery

M6.55a, 2026-09-16. Baseline `e97d3b7` already commits the complete first-rebirth
rite and records the original level/EXP. This follow-up uses that receipt to
restore progression once at level 130, without adding a client protocol.

## Evidence and scope

The original [rebirth advantages](https://co.99.com/guide/reborn/rebornadv.shtml)
describe recovery of the former level and EXP after reaching level 130. The
installed EXP curve is used for the recorded level, so restoring its exact raw
EXP retains the corresponding progress on that same curve. Sources and the
selected 6711 edition are recorded in [the rite](FIRST_REBIRTH_COMPLETION.md).

The prior `ExperienceMath` shortcut jumped directly to `FirstRebornLevel` but
retained only current EXP and reported one level step. It therefore omitted
former EXP and most recovered attribute points. The shared planner now
distinguishes first-life recovery at 130 from reincarnation recovery at 110.
Reincarnation behavior remains covered by its existing regression suite.

## Ownership and calculation

An idempotent nullable `recovered_at` column is added to
`revora_first_rebirths`. The original rite, stone, gift identities, former class,
level and EXP remain intact. A bounded per-session cache reads that record once,
matches its UID, character instance, former class/level and current class family,
and remembers whether it has been consumed. Later promotions in the same school
are accepted. Fake actors cannot obtain a first-life snapshot.

The planner spends EXP up to level 130, restores the recorded level and EXP,
then spends any remaining award on the restored curve. Every recovered level
adds its attribute points. Recovered levels produce no mentor credit; genuinely
earned levels still do. A ready receipt at the same level may restore its EXP
once without granting level points again. A stronger character is never reduced
or granted obsolete EXP. The configured cap, checked arithmetic and normal
maximum-level award policy remain in force.

Receipt consumption is a conditional update within the same owner-locked
transaction as character progression and other experience effects. Profile,
source and receipt failures roll back together. Post-commit cache projection
preserves the existing reconnect fence. Consumed receipts remain visible to the
planner, preventing the old level-only shortcut from replaying after a later
level reset or reconnect. Malformed, changed or missing recorded evidence is
rejected; missing first-rite data cannot quietly become a legacy fallback when
the trusted first-rebirth achievement proves that a receipt should exist.

All four shared planner entry points are integrated: ordinary
`IncreaseExperience`, Shelby's paid experience exchange, mentor EXP claims and
the earned Dark Horn exchange. The latter three already own their complete
payment/reward transactions; recovery joins them without a second commit.
No new item, NPC fee or achievement is created by recovery.

## Validation and limits

The focused suite includes the 392 first-rebirth checks and 50 recovery checks:
actual first-rite receipts; full-width EXP; exact threshold and large-award
carry; same-level and capped recovery; every destination school's later
promotion; production reload; no duplicate gifts or achievements; suppressed
writes; malformed/missing/stale receipt data; overflow; and concurrent awards
with autosaves. It also verifies actual mentor credit and successful/failed
Shelby, mentor-claim and Dark Horn transactions. Those relationships, balances
and Dark Horn provenance are fixtures for the recovery contract; they do not
claim a newly walked mentor or Dis City earning journey.

Characters with no trusted first-rite receipt retain the prior historical
level-only behavior. Their exact former EXP cannot be reconstructed from
`FirstRebornLevel` alone. Historical attribute/EXP reconciliation remains open;
no retroactive EXP is invented. This is separate from the now-complete recorded
first-life recovery path.

The source review also found legacy EXP-bearing item and compound quest handlers
that still split their cost and reward writes. Their progression passes through
this recovery path, but this milestone does not make those surrounding item,
daily-counter, currency or multi-item transactions atomic. Standard and power
EXP Balls are completed in [M6.56a](EXPERIENCE_CONSUMABLES.md); M6.56b tracks
remaining compound reward transactions explicitly.

Genuine 6711 displays and live acceptance remain pending. No live server was
restarted or deployed, and no client patch or website update was published.

Validation completed: Debug x86 build; 442 focused checks plus experience 452,
progression 74, Shelby 34, mastery 75, titles 91, Adventure 171 and default 216
(1,555 gameplay checks total); three achievement-audit tests. The producer
inventory remains 219 present / 118 absent. Focused output is retained locally
in `obj/chi-refill-validation/first-recovery.log`; regression logs share the
`first-recovery-` prefix. Final server SHA-256:
`76C0E188AB9FB2D51AC47FA244C959FE50CB766882DB2DB9C51A68DD72F24B42`.
