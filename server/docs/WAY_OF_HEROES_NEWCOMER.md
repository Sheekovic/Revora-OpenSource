# Way of Heroes beginner introduction

## Rule and evidence

The installed6711 `stagegoal.txt` defines104 as completing the beginner tutorial,
TaskType3, Progress1, OpenLev1, with one bound EXP Potion723017. Revora uses the
owner-approved Twin City introduction3478-3481 as its documented interpretation:
meet KungfuMaster, open Twin City storage, buy one pharmacist supply, and accept
the captain's rune. This is not a claim that the original tutorial was identical.

Eligibility reads `revora_newcomer_intro` for this character's stage4 and non-null
reward UID. The final step and rune insertion already commit atomically. Signup,
partial steps and client flags cannot qualify. Lifetime progress remains valid
after rebirth; the rune need not remain in inventory. Existing ownership locks,
claim ledger, saved Way of Heroes state and inventory checks govern the reward.
No new packet layout, starter pack or spawn-map change is introduced.

## Private acceptance (2026-10-03)

Build SHA256: `BAE4BF90ECD127C29CE2A403AB97751BC5DA558BAB840B6C21A5CAE126DDB8A6`.
`career-newcomer-claim-earn` and `career-newcomer-claim-reload` passed in separate
private game/database processes. Retained ordinary non-GM character4294917801:

- Supplied level20, zero rebirth and100,000 silver are fixture prerequisites.
- Real NPC, warehouse and shop handlers earn all four steps. Out-of-order captain
  use and claim attempts at stages0-3 preserve rewards/claims/balances.
- Eight simultaneous104 requests award exactly one bound723017.
- Actual item use consumes that potion and saves exactly3,600 double-experience
  seconds; replay cannot grant another use.
- Cold load preserves exact saved profile, items, claims, Way of Heroes state and
  potion-use receipts; captain/claim replay makes no reward changes.

The retained fixture never deletes earning receipts to rerun. Tests use offline
player adapters and supplied starting prerequisites, not native-client control.
Native acceptance and live deployment remain pending; no live restart or patch.


## Fresh career continuation (2026-10-03)

The existing earned level124 character now completes the actual introduction,
including paid pharmacist supply and the captain's bound rune, without supplied
profile or funds. Early104 claims refuse. Unclaimed restart, once-only bound
potion claim, active-bonus refusal, normal countdown-handler expiry, potion use
and final cold replay preserve all prior career state. One fixture-helper failure
after payment is inspected and hash-pinned before reusing the already-issued
potion; no second claim is fabricated. Native/client/live gates stay pending.
[Exact recovery evidence and timing limits](CAREER_STARTING_BOUNDARY.md#fresh-twin-city-introduction-and104-reward-2026-10-03).
