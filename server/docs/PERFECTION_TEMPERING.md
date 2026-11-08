# Perfection tempering and wardrobe prerequisites

2026-10-01: the owner approved tested direct progression to54 as Revora's
edition. Original level110/Super admission and shared-phase restrictions are
not pending product decisions for this edition. Existing payment and material
safeguards remain. This approval does not resolve the separately documented
intermittent initial-login failure, native presentation or deployment gates.
See [approved rules](APPROVED_ROADMAP_RULES.md).

## Atomic star-stone tempering (2026-09-26)

The previous active Temper branch scanned raw protobuf integers as material
UIDs, deleted selected inventory entries before saving the target, and could
remove non-star materials without granting progress. The replacement uses the
decoded material array and rejects invalid, repeated, foreign, locked, timed or
suspicious inputs. Bound stones cannot improve unbound equipment. Only owned,
non-cosmetic equipment and unclaimed/self-owned perfection targets qualify.

Ordinary authenticated players use the existing message3251 Temper operation.
The target and selected stones are locked in UID order under the character
lease/owner lock. Their saved ownership and the target's perfection preimage
must match. Progress and material removal commit in one transaction; client,
stats and wardrobe reconciliation follow the commit. A failed installation
fences the character so normal item loading can recover the committed result.
Replaying consumed material UIDs cannot grant progress twice.

Existing source values remain10/100/1000/10000 for stones3009000..3009003 and
level54 is the cap. Excess points at the cap are discarded; a maxed target
consumes no further stones. At present the handler admits individually stored
stones (StackSize1), up to40 distinct UIDs. Stack selection/count semantics still
need client evidence; larger stacks are rejected without loss. This is an
explicit service limitation, not evidence that those inputs are supported.

The source-model protobuf probe successfully decoded target1234/material5678;
it disproved a suspected blanket reader failure. This is source compatibility,
not a native packet capture. Existing CP boost, quicken, exchange, ownership and
signature operations are not covered by this transaction batch. Their broader
correctness remains to be audited; ordinary-session/fence admission now guards
all operation dispatch and mutations share the owner lock.

Validation uses real isolated database rows, the production packet dispatcher,
actual protobuf serializer/reader and full character/item reload. The target and
stones are supplied fixtures; this does not prove acquisition or title earning.
Build succeeds with existing legacy warnings. **417 checks pass** (406 shared
first-rebirth prerequisites plus11 focused tempering checks). Tests cover exact progress, replay/concurrency, invalid and duplicate materials,
binding, cap/overflow, rollback and committed-but-uninstalled recovery. Native
acceptance and deployment remain pending. Working-tree owner changes were
present in the isolated build.

Evidence: `obj/perfection-temper-build.log`, `obj/perfection-temper-tests.log`.
Reproduce with `tests/PotencyHunt/Verify-PotencyHunt.ps1 -PerfectionTemperOnly`.
The three perfection wardrobe rewards remain predicate-only until their earned
equipment/material journey and title thresholds are validated.


## Composition stones (2026-09-26)

