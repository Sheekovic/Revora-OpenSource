# Guild season leader claims (M7.2a)

Staged server behavior: an ordinary current guild leader can visit GuildDirector
10003 in Twin City (317,321), choose **Guild War honors**, and claim one bound
Lord Token for a recorded Sunday victory. Claims open Monday 01:30 and close
Friday 00:00 in the season's saved server time zone. An unclaimed victory follows
the guild's current leader. A claimed token remains with its recipient.

The receipt binds the winning day, season, guild, claimant and exact item UID.
The item expires at the next season boundary. Duplicate clicks, later transfers
and repeated visits cannot claim the same battle twice. Unrecorded legacy Lord
Tokens do not acquire earned provenance from this service.

## Leadership and transaction boundaries

Existing promotion actions 28/29/34 now validate their established 1107 frame,
name bounds and seal before transferring leadership. The owner must be the
canonical current leader; the target must be the actual guild member. Transfer
persists both character ranks and the guild leader name in one transaction,
then projects the guild's leader reference, member ranks and online player ranks.
Offline members are supported by the ordinary guild-member loader.

Claims and transfers share a guild lock. Character rows precede the guild row
in database locking. Online transfer also obtains the target's inventory lock
without waiting while holding the sender's lock. Stale saved ownership cannot
authorize a claim. Item insertion and the unique claim receipt commit together;
failed required writes roll back and fence the affected session for reload.

Full bags, remote interaction, dead/fake/trading players and replaced sessions
cannot claim. Rewards use normal inventory updates and survive ordinary item and
character reload. Native promotion and dialogue presentation still need acceptance.

## Evidence and validation

The installed database identifies NPC 10003 at the above coordinates with mesh
150; installed `npc.ini` agrees on GuildDirector/NpcType15. Read-only inspector:
`obj/chi-refill-validation/GuildInspect.exe`, SHA-256
`1F75BB0627FEF0AF079EB7B8BD828FAC0F75D2397BB24C78FAE1A45FBD7A1175`.
No NPC placement or client patch is required for this staged route.

Build SHA-256:
`41FEBDF33D40479ED9F7BFAC180C770AE7CD37D90C9687F68D62A8B94D51C563`.

`Verify-PotencyHunt.ps1 -GuildSeasonOnly` passes 77 checks: 43 battlefield checks
plus 34 claim/leadership checks. They include actual recorded battle victories,
concurrent native-command transfer and NPC claims, seven malformed promotion
frames, full inventory, suppressed guild/item/receipt writes, exact expiration,
ordinary save/reload, offline leadership reconstruction and stale saved ownership.
Related suites: CTF 48, progression 74 and Potency/daily bosses/Extraction 216.
Total: **415 checks**. Tests use a disposable schema and ordinary non-GM fixtures.

## Remaining M7 work

M7.2b now adds [one/two/three-token exchanges and CP payouts](GUILD_SEASON_PRIZES.md).
M7.2c adds [Gold Prize rollover](GUILD_PRIZE_ROLLOVER.md), and M7.2d corrects
[cup statistics](GUILD_CUP_STATS.md). M7.3 adds [Grail crafting and its two
wardrobe awards](HOLY_GRAIL_CRAFTING.md). [M7.2e](GUILD_WAR_PURSES.md) adds
atomic regular Sunday rank purses. See [research](HOLY_GRAIL_RESEARCH.md).

No live restart, deployment, patch or production-row mutation was performed.
This milestone completes the claim/transfer implementation, not the full M7 route.
