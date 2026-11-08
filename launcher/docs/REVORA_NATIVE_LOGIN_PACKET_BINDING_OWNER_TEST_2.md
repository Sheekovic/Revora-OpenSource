# Native login packet binding Owner-Test 2

This private package targets only the approved PE32 DX9 executable (SHA-256 `4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE`). The loader verifies normalized path, size, architecture, hash, and signed manifest coverage before loading `RevoraBridge.dll`.

The bridge observes the proven native Login dispatcher, retains bounded credentials for one attempt, and gates only the proven account-login request on TCP port 9958. It hashes the final wire bytes, authenticates through the fixed HTTPS origin, and releases the unchanged request only after the authorization is durably accepted. It never writes credentials, request bodies, packet bytes, digests, grants, or nonces to disk or diagnostics.

Install from the extracted package root:

`./Install-PlayRevoraOwnerTest.ps1 -ClientRoot 'E:\Conquer\7630-original'`

The redacted report is:

`E:\Conquer\7630-original\RevoraDiagnostics\native-login-owner-test-2.jsonl`

Rollback:

`./Rollback-PlayRevoraOwnerTest.ps1 -ClientRoot 'E:\Conquer\7630-original'`

Expected states are `login_bridge_loaded`, `login_bridge_ready`, `native_login_submit`, `native_request_identified`, `https_authorization`, and—only after acceptance—`native_request_released`. Any hash, TLS, timeout, malformed-response, or credential failure suppresses the native request and fails closed.
