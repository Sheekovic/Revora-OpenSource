# Manual Playable VPS deployment — 2026-08-29

Scope: a private, owner-source-restricted genuine patch-5165 Account/Game evidence service. Public website, Admin routing, nginx, DNS, AMP, ADS, and PostgreSQL listener settings were not changed.

## Deployed topology

- systemd unit: `revora-game.service` (installed but deliberately not enabled across reboot during the temporary owner test)
- identity: non-login `revora-game`; peer database login `revora-game`, member of non-login role `revora_game`
- application: `/srv/revora-game/app/Revora.Host`
- mutable paths: `/srv/revora-game/data` and `/srv/revora-game/logs`
- Account: TCP `9958`; Game: TCP `9959`
- firewall: one exact owner `/32` accept per port, followed by an explicit drop per port
- PostgreSQL: Unix peer socket; TCP remains localhost-only

Operational configuration contains the reviewed public and owner addresses and is deliberately excluded from Git. The public Game IPv4 passed the exact-client 1055 endpoint emulator before deployment.

## Artifact and protocol boundary

- Host SHA-256: `221a9313b41b95688858f19e67149f2fbeab36d4b196a37429b62ef2d0963d79`
- Windows package SHA-256: `9635b2b1199b8e46ac2c305c6c5bf99204aaf3a856bcf640902c795e88fdd64c`
- approved configuration-only client SHA-256: `902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f`
- signed Server.dat SHA-256: `e0e9e75387b8fd8b1ac9d8505ce7f71fca8d7cfb4f570736c95232ec664e51c2`

The server validates genuine 276/1086, authenticates using zeroable password bytes, sends 32/1055, retains the unchanged M2-H1 DH/Blowfish candidate, validates 28/1052 plus `TQClient`, atomically consumes the digest-only handoff, and sends encrypted packet 1004/2101 with `NEW_ROLE`. The last step is evidence-supported by two independent compatible sources; genuine Windows runtime acceptance remains the owner-test gate.

## Start correction and rollback evidence

The first systemd start failed before READY. The service reached its three-start limit and opened no listener. A narrow rollback stopped only this unit, removed its four temporary firewall rules, restored the two pre-existing game-port firewall accepts, and verified website, Admin, panel, and legacy AMP URLs at HTTP 200.

The root causes were corrected before the second exposure attempt:

1. mutable data/log paths were made explicit outside the immutable app directory; and
2. `MemoryDenyWriteExecute` was removed because the .NET JIT requires executable anonymous memory.

The corrected unit emitted one READY, survived a complete graceful systemd restart with STOPPED then READY, runs with zero capabilities, and has no automatic restart count. A connection from a non-owner source timed out and incremented only the explicit drop counters.

## Rollback

1. `systemctl stop revora-game.service`
2. remove only the four rules whose comments start with `Revora owner test`
3. restore the two exact prior AMP-created game-port accepts from `/root/nginx-cutover-backups` is **not** applicable; use the narrow firewall state captured in `/root/revora-game-backups/20260829T110100Z`
4. verify no listener on `9958` or `9959`
5. verify website, Admin, panel, and legacy AMP URLs

No VPS reboot, nginx reload, ADS restart, AMP modification, or broad firewall-policy change is part of this rollback.

## Character creation / first world entry revision

The completed baseline run was cleaned up before this revision: `revora-game.service` was stopped,
all four temporary owner-test rules were removed, the semantic firewall ruleset matched its narrow
pre-test backup, listeners `9958` and `9959` were absent, `vpsowner` was disabled, and no active
handoff remained. Website, Admin, panel, legacy AMP, nginx, and PostgreSQL remained healthy.

The run genuinely proved the complete Account and Game cryptographic path through `NEW_ROLE` and
the visible character-creation screen. The earlier five-second-close classification is superseded
by the ordered two-attempt evidence below.

Migration `V005__legacy_5165_character_state.sql` adds a unique numeric legacy UID and bounded
mesh, profession, level, and hair fields. A pre-migration protected backup
`revora-20260829T114606Z.dump` was restored into an isolated validation database; account,
character, and audit counts matched before that validation database was dropped. Migration 5 is
recoverable by restoring that protected backup while the Revora game service is stopped.

Migration `V006__exact_5165_creation_professions.sql` aligns the bounded creation-profession
constraint with the exact client's statically recovered base values (`10`, `20`, `40`, `50`,
and `100`); it removes the unsupported creation value `30`.

The replacement Host artifact is `Revora.Host-linux-x64.tar.gz`, SHA-256
`63c40240da3210b6e1410ecc3d29d5ad750334e054a8a6453f128b7ff72d656e`. The private Windows package
is 3,063,083 bytes, SHA-256 `3775079fa10d6e159fb343ce0d8f5ca26419396849e91e14ad612ae98a0261a6`.
It reuses the identical approved configuration-only client and signed real-server `Server.dat`.

For the next single owner run, reinsert only the same exact owner `/32` accepts for TCP 9958 and
9959 followed by their explicit drops. Immediately afterward, stop only `revora-game.service`,
remove those four commented rules, verify both listeners are absent, disable the throwaway account,
and confirm no unconsumed handoff remains. No default-policy, nginx, DNS, AMP, ADS, website, or
PostgreSQL-listener change is permitted.

### Prepared owner run state

