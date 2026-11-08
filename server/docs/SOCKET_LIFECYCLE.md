# Managed socket shutdown correction

## Scope (2026-09-28)

Private roadmap validation intermittently failed during the initial game login.
The most recent captured failure reported ConnectionAborted after packet1006,
32ms into the location request. Its root cause remains unproven.

Review found an independent ownership defect in
`Network/ServerSocket/ClientWrapper.cs`: Disconnect called native shutdown and
closesocket on Socket.Handle, then Socket.Dispose on the same managed owner.
The managed socket was never told its native handle had already been closed.
This creates a double-close/reused-handle risk during concurrent connections.
No trace yet proves this sequence caused the captured login failure.

Disconnect now calls managed Socket.Shutdown and always Socket.Dispose in a
finally block. Shutdown of an already disconnected/unconnected or disposed
socket is tolerated. Native close imports were removed; no repository callers
outside this class used them. Existing disconnect locking, queue-drain policy
and timer disposal remain unchanged. No wire format changed.

## Validation

The committed-source overlay build passed with existing legacy warnings while
preserving unrelated owner work. Private executable SHA256:
`6CF84170A95EF52B68170194D25CE6EA613AA5DD6A6B2F90DFF7ED3D45019E4B`.

`tests/SocketLifecycle/Verify-Disconnect.ps1` compiles with warnings as errors
and tests the actual built ClientWrapper. The fixture installs inert timer
subscriptions and an empty send queue; it does not start the game or background
connection timers. It verifies:

- No native close imports remain in the wrapper.
- Eight concurrent disconnect calls plus a repeated call dispose each timer once.
- Unconnected and already-disposed sockets finish cleanup without an exception.
- Each of64 loopback connections closes to EOF, and a subsequent independent
  connection can still transfer data.

`tools/Verify-ArenaProcess.ps1 -PerfectionWireOnly -ResetOwnedFixture` also
passed all three actual game phases on this build: authenticated pre-commit
rollback with session fencing, fresh authenticated purchase after game/database
restart, and exact purchased-state recovery after a second restart. The driver
removed its owned clone and stopped private PostgreSQL.

These tests validate the cleanup change and exercised roadmap path. They do
not cover saturated send queues, prove all connection races absent, or establish
that the historical login abort is resolved. Keep its bounded diagnostics and
open status until stronger evidence exists. Native acceptance, deployment and
live-server validation remain pending; this batch changed no live process.
