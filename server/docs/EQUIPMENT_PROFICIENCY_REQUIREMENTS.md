# Equipment proficiency requirement repair

The ordinary equipment eligibility check previously retained its earlier success
when no proficiency row existed. A positive item requirement was therefore
ignored for a character who had never trained that weapon family. An existing
but insufficient proficiency correctly failed, making missing records more
permissive than low records.

Both affected branches now require either a zero catalog requirement or an
existing current proficiency at least as high as the requirement. PreviousLevel
does not substitute for current proficiency. Existing reborn character level70+
and equipment level70-or-lower exemption is preserved, as is the non-reborn arrow
exemption. Gender, class, stats and equipment-level checks remain unchanged.
No packet, persistence, item catalog or reward amounts change.

Validation (2026-10-01): the private build passes with existing legacy warnings.
`tests/EquipmentAchievements/Verify-EquipmentProficiency.ps1` compiles the focused
fixture with warnings as errors and passes25 checks against the actual compiled
ItemHandler. It covers missing/low/exact/higher/zero requirements for rebirth0/1/2,
current-versus-previous proficiency, the level70 exemption boundaries, arrows,
and earlier gender/class/stat/level rejection. The fixture supplies an in-memory
profile/catalog, uses no database or listeners and earns no career credit.
The same executable fails the original missing-proficiency case against previous
private assembly2D799D06F7A28A9926D72DFCDAAC24DB135AC6495699DC147D095A86FE1C93FA.
The fixed private assembly is
984BDC6698E0D2A76C865D5F3318B47DFCF96B29704D56B2F4070FDB5B8C8161.

Only the two proficiency conditions are committed from the owner's otherwise
modified ItemHandler file. Other work is preserved. The private candidate was
refreshed with its previous executable/manifest retained; no live deployment,
restart or native acceptance was performed. The earlier full-career earning
snapshots remain historical evidence, not a claim of full end-to-end revalidation
on this new build.

## Mount armor correction (2026-10-02)

The positive-requirement fix also rejected Power Arena's earned five-star mount
armor200449 because its catalog Proficiency field is1. There is no weapon training
requirement for equipping this cosmetic slot. Both rebirth branches now exempt
`ConquerItem.SteedArmor` from the weapon-proficiency lookup, retaining level,
gender, class and stat checks. Weapon checks and the existing arrow/rebirth
exemptions are unchanged; no invented proficiency record is granted.

The focused suite passes30 checks: the previous25 weapon/eligibility cases plus
mount armor at rebirth0/1/2 with no proficiency and preserved level/gender refusal.
The118-check Power Arena suite also earns the actual mount, equips it through
ItemHandler as an ordinary character and reloads its exact UID, slot and binding.
Only the two scoped ItemHandler conditions are staged; the owner's other edits
remain untouched. No native-client acceptance or deployment is claimed.

## Low-level Warrior gear after rebirth (2026-10-03)

A normal post-boss repair exposed an inconsistency in the full equip handler:
`Equipable` accepted a level133, twice-reborn Archer's lowered Warrior helmet,
but later hardcoded Warrior helmet/armor checks rejected re-equipping it.
The [official second-rebirth advantages guide](https://co.99.com/guide/quests/2rebornadv.shtml),
read2026-10-03, explicitly permits other equipment types once the reborn character
reaches70 and the equipment is70 or lower. This supports the existing rebirth
exemption; it does not establish complete6711 equipment parity.

The two late Warrior armor/helmet checks now honor `EquipPassRbReq`.
All earlier eligibility and anti-abuse checks remain; high-level class rules,
gender requirements and separate weapon/offhand restrictions are unchanged.
This is a production correction, not an equipment grant or fixture bypass.
Only these two source lines are staged from the owner's modified ItemHandler.

The private build passed with existing warnings. The focused regression suite
passes40 checks, including both Warrior gear types at character69/70, item70/71,
non-reborn rejection and preserved gender checks. The real saved repair/re-equip
journey supplies the positive full-handler acceptance; see the maintenance evidence
for its result. No live deployment or native acceptance is claimed.
