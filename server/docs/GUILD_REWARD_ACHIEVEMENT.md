# Receiving guild rewards (10228)

The existing ordinary Guild War purse now earns achievement 10228 when delivery
commits. The installed catalog describes receiving guild rewards; this route uses
a real server-owned guild result and paid purse, not a rank, loose token, pending
prize or a client claim that rewards were received. It does not assert coverage of
every original Conquer guild reward variant.

GuildWarPurses.Claim already owns the transaction for CPs, RVPs, silver, bound
one-day items, honor and the claim receipt. Achievement evidence now belongs to
that same transaction. Suppressing its write rolls everything back and leaves the
prize available. Ordinary NPC collection and automatic delivery both use this
path. The committed inventory and currency projection is followed by achievement
and title reconciliation, so receiving credit does not require reconnecting.

Progression recovery consults completed purse receipts when 10228 is missing.
An already-paid receipt can repair missing evidence without paying again; pending
or absent purses cannot. A partial paid-receipt owner index supports this lookup,
and existing evidence skips repeat insertion. Recovery is disabled until the
purse service has initialized its schema. No new NPC, packet, reward amount,
eligibility restriction or fee is introduced.

The isolated build and all 361 guild season checks pass. New checks cover actual
recipient-only credit, duplicate claims, ordinary character reload, automatic
payment projection, the distinction between earned and delivered rewards, atomic
rollback on achievement-write failure, safe retry after reload, and recovery from
an existing paid receipt. Existing battle, guild prize, purse, leadership and
activity checks remain green. Fixtures now initialize the achievement packet on
their ordinary purse characters so immediate packet-state assertions are meaningful.

Native guild UI, full fresh-Host acceptance and deployment remain pending. These
are isolated server packet/database journeys, not a genuine-client playthrough.
Guild Beast credit remains separate: family 7006 is identified by earlier local
research, but a name match or a generic monster kill is not accepted as encounter
ownership evidence. Pillar credit is implemented separately through accepted
first-hit receipts; see [GUILD_PILLAR_ACHIEVEMENT.md](GUILD_PILLAR_ACHIEVEMENT.md)
for the validated scope and remaining acceptance gates.

Guild Beast follow-up: its separate daily earning route is now implemented and
isolated-tested; see [scope and pending gates](GUILD_BEAST_EVENT.md).
