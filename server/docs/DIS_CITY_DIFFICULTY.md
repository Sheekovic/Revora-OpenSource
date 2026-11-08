# Dis City combat editions

2026-09-21: source now offers Standard, Veteran and Nightmare through Solar
Saint. New harder-trial choices require an unexpired, consumed server offer.
Existing runs resume their saved edition regardless of the next selection.
Combined client text is privately staged; genuine-client acceptance remains open. The full dungeon
milestone is not complete.

The [official guide](https://co.99.com/guide/quests/monstercity.shtml), rechecked
2026-09-21, mentions selecting difficulty but does not publish its labels, stat
multipliers or difficulty-specific rewards. Any additional profiles must therefore
be documented as Revora tuning unless further original evidence is found.

## Frozen run identity

`revora_dis_run_rules` records run UUID, character UID, difficulty and revision.
Standard is difficulty 0, revision 1. Its immutable code profile preserves the
existing HP values: ordinary demons 100,000, guardians 500,000 and Pluto 5,000,000.
Raw attack ranges remain 2,500-4,000 and 5,500-7,500 for Pluto. These are raw
combat inputs, not promises about damage after player defenses. Catalog meshes,
other monster attributes, objectives and class quotas are unchanged.

Veteran (1/1) uses twice Standard HP and 125% raw attack. Nightmare (2/1)
uses three times Standard HP and 150% raw attack. These names and multipliers
are Revora tuning, not verified original mechanics. All three modes grant the
same stage/final rewards, including the bound event P7, and share one weekly
completion limit. Increased challenge earns a separate ranking opportunity;
it does not multiply the weekly purse.

New admission saves this identity in the same transaction as the run, first step
and Mirror of Hell. A missing/suppressed contract insert rolls the admission back.
Combat instances load their profile by run and owner; another character cannot
resolve it. Every spawned monster uses that instance's immutable HP/attack profile.
The noncombat treasury does not require or fabricate a combat profile.

Initialization transactionally backfills existing active runs and historical
completions as Standard revision 1, checks ownership and rejects inconsistent
records. Old contract rows survive replacement of the active run at the next
weekly admission. Revision-one values must not be edited when introducing a new
balance edition; add a new recognized revision instead. Unknown difficulties or
revisions fail closed rather than silently using Standard stats.

## Fair finish rankings

Each accepted finish copies difficulty and revision from its owned admission
contract inside the victory transaction. Daily top-ten ordering partitions by
both fields. Each bracket awards the existing three/two/one bound Sacred Refinery
Packs, with the existing deterministic elapsed-time/finish-time/UID tie order.
One day lock closes all brackets atomically. Claims retain the unique player/day
and run constraints, durable item receipts and duplicate-claim protections.

The schema migration is transactional. Existing finishes and prizes default to
Standard revision 1, preserving claimed item UIDs and unclaimed rights. Repeated
initialization preserves new brackets. The board identifies each mode and edition.

## Validation and remaining work

Isolated build and all 202 Dis City checks pass.
Coverage adds unsolicited/expired admission selections, actual harder monster
spawns, frozen difficulty after instance release/reinitialization, the shared
weekly limit, actual scaled Pluto HP/attack, legacy ranking schema migration
and independent top-ten/prize settlement across all three modes.
Existing full-route and transactional failure coverage is retained.

Combined client descriptions are prepared and pass 19 overlay tests with a real
byte-preservation audit ([candidate](DIS_CITY_CLIENT_STAGE.md#current-combined-review-candidate-2026-09-21)).
Ordinary-player native presentation for all three modes remains unverified. No live database, installed client or running
server was changed.
