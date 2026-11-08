# Way of Heroes daily checklist

## Documented Revora rule

Installed6711 stage409 has TaskType40, Progress1, OpenLev101 and one bound
Praying Stone1200001. Its text says to complete all Daily Quests once. Revora
interprets this as the seven regular Daily Quest Envoy services, each completed
and rewarded at least once across the character's lifetime. Completions may be
on different dates. Normal paid completion options count because they settle the
same daily service and charge its saved price. This is a documented Revora rule,
not an assertion that every original6711 server used this exact checklist.

| Quest | Required durable evidence |
| --- | --- |
| Worship the Ancestors | Nonlegacy claim with Chi reward identity |
| Everything has a price | Nonlegacy completed run, reward identity and completion time |
| Search for Soldiers | Complete or paid run with its reward identities |
| Rare Matters | Nonlegacy completed run, reward identity and completion time |
| Flowering Orchid | Nonlegacy completed planting, reward identity and completion time |
| Exorcism | Nonlegacy claimed run, completion day/time and reward identities |
| Soul Ball | Nonlegacy completion day/time and reward identity |

The recurring weekend Anniversary event and unconfigured Paradise destination
are not members of this fixed checklist. Hiding a menu option cannot waive a
requirement. Missing tables fail closed. No single daily flag qualifies. Rewards
may have been consumed or traded: historical receipts remain valid. The Envoy
briefly explains the lifetime rule. Existing atomic Way of Heroes ownership,
inventory, claim-ledger and saved-state safeguards remain in force.

## Private acceptance (2026-10-03)

Executable SHA256: `56C524EBAC3050BC354CF941A297F513F764F81F64211567901D5FB4AB538082`.
`career-daily-claim-earn` and `career-daily-claim-reload` passed.

Retained ordinary character4294907001 had already earned Search for Soldiers,
Magnolia, Exorcism and Soul Ball in earlier career phases. This batch completed
Worship through the ordinary NPC and Price/Rare through their ordinary paid
completion options, checking the exact fee/reward CP changes. A controlled2088
clock avoids overwriting old daily receipts. It does not simulate real waiting.

All seven missing-leg checks and11 invalid-receipt checks reject eligibility,
as does another character's UID. These checks use transaction-local temporary
copies of actual earned receipts and rollback; no retained earning row is edited
or manufactured. Actual claim requests are refused before the final quest and
while it is accepted but unfinished. Eight simultaneous final requests yield one
bound1200001; actual item use grants604,800 seconds once. A separate cold process
preserves exact daily ledgers, items, currencies, blessing, claims and saved Way
of Heroes state; repeated409 requests do not change the snapshot.

The first fixture attempts found full bags and all public warehouses full.
Seven ordinary trades retained existing goods with character4294917801 to free
space. This advances that character beyond its earlier104 snapshot; the older
snapshot remains historical evidence, not a current post-trade baseline. No
items or earning receipts were deleted to reset the fixture. A later use attempt
correctly refused because the offline adapter had omitted mentor loading; after
using `KnownPersons.LoadMentor`, the saved stone was consumed normally. The
failed transaction preserved it and the fixture resumed without another claim.

No new combat/drop coverage is claimed in this batch. Earlier earning evidence
plus the retained receipts establish the first four quest completions. Tests use
offline ordinary-player adapters; native-client acceptance and live deployment
remain pending. No live restart or client patch was performed.

## Fresh production-created career reward (2026-10-03)

The fresh ordinary character now qualifies using its own seven earned daily
routes instead of the retained fixture's mixed starting provenance. Sealed2830
stage409 claim grants one bound1200001; unconsumed restart, sealed item use,
exact604800 blessing increase and another cold reload pass. Duplicate claim/use
packets do not grant more. Full prior career and all seven daily ledgers persist.
Current checkpoint is `revora_fresh_daily_claim_probe`, stage used.
[Evidence and inherited earning limits](CAREER_STARTING_BOUNDARY.md#fresh-seven-daily-way-of-heroes-reward-2026-10-03).
This does not close native/live gates or certify an independently earned320
career or Way of Heroes main-stage aggregate.
