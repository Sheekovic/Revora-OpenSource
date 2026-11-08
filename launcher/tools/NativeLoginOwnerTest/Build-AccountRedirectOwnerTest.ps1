$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$stamp='20260902'
$work=Join-Path $repo 'out\two-phase-owner-canary-package'
$publish=Join-Path $work 'publish'
$package="C:\Users\Administrator\Desktop\launcher-tests\PlayRevora-Identifier-Bridge-Owner-Canary-$stamp"
$zip=$package+'.zip'
$previous='C:\Users\Administrator\Desktop\launcher-tests\PlayRevora-Active-Native-Login-Path-OwnerTest-4-20260901'
if(Test-Path $package){throw 'Package exists.'};if(Test-Path $zip){throw 'Archive exists.'}
if(Test-Path $work){Remove-Item -LiteralPath $work -Recurse -Force};[IO.Directory]::CreateDirectory($publish)|Out-Null
dotnet publish (Join-Path $repo 'src\PlayRevora.Launcher\PlayRevora.Launcher.csproj') -c Release -r win-x64 --self-contained true -o $publish --no-restore
if($LASTEXITCODE){throw 'Publish failed.'}
$trust=Get-Content (Join-Path $previous 'owner-test-public-trust.json') -Raw|ConvertFrom-Json
[IO.Directory]::CreateDirectory((Join-Path $publish 'LauncherAssets'))|Out-Null
Copy-Item (Join-Path $repo 'out\identifier-bridge\Release\RevoraAccountRedirect.dll') (Join-Path $publish 'LauncherAssets\RevoraAccountRedirect.dll')
Copy-Item (Join-Path $repo 'out\identifier-bridge\Release\RevoraAccountRedirectLoader.exe') (Join-Path $publish 'LauncherAssets\RevoraAccountRedirectLoader.exe')
Copy-Item (Join-Path $previous 'payload\patch-index.signed.json') (Join-Path $publish 'patch-index.signed.json')
[IO.Directory]::CreateDirectory((Join-Path $publish 'RevoraUpdates\State'))|Out-Null
Copy-Item (Join-Path $previous 'payload\patch-index.signed.json') (Join-Path $publish 'RevoraUpdates\State\patch-index.cached.signed.json')
$config=Get-Content (Join-Path $publish 'launcher.json') -Raw|ConvertFrom-Json
$config.gameRoot='.';$config.buildChannel='owner-test';$config.manifestKeyId=$trust.keyId;$config.manifestPublicKeySpkiBase64=$trust.publicKeySpkiBase64;$config.dx9BridgeOwnerTestEnabled=$true
$config.dx9BridgeLoaderPath='LauncherAssets\RevoraAccountRedirectLoader.exe';$config.dx9BridgeDllPath='LauncherAssets\RevoraAccountRedirect.dll'
[IO.File]::WriteAllText((Join-Path $publish 'launcher.json'),($config|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
function E($p,$s,$h){[ordered]@{relativePath=$p;size=[long]$s;sha256=$h;critical=$true}}
$dll=Get-Item (Join-Path $publish 'LauncherAssets\RevoraAccountRedirect.dll');$loader=Get-Item (Join-Path $publish 'LauncherAssets\RevoraAccountRedirectLoader.exe')
$rootFiles=@(E 'LauncherAssets/RevoraAccountRedirect.dll' $dll.Length (Get-FileHash $dll.FullName -Algorithm SHA256).Hash;E 'LauncherAssets/RevoraAccountRedirectLoader.exe' $loader.Length (Get-FileHash $loader.FullName -Algorithm SHA256).Hash)
$dx8=@(E 'Env_DX8/Conquer.exe' 18534160 'F417A25AB8B037D37619E304618BE615F7645CAF39F760EFF98D988796EE4E1E')
$dx9=@(E 'Env_DX9/Conquer.exe' 18594824 '4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE';E 'Env_DX9/d3d9.dll' 248320 '1F2400BFE993079477C9C4879BFFD32F40D5D46C2DF6CCBE55E70719EABD9442')
function P($id,$name,$exe,$dir,$files,$d8,$d9,$priority){[ordered]@{id=$id;displayName=$name;executablePath=$exe;workingDirectory=$dir;arguments=@('blacknull');requiredFiles=$files;criticalLaunchFiles=$files;minimumWindowsVersion='10.0';supportedArchitectures=@('x64');requiresDirectX8=$d8;requiresDirectX9=$d9;priority=$priority;automaticFallbackAllowed=$false}}
$previousManifestHash=(Get-FileHash (Join-Path $previous 'payload\client-manifest.signed.json') -Algorithm SHA256).Hash
$manifest=[ordered]@{schemaVersion=2;manifestVersion='owner-test-7630-identifier-bridge-1';issuedAtUtc='2026-09-02T00:00:00Z';expiresAtUtc='2027-09-02T00:00:00Z';minimumLauncherVersion='1.0.0';rollback=[ordered]@{previousManifestVersion='owner-test-7630-two-phase-canary-2';previousManifestSha256=$previousManifestHash};rootRequiredFiles=$rootFiles;profiles=@((P 'dx8' 'DirectX 8' 'Env_DX8/Conquer.exe' 'Env_DX8' $dx8 $true $false 50),(P 'dx9' 'DirectX 9 - IDENTIFIER BRIDGE OWNER CANARY' 'Env_DX9/Conquer.exe' 'Env_DX9' $dx9 $false $true 100));signature=[ordered]@{algorithm='ECDSA_P256_SHA256';keyId=$trust.keyId;valueBase64=''}}
$unsigned=Join-Path $work 'client-manifest.unsigned.json';[IO.File]::WriteAllText($unsigned,($manifest|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
dotnet (Join-Path $repo 'tools\PlayRevora.ClientManifestSigner\bin\Release\net8.0-windows\win-x64\PlayRevora.ClientManifestSigner.dll') sign-windows $unsigned PlayRevoraOwnerTest20260831V2 (Join-Path $publish 'client-manifest.signed.json')
if($LASTEXITCODE){throw 'Manifest signing failed.'}
[IO.Directory]::CreateDirectory((Join-Path $package 'payload'))|Out-Null;Copy-Item (Join-Path $publish '*') (Join-Path $package 'payload') -Recurse
Copy-Item (Join-Path $repo 'tools\DirectProfileOwnerTest\Install-PlayRevoraOwnerTest.ps1') $package
Copy-Item (Join-Path $repo 'tools\DirectProfileOwnerTest\Rollback-PlayRevoraOwnerTest.ps1') $package
Copy-Item (Join-Path $previous 'Verify-OwnerTestIntegrity.ps1') $package;Copy-Item (Join-Path $previous 'owner-test-public-trust.json') $package
$readme="# PlayRevora Identifier Bridge Owner Canary`r`n`r`nInstall: ``.\Install-PlayRevoraOwnerTest.ps1 -ClientRoot '<CLIENT_ROOT>'```r`n`r`nAuthenticate SHEEKOVIC in the launcher, start explicit DX9, then enter the same credentials once in the native login UI. The password is never persisted or sent to the client integration.`r`n`r`nReport: ``<CLIENT_ROOT>\RevoraDiagnostics\account-redirect-owner-test.jsonl```r`n`r`nRollback: ``.\Rollback-PlayRevoraOwnerTest.ps1 -ClientRoot '<CLIENT_ROOT>'```r`n"
[IO.File]::WriteAllText((Join-Path $package 'README.md'),$readme,[Text.UTF8Encoding]::new($false))
$files=@(Get-ChildItem (Join-Path $package 'payload') -File -Recurse|ForEach-Object{$r=$_.FullName.Substring((Join-Path $package 'payload').Length+1).Replace('\','/');[ordered]@{relativePath=$r;size=[long]$_.Length;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}}|Sort-Object relativePath)
$sourceCommit=(git -C $repo rev-parse HEAD).Trim();if($LASTEXITCODE){throw 'Source commit could not be recorded.'}
$contract=[ordered]@{schemaVersion=1;variant='TWO-PHASE NATIVE AUTH OWNER CANARY IDENTIFIER BRIDGE';sourceCommit=$sourceCommit;preflight=@(E 'Env_DX9/Conquer.exe' 18594824 '4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE';E 'Env_DX9/d3d9.dll' 248320 '1F2400BFE993079477C9C4879BFFD32F40D5D46C2DF6CCBE55E70719EABD9442');files=$files}
[IO.File]::WriteAllText((Join-Path $package 'install-contract.json'),($contract|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
$entries=@(Get-ChildItem $package -File -Recurse|Sort-Object FullName|ForEach-Object{[ordered]@{relativePath=$_.FullName.Substring($package.Length+1).Replace('\','/');size=[long]$_.Length;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}})
$payload=[ordered]@{schemaVersion=1;label='PLAYREVORA IDENTIFIER BRIDGE OWNER CANARY';files=$entries};$canonical=$payload|ConvertTo-Json -Depth 8 -Compress
$cng=[Security.Cryptography.CngKey]::Open('PlayRevoraOwnerTest20260831V2');try{$key=[Security.Cryptography.ECDsaCng]::new($cng);try{$sig=$key.SignData([Text.Encoding]::UTF8.GetBytes($canonical),[Security.Cryptography.HashAlgorithmName]::SHA256)}finally{$key.Dispose()}}finally{$cng.Dispose()}
$signed=[ordered]@{schemaVersion=1;label=$payload.label;files=$entries;signature=[ordered]@{algorithm='ECDSA_P256_SHA256';keyId=$trust.keyId;valueBase64=[Convert]::ToBase64String($sig)}}
[IO.File]::WriteAllText((Join-Path $package 'owner-test-integrity.signed.json'),($signed|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
& (Join-Path $package 'Verify-OwnerTestIntegrity.ps1') $package
Compress-Archive (Join-Path $package '*') $zip -CompressionLevel Optimal
$item=Get-Item $zip;[pscustomobject]@{path=$item.FullName;size=$item.Length;sha256=(Get-FileHash $zip -Algorithm SHA256).Hash;dllSha256=(Get-FileHash $dll.FullName -Algorithm SHA256).Hash;loaderSha256=(Get-FileHash $loader.FullName -Algorithm SHA256).Hash}|ConvertTo-Json
