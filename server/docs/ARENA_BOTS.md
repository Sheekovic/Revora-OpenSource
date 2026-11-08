# Solo Arena opponents

## Owner rules

- Fill solo Qualifier Arena only below ten real signed-up participants. Count waiting invitations and active matches as signed up, not just the remaining queue.
- Prefer human matching; a waiting player becomes eligible for an AI invitation after five seconds.
- Bot matches earn the same player Arena points, wins/losses, daily participation and reward eligibility as human matches. Invitation rejection does not earn match credit.
- Use ordinary-player eligibility. No GM/name/account allowlist.
- Nine concurrent bots maximum. Names are visibly prefixed `[AI]`.

## Implementation

`ArenaBots` runs after human matchmaking. The existing native invitation, dynamic map, three-minute match deadline, outcome ledger, result retry, shutdown drain and reward delivery remain authoritative. Bots use the canonical `SessionInstall` fake-session path. `ArenaActors` extends only the solo match lifecycle; `QuestInventory.Player`, purchases and prize delivery remain human-only. Shared team admission rejects bots unless its solo caller explicitly opts in.

The persistent roster is `revora_arena_bots`, using collision-checked character IDs 1300001â€“1300009. Each has an accountless entity and the two Arena statistic mirrors required by the existing ledger. No player account, password, inventory, CPs, RVPs or silver is copied. The roster retains points and history across reuse/restarts. Bot equipment is transient and uses separate synthetic item IDs/buffers, including separate soul/refinery ownership IDs. Bot death goes directly through Arena defeat handling, not equipment drops or unrelated world rewards.

An invitation chooses a random attacking profession different from the opponent: Trojan, Warrior, Archer, Ninja, Monk, Pirate, Dragon Warrior, Fire Taoist or Windwalker. The build uses aggregate equipment and cultivation tiers, never copied item identities, stat rolls, or combat scalars. Nobility is not copied and there is no exact BP compensation.

The generator measures average enhancement/perfection, socket count, soul tier, blessing and broad Chi/Jiang quality. Full +12/P7 gear stays +12/P7; max perfection produces independently rolled levels 51-54 instead of the earlier 18-54. It includes wing and crop slots. Core items are appropriate to the selected class and player level. Chi rolls use a comparable quality band with independently selected secondary attributes, always retaining HP and the class attack attribute. Jiang uses independent, aligned stage rolls within the measured quality band. The normal stat pipeline calculates the build; no player combat values are assigned to it. Cultivation profiles stay detached from persistence and public rankings.

The world model is the supported 6711 male mesh 371006, already used by guards and adventurers. The earlier generated Body=1003/Face=10 combination was unsupported and the owner reported invisibility from entry. This fixes the model input; native confirmation remains required.

The ordinary spell catalog uses NeedXP=2 for many normal skills; only NeedXP=1 is XP-gated. CPCost describes CP training cost, not a casting charge. The old bot filters incorrectly rejected both cases and prevented ordinary spells. They now follow the normal combat gate.

AI decisions occur no more often than 240â€“360 ms, with a previous position observation, bounded candidate movement, clear-path jumps, spacing, retreat and stamina recovery. Skill attempts use `Attacking.Handle`; normal server costs, cooldowns, damage and legal targets apply. The selector does not call the mutating cooldown validator speculatively. No damage multipliers or extra HP are added by the AI. Bots do not consume player prize-ranking positions.

Operator escape hatch: start the server with `REVORA_ARENA_BOTS=0` to disable new bots. Default is enabled. Existing normal Arena draining still owns match completion. Logs are bounded to invitation/start/retirement and failures; they contain character IDs, not secrets.

## Class evidence and tactics

Official guides provide tactics, not verified 6711 numerical rules. The installed `database/magictype.txt`, learned skills, equipment checks and server combat implementation decide availability. Modern guide additions are not automatically granted. There is no claim of perfect or unbeatable play.

