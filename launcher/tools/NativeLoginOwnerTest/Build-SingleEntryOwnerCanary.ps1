[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$stamp='20260904'
$work=Join-Path $repo 'out\single-entry-owner-canary-credential-boundary-fix'
$publish=Join-Path $work 'publish'
$package="C:\Users\Administrator\Desktop\launcher-tests\PlayRevora-Single-Entry-Owner-Canary-Credential-Boundary-Fix-$stamp"
$zip=$package+'.zip'
$previous='C:\Users\Administrator\Desktop\launcher-tests\PlayRevora-Single-Entry-Owner-Canary-Per-Attempt-Readiness-Fix-Verified-20260903'
if(Test-Path $package){throw 'Package directory already exists.'}
if(Test-Path $zip){throw 'Package archive already exists.'}
if(Test-Path $work){Remove-Item -LiteralPath $work -Recurse -Force}
[IO.Directory]::CreateDirectory($publish)|Out-Null

dotnet publish (Join-Path $repo 'src\PlayRevora.Launcher\PlayRevora.Launcher.csproj') -c Release -r win-x64 --self-contained true -o $publish --no-restore
if($LASTEXITCODE){throw 'Launcher publish failed.'}
$bundledProxy=Join-Path $publish 'LauncherAssets\d3d9.dll'
if(Test-Path $bundledProxy){Remove-Item -LiteralPath $bundledProxy -Force}
$trust=Get-Content (Join-Path $previous 'owner-test-public-trust.json') -Raw -Encoding UTF8|ConvertFrom-Json
[IO.Directory]::CreateDirectory((Join-Path $publish 'LauncherAssets'))|Out-Null
$native=Join-Path $repo 'tools\RevoraBridge\out-single-entry\Release'
Copy-Item (Join-Path $native 'RevoraNativeLogin.dll') (Join-Path $publish 'LauncherAssets\RevoraNativeLogin.dll')
Copy-Item (Join-Path $native 'RevoraNativeLoginLoader.exe') (Join-Path $publish 'LauncherAssets\RevoraNativeLoginLoader.exe')
Copy-Item (Join-Path $previous 'payload\patch-index.signed.json') (Join-Path $publish 'patch-index.signed.json')
[IO.Directory]::CreateDirectory((Join-Path $publish 'RevoraUpdates\State'))|Out-Null
Copy-Item (Join-Path $previous 'payload\patch-index.signed.json') (Join-Path $publish 'RevoraUpdates\State\patch-index.cached.signed.json')

$config=Get-Content (Join-Path $publish 'launcher.json') -Raw -Encoding UTF8|ConvertFrom-Json
$config.gameRoot='.';$config.buildChannel='owner-test';$config.manifestKeyId=$trust.keyId
$config.manifestPublicKeySpkiBase64=$trust.publicKeySpkiBase64;$config.dx9BridgeOwnerTestEnabled=$true
$config.dx9BridgeLoaderPath='LauncherAssets\RevoraNativeLoginLoader.exe'
$config.dx9BridgeDllPath='LauncherAssets\RevoraNativeLogin.dll'
[IO.File]::WriteAllText((Join-Path $publish 'launcher.json'),($config|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))

$diagnostics=[ordered]@{schemaVersion=4;reportPattern='RevoraDiagnostics/native-login-single-entry-{CORRELATION}.jsonl';allowedEvents=@(
 'login_bridge_loaded','executable_verified','target_resolved','target_preimage_verified','hook_install_started',
 'hook_install_succeeded','hook_postimage_verified','login_bridge_ready','login_submit_entered',
 'credential_capture_armed','username_accessor_observed','password_accessor_observed','credential_pair_complete',
 'credential_capture_failed','credential_buffer_cleared',
 'account_endpoint_selected','account_request_candidate','account_request_classified','https_started','https_result',
 'native_request_released','native_request_suppressed','native_send_result','account_request_not_observed',
 'authorization_accepted_request_released','authorization_rejected_request_released_for_native_rejection',
 'authorization_indeterminate_request_suppressed','native_rejection_received',
 'login_ui_restored_after_transport_failure','world_handle_captured','world_handle_injected',
 'world_handle_cleared','world_handle_bridge_rejected','final_stage','process_shutdown')
 prohibited=@('username','password','requestBody','requestSha256','token','ticket','worldHandle','packetBytes','cipherState')}
[IO.File]::WriteAllText((Join-Path $publish 'native-login-diagnostics.schema.json'),($diagnostics|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))

function E($p,$s,$h){[ordered]@{relativePath=$p;size=[long]$s;sha256=$h;critical=$true}}
$dll=Get-Item (Join-Path $publish 'LauncherAssets\RevoraNativeLogin.dll')
$loader=Get-Item (Join-Path $publish 'LauncherAssets\RevoraNativeLoginLoader.exe')
$rootFiles=@(
 E 'LauncherAssets/RevoraNativeLogin.dll' $dll.Length (Get-FileHash $dll.FullName -Algorithm SHA256).Hash
 E 'LauncherAssets/RevoraNativeLoginLoader.exe' $loader.Length (Get-FileHash $loader.FullName -Algorithm SHA256).Hash
 E 'TQPlat.dll' 9808288 '4F2C499CAD3272D1CC3AA3C351AB7124215B304BA9866D50362EE769D06CFFC2')
$dx8=@(E 'Env_DX8/Conquer.exe' 18534160 'F417A25AB8B037D37619E304618BE615F7645CAF39F760EFF98D988796EE4E1E')
$dx9=@(
 E 'Env_DX9/Conquer.exe' 18594824 '4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE'
 E 'Env_DX9/TQPlat.dll' 6550288 'E46149B7ED0A3BC142FD22CADF1C30FF6A42204B08F6B5920221A2DF0E69815A')
function P($id,$name,$exe,$dir,$files,$d8,$d9,$priority){[ordered]@{id=$id;displayName=$name;executablePath=$exe;workingDirectory=$dir;arguments=@('blacknull');requiredFiles=$files;criticalLaunchFiles=$files;minimumWindowsVersion='10.0';supportedArchitectures=@('x64');requiresDirectX8=$d8;requiresDirectX9=$d9;priority=$priority;automaticFallbackAllowed=$false}}
$previousManifestHash=(Get-FileHash (Join-Path $previous 'payload\client-manifest.signed.json') -Algorithm SHA256).Hash
$manifest=[ordered]@{schemaVersion=2;manifestVersion='owner-test-7630-single-entry-native-login-6';issuedAtUtc='2026-09-04T00:00:00Z';expiresAtUtc='2027-09-04T00:00:00Z';minimumLauncherVersion='1.0.0';rollback=[ordered]@{previousManifestVersion='owner-test-7630-single-entry-native-login-5';previousManifestSha256=$previousManifestHash};rootRequiredFiles=$rootFiles;profiles=@((P 'dx8' 'DirectX 8' 'Env_DX8/Conquer.exe' 'Env_DX8' $dx8 $true $false 50),(P 'dx9' 'DirectX 9 - SINGLE ENTRY NATIVE LOGIN CANARY' 'Env_DX9/Conquer.exe' 'Env_DX9' $dx9 $false $true 100));signature=[ordered]@{algorithm='ECDSA_P256_SHA256';keyId=$trust.keyId;valueBase64=''}}
$unsigned=Join-Path $work 'client-manifest.unsigned.json'
[IO.File]::WriteAllText($unsigned,($manifest|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
dotnet (Join-Path $repo 'tools\PlayRevora.ClientManifestSigner\bin\Release\net8.0-windows\win-x64\PlayRevora.ClientManifestSigner.dll') sign-windows $unsigned PlayRevoraOwnerTest20260831V2 (Join-Path $publish 'client-manifest.signed.json')
if($LASTEXITCODE){throw 'Client manifest signing failed.'}
dotnet (Join-Path $repo 'tools\PlayRevora.ClientManifestSigner\bin\Release\net8.0-windows\win-x64\PlayRevora.ClientManifestSigner.dll') verify (Join-Path $publish 'client-manifest.signed.json') (Join-Path $previous 'owner-test-public-trust.json')
if($LASTEXITCODE){throw 'Production client-manifest verification failed before packaging.'}

[IO.Directory]::CreateDirectory((Join-Path $package 'payload'))|Out-Null
Copy-Item (Join-Path $publish '*') (Join-Path $package 'payload') -Recurse
Copy-Item (Join-Path $repo 'tools\DirectProfileOwnerTest\Install-PlayRevoraOwnerTest.ps1') $package
Copy-Item (Join-Path $repo 'tools\DirectProfileOwnerTest\Rollback-PlayRevoraOwnerTest.ps1') $package
Copy-Item (Join-Path $previous 'Verify-OwnerTestIntegrity.ps1') $package
Copy-Item (Join-Path $previous 'owner-test-public-trust.json') $package
$readme="# PlayRevora single-entry native-login owner canary`r`n`r`nDo not install until separately approved.`r`n`r`nInstall: ``.\Install-PlayRevoraOwnerTest.ps1 -ClientRoot '<CLIENT_ROOT>'```r`n`r`nPer-attempt reports: ``<CLIENT_ROOT>\RevoraDiagnostics\native-login-single-entry-<CORRELATION>.jsonl```r`n`r`nRollback: ``.\Rollback-PlayRevoraOwnerTest.ps1 -ClientRoot '<CLIENT_ROOT>'```r`n"
[IO.File]::WriteAllText((Join-Path $package 'README.md'),$readme,[Text.UTF8Encoding]::new($false))
$files=@(Get-ChildItem (Join-Path $package 'payload') -File -Recurse|ForEach-Object{$r=$_.FullName.Substring((Join-Path $package 'payload').Length+1).Replace('\','/');[ordered]@{relativePath=$r;size=[long]$_.Length;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}}|Sort-Object relativePath)
$sourceCommit=(git -C $repo rev-parse HEAD).Trim();if($LASTEXITCODE){throw 'Source commit unavailable.'}
$contract=[ordered]@{schemaVersion=1;variant='SINGLE-ENTRY NATIVE LOGIN OWNER CANARY';sourceCommit=$sourceCommit;preflight=@(
 E 'Env_DX9/Conquer.exe' 18594824 '4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE'
 E 'Env_DX9/TQPlat.dll' 6550288 'E46149B7ED0A3BC142FD22CADF1C30FF6A42204B08F6B5920221A2DF0E69815A'
 E 'TQPlat.dll' 9808288 '4F2C499CAD3272D1CC3AA3C351AB7124215B304BA9866D50362EE769D06CFFC2');files=$files}
[IO.File]::WriteAllText((Join-Path $package 'install-contract.json'),($contract|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
$entries=@(Get-ChildItem $package -File -Recurse|Sort-Object FullName|ForEach-Object{[ordered]@{relativePath=$_.FullName.Substring($package.Length+1).Replace('\','/');size=[long]$_.Length;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}})
$integrityPayload=[ordered]@{schemaVersion=1;label='PLAYREVORA SINGLE-ENTRY NATIVE LOGIN OWNER CANARY';files=$entries}
$canonical=$integrityPayload|ConvertTo-Json -Depth 8 -Compress
$cng=[Security.Cryptography.CngKey]::Open('PlayRevoraOwnerTest20260831V2')
try{$key=[Security.Cryptography.ECDsaCng]::new($cng);try{$sig=$key.SignData([Text.Encoding]::UTF8.GetBytes($canonical),[Security.Cryptography.HashAlgorithmName]::SHA256)}finally{$key.Dispose()}}finally{$cng.Dispose()}
$signed=[ordered]@{schemaVersion=1;label=$integrityPayload.label;files=$entries;signature=[ordered]@{algorithm='ECDSA_P256_SHA256';keyId=$trust.keyId;valueBase64=[Convert]::ToBase64String($sig)}}
[IO.File]::WriteAllText((Join-Path $package 'owner-test-integrity.signed.json'),($signed|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
& (Join-Path $package 'Verify-OwnerTestIntegrity.ps1') -PackageDirectory $package
Compress-Archive (Join-Path $package '*') $zip -CompressionLevel Optimal
$extract=Join-Path $work 'fresh-extraction';Expand-Archive $zip $extract
& (Join-Path $extract 'Verify-OwnerTestIntegrity.ps1') -PackageDirectory $extract
$validationTree=Join-Path $work 'production-verification-tree'
[IO.Directory]::CreateDirectory((Join-Path $validationTree 'Env_DX8'))|Out-Null
[IO.Directory]::CreateDirectory((Join-Path $validationTree 'Env_DX9'))|Out-Null
[IO.Directory]::CreateDirectory((Join-Path $validationTree 'LauncherAssets'))|Out-Null
$approvedClient='C:\Users\Administrator\Desktop\launcher-tests\PlayRevora-Client'
Copy-Item (Join-Path $approvedClient 'Env_DX8\Conquer.exe') (Join-Path $validationTree 'Env_DX8\Conquer.exe')
Copy-Item (Join-Path $approvedClient 'Env_DX9\Conquer.exe') (Join-Path $validationTree 'Env_DX9\Conquer.exe')
Copy-Item (Join-Path $approvedClient 'Env_DX9\TQPlat.dll') (Join-Path $validationTree 'Env_DX9\TQPlat.dll')
Copy-Item (Join-Path $approvedClient 'TQPlat.dll') (Join-Path $validationTree 'TQPlat.dll')
Copy-Item (Join-Path $extract 'payload\LauncherAssets\RevoraNativeLogin.dll') (Join-Path $validationTree 'LauncherAssets\RevoraNativeLogin.dll')
Copy-Item (Join-Path $extract 'payload\LauncherAssets\RevoraNativeLoginLoader.exe') (Join-Path $validationTree 'LauncherAssets\RevoraNativeLoginLoader.exe')
dotnet (Join-Path $repo 'tools\PlayRevora.ClientManifestSigner\bin\Release\net8.0-windows\win-x64\PlayRevora.ClientManifestSigner.dll') verify-tree (Join-Path $extract 'payload\client-manifest.signed.json') (Join-Path $extract 'owner-test-public-trust.json') $validationTree
if($LASTEXITCODE){throw 'Fresh-package production client-manifest verification failed.'}
$forbidden=@(Get-ChildItem $extract -File -Recurse|Where-Object{$_.Name -in @('Conquer.exe','TQPlat.dll','LoginLoader.dll','d3d9.dll') -or $_.Extension -in @('.dmp','.pcap','.pfx','.key')})
if($forbidden.Count){throw "Forbidden package artifact: $($forbidden[0].FullName)"}
$item=Get-Item $zip
[pscustomobject]@{path=$item.FullName;size=$item.Length;sha256=(Get-FileHash $zip -Algorithm SHA256).Hash;manifestVersion=$manifest.manifestVersion;clientManifestSha256=(Get-FileHash (Join-Path $package 'payload\client-manifest.signed.json') -Algorithm SHA256).Hash;integrityManifestSha256=(Get-FileHash (Join-Path $package 'owner-test-integrity.signed.json') -Algorithm SHA256).Hash;dllSha256=(Get-FileHash $dll.FullName -Algorithm SHA256).Hash;loaderSha256=(Get-FileHash $loader.FullName -Algorithm SHA256).Hash;sourceCommit=$sourceCommit}|ConvertTo-Json
