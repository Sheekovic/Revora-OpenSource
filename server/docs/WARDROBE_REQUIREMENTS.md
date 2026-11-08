# Wardrobe titles and wings for 6711

For current catalog-wide availability, see the [72-reward earning audit](WARDROBE_AVAILABILITY.md).
The original [quest/event research](research/WARDROBE_QUEST_EVENT_RESEARCH.md) preserves
its earlier baseline and proposed phases.
The eligibility rules below do not by themselves establish playable acquisition
paths: at baseline `23c520b`, local CTF and Elite PK scheduling were parked.
Their restored earning routes are now code-validated in [M2 local CTF](LOCAL_CTF.md)
and [M3 local Elite PK](LOCAL_ELITE_PK.md); deployment and live visuals are pending.

VIP levels no longer grant wardrobe prizes. New characters receive no automatic
Emperor wings. Eligibility is checked on login, after wardrobe operations, before
equipping, and every ten seconds for online players. Earned rewards appear in the
Wardrobe for players to equip; losing a current requirement removes ownership and
clears its equipped appearance. Ordinary players use the same rules as everyone else.

## Automatic requirements

| Reward | Requirement |
| --- | --- |
| Scholar / World-renowned (2027) | 400,000 prestige |
| Superman / World Dominator (2026) | 600,000 prestige |
| Violet Lightning Wings | Total equipped perfection 216 |
| Violet Cloud Wings | Total equipped perfection 324 |
| Great Perfection (2039) | Total equipped perfection 648 |
| Grandmaster | Total Chi score 1,600 |
| Talent | Jiang Hu score 81,000 |
| Rising Star | 320 distinct known achievements supported by durable server evidence |
| Overlord and Wings of Solar Dragon | Leader of the current server kingdom |
| Victor | Current leader of the last winning local CTF guild |
| Wings of Infernal Beast | Unexpired durable award for the local 130+ Elite PK champion |
| Fashionist / Swift Chaser | Five distinct garment / mount designs rated at least four stars |
| Starlight / Moonlight Wings | Own a five-star garment / mount in the wardrobe |
| Monkey / Solar / Lunar / Saint Rider | One permanent monkey / Solar / Lunar / all eight different permanent monkeys |
| Big Fan | One Euro jersey design and a Euro mount armor |
| Euro Collector | All six different Euro jersey designs; Fashion and Legend editions of one design count once |
| Immortal Fate / Successor to Legend | Celestial Fox / Celestial Fox (Fantasy) |
| Slam Dunk Leader | Signed AC Rockets, signed TC Lakers, DC Cavaliers, and BC Warriors uniforms |
| Love Joker | Purple Romance or Bright Purple Romance |
| Sweet Fairy / Love's Madness | Female / male character with Bright Purple Romance, Fierce Cloud, and Pacific Cloud |
| Chosen One | Five maximum-level runes equipped in one rune set |

Expired wardrobe items do not count. Duplicate item instances do not multiply a
collection count. Permanent monkey collections reject temporary versions. Rune
sets cannot be combined to meet the five-equipped-rune requirement.

Local CTF records its winning guild durably at event completion. Leadership
changes transfer eligibility. Old `winnerctf` flags and accumulated CTF reward
balances do not prove a prior result; the first completed CTF after this update
establishes the winner. Local CTF never awards CS-CTF titles or Radiant Wings.
Infernal Wings and highest-bracket champion status share one seven-day award
measured from the committed tournament result. Repeated claims do not renew it;
an unverified legacy champion flag alone does not unlock the wings.

## Historical and unavailable events

Origin-qualified CS-CTF Conqueror and Radiant Wings are a separate local edition
([ORIGIN_CTF.md](ORIGIN_CTF.md)). Origin-qualified CS Elite PK placement titles
are a separate 14-day local edition ([ORIGIN_ELITE_PK.md](ORIGIN_ELITE_PK.md)).
Origin-qualified CS Team PK roster titles are a separate 14-day local edition
([ORIGIN_TEAM_PK.md](ORIGIN_TEAM_PK.md)).
Mr./Ms. Conquer, returning-player campaigns, Poker Cash Tournament, Fashion Icon,
Valentine/Couples events and other historical prizes stay locked without a
matching authoritative award source. [Summer crafting](SUMMER_WINGS.md)
and [Holy Grail crafting](HOLY_GRAIL_CRAFTING.md) now supply earned permanent
awards. Their ordinary server routes are tested; genuine-client acceptance remains
open. Local events, VIP levels and login counts do not substitute for unrelated
event requirements.
The [M1 progression foundation](PROGRESSION_EVIDENCE.md) replaces client-authored
achievement completion with durable server evidence and preserves the original
legacy records separately. Five state observations are connected initially;
the quest/event producers and a proven route to 320 are tracked in the
[roadmap](QUEST_EVENT_ROADMAP.md). M1 is validated in isolation and not yet deployed.

## Persistence and validation

The five-byte saved title record format remains compatible. Existing records are
deduplicated, including old saves whose one-byte record count wrapped. Points are
recomputed from the installed client's numeric title values and stored with the
ownership blob in a transaction. Type 2039 is present in `Storage.ini` but absent
from `title_type.dat`; it receives zero points instead of an invented value.

Before the first changed save, the previous blob and point total are retained
in `revora_title_backup_v1`, once per character. `revora_title_ctf` stores the
current champion guild. Both are additive tables; backup rows contain no account
credentials. Restoring a previous executable alone does not undo reconciled rows;
an operator can restore the matching originals from the backup table with the
server stopped. Preserve that table until the owner decides retention.

The title packet reader bounds and validates protobuf framing. Equip requests
are rate limited and require a currently eligible owned reward. Failed database
writes leave the in-memory wardrobe unchanged. The existing spawn path updates
the wearer and nearby observers after selection or revocation.

Original release validation: the consolidated release passed 1,245 checks (see [cultivation and combat](CULTIVATION_COMBAT.md)); these included 91 wardrobe checks against ordinary players
and a disposable PostgreSQL schema, 21 rune checks, 58 prestige checks, and 19
Jiang Hu checks. Coverage includes all progression boundaries, real maximum
Jiang Hu calculation, collections, rank expiry, CTF persistence, Emperor role,
forged equips, truncated protobuf, duplicate relogs, backup retention, and SQL
rollback. A fresh in-game visual check remains necessary; no client patch is
required for these existing 6711 rewards.

Evidence hashes and packet confidence are in [PROTOCOL_RESEARCH.md](PROTOCOL_RESEARCH.md).
The installed 6711 client supplies the version-specific requirements and point
values. Historical official references corroborate the basic
[wardrobe requirements](https://co.99.com/guide/quest/2016/wardrobe.shtml),
[prestige milestones](https://co.99.com/guide/event/2016/beyond_perfection/index.shtml),
and [Elite PK bracket](https://co.99.com/guide/quests/elite_pk.shtml).
