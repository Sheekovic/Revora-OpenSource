# BP PK server implementation contract

Baseline `6391de8`, 2026-09-15. Server routes implemented; native acceptance and
the original halo projection remain pending. Not deployed.

Use the [reviewed original event evidence](BP_PK_RESEARCH.md): three simultaneous
Battle Power divisions, one entry per hero, last-survivor combat, and the original
300/500/1,000 CP, 5,000/10,000/15,000 Chi and one corresponding bound token.
The seven-day honor is retained as a saved result entitlement; native halo
identity remains unresolved and must not be replaced with a Class PK effect.

Explicit Revora choices resolving unpublished or ambiguous details:

- First four Sundays of each calendar month, 20:00-21:00 Pacific. Registration
  closes at 20:10; combat starts then. A fifth Sunday has no BP PK round.
- Divisions are 250-300, 301-350 and 351+. Admission checks the full current BP.
  The saved round reservation prevents changing divisions or rejoining after
  elimination. Later loss of a shared bonus is allowed; exceeding the division's
  upper bound forfeits eligibility before further accepted combat.
- Each division uses a private copy of the validated 1730 arena. Keep original manager
  coordinates 295,141, permitted by the server floor (stored zero is walkable).
  Place the manager with new server UID 91050 and the installed
  manager presentation (NpcType3727); this UID is not a client presentation ID.
- At least two real admitted competitors and positive accepted damage are
  required for a champion. No sole-entrant prize, re-entry, deadline tiebreak,
  companion entrant or client-reported winner is accepted. Restarts cancel open
  rounds and preserve already committed results and claims.
- Monthly seasons keep the four tokens meaningful. Tokens expire at the next
  month's first Sunday at 20:00 Pacific. Unclaimed CP/Chi purses remain available;
  an expired season no longer issues a redeemable token. Previously issued tokens
  remain physical items until discarded, but their earned ledger expiry prevents
  exchange. No unrelated inventory is deleted by a timer.
- Exchange exactly two, three or four owned, same-kind, same-season earned tokens
  using the original Chi table. Arbitrary item IDs, copied items or legacy tokens
  with no champion-origin record confer no reward.

Persist reservation, admission, participation evidence, contested result, purse
claim, token origin and token exchange under distinct durable identities. CP and
Chi updates must compare their loaded saved preimages and commit with reward
items and receipts. Full inventory, wallet overflow, duplicate requests, failed
inserts and reconnect preserve unpaid purses. Expired seasons stop token issuance
and redemption, while CP/Chi claims remain available.

No new packet family is introduced. Use existing NPC dialogue, owned tournament
movement/combat, item updates, CP projection and 2534 Chi updates. Native manager,
arena, achievement, Chi and halo acceptance remain separate incomplete gates.

## Ownership and recovery

BP kinds 20-22 are separate from weekly, monthly, class and couple identities.
The shared combat coordinator owns all map entry, accepted damage, elimination
and finalization. Reservation uniqueness is `(round_start, uid)` across the three
BP groups; existing `(run, uid)` entries retain admission/competitive state.
A recovery record captures prior PK mode and arena ID before teleport. Its final
admission update commits with the admitted timestamp. Both interrupted transfers
and already-admitted entrants recover through the production login hook, including
when the old dynamic map and its metadata are absent after restart. The entity
loader captures the saved map before its general fallback changes it to Twin City,
so recovery can still identify the admission and restore the prior PK mode.

Championship results retain their original decision time through retries and
store a seven-day honor expiry. The ordinary classic-PK claim and halo queries
exclude BP kinds, preventing accidental Class PK payouts or unrelated effects.
BP prize receipts, earned token origins and exchange receipts have separate tables.
Only a saved owned token can be redeemed; wallet and item preimages plus row-count
checks cover both throwing failures and triggers that silently suppress writes.
All data fixtures use the isolated validation schema; no public game data changed.

The immutable token origin controls season expiry. Physical expired tokens can be
discarded normally and are not erased by a global inventory scan. A one-second
per-session exchange debounce complements permanent spent-token and claim receipts;
the inherited NPC protocol has no verified request nonce. Debounce is not presented
as proof that distinct later user requests can always be identified as duplicates.

## Remaining acceptance

The original seven-day halo has a saved entitlement but no proven effect index.
Do not mark the whole event complete until that presentation is resolved and
validated. Genuine-client checks still include manager/map access, admission and
exit, PK mode restoration, registration preparation, BP ceiling changes, reward
notifications, token exchange and expiry. The NPC describes the selected Revora
season rules in an optional page; its opening stays brief.

## Validation checkpoint

