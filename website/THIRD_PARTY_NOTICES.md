# Third-Party Notices

Revora is independently written. This file records third-party material actually incorporated or adapted under license; research-only repositories are not incorporated code.

## Static client extraction research tools

`hifi/iss_extract` at `01232146c9284a86958817817849f320e77f5fb7` was built from reviewed source under its ISC notice. `twogood/unshield` at `cbed3d19ef9cdf4e5fb86b9d65e48f1bf8d539db` was built from reviewed source under the MIT License. Neither tool nor its source is incorporated into Revora artifacts; both were used only on a `noexec,nosuid,nodev` forensic filesystem. Full provenance is in `docs/CLIENT_DOCTOR_TEST_2.md`.

## Comet

Repository: <https://github.com/conquer-online/comet>

Pinned reference commits:

- patch 5065: `cc9c788a27fbace85325955a67b96b4e8e43843e`
- patch 5187: `18446a8a54b06e9f2c0d69d76f7fec46254910ab`

Comet is licensed under the BSD 3-Clause license and identifies the copyright holder as “Spirited”. Source redistributions must retain the copyright notice, conditions, and disclaimer; binary redistributions must reproduce them in accompanying documentation/materials; the copyright holder/contributor names may not endorse derived products without permission.

Revora's M2-H1 Game transport uses an independently adapted Blowfish CFB64 implementation and the standard Blowfish constants from the pinned Comet 5065 reference. The following upstream notice therefore accompanies source and binary H1 materials:

> Spirited © All Rights Reserved
>
> Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
>
> 1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
> 2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
> 3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
>
> THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

No unlicensed research repository is listed as incorporated material.

## COPS protocol documentation

Repository: <https://github.com/conquer-online/cops-protocols-doc>

Pinned reference commit: `d976e24e02d5d351634fcf30640d8a78e3ece2de`.

The documentation repository applies CC0 1.0 to its contents. Revora currently uses it only as older-patch research evidence and has not copied its generated protocol tables into executable code. Its README states that the material is derived from reverse engineering and covers clients only through patch 5066, so it is never treated as direct proof of patch 5165.

## COPS v7 Emulator gameplay adaptation

Repository: <https://gitlab.com/conquer-online/servers/cops-v7-emulator>

Pinned revision: `84919e25e79eded39314ea50bfd787427eadd59f`

Pinned source archive SHA-256: `11b17a893109e7d4e851c5101e6bd0cabd561a01005fd7602054a70d36815052`.

Revora independently adapted the DMap cell/checksum/accessibility model, movement
direction and jump validation rules, 18-cell view concept, and entity broadcast
boundaries. It did not vendor the source tree or use COPS authentication,
persistence, networking, protocol IDs, launcher, scripts, or binaries.

The COPS license requires the acknowledgement: **This product includes software
developed by CptSky.** Its complete copyright, four redistribution/advertising
conditions, and disclaimer are retained in the upstream `LICENSE` at the pinned
revision and reproduced in `licenses/COPS-v7-LICENSE.txt` for source and binary
deployment documentation.

## Dragon

Repository: <https://github.com/conquer-online/dragon>

Pinned reference commit: `7594b699b08725daba78b60386a429da557b3a13`.

Dragon is BSD 3-Clause licensed, copyright 2022 Spirited. It was inspected only to understand launcher/redirection architecture. No Dragon code is incorporated, and Revora explicitly rejects its process-injection, elevation, and antivirus-exclusion model.

## Launcher research not incorporated

COFlashFixer (commit `a1a7c462e2d2f63384068f9320255d5514cfe82e`) and SmartConquerLoader (commit `42c6610d643e4926dd0ea5b6b8d8d427102ac1a2`) have MIT repository licenses, but contain injection designs and provenance/bundled-binary concerns. OpenConquerLoader (commit `068898b70722a61dd3539028f458e5379b3fe9ad`) has no repository-wide license and GPL-3.0 hook subtrees. Ruffle (commit `c246db7f857fe3e434cfb96d78d93f09a5a1fb15`) is dual MIT/Apache-2.0. These projects were research only; no source, binary, or asset was copied into Revora.

## CubeCoders AMP Templates

Repository: <https://github.com/CubeCoders/AMPTemplates>

The Revora private Generic Module template was written against the documented AMP format and inspected official examples. The upstream template repository is MIT licensed, copyright 2024 CubeCoders Limited. Revora does not redistribute AMP binaries or copy a vendor application template wholesale. Format provenance and the upstream license are documented here for clarity.

## Npgsql

Revora.Persistence uses Npgsql 10.0.3, the official open-source .NET data provider for PostgreSQL, from NuGet. Npgsql is licensed under the PostgreSQL License. Revora uses parameterized commands only and does not bundle PostgreSQL server binaries.

## Client Doctor and compatibility boundary

The Client Doctor, profile signer, and disabled compatibility boundary are independently written Revora code. Dragon remains a BSD-3-Clause architecture reference only; no Dragon source was copied. No Flash runtime, legacy loader, client executable, or proprietary client asset is incorporated.
