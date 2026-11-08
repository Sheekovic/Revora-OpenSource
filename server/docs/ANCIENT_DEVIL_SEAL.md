# Ancient Seal player route

Current source status (reviewed 2026-10-01): the historical staging and
"still required" notes below describe this page's original batch. They are
superseded for NPC admission, summon/claim dispatch, startup/tick integration
and collection achievement 11020 by [lifecycle evidence](ANCIENT_DEVIL_LIFECYCLE.md)
and [collection evidence](ANCIENT_DEVIL_COLLECTION.md). Do not reimplement those
connected components. Composed-process collection and saved-victory reward
recovery now pass in the linked lifecycle record. Mid-fight interruption,
genuine-client acceptance and deployment remain unclosed gates.

Staged source, 2026-09-20. Quest initialization now registers Ancient Seal 300017
at island 1052 (218,208), initializes the paid battle store/world and exposes
summon, resume and claim through the existing NPC dispatcher. Source startup now
calls this initializer; see the subsequent [lifecycle validation](ANCIENT_DEVIL_LIFECYCLE.md).
Achievement completion is implemented in the [collection follow-up](ANCIENT_DEVIL_COLLECTION.md).
This is not a deployment or a genuine-client acceptance result.

The seal reviews nine specific earned, owned and usable amulet UIDs representing
nine different classes. Dialogue lists the classes and states that the offering
will be consumed for one daily attempt with thirty minutes inside. If the player
has ten eligible classes, an option cycles which class to keep. For duplicate
items of one class the lowest eligible UID is selected. No CP or silver fee is
added. The seal uses the existing talker mesh 2177; its native visual appearance
still needs review.

Confirmations are attached to the character session, class and a sixty-second
review. The exact reviewed item UIDs must still be eligible. Moving away, trading,
using another NPC selector, reconnecting or allowing the review to expire cannot
authorize payment. A changed offering is presented for another confirmation;
it is never silently substituted at purchase. Payment and actual actor binding
remain in one transaction under the canonical owner's inventory lock.

An unexpired paid encounter offers only resume. A saved victory offers only
reward claim. A finished or expired attempt on the current quest day offers no
new payment. Full inventory retains the reward and presents a retry. Reconnect
requires a fresh menu, while preserving the paid encounter or saved victory.
Normal exit dialogue leaves a resumable encounter and the remaining guards are
restored on re-entry. No GM or account-name restriction is used.

## Validation

The isolated build and 176 combined checks pass with:

```powershell
& tests/PotencyHunt/Verify-PotencyHunt.ps1 -AncientSealOnly
```

The 258-check guarded boss-world regression also passes after the initialization
and dispatcher changes.

The fixture earns all ten class amulets through actual staged guard trials and
nine ordinary player trades. It uses existing NPC dispatch and sealed 2032 option
frames to review, pay, leave, resume and claim; guard/boss deaths use normal
server combat. It verifies the selected spare survives, changed/stale reviews
cannot charge, eight concurrent confirmations produce one payment, and eight
concurrent claims deliver only one saved reward. Production character reload
retains the result. Inventory filler is fixture-created only for the full-bag
negative case; no offering is fixture-granted.

This proves the server-side NPC route, not physical clicks or packet capture from
a genuine client. Controlled damage is not a balance test. Native NPC rendering,
boss/guard HP presentation, achievement 11020's exact collection boundary and
production lifecycle activation remain open.
