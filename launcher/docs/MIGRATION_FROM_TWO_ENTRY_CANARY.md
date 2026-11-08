# Migration from the two-entry canary

The two-entry flow remains preserved in Git as experimental evidence but is not
selected by the single-entry profile. The launcher credential panel,
`NativeLoginCanaryService`, ticket issuance/redemption, per-launch authorization
pipe, and ticket readiness gate become obsolete for this profile.

Server ticket tables/endpoints are not deleted in this milestone. AccountHost's
existing atomic digest authorization consumer and world grant remain reusable;
the native component calls the existing direct HTTPS authorization producer.
Removing deployed ticket routes or migration data is a later, separately
reviewed operational cleanup.
