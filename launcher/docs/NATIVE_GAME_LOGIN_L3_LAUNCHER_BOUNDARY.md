# Native game login launcher boundary

The PlayRevora Launcher does not authenticate players. It performs signed update and critical-file checks, optional repair, display/profile selection, duplicate-process protection, and direct profile launch using the structured `blacknull` argument.

Account creation is exposed only as a fixed browser link to `https://playrevora.com/register`. The destination is compiled into the launcher, requires HTTPS, permits no query, fragment, alternate port, credentials, configuration override, or remote override, and is opened using the operating system's default browser. The launcher does not embed the page or receive, inspect, persist, or log registration credentials. A three-second local throttle prevents rapid repeated browser creation.

The launcher contains no player-login form, password submission, DPAPI authentication session, session refresh/logout workflow, or game-grant request. Native Patch-7630 credentials remain an Account Server concern and Start Game is independent of launcher authentication.
