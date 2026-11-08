# Trojan Epic training and rebirth

Implemented 2026-09-21. Candidate quest/training initialization is now wired into server startup;
no live server or client was changed.

## Evidence and rules

The [official Trojan Epic guide](https://co.99.com/guide/quests/2014/trojan_epic_quest/index.shtml),
checked 2026-09-21, requires one quest completion for Breath Focus and seven for
Super Cyclone. It requires two Cross Sabers to perform Fatal Cross and Mortal
Strike and says these latter two skills must be relearned after rebirth without
recovering their former proficiency. These are published gameplay requirements,
not evidence of native packet fields.

Installed `database/magictype.txt` SHA-256
`97ECB413566F5FEA541090D1A1C1498ED2ACF0874EFA6056984583C65C187A71`
maps IDs 11960/11970/11980/11990 to Breath Focus, Super Cyclone, Fatal Cross and
Mortal Strike. Their level-zero NeedLevel values are 15/0/40/15. Revora uses
these local catalog minimums for teaching; the guide does not independently
verify these four level values. Weapon arts do not require a quest completion
to learn, but require two active sabers to perform.

## Implementation

Trojan Star's options 49 and 84 open and confirm earned lessons. Both ordinary
NPC opening and the sealed dialogue dispatcher call the owned handler. The
old unconditional four-skill grant is removed, including its Warrior admission.
Existing promotion and regular skill choices remain intact. An eligible
option-50 referral now goes to Pak's Ghost at 425,302 in Twin City and saves
arrival. Before quest initialization, these epic options explicitly remain
unavailable rather than falling through to the old shortcut.

Teaching requires an ordinary canonical character, nearby Trojan Star, Trojan
class, the catalog level, no trading/booth/poker and a recent server-issued offer.
The transaction locks the character, validates its durable profile and compares
completed journeys with reward receipts. New skill rows and individual learning
receipts commit together before updating the client. Repeated lessons do not
downgrade existing training or reinsert skills. New lessons start at zero level,
experience and previous proficiency. Existing legacy skills are not retroactively
deleted; this change closes new unearned grants.

Fatal Cross and Mortal Strike check Trojan class and two distinct equipped 614
weapons at the common CanUseSpell boundary, before cooldown/resource changes.
Alternate equipment checks slots 24/25 instead of inactive primary slots 4/5.
The shared rebirth loss rule removes both arts from first-rebirth, second-rebirth
and paid-reincarnation plans. Existing Breath Focus/Super Cyclone retention still
follows the class history matrix; no additional skill is granted by rebirth here.

## Validation scope

Isolated build and 1699 combined Trojan checks pass (1433 Trojan assertions plus
266 shared promotion prerequisites).
The dedicated `-TrojanRebirthOnly` fixture passes 491 checks, including 403
existing first-rebirth/material prerequisites and 88 additional checks. It uses
the real first-rebirth, second-rebirth and paid-reincarnation handlers for Trojan
and Warrior destinations, successful commits and injected rollback failures.

The expanded Trojan fixture teaches Breath Focus after the full ordinary NPC
earning route and reloads skills/receipts. Six/seven-completion boundaries use
explicit synthetic historical completion/reward rows, not a claim that seven
complete ninety-day journeys were exercised. Tests cover unoffered confirmation,
catalog levels, duplicate lessons, transaction rollback on receipt failure,
class/primary/alternate weapon guards and all three rebirth planning functions.
Weapon setup remains a server fixture; genuine-client casting remains open.
The subsequent full-rite fixture teaches both weapon arts through Trojan Star,
seeds former proficiency, performs each rite and reloads through production
character/skill loaders. Committed rites remove both arts in memory and storage;
failed rites preserve levels, experience and currency across reload. Reborn
Trojans relearn Mortal Strike at 15 and Fatal Cross at 40 with zero previous
proficiency; Warriors cannot relearn either art. This closes the specific
server-side rite persistence gap, not native UI/casting acceptance.

Candidate initialization/timers are connected and capacity-tested in the
[world lifecycle batch](TROJAN_EPIC_WORLD.md#candidate-lifecycle-and-capacity-acceptance-2026-09-21).
Live Host restart, matching private journal navigation and native client acceptance remain open. This does not mark the overall Trojan milestone
or a new available earning family complete.
