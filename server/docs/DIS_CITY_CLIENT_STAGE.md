# Dis City tracker and private content stage

2026-09-15, baseline `bd8f6e6`. The combat route is implemented; this checkpoint
corrects its tracker projection and prepares matching private client content.
It does not deploy a patch or certify native rendering. The later
[treasury checkpoint](DIS_CITY_TREASURY.md) supersedes this combat-only private
stage with usable key text and the expanded final reward.

## Current combined review candidate (2026-09-21)

The current candidate is
`obj/chi-refill-validation/dis-city-difficulty-overlay-v2`.
It composes the reviewed Trojan/Warrior/Monk/starter journal with the Magnolia
bound-P7 item source, reapplies the reviewed Summer and classic Labyrinth item
changes, then updates Dis City. The first difficulty overlay omitted the earlier
Summer/Labyrinth item changes and is superseded; do not install it.

The four Dis City records now explain Standard, Veteran and Nightmare, their
Revora HP/raw-attack multipliers, equal rewards, frozen selection across resumes,
the shared weekly victory limit and separate top-ten brackets by difficulty and
combat edition. The final prize includes the bound P7 soul for newly admitted
expeditions. The Mirror tooltip explains that difficulty remains fixed.
Older admitted expeditions retain their original reward contracts.

Prepare with the explicit `combined` profile:

```powershell
python tools/stage_dis_city_overlay.py obj/chi-refill-validation/magnolia-bound-p7-overlay/ini/itemtype.dat obj/chi-refill-validation/trojan-epic-journal/staged/ini/Questinfo.ini obj/chi-refill-validation/dis-city-difficulty-overlay-v2 --profile combined
```

The tool refuses an existing destination; the command records the original
preparation, not an instruction to overwrite the candidate. Both full input
hashes are required, and the destination remains separate from both sources.
The manifest records 36 reviewed item records and four Dis City missions.
Summer's existing item identity-field repairs are included alongside tooltips;
no other item fields change. The two Magnolia pack descriptions and all other
quest records, including the staged epic quests, retain their original bytes.
This is a review composition, not proof that every preserved quest is enabled
or that every older client artifact is ready for release.

| File | Input SHA-256 | Output SHA-256 |
| --- | --- | --- |
| `ini/itemtype.dat` | `ed2936c1f1a83aea39093a58f1b6f7f04fa277b665e982a13a4bf5f69bd3f190` | `799b083e2effd8c80d99f07f22bd70091d3ce29319a3770d1258b70987434164` |
| `ini/Questinfo.ini` | `c3d232473ff879c057fa3a5725ac013e84118f3fa68aa747201b5e7d7218f5d8` | `09dd2f2d01d5b775696f1d532eaeb0a2854aeb3adc024ec6a00e216e2b8aaf82` |

Validation: all 19 overlay tests pass (seven Dis City, including combined-input
preservation and difficulty/reward text). A separate audit against the actual
composed files confirms exactly the allowed 36 item rows/four mission records
change and all unrelated bytes survive. Item encryption round-trips. The installed
journal still matches `f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14`,
and DX9 still matches the approved `3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60`.
No server code changed in this content batch; the preceding server build and
202 Dis City checks cover difficulty admission, ranking and reward persistence.

The candidate is unsigned, uninstalled and unpublished. Genuine DX9 acceptance
still needs all three difficulty choices, tracker wording, private-map travel,
monster/boss presentation, victory rewards and the separate standings/claims.
Generated client files stay ignored. A future installation must validate the full
installed preimages, back them up and provide transactional rollback; this tool
does not install or authorize a release.

## Tracker correction

Installed missions 6472-6475 use counter 1 as an objective-ready checkbox. The
first combat implementation sent the raw progress there, incorrectly implying
readiness after one stone, kill, crystal or scrap. Counter 1 now becomes true
only for five collected issued stones, the frozen Hall quota, all nine sealed
crystals, or committed Pluto victory. Counters 2 and 3 retain progress and target.
Collecting deferred stones refreshes readiness immediately. A new weekly run
clears the later-stage tracker entries and sends the refreshed list; earned
achievements remain intact.

The existing task writer and catalog syntax are reused. This is source-backed
projection, not an observed native checkbox trace. Quest-begin packets remain
unable to grant achievements.

## Content and staging safeguards

`tools/stage_dis_city_overlay.py` prepares two files in a new separate directory:

- Four existing `Questinfo.ini` missions receive current rewards, accurate
  objectives, corrected Twin City admission coordinates and non-expired dates.
  Fixed finish links are removed using the catalog's existing blank-field form;
  the issued mirror returns the player to the actual private stage guide.
- Nine `itemtype.dat` descriptions explain issued quest items, usable refinery
  packs and paired jade consumption. Text explicitly identifies the unopened
  treasury and later Labyrinth jade route. The earlier Summer and classic
  Labyrinth modifications are retained.

Both input profiles are hash-gated. Every unrelated quest byte and item field is
preserved; duplicate/missing identities or fields are rejected. Output is written
to a temporary directory, verified, and renamed into a new private destination.
Existing destinations and paths overlapping either source tree are rejected.
Source hashes are rechecked before publication into that private destination.
The review manifest records file and changed-record hashes. Generated client
content is ignored and is not committed to Git.

Private stage: `obj/chi-refill-validation/dis-city-overlay-v2`.
The earlier local draft is superseded; v2 clarifies that refinery material binding
follows the consumed pack.

| File | Input SHA-256 | Output SHA-256 |
| --- | --- | --- |
| `ini/itemtype.dat` | `d2a2014fb2acea3af4e15388eb2bec8ee32299a667478006306c94dc8037422a` | `14c37d5033b00676a0a924fff3b91ab33dc0a2ad831205c8bf96feabc9cfa0b0` |
| `ini/Questinfo.ini` | `f783c2a8ead9461a469c79b6c7e7aae00b2604066d4bc6e2e2c56d7e98fc5e14` | `8b909ddb1b0083b3257255a36f3b145f5b6a1498a7963ceecce91e813b604937` |

Five new overlay checks and seven existing overlay checks pass. The expanded
Dis City fixture adds readiness checks through genuine stage progression and
new-week reset. All 92 Dis City checks and nine quest-packet regressions pass
after the final immediate-list refresh change. The isolated Debug/x86 build passes;
tested assembly SHA-256:
`6F034E33E52993E33A63F7E2EFD20D30158A3B0F947AF810706A0C1719CDF2B2`.
The preceding combat batch passed all 1,408 existing regressions. This follow-up
changes only Dis City tracker projection and private client-content tooling.

## Remaining gates

At this combat-only checkpoint, treasury keys, difficulty variants, Dark Horn/Star
Sword exchanges and next-day rank prizes still needed complete earning/use paths.
The treasury is now implemented in the linked successor checkpoint. Additions must
revise this private content coherently before release. Installed client files,
public downloads, production records and live binaries were not changed.
Genuine 6711 tracker rendering, item tips, private terrain, crystal appearance,
combat and boss-bar acceptance remain pending. The private stage is unsigned.
