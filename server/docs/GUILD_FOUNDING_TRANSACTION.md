# Guild founding transaction

The Guild Director (10003, Twin City) previously debited CPs/RVPs, inserted the
guild, saved membership and rank, and advanced the ID counter in separate writes.
Its compensating refund could not make those steps atomic after a timeout or
process interruption. Guild-event prerequisites must not rely on that path.

## Implemented behavior

`GuildFounding.Create` consumes the existing registrar name submission and chosen
payment. The configured level and fees are unchanged: level 90, 25,000 CPs or
5,000 RVPs in the current defaults. The initial guild fund remains 500,000 silver.
Ordinary players use this route; no GM, owner-name or account allowlist is used.

A canonical session lease, owner lock and founding gate protect the request.
The transaction locks the character and ID counter, serializes name allocation,
checks case-insensitive name uniqueness, and saves the guild, leader membership,
fee and counter together. A saved purse mismatch or suppressed write rolls back.
The counter advances beyond saved/live IDs and rejects exhaustion of the 16-bit
wire ID range. Names require 4?15 characters, at most 15 losslessly encoded bytes,
and no control characters. Already affiliated union members must leave their
union before founding an independent guild; this route does not move union roles.

After commit, the production guild loader recovers the actual saved member fields,
then publishes the registry and immediate guild/currency packets. Projection or
uncertain-commit errors fence the session for reload. There is no compensating
refund that could duplicate a committed payment. Repeated submissions see saved
membership and cannot create or charge again. No achievement is invented for
founding; this is the prerequisite for subsequent ordinary guild earning.

## Private process evidence (2026-10-01)

- [x] `career-guild-founder-fault`: retained non-GM character 4294907001 submits
  the ordinary CP option and name packet. A trigger suppresses the later CP debit
  after guild/member insertion. Guild, membership, purses, saved items and counter
  remain unchanged; the failed guild does not exist.
- [x] `career-guild-founder-earn`: a fresh process creates CareerBanner through
  the registrar, takes exactly one configured CP fee, preserves silver/RVPs and
  retains the existing clan. Repeated submission does not charge again.
- [x] `career-guild-founder-reload`: production startup restores the guild and
  founder/leader membership. Guild row, saved items, currencies and counter match
  exactly; another submission and save preserve that checkpoint.

These CP phases passed on game SHA-256
`009A273CB6D477AFB7F5C5057629C6AA03741B7AFB76E4B59791073DEB183C8B`.
The subsequent independent-union guard build is
`07971A3643E79E748976CEA84BC8067E0318D5F6EF2689B0CAD173787105EED2`.

- [x] `career-guild-rvp-fault`, `career-guild-rvp-earn` and
  `career-guild-rvp-reload` pass on the latter build. Separate ordinary profile
  4294908801 starts with the configured RVP fee plus17,30,000 CPs and123,456 silver
  as explicit test prerequisites. A suppressed RVP debit rolls back everything;
  fresh-process retry founds RvpBanner and leaves17 RVPs without changing CPs or
  silver. Cold startup and duplicate submission preserve the exact saved result.

The later RVP founding legitimately advances the shared ID counter, so the earlier
CP snapshot's counter is historical; its guild, membership and paid balance are
not overwritten. Both suites use the same transaction implementation.

The fixture controls NPC proximity and uses the existing sealed name-input packet
handler without a real socket. Native UI, natural disconnect races, competing
founders and a crash specifically between commit and projection remain unverified.
The retained career is not granted fixture funds. A separate RVP test profile has
explicitly supplied starting funds and contributes no retained-career credit.
No live deployment, restart or client publication is included.


## Fresh career founding and cold membership (2026-10-03)

The same fresh career funds FreshBanner through ordinary Guild Director10003
and sealed2032 name input, paying exactly25,000 earned CPs. Guild51102 has one
member, rank1000 on the founder, and its configured500,000-silver starting fund.
RVPs and personal silver are unchanged by guild founding. The prior FreshKeep
clan remains joined. Cold startup restores the guild and member objects; repeated
name submissions create nothing and charge nothing. Final personal balances are
328,905 CPs and8,497,612 silver. No guild, fund or membership is fixture-inserted.
This is an event prerequisite, not an invented guild-founding achievement.

`tools/CheckCandidateFreshAffiliations.cs` passes
`career-fresh-affiliations-clan`, `career-fresh-affiliations-guild` and
`career-fresh-affiliations-reload` on their first run. Latest checkpoint is
`revora_fresh_affiliations_probe`, stage `guild`, nesting all prior Ancient,
contributor, quest and reward snapshots plus exact affiliation rows/counter.
Candidate SHA-256 remains
`121E1EF7F880B5CB082DE44D8422600308C020D4E00F5D3C7A49B24AECA808F7`.
NPC proximity is controlled; this is ordinary handler earning, not native UI
acceptance, a new concurrency/fault suite or live deployment. The preceding
post-Ancient receipt audit is historical after these legitimate mutations.

## Ordinary invitation ownership (2026-10-04)

A guildless ordinary player sending InviteRequest previously caused a null
membership dereference. The invitation branch also trusted a cached leader rank
without the canonical guild/member checks already used by leadership services.
It now calls `GuildLeadership.Leader` before inspecting or offering an invitation.

The baseline private build reproduces the exception in `GuildCommand.ProcessCurrent`.
Nine focused checks pass after the one-line correction: guildless/orphaned records,
stale leader/guild/member/rank refusal, and normal leader blacklist/offer behavior.
An ordinary valid leader still offers an invitation; consent remains required
before the target becomes a member. No enrollment, payment or reward rule changes.

Run `tests/GuildInvitations/Verify-GuildInvitations.ps1` against the private artifact
folder. This test uses an offline packet adapter and no database or game listener.
It does not replace saved admission/cold-recovery evidence or native acceptance.
No career runtime or live deployment. [Exact evidence](research/GUILD_INVITATION_OWNERSHIP.json).