The isolated build passes 100 direct BP PK checks. They exercise all three real
NPC admission/claim routes, separate arenas, registration/ceilings, the matching
achievement, outside/cross-group attack rejection, exact original rewards, all
nine earned-token exchanges, replay, full bags, both wallet overflows, foreign and
locked items, expiry, unlike tokens, failed/suppressed writes, recovery after
missing arenas and failed post-teleport admission, prior PK-mode restoration,
64-player capacity, daylight saving and the four-Sunday schedule.

A 23-suite regression run passes 1,975 checks covering existing PK/CTF/Arena,
quests, Chi, progression, titles, experience, auction and mail. Combat passes 669
scenarios and the source/overlay/silver audits pass 16 checks. Those regressions
preceded the final recovery-default and saved-login-location changes. The final
build passed all 100 BP checks plus 886 checks across Local PK, Class PK, Couple
PK, Elite PK, progression, experience and Battle Power. No test failure remains open.

Final isolated server SHA-256:
`02BB7BAAB4A1034A81DE16965D1CC72BBA5A83D10FF4B55EEAFE9B049F9A9199`.

Read-only manager-ID collision inspection also returned no existing NPC for 91050.
The Revora-owned x64 inspector was hash-recorded before execution as
`89B5A4B4AD9E2D0ABAF390C282FE6DC645D9207FCCD545C8050802C8AFE9F385`.
No server process was restarted and no client patch was installed or released.

## Guaranteed bound P7 champion reward (2026-09-21)

New BP tournaments award one bound catalog P7 soul alongside the division's
original CP/Chi purse and, while its season is active, its bound token. The soul
is not a season token and remains claimable after token expiry. Existing token
exchange costs, Chi outcomes and origin protections are unchanged; exchanges
consume only tokens and retain earned souls.

BP uses its own `revora_pk_wars.bp_valuable_reward` flag. It defaults false for
existing runs, including runs whose unrelated classic `valuable_reward` flag
was already true. New tournament inserts freeze it true for BP kinds only.
Neither re-opening nor claiming changes a saved contract. Old paid claims remain
closed; old unpaid runs retain the original purse/token behavior.

The nullable unique `revora_bp_claims.soul_uid` records the new item. Soul/token
creation, checked saved item preimages, CP/Chi changes, claim and token origin
commit together. Inventory capacity and its persisted preimage include the soul,
including a late claim that no longer grants a token.

Build and 104 BP checks pass. Actual admitted combat earns all three division
prizes; each champion then passes fresh production character/item/Chi loading
and claim replay with the same reward UIDs and purse. Profiles are saved before
claims. A separate old-contract fixture marks its admitted run before combat,
wins normally and claims its original token/CP/Chi once after coordinator restart.
Token exchanges retain the souls; exact capacity, overflow, throwing/silent
storage failures and expired-token claims pass. The shared tournament creation
change also passes 65 Local PK and 120 Class PK regressions.

Logs under ignored `obj/chi-refill-validation/`: `bp-pk-p7.log`,
`local-pk-after-bp.log`, `class-pk-after-bp.log`. NPC text announces the new soul.
Genuine-client reward/Chi display, the unresolved native honor effect and live
release remain pending. Fresh character fixtures do not certify socket login.

## First division saved champion across full restart (2026-10-01)

The existing ordinary fixtures4294908703/4294908704 now enter the 250-300 BP
bracket through Manager91050. Admission BP is controlled at250 using the bounded
runtime ExtraBattlePower field; no equipment acquisition or natural BP earning
is claimed. Existing profiles, marriages and earlier rewards remain retained.

The initially selected November1 window was already complete with no winners
from earlier calendar tests. Both admissions were correctly refused. Those rows
remain closed; the fixture uses the unused November8 Sunday window instead
(2026-11-09 04:00 UTC). After the ten-minute registration period, controlled
one-point accepted damage and the normal death callback produce a saved champion
and return both characters. No championship or prize row is injected.

An abrupt process exit leaves the champion unclaimed. Game/database restart
preserves the snapshot, then the ordinary manager pays exactly300 CPs,5,000 Chi,
one bound season token3006840 and one bound catalog P7 soul. The token-origin
and claim rows persist with their exact item UIDs. Repeated winner/loser claims
change no purse, item, claim or token. A further full restart retains the exact
nested event snapshot, including earlier Couple PK and Ancient Devil evidence.

Passing phases: `career-bp-win`, `career-bp-claim`, `career-bp-reload`.
Checkpoint: `revora_bp_process_probe` done=true / `BpProcessSnapshot`.
Candidate SHA-256:
`F5683ADB48B1D7015F0B998AA48F569431955CAA298FD506FE0BA78F35F93B96`.

