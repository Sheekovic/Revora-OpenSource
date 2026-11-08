# Summer Hunt midnight cleanup correction

## Defect and correction (2026-09-28)

Summer Hunt cleanup captured a Pacific quest day once, then removed active
entries whose day differed. If a new hunt was accepted after midnight while
cleanup's clock read was delayed, the old day could remove the new admission.
The saved hunt remained intact, but SummerHunt.Credit requires an active entry,
so subsequent kills could be ignored until a new acceptance/login restored it.

Cleanup now rechecks the canonical player, current active value and current day
under the same owner lock used by acceptance. Removal also compares the captured
key/value instead of removing by UID alone. Current-day admissions survive;
expired/invalid entries still clean up. Saved hunt terms, rewards and records are
unchanged.

## Focused evidence

`tools/Verify-ArenaProcess.ps1 -SummerCleanupOnly -ResetOwnedFixture` uses the
actual private game and an ordinary supplied level120 profile. Production NPC
acceptance creates hunts immediately before and after Pacific midnight. An
injected clock delay makes cleanup resume with yesterday's read after accepting
today's hunt. No hunt or kill counter is inserted by the fixture.

The first fixture attempt targeted the crafting cleanup's clock read, which was
too early to reproduce the hunt cleanup race. After targeting the hunt's day
read, old executable13F3B2ED failed the retained-active-admission assertion.
Corrected executable SHA256
`AB22DB1A204DC0987532815E801176F248D078AF89380579B54C6A1B8548E11B`
passes. The isolated build passed with existing legacy warnings, and the helper
compiles with warnings as errors.

The corrected run verifies ordinary cleanup retains the current hunt. A matching
startup-spawned target then dies through the production damage/death route and
records one kill. Advancing another quest day removes the expired active entry
while preserving both unclaimed saved hunts and the exact one-kill total.

The clock, level, proximity and lethal damage amount are fixture controls. This
is focused admission/credit/cleanup evidence, not the complete120-kill reward
journey, authenticated combat or native acceptance. The owned clone was removed
and private PostgreSQL stopped. No live deployment occurred.

The adjacent cleanup review found Labyrinth already rechecks map/eligibility
inside the owner lock and compares visit identity; Power Arena rereads the run
under its lock before expiry recovery. This bounded review is not a global
concurrency audit.

## Delayed final-kill correction (2026-09-28)

A second reproduction found the completion path could remove a newer day's
active admission. Credit captures the accepted day, updates that day's counter
transactionally, then previously removed the active cache entry by UID alone.
An ordinary next-day acceptance during the old credit transaction could therefore
be erased when yesterday's120th kill completed.

Completion now conditionally removes only the captured character/day pair.
Yesterday's completion and notification remain valid; today's admission is kept.
The change does not take an inventory lock from the kill callback or move its
existing transaction boundary.

The focused process fixture supplies119 prior kills on a second ordinary
character's NPC-issued hunt. A disposable-database trigger waits on a held
advisory lock before the final counter update. After verifying the database wait,
the fixture accepts the next day's hunt through the ordinary NPC, releases the
old callback and checks both saved counters and the active day. It supplies the
counter and calls the existing credit boundary directly; this is concurrency
evidence, not a claim of an earned120-kill journey.

Old executableAB22DB1A failed the retained-new-day assertion. Corrected SHA256
`B1ABF827707D5F8560DE9551CA597380FF4B1891E9ED6445A52F69D29BB1C3B2`
passes the expanded `-SummerCleanupOnly` suite: the prior midnight/normal-expiry
tests and real one-kill credit still pass, while the delayed prior-day counter
reaches120 and the newly accepted day remains active with zero kills. No new
reward is granted by this race fixture. The trigger/function are removed after
the callback finishes; the successful driver then removes the owned clone.

The isolated build passed with existing warnings; the helper compiled with
warnings as errors. Private PostgreSQL stopped successfully. Full hunt rewards,
socket/native acceptance and live deployment keep their separate gates.
