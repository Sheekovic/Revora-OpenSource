# Daily quest reward correction

Final owner clarification, 2026-09-21: remove direct P7 rewards from daily quests
only. Keep bound P7 rewards in one-time quests, events and both Magnolia custom
packs. This supersedes the temporary strict events-only interpretation.

Daily Hunt, Rare Materials, Soul Ball, Release Souls, Magnolia's base harvest,
Exorcism and Ancestor Worship give no direct P7 at completion. Their configured
currency, EXP, Chi, study, blessing and token rewards remain. Saved obsolete
valuable_reward flags cannot re-enable the removed direct daily P7.

Follow-up audit: Elimination is also a daily quest and was missed by the original
correction. Its pending claims now ignore the old P7 flag. New contracts instead
award a bound level-1 Tortoise Breaker rune after the complete rescue/cure/den
journey. Existing completed receipts/items are retained. The global release gate
stays false; this correction does not enable the staged quest.

Follow-up correction: Sky Pass uses a daily claim receipt and Snake Islands
saves daily hunts. Both now ignore old P7 flags and award new contracts one
bound level-1 Tortoise Breaker rune beside their existing Meteors. Existing
contracts retain Meteor-only rewards. Build, 45 Sky and 30 Snake checks pass,
including full routes, old flags, exact rune receipt/binding reload and replay.
Native/deployment remains pending. The original seven-service correction was
not an exhaustive daily audit.

The [P7 source follow-up](P7_REWARD_SOURCE_AUDIT.md) records why Moon Box,
Labyrinth exchanges, Flame Lit and Path Hunt retain their non-daily/event
rewards, along with the remaining search and valuable-reward coverage limits.

Daily Exorcism now adds one bound level-1 Tortoise Breaker rune to each new
contract at every difficulty, alongside its existing Chi Token, one-to-three
Exorcism Tokens, currency, study and EXP. `rune_reward` is saved separately
from the obsolete P7 flag; existing/imported contracts default false. Difficulty
changes and paid completion retain the saved terms. The rune UID is included
in `reward_uids` within the existing all-or-nothing reward transaction.
New claims require one additional inventory slot. Token-pack contents and their
existing P7 exclusions are unchanged. Native acceptance/deployment remain pending.

Magnolia now adds one bound level-1 Tortoise Breaker rune to each new daily
harvest contract, including a seed first presented directly at the garden.
The existing per-character Pacific-day claim limit applies. A saved `rune_reward`
flag defaults false on existing rows; old and imported completed contracts are
not retroactively upgraded. `rune_uid` records the exact item alongside the
Chi Token, seed consumption, purse, study/EXP and bonus receipt in one transaction.
Capacity reserves the rune slot and possible bonus before rolling the bonus.

The 20% bonus chance for Refined-or-better seeds, grade pools and custom pack
contents are unchanged. The guaranteed base reward has no P7; the explicitly
approved custom packs still contain bound P7 + 5,000 CPs + 1,000 RVPs.

Valuable non-P7 additions: new Demon Exterminators contracts now save one bound
level-1 yellow rune alongside their five EXP Balls; new Ancestor Worship claims
add a bound level-1 Tortoise Breaker rune. Existing completed receipts are not
reopened, and existing Demon contracts retain their original rewards. These
are Revora economy choices; native acceptance and deployment remain pending.

Daily Hunt and Rare Materials now also add a bound level-1 Tortoise Breaker rune
(4030101) to new contracts. A separate saved `rune_reward` flag freezes this
addition; existing rows and imported legacy assignments default false. Rerolls,
Rare Materials star changes and reconnects preserve the flag. Earned turn-in and
the existing paid completion honor the same saved reward terms. Old P7 flags
remain ignored. No completed receipt is reopened.

The rune insert and its `rune_uid` receipt commit together with the Chi Token,
offering/list debits, currency, study and EXP effects. Capacity counts two gifts
for a new contract after fully consumed items are removed; partial stacks still
occupy a slot. Old contracts retain the original one-gift capacity. Ordinary
eligibility, fees and rune equipment requirements are unchanged.

