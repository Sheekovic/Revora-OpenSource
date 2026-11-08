# Modern Dragon Soul compatibility

Reviewed 2026-09-25 against the installed server catalog
`bin/Debug/database/itemtype-new.txt`, SHA-256
`1ed7a23b7a34b5e3706277f97e421e3cdf0b269eeb5be7367a4ebc0e4f22b3ef`.
The earlier compatibility text file lacks these 25 identities. Only numeric
rules and summarized evidence are recorded here; no proprietary catalog is added.

| Soul identities | Permitted target families | Evidence |
| --- | --- | --- |
| 801000?801004 | 617 Nunchaku | Installed descriptions explicitly name Nunchaku |
| 801100?801104 | 421 Backsword, 619 Hossu, 620 epic Backsword | Descriptions name Taoist weapons; mapping that category to these three Revora families is an explicit Revora interpretation |
| 801200?801218, even IDs | 624 Fist | Installed descriptions explicitly name Fist |
| 801300?801308, even IDs | 626 Handfan | Installed descriptions explicitly name Windwalker Fan |

Target catalog level must meet the soul requirement (120 or 130), and the soul
must have a valid installed phase. All existing transactional payment, binding,
capacity, ownership and replay protections still apply. These mappings do not
change prices, durations or rewards. Other families continue using reviewed
legacy mappings. Non-soul packs with nonzero purification-level catalog fields
remain rejected. This implementation does not infer compatibility for every
future ID or accept an arbitrary unknown soul on arbitrary gear.

The ordinary handler suite covers all 25 identities across 35 allowed pairs,
low-level rejection, high-level incompatible-family rejection, exact soul/Meteor
consumption, achievement 10501, and production item reload of the saved identity
and phase. Eight non-soul pack IDs are explicitly rejected. Isolated Debug/x86
build and all 553 refinery/artifact checks pass. The test setup supplies gear,
souls, Meteors and character qualification. Unrelated owner edits are present in
the isolated build. No live deployment or client patch occurred.

The original game's precise Lotus/Hossu/epic split is not independently verified;
native UI and visual acceptance also remain pending. The server-side mapping is
documented Revora behavior. Full earned target/soul/payment/stone acquisition and
full character/achievement reload remain separate milestones.

## Earned-route follow-up

ARTIFACT_EARNED_ROUTE.md now records the earned Terato soul, boss-funded equipment,
Meteors and stabilization stone through both handlers and full character/effect/
achievement/purse reload. All 320 Grotto checks pass. Earlier fixture limitations
above describe the initial batch; native/live acceptance remains pending.
