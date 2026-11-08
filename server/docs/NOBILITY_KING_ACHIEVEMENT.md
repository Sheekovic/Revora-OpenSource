# Earned ranked King achievement

Achievement 11120 names the King title in the installed 6711 achievement catalog.
The official nobility guide identifies King/Queen as a donation-ranked title;
research/NOBILITY_KING_CHAMPION_EVIDENCE.md records the source and catalog hash.
For Revora, this is implemented as attaining one of the owner's five ranked
King slots through a successful paid donation. This is the documented Revora
earning interpretation, not a claim that original CO has five King slots.

NobilityDonations.Pay counts saved rivals with a higher donation, or the same
donation and a lower character UID. That uses the same ordering as the board.
The existing transaction advisory lock serializes competing payments and reset.
At the qualifying minimum with fewer than five rivals ahead, the transaction
records 11120 alongside the purse debit, donation, payment receipt and 10318.
A failed achievement insert rolls back all of those writes. Immediate refresh
uses the committed evidence; no cached rank or Rey override creates it.

The achievement records having earned King, so demotion does not revoke it.
Legacy high donations do not receive a retroactive award solely on login; they
must qualify through a successful payment. Champion 11123 is separate and remains
unimplemented pending its earning identity. This does not add a new wardrobe
title or change nobility battle-power values.

Validation: isolated Debug/x86 build; sixteen added purse-review assertions test
six ordinary equal-donation contenders, exact top-five results, one-silver-value
overtaking, concurrent duplicate requests, demotion and production reload,
and a suppressed King receipt that rolls back both achievements and payment.
The full purse review reports zero failures with its 407 prerequisite counter
(the sixteen review assertions are additional). The 329-check Grotto route also
verifies boss-earned CPs through the installed donor NPC and both 10318/11120
after full reload. Five inventory audits pass; producer inventory is 280/337,
with 57 missing identities, not a playable-completion claim.

Native UI, complete ranking-list pagination, bound-CP NPC earned route and live
deployment remain separate gates. No live process or database changed. The
isolated build includes unrelated owner edits.

The earned-route rerun initially encountered the live Homecoming advisory lock:
PostgreSQL advisory locks are database-wide, not scoped to the disposable schema.
The shared test class now selects a distinct Homecoming object ID (67116711)
before initialization, including child processes. Production retains object ID 1.
No production lock was released and no live process was stopped for validation.
