# Nested Elite prize packs

Date: 2026-09-16. Server build milestone; not deployed.

## Behavior

`EliteNestedPacks` replaces the split-write openings for three nested rewards
from the existing Elite prize bundles. Every eligible ordinary player can open
an owned pack; no GM status or character allowlist is used.

| Pack | Complete gift |
| --- | --- |
| 720049 | One +6 steed (300000), Horse effect and a saved color |
| 720731 | One client-defined weapon accessory from 236 reviewed catalog entries |
| 729626 | One level-5 Super weapon refinery selected for the current class |

All gifts inherit source binding. The steed retains the existing Revora three-
color distribution, 50% / 25% / 25%, and its exact packed color persists across
reload. This is the legacy recipe, not proof of the original rare-steed lottery.

The accessory pool contains 130 permanent, five 5-day, 57 7-day and 44 30-day
entries. Timed rewards persist their actual catalog lifetime and issuance time.
Draws are uniform across eligible item IDs using rejection-sampled cryptographic
randomness. These are Revora rates; historical official rates are not claimed.

The old refinery `Name.Contains("Super")` filter selected Superior packs and
non-weapon materials too. The replacement validates level-5 metadata and uses:

| Class | Eligible refinery IDs / weapon family |
| --- | --- |
| Trojan, Ninja, Monk, Pirate, Dragon Warrior, Wind Walker | 724409, 724453 / one-handed class weapons |
| Warrior | 724409, 724414, 724453, 724458 / one- or two-handed weapons |
| Archer | 724404, 724463 / bow |
| Initial, Water and Fire Taoist | 724424 / backsword |

Class selection is a reviewed Revora policy based on the pack's class/weapon
contract. It does not promise a reward for the specific weapon currently
equipped; for example, a Ninja receives a katana-compatible material. Unknown
class values preserve the pack. The supported ordinary promotion boundaries are
tested, and each selected material fits a catalog weapon through the actual
refinery compatibility service.

## Ownership and recovery

Under the owner inventory lock, one transaction locks the durable character,
checks its class and complete bag UID set, consumes one exact source unit,
writes the full gift and an opening receipt. Receipt fields include source UID,
stack, class, result UID/type, binding, plus, effect, color and lifetime.
Ownership, lock, suspicion, wardrobe, expiry and stack preimages are checked.
Each required write must affect one row. Cache and client item changes follow
commit. Failed writes roll back the entire exchange and require a normal reload.
No currency or experience is granted by these nested openings.

A consumed singleton frees its slot. A remaining stack needs one empty slot.
Capacity, eligibility and ownership denials occur before drawing a reward.
Zero-stack legacy singletons still work. Concurrent duplicate stack requests
are debounced for one second; later deliberate uses may consume another unit.
The current item-use protocol has no per-click durable request ID, so the
short debounce is not described as unlimited request replay detection. Durable
source ownership/debits prevent a spent singleton from being replayed.

## Catalog evidence

The [official Elite prize guide](https://co.99.com/guide/prize_upgrade/elite_pk.shtml)
lists the +6 steed, accessory and Super refinery packs in tournament bundles.
The installed 6711 descriptions identify accessory and class-based weapon
refinery contents. The steed text has encoding damage; the +6 recipe is also
established by the legacy server branch and official prize name.

The actual server loader reads `itemtype-new.txt` plus `ItemtypeSub.txt`, not
its older `itemtype.txt`. Their SHA-256 values are respectively:
`1ED7A23B7A34B5E3706277F97E421E3CDF0B269EEB5BE7367A4EBC0E4F22B3EF`
and `6E318B1E53B0FB28036BD547B5CB78914E3E66B1272D8054259B6B0BCAA66AEA`.

All 236 accessory IDs exist in the active client catalogs after read-only
in-memory decoding with the existing reviewed transform and exact re-encoding
round trips. Client encrypted hashes:

- `ini/itemtype.dat`: `D49F0658BF01387B99C284DDEC81713838F2EAE3ADB8E63666E5C906F63CF87A`.
- `ini/ItemtypeSub.dat`: `3388FB5751C4B3AFE9B3E578DACFF8937AC43C2196894180A11403A9837DF594`.

The server supplemental record SpeedSlayer (360204) exists in neither active
client catalog. It is excluded from this random prize pool rather than awarded
as an undefined item. Eleven other IDs absent from the stale plaintext main
catalog are confirmed in the active supplemental client catalog. No active
client file was edited or copied into Git; record presence is not native render
acceptance.

Public refinery metadata was inspected in a read-only transaction, without
player data. Revora-owned x64 inspector SHA-256:
`9D798EAEBC372DEE8010369F7B1858B7863B19AC50FF807E346999D65F0D29D9`.
Helper source, binary and logs remain ignored under `obj/chi-refill-validation/`.

## Validation and remaining work

The isolated Debug x86 build passed, followed by 1,638 prize checks and three
producer-audit tests. This includes all 236 accessory outcomes bound/unbound,
production reloads, class boundaries and actual weapon compatibility, all
steed colors, exact capacity, zero/stacked sources, duplicate use/autosaves,
source expiry, stale metadata/class/bag rejection, invalid RNG and suppressed
source/gift/receipt writes. The prior outer-prize, EXP, mentor and rebirth
recovery cases remain green. Fixtures use only the disposable validation schema.

Final server SHA-256:
`83CC4D98F9F7C449F5078992FB2F8CFAD43FDE499C64B010B78BA9FE818ABC9B`.
Final harness SHA-256:
`7E5E7F0FB0337F067E2AAEDFC3ED13BECF06D72FFF0B0DB183CF71C7EB7AEA08`.
Ignored validation log: `obj/chi-refill-validation/nested-prizes-final.log`.
The audit remains 219 present and 118 absent; it is not per-character earning
proof.
The champion bundle fixture supplies the original pack, opens all eleven nested
packs, reloads every reward and applies a resulting refinery to an owned weapon.
It does not establish an earned tournament placement. The placement-to-claim
route, original full prize economy, steed lottery parity and genuine-client
acceptance remain separate work. No new title or achievement producer is added.
No server restart, live schema mutation, client patch or website update occurred.

2026-09-16 claim follow-up: The previously supplied-pack fixture is now complemented by an actual four-bracket ordinary-player earning journey in [Elite prize claims](ELITE_PRIZE_CLAIMS.md). Original economy and genuine-client gates remain open.
