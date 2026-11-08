# Local Revora tournament wardrobe editions

The owner approved local Revora earning rules on 2026-09-30 for the ten wardrobe
rewards whose original requirements named cross-server events. This implementation
uses the existing local competition calendars and results. It does not implement
inter-realm battles or present local entrants as separate origin servers.

## Implemented earning rules

| Competition | Eligibility and result | Reward | Duration |
| --- | --- | --- | --- |
| Saturday CTF | Saved leader of the winning guild at finalization, personally recorded as a contributor; contested result with a unique winner | Conqueror 20030004 and Radiant Wings 60020021 | Permanent |
| Friday Elite PK, level 130+ | Verified combat placements, with an earned champion | Legendary 20352035 for first; Peerless 20362036 for second; Outstanding 20372037 for third; Expert 20382038 for fourth through eighth | 14 days from finalization |
| Saturday regular Team PK, level 130+ | Top-eight team placement; member in the frozen roster and personally present in at least one contested match | King Team 20592059 for first; Dominator Team 20602060 for second; Power Team 20612061 for third; Elite Team 20622062 for fourth through eighth | 14 days from finalization |

CTF grants belong to the saved winning character; later guild succession does
not transfer the permanent honors. Victor retains its existing current-leader
rule. Contributors retain their bound P7 prizes. Elite PK retains its existing
prizes and seven-day champion wings. Only the highest level bracket gets these
four additional titles. A buy, pack opening, old ranking or administrative role
cannot substitute for the tournament result.

All three run tables store `local_wardrobe`, default false for previous runs. New
runs explicitly enable the edition. Existing and historical runs retain their
previous terms. Finalization writes awards in the same transaction as the result;
a missing/suppressed grant aborts that transaction. Repeated finalization returns
the saved result without granting again. Progression recovery uses the existing
wardrobe award ledger and preserves the original expiry.

Sources `ctf-local-wardrobe-v1`, `elite-pk-local-wardrobe-v1` and
`team-pk-local-wardrobe-v1` are separate from
the origin-qualified producers. Existing origin grants retain their authentication
and realm filters. Local grants do not require an artificial origin session.

## Private evidence and limits

- [x] CTF: four ordinary characters in two supplied guild affiliations, actual
  base attack handling, movement flag pickup and bounded-jump delivery. Only the
  winning contributing leader equips both new rewards. Winning member, losing
  leader and idle member cannot equip them.
- [x] CTF: suppressing Radiant Wings insertion rolls back the entire result,
  first title and prize entitlements. Retry succeeds. Permanent rewards and
  equipped title/wing survive game/database restart; replay grants nothing extra.
- [x] Elite PK: four ordinary level-140 characters enter through the existing
  Envoy, compete through the production arena and damage/death handlers, and earn
  all four placement keys. Suppressed wardrobe insertion rolls back placements;
  retry produces one award per fighter, with exact fourteen-day expiry.
- [x] Elite PK: equipped-title and exact award recovery across game/database restart.
- [x] Implement the four regular Team PK local edition rewards with saved rosters
  and actual combat participation; authenticated combat, rollback, stale-cache
  refresh and restart passed privately.
- [ ] Native DX9, authenticated UI, natural calendar observation and deployment.

Reproduce CTF with `tools/Verify-ArenaProcess.ps1 -LocalCtfOnly`; use
`-LocalWardrobeOnly` for Elite PK. CTF tested game SHA-256:
`3EB8DE4697A1DEA08F1A4059BCA3AF312CC16D9F8367366B7A7C4A7479F99292`.
Elite PK tested game SHA-256:
`7BA5EA41DF3C990B39E206213AE6C02F77A49DDE32D5C4763B579A7D5514DA59`.

The fixtures use controlled clocks and supplied starting levels/guild affiliation.
Elite combat supplies bounded damage to production handlers; it is not a full
native-client attack test. Four-player placement coverage does not claim a new
32-player acceptance run. Migration defaults were reviewed in source; an earned
pre-upgrade season was not replayed in this batch. No live deployment or restart.

## Regular Team PK edition (2026-09-30)

The existing regular Team PK rules apply: at least eight teams must remain at
registration close; teams contain one to five players from the same level group.
This edition preserves that local roster size rather than inventing foreign
origins or silently requiring the original cross-server five-person roster.
Only regular Team PK's level-130+ bracket enables the new edition. Skill Team PK
and the lower brackets retain their existing rewards.

The roster is frozen at registration close after every participant has entered
through the registrar. Its primary key permits one team per character per run.
Retry checks the exact membership and captain assignment; it cannot extend an
already frozen roster. Completed contested matches save winner, loser and the
members present on their owned battlefield. A match needs recorded damage to
qualify; a bye, no-show or name in an old ranking is insufficient. The actual
placed team leader determines the title tier, while qualifying members each
receive their own grant. Disconnecting before the final does not erase a saved
participation receipt. Later replacement members do not inherit it.

Title grants share the final result transaction. Suppressed insertion must roll
back that result, while already saved rosters and battle receipts survive retry.
Returning players reconcile wardrobe eligibility through the existing return
path; offline players recover from the normal award ledger. Original dates
remain fixed across reconnect, restart and repeated finalization.

- [x] Implement frozen rosters, contested-member receipts and transactional awards.
- [x] Verify all four tiers with eight ordinary teams through authenticated combat,
  injected grant failure, final retry and equipped-title recovery after restart.
- [ ] Native team UI, natural calendar and live deployment.

Reproduction: `tools/Verify-ArenaProcess.ps1 -TeamWardrobeOnly`.

Initial ordinary acceptance passed on game SHA-256
`5B5B6432AFC3B943758C49BA884D886145BF22E636E43FE02D7AE10A5F9E2085`.
Sixteen authenticated non-GM characters formed eight two-person teams through
real invitation and admission packets. Real melee packets produced recorded
combat scores; controlled timers settled the full bracket. All sixteen members
received the tier matching their saved leader's placement, and all four keys
were represented. Suppressing the first wardrobe insert left the run active,
with no awards; retry finalized exactly once. Each equipped title and exact
award dates survived a game/database restart. The champion's existing silver
and bound P7 reward remained single-claim.

The new return path invalidates cached award evidence before reconciling titles.
Passing follow-up validation primes an award-free cache after the injected rollback,
holds that cache valid for five minutes and requires title availability before
an explicit wardrobe login. This is a controlled cache test, not five minutes
of real elapsed time. Natural schedule timing and native rendering remain open.

The final combat-receipt retry path treats an already committed `(run, match,
round)` receipt as authoritative after verifying its winner and loser. A later
disconnect cannot shrink its original participants, and a later arrival cannot
expand them. An actual lost-acknowledgement/disconnect fault was not injected in
this batch; this branch was reviewed in source. No cross-server battle or legacy
season migration acceptance is claimed.

Final candidate SHA-256:
`06B1CC84D242C6C556867BD6A721285FF744EE299C691E346735DE11EC9827DC`.
The complete authenticated combat fixture passed on this build, including the
forced stale cache and title availability before explicitly reopening wardrobe.
The subsequent game/database restart phase also passed with exact equipped titles
and unchanged award timestamps for all sixteen characters.
