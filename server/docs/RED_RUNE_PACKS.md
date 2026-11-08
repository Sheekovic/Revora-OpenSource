# Leftover red rune fragments and pack 3306369 → 3306509

M6.56b10c, 2026-09-23. Server implementation. Not deployed.

Ordinary 1009/action-4 and direct `UseItem` of leftover **RedRuneFragment
3306369** and **RandomRedRune(B)Pack 3306509** go through `RedRunePacks`.
The leftover cases fail closed.

Yellow 3306370/3306510 and blue 3306371/3306511 are now owned consumer transactions
([YELLOW_BLUE_RUNE_PACKS.md](YELLOW_BLUE_RUNE_PACKS.md)). Their issuance gaps remain unchanged.
They are not folded into this family.

## Ordinary issuance

NPC **900181** Online Points Seller option 3 sells unbound 3306369 for
7,000 / 12,500 / 20,000 Online Points (5 / 15 / 20 units). Option text that
says `[10]` still `Add`s 15. A hidden dialog is not required.

Yellow SuperBoss VIP7 `MonsterTable` `Random.Next(1, 17)` case 17 names
3306370. `Next(1, 17)` yields 1–16, so that branch is not issuance.
Blue seller options 71–73 `Add(3306368)`, not 3306371. GM `@itemid` logs
are not ordinary producers.

## Leftover terms (red, preserved except capacity)

Combine requires **20** fragment units. Output is one **bound** plus-0 pack
3306509 regardless of fragment Bound flags. Refusal text stays
`You do not have 20 pieces, or you lack 5 inventory spaces.` Catalog
“random Red Rune (B)” copy is the pack, not a second mapping.

Owned capacity credits released stack slots: after consuming 20 units the
pack needs one bag slot. Leftover `Count <= 38` is not adopted.

If any one fragment stack is larger than 1 and at least 20, leftover
`Remove` takes the 20 from that first stack. Otherwise units are taken in
`Objects` order.

Pack `Random.Next(1, 11)` yields 1–10 bound inventory runes:

| Pick | ID |
| ---: | ---: |
| 1 | 4010001 |
| 2 | 4010101 |
| 3 | 4010201 |
| 4 | 4010301 |
| 5 | 4010401 |
| 6 | 4010501 |
| 7 | 4010601 |
| 8 | 4010701 |
| 9 | 4010801 |
| 10 | 4010901 |

Case 11 is unreachable. Leftover `AddRune` then `Inventory.Add` stores the
rune at bag position 0. A stacked pack with `UseItem` set removes one stack
unit. Owned consume+insert+receipt commit together under `ItemSyncRoot`.

## Remaining work

Other leftover packs, Host restart, native acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
