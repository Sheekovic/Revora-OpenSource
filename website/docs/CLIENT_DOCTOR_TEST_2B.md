# Client Doctor Test 2b

Test 2 failed deterministically on Windows because `AtomicUpdate.StageAsync` retained the `.partial` verification stream through `File.Move`. Windows correctly rejected renaming the open file; Linux permits that rename and did not reveal the defect.

Test 2b scopes and disposes the verification stream before digest validation and rename. The final client verifier was also changed to an explicit stream scope. Download input/output streams were already scoped and disposed before hashing; `ClientConstructor` uses `File.Copy` and held no stream across activation.

## Windows regression evidence

The regression signs a test profile, downloads its test object through `AtomicUpdate`, writes and hashes the `.partial` file, renames it, opens the result exclusively, renames it again, and verifies that no `.partial` remains. It ran on GitHub's actual `windows-2025` runner at commit `350b23a094ee068b41d50503ec4b7f549a5303e5` and passed:

`https://github.com/Sheekovic/Revora/actions/runs/33193965584`

## Staging safety and diagnostics

New staging trees contain `.revora-client-doctor-staging.json` with the exact owner and signed build ID. Automatic retry cleanup is limited to `Revora Client Clean.staging`, outside the donor, without reparse points, and only when that marker matches the current build. Unmarked directories are preserved and produce a narrow manual instruction.

Build reports contain only phase, signed relative path, exception category, cleanup outcome, copied/downloaded/excluded counts, final counts and bytes, destination verification state, and signed-profile verification state. Absolute paths and raw exception messages are excluded. Progress reports copy/download/hash/move/final-verify phases, files, and downloaded bytes; cancellation uses the same marked cleanup path.

## Owner artifact

- Build ID: `doctor-reconstruction-5165-test-2b`
- Executable: 139,815,663 bytes
- Executable SHA-256: `2e4749868b40347cbf3a959f04bf1f5d955c3d94806ba19590fb068cc8494636`
- Signed profile SHA-256: `60bb1a88314e9db2bf3339ddc8fef1314280b10b3e3cb6f7608354c8c8ede1ed`
- Store-only ZIP: 146,305,854 bytes
- ZIP SHA-256: `bf2a8f4aaa3475357c53df800422292adfb60a0485138f474544c28c0d9d6328`
- Authenticated route: `/owner-downloads/client-doctor-test-2b`

Two independent Windows-targeted cross-publishes were byte-identical. ClamAV 1.4.3 reported zero detections for both the first-party executable and final ZIP. This remains a construction-only test: launching Conquer is blocked, Flash is absent, and active client approval still requires the owner's Defender and Malwarebytes results.
