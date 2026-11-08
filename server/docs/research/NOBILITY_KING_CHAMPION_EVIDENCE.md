# King and Champion achievement evidence

Reviewed 2026-09-25. Research only; neither 11120 nor 11123 is implemented by
this checkpoint.

## Installed identities

The installed 6711 ini/Achievement.dat SHA-256 is
`df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc`.
Its decoded entries distinguish 10318 (nobility rank), 11120 (King title) and
11123 (Champion title). The latter resource identifier is
Achievement_WinTheVeryBestTitle. Neither entry specifies an event or ranking
position. The official [achievement guide](https://co.99.com/guide/guides/achievement.shtml)
also lists receiving Champion and King separately from weekly/monthly and
class PK victories; it does not establish a grant-to-ID mapping.

Installed ini/Title.ini SHA-256 is
`62de06f8145472cb53f15608f06268c4a062f8c6aa23599bf34dbbda137fd46e`.
It describes lower/higher Elite PK Champion entries 12 and 16 and separate
cross-server placement titles. Those similar names are not sufficient evidence
that either Elite PK grant should complete 11123. Weekly/Monthly PK guides also
use champion generically. Do not conflate those descriptions into a confirmed
earning rule.

## Rank limits differ

The official [Nobility System guide](https://co.99.com/content/2008-02-29/20080229003108707.shtml)
specifies King/Queen at positions 1–3, Prince at 4–15, and Duke at 16–50.
It lists lower thresholds of 200 million, 100 million and 30 million silver
for Earl, Baron and Knight, and a CP rate of 50,000 silver per CP.

Current Game/ConquerStructures/Nobility.cs SortCore uses zero-based cutoffs
4, 16 and 150: positions 1–4, 5–16 and 17–150. Its lower-rank thresholds and
CP conversion agree with the guide. The four-King arrangement is therefore
an existing Revora difference, not verified original behavior. The owner was
asked whether to restore official limits or retain the existing Revora limits.
No choice is inferred from silence. Rank limits are unchanged in this batch.

## Legacy paid flag is not earned rank evidence

SortCore overrides donation ranking when Rey is 1. The remaining production
AgregarRey/RemoverRey callers are the agregarrey/removerrey command branches in
Network/GamePackets/Chat.cs. Database/NobilityTable.cs persists that flag.
An administrator-set flag or loaded legacy row is not proof of an ordinary
paid donation earning King. No retrospective achievement should be minted solely
from Rey, NobilityRank or the unused NobilityInformation.KingTitle boolean.

## Next implementation requirements

1. Resolve official versus existing Revora ranking limits. Keep 10318's existing
   minimum-rank earning path independent of that choice.
2. Establish a deterministic tie rule shared by authoritative transaction ranking
   and board projection. Current SortCore orders by donation only.
3. Record a King earning receipt in the same serialized transaction as a qualifying
   paid donation, using saved competitor rows and the agreed rank rules. Do not
   infer eligibility from a stale cached board or the Rey override.
4. Verify boundary ranks, ties, concurrent donors, failed evidence writes,
   demotion/reset followed by reconnect, and ordinary earned funds.
5. For Champion, obtain a source that connects the specific achievement identity
   to an actual title grant, or an explicit documented Revora rule. Until then,
   retain 11123 as missing rather than attaching it to an arbitrary winner.

No gameplay, deployment, native acceptance or source-producer count changed.

## Subsequent owner decision

The owner selected five Kings, fifty Princes and unlimited Dukes, explicitly
making all qualifying donors below position 55 Dukes. NOBILITY_REVORA_RANKS.md
records the resulting implementation and validation. The pending-choice wording
above describes this research checkpoint, not the current decision state.
