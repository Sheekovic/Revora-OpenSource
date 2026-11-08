# Ancient Devil class earning and cooperation

Current source status (reviewed 2026-10-01): the historical staging and
"still required" notes below describe this page's original batch. They are
superseded for NPC admission, summon/claim dispatch, startup/tick integration
and collection achievement 11020 by [lifecycle evidence](ANCIENT_DEVIL_LIFECYCLE.md)
and [collection evidence](ANCIENT_DEVIL_COLLECTION.md). Do not reimplement those
connected components. Composed-process collection and saved-victory reward
recovery now pass in the linked lifecycle record. Mid-fight interruption,
genuine-client acceptance and deployment remain unclosed gates.

Staged source, 2026-09-20. The Exorcist/guard coordinator now exists and is
connected to ordinary NPC dispatch. Production startup does not initialize it
or schedule its tick: activation waits for the unfinished summon/boss contract.
No live server, schema, client patch or public release was changed.

## Player route

- Exorcist 300010 stands at the published Bird Island position (715,584).
  Level-50 players can take free passage to Ancient Island, world 1052/base 1082.
- The five installed guards keep their database identities, positions and
  appearances: meshes 7690, 7710, 7720, 7730 and 7740 in guard order. Admission
  checks that metadata rather than silently replacing changed catalog entries.
- Each guard briefly offers the appropriate trial, pending certificate claim
  or certificate exchange. A classic profession must use its own guard; newer
  professions use the explicit [Revora class policy](ANCIENT_DEVIL_RESEARCH.md).
  A pending trial from another guard must be finished with its issuer.
- An Exorcist return guide, identity 300016, stands at (214,208) on the public
  island and in each owned trial. Public passage returns to Bird Island at
  (713,584); private passage safely abandons an unfinished hunt. Both new guides
  use server appearance 2177, a Revora presentation choice awaiting native review.
- The earned certificate is bound and cannot be offered in ordinary player
  trade. Exchanging that exact certificate creates the transferable class amulet.
  Brief dialogue explains collecting nine different kinds through other classes.
  No CP, RVP or silver fee is invented for passage, trials, claims or exchanges.

Menus authorize one action for sixty seconds and belong to the exact session,
NPC, map and profession. Confirmation rechecks range, eligibility and saved
ownership; stale, expired, mismatched or unoffered choices do nothing. Claim and
exchange confirmations retain the displayed run and certificate identity.
Repeated replies cannot claim or exchange twice. Reconnecting invalidates old
offers. Full bags keep pending rewards, and unexpected persistence failures use
the existing fence/reload path.

The coordinator reuses the existing NPC request/dialogue formats. New replies
receive the existing bounded-length/input-count checks before deserialization.
No new packet format or native-client interoperability claim is introduced.

## Installed terrain and validation

Both new actor positions and legal nearby approaches were checked against the
installed terrain. Bird Island 1015.map SHA-256:
`e47df70501200965040755a424873395172c03e7dad920d483414f85c0359ba7`.
Ancient Island's reviewed hash is recorded in the research/world documents.
The earning fixture walks between the actual five guard positions and the
return/collection point with production movement; it does not substitute direct
position assignment for island travel between these quest actors.

```powershell
& tests/PotencyHunt/Verify-PotencyHunt.ps1 -AncientQuestOnly
& tests/PotencyHunt/Verify-PotencyHunt.ps1 -AncientWorldOnly
```

The isolated build and 155 quest checks pass. Ten ordinary characters, one per
class, each use Exorcist passage, walk to a permitted guard, enter a private trial,
deal lethal damage through `Handle.ReceiveAttack`, claim their certificate and
exchange it through NPC dispatch. Menu choices use sealed 2032 frames. Initial
menu opening calls `Npcs.GetDialog`; this is not a captured native click test.

Nine ordinary atomic trades on the island transfer those earned amulets to the
first hunter. Production reload preserves all ten distinct types and exact item
UIDs; every item still joins its own durable trial receipt. The collector returns
through the Exorcist and reloads with the collection intact. Bound certificate
trade refusal, duplicate replies, incorrect class/NPC, low level, stale session,
expiry, distance and malformed dialog bounds are also covered. Invalid-dialog
tests keep the transport seal valid to exercise NPC validation specifically.
The world/recovery regression passes 42 checks with the private return guide.
The shared NPC-dispatch regression passes 101 house-visit checks.

Character levels/classes, initial currency and damage amounts are fixture
inputs. The fixture earns all certificates and amulets rather than seeding those
items. It proves server earning and atomic cooperation, not natural progression
to those classes, time-to-kill balance, native visuals or real elapsed pacing.

## Still required

Nine-distinct-amulet selection already has staged rules, but its atomic summon,
daily entitlement, verified Ancient Devil boss profile, owned fight, once-only
rewards and achievement 11020 are unfinished. The collection fixture intentionally
does not award that achievement. Complete those pieces, lifecycle ordering and
genuine-6711 acceptance before enabling the quest in the production startup.
