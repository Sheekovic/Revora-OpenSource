# Trojan Epic durable progress and reward prerequisite

Implemented 2026-09-21 in `Game/Features/TrojanEpicStore.cs`. This is a tested
storage/journal component, **not an available player quest**. Production startup
does not initialize it. The staged public/private NPC route and combat callbacks
call the store under validated actor/scene ownership. Journal hooks remain disabled
until their explicit initialization by the coordinator.
The legacy closed entrance remains closed. The completed earning-family count
has not increased.

## Contract

The twelve installed journal steps map to ordered stored stages, followed by
victory and claimed states. Each mutation requires the canonical ordinary
session, its item lock, an eligible durable character profile and a caller's
world-context predicate, rechecked before commit. Eligibility is Trojan classes
10–15, level 120 or reborn, following the published requirement rather than the
legacy second-rebirth restriction. Revision checks reject stale actions.

One free energy is available per UTC date, without banking missed days. Both
the date and ordinal have durable uniqueness constraints. UTC conversion is
explicit in SQL and the last consumed date survives a new quest cycle. Reconnect,
concurrent requests and a backwards clock cannot recreate a consumed day.

The [official guide](https://co.99.com/guide/quests/2014/trojan_epic_quest/index.shtml)
gives 90 energies and overlapping stage boundaries. Revora's explicit scheduling
interpretation is:

| Work | Actions | Paid energies in this stage | Cumulative energy |
| --- | ---: | ---: | ---: |
| Solar scraps | 27 | 27 | 27 |
| Evil array | 23 | 22 | 49 |
| Ghost Reavers | 18 | 17 | 66 |
| Solar essence | 25 | 24 | 90 |

The first action of each later stage shares the preceding boundary energy.
Array progress distributes 10,000 points over 23 actions; essence progress
distributes 99 units over 25 actions, using integer cumulative totals. These
distributions are **Revora rules**, not verified original random rolls or packet
fields. Narrative handoffs, the traitor and the final boss cost no extra energy.
There is no paid acceleration in this component.

The eighteen reavers, traitor and Devastator require owned, revision-specific
encounters. Replacing an encounter retires the old one; a victory and its story
receipt commit together. The staged combat component now validates actual owned
actors and death callbacks in private scenes; the ordinary NPC coordinator and
native acceptance remain open. See [combat scope](TROJAN_EPIC_WORLD.md#owned-combat).

The owner's valuable-reward policy is implemented as one **new unbound
level-140 FangCrossSaber (614439), +12, -7, two empty sockets** per completed
cycle. This is a Revora reward grant, not retail conversion of an equipped
weapon. Existing equipment and purification are untouched. The claim verifies
all 90 energy receipts, twenty distinct linked combat victories, exact inventory
ownership and the issued Solar Blade. Returning the blade frees the reward slot,
so a legitimate claim works with forty occupied slots. Weapon, enhancements, reward receipt, completion
count and final story revision commit together. Completed cycles persist for
future trainer unlock checks. No P7 is granted by this route.

## Borrowed Solar Blade

The `BorrowBlade` transition now issues one bound Solar Blade (3003340), its
per-cycle loan receipt and the next story revision in one transaction. A full
bag leaves the borrowing step pending. The issued UID, not just item type,
must be held in usable bag storage both when opening the Devastator encounter
and when recording victory. Generic legacy drops cannot satisfy that ownership.

Lost-item recovery restores the original issued UID while preserving progress.
It is available before or after the final victory, only when that UID is absent
from all stored items. An existing warehouse or foreign-owner item cannot be
duplicated. Concurrent recovery creates one item; failed item/receipt writes
roll back. The claim atomically consumes the issued blade and marks the loan
returned alongside the permanent reward. Failed returns or reward writes keep
the loan and victory intact. Claimed cycles cannot recover a returned loan.

These are storage APIs pending real NPC integration, not an available recovery
service or proof of native journal acceptance.

## Journal integration

`TrojanEpicJournal` supplies the twelve installed missions in story order from
saved progress, suppressing conflicting legacy entries without deleting their
stored objectives. The final mission stays accepted after victory until its
reward claim commits; a repeat cycle starts a fresh displayed route. Saved
history remains visible after a class change, while an ineligible unadmitted
character cannot expose a forged route.

The existing 1134 Begin and NPC-dialogue quit handlers refresh authoritative
progress instead of accepting client-authored changes. Two existing 1135
objective-one flags report traitor and final-boss victories, matching the local
journal's explicit `chk_quest` links. Energy and array counters are not invented
as packet fields. Full GUI refresh sends those flags from a fresh storage read.
Registration/projection/protection are all disabled until explicit initialization;
the production startup remains unchanged.

This verifies server projection and synthetic bytes, not rendering or navigation
in a genuine client. Scene links, brief NPC dialogue, reward text and the client's
level filter for reborn Trojans below level 120 still require coordinated review.

## Validation

Isolated build passed. `tests/PotencyHunt/Verify-PotencyHunt.ps1
-TrojanEpicStoreOnly` passed **1699 combined checks**, including **1433 Trojan
assertions** and the promotion fixture prerequisites. A disposable schema is
used; no live quest tables or characters were changed.

Coverage includes the full storage journey, ordinary account eligibility, exactly
90 distinct days, all twenty combat receipts, stale/missing/retired encounters,
midnight rollover, rollback of failed combat credit, cold reload without an extra
character save, concurrent acceptance/actions/claims, full bags, suppressed item,
enhancement, receipt and state writes, lost world context, stale saved class,
replaced sessions and repeat-cycle daily limits. The reward is reloaded with its
full enhancement profile. Existing promotion checks also pass.
The expanded fixture covers borrowed-item issuance, receipt failures, warehouse
duplication rejection, missing/forged item identities, late victory rejection,
concurrent recovery, post-victory recovery, atomic return failures and full-bag
exchange. All item and completion checks include fresh persistence reloads.
Journal checks follow the actual storage journey, including final victory,
claim and repeat phases. They cover conflicting serialized objectives, forged
Begin/quit requests for all twelve missions, duplicate suppression, unadmitted
class filtering, post-class-change history and 1135 flags at the serializer's
existing byte offset. The separate Warrior Epic suite passes **458 checks** with
Trojan initialization disabled, exercising the shared journal/packet integration.

## Required next work

- Activate the [implemented full NPC route](TROJAN_EPIC_WORLD.md#public-start-and-resume)
  with production timers after native acceptance. Trojan Star's referral and
  earned teaching are implemented. Public scraps/forging, private array/extraction,
  combat and final exchange are fixture-tested; actual client interactions and
  physical-item journal display still need validation.
- Initialize the implemented journal with the complete coordinator and prepare
  private navigation content consistent with scene locations and Revora rewards.
- Complete native training acceptance for the [implemented trainer and rebirth
  rules](TROJAN_EPIC_TRAINING.md); actual server rite persistence now passes.
- Native client acceptance and separately authorized deployment. Full ordinary
  NPC/combat earning and persistence fixtures now pass; these server tests do not
  close native or production gates.
