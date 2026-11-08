# Starter quest journal correction

2026-09-21. A private, unsigned overlay is staged in
`obj/chi-refill-validation/starter-quest-journal-v2`. It has not been installed,
signed or published. Genuine 6711 navigation and rendering acceptance is pending.

The installed Courier mission 90002 incorrectly sent its Bandit objective to
Bird Island 1015. The server's accepted route is Phoenix Castle 1011 through
Cerys's passage. The correction links that objective to Cerys at Twin City
(319,303), then tells the player to hunt 20 Bandits in Phoenix Castle. This uses
the verified departure NPC and does not invent a Bandit spawn coordinate. The
Commander and Clerk objectives, quota/progress marker and mission identity stay
intact. Courier's reward line and Soul Trial mission 90003's reward line now
explicitly say **bound P7 soul**.

`tools/ClientPatches/prepare_starter_journal.py` reads hash-checked installed
journal, approved DX9 executable and prior quest bundle. It composes the existing
Monk and Warrior fixes before changing only five fields across the three starter
missions. Restoring those changed sections must reproduce every input byte.
The prior composition hash matches the previously staged Warrior artifact.
The preparer checks the installed inputs again after staging, retains original
journal bytes for future rollback and refuses differing existing output files.
A second preparation run is identical.

Validation: **17 synthetic journal tests** pass, comprising the seven new starter
checks and ten existing Warrior checks. They cover byte/line-ending preservation,
idempotence, route/reward content, missing or duplicate mission/field rejection,
ambiguous numeric or malformed identities, and agreement with server NPC/map,
quota and reward configuration defaults. The real hash-gated composition also
passes. No server behavior changed in this batch, so no server rebuild was needed.

[Derived manifest](research/STARTER_JOURNAL_REVIEW.json) records input/output
hashes, architecture, relative destination and unsigned/unpublished status. Only
tooling, synthetic fixtures, documentation and derived metadata belong in Git;
client journal copies remain ignored. This is not a signed patch manifest or a
release certificate.

Before release, review both mission entries on genuine 6711: link resolution,
text fit, progress markers, receipt-backed completion, item binding display and
coexistence with the staged Monk/Warrior journal changes. Deployment must match
the validated server, verify/back up the exact installed preimage and provide
transactional restoration. No installation or server restart was performed.

Version 2 also updates Path Hunt mission 90001's prize: newly accepted hunts earn
a bound P7 soul, while earlier accepted hunts retain their promise. Other Path
Hunt fields are byte-preserved. The previous private v1 artifact remains intact;
v2 is the composed review candidate. All 17 journal checks still pass.
