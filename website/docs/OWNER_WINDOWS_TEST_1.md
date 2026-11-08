# Owner Windows Test 1 — Inventory Only

Build ID: `doctor-inventory-1`. This test must not launch Conquer or construct a distributable client.

1. Use current Windows 10 or Windows 11 x64 as a standard non-admin user. Keep Microsoft Defender and Malwarebytes Premium enabled and current.
2. Download the package from the authenticated Revora Admin link supplied in the milestone report.
3. Run `Get-FileHash -Algorithm SHA256 .\revora-client-doctor-test-1.zip` in PowerShell and compare it with the reported hash.
4. Scan the ZIP and extracted folder with Defender and Malwarebytes. Never restore or allow a quarantined item.
5. Extract into a new ordinary user-owned folder, not Windows, Program Files, a drive root, network share, synchronized folder, or the legacy client folder.
6. Networking may remain enabled because this build performs no downloads; disconnecting is also acceptable.
7. Start `Revora.ClientDoctor.exe` without elevation. Reject any unexpected UAC prompt.
8. Select the owner's existing legacy Conquer folder. It is inventory input only and remains unchanged.
9. Expected state: **Unsafe source inventoried**. Active files must be `ActiveUntrusted`; unexpected or mismatched files must not be reusable.
10. Do not launch Conquer. Test build 1 has no launch or build action.
11. The sanitized report is `%LOCALAPPDATA%\Revora\ClientDoctor\reports\doctor-<UTC timestamp>.json`.
12. Return only that JSON report and Defender/Malwarebytes product, version, and result summaries. Remove any personal path manually if one unexpectedly appears.

No later package will use donor assets until this report and both security-product results pass review.
