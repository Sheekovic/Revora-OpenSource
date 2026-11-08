# P7 reward source follow-up

Read-only source audit at `e2f5eb9`, 2026-09-21. This resolves repeat-limit
ambiguities encountered after correcting the daily quest sources. It is not a
runtime certification or an exhaustive audit of legacy NPC/item branches.
No build, client launch, migration or test suite was repeated for this record.

## Corrected daily sources

Demon Exterminators, Ancestor Worship, Daily Hunt, Rare Materials, Soul Ball,
Release Souls, Elimination, Sky Pass and Snake Islands now have new-contract
bound rune rewards. Their scoped test results are recorded in
[daily reward policy](DAILY_REWARD_POLICY.md) and the linked feature documents.
Of these, Elimination, Sky Pass and Snake Islands were missed by the original
P7-removal audit and corrected in `ed956e3` and `e2f5eb9`. Existing completed
receipts and already-issued items are not reopened or rewritten.

## Ambiguous repeatable routes inspected

| Source | Actual completion boundary | Decision under the owner clarification |
| --- | --- | --- |
| `MoonBoxQuest` | One active run; terminal completion/abandonment permits another run. Its run/result schema and admission have no daily claim key. | Retain bound P7. Repeatable is not automatically daily. |
| `LabyrinthItems.Exchange` | Kind-3 exchange consumes seventeen owned bound diamonds and writes a unique exchange receipt. No calendar claim or daily admission is used in this exchange. | Retain bound P7 beside the Ancestor Box. Do not invent a daily limit. |
| `FlameLitStore` | Admission and advancement require `GuildSeasonWar.FlameAccess` and the persisted battle window. Only stage 10 receives the full gift. | Retain bound event P7; a battle date alone does not make this a daily quest. |
| `PathHuntSystem` | `AlreadyDone` checks the saved `DoneKey` bit for `LifeBit(Reborn)`. Completion sets that bit; it is not a daily reset. | Retain the owner-approved one-time-per-life reward. |
| `AnniversaryQuest` | New bead admission is restricted to Saturday/Sunday Pacific; existing beads remain finishable. | Retain the explicitly requested recurring-weekend event reward. |
| `QuestGiftPacks` | The custom Magnolia identities 721269 and 720957 have their own item-opening receipts and fixed CP/RVP terms. | Retain bound P7 + 5,000 CPs + 1,000 RVPs under the explicit custom-pack exception. |

The remaining `QuestSoulReward.Create` call sites found in `Game/Features`
are Ancient Devil victory, BP PK, Couple PK, Dis City, Elite PK, Local PK,
Quiz results, Monk/Ninja awakening and second rebirth. Their existence does not
establish native readiness; preserve each feature's own acceptance gates.
The helper itself creates bound items from catalog purification level 7.

A separate search for direct purification-level-7 selectors also found Courier,
Soul Trial, Daily World Bosses, P7SoulPacks, Magnolia gift openings and the
Sword Master/Snow Banshee/Purple Banshee reward services. The owner explicitly
retains Courier/Soul Trial one-time gifts and event/custom-pack rewards. This
inventory is a search boundary, not proof that every legacy numeric item grant
or pack-opening path was examined.

## Remaining work

### Legacy numeric-reference pass (2026-09-21)

At gameplay baseline `036b78e`, `tools/audit_legacy_p7_literals.py` inventories
literal references to the loaded catalog's 33 purification-level-7 soul IDs and
the two recognized soul packs, 3004247/3004248. Catalog SHA-256:
`1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef`.
The inspected `Npcs.cs` SHA-256 is
`b28d917385e72c5300d48b37f07eca269d5ca78fe0ce387e585ceb279f425fe8`.

| NPC source region | Matching lines | Inspected classification |
| --- | ---: | --- |
| All Nps P7 | 4 | CP purchase branch; soul-pack selection, not daily quest completion. |
| War Award System / King plate change | 2 | War-medal exchange for soul packs. NPC 900046 is listed among Twin City closed desks; no native reachability claim. |
| Boyaca task | 12 | Calendar-event token exchanges behind an unconditional closed-stall return in NPC 900172. Not an active daily quest repair target. |
| Soul P7 / Soulp7 | 30 | Legacy direct soul shop behind an unconditional closed-stall return in NPC 442554. |
| Prizewell / PASCUA | 2 | Easter egg exchange for four of each soul pack, using the legacy session `equipo` guard. This is event code, not proof of durable or safe claims. |
| Prizewell / OBSEQUIO | 2 | Line-commented welcome-gift grants; not executable statements. |
| PkComun winner | 7 | Tournament survivor reward branches; event classification, not daily quest completion. Ownership/claim safety is outside this lexical pass. |
| NPC StuffP7 | 70 | `AddSoul` equipment creation references, not standalone soul-item rewards. No earning/persistence certification. |

Total: 129 matching source lines. The same literal scan finds zero matches in
`ItemUsage.cs`, `ItemUsageOld.cs` and `ItemUsageRenew.cs`; this does not exclude
their helper calls or computed/data-driven grants. The ignored JSON artifact
`obj/chi-refill-validation/legacy-p7-literals.json` records every line/region,
detected ID, line-comment marker and exact source-file hash. Reproduce with:

```powershell
python tools/audit_legacy_p7_literals.py > obj/chi-refill-validation/legacy-p7-literals.json
```

No additional direct daily P7 payout was identified in this bounded pass.
No rewards were removed or rewritten and no gameplay suite was rerun. This is
not a whole-program call-graph audit: computed IDs, aliases, other containers,
database rewards, dispatch overrides and live NPC placement remain outside it.
Legacy event/shop code still needs its own transactional earning review; source
presence alone must not be reported as ordinary-player availability.

### Common PK orphan claim follow-up

The admission desk 900032 was already in `TwinCityPlaza.ClosedDeskIds`, but its
17:00/22:00 popup and reward clerk 900033 were not paired with that retirement.
The legacy reward checks a live map-wide survivor count and the wall clock; it
does not prove the claimant's recorded admission, finalize a winner or persist
a unique event claim. It is unsuitable as a restored earning path.

The reward clerk now uses the same closed-handler gate, and the popup checks
whether admission is closed before inviting anyone. This restores consistent
retirement, not a working Common PK event. Existing items and claims are unchanged.
Restoration still requires eligibility (including the advertised level check),
owned admissions/combat, a finalized winner, atomic rewards, restart recovery
and ordinary-player/native acceptance. No roadmap restoration checkbox is closed.

- Daily Exorcism and Magnolia's subsequent new-contract bound-rune additions
  are recorded in [current policy](DAILY_REWARD_POLICY.md). Both still need
  genuine-client acceptance. The explicitly approved Magnolia bonus chance
  and custom packs remain unchanged; legacy branches outside the tracked
  services still require reward coverage review.
- Continue unimplemented earning paths and the genuine-client/restart acceptance
  work in the main roadmap. These source classifications do not complete them.
- Keep source checkmarks separate from deployment. The running server was not
  inspected or changed in this audit.


## Daily world-boss binding follow-up (2026-09-27)

The scheduled world-boss event is allowed to award P7, but its shared ground-drop
path omitted binding both in memory and in the inventory INSERT. Both are now
corrected for newly picked-up rewards, including restored unclaimed drops.
Existing owned items remain unchanged. The two-process ordinary-player pickup
and restart check passes; [scope and build](DAILY_WORLD_BOSSES.md#bound-p7-correction-2026-09-27).
This is a confirmed event-binding correction, not a claim of exhaustive legacy
reward coverage or live deployment.
