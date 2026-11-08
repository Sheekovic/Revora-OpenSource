# Client Doctor Test 2c

Test 2b crashed when its WPF `Build` handler evaluated `SourcePath.Text` inside the `Task.Run` worker. A WPF control is a thread-affine `DispatcherObject`; Windows therefore raised `InvalidOperationException`, which escaped the `async void` event handler.

Test 2c captures source, destination, signed profile, inventory and cancellation token on the STA/UI thread. The worker receives an immutable request record and progress sink only. Progress is marshalled through the captured UI synchronization context and scheduled at a bounded rate of at most ten updates per second. Ordinary unexpected build errors become sanitized `Unexpected` build reports and always restore button and cancellation state. A dispatcher-level handler writes sanitized crash evidence and then shuts down rather than continuing in an unknown state.

## Windows regression evidence

The actual WPF regression constructs `MainWindow` on an STA with a dispatcher synchronization context, captures the build request on that thread, runs a worker on a distinct thread, checks that the request contains no `DispatcherObject`, exercises a simulated unexpected failure and cancellation, and verifies report creation and restored buttons. GitHub's real Windows runner passed the regression for commit `775c280a92db4efaef52acbc7a0bd591be4a4b5c`:

`https://github.com/Sheekovic/Revora/actions/runs/33195229345`

Test 2c also narrowly permits recovery of a correctly marked Test 2b staging directory. The stage name, marker owner, predecessor build ID, path containment and absence of reparse points must all match. Unmarked, foreign and unexpected staging directories remain untouched.

## Owner artifact

- Build ID: `doctor-reconstruction-5165-test-2c`
- Executable: 139,827,951 bytes
- Executable SHA-256: `346a371304e6cd0e8f980fa87211e4c201edc16befad42f7caedde3aead99fec`
- Signed profile SHA-256: `6659b970e749786e38afdea475a4a1f14a3ceb2ba5d63f20f03b0c6f17ba56aa`
- Store-only ZIP: 146,317,883 bytes
- ZIP SHA-256: `d551396ca5db8299fd9b2499aa6af14c0ab518779d50d526ca9e39f7b558b54e`
- Authenticated route: `/owner-downloads/client-doctor-test-2c`

Two independent Windows-targeted cross-publishes were byte-identical. ClamAV 1.4.3 definitions 28106 reported zero detections for the executable and ZIP. Client launching remains blocked; this package performs reconstruction and verification only.
