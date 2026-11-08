# Launcher L1 owner review

## Reviewed baseline

- Repository: `PlayRevora-Launcher`
- Branch: `main`
- Source commit: `f8c35a693792ffe82ab6b11b7767e6527ffb8885`
- Remote synchronization at start: `HEAD == origin/main`
- Review date: 2026-08-31
- Build SDK: .NET SDK 10.0.400, targeting .NET 8 Windows

The source tree started clean. The owner-review package was built directly from the approved commit before documentation changes.

## Build and validation

```powershell
dotnet restore PlayRevora.Launcher.sln --locked-mode
dotnet build PlayRevora.Launcher.sln -c Release --no-restore
dotnet run --project tests/PlayRevora.Launcher.FoundationTests/PlayRevora.Launcher.FoundationTests.csproj -c Release --no-build --no-restore
dotnet publish src/PlayRevora.Launcher/PlayRevora.Launcher.csproj -c Release --no-restore -o <private-review-directory>/launcher
```

Results:

- Release build: 0 errors and 0 warnings.
- Foundation tests: 23/23 passed.
- Package scan: no `Conquer.exe`, profile DLL, PEM, PFX, P12 or private-key file.
- Only `PlayRevora.Launcher.exe` was executed for UI review.
- `START GAME` remained disabled because no production verification key or signed client manifest was provisioned.
- No updater, repair, authentication, IPC, game launch, deployment or network listener was exercised.

## Private artifact

- Path: private owner-review directory outside Git (reported directly to the owner)
- Size: 9,975,717 bytes
- SHA-256: `796EA4D040E6458A3B6C366D0DBAD87EFF318E2C92D176E60A894BC34A151141`

This artifact is private owner-review material and must not be uploaded to the public website, attached to a GitHub release, or treated as a production build.

## Visual review correction

The original automated review incorrectly reported no visual regression. Owner inspection found that the default WPF selected-content presenter retained near-white text on a light ComboBox field. This affected the profile, display-mode and resolution values and made disabled-state communication ambiguous. The finding was genuine and the initial review conclusion is superseded by the incremental L1 UI-fix review.

The correction replaces the complete shared ComboBox template: selected-content presenter, field and arrow surfaces, popup, item containers, focus/open/hover/selected states and explicit disabled colors. It also separates profile preference from verification/launch authorization and communicates why Play is disabled.

## Original visual evidence

Screenshots are stored in the private owner-review directory outside Git.

| Capture | State |
|---|---|
| `01-main-screen.png` | Main launcher screen |
| `02-settings-screen.png` | Settings drawer and verification failure |
| `03-automatic-profile.png` | Automatic selection |
| `04-dx8-override.png` | Explicit DirectX 8 override and unavailable explanation |
| `05-dx9-override.png` | Explicit DirectX 9 override and unavailable explanation |
| `06-verification-unavailable.png` | Fail-closed verification state |

The review was captured at 144 DPI and normalized to the launcher's logical 1280 x 768 size. Accessibility automation confirmed the selected values `Automatic`, `DirectX8`, and `DirectX9` independently.

The overall composition remained intact, but the initial review missed the ComboBox contrast defect described above. Artwork, custom frame, navigation, progress/status footer, Display, Proxy, Verify/Repair and graphics controls were not otherwise regressed.

## Incremental UI-fix validation

The corrected private review set contains closed/open ComboBox states, Automatic/DX8/DX9 preferences, display and resolution values, the manifest-not-provisioned explanation, and the disabled Play explanation. Evidence remains outside Git and contains no client binaries or keys.

- Actual host scaling: 150% (144 DPI), captured at native physical resolution.
- 100%: captured in WPF logical 1280 x 768 device-independent pixels.
- 125% and 200%: layout projections were raster-checked for clipping at their expected pixel dimensions.
- Keyboard focus and selection were exercised through UI Automation; the selected values were independently read back as `Automatic`, `DirectX8` and `DirectX9`.
- The open dropdown confirmed dark text on the neutral field/list background and near-white text only on the dark selected/hover surface.
- Explicit template triggers provide a distinct disabled background and dark muted disabled foreground without opacity-only signaling.

Preference selection remains enabled without a manifest. The UI reports `VERIFICATION NOT PROVISIONED` and explains that profile review is allowed while launch remains disabled. `START GAME` stays disabled through WPF command state and the underlying launch service independently rejects invocation without successful verification.

Incremental private artifact metadata:

- Size: 9,979,653 bytes
- SHA-256: `949865555D6488EF7A2A1D5309F6612725D432A900A6A0BA1AFEE40CCC762F29`

## Owner-review boundaries

The expected `VERIFICATION FAILED` state is correct until production signing is provisioned. It is not acceptable to enable Play with a test key. Owner acceptance should cover layout and selection UX only; it does not establish client compatibility, updater safety in production, authentication readiness or gameplay readiness.
