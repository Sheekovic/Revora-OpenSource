# Battle Power sharing and threshold achievements

2026-09-15, baseline `f178e18`. Server implementation and isolated tests;
genuine 6711 presentation and deployment remain pending.

## Behavior and evidence

The [official calculation guide](https://co.99.com/guide/guides/calculate.shtml)
includes mentor and clan sharing. The [clan guide](https://co.99.com/guide/guides/family.shtml)
and [official FAQ, section 20a](https://co.99.com/guide/faqs.shtml) identify the
strongest eligible same-clan teammate on the same map. The legacy server only
considered the clan leader, omitted clan sharing from combat BP, and calculated
its margin using already-shared totals. Its mentor taper could wrap above 135.

`BattlePowerSharing` separates source BP from dynamic shares. Source BP preserves
the existing equipment and two-handed weapon policy, level, rebirth, nobility,
guild contribution capped at fifteen, and independently configured extra BP.
Mentor/clan bonuses cannot be used to increase another mentor/clan bonus.
The total adds the **higher** eligible mentor/clan share, not their sum. This is
explicit in installed `ini/StatusTips.ini` section 44, SHA-256
`6DEC8FE19FEF9EC06AA7D32BF0F16D861439421C6B1822342348C05884A5CD35`.
That client evidence corrects the additive policy initially committed in `7e93cca`.
For example, source BP 250 plus mentor 27 and clan 27 is 277, not 304; the latter
would incorrectly earn the 300 BP achievement. Both display source fields remain
available while combat and achievements use only the winning share.

- Clan sharing uses the strongest current teammate with matching loaded clan
  membership, session and map. Tower levels zero through four correspond to
  30/40/50/60/70 percent of the positive source margin, rounded down. Invalid
  tower levels grant nothing. Team and clan leadership are irrelevant.
- Mentor sharing retains Revora's existing `floor(gap / 3.3F)` policy, then
  floors the level-125-through-134 taper. Level 135 and above receives zero;
  the share is bounded at 200. Both sides must have the matching loaded
  relationship and current eligible sessions. Ambiguous duplicate child rows
  are preserved for remediation but confer no share.
- Combat totals compute eligible shares from current sources. Stale cached
  display fields cannot retain a disconnected source or fabricate extra BP.
  Refreshes preserve independent extra BP when a mentor disappears.
- Team join/remove/equipment hooks refresh all affected teammates. Existing
  mentor/stat/timer hooks also reconcile clan projections. Periodic reconciliation
  covers other membership/map changes; authoritative totals reject invalid
  sources immediately, without waiting for that projection.
- Clan member enumeration uses a concurrent dictionary. Sharing uses the existing
  mentor coordination lock and never acquires another character's inventory lock.
  It does not write relationship records or alter contribution balances.

The exact original mentor ratio and whether source margins include borrowed BP
are not established by these public guides. Retaining the inherited ratio and
excluding borrowed bonuses from both source margins are explicit Revora policies,
not a claim to have recovered an unpublished formula.

## Achievement earning

The installed `Achievement.dat`, SHA-256
`DF78DD498353B0B089F9B4228CADA5D79F4547AAE7B705349816671B2C29EBFC`,
maps 10312/10313/10314 to reaching 100/200/300 BP. These now use the canonical
server total and the existing transactional evidence writer. Ordinary players
can earn them through level, equipment and eligible shared BP. Fake, disconnected,
replaced and persistence-fenced sessions cannot establish these observations.
Earned achievements survive later stat loss and reload, and repeated observations
cannot duplicate them. Client completion requests still cannot create evidence.

Producer coverage becomes 104/337 with 233 missing identities. This is source
coverage; it does not certify a single-character 320-achievement route. BP PK
achievement 11114 remains disabled until its distinct event is implemented;
the newly resolved [event identity](BP_PK_RESEARCH.md) is a separate deliverable.

## Validation and rollout

The 59 direct checks cover taper boundaries, independent-bonus preservation,
nonrecursive mixed mentor/clan chains, parallel refreshes, canonical eligibility,
duplicate saved relationships, all five tower levels, nonleader sources,
membership/map/session changes, team-wide projection, guild cap and null subjects.
They also exercise real achievement packets and PostgreSQL evidence reload,
threshold boundaries, worn versus alternate equipment, replay and excluded actors.
The shared progression suite verifies whole-observation rollback and retry.

The 38-suite related regression pass had one obsolete growth-count expectation;
the test now scopes the original three growth identities and explicitly checks
the additional BP achievement on retry. All other 37 suites passed on that build.
Final validation passes 59 direct checks, 1,200 related checks across ten suites,
669 combat checks, and 16 audits (1,944 final checks). The earlier 33 guard and
25 gold checks also passed before the final relationship-display/team-refresh
adjustment. The initial 37 passing related suites covered 2,608 checks.

Final isolated server SHA-256:
`400FC25425DCB23B5E45E210D620F1B502027BAF50558786259A57CA29C72035`.
The combat suite passed 669 scenarios, guards 33, gold pickup 25, and source/overlay/
silver audits 16. Native rendering is not established by those managed checks.

No production instance, database, client or public patch was changed. Deployment
must preserve the original data and follow the existing rollout gates. Test native
clan/mentor BP, equipment transitions, source departure, reconnect and achievement
notifications with ordinary accounts. Exact mentor formula research and historical
duplicate remediation remain open. Do not deduplicate saved relationships merely
to enable this bonus.

## Higher-share correction checkpoint

The post-M6.38 client-tip audit resolved the overlap rule. Sixty-three direct,
625 progression/cultivation/mentor/PK/title regression, 669 combat and 16 audit
checks pass: 1,373 checks. New cases cover equal shares, a stronger clan share,
mentor fallback when the clan source changes maps, independent rounding, and
avoiding a false 300 BP achievement. Neither calculation was deployed by this work; the superseded calculation
was exercised only in the isolated validation environment.

Corrected isolated server SHA-256:
`FB636C8469B5908C985F1B8C349997988A6F186B29A8E9F5B376FC9FC3D8EA87`.

## Retained Battle Power composition and repair (2026-10-02)

The cold retained audit initially disproved its assumed current300 threshold:
total260 equals level140 + equipment92 + rebirth10 + nobility12 + guild6.
Independent/test-added BP, mentor share and clan share were all zero. Five
broken non-weapon items no longer contributed their20 points each after the
long personal-revenge fight. Historical achievement10314 remained stored; its
presence does not imply the current equipment must always supply300 BP.

`career-retained-bp-repair` repairs the actual owned equipment through ordinary
unequip, CP-shop Meteor purchases, repair and re-equip handlers. Broken items
use the service's five-Meteor rule; nonbroken damaged talismans use its silver
charge. No durability, purse or BP value is assigned directly. The first
attempt reached bag capacity during Meteor purchases. Successful mutations
were checkpointed; nine earned amulets moved temporarily to the existing
ordinary peer through reciprocal trades, then all nine returned after repair.
No items were deleted to make room, and all equipment UIDs were retained.
A missing namespace qualification was corrected before fixture execution.

`career-retained-bp-audit` then passes after fresh game/database startup with
exact prior-career snapshot equality. Total360 = level140 + equipment192 +
rebirth10 + nobility12 + guild6. Independent, mentor and clan shares remain0;
there is no team or Skill/Champion BP override. The three observed threshold
receipts remain single rows. [Runtime components and owned item identities](research/RETAINED_BATTLE_POWER_AUDIT.json).

Item contributions are obtained from the production helper and reconciled to
both source and total BP. This proves this profile's numeric composition and
absence of fixture-only BP; it is not an independent proof of the entire combat
formula or original Conquer rules. Calendar/proximity controls and prior funding
limitations remain. Private game SHA256 is unchanged:
`34868D44DF9E1EFE5948F6B846C2520579CD53625538F30DAB2BF5D2FA78B0A3`.
No live server, client or production calculation changed.

## Equipment bank selection follow-up (2026-10-02)

Source BP also checks AreStatsLoadable and nonzero durability for nonweapon
items, because SwitchEquipment can restore IsWorn flags on inactive items for
presentation. Occupied alternate slots replace main counterparts; empty-slot
fallback stays unchanged. The composed ordinary character shows350 on primary
boots and348 on its lower alternate pair, with exact cold persistence. Shared
BP arithmetic is unchanged. See WAY_OF_HEROES_BATTLE_POWER.md for fixture
recovery, the earned stabilization reward and remaining native/live gates.
