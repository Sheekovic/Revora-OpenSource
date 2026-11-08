# Training Ground paid admission

2026-10-01. The existing level20 requirement,1000-silver fare and trainer NPCs
181-185 are preserved. Admission now records a pending trip in the same owner-
locked PostgreSQL transaction as the silver debit. A failed receipt insert rolls
back payment. Map persistence clears the pending flag; if teleporting fails,
the session is fenced for reload and any eligible city trainer can resume that
paid trip without another debit. Leaving and buying a new trip costs the fare.

The handler requires a canonical living ordinary player, trainer proximity,
a supported city map, matching NPC request, no trade/booth/poker activity and a
valid public1039 landing before payment. It uses the existing silver preimage
checks and session lock. Arrival and receipt are durable server facts; no new
protocol format or client changes are introduced.

## Verification

The private x86 legacy server build passes with existing repository warnings.
The committed-source overlay excludes unrelated owner work. Executable SHA-256:
D7929D42DE16B21CF34C25B815AEDCC238168D0C82A1E00AD3FD65FE81EEF2DF.

Four independent full startup/restart phases on the retained ordinary career:

- career-training-rollback: rejecting the receipt INSERT after debit leaves the
  exact purse, inventory and prior career unchanged and fences the session.
- career-training-interrupt: rejecting the destination map write leaves one
  pending trip and exactly1000 silver charged. Cold reload retains both.
- career-training-recover:12 concurrent ordinary NPC confirmations resume the
  same trip without a second charge; another request at1039 changes nothing.
  An ordinary200-silver Town scroll purchased before the fault returns home.
- career-training-reload: exact trip, money, inventory and career survive restart.

Current checkpoint: revora_career_training_probe stage2, using the wrapped
CareerTrainingSnapshot. Earlier travel snapshots remain historical. No new
achievement identity is added: the prior10807 survives. This test does not
prove native UI, real-time packet pacing, all five city approaches or every
possible teleport-coordinate crash window. No live deployment or restart.
