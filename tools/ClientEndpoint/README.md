# Revora Client Endpoint

**[Download portable Windows x64 version 1.1](https://github.com/Sheekovic/Revora-OpenSource/releases/tag/client-endpoint-v1.1.0)** ·
**[Full English/Arabic guide](https://github.com/Sheekovic/Revora-OpenSource/blob/main/docs/CLIENT_ENDPOINT_TOOL.md)**

Edit **Server.dat / RevoraServer.dat** and the account endpoint of two reviewed
6711 DX9 client builds in one tool. Includes signature verification, a server
name/IP/port editor, private operator signing keys, backups and paired restore.
The portable release includes .NET 10; no SDK installation is needed.

## Server.dat editor

1. Extract the ZIP. Work on a private copy of your lawfully obtained client and
   close the game/launcher. Open `Revora.ClientEndpoint.exe`.
2. Open **Server.dat editor**. Select the executable and its matching signed
   `Server.dat` or `RevoraServer.dat`, then **Load and verify pair**.
3. Choose a server row and edit its name, IPv4 and **Account Server** TCP port.
   The repository defaults are Account **9958**, Game **9959**. The game port
   is a separate server setting; do not put it into this account-port field.
4. **Save signed pair** updates the list and executable together. It creates or
   reuses your local RSA-2048 signing key, changes the executable's public-key
   constants, and removes any earlier endpoint override so the list controls
   routing. RSA verification remains enabled. Other server rows are preserved.
5. Keep `.revora-endpoint/` beside the executable. It contains private backups,
   journals and `keys/server-list-private.pem`. **Never publish it or the key.**
   New keys have an owner-only Windows ACL; they are not password-encrypted.
   Back up this folder privately. **Restore pair** restores both files exactly.
6. Re-hash both changed files and re-sign your own 6711 launcher manifest. Keep
   the matching launcher/server profile and verify native login and gameplay.

The executable endpoint tab remains available for the original signing keys.
After using the signed-list editor, continue editing/restoring through the
Server.dat tab's paired workflow to avoid breaking the executable/list pair.
An invalid signature, unknown executable, unrelated file changes or corrupted
backup causes refusal. The tool never disables RSA validation or downloads,
injects or starts the game. No client/list/key is bundled.

## بالعربي

فك ضغط الأداة وافتح تبويب **Server.dat editor**. اختار ملف العميل وملف
`Server.dat` أو `RevoraServer.dat` المطابق ليه، واضغط **Load and verify pair**.
اختار السيرفر وعدّل الاسم والـIP وبورت الـAccount Server، وبعدها اضغط
**Save signed pair**. البورت الافتراضي للسورس `9958`؛ بورت اللعب `9959` منفصل.

الأداة بتوقّع القائمة بمفتاح خاص بيك وبتحدّث المفتاح العام في نسخة العميل،
وبتشيل تحويل الـIP القديم عشان العميل يقرأ الإعدادات من ملف DAT نفسه.
التحقق من توقيع RSA بيفضل شغال. **Restore pair** بيرجع الملفين مع بعض لنفس
البايتات السابقة. اشتغل على نسخة خاصة واقفل اللعبة واللانشر قبل التعديل.

احتفظ بمجلد `.revora-endpoint` بشكل خاص: فيه النسخ الاحتياطية وسجل الاسترجاع
والمفتاح السري `keys/server-list-private.pem`. **مترفعش المجلد أو المفتاح على
GitHub ومتشاركهمش مع حد.** بعد التعديل حدّث بصمات الملفين في بروفايل اللانشر
الخاص بيك ووقّع البروفايل من جديد. تشغيل اللعبة وتسجيل الدخول لسه محتاجين
اختبار على سيرفرك؛ نجاح تعديل الملف وتوقيعه مش إثبات لنجاح اللعب.

## Build and automate

From the repository root, with a .NET 10 SDK on Windows:

```powershell
dotnet run --project tools/ClientEndpoint/Gui -p:RevoraRelease=true
dotnet run --project tools/ClientEndpoint/Cli -p:RevoraRelease=true -- dat-inspect "C:\MyClient\Env_DX9\Conquer.exe" "C:\MyClient\Server.dat"
dotnet run --project tools/ClientEndpoint/Cli -p:RevoraRelease=true -- dat-apply "C:\MyClient\Env_DX9\Conquer.exe" "C:\MyClient\Server.dat" 101 "My Revora" 192.168.100.200 9958
dotnet run --project tools/ClientEndpoint/Cli -p:RevoraRelease=true -- dat-restore "C:\MyClient\Env_DX9\Conquer.exe" "C:\MyClient\Server.dat"
./tools/ClientEndpoint/Test.ps1 -ReleaseRuntime
./tools/ClientEndpoint/Build-Package.ps1
```

`inspect`, `apply <exe> <IPv4> <account-port>` and `restore` remain available for
the earlier endpoint-only mode. `dat-inspect` prints local list configuration;
keep it private. `dat-apply` selects an existing non-category ID and saves a
paired restore point. `dat-restore` rolls back the latest matching pair; repeat
to restore earlier edits. There is no force option. After an interrupted write,
keep the game closed and restore using the same paths before further editing.

Tests need Visual Studio C++ x86 tools for the synthetic native ABI harness.
Omitting `-p:RevoraRelease=true` / `-ReleaseRuntime` allows .NET 8 development
machines to test the same source; published packages always use .NET 10.
Rebuild releases to receive runtime servicing updates. See `RUNTIME-NOTICES.txt`
for embedded runtime terms. The tool source is MIT; the executable is unsigned.
