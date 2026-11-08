# Atomic sign-in issuance

2026-09-30. Private implementation and earning validation; not deployed.

`MsgSignIn` now commits payment, daily state, chance consumption, all due packs
and a unique character/month/day receipt in one PostgreSQL transaction. The
character inventory lock covers execution and post-commit installation; an
entity-row lock serializes durable owner state. A changed purse or inventory
fences the session for reload. A failed/uncertain result never permits a normal
save of speculative state. Nullable legacy reward/chance columns import as zero.

Normal sign-in preserves level 140, second rebirth, VIP 5 and 4,000 RVP terms.
Late sign-in preserves VIP 7, an available chance and 10,000 RVP; it fills the
first unsigned day up to today, including today as the legacy handler did. Late
requests can deliberately buy different days; the receipt protects each day,
not a nonexistent client request nonce. Failed payment, full bags and no unsigned
day consume no chance. Existing pack contents and prices are unchanged. The
normal/milestone packs stay unbound with their two-day expiration.

`revora_sign_in_state` owns the full year/month, 31 signed-day bits, last reward
and remaining chances. Legacy fields are synchronized for the existing client
and save path, but cannot overwrite that ledger. The next month starts empty
with ten late chances under the same owner transaction. Earlier calendar months
are refused. Startup's old reset only touches characters without a ledger; first
migration still depends on the existing legacy month marker, which contains no
year. No historical year is invented for unversioned data.

The existing packet serializer defines the private fixture input. Length,
identity, declared size, seal and action are bounded before mutations. This is
synthetic-handler validation; native client framing and display remain pending.

## Reproduce and evidence

Run `tools/Verify-ArenaProcess.ps1 -SignInOnly` against the existing isolated
candidate. The driver stages the hash-verified local sign-in catalog into that
fixed private package and refuses to overwrite a different catalog. The source
is `bin/Debug/signin.ini`, SHA-256
`6A70EE5CB0A84B41821523490012086EC2448348F8B86757F6B78ADC15F49341`.
No catalog or live service is published by the stage helper.

The completed earning, reload and verification phases used executable SHA-256
`99E6736D25B8DE087E95DD8562CB8D26901ABBD884D872E0D57439C809C948D5`:

- One non-GM character earns 50,000 RVP from the daily boss reward path, signs
  seven days through `MsgSignIn.Handle` and has 22,000 RVP remaining. Eight
  concurrent normal requests each day produce one debit/receipt and one set of
  due packs. Eligibility, lethal combat and the local calendar are controlled.
- The actually issued seven-day pack opens into an Easter pack through the
  existing owned consumer. A controlled Easter choice yields permanent unbound
  mount 200481; item use replay produces no duplicate, and normal wardrobe
  deposit persists the mount. No pack/reward rows are seeded for this character.
- Separate supplied fault characters check a 39-slot bag when two gifts are due,
  successful retry after freeing a slot, insufficient late-sign funds without
  chance loss, successful late payment, and a suppressed receipt insert rolling
  back the payment, packs and state while fencing the session.
- Cold game/database recovery matches the complete saved item, receipt and
  sign-in fingerprint. Same-day replay remains inert. Month rollover resets the
  ladder/chances, charges once for the new day and survives another game process.
- Malformed short/oversized, declared-length, identity, seal and unknown-action
  requests leave an otherwise eligible next day unchanged. A valid request then
  succeeds. Moving the clock back a month cannot restore another claim.

Build passed with existing legacy warnings; probes compile with warnings as
errors. An initial harness success-marker mismatch was fixed, and the existing
earned clone was resumed for recovery rather than reseeded. The extended final
request checks also passed against that same clone. Logs remain ignored under
`obj/signin-*` and the private candidate's `arena-process-sign-*.log`.

This establishes one earned Easter mount supplier, not all 30 random outputs,
the combined 50-mount collection, authenticated TCP behavior, native acceptance,
or production deployment. The private clock advances days/months; pack expiry is
not a claim that real elapsed time was exercised. The 50-mount journey still
needs the shop, pack, Planet and sufficient distinct Easter outputs together.
