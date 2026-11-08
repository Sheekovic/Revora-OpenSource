# Superior Refinery Pack (M6.48a prerequisite)

Item 720399 now opens for ordinary eligible players through the existing managed
refinery-pack service. Previously it had no executable item-use case. This is a
prerequisite for restoring Terato Dragon's rewards, not a completed boss encounter
or a newly enabled earning source.

## Evidence and behavior

The official [Dragon Slayer guide](https://co.99.com/guide/quests/10expansion/terato_dragon.shtml)
lists Superior Refinery Packs among the final encounter's random rewards. The
installed 6711 item catalog identifies 720399 as `SuperiorRefineryPack` and
specifies 70% level-four / 30% level-five material. The reviewed itemtype.dat
SHA-256 is `D49F0658BF01387B99C284DDEC81713838F2EAE3ADB8E63666E5C906F63CF87A`.

An integer draw from 0 through 99 selects level four for 0-69 and level five for
70-99. A second uniform draw selects an installed, usable refinery material at
that level. The eligible pool is the intersection of the server refinery and
base-item catalogs, excluding materials intrinsically marked untradable. Using
that complete installed pool is Revora policy; the source establishes the quality
probabilities, not a historical per-material distribution. Bound packs produce
bound materials; unbound packs produce unbound materials. Loose materials have
no expiry. Applying one uses the established refinery lifetime and stabilization
rules.

## Transaction and compatibility

The service reuses `DisCityPacks` and its existing
`revora_dis_refinery_packs` receipt table. The internal legacy names remain to
avoid a data migration. The new type does not change the quality distributions
of Dis City's Super (3008057) or Sacred (3008058) packs.

Opening validates the canonical session and inventory object, absence of trade
or booth, lock/suspicion/wardrobe/expiry state, and capacity. A single pack may
replace itself in a full bag; a stacked pack needs another slot. The character
row lock serializes payment with other persisted player services. Saved
inventory count and exact item preimages must match before debit. Debit, output
item and opening receipt commit together, with affected-row checks that also
detect triggers silently suppressing inserts. Only then are inventory packets
sent. Failed transactions preserve the pack and require session reload.

Concurrent clicks share the owner lock and the existing one-second stack replay
guard. A later deliberate use can consume another stack unit. Spent single-pack
UIDs cannot open again. No new wire layout or native profile was introduced.

## Validation

The staged build passes 174 refinery checks, including the existing Dis City
packs and refinery application/stabilization coverage. New fixtures cover the
four probability boundaries, every eligible level-four material, binding,
ordinary item reload, concurrent clicks, full bags, invalid sessions/items,
stale persisted inventory/locks, silent result/receipt failure and retry, and
invalid random outputs. A material obtained from the new pack is applied to a
blade through the production refinery dispatcher and persists on the equipment.

Related suites: 164 Dis City, 191 Grotto, 64 subclass progression and three
producer-audit checks, for 596 checks total. Producer coverage is unchanged at
156/337. Tests use disposable server fixtures; no genuine-client display or
live deployment is claimed.

Staged server SHA-256:
`F26C397DD52F1B5E76B67D7DFFC84F7A44D92D2FDAAE0BABB5AF19C7D86055A0`.

The subsequent [M6.48b milestone](TERATO_DRAGON.md) supplies the Terato earning
source, paid-summon recovery, team eligibility and durable reward lifecycle.
Native acceptance and deployment remain separate open work.

## Way of Heroes catalog identity (2026-10-02)

Item728919 now shares this handler with720399 because its installed description
specifies the same70/30 level4/5 distribution. Stage904 supplies three bound packs.
See [earning and cold-restart evidence](WAY_OF_HEROES_EIGHTEEN_SOCKETS.md).
