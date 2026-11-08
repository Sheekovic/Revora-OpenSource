# Way of Heroes: Lottery milestone

Implemented and privately validated 2026-10-02; not deployed live.

Installed stage306 (task15, opening level61) asks the player to play Lottery once
and awards three bound EXP Balls723700. The claim now requires a completed saved
draw: its prize has been claimed, its initial payment receipt consumed three
Small Lottery Tickets, and its final saved prize attributes match the last draw
step. Merely entering the room or paying for a still-unclaimed draw does not qualify.
This uses the existing completed-prize boundary that records lottery achievement10118.
Actual Lottery entry retains its independent level70 requirement. The prize may
subsequently be used or traded without losing historical completion credit.

The normal owner-locked claim transaction inserts all three bound items, stage
state and one unique claim receipt atomically. Lottery prices, probabilities and
reward pools are unchanged; no GM or owner-name requirement is introduced.

## Private evidence

CheckCandidateLotteryClaim.cs and career-lottery-claim-earn/reload verify:

- [x] An ordinary non-GM character with supplied level110 and100000 CPs buys the
  Super Magnolia seed at the configured price, plants it, earns the controlled
  Lottery Ticket bonus and exchanges it for three Small Lottery Tickets.
- [x] The existing registrar admits the character; room entry alone refuses306.
- [x] Opening the normal box consumes the three earned tickets and saves a pending
  draw. A milestone request still refuses until the prize is claimed.
- [x] The normal sealed1314 prize-claim request saves the output; replay does not
  duplicate it. Eight concurrent306 requests create exactly three bound EXP Balls
  and one non-legacy milestone receipt.
- [x] All three earned balls are consumed normally, increase experience and leave
  exactly three uses in the durable daily allowance. Item-use replay is harmless.
- [x] A real private game/database restart preserves exact scoped inventory,
  payment/draw, milestone and experience/allowance records. Reclaiming306 changes none.

The first probe failed after ticket acquisition because its snapshot query used an
ambiguous step column. After qualification, the resumed fixture also needed to
return the saved character from the Lottery map to the town registrar before
re-entry. Those fixture corrections retained the paid seed and earned tickets;
no second seed purchase, data reset or fabricated draw was used. Subsequent earning
and cold-reload checks passed.

Game SHA256:
250BF7DE2C0C1A82E8D010A7EC86F3B53B52C58DFBCD127D7A9647F956398CEF.
The committed-source overlay excludes unrelated owner WIP. Magnolia's date/bonus
and Lottery selection are controlled fixture inputs; this does not measure their
natural odds. Native UI acceptance and live deployment remain pending.
