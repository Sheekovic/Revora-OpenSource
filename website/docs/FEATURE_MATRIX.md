# Legacy Feature Matrix and Revora Design

Legend: **S** = small/skeleton, **B** = broad implementation, **D** = documentation only, **—** = not relevant/observed. “Broad” describes surface area, not correctness. Comet is inspected at 5065/5187; COPS at 5066; Redux claims 5065; Heroes claims 5165; Dragon is a client injector/redirector, not a server.

| Feature | Comet | COPS docs | Redux | Heroes | Dragon | Clean Revora recommendation |
|---|---|---|---|---|---|---|
| Accounts | S: repositories, salted legacy hash, realms | — | B: DB account, plaintext comparison; hidden missing-account GM creation in duplicate LoginServer source | B: plaintext/recoverable comparison, hardcoded privileged accounts | — | Account aggregate; memory-hard hash; explicit registration use case; deny-by-default roles |
| Characters | S: one character, creation and base info | D: 1001/1006 | B: persistence, stats, creation | B: very large mutable character class | — | Aggregate with transactional unique name and versioned persistence |
| Maps/portals | S: entry action only | D: action/map fields | B: DB maps, TinyMap collision, portals/passages | B: DMap/resources, portals, teleports | — | Immutable map data + owned zone actor; validated transitions |
| Movement | packet skeleton | D: 1005 | B: walk/jump and collision hooks; trust/race concerns | B: claimed 10005; mode-9 double-walk defect | — | **Implemented first slice:** exact-client 20/10005 walking and genuine-tail 32/10010 action-137 jumping, action-108 semantic correction, DMap collision/altitude checks, UID/rate validation, PostgreSQL optimistic persistence, nearby broadcast |
| Player visibility | minimal login client registry | D: 1014 | B: screen queries over concurrent map objects | B: global hashtables and repeated full scans | — | Spatial index owned by zone; deterministic enter/stay/leave deltas |
| Inventory | minimal item packet | D | B | B | — | Inventory aggregate with capacity/invariant tests and transactional mutations |
| Equipment/item attributes | minimal | D | B: item additions, sockets, compose, durability | B: large packet/item code | — | Data-driven definitions; value objects; one calculation service |
| NPCs/shops | — | D | B: many per-NPC classes, shops | B: central dialogue handlers, shops | — | Declarative dialogue/use-case registry; no reflection from untrusted names |
| Warehouses | — | D | B | B | — | Separate container aggregate with authorization and atomic transfer |
| Monsters/spawning | — | D | B: spawn managers/threads | B: file/global-table spawns | — | Zone-owned spawn schedules; deterministic clock and bounded work |
| Combat calculations | — | partial message docs | B: combat manager/thread | B: duplicated, monolithic attack logic | — | Pure domain calculators with golden tests; authoritative command pipeline |
| Skills/experience | — | D | B: repositories/managers | B: large Skills class | — | Versioned data definitions + pure progression rules |
| Drops | — | D | B: drop rules | B: global rates/hard-coded paths | — | Transactional loot generation with injectable RNG and audit events |
| Quests | — | D | partial task/NPC scripts | event/NPC-specific logic | — | Explicit quest state machines; no arbitrary script execution initially |
| Teams | — | D | B | B | — | Team aggregate with serialized membership changes |
| Friends/enemies | — | D | B | B | — | Relationship service with symmetric transaction rules and limits |
| Guilds/clans | — | D | B guilds; clan coverage unclear | B guilds; clan semantics unclear | — | Separate aggregates, role capabilities, optimistic concurrency |
| Trading | — | D | B | B | — | Two-party escrow state machine; atomic commit/rollback |
| Vending | — | D | B player shops | B market shops | — | Listing/escrow aggregate; immutable quoted price/currency |
| Chat | minimal talk | D | B channels and DB logging; GM commands share chat path | B; commands/privileges mixed into gameplay | — | Bounded channels, sanitization, rate limits; admin console fully separate |
| Moderation | — | — | dangerous command path and permission enum | hardcoded names/GM states | — | Capability authorization, audited actions, no hidden identities |
| Persistence | EF-style repositories/MySQL; skeleton | — | NHibernate + broad catch/console logging | custom concatenated SQL, global connection | — | Parameterized provider adapter, migrations reviewed separately, unit of work |
| Scheduled events | — | — | B: guild war/timed threads | B: many hard-coded day/time checks | — | Scheduler emits idempotent commands; persisted leases; test clock |
| Concurrency | static dictionaries, unbounded channel/RPC retry loop | — | concurrent dictionaries mixed with locks, timer threads, APM sockets | global Hashtables, many polling threads, shared Random | — | Explicit owner per connection/session/zone; bounded channels and cancellation |
| Shutdown/recovery | host skeleton, limited RPC recovery | — | no coordinated bounded drain observed | infinite console/thread loops; broad catches | — | Existing `Revora.Host` drain/flush/STOPPED contract remains authoritative |
| Client redirection | — | — | README recommends third-party loader/AV workaround | separate client repo referenced; untrusted | BSD injector hooks connect calls | **Do not adopt injection.** Future signed launcher uses documented configuration/patching only after M4 |

## Features worth modeling, not copying

- Redux demonstrates the breadth and dependency relationships needed for a playable classic world: maps → visibility → movement → combat → inventory/persistence.
- COPS makes padding and fixed-layout ambiguity explicit and is a strong template for independently written protocol documentation.
- Comet's Account/Game separation and packet-per-type organization align most closely with Revora, but Revora retains stronger lifecycle, ownership, and backpressure rules.
- Heroes supplies exact-5165 hypotheses and feature names, not trustworthy implementations.
- Dragon confirms that injection-based redirectors create elevation, antivirus, and maintainability costs; Revora rejects that path.
