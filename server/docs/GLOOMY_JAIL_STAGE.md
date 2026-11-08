# Map 1363 private repair stage

2026-09-15. M6.45c completes a reproducible terrain/index prerequisite for the
remaining exploration destination. It does not enable a player service or mark
the destination's admission/native acceptance complete.

## Installed evidence

The approved client already contains `map/map/maidan-D.7z`, with one member,
`maidan-D.DMap`. Both the client and server `GameMap.dat` omit map 1363, while
the installed server map table and spawn records still reference it. The client
region catalog assigns achievement 10726 to map 1363. The active server lacks
`database/maps/1363.map`.

The extracted DMap has version 1004, dimensions 560 by 560, puzzle reference
`map/puzzle/maidan-D.pul`, no embedded portals and 178 type-4 decorative objects.
Its 313,600 cells comprise 134,697 walkable and 178,903 blocked cells. There are
no type-1 collision scenes requiring an external scene merge. The reviewed
server `Game.Map` reader treats type 4 as a fixed 416-byte payload after its
type word. The staging parser rejects other object layouts and unexpected
trailers rather than dropping unrecognized collision data.

Registration uses 1363 and `map/map/maidan-D.7z`. Its trailing value 256 follows
all four installed A/B/C/E sibling entries. The meaning of that value and the
resulting native display are not claimed as newly verified. This remains a
private candidate pending native acceptance.

Both source indexes contain a conflicting duplicate for map 1067: the first
record selects `qiling-z.7z` with value 256, the second `09Christmas02.7z` with
value 128. The repair preserves all original record bytes and order, including
this pair. It changes the header count and appends only the new 1363 record.
Other duplicate identities are rejected. Record counts change from 379 to 380
for the client and 364 to 365 for the server.

## Provenance and hashes

Approved DX9 executable: `Env_DX9/Conquer.exe`, SHA-256
`3728BC3FD4E584D85301EDD2B1887A176267CDAA6356038F9951EF23E4FDED60`.
The tool verifies its hash but neither executes nor copies it.

| Artifact | SHA-256 |
| --- | --- |
| Existing client archive | `5E54B7F276B845120E37BCF1E930315ED4E9008A1F05DAF67735E95C5036B2C7` |
| Extracted DMap | `DDCE0FEC05AB9D2DD1C6509CFD1867A08CE75F3143875F57EE920B80CA65102B` |
| Original client index | `86A54928A9682617371542CDB5A72707C799B92A5C820E3581183ADAA94F32B0` |
| Original server index | `D1F35D6DC3DC0D1958A2AA612C4CDC988ED693636D89AD93078388E81CD23C19` |
| Staged client index | `97EE94E06E3C2CBF19CEF7FAA5CB4B4A4559DCC7E47860E6737EC6DC97F17018` |
| Staged server index | `A4D824FF90445C59DF790F2045D894514D35C5FAB64FB72F2772BF8D323EED78` |
| Staged server terrain | `A9A555CF6B1B407F75B5CE19AAAEC2109D3D1397CF8B0675B67ACFDF7B5A54DD` |

The single archive member was extracted with installed 7-Zip 26.02 x64, executable
SHA-256 `83967F1B02B43C4EFEDA302795722C809E0E81B8307DE73558D10484D5676A7D`.
The archive listing, size and member path were inspected before extraction.
No extracted code was executed.

## Reproduction and validation

From the server repository, after bounded extraction to an ignored directory:

```powershell
python tools/stage_gloomy_jail.py `
  C:\Users\Administrator\Desktop\Revora\6711 `
  bin/Debug `
  obj/chi-refill-validation/travel-1363-source/maidan-D.DMap `
  obj/chi-refill-validation/gloomy-jail-stage
```

The final destination must be new and outside every source tree. The tool reads
exact reviewed inputs, creates outputs in a temporary sibling directory,
rechecks all source hashes and renames the complete stage into place. It rejects
an existing destination. The private stage contains separate client/server trees
and a review manifest recording provenance, output hashes and pending gates.

Validation passes 11 Python staging tests, including malformed/truncated data,
path/count guards, duplicate preservation, blocked/walkable polarity, immutable
inputs, overwrite rejection and changes detected during staging. Six C# floor
integration checks compare every source cell with the actual Revora `Floor`
implementation, verify bounds and check that an eligible staged point matches
10726's geometry. Three achievement-audit tests pass; source coverage stays
156/337. No new production build was needed for this tooling-only change.

