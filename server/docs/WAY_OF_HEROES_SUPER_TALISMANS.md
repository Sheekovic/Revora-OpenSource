# Way of Heroes: Super Talismans

Implemented and privately validated 2026-10-02; not deployed live.

Stage 408 previously refused every claim while its ordinary acquisition path
remained unresolved. The installed stage requires two Super talismans and awards
one bound 3005123 pack. Quality is the item identity (HeavenFan 201009 or StarTower
202009); plus level is a separate property and is not a requirement.

The ordinary retained character already earned a plus-0 bound Super Fan and a
plus-0 bound Elite Tower from the attack/defense talisman quests. The existing
owned quality-upgrade service accepts the Tower and can upgrade 202008 to 202009
with a purchased DragonBall while preserving plus 0. This supplies the missing
plus-independent acquisition evidence without changing either quest's rewards.

## Claim behavior

The existing owner-locked, transactional Way of Heroes claim service now admits
stage 408 after its installed stage-opening gate. It counts saved owned item
instances with either Super identity in the inventory or the correct active
slot (10 for Fan, 11 for Tower). Stored, wardrobe, suspicious, rental and stacked
rows do not count. Two real qualifying item instances count; the rule does not
require different families. Locked ownership is not a disqualification because
no qualifying item is consumed. No plus field or client-reported completion bit
is used as eligibility. Item rows are locked with the claim transaction.

The existing claim transaction saves the bound plus-0 prize, stage blob and
unique owner/stage receipt together. Existing payment, replay, capacity and
session guards remain. Guild stage 303 and unrelated/main-stage prizes are
unchanged and remain outside this change. The pack's existing item-use service
awards 200 Study Points atomically; this batch does not rebalance that reward.

## Ordinary retained earning and restart

The new `career-talisman-claim-earn` and `career-talisman-claim-reload` phases pass
in the composed private game. They start from the exact retained travel
checkpoint for ordinary character 4294907001 and verify that both plus-0 input
UIDs belong to that character's actual quest reward arrays. No talisman is
inserted or changed by direct fixture SQL.

The run stores the older equipped +12 pair, then confirms the plus-0 Super Fan
and Elite Tower cannot claim stage 408. Stored +12 talismans do not satisfy it.
It buys a DragonBall through the ordinary CP shop and submits the normal quality
upgrade request. The result is a bound Super Tower at plus 0, with exact material
consumption and purchase debit. The success roll is controlled; this does not
claim a guaranteed upgrade or validate its statistical odds.

Eight concurrent claim packets produce one bound pack and one nonlegacy claim
receipt. Actual item use consumes the pack, grants exactly 200 Study Points and
rejects replay. The original equipped pair is restored. Both game and PostgreSQL
then stop and restart; the fresh process compares the exact scoped career,
Way of Heroes blob, claim rows and bag-use ledger, checks both plus-0 Super items
and the Study balance, and confirms claim replay changes nothing.

All ordinary warehouses were full at first. The initial storage attempt failed
before payment. The corrected fixture uses ordinary trades with existing private
peer 4294908901 to hold selected items, creates warehouse room, and returns the
held items through the same trade/storage paths afterward. No item was deleted.
A later fixture omission of `WayofHeroes.Load` was corrected before the successful
claim. Its failed attempt fenced the session without a reward; the next process
loads the character normally. Position-only changes from that stage-zero setup
are reconciled explicitly; item/currency differences are never waived.

The latest private checkpoint is `revora_retained_talisman_probe`, step 4, plus
its exact hold-identity table. Earlier retained travel snapshots remain historical
and are not rewritten to hide the legitimate Tower upgrade or Study reward.
Other private fixture identities and databases are not reset.

## Evidence and limits

The isolated committed-source-overlay build succeeded with existing legacy
warnings. The composed harness compiled with warnings as errors. Private game
SHA-256: `71618F36B1D5A9B804D3B6D42F17D61A397B97A15680BDE4375C26133DDAD96B`.
The previous private executable and manifest were retained by the normal
hash-gated refresh. Logs: `arena-process-career-talisman-claim-{earn,reload}.log`.
Both close paths completed and PostgreSQL stopped; no postmaster.pid remained.
The known private vote-pulse PostgresException also appears in the earn log;
this is not a vote-system fix or globally error-free startup claim.

NPC approach and the upgrade roll are controlled, and packets are dispatched
in process. The earlier quest earning evidence retains its own qualifications
and combat limitations. Native claim-page/progress presentation, authenticated
client use and live deployment remain pending. No new achievement total or
complete compatible career is inferred. Existing owner worktree edits were
preserved and excluded from the validation build as before.
