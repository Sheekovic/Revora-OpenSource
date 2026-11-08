# Frozen Grotto fifth-floor passage

Milestone M6.45d repairs the existing Guardian service for ordinary players.
It is built and tested in staging, not deployed or accepted in a native client.

## Player behavior

The Guardian (7764) on floor four, map 2054 at (388,460), admits a nearby living
player to floor five at a validated landing near (537,568). Admission consumes
one usable Grotto Key (721347), including one unit from a stack. Bound keys are
selected first. The existing Revora VIP-4 free-entry option remains available.
Both routes previously sent the player to Twin City despite their dialogue.

The floor-five Guardian (7765) returns the player to floor four near (388,463).
Its other options, including Shadow Jade, remain separate legacy services.
Travel between floors five and six belongs to the same visit. Leaving either
upper floor for another map ends an entered visit; a later visit requires a new
key or eligible VIP admission.

The dialogue is brief. An interrupted admission can be resumed beside the
floor-four Guardian without another charge. It does not automatically move a
reconnecting player back beside the Guardian.

## Evidence and edition limits

The official [Missing Key guide](https://co.99.com/guide/quests/10expansion/missing_key.shtml)
identifies keys earned on floors three/four and handed to the floor-four
Guardian for fifth-floor entry. Read-only installed NPC data places that
Guardian one tile from the guide's coordinate. The installed 6711 item catalog
confirms item 721347 as a tradable Grotto Key. The official
[patch 5580 notes](https://co.99.com/news/2012-03-19/Patch_5580.shtml) confirm
stackable keys. Reviewed itemtype.dat SHA-256:
`D49F0658BF01387B99C284DDEC81713838F2EAE3ADB8E63666E5C906F63CF87A`.

The official [VIP update](https://co.99.com/content/2011-09-21/20110921230837440.shtml)
confirms free fifth-floor entry opportunities for VIP levels 4–6, but does not
establish an exact daily allowance. This change preserves Revora's existing
repeat-entry rule; it does not claim an exact original quota. The inherited
character loader also grants at least VIP 4 to normal players. That server
policy is unchanged; the paid-path fixtures deliberately use VIP 0.

The installed fifth/sixth-floor portals and floor-five return NPC supply the
route evidence. No new packet format, native client patch or map identity is
introduced. Key earning now follows in [M6.45e](GROTTO_KEY_EARNING.md), with a
documented Revora drop-rate policy. Dragon Slayer objectives and Magnolia
flowering remain separate work.

## Payment, recovery and ownership

`revora_grotto_passages` retains one open passage per character. The character
row is locked while a transaction validates the saved origin, debits the exact
owned key preimage and inserts the passage. Inventory locking serializes
ordinary-session requests. The service rejects noncanonical sessions, remote
requests, dead/fake players, trade/booth activity, unsuitable keys and invalid
destination metadata before admission.

Only a committed debit changes the visible inventory. A map persistence trigger
records arrival and departure. If payment commits but teleport fails, the
session is fenced for reload and the open passage survives for a later retry.
Failures before commit roll back both debit and passage. Reconnect fixtures
exercise the real character-state and inventory loaders, then return the player
beside the Guardian. They do not simulate native login or display.

## Validation

The staged server builds successfully. Final validation: 26 Grotto, 105 travel,
179 cosmetic ownership, 9 quest-packet, 43 Labyrinth and 3 producer-audit checks
(365 total). Tests include concurrent confirmations, stacked/final keys, VIP and
non-VIP paths, missing destinations, ownership/stack/bound/lock mismatch,
suppressed receipt insertion, failed map persistence, ordinary-player reload,
full-save persistence, upper-floor transitions and coexistence with the dye-shop
map trigger. No live database mutations are performed by these disposable-schema
fixtures.

Staged server SHA-256:
`6B6210F92243D851BE0B726E97A6A16D667F6CCA829320EB28A8442B50EF9D41`.

Source producer coverage remains 156/337, an upper bound rather than proof of all
ordinary-player routes. Native entry, return and inventory presentation remain
unverified. No live restart, deployment, client installation, public patch or
website publication was performed.
