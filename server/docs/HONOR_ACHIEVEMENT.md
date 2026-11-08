# First earned Honor achievement

Achievement 10320 identifies receiving Honor for the first time in the installed
hash-bound Achievement.dat catalog (SHA-256
 df78dd498353b0b089f9b4228cada5d79f4547aae7b705349816671b2c29ebfc).

Solo and team daily Arena payouts, custom Arena prizes and positive daily ranking
payouts now record this achievement in the same transaction as their Honor
mirrors, items/currencies where applicable and paid receipts. An entitlement,
rank snapshot, overflow refusal or failed payment does not grant it. A zero-Honor
ranking payout does not qualify. Administrative balance changes are not earning
producers. No arbitrary current/historical balance is treated as proof.

The shared unique character/achievement key records one completion across repeated
payments and reward families. Sources retain the first committed reward's day and
milestone (ranking uses day). Postcommit refresh invalidates the achievement cache
and reconciles ordinary progression. Native notification rendering is not verified.

The ordinary-player fixtures produce owned solo/team results and frozen ranking
or custom entitlements, then deliver through the production payout services. They
exercise concurrency, full bags, ineligible recipients and suppressed persistence
writes. Suppressing the first achievement insert rolls back the reward instead
of leaving a paid reward without achievement evidence. Fresh Achievement objects
restore the bit through the production evidence loader; this is not a full client
reconnect or native match acceptance claim.

Already-paid historical receipts are not backfilled in this change. Legacy Elite
PK and other unconverted in-memory Honor writers remain separate work: do not
claim universal first-Honor coverage from these four payment paths. Existing
custom-prize eligibility policies remain unchanged.

Source changes only; no deployment, live database mutation or native acceptance.

Validation (2026-09-25): isolated Debug/x86 build, 1,161 Arena daily/prerequisite
checks and five achievement inventory tests passed. Existing unrelated owner
edits were present during the build; it is not a clean release artifact.
