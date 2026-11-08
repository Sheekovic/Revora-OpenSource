# Way of Heroes: Dis City entry

Implemented and privately validated 2026-10-02; not deployed live.

Installed stage404 (task39, opening level101) asks the player to take the Dis City
challenge once and awards one bound EXP Ball723700. Actual Dis City admission
retains its independent level110 gate and Monday/Wednesday/Friday schedule.
Revora's existing Solar Saint is in Twin City, despite the installed task's older
Ape City wording; this batch does not change client text or move the registrar.

## Durable qualification

A run is saved before instance travel, so a begin record alone is insufficient.
After successful owned arrival and encounter population, DisCityQuest now records
one revora_dis_entries row per run. It verifies current player/instance ownership,
map arrival and the saved run/stage under the character lock. Re-entry retains
the original receipt. An allocation, recovery-record or travel failure before
arrival cannot award this milestone merely because a run was saved.

The claim requires that entry and its matching run-rules owner, or an existing
saved Dis City completion with matching rules. This preserves historical completed
runs without inventing old entry receipts. An older unfinished run earns entry
credit when resumed normally. Standard, Veteran and Nightmare all qualify; winning
or clearing the first stage is not added to this entry requirement.

The normal milestone transaction grants the bound item, stage state and unique
claim receipt together. Leaving Dis City does not revoke historical qualification.
No GM, owner-name or account restriction is introduced.

## Private process checks

CheckCandidateDisClaim.cs provides career-dis-claim-earn/reload with three ordinary
non-GM characters at supplied level110, one for each difficulty.

- [x] No-entry and closed-day requests refuse404 and leave owned state unchanged.
- [x] A UID-scoped private fault on the map-recovery update interrupts Standard
  entry after the run is saved. No entry receipt exists. After canonical player
  reload, that saved run still cannot claim404; resuming then enters normally.
- [x] All three difficulty offers enter the correct owned first-stage map with
  live encounter monsters. Saved difficulty and entry owner agree.
- [x] Normal return/resume/return produces one entry receipt per run and retains
  qualification after leaving the instance.
- [x] Eight concurrent claims per character yield one bound EXP Ball each.
  Normal consumption increases experience, records one use and rejects replay.
- [x] Full private game/database restart preserves exact scoped entry/rules,
  run/steps, items, milestone, experience and allowance snapshots; repeated claims
  change none. Fault trigger/function cleanup is verified.
- [x] A read-only predicate check accepts a pre-existing completion with no new
  entry receipt. This does not modify that retained character or award a second prize.

Game SHA256:
2776268AFA0D62445E95F23D4CFB97458C5D9E90B0C7649C01FC3B376757619F.
Earning and cold reload passed on the first execution of this fixture, including
the intentional arrival fault. The build uses the committed-source overlay,
excluding unrelated owner WIP. Calendar dates, level and the fault are controlled
inputs. This verifies entry and milestone use, not a new full Dis City victory,
natural elapsed-time scheduling, native client display or live deployment.
