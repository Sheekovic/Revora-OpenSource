# Owner-selected Revora nobility ranks

On 2026-09-25 the owner selected five Kings, fifty Princes and unlimited Dukes,
then clarified that everyone below the top 55 should be a Duke. This supersedes
the pending official-versus-legacy decision in the King/Champion research note.
No additional 300-million Duke threshold was adopted.

King/Queen and Prince/Princess retain the 30,000,000 silver minimum
(600 CPs at the unchanged conversion rate): qualifying positions 1-5 receive
King/Queen and 6-55 Prince/Princess. Everyone else receives Duke/Duchess,
including zero-donation characters and characters without a leaderboard entry.
Duke requires no donation and survives login and weekly donation resets. Knight, Baron and Earl are no longer assigned
by this ranking system. Weekly reset and historical achievement 10318 remain.

Equal donations are ordered by ascending character UID, independent of database
or dictionary enumeration. This is a documented Revora tie rule, not a claim
about the original client's earning rules. A higher donation always comes first.
Legacy Rey overrides no longer mint additional Kings or waive the minimum.
Legacy records are not deleted; their flag simply does not control rank.

Next-rank donation calculations use the same threshold, rank counts and tie rule,
with zero required for Duke and the extra silver needed to overtake an earlier UID on a tie. Existing
2064 serialization is unchanged. Retired lower-rank entries contain zero; Duke,
Prince and King entries carry the required additional donation. Genuine 6711 UI
presentation remains unverified. The existing list packet still advertises five
pages; showing all 55 ranked leaders requires a separate verified client review.

Validation: isolated Debug/x86 build and full purse review with five additional
rank assertions covering 170 qualifying donors plus zero and subminimum donors, exact counts, the 5/6 and 55/56
boundaries, positions beyond the former Duke cap, equal donations, obsolete Rey
flags, next-rank amounts, and promotions with displaced boundary ranks. Existing
atomic payments, full reload and weekly-reset cases run in the same suite.
The runner's 407 prerequisite counter excludes these review assertions.

This changes source behavior only; no live process or database was changed.
NOBILITY_KING_ACHIEVEMENT.md records the subsequent authoritative paid King
earning receipt and ordinary earned-funds validation for achievement 11120.
Champion 11123 remains a separate unresolved earning identity.
