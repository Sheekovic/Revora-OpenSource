# Guild Beast valuable kill reward

Current process evidence: [earned Guild War funding, beast kill and post-midnight
claim across restarts](GUILD_BEAST_EVENT.md#earned-funding-and-post-midnight-process-recovery-2026-09-27).
That extends the original component-only scope below; an intermittent startup
failure, native acceptance and deployment remain open.

New owned Guild Beast kills award the existing unbound Dragon Ball plus one
bound random P7 soul. This is an event kill reward. The winning-guild leadership
fallback for an undefeated beast remains Dragon Ball-only.

The lethal combat transaction selects a catalog P7 and stores its type alongside
the saved killer and achievement. Duplicate death callbacks cannot change that
choice. Additive `soul_type` and `soul_uid` columns default to zero, preserving
older saved kills and already completed claims without inventing extra rewards.

The Beast Guard delivers both items in the existing owned claim transaction.
The killer needs two free inventory spaces; saved inventory must match live
inventory. The exact soul UID is saved with the claim. Failure to insert either
item or save the receipt rolls back everything. The existing reconnect and
post-midnight killer-claim recovery still apply. NPC dialogue stays brief.

## Validation

Staged source build and the guild-season suite passed **868 checks**. Five new
assertions verify the frozen catalog P7, duplicate-death stability, exact bound
NPC-delivered item, fresh item reload and no P7 for a no-kill fallback. Existing
fault and capacity checks now reject the second item insert after the Dragon Ball
insert (both roll back), and require two free spaces rather than one. The suite
also exercises ordinary unguilded killer access, real combat callbacks, saved
achievement recovery, stale actors/sessions, claim retry and after-midnight claims.

Tracked checks: `tests/PotencyHunt/GuildBeastTests.cs`.
Ignored evidence: `obj/guild-beast-soul-build.log`, `obj/guild-beast-soul-tests.log`.
The 868 count covers the full guild-season suite, not 868 new reward tests.
Native combat/NPC/item presentation, actual Host acceptance and deployment remain
pending. No production restart, live migration or client patch was performed.