The floor harness SHA-256 is
`0070741BB0AB692F37BEFF30499D864A2A20D300B46A51444E913470DF961FE5`.
It was compiled against staged server
`F298D76F06865F0C061A9D7725D16A81AD50624FE98E553B77DE43CDFD632B0B`.

## Remaining gates

2026-09-20 admission follow-up: the achievement catalog calls 10726's destination
**Asura Hell**, while `region.ini` uses the shared `GloomyJail` label for both
1363 and 1364. The adjacent achievement names are Deserted Wild, Flame Plain,
Death Cave and Corpse Forest (10723-10725 and 10727). Search by the shared region
label must not be treated as proof of a PK-jail or Violet Jail admission route.
The installed achievement hash remains the one recorded in
[housing research](HOUSING_EARNING_RESEARCH.md); the region file hashes to
`939c68eb8a7d00b6a21adbf494ab421140b8f7b7f139f5eb7a2c9645ef404618`.
Bounded official-guide searches by these destination names did not recover a
verified entrance, cost, exit or quest contract. Admission remains unresolved.

The private stage is under ignored `obj/chi-refill-validation/gloomy-jail-stage`.
No installed server/client files were changed. No proprietary map, archive,
index, executable or release artifact is committed to Git.

Ordinary entry and exit remain unimplemented: the inspected map has no embedded
portals, no installed portal-table section and no installed NPCs. Twelve server
spawn rows exist on 1363, but the broader 1360–1364 population contains many
numeric placeholder monster names. These rows alone do not establish a complete
quest or admission contract. Do not add a free teleport and label the original
quest restored without resolving its requirements and population.

Native registration, visual/collision agreement, content dependencies and a
complete ordinary-player route must be verified before coordinated installation.
The final exploration setup gap therefore remains open; 45 regions have current
runtime terrain/setup support. The staged 1363 candidate is an additional
prerequisite, not a deployed 46th destination. Any later installation must back
up and verify exact preimages and supply rollback. Signing/public distribution
and all inherited live deployment gates remain pending.


## Current artifact and navigation review (2026-09-27)

At source4c38cd80, the installed client index and maidan-D archive still match
CLIENT_INDEX and ARCHIVE above. The private staged client/server indexes and
1363.map also match all three recorded output hashes. Reusing the existing
stage is sufficient; no new extraction, package copy or runtime install is needed.

A bounded four-neighbor flood fill of the hash-verified313,608-byte server floor
finds **one connected component containing all134,697 walkable cells**. The
central tile280,280 has a completely walkable5-by-5 neighborhood. This establishes
a candidate for future entry/return placement, not an approved NPC location or
an original entrance. It uses no diagonal corner crossing or jump assumptions.
The ignored result is `obj/asura-terrain-connectivity.json`; reproduction is to
read the two little-endian dimensions, flood-fill zero-valued cells using the
four cardinal neighbors and assert the reviewed floor hash before inspection.
Native visual/collision acceptance remains separate.

The installed navigation files have these hashes:

| File | SHA-256 |
| --- | --- |
| ini/MapDestination.dat | 60e0fff60469fb3b3907b0a9cdb010572d08121444ec7551fae6d9cb99f6af96 |
| ini/MapRelationInfo.ini | 755a7a00914182cb36eefda03937005d1ccd8e199ccc89a1978f72c2fe436ac3 |
| ini/GameMapEx.ini | 64ce044b2eb7ad15e980d91a5834f19d5ccc5dee2d3022cacb72e7a99833af7f |

A bounded text search in those three files found no1363, Asura, Gloomy,
Death Cave or Flame Plain reference. This is only a text-search boundary, not
proof about binary/encoded data or the whole client. Fresh exact-name searches
restricted to official co.99.com/co.91.com guides supplied no usable admission
contract. No unrelated search results were adopted as evidence.

An owner decision is pending between a documented Revora hunting quest with
ordinary NPC entry/enemies/reward/exit, and retaining the lock pending original
rules. No answer is assumed. No admission, achievement, population or release
checkbox is closed by this topology review.