This covers the first division's unexpired-season claim. Higher divisions,
season expiry/exchange through full processes, mid-fight interruption, lost
COMMIT replies, natural time/BP progression, combat calculation and native
acceptance remain outside this batch. Clock, approach and damage are controlled.
No gameplay source, live restart, deployment or client release changed.

## Retained earned-power champion (2026-10-01)

The retained ordinary Windwalker qualifies at 282 BP from its current saved
career/equipment, without a BP override on the winner. It enters through manager
91050, option 1, and defeats the supplied ordinary opponent through the production
melee handler. Round `pk-20-20280611-0` records two competitive admissions, the
champion, completed returns and participation achievement 11114.

Two previously earned Arena runes move into the rune bag through sealed normal
item actions, freeing two inventory slots without deleting either item. A fresh
process claims through manager option 10: exactly 300 CPs, 5,000 Chi, one bound
3006840 season token and one bound P7 soul. All earlier inventory UIDs remain.
The token origin/expiry, claim, Chi row, admission/result and complete prior
career snapshot survive another process restart. Two repeated claims in each
claim/reload phase leave that snapshot unchanged, including at full bag capacity.

Only the opponent's admission BP is supplied (raised to 282); this does not prove
its equipment progression. It does not retaliate. Calendar, admission wait,
proximity and attack cooldown are controlled; damage and death use normal combat.
The route covers the first division and an unexpired token. Higher divisions,
season exchange/expiry, native UI, natural scheduling and deployment remain
outside this retained route. No production code or live server changed.

Validation: `career-retained-bp-win`, `career-retained-bp-claim` and
`career-retained-bp-reload` passed against candidate SHA256
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.

## Fresh earned division, victory and gear restoration (2026-10-03)

The fresh ordinary Warrior and Trojan enter with earned Battle Power. Preflight
reports383 and269 respectively. Normal unequip packets move the Warrior's exact
headgear, necklace, armor, boots and ring into its bag, reducing power to283.
The Trojan remains269. Both qualify for250?300 without supplied ExtraBattlePower,
profile edits or a seeded opponent. The Warrior keeps this equipment configuration
through the fight and receives no in-arena equipment boost.

Round `pk-20-20410811-0` admits both ordinary players, waits the required ten
minutes under the controlled event clock, and records two calculated melee hits
for the Warrior's victory. Both return to town; normal revival handles the
loser. The unclaimed result survives a separate process boundary. The winner's
claim gives exactly300 CPs,5,000 Chi, bound season token3006840 and one bound P7
soul. Achievement11114 uses source`bp-pk-v1`. The opponent receives no claim.
Repeated winner/loser claims preserve currencies, items and results.

All five original equipment UIDs are subsequently re-equipped through normal
packets. The final cold phase requires restored383 Battle Power, both reward
UIDs in inventory and an unchanged complete nested career checkpoint.

Two fixture corrections preserve their observed history:

- The initial preparation installed the event clock too early, opening an empty
  August4 round. Restart correctly cancelled it before any admission. Only its
  state and cancellation time changed. Recovery keeps that cancelled round and
  advances to August11 without replaying equipment preparation. Full old/current
  snapshot hashes gate this probe-only checkpoint update:
  `74c322cb07efa1d99f9507ecc9a7ca530a3c881fac77af67a52971f0c0d8992f` /
  `57b078f66509c12d7ebf57955b8cfedaa8f732812a48bee1a58f6beafe271564`.
- After the successful claim was saved, the loser-refusal helper approached the
  manager, changing only its town coordinates408,330 to295,143. The complete
  snapshot comparison confirmed no item, payment, result or entitlement change.
  A second probe-only recovery is gated by
  `c6626b2c81081ba048867de2392b52f5de5efc75bee6ff6f3cfb9e664a61414e` /
  `818bbce7f093cc08a47ad32d43b6cf0996818171578f1de235b0d9ff5373ea86`.
  Future claims approach the loser before recording the replay baseline. An
  ambiguous fixture BitConverter reference was fixed before recovery execution.

Fixture:`CheckCandidateFreshBp.cs`. Private logs use
`arena-process-career-fresh-bp-{inspect,prepare,state,recover,earn,claim,claim-recover,restore,reload}.log`
under `C:\Revora\DisposablePostgres\moon-box-candidate`.
Candidate SHA-256 remains
`D48A6C00F6BF84889581ADFEABEB924C4C7448C5DC3C6D17F8674129C43665B6`.
Calendar, NPC/attack approach and cooldowns are controlled. Higher divisions,
competitive retaliation, season-token exchange/expiry and native acceptance are
outside this route. No live deployment or production source changes.

Final restoration and cold reload both passed. No extra grant or gameplay-state
repair was used for either reviewed fixture recovery.
