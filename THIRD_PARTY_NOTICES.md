# Third-party notices

The root MIT license covers Revora-authored material. Separately identified
third-party code and binary dependencies retain their original licenses and
copyright notices.

## Website references and adapted gameplay

Keep [`website/THIRD_PARTY_NOTICES.md`](website/THIRD_PARTY_NOTICES.md) and
[`website/licenses/COPS-v7-LICENSE.txt`](website/licenses/COPS-v7-LICENSE.txt)
with source and binary redistributions to which they apply.

**This product includes software developed by CptSky.**

The COPS notice includes its own redistribution and advertising conditions;
the root MIT license does not replace them.

## PostgreSQL provider

`server/lib/Npgsql.dll` is Npgsql 4.1.14 and retains the PostgreSQL-style license
in [`licenses/Npgsql-4.1.14-LICENSE.txt`](licenses/Npgsql-4.1.14-LICENSE.txt),
copied from the upstream `v4.1.14` source tag. The website and administration
application declare Npgsql 10.0.3 as a NuGet dependency. Their package terms
also remain in force.

## Microsoft .NET support libraries

The `Microsoft.Bcl.AsyncInterfaces` and `System.*` support assemblies under
`server/lib/` are Microsoft/.NET components. Retain the upstream
[.NET MIT license](licenses/dotnet-corefx-LICENSE.txt) and
[third-party notices](licenses/dotnet-corefx-THIRD-PARTY-NOTICES.txt), preserved
from the CoreFX `v3.1.32` source tag for this generation of support assemblies.
These are not relicensed as Revora-authored code.

## Other package and operator dependencies

NuGet dependencies declared in project files retain their package licenses.
The game-server project also references external assemblies not present in
the repository; see [the dependency inventory](docs/SERVER_BUILD.md).
Those assemblies are not offered for download or licensed by this repository.

The imported launcher's unused prebuilt `LauncherAssets/d3d9.dll` is excluded
from this public source snapshot. No proprietary Conquer client, runtime data,
private account database, packet capture, or memory dump is distributed here.

Names and trademarks identify interoperability targets and do not imply
endorsement or grant rights in third-party games or artwork.

## Client endpoint tool runtime

The portable ClientEndpoint release embeds Microsoft .NET and Windows Desktop
runtime components under their upstream terms. The package includes the
runtime packs' license/notice files and the complete WinForms third-party BSD
notice from [dotnet/winforms v10.0.14](https://github.com/dotnet/winforms/blob/v10.0.14/THIRD-PARTY-NOTICES.TXT),
preserved in [`licenses/dotnet-winforms-10.0.14-THIRD-PARTY-NOTICES.txt`](licenses/dotnet-winforms-10.0.14-THIRD-PARTY-NOTICES.txt).
Upstream Git blob: `d8d1743824a0ab5bdc7d45623ebee5e71cf59da1`.

