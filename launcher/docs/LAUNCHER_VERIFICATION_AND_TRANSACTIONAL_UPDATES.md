# Launcher verification and transactional updates

The current integrity and update policy is defined in [LAUNCHER_CRITICAL_INTEGRITY_AND_REPAIR.md](LAUNCHER_CRITICAL_INTEGRITY_AND_REPAIR.md).

The earlier receipt-based policy that automatically hashed the complete client after first use, receipt loss, or ordinary patches was retired by owner decision. Legacy verification receipts may remain as repair history but are ignored for launch authorization.

The retained security properties are signed update indexes and packages, deterministic patch chains, bounded resumable downloads, safe private staging, path/reparse/link/collision rejection, rollback journals, atomic `RevoraVersion.ini` updates, and post-install verification of affected files plus the CriticalLaunchSet.
