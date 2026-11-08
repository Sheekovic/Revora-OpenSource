# DX9 D3D9On12 Owner-Test A

This private configuration binds the launcher payload and DX9 critical profile
to proxy SHA-256 `95563D1761E172ACDD31AFE695B15CA7B4A4E0CC38CDC21E1E9745B28DE51986`.
The settings UI and diagnostics identify `D3D9On12 OWNER-TEST`. Applying
settings preserves that exact payload; it does not deploy the native proxy.

The signed critical contract covers signed root `play.exe`, `AutoP.exe`, the
unchanged Owner-Test Bridge components, the launcher proxy payload, independent
DX8/DX9 executables, and the profile-local DX9 proxy. The optional Repair
Catalog is absent. Installation stages and verifies every launcher-owned file,
journals prior files under `RevoraUpdates/Rollback`, and rolls back completed
entries if installation fails. The supplied restore command refuses rollback
if an installed file has changed unexpectedly.

No game executable is run by installation or validation. Runtime validation
uses the separately signed v5 observer and stops on protection rejection or a
repeatably earlier exit.
