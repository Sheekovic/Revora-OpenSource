# Way of Heroes 350 Battle Power milestone

Stage 1001 now awards one bound Permanent Stone 723695 when current server
Battle Power reaches 350. The installed opening gate remains second rebirth,
level 121. The existing stone consumer supplies 100 stabilization points.
Implemented and privately validated on 2026-10-02; not deployed.

The claim uses the same canonical total as combat and existing BP achievements,
under the owner inventory lock and normal claim transaction. Saved level/rebirth
must match the session. No client progress, cached extra-BP display field or
equivalent 300-BP achievement substitutes for the 350 threshold. Dynamic eligible
mentor/clan sharing remains part of the canonical total, using the larger share;
this milestone does not introduce another calculation or sharing rule. Current
attainment is required before the first claim; the unique saved claim survives
later power loss and cannot pay again.

## Equipment calculation correction

SwitchEquipment restores IsWorn flags for client presentation. That flag alone
could cause source BP to count both primary and alternate nonweapon gear.
Source BP now also checks the existing AreStatsLoadable selection and nonzero
durability, matching those nonweapon combat-stat prerequisites. Empty alternate
slots retain their existing primary fallback; occupied alternate slots replace
their primary counterpart. Weapon selection, two-handed multipliers, nobility,
guild contribution, skill-PK caps and sharing formulas are unchanged.

## Private evidence and fixture recovery

The ordinary character in `tools/CheckCandidateBattlePowerClaim.cs` receives
supplied level 140, second rebirth, Warrior qualification, proficiencies, gear
and composition material. Gear is equipped through normal requests. These are
starting prerequisites, not proof of a full natural gear-acquisition career.

- [x] Refuse at 349; overwriting the extra-BP display cache does not change the
  canonical total or earn a reward.
- [x] Ordinary composition raises boots from +1 to +2, taking BP to exactly 350.
  Bagged boots reduce BP and cannot satisfy the threshold.
- [x] An empty alternate bank retains main gear as implemented by the existing
  combat selection. Occupied +0 alternate boots produce 348, not a sum of both
  pairs; switching back restores 350.
- [x] One bound stone is awarded and eight subsequent concurrent claim requests
  leave exactly one nonlegacy claim. The earned stone permanently stabilizes a
  normally applied P1 refinery. Replaying stabilization consumes nothing else.
- [x] Game and PostgreSQL restart; exact scoped equipment, effects, composition,
  currency and claim snapshots match, and replay does not award again.
- [x] After reload, pure calculation checks force the inactive IsWorn flag and
  still get 350; zeroing active boots' durability yields 340. Restore both fields
  and verify the scoped saved snapshot is unchanged. These are calculation
  fixtures, not naturally earned item damage.

The initial fixture budget omitted the server's existing seven-point Duke
contribution. It retained the original supplied crop in inventory and normally
equipped a supplied lower-plus replacement to reach 349. Its next assertion
incorrectly expected an empty alternate bank to remove main gear. The claim at
350 in that run was legitimate and was retained. The fixture resumed that
earned reward, supplied occupied alternate boots and verified the selection fix;
no claim, currency, item or checkpoint was reset. Fresh concurrent first-claim
creation was not exercised by this recovery, only the initial single claim and
eight concurrent replays. Shared-BP threshold crossing was not exercised in
this batch; underlying sharing evidence is in BATTLE_POWER_SHARING.md.

The committed-source-overlay build passed with existing legacy warnings and the
composed harness compiled with warnings as errors. Final earning/reload phases
passed on SHA-256
`26DCFFCDC9AB5A949E3B53700520CA64BF33C81230BF911E486DAAAAEB59A123`.
The final additional cold-reload calculation checks use that same executable.
Logs are `arena-process-career-battle-power-claim-{earn,reload}.log`.

Native/authenticated presentation, live release, natural prerequisites,
shared-source departure during a claim, interrupted claim commits and a fresh
concurrent first-claim race remain separate gates. No live server was restarted
or client patch published. Owner work-in-progress files remain untouched.
