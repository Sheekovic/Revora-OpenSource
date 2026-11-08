# Way of Heroes: trainer promotion

## Installed rule (2026-10-02)

Private `database/Slayer/stagegoal.txt` section `[1-5]` defines stage105,
TaskType6, progress1, opening level1 and one bound EXP Ball723700 for promotion
to a higher class title.

The claim now requires a durable `revora_achievement_evidence` row belonging to
the character, with source `class-promotion-v1` and rule_version1. `ClassPromotion`
is the owning writer; it saves the achievement receipt in the same transaction
as class transition, material debit and any promotion reward. An advanced class
number, rebirth or school change alone cannot substitute for that receipt.
Existing trainer-earned receipts qualify without another promotion.

Normal stage opening, capacity, ordinary-player/session checks, plus0/bound
reward and atomic unique claim/progress/item transaction are preserved. No new
packet, schema or promotion behavior is introduced. Main-stage prizes and
unfinished substages remain refused.

## Nine starting families and cold restart

`tools/CheckCandidatePromotionClaim.cs` adds `career-promotion-claim-earn/reload`.
Nine ordinary non-GM characters have supplied level20, no rebirth and starting
classes10/20/40/50/60/70/80/100/160. Each initially receives no milestone reward.
Normal trainer menus promote them to11/21/41/51/61/71/81/101/161 respectively.
This covers Trojan, Warrior, Archer, Ninja, Monk, Pirate, Dragon Warrior, initial
Taoist and Windwalker. The initial Taoist promotion precedes the Water/Fire fork;
this batch does not claim a separate fork test.

Each character sends eight concurrent claim requests and receives exactly one
bound plus0 single EXP Ball. Normal use and replay consume it once, increase
experience/level, and produce a saved daily allowance of one. No class-promotion
receipt, resulting profession or milestone reward is inserted by fixture SQL.
NPC proximity/map setup is controlled; dialogue handlers are invoked in process.

Ordinary EntitySave and game/PostgreSQL stop/start preserve exact scoped player
purse/inventory/Chi/Way, class/level/experience, trainer receipts and EXP Ball
allowance snapshots for all nine characters. Replaying each milestone changes
nothing. The isolated committed-source-overlay build passed with existing legacy
warnings; the harness compiled with warnings as errors. Both phases passed on
their first gameplay runs. Private game SHA-256:
`DFB0CF559E177C3640A85E429EF40CA7B70FAD5670400553E19F8ED1B63BC1ED`.
Logs: `arena-process-career-promotion-claim-{earn,reload}.log`.
Both game closes completed and PostgreSQL stopped. Previous executable/manifest
preserved; unrelated owner WIP excluded from the build.

Native/authenticated trainer/claim UI, natural starting-level acquisition,
later promotion material tiers, interrupted-claim injection and live deployment
remain pending. Supplied initial classes/levels and controlled proximity are
explicit limits. Existing broader promotion evidence remains separate; the
retained main-career checkpoint is unchanged.

## Equipment-quality reward prerequisite

The still-disabled stage302 pays item3001264. Its current legacy ItemHandler
case removes the pack and separately adds a randomly selected bound garment.
That is not the atomic ownership/reward/replay contract needed before restoring
an ordinary milestone reward. The available pool is a broad numeric range and
also needs a catalog review. This batch does not enable stage302 or claim its
reward consumer is complete. Earlier quality milestones and the Super-quality
Moon Box reward need their own predicate/consumer verification.


The subsequent [Fashion Suit pack batch](WAY_OF_HEROES_FASHION_PACK.md) resolves
the atomic-consumption prerequisite and restores the installed seven-day duration
with the owner's five-star reward policy. Stage302 earning remains disabled.