- branch head used for deployment: `c3b27e03eaeab7e76f5f185faa482c9cbbb937b2`
- deployed Host executable SHA-256: `a797f3a5898a313378d53309a0b90a99e5e775b9f960a688f886583a861ccf12`
- deployed Admin executable SHA-256: `399a9ce648bc6e20697aee94d0048262962227103d0eee921f918ccd13d51aae`
- private package SHA-256: `3775079fa10d6e159fb343ce0d8f5ca26419396849e91e14ad612ae98a0261a6`
- rollback directory: `/root/revora-game-backups/20260829T115300Z-first-world`
- Windows CI: <https://github.com/Sheekovic/Revora/actions/runs/33251090539> (passed)

The replacement service emitted READY, completed a graceful STOPPED/READY restart, and retained
zero automatic restart count. Only the four temporary `Revora owner world` rules differ from the
captured firewall ruleset; removing those four lines produces an identical semantic rule list.
Website, Admin, panel, legacy AMP, nginx, PostgreSQL, and the independent Web service remained
active; their public HTTPS checks returned 200. PostgreSQL continued to listen only on loopback.
The authenticated Admin route is `/owner-downloads/character-first-world-entry`; unauthenticated
access redirects to login.

### Ordered two-attempt evidence and root cause

The owner completed two distinct attempts:

| Stage | Attempt 1 | Attempt 2 |
|---|---:|---:|
| Owner launcher PID | 28064 | 3796 |
| Account 1086 validated | 11:57:38.876 UTC | 12:00:49.427 UTC |
| Account 1055 sent | 11:57:39.175 UTC | 12:00:49.746 UTC |
| Game connection accepted | 11:57:39.361 UTC | 12:00:49.994 UTC |
| 1052 validated / handoff consumed | 11:57:40.951 UTC | 12:00:51.588 UTC |
| NEW_ROLE sent | 11:57:40.962 UTC | 12:00:51.592 UTC |
| Connection stopped | 11:57:55.520 UTC | 12:01:12.306 UTC |
| Safe category | protocol-validation | protocol-validation |

Both session rows closed as `connection-ended`. A read-only database inspection before cleanup
found no case-insensitive `SHEEKO` row and no character owned by `vpsowner`; neither registration
reached a database transaction. Attempt 2 correctly received `NEW_ROLE` because the account still
had zero characters.

The first root cause was Revora's pre-persistence treatment of packet-1001 bytes 36–51 as a
required second password. Exact-client construction permits the third fixed field to be empty,
while pinned Comet skips those bytes as reserved. That first correction accepted and promptly
zeroed the sensitive reserved field, but retained a username match against the separate Account
field; the corrected-run evidence below disproved that remaining assumption.

After evidence capture, the service was stopped, both owner-scoped allow/drop rule pairs were
removed, `vpsowner` was disabled, and all its handoffs were confirmed consumed or revoked. No
process remained listening on 9958 or 9959. Website, Admin, AMP panel, nginx, PostgreSQL, and
unrelated firewall policy remained healthy and unchanged.

### Corrected-run follow-up evidence

The owner completed two additional attempts against commit `fede23d402ca5ddfe4505c9ce40ac9d452df1d04`.
The launcher PIDs were 1188 and 27636. Each completed Account 1086/1055, Game DH/Blowfish,
1052, and NEW_ROLE. Each then delivered a footer-valid 112-byte encrypted frame whose decrypted
header was exactly length 104 and packet ID 1001. Both stopped before
`CharacterRegistration1001Validated`.

The exact caller disassembly resolves the failure without another owner observation. It loads EBX
with literal address `0x68f370`, whose data is NUL, then pushes EBX as both Account and reserved
arguments at `0x49198e` and `0x491990`. Revora accepted the corrected reserved field but still
called its non-empty ASCII decoder for Account. That exact empty-field predicate threw before the
name, mesh, profession, Account word, or database branch ran.

Read-only inspection found no `SHEEKO`, no `SHEEKO2`, and no character owned by `vpsowner`.
No creation audit existed. The UID sequence remained `last_value=1, is_called=false`, proving no
allocation. Both sessions closed as `connection-ended` after server-side bounded protocol
validation.

Cleanup then stopped and disabled the service, removed only the four corrected-run rules, disabled
the throwaway account, and confirmed no open handoff or 9958/9959 listener. Website, Admin, panel,
nginx, PostgreSQL, and AMP remained healthy.

The final correction requires the exact fully zeroed Account field, matching consumed-handoff
Account word, and bounded name/mesh/profession values. A first-party emulator independently
transcribed from the exact client builder passes the complete production Account/Game path:
registration commits exactly one character, a case-insensitive repeated Create is rejected before
another UID is allocated, reconnect does not send NEW_ROLE, and `1006`, exact-build action `74/10010`, and
`1110` keep the character connected in starter map 1002. The disposable PostgreSQL database,
role, system user, and test files were removed after the 13/13 Playable suite.

The corrected stopped-service artifact is `artifacts/character-world-account-field-fix/Revora.Host-linux-x64.tar.gz`,
32,833,338 bytes, SHA-256 `8f32313cab729e6c64f3a2441436acd63cd0969c61392dc88bde2f5f48146d0f`;
its executable SHA-256 is `772effb8685057a31d31a40ea41b2e97840b3af13fcc8a6e65f55cedc0034317`.
It was reproduced byte-for-byte from two independent publishes and passed ClamAV. It was not
installed or started during cleanup, and migration V006 was not applied to Production.
