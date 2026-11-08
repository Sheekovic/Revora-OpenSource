# Way of Heroes: eight Super Gems

Implemented and privately validated 2026-10-02; not deployed live.

Installed stage707 (task28, second rebirth level15 opening) requires eight
embedded Super Gems and awards bound Refined Tortoise Gem700072. The client
item catalog identifies this as the refined damage-reduction gem; the normal
embedding service accepts it. The reward is not a Super gem.

## Shared socket calculation correction

The former shared socket predicate read primary positions1-12 without checking
the active bank. It could qualify from inactive gear. Socket/Super-Gem milestones
now read the active combat bank's slots1,2,3,4,5,6,8 and shared slots10,11,12.
They require permanent, nonsuspicious, single equipped items, saved ownership,
valid item category/slot and matching runtime item identity. Cosmetics, bag,
warehouse, wardrobe and inactive-bank equipment do not contribute. Ordinary
one-handed offhands retain the existing slot5 exception. Duplicate positions
require reload. Existing socket scoring still counts a legitimate empty socket
for socket milestones, and only filled Super-grade gems for Super-Gem milestones.

This changes the common calculation for existing task27/28 substages as well as
new707. It does not change their thresholds, issued rewards or previous receipts.
Previously claimed milestones remain claimed. Unclaimed milestones are evaluated
against currently eligible equipped gear.

## Private process evidence

CheckCandidateEightGemClaim.cs supplies level130, second rebirth, Warrior class,
stats,500,000 CPs and four low-level pieces with empty double sockets. It does not
supply filled qualifying gems. Eight Super Gems are bought from the actual CP
shop at loaded prices, then embedded through normal sealed1027 packets.

- [x] Claim refuses before each embedding, covering zero through seven Super Gems.
- [x] Removing the completed boots to inventory refuses; re-equipping restores
  the eligible set. Switching to an empty alternate bank also refuses.
- [x] After switching back, eight concurrent requests award one bound refined
  Tortoise Gem with one unique707 receipt.
- [x] The reward is embedded normally into a separately supplied empty-socket ring,
  consumed once, and the ring equipped. Nine embedding receipts are saved.
- [x] Full private game/database restart preserves the exact scoped inventory,
  socket/embedding, purse and milestone snapshot. The refined gem remains in the
  ring, and repeated claim packets change nothing.

Game SHA256:
004068C236E4A19443E4E64A37B9D43F07F83C9319683242C214225720EA8FD6.
Build completed with existing warnings under the committed-source overlay,
excluding unrelated owner WIP. Earning and cold reload passed on the first run.
This fixture verifies the shared count through707; it does not independently
re-earn every previously enabled socket milestone or claim native acceptance.
