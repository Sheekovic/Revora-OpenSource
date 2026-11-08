# Way of Heroes: taking an apprentice

Implemented and privately validated 2026-10-02; not deployed live.

Installed stage705 (task41, second rebirth level15 opening) awards one bound
+2 Stone730002 for taking an apprentice. Qualification uses the mentor owner of
a durable accepted admission in revora_mentor_admissions. An invitation alone,
unsolicited acceptance or being the apprentice does not qualify. A later normal
departure preserves the historical achievement. Existing unrecorded legacy
relationships are not backfilled as consent evidence.

The existing claim transaction commits the item, stage and unique receipt
together. No GM or account allowlist is required.

## Private process evidence

CheckCandidateMentorClaim.cs provides career-mentor-claim-earn/reload with two
ordinary non-GM characters. Level140/130 and second rebirth are supplied fixture
prerequisites, not naturally earned progression.

- [x] Both characters refuse before admission; an unsolicited acceptance creates
  no relationship and an unanswered invitation cannot claim705.
- [x] Normal request/accept creates mutual membership and one saved admission;
  repeated acceptance does not change the scoped state.
- [x] The apprentice cannot claim the mentor milestone despite satisfying its
  level/rebirth opening gate.
- [x] Normal mentor expulsion removes current membership while retaining earned
  qualification and one departure receipt.
- [x] Eight concurrent mentor claims produce exactly one bound +2 Stone. Normal
  composition consumes it once, making supplied +0 boots +2 with zero remainder;
  packet replay cannot consume or apply it again.
- [x] Full private game/database restart preserves exact scoped admissions,
  departure, membership, inventory, composition and milestone snapshots. Both
  characters' repeated claim packets leave those snapshots unchanged.

Game SHA256:
E5D5617E9D51A16386E78149FDEDA2CD418F5EA5BD2A5A64DD7406F4D66192CA.
The committed-source overlay excludes unrelated owner WIP. Build completed with
existing warnings; the harness and both process phases passed. These checks do
not constitute native-client acceptance or live deployment.
