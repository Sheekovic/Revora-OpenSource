# Way of Heroes: Team PK participation

Implemented and privately validated 2026-10-02; not deployed live.

Stages406 (Team PK, task18) and407 (Skill Team PK, task19) each pay one bound
EXP Ball723700. Both installed definitions require participation once and open
at level101; the existing higher-rebirth gate applies.

## Revora participation definition

A player qualifies when their validated team remains admitted as the tournament
starts with at least eight eligible teams. Captains and ordinary members qualify
equally. A win, damage score or final placement is not required by these participation
tasks. Merely registering or entering the waiting area is insufficient. Cancellation
for too few teams gives no milestone credit. Once a valid tournament has started,
later elimination, departure or interruption does not erase earned participation.
This is the documented Revora interpretation of the installed ?Compete ... once?
text, not a claim of verified original retail trigger timing.

The existing start callback removes invalid teams and verifies the minimum roster
before RecordParticipation writes participated_at on the admitted owners. The
transaction locks the active run, updates only unreturned admissions and verifies
the complete requested owner count. Repeating the start preserves the first stamp.
The claim joins this durable marker with the correct tournament kind; unrelated
events and ordinary Team Qualifier results cannot qualify. Old admissions without
the new marker are not backfilled from registrations or historical prizes.

The ordinary claim transaction grants the bound item and unique stage receipt
atomically. It introduces no GM, owner-name or captain requirement. Existing entry,
round, final and wardrobe rewards remain separate.

## Private process evidence

CheckCandidateTeamParticipationClaims.cs provides career-team-participation-earn
and career-team-participation-reload. The final candidate verifies:

- [x] Two full eight-team brackets, each with sixteen ordinary non-GM characters
  at supplied level130, enter through the normal registrar dialogue handlers.
- [x] Pre-registration and waiting-room claims refuse. At the validated start,
  eight concurrent requests per character produce exactly one bound EXP Ball.
- [x] All32 milestone balls are consumed through normal use with experience gain
  and replay protection. Claiming the other tournament kind refuses.
- [x] Both brackets finish via controlled timeout outcomes and return their players.
  No combat-balance or damage-participation claim is made by this test.
- [x] Separate two-team registrations in both event kinds cancel normally, return
  eight players, leave participated_at unset and cannot claim either milestone.
- [x] Actual private game/database restart preserves exact scoped item/claim,
  experience/allowance and participation-marker snapshots for all40 characters.
  Duplicate claims and cancelled-entry refusal remain unchanged after reload.

## Diagnostic correction and limits

The first fixture incorrectly expected a two-team tournament to finish. It exposed
that the initial marker placement preceded the existing eight-team minimum check.
The final implementation moves it after that check. Four invalid private diagnostic
claims and their consumed rewards are retained as failed-test evidence on UIDs
4294911201?4294911204; they are excluded from final acceptance. No live instance ran
that intermediate build. Final positive evidence uses4294911301?4294911332;
cancellation evidence uses4294911401?4294911408. No player data was reset to make
checks pass. The final fixture also advances final-match timers held in the
separate ExtendedMatchArray, in addition to the ordinary match dictionary.

Game SHA256:
516B6B0095E3C1B8D90AEC08185D4E3AA91887C5E2E99A371B46E78174AB8211.
Build uses the committed-source overlay, excluding unrelated owner WIP. Calendar
identities, deadlines, preparation/finalization waits, levels and team formation
are fixture-controlled. The regular positive run uses a fresh private calendar
identity after the diagnostic cancellation; no production schedule changes.
Native-client acceptance, natural elapsed-time scheduling and live deployment
remain pending. Scoped snapshots do not claim to preserve unrelated world state.
