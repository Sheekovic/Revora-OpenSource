# BP PK identity and admission prerequisites

Research checkpoint 2026-09-15; baseline `f178e18`.

The [2015 official BP PK event page](https://co.99.com/guide/event/2015/BPPK/)
and [July 2 announcement](https://co.99.com/news/2015-07-02/battle_power_tournament_is_about_to_show_on_stage_.shtml)
resolve a distinct Battle Power Tournament, separate from Elite, Extreme and
Class PK. Its three simultaneous divisions are 250-300, 301-350 and 350+.
The published 350 boundary overlaps; an implementation must explicitly resolve
it rather than putting one player into two groups. The listed 2015 rounds are
four Sundays, with an hour-long competition and an NPC at Twin City 295,141.
The news date prose and four-date schedule differ; use the explicit date table
for historical evidence, not a claim that a 2015 schedule is currently live.

Champion rewards differ by division: 300/500/1,000 CPs, 5,000/10,000/15,000 Chi,
a seven-day honor halo, and a corresponding non-tradable token. Two to four
same-kind tokens exchange for larger Chi awards. The text says tokens are cleared
before the next tournament, which conflicts with its repeat-champion collection
prose unless tournament means the overall campaign. That ambiguity needs a
published Revora season policy and recovery-safe claims. The original arena, manager UID, halo identity and reward delivery policy
remain prerequisites; subsequent evidence below resolves combat and token IDs.
Installed achievement 11114 describes joining BP PK; evidence is now strong for
this separate event family, not permission to award it for another tournament.

## BP calculation prerequisite

The [official BP guide](https://co.99.com/guide/guides/calculate.shtml) includes
mentor and clan sharing as contributions. The
[clan guide](https://co.99.com/guide/guides/family.shtml) and
[official FAQ, 20a](https://co.99.com/guide/faqs.shtml) select the strongest
same-clan teammate on the same map, not necessarily the clan or team leader.
Bonus tower levels 0-4 correspond to 30/40/50/60/70 percent in Revora's storage;
the basic clan has tower zero. The legacy team method restricts the source to the
clan leader and never includes its resulting field in total server BP. Simply
adding the cached field creates a feedback loop because the old source margin
uses already-shared totals.

Mentor calculation uses an existing Revora gap/3.3 policy and a 125-135 taper.
Its unsigned conversion goes wrong above 135. Exact original mentor percentage
and whether borrowed BP enters a source margin are not established by the published guide.
Preserve the explicit Revora mentor ratio, floor each stage, end the taper at 135,
and compute both dynamic shares from sources excluding mentor/clan shares so
neither can recursively amplify the other. Only the higher mentor/clan share contributes to total BP, as established by
installed StatusTips.ini section 44. The guild and independent configured
bonus remain in the source score. This is a documented Revora calculation policy,
not a claim that an unpublished original formula was recovered.

Native shared-BP messages and admission/result presentation remain acceptance
gates. Identity research alone does not complete the BP PK coordinator.

## Follow-up evidence, baseline 7e93cca

The [official July 2015 launch notice](https://co.99.com/news/2015-07-09/battle_power_tournament_officially_meets_all_this_sunday_.shtml)
explicitly makes the last survivor champion and directs the champion to the
manager to claim. The [patch 6102 notice](https://co.99.com/news/2015-07-08/patch_6102__battle_power_tournament__july_super_sales_and_more_.shtml)
confirms that the simultaneous groups allow one competition per hero. Interview
links from the event landing page now redirect to the official error page;
those inaccessible pages provide no further rules.

Installed, read-only content inspection:

| Artifact | Numeric fact | SHA-256 |
| --- | --- | --- |
| ini/itemtype.dat, decoded in memory with the existing reviewed codec | Average/Elite/Super tokens 3006840/3006841/3006842, one-item stacks | d49f0658bf01387b99c284ddec81713838f2eae3adb8e63666e5c906f63cf87a |
| ini/npc.ini | NpcType3727 identifies BPTournamentManager | b06db1e2484992a1b58eabd7a3d884da07ac728a4a183d0fd18f2dccd5b20497 |
| ini/NpcX.ini | Presentation entry 5488 identifies BPPKEnvoy | 60107f543d4bdb861984ea5021a3713f735ea1956d97c4b74956ec00fe66a1ce |
| ini/StatusTips.ini, section 44 | Total BP uses the higher mentor/clan share | 6dec8fe19fef9ec06aa7d32bf0f16d861439421c6b1822342348c05884a5cd35 |

These presentation identifiers are not server NPC UIDs. A bounded read-only query
found no installed NPC matching these names/looks and no NPC within five tiles
of the original location. The ignored Revora-owned x64 inspector was hash-recorded
before execution: `B74C2D94D3026A0EB7230E31AD735E5873D701C6635226CF0082B95F3486A646`.
It read only NPC columns inside an explicitly read-only PostgreSQL transaction.
No original client content was copied into Git or changed on disk.

## Implementation contract and remaining work

The [implemented server contract](BP_PK_IMPLEMENTATION.md) now reuses the owned
local-PK coordinator with distinct immutable BP kinds and three private arenas.
It preserves original CP/Chi/token rewards and last-survivor combat. Four-Sunday
monthly seasons, non-overlapping divisions (250-300, 301-350, 351+), a 20:10
admission deadline and token expiry at the next season resolve historical
ambiguities explicitly. These are Revora policies, not recovered 2015 rules.

Admission freezes one division for the round across retries, reconnects and all
three groups. Combat checks reject power above the frozen ceiling. Loss of a
shared bonus after admission is allowed. Only a contested BP field earns 11114;
other PK results cannot award it.

Claims use saved champion identity, CP and loaded Chi wallet preimages, a bound
token with an earned origin, overflow/full-bag deferral, atomic receipts and
reconnect recovery. Exchanges consume only the winner's issued same-kind,
same-season tokens. The original native halo still needs evidence; generic PK
effects are not proof of this honor. Server earning routes are implemented and
tested; the roadmap keeps halo projection and genuine-client acceptance open.
