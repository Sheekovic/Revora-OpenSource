# Way of Heroes: make or join a team

Implemented and privately validated 2026-10-02; not deployed live.

Installed stage203 (task12, level31 opening) asks the character to make or join
a team and awards one bound hour EXP Potion723017. Creating a one-member team
qualifies; requiring a partner would contradict the creation alternative.

The existing packet handlers now record successful creation or accepted joining
after publishing actual owned membership, under the existing participant locks.
revora_team_earning retains the first success per character: leader UID, UTC time
and kind0 (creation),1 (accepted invitation) or2 (accepted application). Repeated
success does not rewrite the first receipt. Unanswered or unsolicited requests
cannot write it. The claim reads this durable evidence; merely requesting a claim
never creates progress. Leaving/disbanding or restarting does not erase credit.
Old unrecorded teams are not backfilled: players can form or join another team.

The writer checks current canonical sessions and membership, locks the saved
owner and commits the receipt. Persistence errors fence the earning session for
reload. The milestone uses the existing atomic item/stage/unique-claim transaction.
No packet layout, team consent rule, GM restriction or production deployment changes.

## Private process checks

CheckCandidateTeamClaim.cs uses three ordinary non-GM level50 characters. Level is
a supplied prerequisite. Normal sealed1023 packets create a team, invite/accept
one player, accept the other player's join request, leave and dismiss.

- [x] Characters without evidence refuse203 without changes.
- [x] Duplicate creation preserves the original receipt.
- [x] Unsolicited acceptance and unanswered invitation/application cannot qualify.
- [x] All three successful routes save the expected owner/leader/kind.
- [x] Both members leave and the leader dismisses before claiming; credit remains.
- [x] Eight concurrent claims per character award one bound potion each. Normal
  item use consumes each once and saves3600 bonus seconds; replay adds nothing.
- [x] Full private game/database restart preserves exact scoped team evidence,
  claims, inventory, potion-use and timer snapshots. Teams are absent after reload;
  repeated milestone requests remain harmless.

Game SHA256:
83B912EE4FD6FC426D5DA608F39AB13FA4493C5471EA4BCDE9F3C7214180D14D.
Build completed with existing warnings using the committed-source overlay,
excluding unrelated owner WIP. Initial private checks exposed missing restored
stats and social-list loaders in the new fixture; team creation was refused with
zero maximum HP, then invitation encountered an unloaded social list. Fixture
setup now uses the normal loaders. A namespace ambiguity was corrected before
rerunning. The saved characters and first creation receipt were retained rather
than reset. Final earning and cold reload passed. This does not claim elapsed
potion countdown testing, a database-fault fixture, native acceptance or deployment.
