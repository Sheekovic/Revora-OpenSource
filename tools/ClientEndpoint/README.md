# Revora Client Endpoint

**[Download the portable Windows x64 tool](https://github.com/Sheekovic/Revora-OpenSource/releases/tag/client-endpoint-v1.0.0)** ·
**[Full guide and supported hashes](https://github.com/Sheekovic/Revora-OpenSource/blob/main/docs/CLIENT_ENDPOINT_TOOL.md)**

Configure your own **6711 DX9 account server IPv4 and TCP port** without a hex
editor. Includes inspection, exact-hash guards, automatic backups, retargeting
and restore. The portable release includes .NET 10; no SDK installation needed.

1. Extract the release ZIP. Work on a copy of your lawfully obtained client;
   close the game and launcher. You do not normally need administrator rights.
2. Open `Revora.ClientEndpoint.exe`. Browse to `Env_DX9/Conquer.exe` or
   `Env_DX9/Conquer-Revora.exe` and choose **Inspect**.
3. Enter your account server's IPv4 and account TCP port, then **Apply endpoint**.
   The repository defaults are Account **9958**, Game **9959**; the local
   acceptance setup uses Account **19558**. This field is the **account** port.
4. Keep `.revora-endpoint/backups/` beside the client. Inspect the result.
   **Restore original** reverses only a recognized output of this tool.
5. Configure the separate server, valid matching server list and launcher
   profile as described in the full guide. A patched endpoint alone does not
   establish working login or gameplay.

### بالعربي

فك ضغط الأداة وافتحها، واختار ملف العميل من مجلد `Env_DX9` واضغط **Inspect**.
اكتب IP السيرفر وبورت الـAccount Server، وبعدها اضغط **Apply endpoint**.
البورت الافتراضي في السورس هو `9958`؛ بورت اللعب `9959` بيتظبط بشكل منفصل.
اشتغل على نسخة من العميل واقفل اللعبة واللانشر قبل التعديل. الأداة بتحتفظ
بنسخة احتياطية، وزر **Restore original** بيرجع الملف الأصلي بعد التحقق من بصمته.
الأداة بتقبل نسختين DX9 محددتين بالبصمة؛ لو نسختك مختلفة، هترفض التعديل.

## Build / automation

From the repository root, with a .NET 10 SDK on Windows:

```powershell
dotnet run --project tools/ClientEndpoint/Gui -p:RevoraRelease=true
dotnet run --project tools/ClientEndpoint/Cli -p:RevoraRelease=true -- inspect "C:\MyClient\Env_DX9\Conquer.exe"
dotnet run --project tools/ClientEndpoint/Cli -p:RevoraRelease=true -- apply "C:\MyClient\Env_DX9\Conquer.exe" 192.168.100.200 9958
dotnet run --project tools/ClientEndpoint/Cli -p:RevoraRelease=true -- restore "C:\MyClient\Env_DX9\Conquer.exe"
./tools/ClientEndpoint/Test.ps1 -ReleaseRuntime
./tools/ClientEndpoint/Build-Package.ps1
```

The tests need Visual Studio's C++ x86 build tools for the synthetic native ABI
harness. Omitting `-p:RevoraRelease=true` / `-ReleaseRuntime` allows existing
.NET 8 development machines to test the same source; published binaries always
use .NET 10. Rebuild releases to receive runtime servicing updates.

The executable is unsigned. This project does not ship a game client or change
RSA validation, server-list signatures, anti-cheat, activation or account
authentication. It performs no downloads, injection or game launch. The source
is MIT; embedded Microsoft runtime terms are listed in `RUNTIME-NOTICES.txt`.
