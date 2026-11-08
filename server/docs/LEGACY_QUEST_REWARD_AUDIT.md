# Legacy quest reward audit

2026-10-01. Bounded source audit of Npcs.cs; not an exhaustive completion census.
Restored feature handlers run before the legacy switch, so finding an old reward
branch alone does not establish an ordinary earning route.

| Legacy family | Observed authority and gap | Disposition |
| --- | --- | --- |
| New Year / Christmas cards | NPC899979 default dialogue announces closure, but options1/2/3 still expose the card exchange and100M-CP teleport. NPC899980 also advertises closure while option1 charges1M CP. The private catalog contains899979 on5001 at48,66;899980 is absent. | Fixed: both identities now pass through TwinCityPlaza.HandleClosed before every legacy option. No event reactivation or new reward is claimed. Existing cards/items remain. |
| B-O-Y-A-C-A | NPC900172 unconditionally sends closed dialogue and breaks before its old option switch. | Already closed; do not count its old lottery branches as a restored quest. |
| Seven Inner Power halls | Entry900005 is parked and closed by TwinCityPlaza. Secondary900013 and final900012 remain legacy handlers; final output separately changes progress, items, currency and an inner-potency command. | Ordinary descendant access, owned progression, atomic completion and valuable reward classification remain unverified. Closing the entry does not certify every descendant. Do not rewrite old TareaInner state as completion. |
| Slayer Cup | Entry900174 is closed by TwinCityPlaza. Secondary900182 exchanges daily materials using transient flags and TareaCopa, with separate item/progress writes. | Descendant catalog/access and final earning route need review. Intermediate parchments are not separate valuable quest completions. Existing TareaCopa is preserved. |
| Legacy Perfection trial | NPC3215 unconditionally explains that the trial is not running; no option switch follows in that case. | Closed desk, not an active completion family. Review other stage identities before restoring. |

## New Year closure validation

A source build passes with235 existing warning lines in
`obj/legacy-newyear-build.log`; it is not warning-free. The build uses the
existing committed-source overlay to exclude unrelated owner work in progress.
Only the stopped private candidate was refreshed, preserving its prior executable
and manifest. New executable SHA256:
17976314CE4445B573179FF4AC744F2581196BA7E242D2A6BE2A09C31AF713A6.

`career-legacy-closed` passes with an ordinary non-GM character. Both clerk IDs
consume options0/1/2/3/255 through HandleClosed; BeastTrial remains outside that
closed set. For the installed899979 actor, the fixture approaches on map5001 and
sends those options through Npcs.GetDialog. It never teleports into the retired
paid destination or changes any of the five event characters' exact items,
currencies or earned-result receipts. The fixture returns to town and saves.
The absent899980 identity has handler-level coverage, not installed-NPC coverage.
The test does not seed lottery cards, fund a100M-CP purchase, claim native UI
acceptance or certify the old lottery implementation.

Next: trace secondary Inner Power/Cup identities in the loaded catalog and their
ordinary admission paths before selecting a complete restoration/reward change.
Do not activate these retired custom systems merely because legacy code exists.
No live deployment, client release or historical reward migration occurred.

## Loaded descendant catalog and entry authority

The read-only private database check finds these actors in `sobnpcs`, not `npcs`.
Numeric coordinates and committed branch hashes are retained in
[LEGACY_STAGE_ACCESS.json](research/LEGACY_STAGE_ACCESS.json). All17 inspected
legacy branches match committed source despite unrelated owner work in Npcs.cs.

- Inner entry900005 is parked on6000 and blocked before the legacy switch.
  Stages900006-900012 and entry/exit900013 remain on10332. Alternate entry900175
  has code but no actor in either checked NPC table. No admission to10332 was
  demonstrated by this audit. Stage presence is not proof of a complete route.
- Cup entry900174 and first-city stage900176 are parked/closed on6000. The other
  city-stage actors900177-900180 remain on1011/1020/1000/1015;900182 remains on1819.
  A player already holding legacy progress may therefore reach descendant actors.
  No blanket closure or migration was added to discard that player's progress.
- Inner final900012's success path adds CPs, increments TareaInner, inserts three
  books and invokes the inner-potency command separately. Its paid option checks
  the balance, ignores the spend result and then grants outputs. This is not an
  atomic, validated completion contract; it must not be counted as restored.
- Cup900182 increments TareaCopa before adding parchment722791. Its preceding
  exchange separately inserts711331, updates progression and removes materials;
  daily guards use session flags. TareaCopa/TareaInner do persist through entity
  setters/loading, but those writes do not make the combined exchange atomic.
- Cup state17/18 also feeds the legacy second-rebirth branch near Npcs.cs:701.
  That overlap is already documented in ABYSS_TRIAL_RESTORATION.md. It is not a
  reason to manufacture or overwrite legacy Cup history for the restored rite.

These findings resolve the earlier question of whether descendant NPCs exist,
not whether their entire quest is playable. Restoration would require explicit
owned contracts, material provenance, atomic fees/rewards and final reward rules.
Keep this retired-system debt separate from the approved active quest roadmap;
do not silently activate it or report completion from branch existence. Continue
active quest reward coverage and ordinary earning work independently.
