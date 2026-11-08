# Aura creation achievement

Installed 6711 Achievement.dat SHA-256
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`
identifies 10919 as creating an Aura. The seven existing self-cast Aura spell
paths are 10395, 10410 and 10420 through 10424. Received teammate buffs and toggle-off
are not new Aura creation and do not generate credit.

HandleAuraMonk now owns the caster inventory lock and validates the canonical
ordinary player and learned spell. It retrieves power/level data from the actual
learned catalog entry. Unknown Aura IDs cannot enter the state machine. Existing
CanUseSpell costs, cooldowns and event restrictions remain in force.

Previously switching first removed the existing Aura, then checked whether the
replacement could be used. The handler now validates the replacement first;
insufficient mana preserves the current flag and bonus. Toggle-off remains free.
Successful activation saves one earned receipt per caster, then atomically grants
10919 and completes that receipt. A failed achievement write recovers through
progression reads/login. An initial receipt-write failure has no durable retry
record. Buff/cost mutation and initial receipt save are separate operations.

The Head Abbot (NPC 4271 in map 1004) teaches these seven Auras through
MonkTraining. Existing Monk class eligibility (60 through 65) and levels are
preserved: Tyrant/Fend at 20, elemental Auras at 100. These lessons are free;
the three existing advanced arts still consume their required scrolls. The
lesson saves the skill and receipt together before exposing it in the session.
Duplicate requests preserve learned skill metadata. Failed persistence fences
the session and allows a retry after reconnect without an unsaved learned spell.
Free-lesson receipts have zero item identity/type/stack and bound=false.

Build and 60 focused checks pass with `-AuraProgressOnly`: all seven decoded
spell activation paths and toggle-off, full reload, dead/fake/unlearned/no-mana
rejection, failed switch preservation, concurrent elemental toggles without
bonus stacking, and suppressed evidence followed by login recovery. Actual NPC
packet lessons cover all seven skills, exact level boundaries, wrong class,
remote/wrong-NPC/dead access, duplicate receipts, save rollback and reconnect
retry. The ordinary-player journey learns from the trainer, reloads, then casts
for achievement credit. The existing Monk training suite passes 397 checks,
including scroll consumption, concurrency, rollback and ownership conflicts.

Qualification levels/classes remain fixtures. Event-specific behavior, teammate
aura propagation, stat-cap interactions, native 6711 presentation and deployment
remain acceptance work. These checks do not certify every Aura stat calculation
or a complete level-one player journey.


### Retained ordinary career route (2026-10-02)

- [x] Retained ordinary UID4294907001 buys Dew for2,000 CPs, pays the configured
  reincarnation fee through Sereth and becomes Monk61 at level15, retaining
  female body2001 and rebirth2. No class/level setters provide qualification.
- [x] 232 purchased EXP balls, with controlled daily eligibility, recover the
  prior level140 through normal experience/rebirth recovery. The character
  travels to the Monk trainer and learns Tyrant Aura10395 through option13.
  Training alone grants no10919 credit. Spells and career survive restart.
- [x] A decoded normal magic attack activates the learned aura and earns one
  aura-cast-v1 receipt. A third process preserves exact career/training/credit,
  the10919 packet bit and completed_at. Recasting/toggling changes no saved credit.

Phases `career-retained-aura-train`, `career-retained-aura-cast`, and
`career-retained-aura-reload` pass on private binary SHA-256
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
Position, experience-day clock and attack cooldown are fixture-controlled.
The retained character is now Monk61/level140; earlier quest rewards and all19
completed equipment chains remain saved. Funds retain mixed prior provenance.
Native aura presentation, teammate propagation and broad balance are not certified.
No live deployment. Latest316 stored/21 absent achievement identities remain a
mixed-provenance inventory, not an independently earned completion percentage.
