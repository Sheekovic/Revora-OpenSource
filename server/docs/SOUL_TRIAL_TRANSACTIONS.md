# Soul Trial completion and earning

2026-09-21. Source implementation and isolated server checks are complete for
the existing Revora Soul Trial. Genuine 6711 interaction/display and live
acceptance remain pending. This is a Revora quest, not a claim of retail parity.

Caelan remains at Twin City 1002 (319,295). An ordinary authenticated player
accepts once per rebirth stage and can travel through his three existing options.
The default hunt is 50 Birdmen on Bird Island, 50 Macaques on Ape Mountain and
50 Sand Monsters in the Desert. The fixture checks that the selected installed
families have spawn rows on those maps, then drives registered monster deaths
through `Entity.Die`, rather than assigning successful counters for the earned
route. Mid-hunt reloads retain each map's earned progress.

## Owned completion

NPC requests require the canonical loaded player, the registered nearby NPC,
a living character and no active trade/booth. The owner inventory lock serializes
claims against other item/profile operations. The variable lock protects the
complete progress snapshot through the database transaction.

One transaction commits the bound class-oriented P7 soul, existing unbound EXP
Potions, silver/RVP purse, progression profile, quest-variable completion image
and a unique `(uid, life)` receipt in `revora_soul_trial_claims`. It checks every
write result. Silver uses its loaded durable preimage; RVP and the full profile
also reject stale writers. Items and client values are projected only after
commit. Any failed or ambiguous write requires reconnect before another claim.

The default rewards remain 50,000 silver, 500 RVPs and five EXP Potions. A player
below the configured target receives the existing level puff (90/100/110 by
rebirth stage). A player already at the target receives ten additional potions.
The old first-life installed-stat calculation, Dragon-to-Warrior stat mapping,
reborn free-attribute calculation and overflow saturation are preserved. This
change does not redesign those legacy balance choices. EXP resets only on a puff.

Existing completion bits still prevent retroactive rewards. A saved receipt also
repairs stale completion variables when the player visits Caelan, preventing an
older session's variable save from reopening payment. The prior policy capped
rebirth stages above two to the same completion bit; the receipt preserves this.

## Earning protection and test scope

Only nearby registered wilderness deaths with matching installed family metadata
count. Alive monsters, companions, guards, missing/replaced bodies, wrong maps,
wrong owners and fabricated families cannot earn progress. Each monster life
credits once; the existing normal respawn path clears its weak per-life marker.
Each credited kill saves its counters before progress notification. Acceptance
and rebirth reset save their related variables together under the variable lock.

`Verify-PotencyHunt.ps1 -SoulTrialOnly` passes **510 checks**, including the
existing 403-check first-rebirth prerequisites. Soul Trial coverage includes:

- Actual 150-death earning for each configured rebirth stage, NPC travel,
  mid-hunt fresh reload, concurrent claims, exact bound item UIDs and purse/stat
  persistence after a fresh character/item/variable reload. No post-claim entity
  save is used to make the reward durable for those reload assertions.
- Suppressed purse, progression, soul, potion, variable and final receipt writes;
  all rewards roll back, preserved completion can be retried after reconnect.
  These fault fixtures prepare completed counters; they are separate from the
  actual earning routes above.
- Stale silver, RVP and progression preimages; overflow refusal; full and exact
  bag capacity; extra potions instead of a puff; legacy paid completion; stale
  flag recovery from a receipt; remote claims; invalid kill sources; corpse
  replay, revival and persisted rebirth reset.

The fixture uses ordinary non-GM player accounts. The three stage scenarios are
configured rebirth profiles, not a new end-to-end proof of crossing both rebirth
rites with one character. It exercises server packet handlers and production
persistence; it does not prove native client presentation or purification use.
Class-oriented catalog selection is documented separately in
[the candidate audit](LEGACY_STARTER_QUEST_AUDIT.md). No class-exclusive use claim
is inferred from catalog names or the all-zero catalog class fields.

Validation: isolated legacy server build, 510 checks, 29/29 candidate catalog
verification and the unchanged achievement-producer audit (240/337 IDs present,
97 missing). Fixture log: `obj/chi-refill-validation/soul-trial-atomic.log`
(ignored). No live schema changes, release installation or restart occurred.


## Actual ordinary access (2026-09-27)

An ordinary authenticated character accepted Caelan's trial, visited all three
configured wilderness destinations through his actual dialog, and retained the
accepted zero-kill state across a real game/database restart. All six travel
checks passed. [Evidence and limits](ROADMAP_STARTUP_ACCEPTANCE.md#soul-trial-ordinary-acceptance-and-travel-2026-09-27).
Actual socket combat, turn-in/reward recovery and native acceptance remain open.


Combat follow-up: the same ordinary character now earned two real kills in each
required family over actual Game TCP, with progress retained across restarts.
[Exact scope](ROADMAP_STARTUP_ACCEPTANCE.md#soul-trial-actual-monster-combat-2026-09-27).
Full50/50/50 completion and actual socket reward acceptance are still pending.


## Full ordinary socket earning and restart (2026-09-27)

The same character has now completed50/50/50 through real combat, claimed once
through Caelan, and retained the exact paid receipt and all16 permanent reward
items after a real game/database restart. Immediate updates matched500 RVPs,
50000 silver,15 unbound EXP Potions and a bound P7 MoonHeadgear820073; level120
and CPs were unchanged. Both same-session and post-restart replay were refused.
[Evidence and remaining gates](ROADMAP_STARTUP_ACCEPTANCE.md#soul-trial-full-earning-reward-and-restart-2026-09-27).
This supersedes the preceding pending life0 combat/turn-in entries, not their
native, in-flight failure or deployment gates.

## Fresh second-reborn earning (2026-10-03)

The production-created character now earns50/50/50 through calculated attacks,
reloads before claiming and retains the exact once-per-life rewards across a
second restart. Its32 prior items are preserved through public warehouse packets.
[Evidence and controlled limits](CAREER_STARTING_BOUNDARY.md#fresh-soul-trial-and-public-warehouse-2026-10-03).
This closes that starting-qualification gap for life2; it does not claim newly
earned life0/life1 trials, native presentation or deployment.
