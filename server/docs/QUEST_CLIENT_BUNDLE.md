# Private quest client review bundle

2026-09-16. `tools/stage_quest_client_bundle.py` composes the reviewed Summer,
classic Labyrinth, Dis City and Abyss content into one private directory:
`obj/chi-refill-validation/quest-client-bundle-v1/`.

This is three data files and an unsigned review manifest, not an installed
client, installer or public patch. Proprietary content stays ignored and is not
committed. The total data payload is 9,978,799 bytes. Existing original/staged
trees are retained; this does not create another full client or release tree.

| Destination | Reviewed source | Output SHA-256 |
| --- | --- | --- |
| `ini/itemtype.dat` | Final Dis City ranking overlay, retaining Summer and classic Labyrinth changes | `96f0596b854f2e3ff9df6007317c5272fb9d08329995c6dc3357d33addc86f1d` |
| `ini/Questinfo.ini` | Final Dis City ranking overlay | `1cf153510ca3767b49345192c57e852e44abd122f27fd36bc9f993c84028f310` |
| `ini/monster.dat` | Fifteen independent Abyss boss aliases | `df6431be254159ce83a812532b6f24330df799bfec66e1cfc2ba6436b5d6970a` |

The tool checks fixed active-client preimages, the genuine x86 DX9 executable
hash and every source output hash. It records the four source review manifests'
digests, writes only to a newly owned temporary directory, then rechecks all
inputs and outputs before publishing that directory to its private destination.
An existing destination is rejected and retained. Failure cleanup touches only
files this invocation created in its verified temporary directory.

All three copied outputs match their reviewed digests. All three installed
inputs retain their preimage hashes. An attempted repeat was rejected without
overwriting the bundle. The twelve existing Summer/Labyrinth/Dis City overlay
tests pass; [Abyss native loading and HP projection](ABYSS_TRIAL_RESTORATION.md)
were separately verified before composition. This step changes no server code
and requires no new server build.

Review manifest SHA-256:
`f1224ae8cb0ebefab8bcc8850a0d7451f800750859e435a010b097136ac3f7f9`.

## Remaining in-game acceptance

The current process observation found the live `bin/Debug/Revora.exe` and no
running `Conquer.exe`. The validation workspace contains data overlays and
offline native readers, not a configured full isolated client/server session.
Neither the live instance nor the installed client was altered to manufacture
an acceptance result.

A full isolated ordinary-player session still needs to validate the combined
item descriptions and quest traces; private Dis City admission, combat and
rewards; and Abyss journal/Cup switching, model rendering, all fifteen boss
bars through damage, keeper navigation, death/reentry and earned rebirth.
Multiplayer event and wardrobe visual checks in the main roadmap remain open.
Native DLL profile lookup and synthetic server movement do not prove those
rendered interactions.

Before a separately authorized installation or publication, compose any further
required content, preserve exact installed preimages, provide transactional
rollback, complete the applicable client acceptance and signing/release gates,
and only then enable production quest initialization. This unsigned review
manifest is not a launcher patch manifest and has no automatic update action.
