# Secret-scan review

The initial full-history Gitleaks scan reported 12 findings. Each was reviewed
in context before an exception was added. No finding in this set was a reusable
service credential, real account password, signing private key or database secret.

| Findings | Reviewed material | Classification |
| --- | --- | --- |
| 1 | Launcher manifest-signing instructions, line 76 | Ordinary prose mentioning public-key fingerprints/key IDs |
| 6 | Career resource/expense, elimination and worktree-integration evidence JSON | 64-hex SHA-256 source, build and log digests |
| 1 | `server/tests/PotencyHunt/VoteGrantTests.cs`, line 24 | Deliberate repeating ABCDEF/0123456789 synthetic fixture |
| 3 | `AdminDataStore.cs` and `WebAccountStore.cs` | Parameterized SQL containing algorithm/column identifiers, without credential values |
| 1 | `launcher/manifests/master_manifest.json`, line 352533 | A 64-hex `sha256` file checksum matching a Square token prefix by coincidence |

The [Gitleaks ignore file](../.gitleaksignore) lists only the exact
commit/path/rule/line fingerprints from the initial snapshot. No whole file,
directory, secret-detection rule or future commit is excluded. All published
Git history is scanned with redaction. New findings continue to fail CI and
must be investigated; do not add broad exceptions to obtain a passing result.

Private mirrors, local runtime state, generated credentials, keys, captures and
logs are excluded from Git. Configuration fields named `SecretFile` and
`SecurityHmacKeyPath` in deployment examples refer to absent local files; they
contain no key/password values. The static website's configuration has public
URLs only and no connected account endpoint by default.

Automated checks are bounded and can miss secrets. If a real credential is ever
found in published history, revoke/rotate it and separately plan any necessary
history cleanup. Removing it only from the newest file is not remediation.
