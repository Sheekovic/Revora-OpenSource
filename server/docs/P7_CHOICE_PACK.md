# P7 Dragon Soul choice pack

A valid soul selection could leave the pack untouched: ordinary dialogue
fallback required a world NPC, while item3006233 creates an inventory menu.
The packet handler now routes the active P7 menu before world-NPC lookup,
validating the complete dialogue frame before deserialization. Sealed item-use
routing, startup registration and the reviewed staged consumer are included.

The bound pack must be owned, unlocked and in the current ordinary player's
bag. Its short-lived offer belongs to that session and does not survive reload.
Selection consumes the exact pack, creates one bound catalog P7 soul and saves
a unique pack/result receipt in one transaction. Cancellation, malformed frames,
invalid options and stale/repeated selections do not award another item.
No GM name or account allowlist applies; no new pack issuer is introduced here.

## Validation (2026-10-03)

The fresh career UID1096322's earned level120 pack61696829 selected soul800618
on menu page2/option2, preserving currency and all other items. Cancellation,
page navigation, lost offers on restart and cold reward ownership pass through
the decoded dialogue dispatcher. This first test exposed the need to separately
exercise network routing; decoded-dialogue success alone was insufficient.

A separate ordinary regression character4294919101 has a deliberately supplied
pack/profile and receives no fresh-career earning credit. The valid sealed2032
selection failed against the prior candidate: the pack remained owned. Against
the corrected dispatcher, sealed1009 opening, page navigation and selection
consume it once; trailing-frame bytes, input-length overrun, invalid options,
cancellation, stale/repeated requests and cold ownership all pass. Filtering
is enabled and requests are paced550ms apart. The regression setup's initial
profile-save failure was corrected without deleting its supplied item/profile.

The complete starter-overlay candidate passed these runtime checks. A separate
build of the minimal intended gameplay changes against committed sources also
passes; that build excludes the unrelated staged starter work. These are private
server checks, not native6711 rendering, socket authentication or deployment.
[Exact source/build/log provenance](research/FRESH_CHOICE_VALIDATION.json).
