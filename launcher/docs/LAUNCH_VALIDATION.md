# End-to-end launch validation

Date: 2026-08-31. The visible WPF `START GAME` automation element was invoked
through Windows UI Automation. No credentials were entered and no login action
was performed.

## Result

- `LaunchGameCommand` created the expected staged executable at
  `C:\Users\Administrator\Downloads\Revora-Client-Staging\Conquer-7630\assembled\Env_DX9\Conquer.exe`.
- Process ID 3332 remained responsive during the bounded observation window.
- No TCP sockets were observed for the client process.
- The launcher read the existing render configuration and atomically wrote the
  proxy-supported keys: native dimensions, forced windowed mode, borderless
  mode, nearest monitor, and 300-frame present logging.
- Staged and bundled `d3d9.dll` files both had SHA-256
  `1F2400BFE993079477C9C4879BFFD32F40D5D46C2DF6CCBE55E70719EABD9442`.
- The proxy log recorded `direct3d_create9`, the requested/effective presentation
  parameters, successful borderless application at 2560 × 1440, and successful
  device creation for the observed process ID.
- The client and launcher were closed after evidence capture; neither remained
  running.

This proves the bounded launcher-to-client startup path and proxy activation. It
does not prove login, gameplay, long-session stability, or public server
compatibility.
