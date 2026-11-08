# Armor dyeing and retired Black Tulip

2026-09-15; implementation baseline `e19d3a6`. Source implementation and isolated
ordinary-player fixtures are complete. Native 6711 appearance acceptance and
production rollout remain pending.

## Evidence and edition decision

The official [dye guide](https://co.99.com/guide/guides/haird.shtml) and
[customization guide](https://co.99.com/guide/guides/customize.shtml) describe a
one-Meteor admission through Shopboy, then repeated free armor color changes
by Tinter during that visit. Their published coordinates vary by edition.
Read-only inspection of the installed server database confirms Shopboy 10063
at map 1002, (429,312), and Tinter 10064 at map 1008, (27,16), with mapdoc 1008.
The existing entry at (22,26) is walkable in the installed server floor.
These NPCs keep their existing identities, meshes and positions.

The installed `ini/itemtype.dat` describes item 723584, Black Tulip, as a
10-Chi redemption. The historical [2008 mall listing](https://co.99.com/content/2008-10-21/20081021013424194.shtml)
used it for black armor; that obsolete effect does not override the installed
6711 description. The source's separate one-DragonBall black armor service is
preserved as existing Revora policy. Its price is not presented as independently
verified official behavior.

Read-only local evidence, approved client root `Desktop/Revora/6711`:

| Artifact | SHA-256 |
| --- | --- |
| Genuine DX9 Conquer.exe | 3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60 |
| ini/itemtype.dat | d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a |
| ini/Achievement.dat | df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc |

The installed achievement 10115 describes changing clothing color. A committed
body-armor color change earns it; a helmet-only change, purchase, admission,
same-color request, or retired item redemption does not. Restricting this to
body armor is the documented interpretation of the installed wording, not a
claim of a dynamically traced native completion trigger. No new packet layout,
client execution, proprietary source or client artifact is part of this change.

## Completed behavior

Shopboy admits any eligible ordinary player for one owned Meteor unit. Bound
materials are accepted. The inventory debit and durable visit record commit
together before teleporting. A failed teleport preserves a pending admission;
returning to Shopboy resumes it without another fee. Admission is rejected before
payment if the destination floor or Tinter is unavailable.

A visit becomes entered when the saved map changes to 1008. Leaving 1008 closes
it in the same database statement through `revora_dye_map_changed`. Reconnecting
inside the shop retains the visit. Returning after leaving requires another
Meteor. There is no invented time limit or migration granting free visits to
characters already inside. The schema-local trigger captures its creation search
path and runs with caller privileges, not as a security definer. Initialization
creates the tables and replaces the trigger transactionally.

Tinter offers the existing seven colors, now named in the dialogue. A selection
captures the actual equipped item reference, UID, type, slot, color and visit ID.
The player must remain beside the NPC with that piece still worn. The one-DragonBall
black recipe uses the same selection protections. Same-color selections consume
nothing. The selected equipment must be a genuine head/body item; alternate or
inactive pieces cannot establish a worn clothing completion.

Equipment color, fee where applicable, receipt and achievement evidence commit
together. Durable ownership, slot, stack, bound, old color and expiry metadata
are checked before mutation. Item and nearby-player appearance packets are sent
after commit, and achievement/title state refreshes immediately. Missing writes
or stale preimages roll back and fence the session for reconnect. Selection is
one-use and never inherited by a new session or a new visit. Player map setters
share the item owner lock and publish the new map only after persistence succeeds.

Black Tulip now consumes exactly one selected owned inventory unit and credits
ten Chi in one transaction with a receipt. It follows the loaded Chi wallet
preimage, preserves an item while pending Chi changes await save, checks uint
overflow, and sends the existing Chi update after commit. There is no newly
invented 4,000/8,000 redemption cutoff. Immediate duplicate uses of a stack are
debounced; later deliberate use is allowed. It neither requires nor dyes armor
and awards no clothing achievement.

## Validation

The final isolated Debug artifact, SHA-256
`a4b939b766ff33e2a804b689ad2f7525c8c478bd70e4b99aaf113d3d7af0a64a`,
passes **179** cosmetic integration checks: the existing 119, 28 new Black Tulip
checks and 32 armor-shop checks. The suite invokes the production item-use and
NPC routes with non-GM characters, reloads via the production character and item
loaders, checks immediate achievement packets, concurrent repeats, full Chi
boundaries, changed ownership, expired/ineligible items, suppressed writes,
failed admission/teleport recovery and closed-visit reuse. Fixtures use only
`revora_potency_validation`, never production character mutations or external
identity grant delivery.

Before the final inactive-equipment guard, artifact
`f27f1efa0ff75ea5985e62276bb3f5cc4b222c8a79ce1b05e467d8b485633c38`
passed the same then-177 checks plus appearance 23, progression 74, titles 91,
Chi jades 57, Dis City 164, Moon Box 31, Snake Islands 25, Labyrinth 42,
Sky Pass 34, Power Arena 95, BP PK 100 and combat 669. The independent cosmetics
catalog/wire suite passes 68 checks. Three achievement-audit fixture tests pass.
The final guard adds two focused cases; it changes no other event or wire code.

The producer inventory advances to **109/337**, leaving **228** without a trusted
producer. This is not proof of a per-character route to 320 achievements.

## Remaining gates and rollout

- [x] Implement ordinary-player admission, dyeing, persistence and 10115 evidence.
- [x] Match retired Black Tulip to the installed 6711 redemption description.
- [x] Validate isolated payment/replay/reload/failure behavior and related regressions.
- [ ] Confirm native color rendering on all eligible armor families, self and nearby players.
- [ ] Confirm shop travel and dialogue flow in a genuine patched 6711 session.
- [ ] Deploy the coherent server build and initialize the new schema in production.

No live server restart, deployment, website update or public patch occurred.
A future rollout must back up the database and preserve receipts/visits on
rollback. Rolling back the server alone would restore obsolete dye/item behavior;
review this gameplay consequence instead of dropping durable evidence tables.
