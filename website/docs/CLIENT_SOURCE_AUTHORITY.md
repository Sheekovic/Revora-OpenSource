# Client Source Authority Register

Date: 2026-08-28 UTC. None of these candidates is an authoritative Revora client yet.

| Candidate | Provenance | Evidence and decision |
|---|---|---|
| Cooldown/MEGA 5165 | Community preservation link | 318,091,319 bytes; SHA-256 `33ed8cadc18c78483d1eed0fad38b69dd6cdd721ff1b6e510310aad5ee76a212`. Rejected after three malware detections. Passive comparison only. |
| ModDB/GameWatcher 5118 | Community mirror with published MD5 | 491,811,761 bytes; MD5 `75a3c2fda261153c6c7a9c9b0ccda903`; SHA-256 `36a9db229e4a605c717f18bef4d25079c59427f8fb4ee1524be008bd82dfc78d`. Packed InstallShield payload is not fully scanned; research only. |
| TQArchive/Internet Archive 5156 | Preservation index attributes installer to TQ Digital; Internet Archive item uploader CptSky | 515,333,361 bytes; archived MD5/SHA-1 match. SHA-256 `d0536933cdefb785271c760f476fc8daa399e1f0b3462e5b41da76b72267a025`; SHA-512 `da8bfc0b7911e88844bd0c616b4756ac5ead4c4d74adc035e8c12eac15fb5c84153c816b81c8613992a66eaece4140d88f56724d8880798f40d0b83a7fb24d72`. InstallShield cannot be statically extracted by installed Linux tools; owner-side offline extraction is required. |
| TQArchive 5000–5999 patches | Preservation collection attributes patchers to TQ Digital | 1,392,709,743-byte RAR; archived MD5/SHA-1 match. SHA-256 `2addc5f071d472abd61eeec343ed0b541c69d5f7a79284d15ab10927278a5ca3`; SHA-512 `dffbd8d1eb3df60e30df9708bd2dc2204b05a3ae428bba5d42c31a760fd878d095edafc67cb5e99364e280c0af2e9b624845ea8b9a3801cac967c9e77ee0b0b6`. Contains 5157–5163 and 5165 patchers. RAR method v6 listing works, but both 7-Zip and unar failed payload extraction; no patcher was executed. |
| TrinityConquer 5165 | Current community server download claim | Independent 5165 candidate, but modified connection/client content and unclear executable provenance make it evidence-only. Not downloaded. |

TQArchive provides the strongest current reconstruction lead: preserved 5156 plus the complete historical patch sequence through 5165. It is still a preservation source rather than a cryptographically authenticated NetDragon origin. The official patch-5165 announcement corroborates the version/date, not these archive bytes.

The next source step is offline extraction in a disposable Windows VM. Extract the 5156 InstallShield installer and 5157–5165 patch payloads without launching the client, preserve before/after trees, hash every file, scan every stage with Defender and Malwarebytes, and return sanitized hash manifests. Executing the patchers is a later separately reviewed fallback only if payload extraction cannot be achieved.
