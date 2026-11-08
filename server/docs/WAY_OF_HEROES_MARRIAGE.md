# Way of Heroes: marriage and Sweet Love Pack

Implemented and privately validated 2026-10-02; not deployed live.

Installed stage710 (task36, second rebirth level15 opening) asks the character to
marry. Its bound SweetLovePack3005134 was unusable. Qualification now requires
owned achievement10129 evidence from the existing transactional mutual-marriage
writer, source mutual-marriage and rule_version1. Each spouse qualifies separately.
A proposal or unsolicited acceptance does not qualify. Historical consent remains
qualifying; the milestone is once per character, not once per wedding. Legacy
spouse strings without the consent record are not backfilled as evidence.

## Owner-approved Revora reward

On 2026-10-02 the owner approved the proposed custom reward:

- One bound 99 Red Roses gift751099.
- One bound 99 Kisses gift755099.
- 5,000 regular CPs, with no RVP or silver addition.

The installed client description names a wedding ring, red rose and99 kisses,
but does not identify the ring. Targeted catalog/source research did not establish
its identity. This custom reward replaces that unresolved original definition;
it is not claimed to reproduce the original pack. No P7 soul is included.

QuestGiftPacks consumes the owned pack, inserts both gifts, saves the CP balance
and records the reward UIDs/currency in one transaction. The immediate CP update
uses ApplyCommittedConquerPoints. Capacity requires two result slots after source
consumption, and uint overflow refuses. Existing framing, ownership, trade,
expiry, inventory preimage and session checks remain in place. The result is
bound even when the source pack is unbound.

## Private process evidence

CheckCandidateMarriageClaim.cs uses two ordinary non-GM characters, supplying
level130 and second rebirth. The marriage request/accept uses the normal attack
handler; pack, claim and gift delivery use their normal packet paths.

- [x] Both spouses refuse710 before consent and while only a proposal is pending.
  Unsolicited acceptance leaves both unmarried.
- [x] Mutual request/accept writes reciprocal spouses and owned earning evidence.
- [x] Eight concurrent claims per spouse produce one bound Sweet Love Pack each.
- [x] Eight concurrent uses per earned pack produce exactly the two bound gifts
  and5,000 CPs; RVP and silver stay unchanged. Claim/use replay changes nothing.
- [x] The husband sends his99 Roses to the wife; the wife sends her99 Kisses to
  the husband. Each consumes its bound gift and records one99-unit delivery;
  replay does not deliver again. The opposite gift remains in each inventory.
- [x] A full private game/database restart preserves exact scoped spouses,
  earning evidence, claims, item/currency state, openings, flower balances and
  deliveries. Acceptance/claim replay after reload leaves the snapshots unchanged.

Game SHA256:
FC269936980799A974DC0D2DB6F02CEAAA83EE86133039B2C0E4A96A0D74D56F.
The build completed with existing warnings using the committed-source overlay
that excludes unrelated owner WIP. Earning and cold reload passed on the first
execution. No new database-fault or capacity/overflow process fixture is claimed.

## Remaining release gates

- [ ] Update the client Sweet Love Pack description to the approved custom contents
  in a reviewed patch; the old wedding-ring wording must not accompany release.
- [ ] Native-client reward, CP refresh and flower-display acceptance.
- [ ] Live deployment.
