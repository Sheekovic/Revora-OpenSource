# Way of Heroes guild milestone

Stage 303 (create/join a guild, opening level 61) now awards one bound Study
Points bag 3005123. Its existing ordinary consumer grants 200 Study Points.
Implemented and privately validated on 2026-10-02; not deployed live.

Guild founding and accepted recruitment insert the first
`revora_guild_earning` record inside their existing membership transaction.
The record contains owner, guild, founder/member status and UTC joining time.
Invitation/application alone cannot qualify. Membership rollback cannot leave
a receipt behind. The receipt survives leaving or guild dissolution, and the
normal unique claim record prevents collecting again after another admission.

Existing members do not have to leave and rejoin: a saved membership can also
qualify when it references an existing guild with a matching saved leader.
Runtime GuildID alone is not evidence. Unknown historical membership after
leaving before this change is not reconstructed. No migration invents past
admissions. All ordinary eligible players use the same path; no owner/GM gate.

## Validation

`tools/CheckCandidateGuildClaim.cs` supplies three ordinary level-130,
twice-reborn characters with starting CPs. It does not insert guilds,
memberships, earned receipts, claims or rewards through fixture SQL.

- [x] Guildless claims fail without changing scoped state.
- [x] Founder pays through the Guild Director's normal dialog and naming packet.
- [x] One recruit accepts an invitation; another applies and receives acceptance.
  Both pending states refuse the reward before mutual consent.
- [x] Both recruits leave through the normal guild request; saved earning remains.
- [x] Eight concurrent claims per character yield one bound bag each. Ordinary
  use plus replay consumes once and adds exactly 200 Study Points.
- [x] Game and PostgreSQL restart; fresh processes compare exact scoped currency,
  inventory, membership, study, earning, claim and bag-use records. Claims replay
  without another reward.
- [x] Read-only eligibility check against the retained CTF founder, whose guild
  predates these receipts, confirms the existing-member fallback. The guildless
  pill fixture refuses. Neither older checkpoint nor reward state is changed.

The committed-source-overlay production build passed with existing legacy
warnings; the composed harness compiled with warnings as errors. An ambiguous
KnownPersons test reference was qualified before gameplay. The initial earning
and cold-reload phases passed on SHA-256
`4FBA8968756320B64C15D0B71B9355764077EF8DBA2535FD14889584319A54CF`.
After adding existing-member eligibility, the retained snapshot/replay phases
and both fallback checks passed on final SHA-256
`26DA26CA5F8F0765189EACAAA88D1C69E6394D742907F16E745381AE02930257`.
No fixture database was reset. Private logs are
`arena-process-career-guild-claim-{earn,reload}.log`.

Native/authenticated acceptance, natural starting-fund acquisition, explicit
interrupted membership-commit checks and live release remain open. The existing
member fallback was checked without claiming that older character's reward;
the actual reward path was exercised by the three new ordinary characters.
No live server, client patch or owner work-in-progress file was deployed.
