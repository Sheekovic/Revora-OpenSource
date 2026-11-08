# Revora starter integration ? 2026-10-03

This batch integrates the reviewed starter and rebirth garment implementation into
normal server startup, login and item dispatch. It is source publication and
private validation, not a live deployment or native client acceptance.

## Rewards and ordinary access

New-character creation already starts at map1002 (327,303) and retires new grants
of legacy packs727026/723753. The durable creation entitlement delivers item727990
(RevoraStarterPack) to its ordinary account-owned character. Existing characters
without that entitlement receive no retroactive giveaway. Missing/invalid catalog
entries fail closed; the client/catalog release remains pending.

- Initial opening: bound class equipment, +12/-7/two Super gems where applicable,
  50000 bound CPs and1000000 silver. Class and item requirements remain enforced.
- Original-life milestones: level40 steed,70 crop,100 talismans,120 bound P7 choice.
  The steed and crop have no gem sockets or blessing; talisman attributes follow
  the implemented item-specific rules. Milestones do not repeat after rebirth.
- First rebirth: new-class kit and a bound garment pack723981. The pack selects
  a permanent four/five-star garment uniformly over eligible designs, with10%
  chance of blessing1. No opening currency is repeated.
- Second rebirth: new-class kit, two bound P7 choices,100000 bound CPs and5000000
  silver. The transaction consumes the handle and closes the creation entitlement.

The item is only a handle to character-owned claims. Account/session ownership,
locked durable profile, inventory capacity and currency limits guard each claim.
Stored handles are not duplicated; a genuinely lost handle can be replaced without
resetting claims. Commit failures roll back; postcommit failures require reload.
Archer arrows remain ordinary bound arrows. Warrior shield900009 requires level40.
P7 souls here are one-time starter rewards, not daily-quest payouts.

## Validation

- [x] Integrated Debug/x86 game build succeeds, with existing legacy warnings.
- [x] Focused x86 fixture compilation with warnings as errors;911 checks pass.
  Coverage includes supported class kits, eligibility/forgery, all milestones,
  concurrent/repeated claims, capacity/overflow, lost handles, garment selection,
  P7 selection and before/after-commit interruption recovery.
- [x] Separate stopped/started private process reload of the earned character
  UID1096322 passes on the integrated executable. It retains level40/class21,
  reborn2, seven equipped Warrior items,235993 CPs,150000 bound CPs,6100000 silver
  and the closed final entitlement. No progression or rewards were replayed.
- [ ] Genuine6711 login/item presentation and coordinated client/catalog release.
- [ ] Live deployment, separately gated.

Integrated game SHA256:
`3B0741F840844A1116C4FB9297E8279EF8C418BB4A3130901007CDF72A363A0E`.
Private cold phase: `career-fresh-third-shield-reload`.
The fresh-career limits in [Career starting boundary](CAREER_STARTING_BOUNDARY.md)
remain applicable; this does not certify a complete independently earned320 career.

The focused fixture uses a nullable full-shaped synthetic character table before
mentor foreign keys are installed, and initializes current Champion/TeamPK login
storage. This repairs obsolete test setup, not production database behavior.
It runs only against explicit `REVORA_QUARANTINE_TEST_CONNECTION`, validated as
127.0.0.1:25439 / revora_candidate_game; no secret is stored in source. It creates
and removes its own revora_potency_validation schema. The private owned catalog
row and installed catalog/runtime are prerequisites, not distributable Git assets.

Clean-tree invocation after preparing the reviewed private build/catalog:

```powershell
./tests/PotencyHunt/Verify-PotencyHunt.ps1 -RevoraStarterOnly `
  -BuildDirectory obj/starter-integration-validation/bin -TestPlatform x86
```

The reviewed run substituted HEAD versions of unrelated owner-modified sources
and test files. Those unrelated changes are not included in this integration.