The [official Perfection guide](https://co.99.com/guide/quest/perfection.shtml)
confirms +Stones as tempering materials, with their composition points; its
+6 example contributes3,240. The service now accepts individually stored
+1..+8 stones730001..730008 when the item's Plus agrees with its ID. Values come
from the existing `DataHolder.StonePlusPoints`:10,40,120,360,1080,3240,9720,29160.
The database Plus must also match before any target update/material deletion.
The same binding, ownership, cap, transaction and recovery rules apply.
Same-type gear as a minor material and stacked inputs remain unimplemented.

Build succeeds with existing legacy warnings. **427 checks pass** (406 shared,
11 prior tempering, eight exact +Stone outcomes and two ID/Plus/preimage
rejections). Evidence: `obj/perfection-plus-build.log` and
`obj/perfection-plus-tests.log`. These are supplied-material tests, not an
acquisition route. The three wardrobe rewards remain predicate-only.

The official guide also describes level110/Super-equipment admission, shared
perfection phases and server phase unlocks. Those broader admission/phase rules
are not established by the legacy Revora implementation or this material batch;
record their divergence in the remaining perfection audit before claiming
original-system completeness. No new phase gating or economy change is claimed.


## Earned star-stone supply (2026-09-26)

An ordinary saved character starts with zero RVPs and no stones. Actual scheduled
boss combat pays its durable50,000 RVP bounty. Normal Vesper dialogue then buys
Bright, Radiant and Splendid stones for400+2,000+8,000 RVPs. All three are
individually stored usable items. The actual3251 request consumes those purchased
UIDs and commits11,100 points (level10 plus1,460 progress) without another purse
charge. Replaying the UIDs does nothing. Full character/item loading preserves
the39,600 RVP balance, consumed stones, perfection owner and resulting progress.

**434 checks pass**:406 shared prerequisites,21 tempering checks and7 supply-route
checks. Production source is unchanged from `fd9f07e7`; the existing isolated
build was reused. Log: `obj/perfection-acquisition-tests.log`. The existing
`-PerfectionTemperOnly` suite now includes this route. Equipment is a supplied
Super armor prerequisite; event time and killing damage are controlled. This
proves material supply, not equipment acquisition, all wardrobe thresholds,
phase authenticity, native acceptance or deployment.

Owner choice requested: retain direct progression to Revora's54 cap with
level110/Super admission, or restore original equipment/server phase gates.
No choice is inferred from silence. Independent roadmap work can continue.


## Equipment materials (2026-09-26)

The [official guide](https://co.99.com/guide/quest/perfection.shtml) identifies
same-type composing equipment as tempering material and explicitly gives +6 gear
3,240 progress, like a +6 stone. It also distinguishes same-class tempering from
cross-class perfection exchange. Revora now admits individually stored +0..+8
materials matching the target's numeric equipment family and catalog class.
Level/quality may differ within that family. The existing composition table gives
+0 one point and +1..+8 the same values as stones; +9..+12 gear is still rejected
without loss pending contribution evidence. This is not a claim of complete
retail material compatibility.

Ownership, binding, lock/expiry and saved Plus checks apply to equipment as to
stones. Material deletion and target progression remain one transaction. Attached
`itemadding` effects are deleted in that transaction, so consumed equipment leaves
no orphan effects; a failed material delete rolls back those effects and target
progress together. Consumed UID replay cannot credit a second temper.

**443 combined checks pass** with the existing shared prerequisites, including
exact +0/+6/+8 gear progress, wrong-family refusal, full reload/replay, suppressed
material deletion preserving effects, successful retry removing effects, and the
previous +Stone/star-stone/earned-supply cases. Build passes with legacy warnings.
Evidence: `obj/perfection-equipment-build.log`, `obj/perfection-equipment-tests.log`.

Equipment is supplied for these transaction cases. Full equipment acquisition and
wardrobe-threshold earning, higher-plus material rules, stack selection, other
perfection operations and native acceptance remain unfinished. The pending
admission/phase-gate decision is unchanged. No live deployment occurred.


## Atomic ownership transfer (2026-09-26)

The live transfer branch previously deducted RVPs before saving equipment
ownership and used a combined name/UID comparison. It now commits the configured
**100,000 RVP** charge and owner UID/name together under session, character and
item locks. Saved currency, item ownership and perfection preimages must match;
trade, insufficient balance, locked/timed items and stale sessions cannot pay.
UID determines ownership even when the previous owner name matches the current
character. Unclaimed/untempered gear is not charged for a transfer; ordinary
first tempering establishes its owner for free.

A successful transfer retains the level/progress and sends the changed item and
Perfection state after commit. Repeated/concurrent requests see the current UID
and cannot charge again. Failure fences the character; normal item reload permits
retry against the saved state. The existing server price is preserved, not
replaced with the retail guide's CP price. Native price display remains unverified.

Build passes with legacy warnings and **451 combined checks pass**, including
concurrent one-charge transfer, trade/insufficient-funds refusal, suppressed item
writes rolling back payment, fresh reload/retry and unchanged progress. Existing
tempering/material/earned-supply regressions pass in the same run.
Evidence: `obj/perfection-transfer-build.log`, `obj/perfection-transfer-tests.log`.
Transfer fixtures supply the traded gear and fee; they do not prove an earned
trade-to-transfer journey. Exchange, CP boost, quicken, signature, pending gates,
complete wardrobe thresholds and native acceptance remain open. No deployment.


## Atomic exchange (2026-09-26)

Exchange now validates exactly two distinct owned equipment items, matching
binding and compatible equipment categories. Armor can cross classes; weapons
currently require the same numeric family. Foreign Perfection ownership requires
transfer first. Identical progress does not charge. The existing **100,000-RVP**
price is retained. Both item states, payment and an exchange receipt commit in
one transaction with saved preimages, UID-ordered item locks and session checks.
Post-commit installation refreshes equipment/stat/title projections.

`revora_perfection_exchanges` is initialized by the normal Perfection startup.
It records an unordered item pair and suppresses requests for that pair for one
second, including reversed-order retries and reconnects. A later identical request
is a new paid exchange, not an indefinitely deduplicated operation: the existing
request has no unique operation nonce. A two-second-later reversal is tested.
Deployment must include the new schema initialization; no live schema changed.

The new fixture also exposed `ConquerItem.PerfectionOwner` retaining trailing bytes
when clearing a name. Its existing 16-byte buffer field is now cleared before
writing, keeping runtime and saved ownership consistent across exchanges.
No packet layout changed; protocol evidence records the source-level scope.

Build passes with legacy warnings. **461 combined checks pass**, including
cross-class armor exchange, empty/self/wrong-type/binding/foreign-owner refusal,
concurrent one-fee operation, suppressed receipt rollback of both items/payment,
reload/retry, immediate unordered-pair suppression and later paid reversal.
Prior tempering, transfer and earned-stone checks also pass. Evidence:
`obj/perfection-exchange-build.log`, `obj/perfection-exchange-tests.log`.

Items/fees remain supplied in exchange tests. Full earned-equipment progression,
client price/selection acceptance, broader weapon compatibility, admission/phase
rules, higher-plus/stack material rules and boost/quicken/signature remain open.
No live deployment or patch was performed.


## Earned Super equipment target (2026-09-26)

The material-acquisition fixture no longer inserts its target armor. It reuses
the complete ordinary Family Feud route: NPC referrals, Bandit hunts, owned
encounters, maze/chamber progression, final claims and saved reload. The selected
first-reborn character opens its earned 728248 pack through ItemHandler, receives
the bound Super necklace 120189 and equips it through the ordinary item handler.
That same character then kills the scheduled daily boss, spends 10,400 of its
50,000 earned RVPs at Vesper, and tempers the earned necklace with the three bought
stones. Replay does not consume or credit them again; full character/item reload
retains level 10/progress 1460, ownership and the 39,600-RVP balance.

**566 combined checks pass** (`obj/perfection-earned-target-tests.log`). This
includes the existing Family Feud routes for all three lives plus the Perfection
transaction and supply suites; it is not 566 new or independent tests. Existing
production build reused, tests/docs only. Character level 140 and first rebirth
are supplied eligibility; combat damage, event time and quest RNG are controlled.
No target equipment, tempering stones or RVP balance is supplied to this journey.

This closes one Super target's acquisition gap, not a complete equipped set or
the 216/324/648 wardrobe thresholds. Those journeys, the pending phase/admission
choice and native/Host acceptance remain open. Signature work was deferred because
it does not establish the earning path. No live service/schema/client changes.


## Second earned equipment slot (2026-09-26)

The same first-reborn Family Feud character now continues through the complete
Desert Expedition quest, opens its earned 728250 pack and equips bound Super ring
150219 through the ordinary item handler. Normal warehouse deposits preserve the
earlier quest rewards and free inventory space; no earned rewards are deleted.
The subsequent boss/Vesper/necklace-tempering journey and full reload retain both
equipped items and all banked reward UIDs.

**648 combined checks pass** (`obj/perfection-equipment-journey-tests.log`),
including all three independent life variants of each reused quest suite and the
Perfection transaction checks. This is an overlapping integration-suite total,
not 648 new tests. Production build reused; tests/docs only. Level/rebirth
eligibility and controlled combat/time/RNG remain fixture prerequisites. The ring
is earned and equipped but not yet tempered. Full equipped-set 216/324/648 earning,
phase/admission decisions and actual Host/native acceptance remain open.
No live deployment, schema change or client patch installation.


## Third earned equipment slot (2026-09-26)

After the scheduled boss and necklace tempering, the same ordinary character
banks the remaining quest rewards using warehouse packets and follows Desert
Treasure's complete NPC, combat, vault-jump, timed capture and final-claim route.
Its existing boss-earned silver pays the quest purchase; this continuing character
receives no fixture money update. Standalone life variants retain their explicitly
supplied 200,000-silver setup. Assertions compare each character's opening balance
against the exact net cost of 93,499 silver, including quest payouts.

The earned 728249 pack opens into bound Super boots 160199, equipped through the
ordinary handler. Full reload verifies all three equipped reward UIDs, necklace
level 10/progress 1460, 39,600 RVPs, exact remaining silver and every banked reward.
**740 combined checks pass** (`obj/perfection-three-slot-tests.log`). This includes
reused quest/transaction suites, not 740 new tests. Existing production build was
reused; this batch changes tests and documentation only. The fixture working
directory is restored after boss setup before initializing the next quest's maps.

The ring and boots are not yet tempered. Full-set thresholds, remaining equipment
sources, phase/admission rules and actual Host/native acceptance remain open.
Eligibility level/rebirth and controlled time, RNG and combat remain supplied as
previously documented. No live deployment or client/schema changes.


## Fourth earned equipment slot (2026-09-26)

The continuing character completes the Moon quest's six tactic hunts and ordinary
NPC exchange for three Meteors. Its bound P7 bonus is deposited through normal
warehouse packets, preserving the reward separately from the next quest's payout.
After saved reload, Hero Wings consumes exactly one earned Meteor, including a
repeat-dialogue check, and the full combat/referral route earns Super headgear
118089. The reward pack opens and equips through ordinary handlers.

Full reload proves all four equipment UIDs remain equipped and bound, the necklace
retains level 10/progress 1460, two unused Meteors remain, all previously stored
rewards remain in the warehouse, and silver/RVP balances match the earned quest
payouts and earlier payments. **837 combined checks pass**
(`obj/perfection-four-slot-tests.log`), including the reused three-life quest and
transaction suites. Standalone Hero Wings cases still supply their Meteor input;
the continuing character earns its input. No equipment, Meteors, stones or currency
are inserted for this linked journey. Eligibility level/rebirth and controlled
combat, time and RNG remain fixture prerequisites.

Tests/docs only; production build reused. Ring, boots and headgear are still
untempered. Remaining equipment sources, full-set wardrobe thresholds,
phase/admission decisions and Host/native acceptance remain open. No deployment.


## Fifth earned equipment slot (2026-09-26)

The same character preserves Hero Wings rewards in the warehouse and spends one
of its two remaining Moon-quest Meteors on Power Book admission. It completes the
ordinary cave population, hunts, herding, timed kills, material uses, four-floor
travel, corpse burns and final NPC claims. The earned 728251 pack supplies bound
Super armor 130109, equipped through the ordinary handler. The existing standalone
Power Book test retains its explicit supplied Meteor; only the continuing journey
uses its earlier earned input.

**885 combined checks pass** (`obj/perfection-five-slot-tests.log`). Full reload
verifies all five equipped reward UIDs, necklace level 10/progress 1460, the one
remaining Meteor, 39,600 RVPs and every previously banked reward UID. The final
Power Book receipt is unique after duplicate claim attempts. These are overlapping
handler/database integration checks, not native-client or real-time combat proof.
Eligibility level/rebirth and controlled combat/time/RNG remain fixture inputs.

Tests/docs only, existing production build reused. Four pieces are untempered;
remaining weapon/talisman supply and the full-set wardrobe thresholds are not
claimed. Phase/admission decisions and actual Host/native acceptance remain open.
No deployment, live schema mutation or client patch installation.


## Earned tower and fan (2026-09-26)

The continuing character banks its Power Book rewards and remaining Meteor, then
completes the Defense Talisman quest followed by the Attack Talisman quest. Its
existing boss-earned silver funds fishing nets and fan-quest purchases; the linked
character receives no balance update. Independent life variants retain their
explicit 200,000-silver fixture setup. The tests reuse installed isolated tower/fan
scene files and retain the missing-scene activation refusal check.

Ordinary dialogue, item use, hunts and scene transitions earn bound Elite tower
202008 and bound Super fan 201009. Both equip through ItemHandler. The fan helper
returns the character produced by its internal reload, so subsequent assertions
use the current session. Full final reload verifies both talismans together with
the five earlier equipment UIDs, necklace level 10/progress 1460, 39,600 RVPs and
all banked reward UIDs. **1,023 combined checks pass**
(`obj/perfection-talisman-journey-tests.log`), including reused three-life routes.
This is an overlapping suite total, not 1,023 independent new tests.

The tower is Elite, not Super; its quality-upgrade path remains necessary if
original Super-only tempering admission is adopted. Neither talisman has been
tempered. Weapon supply, full-set thresholds, phase/admission decisions and actual
Host/native scene acceptance remain open. Tests/docs only; production build reused.
No live scene installation, schema change, deployment or client patch.


## Earned 216 and 324 wardrobe thresholds (2026-09-26)

The linked ordinary character now tempers its six earned Super pieces to level54
through the existing direct-cap rules. Every additional stone is bought through
Vesper with actual scheduled daily-boss bounties; each boss is killed through the
production combat handler and each purchase is consumed through message3251.
The test advances the event clock, controls killing damage and visits the required
locations; it is not a real-time or contested-world grind. It supplies no equipment,
materials, RVPs or Perfection levels to this linked character.

There are **50 saved paid boss runs**, **311 additional Splendid stones** and
**2,498,400 total RVPs spent**, including the initial three-stone purchase.
The exact remaining balance is1,600 RVPs. All purchased stone UIDs are consumed;
replayed temper requests cannot supply another material. Work is bounded to60
bosses and60 purchases per piece, with immediate failure on missing progress.

Eligibility is checked after each piece (54/108/162/216/270/324). Violet Lightning
Wings60086008 first appears at216 and Violet Cloud Wings60076007 at324; each is
equipped through the wardrobe handler and survives full character/item/title reload.
Great Perfection20392039 remains absent. The Elite tower is left untempered.
**1,041 combined checks pass** (`obj/perfection-earned-threshold-tests.log`).

This proves two earning paths under existing Revora rules. It does not resolve
original Super/level110 admission or shared/server phase rules: several earned
pieces are lower-level quest equipment, and the test uses the current direct54 cap.
Those owner decisions remain pending, so matrix classifications stay conservative.
648, weapon/accessory supply, original-rule fidelity and actual Host/native acceptance
remain open. Tests/docs only; production build reused; no deployment or live changes.


## Earned 648 wardrobe threshold (2026-09-26)

The same character buys RevoraMaster's five-item mount kit using boss-earned CPs,
with a durable purchase receipt and the exact configured payment. It equips the
steed, crop and Tempest Wing; the already earned quest fan/tower remain equipped.
It buys two low-level blades from an installed silver shop using earned gold,
with one atomic quantity-two purchase receipt and exact catalog payment. The
fixture resolves the shop NPC's actual map from the catalog rather than assuming
Twin City. High-level shop blades failed ordinary equip requirements; the route
therefore chooses installed stock at or below level70 and preserves all equip checks.

All twelve active Perfection slots are populated through ordinary equip requests.
Normal Vesper purchases and message3251 tempering then reach54 on each piece.
The test checks wardrobe eligibility after every completed piece through648,
including no premature Great Perfection grant at594. Great Perfection20392039
is equipped through TitleStorage and its ownership, total648 and equipped title
survive full character/item/title reload. The216/324 wing checks still pass.

**1,058 combined checks pass** (`obj/perfection-648-tests.log`): **100 saved paid
boss bounties**, **623 additional Splendid stones**, **4,994,400 total RVPs spent**
including the initial mixed-stone purchase, and **5,600 RVPs remaining**. All
purchased tempering stones are consumed. The run is bounded to120 boss kills and
60 purchases per equipment piece. Time and killing damage are controlled; this
does not measure a real-world grind, competition, combat balance or client rendering.

This closes the current-rule earning-source gap for all three Perfection rewards.
It deliberately does not certify original rules: ordinary low-level blades, the
Elite quest tower and other low-level quest pieces are admitted by current code,
and direct54 progression bypasses the unresolved shared/server phase model.
Matrix classifications remain conservative pending that decision and actual
Host/native acceptance. Tests/docs only; production build reused. No deployment,
live balance/price/rule changes, schema mutation or client patch installation.


## Atomic Perfection signatures (2026-09-27)

The [official guide](https://co.99.com/guide/quest/perfection.shtml) restricts
signatures to 9 Moons or higher and lists a free initial signature followed by
270 CP changes. The existing dispatcher charged before saving and admitted
lower-tier equipment. `PerfectionTempering.Sign` now serves ordinary eligible
players through message3251 with equipped, usable, self-owned Perfection gear
at levels18..54. The existing per-item empty-signature/free-first behavior and
270 CP price remain; the unresolved general admission/shared-phase policy is
not changed here.

The character lease/item lock and saved owner/item locks protect one transaction
for the exact CP preimage and item signature/perfection preimage. Repeated
identical text is a no-op. Failed item writes roll back payment. After commit,
CP and item updates are installed and sent; installation failure fences the
session, with ordinary reload recovering both saved text and balance.

The existing item field is32 encoded bytes. The request rejects blank text,
control characters, oversized encoding and text that cannot round-trip through
the server encoding. Clearing signatures is not supported. The item setter now
clears old bytes before writing and bounds writes to that field, avoiding stale
suffixes and corruption of adjacent fields. This is source-buffer evidence;
it does not prove native text rendering or a new packet layout.

Validation covers the ordinary free initial signature, a shorter paid replacement,
concurrent identical requests, item/character reload, invalid text, lower/upper
level rejection, another Perfection owner, payment rollback, committed-but-not-
installed recovery and a changed saved signature preimage. Build passed with existing legacy warnings; **1,205 combined Perfection checks
passed**. This suite includes prior career prerequisites and controlled supplied
signature gear; it is not a new earned-equipment or native-client proof.
Evidence: ignored `obj/perfection-signature-build.log` and
`obj/perfection-signature-tests.log`. CP boost and quicken remain unaudited;
Host/native acceptance and live deployment remain open.


## Atomic Quicken attempts (2026-09-27)

The old active Quicken branch changed item memory before saving, lacked owner
and cap checks, and indexed the upgrade catalog directly. The replacement uses
the ordinary session lease, item lock and saved owner/item locks. It verifies
the saved level, progress and Perfection owner before selecting an outcome.
Resulting level and consumed progress commit together; client stats and wardrobe
reconciliation follow the commit. A zero-progress replay cannot roll again.

This retains existing Revora behavior: chance is accumulated progress divided
by the next level's cost, capped at100%; success adds one level and either
outcome consumes all current progress. No CP/RVP fee is added. Only equipped,
usable self-owned Perfection gear at levels10..53 with positive progress is
admitted (minimum corrected in the following client-evidence checkpoint).
The original-rule admission/shared-phase question remains unresolved; this does
not claim official Quicken parity.

The next-level cost now uses the same bounded source calculation as ordinary
tempering. Before changing it, all702 rows across13 equipment groups and54 target
levels in the private runtime `item_refine_upgrade.txt` were compared to that
calculation: zero mismatches. Catalog SHA-256:
`64691DE654AD4D05FEFA758C69E7E1CC7C35729C23603F311E6F6136352BBB98`.
This is current local catalog evidence, not an independently verified native rule.

Build passed with existing legacy warnings; **1,216 combined Perfection checks
passed**, including the existing earned-equipment/wardrobe career.
Focused checks exercise controlled success/failure at50%, duplicate/concurrent
packets, cap and foreign-owner rejection, rejection of stale saved progress
before RNG, pre-commit rollback, post-commit installation failure and production
reload. Supplied equipment and controlled randomness do not prove a real grind
or native UI. Evidence: ignored `obj/perfection-quicken-build.log` and
`obj/perfection-quicken-tests.log`. CP boost, original rule decisions and
Host/native acceptance remain open; no deployment or schema change is included.


## Client-declared Quicken minimum and Boost research (2026-09-27)

The installed6711 `ini/Cn_Res.ini` declares level10 for Quick Temper through
`STR_DLG_FEFINE_IMME_TIP` (line3694). The first atomic Quicken batch preserved
the legacy handler's lower-level admission and therefore missed this requirement.
The handler now rejects levels below10 before choosing an outcome or consuming
progress. Boundary fixtures use level10,2500/5000 progress (50% chance), verify
success to11 and failure at10, and explicitly reject level9. Build passed and
**1,217 combined Perfection checks passed**, including the full existing career.
Logs: ignored `obj/quicken-level-gate-build.log` and
`obj/quicken-level-gate-tests.log`. This is a client-declared rule; native running
client enforcement and presentation are still untested.

Localization SHA-256:
`DE05F60546FE635B9D3A37C5134761DDF27D5C21851058CAC2E65E585EF76599`.

The same file declares CP-based Boost confirmations, including
`STR_DLGEQUIPREFINE_EMONEY_UPGRADE` and its `_2` form (lines3485/3492/3696).
Read-only static inspection of the approved DX9 executable, SHA-256
`3728BC3FD4E584D85301EDD2B1887A176267CDAA6356038F9951EF23E4FDED60`,
found the null-terminated resource keys at VAs0xB863EC and0xB86410, referenced
by pushes at0x4A0081 and0x4A000F respectively. Confirmation formatting near
0x4A004B consumes the target level, added progress and a value stored at
`this+0x20C`. These are static resource/formatting anchors, not a verified
network request or a proven conversion rate.

The current server Boost branch instead requires300M RVPs, deducts30M RVPs,
and mistakenly uses the wallet balance after the first deduction as progress.
A commented1.25 multiplier does not establish the client contract. Boost is
still open: trace the confirmation calculation and accepted request fields,
then implement bounded, owned, atomic CP payment/progression. No Boost pricing
or availability change was made in this correction, and no parity is claimed.


## Boost static price and request contract (2026-09-27)

Read-only audit `python tools/audit_perfection_boost_native.py` passes **59
instruction checks** against the approved DX9 SHA-256
`3728BC3FD4E584D85301EDD2B1887A176267CDAA6356038F9951EF23E4FDED60`.
It also gates `ini/info.ini` by SHA-256
`F698ABCFC6595A6ABA333B5B1C87545DD3B4891FFFA284A2A6372139B5997337`.
No client executable is executed or copied into Git.

Static findings supersede the earlier resource-only uncertainty:
- `[EquipRefine] Emoney=8, RefineExp=10`; native reads at 0x49FD8D and
  0x49FDFF feed whole-bundle division/multiplication at 0x49FEC2..0x49FECF.
  The installed client therefore budgets 8 CPs per 10 progress points.
- Confirmation handler 0x49C5EF passes operation 3, selected-item accessor
  result, count 1 and the address of confirmed spending (`this+0x20C`) to
  builder 0x8C0788. The builder selects packet 3251.
- Protobuf writer 0x8C01F5 writes operation as field 1, item as field 2,
  and the integer array as repeated varint field 4. The builder does not
  set string field 3. Native parser 0x8C089A accepts field 4 packed and
  unpacked. These are static constructor/writer/parser observations, not
  captured traffic or successful native server acceptance.
- An item flag selects either one wallet getter or the sum of two getters
  (0x49FE43..0x49FE78). Exact binding identity, wallet mapping and payment
  priority are still unconfirmed. Do not infer a bound-CP spending policy
  solely from this branch.

Boost is **not fixed or completed**. Next: resolve the wallet/binding contract,
then implement bounded atomic payment/progression, reject excess spending at
cap, and test ordinary-player ownership, malformed/native-shaped requests,
duplicate delivery, rollback and fresh reload. Existing broken RVP behavior
is unchanged by this evidence checkpoint. No production deployment or schema
change; native acceptance remains pending by owner choice.


## Atomic Revora CP Boost (2026-09-27)

The old handler requiring300M RVPs, deducting30M RVPs and converting the wallet
remainder into progress is replaced. Ordinary eligible players can boost their
equipped, usable gear at8 CPs per10 progress; the first boost assigns Perfection
ownership. Foreign-owned, locked, temporary, suspicious, stacked and max-level
items are rejected. Revora's existing level54 cap and admission rules remain.

Bound gear consumes bound CPs first, with regular CPs covering the remainder;
unbound gear uses only regular CPs. This is an explicit Revora wallet priority,
not a claim about the original server. Static audit now includes the item's
`(B)` label path and passes70 instruction checks. Requests with no string field3
are exercised through the production packet decoder and handler.

The service requires exactly one positive multiple-of8 CP amount, rejects fees
above the minimum whole bundle reaching the cap, includes existing progress and
clears the final bundle's surplus at level54. Owned item preimages and both
wallets are checked under a character/item transaction. Progress, owner, both
wallet debits and the purchase receipt commit together; committed values then
refresh wallet, item, stats and wardrobe projections. RVPs are untouched.

New startup table `revora_perfection_boosts` records the cost, bound portion,
before/after progress and purchase time. A durable one-second per-player/item
cooldown prevents immediate repeated/concurrent clicks, including across reload.
It is not unbounded exactly-once delivery: the native request supplies no nonce
or expected progress, so delayed identical requests can be new purchases.

Validation uses the isolated committed-source overlay, preserving unrelated owner
work. Controlled fixtures cover both wallets, cap rounding, malformed amounts,
foreign ownership, stale saved wallet/item/binding state, pre-commit rollback,
post-commit installation failure and production character/item reload. No live
schema change, deployment, restart or client execution occurred. Actual Host
startup with the new receipt table and native UI acceptance remain pending.

Build passed with existing legacy warnings and **1,242 combined Perfection checks
passed**. Logs: ignored `obj/perfection-boost-build.log` and
`obj/perfection-boost-tests.log`. This includes the existing earned-equipment and
wardrobe career; do not add the count to previous overlapping runs.

Follow-up: actual isolated Host startup/schema retention now passes two cycles
for the Boost table; see `ROADMAP_STARTUP_ACCEPTANCE.md`. Logged-in player
recovery across a Host restart and native acceptance remain open.


## Authenticated Boost wallet and restart acceptance (2026-09-28)

`tools/Verify-ArenaProcess.ps1 -PerfectionWireOnly -ResetOwnedFixture` passes two
actual game-process phases with an intervening PostgreSQL restart on private
executable SHA256
`59B88E6D1FCC30877C4856ABC00451786CF62A46D7BF7394B288FF5A2883573D`.
Both helper programs compile with warnings as errors. No gameplay source changed.

A non-GM Pirate buys and equips611004/612004 using the ordinary purchase/equip
handlers, then authenticates over SRP/DH and submits native-shaped3251 Boost
requests over the game socket (operation3, item field2, spending field4, no
string field3). Eligibility,1000 regular CPs,10 bound CPs, purchase silver and
one weapon's bound flag are supplied fixture controls.

Each item buys30 progress for24 CPs, reaching level1/progress0 with the same
item UID and new Perfection owner. The unbound weapon spends24 regular CPs;
the bound weapon spends10 bound plus14 regular CPs. Immediate socket wallet
updates show976/10 then962/0. The client probe reads Perfection from the existing
1008 item fields at offsets60/64/68 and from3250, verifying level, progress and
owner. This validates Revora's existing wire output, not new native ABI evidence.

The exact database snapshot includes item ID/binding/position/Perfection fields,
regular CPs, bound CPs, RVPs and both complete purchase receipts. Fresh process,
PostgreSQL restart, canonical profile/item load and fresh authentication preserve
that snapshot byte-for-byte. Four repeated confirmation requests (two per item)
after restart leave it unchanged. The clock is intentionally frozen within the
same one-second service window across processes: this proves durable cooldown
rejection, **not** indefinite deduplication of later identical purchases.

Initial fixture corrections: a snapshot column used the wrong RVP spelling;
login progress was initially expected only in3250 even though login supplies it
in1008. Both are fixed in the helper. One intermediate run also failed at the
initial action74 socket read with IOException, before Boost; its underlying
cause remains unresolved and is not counted as a repaired server fault. The
probe now records the inner socket error code, without packet bodies or secrets,
if such a failure recurs. The final unchanged-game run passed both phases.

This closes authenticated Boost purchase and ordinary restart recovery under
current Revora rules. It does not resolve Perfection's edition/admission choice,
prove interrupted-COMMIT recovery over the socket, complete the wider earning
career, or establish native UI/live deployment. The disposable clone was removed
and PostgreSQL stopped by the successful driver cleanup.


### Bounded login timing investigation (2026-09-28)

Two further private Boost purchase/reload runs passed on the same59B88E6D game
hash. They do not reproduce or resolve the earlier action74 IOException.
The first instrumented run observed initial location replies in333/386ms.
The final run measured authenticated login receive callbacks at464/564ms and
initial location replies at302/384ms. Later location barriers took25-47ms.
The separate replayed-grant rejection still closes as expected; its1/2ms callback
with `alive=False` is the intentional negative authentication test.

Source evidence: `GeneralData.AppendConnect` queues character info before its
remaining quest-recovery callbacks; `ClientWrapper.doReceive` invokes that receive
callback synchronously. The synthetic client sends action74 after character info
and its socket receive timeout is3000ms. Slow remaining recovery could therefore
cause a probe timeout, but these successful subsecond measurements do not prove
that it caused the historical failure. No timeout or server logic was changed.

The test client now reports inner SocketErrorCode, action elapsed time, time since
the last complete frame, last packet kind and complete-frame count on failures.
Optional `REVORA_LOGIN_TIMING=1` emits location-reply timing. The private Boost
fixture wraps its accepted connection callback solely to time the initial login;
it does not capture request/response bodies or modify their processing. Probe
output forwarding retains only lines with the fixed timing prefix. Credentials,
grants, public/shared keys and packet contents are excluded from these diagnostics.

The failure remains open. If it recurs, distinguish an actual socket timeout,
peer closure, stalled callback or completed callback without a location reply
using this evidence before altering timeouts or asserting a gameplay fix.
Both runs' owned database clones were cleaned up successfully. No production
restart, deployment or native-client test occurred.

### Authenticated rollback and reconnect recovery (2026-09-28)

The private `-PerfectionWireOnly` suite now runs three phases: rollback,
purchase after restart, and purchased-state reload after another restart.
All three passed on the same59B88E6D executable; no gameplay code changed.

An ordinary authenticated character submits an unbound weapon's24-CP Boost.
The existing private BeforeCommit hook throws after the transactional item,
wallet and receipt writes. The fixture verifies that the hook ran exactly once,
the complete database snapshot is unchanged, and the live wallet, level,
progress and owner remain unchanged. The session is correctly fenced with
PersistenceRequiresReload and Action0. It cannot continue purchasing until reload.

After game and PostgreSQL restart, canonical loading preserves the pre-failure
snapshot. Fresh authentication then purchases both bound and unbound Boosts,
with the existing exact-wallet and immediate packet checks. Another restart
preserves both receipts and item states and rejects repeats within the frozen
one-second cooldown. This is pre-commit rollback/reconnect evidence, not a lost
COMMIT reply or abrupt-process-interruption test. Only the unbound purchase has
the injected failure; the successful retry phase covers both wallet policies.

Fixture corrections included removing the action74 barrier after an intentional
session fence and using instance reflection for the internal reload flag. During
an intermediate run, the unrelated initial login failure recurred before any
purchase: SocketErrorCode ConnectionAborted, action32ms, idle44ms, last packet1006,
18 complete frames. This is evidence of an aborted connection, not proof of a
three-second timeout or its cause. The unchanged-fixture retry passed all three
phases. The login issue remains open; successful runs do not close it.

Both probe helpers compiled with warnings as errors. The successful driver
removed its owned clone and stopped private PostgreSQL. Native acceptance and
live deployment remain pending.
