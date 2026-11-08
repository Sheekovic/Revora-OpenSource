# Dragon Architecture Assessment

Dragon is a command-line x86 injector and hook-module framework, not the graphical launcher or
auto-patcher described by some deployments.

## Launch flow

`Dragon.Launch` constructs a client command line, starts the game with `CreateProcess` and
`CREATE_SUSPENDED`, and requires both launcher and child to be WOW64 processes. It enumerates the
current working directory for every regular file matching `Dragon.Module.*.dll`. For each match it
allocates memory in the child, writes the absolute DLL path, queues `LoadLibraryA` through
`QueueUserAPC` on the primary thread, resumes the child, and polls its module list until the number
of matching loaded modules equals the number discovered.

The code allocates the path buffer with `PAGE_EXECUTE_READWRITE`, although the buffer contains
data, not executable instructions. The polling loop has no reviewed deadline. Client arguments
and module membership are not fixed by a signed manifest or exact hashes.

## Hook modules

The shared module framework uses Microsoft Detours initialization and `DetourAttach`/detach.

- `Dragon.Module.Connect` hooks Winsock `connect`. An editable local JSON file enables redirection
  for Account destination ports 9950 through 9980 and Game ports 5810 through 5820. It rewrites
  the caller's IPv4 address and port before invoking the original API.
- `Dragon.Module.Flash` hooks `LoadLibraryExW`; a request whose library name contains `Flash.ocx`
  is changed to `./Flash.ocx`. It still depends on a local proprietary obsolete ActiveX runtime.
- `Dragon.Module.Websites` hooks `ShellExecuteA` and rewrites or suppresses recognized historical
  home, forum, credit, signup, and signout URLs from editable JSON.

The source and client must share the same 32-bit architecture. The README demonstrates launching
through a `runas` verb and recommends antivirus exclusions. Static source review did not prove
that elevation is technically necessary for a same-user child; Revora must not inherit that
operational requirement without an independent standard-user Windows proof.

## Useful reference concepts

The useful architectural observations are narrow: starting the exact child before it can contact
historical endpoints, classifying the known Account/Game port families, redirecting only the
client-owned socket call, and cleaning up after module loading. These remain design evidence, not
approval to adopt Dragon's runtime mechanism.
