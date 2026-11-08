# Ancient Devil trial transaction store

Current source status (reviewed 2026-10-01): the historical staging and
"still required" notes below describe this page's original batch. They are
superseded for NPC admission, summon/claim dispatch, startup/tick integration
and collection achievement 11020 by [lifecycle evidence](ANCIENT_DEVIL_LIFECYCLE.md)
and [collection evidence](ANCIENT_DEVIL_COLLECTION.md). Do not reimplement those
connected components. Composed-process collection and saved-victory reward
recovery now pass in the linked lifecycle record. Mid-fight interruption,
genuine-client acceptance and deployment remain unclosed gates.

Source prerequisite, 2026-09-20. This is not a playable or deployed quest.
Follow-up: the [staged trial engine](ANCIENT_DEVIL_WORLD.md) now validates owned
combat and recovery against these records. NPC admission and startup/scheduling
integration are still absent.
`AncientDevilStore` is compiled and called by the staged engine, but the live
server lifecycle does not initialize or admit players to it. No new achievement
producer is enabled.

## Implemented transaction boundaries

- Admission at the correct installed guard on world 1052/base 1082 creates one
  active owned trial. Concurrent/repeated admission resumes that record; another
  guard cannot replace it. Class, level, location, canonical session, owner lock,
  transaction and saved character state are checked. No GM privilege is needed.
- A trial binds one life GUID to a private map identity and monster UID. Its
  selected guard fixes the expected family. Rebinding different identities is
  refused. Reused map/monster numbers in later trials have different life GUIDs.
- A matching internal defeat transaction marks that life defeated and creates a
  pending certificate entitlement together. Wrong owner, life, family, map or
  monster cannot credit that trial. Duplicate credit changes nothing.
- An unfinished trial can be abandoned once. Earned pending certificates cannot
  be abandoned through that operation. A new trial follows abandonment or a
  completed exchange; there is no invented daily certificate limit here.
- The issuing guard delivers one bound class certificate when inventory space
  exists. Full bags retain the entitlement. Saved and in-memory bag counts must
  agree. The certificate UID remains tied to its earning record.
- That exact owned certificate exchanges for one unbound same-class amulet. The
  personal-certificate-to-transferable-amulet transition is the explicit Revora
  cooperation policy described in [the research contract](ANCIENT_DEVIL_RESEARCH.md).
  Unrecorded, foreign, stored, locked, suspicious, temporary, stacked or stale
  certificates cannot use it. Item consumption, amulet insertion and final receipt
  share one transaction. A full bag can exchange because the consumed item frees
  its slot. Replay never mints a replacement amulet.

The caller commits before using `QuestInventory.Install` under the same owner
lock. A future runtime caller must fence/reload on post-commit delivery failure;
production item loading already recovers committed inventory. None of these
store methods sends a success dialog or awards achievement 11020.

## Validation

The isolated server build and the following fixture pass:

```powershell
& tests/PotencyHunt/Verify-PotencyHunt.ps1 -AncientStoreOnly
```

The fixture reports 41 checks, including its existing catalog baseline check. It
uses ordinary accounts and a disposable private schema, removed on completion.
It covers concurrent admission, claim and exchange; foreign and incorrect
encounter inputs; pending and exchanged inventory through production reload;
full bags; stale saved certificate state; missing locks/transactions; trade/class
rejection; and rollback when victory, certificate, amulet or final receipt writes
are suppressed. No live schema migration was performed.

The fixture deliberately supplies character state, guard placement and encounter
IDs, then calls the internal transaction boundary directly. It does **not** prove
NPC packets, admission travel, actual combat, trial difficulty, natural drops,
cross-player trading, ordinary daily pacing or native-client acceptance. The
store does not independently authenticate a dying entity: that must be proven by
the world caller against its canonical owned encounter before calling `Defeat`.

## Required integration before enabling

1. Exorcist admission and return travel, runtime guard offers and private map
   allocation with canonical owned monster registration/death callbacks.
2. Explicit trial objectives/difficulty and any unpublished numerical policy.
   The current life record is an encounter completion boundary, not a claim that
   every shared class-devil kill should drop a certificate.
3. Disconnect/death/restart cleanup and retry of unfinished encounters. A caller
   must abandon an unrecoverable hunting attempt before creating another; the
   store does not silently reuse a stale life after restart. Pending earned
   certificates and exchanges must survive that cleanup.
4. Legal amulet transfer validation, nine-class ownership/provenance checks and
   an atomic summon with daily boss eligibility, completion and rewards. Daily
   boss accounting remains absent and must not be inferred from trial admission.
5. Verified Ancient Devil boss profile, full ordinary-player earning route and
   genuine-6711 acceptance before release. Achievement 11020 stays incomplete
   until the collection boundary has a trusted producer.
