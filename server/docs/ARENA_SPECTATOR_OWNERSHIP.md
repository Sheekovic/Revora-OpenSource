# Solo Arena spectator ownership

Both production BeginWatching overloads now serialize on the fight lock and try
the spectator owner lock without waiting. Admission requires an active imported
fight, its owned map, a canonical living ordinary player and no existing solo or
team fight/watch ownership. Explicit fighter selection must identify one of the
two participants. Successful teleport to the exact dynamic map is required before
adding a watcher. A failed teleport clears the attempted reference and earns no
membership. Duplicate joins and competing solo fights cannot both own a watcher.

Leave captures one group and uses that same group/owner lock pair. It only returns
a canonical spectator still on the original map; someone who already left is not
teleported again. It detaches before sending the existing watcher-close packet.
Cheer serializes with join/leave/cleanup, requires actual spectator membership on
the fight map and a valid fighter, and counts each watcher once. Ended fights
reject new spectators and cheers. Packet layouts and the existing 1005 spectator
display base are unchanged; this is not new native-client protocol evidence.

Legacy Export now uses the shared owned cleanup, acquires all participant and
spectator locks without waiting, and reports success only once. CheckGroups calls
legacy Win only after a successful export. A busy owner retains the group for a
later tick; the previous one-day DoneStamp postponement is removed. This closes
the spectator-versus-map-disposal race in the existing solo path. It does not
activate the transactional coordinator or replace the legacy reward calculation.
Team Arena spectator lifecycle is not changed by this batch.

The disposable-schema Arena fixture covers failed teleport, invalid target, busy
owner, concurrent duplicate joins, concurrent leaves/cheers, late admission/cheer,
busy export retry, one successful export and competing fights for one spectator.
The cleanup spectator fixture now provides its required display map path and
asserts actual admission before testing return. The earlier cleanup-only check
was insufficient because it could pass without a successful spectator join.

See QUEST_EVENT_STATUS.md for the completed build/test total. Ignored log:
obj/chi-refill-validation/arena-spectator-tests.log. Native spectator UI acceptance,
disconnect/final-save coordination and full durable-result lifecycle activation
remain open. No live restart or deployment is included.
