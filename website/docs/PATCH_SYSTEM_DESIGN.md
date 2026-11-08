# Patch System Design

Revora's future patch service distributes only artifacts whose provenance and redistribution rights are approved.

An immutable canonical manifest contains a schema version, channel (`stable` or `beta`), release version, minimum launcher version, creation time, optional localized maintenance message, and files. Each file has a normalized relative destination, nonzero byte size, lowercase SHA-256 and HTTPS URL. Paths reject rooted paths, empty/dot segments, backslashes, control characters and traversal. Duplicate destinations are invalid.

The canonical manifest bytes are signed offline. The private signing key never exists in Git, AMP, the website, logs, public configuration or the production database. Launchers embed only a pinned verification public key and reject unknown algorithms/key identifiers, invalid signatures, unsupported schema versions and expired policy.

Downloads stage outside the live client tree, resume only with validators/ranges, verify size and SHA-256, then replace atomically. Repair mode hashes installed files against the signed manifest. Rollback retains the last verified version and never follows manifest-provided commands: manifests are declarative and cannot execute programs, scripts or arguments.

No proprietary client is hosted now. The repository may contain a safe example manifest whose URLs are reserved documentation examples and whose files are non-proprietary placeholders.
