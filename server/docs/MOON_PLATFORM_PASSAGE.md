# Moon Platform paid admission

2026-10-01. Luna4178 in Twin City retains the configured LunaTeleportGold fare
(default300 silver), no new level requirement, and the existing1103 landing
near188,165. The legacy handler could save that destination while losing its
unflushed fare on interruption. The new handler commits the fare and a pending
trip together before teleporting. Failed payment/receipt work rolls back; an
interrupted paid entry resumes after reconnect without another charge.

The receipt saves its original fare. A later configured price change does not
reprice an already-paid trip. Destination persistence clears pending state.
A subsequent deliberate trip costs the current fare. The handler requires the
canonical living ordinary player near Luna on the real public Twin City map,
matching request identity, no trade/booth/poker activity, valid destination
metadata and an accessible landing. Currency uses the existing durable preimage
and owner/session locks. No GM status or character allowlist is required.

## Private validation

The legacy x86 build passes with existing repository warnings; unrelated owner
work is excluded by the committed-source overlay. Runtime SHA-256:
F020C8A2B264B6D420B62A5DFA5421387E11B8D5F6AC44BB8305FEEC1608049B.
Four fresh-process phases on the retained ordinary career cover:

- Receipt INSERT rejection after attempted debit: exact rollback and session fencing.
- Map-write rejection after committed payment: one pending trip, exactly the fare
  deducted, and unchanged prior career recovered after game/database restart.
- Twelve concurrent confirmations after increasing the configured fare by123:
  one resumed entry, no extra deduction, one trip; destination replay changes nothing.
- Ordinary purchased Town-scroll return and exact final cold persistence.

The recovery test restores the temporary configuration change. No funds or
items are injected. The extra return scroll costs200 silver. The latest durable
checkpoint is revora_career_luna_probe stage2, using CareerLunaSnapshot. Earlier
Moon route snapshots remain historical; no new achievement identities are added.
Native UI acceptance, every possible coordinate crash window and live deployment
remain separate. No live restart, public patch or release was performed.