Soul Ball and Release Souls follow the same new-contract rune policy. Their
independent `rune_reward` flags default false for existing rows. Imported legacy
Soul Ball beads also keep their original rewards. Soul Ball's next newly issued
cycle gets the new terms; replacing a lost bead preserves the existing cycle.
The grant is a bound level-1 Tortoise Breaker rune, with no direct P7.

Soul Ball records `rune_uid` with its Chi Token, blessing, study, purse and EXP
transaction. New earned claims need one empty slot alongside the consumed bead;
new paid claims with no bead need two empty slots. Release Souls records the
rune UID in its existing reward array and requires three empty slots for new
contracts (EXP Ball, Chi Token and rune). Its paid shortcut still grants no
earned quest achievement. Both services preserve completed receipts and old
contract capacity. Native acceptance/deployment remain pending.

Soul batch validation: isolated build, 523 Release Souls and 628 Soul Ball
checks passed, including shared prerequisites. Checks cover earned routes,
paid completion with/without a bead, suppressed rune inserts, exact bound
item/receipt reload, old contracts, full bags, replay and purse/progression
rollback. Logs: `obj/chi-refill-validation/release-souls-rune-tests.log` and
`obj/chi-refill-validation/soul-ball-rune-tests.log` (ignored). No production
schema, installed client or live process was changed.

Rune batch validation: isolated build, 581 Daily Hunt checks and 590 Rare
Materials checks passed (each includes shared prerequisites; do not sum them
as unique tests). Coverage includes ordinary earning/reload, saved bound rune
UIDs, paid completion, suppressed rune inserts, offering/purse rollback,
two-gift capacity, replay and old contracts. Logs are ignored under
`obj/chi-refill-validation/daily-rune-price-tests.log` and
`obj/chi-refill-validation/daily-rune-rare-tests.log`. An initial capacity fixture
retried without reopening the offer; it was corrected to follow the NPC flow.
No server rebuild was needed for that fixture-only correction. Native/live
acceptance remains pending.

Magnolia's 20% bonus chance is unchanged. Custom packs 721269 and 720957 retain
one random bound P7 soul plus 5,000 CPs and 1,000 RVPs, regardless of the source
pack's binding. Their complete item/currency/source/receipt transaction, inventory
capacity check, overflow rejection, rollback and reconnect behavior are retained.
Historical currency-only opening receipts remain readable and are not reopened
for retroactive gifts. Already-issued items are unchanged.

Soul Trial, Courier Loop, Path Hunt, epic and rebirth quests keep their existing
reward contracts. The experimental starter cosmetic/rune replacement was reverted
before any commit, push or deployment. Its private journal v3 is obsolete; retain
the prior reviewed starter journal v2. Do not install the currency-only Magnolia
candidate either. The current Magnolia tooltip is the previously staged bound-P7
overlay at `obj/chi-refill-validation/magnolia-bound-p7-overlay` (unsigned and
uninstalled), expected SHA-256
`ed2936c1f1a83aea39093a58f1b6f7f04fa277b665e982a13a4bf5f69bd3f190`.

Prior direct-daily validation: build; Worship 515, Daily Hunt 556, Rare Materials
568, Soul Ball 617, Release Souls 508 and Exorcism 628 checks pass. Quiz's 36 checks
retain bound event P7. Counts include shared prerequisites and must not be summed
as independent tests. Exorcism token-pack catalog guards restrict souls to P3-P4
standard/P3-P6 golden, preventing an indirect P7 there.

Restored custom-pack validation: isolated build, 862 Magnolia checks, 1,388
quest-gift checks and five tooltip tests pass. Coverage includes earning both
pack types, binding, concurrent openings, failed transactions, capacity, overflow,
fresh reload and historical currency-only receipt preservation. The existing
bound-P7 tooltip matches the restored generator byte-for-byte. The achievement
producer audit remains 240/337. No live database, installed client or running
server was changed. The wider roadmap and native acceptance remain open.
