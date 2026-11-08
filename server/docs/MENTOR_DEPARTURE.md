# Mentor departure, fees and retained EXP

2026-09-15, M6.34, based on `11e32e1`. Departure and retained EXP recovery are
implemented and tested for ordinary players. Retained blessing/+1 claims, native
acceptance and live deployment remain unfinished.

Leaving or expulsion now moves the original relationship and every contribution
balance into a durable departure record before removing active membership. An
apprentice's 50,000-silver payment and the mentor's entitlement commit with that
transition. The previous path simply deleted the relationship and its rewards.
Achievement evidence remains permanent.

## Evidence and lifecycle

The [official FAQ, section 10i](https://co.99.com/guide/faqs.shtml), checked
2026-09-15, clarifies the [mentor guide](https://co.99.com/guide/guides/mentors.shtml):
the apprentice pays the mentor 50,000 silver and cannot apply for another mentor
for three days. Membership therefore ends immediately, with a durable admission
cooldown. A mentor does not pay this apprentice departure fee when expelling.
The same three-day child cooldown after expulsion is an explicit Revora
interpretation of the guide's general reset interval; more precise original
expulsion evidence remains open.

The existing 2065 actions 3/4 select leave/expel. Leaving always applies to the
authenticated actor; expulsion verifies the actor owns the saved relationship.
Both sides work while the peer is offline. Bounds and canonical notification
identities are documented in [protocol research](PROTOCOL_RESEARCH.md). No
unverified timer field or native nonce is introduced.

Character owners are locked in UID order, then `MentorExperience.Sync`, then
database entity rows in UID order. The saved relationship must be unique and
belong to the acting pair. Ambiguous rows remain intact for repair. The same
transaction copies the original row as JSON, preserves actual/lifetime EXP,
blessing and +1 contribution values, records the reason and cooldown, conditionally
debits silver, and removes exactly one active relationship. Failed or silently
skipped writes cannot consume the fee or lose membership/rewards.

Owned mentor caches are rebuilt before releasing contribution ownership, so a
new EXP/blessing operation cannot use a just-retired membership. Post-commit
online resolution and a final mentor load after login registration also cover
departure overlapping an offline peer's entry. Native removal notices and the
existing apprentice tombstone record refresh lists; shared BP is cleared when
the child no longer has a mentor. The broader BP formula remains separate work.

The fee is owed to the mentor, not destroyed. An online mentor receives it through
a separate owned silver/entitlement transaction after departure. Offline, full-
purse or failed deliveries remain pending. Login and mentor reward requests retry
up to 64 pending fees per transaction without duplicate payment. A failed payout
does not undo the completed departure or its payment obligation.

The saved cooldown is checked inside new admission after owner locks, including
at the exact UTC seventy-two-hour boundary. Replays after membership removal
cannot charge again. As with the inherited social protocol, an arbitrarily delayed
identical departure packet against a later re-established relationship cannot be
distinguished by an unverified nonce; current owned membership is authoritative.

## Retained EXP and remaining contribution work

Mentor EXP loading and the native 2067 claim transaction now include retained
contributions. A bounded batch of up to 64 departure records joins active balances
under the existing 30,300-unit visible/claim cap. Reserve debits, complete character
progression and required side effects commit together. Maximum-level mentors keep
unclaimable EXP. Lifetime amounts and the original source row are preserved.
Projection distinguishes retired rows from a child's new membership, preventing
an old claim from overwriting new contribution state or generating upstream EXP.

At the M6.34 checkpoint, retained blessing and +1 balances were preserved exactly but **not yet exposed as
completed claim services**. Their existing live claim handlers also need transaction
and unit corrections: blessing contributions are stored as hours while the legacy
claim feeds seconds to AddBless, and the +1 handler can debit the wrong point
quantity or lose credit on a full inventory. Restore these claims across both
active and retained balances next; do not advertise the complete mentor lifecycle
or deploy this batch as full reward recovery. Their earning-source audit and the
historical duplicate remediation remain open too.

## Validation

34 departure checks cover ordinary ownership, immediate role state, exact fee
transfer, offline/full-purse delivery, replay, cooldown boundaries, retained EXP
claims, new-mentor isolation, maximum-level preservation and immutable lifetime
evidence. Archive, fee-debit, membership-delete, fee-delivery and EXP-grant fault
injection demonstrates rollback/recovery, including a silently skipped deletion.

Related regressions pass: 55 admission, 452 EXP, 43 friend, 51 partner, 74
progression, 91 title, 89 auction and 19 trade checks, plus 16 source/overlay
audits. **924 checks total.** Final isolated `Revora.exe` SHA-256:
`7C110C76C5F81B5CC7D3538D940D598FE727BEC0D3E18EE9AADA8474DBAE395A`.
All writes used disposable fixture schemas. No live binary, public schema,
client content, public patch or database dump was changed or committed.

The broader roadmap remains open. Producer coverage stays 101/337. Before live
deployment, preserve database/binary rollback, complete the remaining contribution
claim paths and validate genuine 6711 departure, list/BP refresh, fee delivery,
cooldown and reward presentation with ordinary players.

M6.35 subsequently implements both active/retained claim paths and blessing
persistence; see [mentor reward claims](MENTOR_REWARD_CLAIMS.md). Native acceptance,
earning-source transactions and duplicate remediation remain open.