| Class | Implemented selection / movement | Primary research |
|---|---|---|
| Trojan | Close pressure, epic attacks only with legal paired epic weapons, line skills, healing when learned | [Trojan guide](https://co.99.com/guide/event/2014/trojan/sub_trojan.shtml) |
| Warrior | Close-range weapon attacks, shield-block preference; does not use PvE Superman as a PvP attack | [Warrior skills](https://co.99.com/guide/skills/m_warrior.shtml) |
| Archer | Ranged spacing, Rapid Fire/Star Arrow and flight; normal arrow/skill checks | [Archer skills](https://co.99.com/guide/skills/m_archer.shtml) |
| Assassin | Distant Spirit Focus setup, five-second stationary charge, interrupt to retreat under pressure, Mortal Wound/Blistering Wave | [Assassin skills](https://co.99.com/guide/skills/m_assassin.shtml) |
| Ninja | Melee burst, poison only when not already poisoned, learned utility/counter preferences | [Ninja skills](https://co.99.com/guide/skills/m_ninja.shtml) |
| Monk | Close combos, Tranquility on harmful effects, aura choice without repeated toggle | [Monk skills](https://co.99.com/guide/skills/m_monk.shtml) |
| Pirate | Black Spot before follow-up, Eagle Eye/Gale Bomb, medium-range movement | [Pirate skills](https://co.99.com/guide/skills/m_pirate.shtml) |
| Dragon Warrior | Close pressure, learned Dragon attacks and resource/defensive skills | [Dragon Warrior skills](https://co.99.com/guide/skills/m_dragon_warrior.shtml) |
| Water Taoist | Ranged attacks actually learned, healing/shield/mana priorities | [Water Taoist skills](https://co.99.com/guide/skills/m_watertao.shtml) |
| Fire Taoist | Ranged Tornado/Fire pressure, mana recovery and kiting | [Fire Taoist skills](https://co.99.com/guide/skills/m_firetao.shtml) |
| Windwalker | Chaser roster; separate Chaser/Stomper policy attacks and spacing, Healing Snow without repeated toggle | [Windwalker skills](https://co.99.com/guide/skills_-_windwalker.shtml) |

## Validation and remaining acceptance

- [x] Reviewed production-source build succeeds.
- [x] 761 compiled checks (including 27 complete generated builds across nine attacking classes, legal souls/Chi/Jiang, ordinary spell availability and terminal packets): signup cutoff/wait boundaries, class policies, obstacle/spacing preference, independent equipment/progression generation, canonical-session replacement rejection, fenced/dead/retired bot eligibility and human-service exclusion.
- [x] Main database schema probe: all nine identity/arena/teamarena/marker inserts and profile updates accepted (63 writes), then rolled back; no probe characters remain.
- [x] First native 6711 match: user accepted and defeated the mirrored Windwalker; durable result finished with two receipts and human points 4000 -> 4120.
- [ ] Revised native acceptance: different class/appearance, no mirrored noble, complete inspection, correct class label and Arena overlay closes on return.
- [ ] Ordinary-player complete match, decline, timeout, disconnect and reconnect: verify exactly one durable result and correct immediate UI totals.
- [ ] Live class-by-class combat assessment, including rune triggers, defensive timings, flight, resource exhaustion and charge interruption. Policies and a successful build are not proof of expert play.
- [ ] Fault-injected result/recovery acceptance for bot matches. Existing durable lifecycle is reused, but its new bot admission path still needs end-to-end acceptance.

No client patch is required: this reuses existing player spawn and Arena packets. Native acceptance and tactical tuning remain open until observed, not inferred from unit checks.

## First production acceptance build

Deployed on 2026-10-05. Startup confirms `[arena-bot] enabled=True human-threshold=10 capacity=9`, both Arena caches prepared, and Account/Game listeners 9958/9959 owned by the new process. Runtime and reviewed-build executable hashes match. Previous executable/PDB retained at `C:\Revora\DeploymentBackups\arena-bots-20261005-0255`.

Native acceptance is requested: reconnect through the normal launcher, sign up for solo Arena, wait 5?10 seconds, accept the named AI opponent, then complete a fight. No client patch/download is needed. This is a first playable implementation; do not mark expert class behavior, ordinary-player reward delivery or failure recovery as accepted before observing those checks.


## First owner feedback correction, 2026-10-05

The first owner match exposed an unset spouse string during inspection and missing class fields in the minimal fake-player spawn. Both are corrected. Owned match cleanup now sends the existing 2205 MatchOff message, and registered bot damage is accepted by the match scoreboard so timeout scoring includes both fighters. Retirement logs include both damage totals. The class/nobility policy above supersedes the first mirrored build. Build plus 317 bounded checks passed; native UI acceptance and cross-class tactics tuning remain pending.

Corrective build deployed after a graceful stop, process 6984; startup prepared Arena caches and listeners. Prior build retained at `C:\Revora\DeploymentBackups\arena-bot-feedback-20261005`. Client retest remains pending.


## Independent builds and scoreboard follow-up

The owner rejected normalized mirrored stats and confirmed the damage scoreboard still remained. The current generator above replaces the prior mirrored implementation entirely. Cleanup now also sends the legacy terminal 2205 action 10 (win option 1, loss option 0), which the durable exit path had omitted. Action 6/3 alone failed native acceptance. The new sequence is source-supported; native scoreboard closure remains pending confirmation.

Validation: reviewed build passes; 761 checks, including 27 full stat-pipeline builds using the installed catalog and both terminal outcomes, pass without database writes. The prior live Dragon Warrior match recorded zero bot damage; the spell-filter correction addresses a confirmed cause, but live combat effectiveness still needs retesting.

Deployed independent-build correction with graceful restart (PID 8628); prior executable/PDB retained at `C:\Revora\DeploymentBackups\arena-independent-builds-20261005`. No client patch.


## Comparable tier and visibility correction

Owner clarified that variation must not mean weaker equipment. The previous independent generator undershot full +12/P7/54 gear and omitted two slots. Aggregate tier matching now keeps upgrade quality comparable while generating a different class and independently rolled progression. Model selection reuses `CityGuardSystem.PaintLook` / `docs/WORKLOG.md` documented genuine 6711 male mesh. Installed-catalog checks also caught and removed unsupported arrow IDs from Archer generation.

Reviewed build and 1,027 checks pass, including all nine attacking classes, full-tier measurement/generation, independent progression, complete equipment slots and supported model assertions. Native visibility and live combat balance still need owner acceptance.

Tier/model correction deployed with graceful restart, PID 1672. Prior executable/PDB: `C:\Revora\DeploymentBackups\arena-tier-visibility-20261005`. Client patch not required.


## Attack uptime and roster rotation

The free-slot search always started at Kael. It now advances through the nine identities, starts at a random roster offset after restart, skips occupied slots and advances only after a successful offer.

Bots now hold valid attacking positions, stop moving during their own attack interval, use short steps to reach melee range, and close to their normal weapon range when skills are unavailable. Spell readiness checks the same weapon restrictions as the combat handler. Low HP alone no longer causes perpetual retreat. Only resource shortage initiates the recovery retreat.

The shared timer eligibility check previously dereferenced a null socket for synthetic players. It now accepts only canonical registered Arena bots through ArenaActors, rejects other synthetic/retired actors, and retains normal socket checks for real sessions. This allows the already-owned bot buff timer to run.

Diagnostics record bounded per-match combat-stat comparisons and spell/basic/movement counts alongside damage totals. Some observed matches dealt zero damage, while others dealt about 22k-26k; this was not evidence that the owner was simply too strong. Build and 1,073 focused checks passed, including roster rotation, unavailable-skill fallback, weapon legality and registered/retired timer eligibility. Native sustained combat assessment remains pending.

Attack uptime/rotation deployed with graceful restart, PID 476. Both game listeners are healthy and the live executable matches the tested build. Prior executable/PDB: `C:\Revora\DeploymentBackups\arena-uptime-rotation-20261005`. Native combat retest remains pending.

## Combat target lookup and rejected casts

Live matches after the uptime correction included Ninjas with 38/40 attempted spells and zero damage, and an Archer with nine attempts and zero damage. An attempt was not proof of execution: the magic handler clears its supplied target and resolves it through Screen.TryGetValue. WorldActorMotion notifies nearby humans but does not refresh the moving bot's screen, leaving an approaching bot dependent on the human moving/spawning into its screen.

The brain now refreshes its owned opponent in its combat screen within normal sight distance, same map and visible state. Existing match ownership and canonical actor checks run before this. A scoped, thread-local receipt from the normal PrepareSpell path acknowledges only the same actor and spell. Rejected calls back off for three seconds and allow another spell/basic attack or movement in the same decision, rather than consuming a successful attack interval. This does not guarantee damage: normal hit, defence, resource and cooldown rules still apply. Archer Bane is skipped against grounded targets. Match summaries distinguish rejected spell attempts.

Reviewed build and 1,080 checks pass, including absent-target insertion, cross-map rejection and spell/actor-specific acceptance receipts. Deployed with graceful restart, PID 1792; rollback executable/PDB under `C:\Revora\DeploymentBackups\arena-target-casting-20261005`. Native attack effectiveness remains pending retest.

## Assassin-only Archer branch and class variety

Owner requested no bow opponents. Class 45 now equips two catalog throwing knives (613 family), retains legal souls and measured enhancement tiers, learns the Assassin attack/stance/focus skills instead of bow skills, enters Path of Shadow through the normal handler, and uses ranged basic attacks when skills are unavailable. Class 45 remains the native Archer-family profession identifier; Assassin is its weapon/skill branch. Path of Shadow changes state without calling PrepareSpell, so the brain acknowledges its resulting active flag specifically.

Class offers now exclude the player's class and the last seven offered classes for the usual eight-class pool. Random choice among remaining classes gives coverage before repetition instead of unrestricted repeated random picks. Recent history is bounded to eight entries per character and 256 characters, is updated only after a successful offer, and resets on process restart/eviction. Native logs before this change already showed two Ninja opponents; this change makes their frequency predictable for non-Ninja players.

Reviewed build and 1,313 focused checks pass, including paired knives, Assassin learned skills, legal generated souls and three full class rotations for every attacking player class. Gracefully deployed as PID 6640; rollback files are in `C:\Revora\DeploymentBackups\arena-assassin-variety-20261005`. Native Assassin combat acceptance is pending.
