# Leftover Study books 720774 / 720775

M6.56b9i, 2026-09-22. Server implementation; not deployed.

Ordinary 1009/action-4 and direct `UseItem` of leftover **StudyBook(Arena)
720774** and **StudyBook(Race) 720775** go through `StudyBooks`. Both legacy
cases fail closed. Study uses `entities.studypoints` compare-and-swap. The
confirm prompt, `SubClassShowFull` (`ID=8`, `StudyReceive=50`), `zf2-e300`
and `You received 50 Study Points!` (`Color.Red`, System) occur only after
commit. Overflow above `ushort.MaxValue` refuses without source loss.

Leftover `GameState.MessageBox` is gated by `AutoInvite.Enabled`, which stays
false in installed source, so the leftover callback never ran. This service
sends the same `NpcReply` MessageBox prompt and a versioned pending offer
instead of inventing a different dialog.

Catalog and HonorShop copy advertise “once a day” / “such a book”. Leftover
executable has **no day ledger**. This service does not invent a daily limit
or share one across the two IDs.

| ID | Catalog | Honor price | Study |
| ---: | --- | ---: | ---: |
| 720774 | StudyBook(Arena) | 10,000 | 50 |
| 720775 | StudyBook(Race) | 1,500 | 50 |

Ordinary issuance is the loaded Honor shop (`HonorShop.Load`, UID 6000) through
already-owned `HonorPurchases` (unbound). Ordinary Honor earning includes
Arena daily reward +500 and Qualifier 100-win +500. Receipt table
`revora_study_book_uses`. Direct daily P7 is unchanged.

## Remaining work

Catalog-only Chi pills 3000051/729476–729479, catalog-only Surprise box 722178,
nested/gear leftover 721222–721245, classified silver leftovers, leftover
medicine bundles 720010–720017, other leftover packs, Host restart, native
acceptance and deployment remain open. Parent
[M6.56b](COMPOUND_REWARD_INVENTORY.md) stays open.
