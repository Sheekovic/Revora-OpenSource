# Session registration ownership during disconnect

The actual GameState.ShutDown path now elects one final-save owner with an atomic
per-session guard. Its early GamePool removal and final GamePool/DisconnectPool
removal compare both UID and the exact GameState object. A delayed or repeated
old-session callback cannot remove a replacement session or its disconnect barrier.
The early release retains this session's DisconnectPool entry until final cleanup,
preserving the existing login check in GeneralData.

This is a prerequisite for deferred Arena departure completion, not that complete
migration. Current Disconnect still unregisters timers/closes the transport before
ShutDown, and the legacy Arena End call still occurs after the early GamePool
release. Durable result completion must move ahead of final saves/session release,
with an explicit retry owner for Busy and recovery for failed captured outcomes.
The new once-only shutdown guard must be entered only when that future deferred
path is ready to perform final teardown; a Busy return must not consume it.

The combined Arena fixture tests concurrent election, early release retaining the
login barrier, late old cleanup with a replacement already registered, repeated
old callbacks, and independent replacement shutdown/release. It calls the same
production ownership methods but does not run the entire database-heavy ShutDown
body or claim actual socket reconnect acceptance. No live deployment/restart was
performed. See QUEST_EVENT_STATUS.md for passing build/test totals. Ignored log:
obj/chi-refill-validation/arena-session-release-tests.log.
