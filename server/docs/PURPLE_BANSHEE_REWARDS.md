# Purple Banshee victory rewards

Current follow-up: the [Purple Banshee encounter candidate](PURPLE_BANSHEE_ENCOUNTER.md)
now connects these participants to owned item confirmation, combat and victory.
Earlier dormant/source-integration statements below describe their historical
batches; native/live acceptance and catalog repair remain open.

The [retained Arena-funded journey](PURPLE_BANSHEE_ENCOUNTER.md#retained-arena-funded-house-hunt-2026-10-01)
now verifies ordinary acquisition, normal combat, exact 200-Study/bound-P7 reward
persistence and replay protection across private process restarts. Deployment
and native acceptance remain pending.

## Reward contract

The local target-client tooltip supplies 200 Study Points for the house summoner
and 50 for teammates, with possible P5/P6 souls or refinery items. Victory uses
the saved house owner for the 200-point share, even if a teammate lands the final
blow. A caller cannot omit the summoner, inject a foreign recipient or manufacture
rewards from an active/non-victorious run. The owned combat coordinator must
validate the final eligible party before calling this transaction participant.

Unpublished odds and loot count are explicit Revora choices: two independent
rolls, each 60% loaded P5/P6 soul and 40% loaded tradable level-5/6 refinery item.
Selection is uniform within each sorted catalog pool. An extra bound P7 soul is
an event reward under the owner's valuable-reward policy. P5/P6 and refinery
materials are unbound and untimed. No direct daily quest P7 reward is introduced.
Original Purple skill behavior, odds and loot count are not claimed as verified.

## Persistence and pickup

Study and all three loot receipts join the caller's victory transaction. Failure
in any receipt rolls back victory and rewards together. Study payment verifies
the durable balance, records actual 50/200-point amounts and retains entitlements
that would overflow the client balance. Concurrent/repeated callbacks cannot pay
again. Client updates use the existing Study/inventory serializers.

Ground loot has a two-minute lifetime and existing owner/team protection. Saved
uncollected drops recover only into the original canonical house, terrain and
house level. Pickup checks origin identity, binding, item state, house ownership,
position, inventory capacity and durable claim in the same item-insert transaction.
It preserves bound P7 through production reload. Collected or expired loot never
reappears. House construction is blocked during an active run and while unexpired
spoils remain, so an upgrade cannot strand them in the old house.

Recovery polls bound its online-player and ground batches to 256 and each Study
claim to 64 receipts. Initialization/Tick are not wired to the production Host in
this batch. Shared floor handlers only recognize drops registered by this
participant; no client packet alone can create an entitlement.

## Acceptance

The isolated build and **261 checks** pass: 139 Purple reward, 28 Purple
admission, 79 Sword Master and 15 quest-packet checks. Logs are ignored under
`obj/chi-refill-validation/`: `purple-banshee-rewards-tests.log`,
`purple-rewards-admission-regression.log`, `purple-rewards-sword-regression.log`
and `purple-rewards-packet-regression.log`. Build SHA-256:
`146e933c2df2de48e080f5c9f7a553ec6e3bdb20445acb3a3b6b61f6af78f615`.


The fixture sets up admission through the real house transaction but marks
victory explicitly to exercise the reward participant. It is not a combat test.
It covers both reward branches and invalid draws, frozen recipients, duplicate
victory, teammate kills, concurrent Study credit, malformed/full-bag/concurrent
pickup, binding tampering, injected receipt/balance/item write failures, reload,
overflow, recovery, changed ownership, expiry and pending-loot house protection.

Ordinary Honor acquisition, item confirmation, boss combat, atomic owned victory,
interruption cleanup, catalog repair, genuine client behavior and live deployment
remain open. See [admission scope](PURPLE_BANSHEE_ADMISSION.md).
