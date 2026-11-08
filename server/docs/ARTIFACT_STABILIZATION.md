# Dragon Soul stabilization transaction

Reviewed 2026-09-25. Installed Achievement.dat identifies 10502 as stabilizing a
Dragon Soul. Catalog SHA-256:
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.

The old handler counted repeated stone UIDs multiple times and projected a
permanent effect before separate effect-save and inventory-removal operations.
ArtifactStabilization now rejects duplicate selections, validates canonical player
and owned item state, locks owner/items/effect, and commits effect permanence,
one stack unit per selected stone, and achievement 10502 in one transaction.
Every effect and payment write must affect one expected row. Failure fences the
session; postcommit cache updates preserve the existing reconnect recovery rule.

Only a saved, unexpired, temporary catalog soul may be stabilized. Existing
permanent effects cannot spend more stones or earn new credit. Changed ownership,
locks, stacks, binding or effect preimages abort; wardrobe, rental, suspicious,
foreign and incompatible-bound materials are excluded. The target may be in the
bag or equipment. Prices retain the existing DataHolder rule: 10, 30, 60, 100,
150, 200 points for levels 1 through 6, and 200 for level 7. This is preservation
of Revora behavior, not a newly verified original-server economy claim. Extra
selected points remain spent as before. Packet IDs and layouts are unchanged.

Validation: isolated Debug/x86 build, 201 refinery/artifact checks and five audit
checks pass. New coverage includes concurrent valid requests, duplicate IDs,
insufficient payment, expired souls, bound-to-unbound denial, stale ownership,
suppressed effect/payment/achievement writes, exact stack debit, one achievement,
and production item reload of the permanent soul. Supplied equipment, temporary
effect and stones are fixture setup; this is not an earned full-character route.
The build includes unrelated owner edits and is not a clean release artifact.

Artifact application now has its atomic transaction and 10501 producer; see
ARTIFACT_APPLICATION.md for its tested scope and missing compatibility families.
Remaining:
validate earned soul/target/meteor/stone acquisition, full character and achievement
reload, additional stale-state rejection, and native application/stabilization UI.
No live deployment, database mutation or client patch occurred in this batch.

## Earned-route follow-up

ARTIFACT_EARNED_ROUTE.md now records the earned Terato soul, boss-funded equipment,
Meteors and stabilization stone through both handlers and full character/effect/
achievement/purse reload. All 320 Grotto checks pass. Earlier fixture limitations
above describe the initial batch; native/live acceptance remains pending.
