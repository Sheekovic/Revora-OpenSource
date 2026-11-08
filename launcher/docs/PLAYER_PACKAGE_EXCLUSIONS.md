# Player package exclusions

Exclude generated build directories, source, research reports/tools, debugger
artifacts, dumps, captures, logs, PDBs, credentials, keys, tickets, private
configuration, backups, owner-canary installers, `LoginLoader.dll`,
`RevoraNativeLogin.dll`, `RevoraNativeLoginLoader.exe`, `RevoraBridge.dll`,
`RevoraBridgeLoader.exe`, and login-related or graphics-proxy `d3d9.dll` files.

Required TQ/NetDragon runtime libraries are retained despite legacy filenames.
Uncertain engine dependencies are retained until a bounded launch proves they
are unnecessary.
