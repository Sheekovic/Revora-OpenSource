# Native NPC Buy workaround, patch 1.0.9

Owner report: only Meteor and Bomb could be purchased at the two progression
shops; clicking Buy on other entries did nothing. All 26 blocked entries had
client catalog field 11 (silver price) zero. Meteor had 10000 and Bomb had 1.
All 28 entries already had the intended positive CP prices in field 36.

## Local binary evidence

Read-only disassembly of the lawfully possessed `6711/Env_DX9/Conquer.exe`, x86,
SHA-256 `3728bc3fd4e584d85301edd2b1887a176267cdaa6356038f9951ef23e4fded60`.
Addresses below are preferred VAs, image base 0x400000. No third-party source
code was copied, and no executable was modified or included in this patch.

- Shop loader 0x99837F stores MoneyType at shop+8. Getter 0x9980A8 returns it;
  0x82C442 stores it at UI context+0x50. Getter 0x82C085 returns that value.
  Tooltip caller 0x6B0A04 selects the CP price for nonzero MoneyType.
- Native NPC purchase function 0x44C7D1 zeros EBX at 0x44C7E3. The arguments
  pushed at 0x44C842/0x44C843 pass flag zero and currency zero to validator
  0x44BB20 at 0x44C85C, independently of Shop.dat MoneyType.
- Currency zero selects 0x44BC36: the item price at object+0x4C must exceed
  zero. Failure reaches the assertion string `pItem->GetPrice() > 0` at
  0xB745FC and returns false before the purchase packet is constructed.
  The currency-one branch instead checks object+0x80 (CP price).
- The same currency-zero path calls affordability helper 0x44B25B, whose
  branch at 0x44B3F2 checks silver price, then divides silver balance by that
  price. Setting a positive price therefore retains a silver balance gate.
- The caller returns on false at 0x44C866. Its immediate-buy path constructs
  a request at 0x44C896 and sends at 0x44C8A5 only after validation succeeds.

Confidence: high for these static branches and their match to the owner's
item-specific symptoms. No live client click or packet capture was performed
by the agent, so the complete client interaction is not declared verified.
The existing packet format and server handler are unchanged.

## Deliberate compatibility workaround

Patch 1.0.9 changes only field 11 from zero to one for the 26 affected client
catalog records. All CP prices, item properties, server silver values and
server payment logic remain unchanged. The server's validated shop whitelist
selects CP prices and performs its existing atomic CP/item transaction.
Changing the server's silver values would also affect resale economics and
is intentionally unnecessary for this client gate.

Limitation: carry at least one silver per requested unit of a corrected item.
That silver is not charged by these CP shops. The native quantity dialog may
still use its silver-mode affordability/text. This is a data workaround for
the silent rejection, not a complete replacement of the native currency UI.
Meteor and Bomb retain their existing client prices and behavior.

## Validation and release

`tools/ClientPatches/prepare_shop_buy_hotfix.py` checks immutable 1.0.8 inputs,
encryption round trips, all 28 CP prices, the exact 26 one-byte plaintext
changes, preservation of all other bytes, ZIP contents and file hashes.
All 39 existing ordinary-player shop/persistence fixtures passed again.
Those fixtures exercise the server, not the native client window.

The cumulative progression-shop ZIP contains `ini/itemtype.dat`, `ini/Shop.dat`
and `ini/npc.ini`; the latter two match 1.0.8 byte for byte. It uses the
existing legacy ZIP channel and is not a signed modern critical-file profile.
ZIP SHA-256: `6f6e0240b95ce5cad797c8d8e266592d733a342005ecb8f14277dc2482c0b9bb`.
New itemtype SHA-256: `d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a`.

Local backups, manifest and staged files are under ignored
`obj/chi-refill-validation/shop-buy-hotfix`. For a local rollback, close the
client, verify its current itemtype hash matches the new hash, and restore
the original itemtype from that directory (SHA-256
`5c60721dafdb0d3355111a6b24f4673bcf2349969efbdaa27ac0d2826bad095a`).
Published ZIP versions remain immutable; a public reversal needs a later
version containing the prior content. No server restart is necessary.
