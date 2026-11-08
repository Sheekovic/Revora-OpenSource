$ErrorActionPreference = 'Stop'

$templatePath = Join-Path $PSScriptRoot 'Build-NativeLoginOwnerTest2.ps1'
$recipe = [IO.File]::ReadAllText($templatePath)
$replacements = @(
    @('native-login-packet-binding-test2-package', 'active-native-login-path-test4-package'),
    @('PlayRevora-Native-Login-Packet-Binding-OwnerTest-2-20260901', 'PlayRevora-Active-Native-Login-Path-OwnerTest-4-20260901'),
    @('out\bridge-test2\Release', 'out\bridge-test3\Release'),
    @('owner-test-7630-native-login-packet-binding-test-2', 'owner-test-7630-active-native-login-path-test-4'),
    @('DirectX 9 - NATIVE LOGIN OWNER-TEST 2', 'DirectX 9 - ACTIVE NATIVE LOGIN OWNER-TEST 4'),
    @('NATIVE LOGIN PACKET BINDING OWNER-TEST 2', 'ACTIVE NATIVE LOGIN PATH OWNER-TEST 4'),
    @('Native Login Packet Binding Owner-Test 2', 'Active Native Login Path Owner-Test 4'),
    @('native-login-owner-test-2.jsonl', 'native-login-owner-test-4.jsonl'),
    @('C:\Users\Administrator\Desktop\launcher tests\PlayRevora-Launcher-DirectProfile-L1-1-OwnerTest-20260901', 'C:\Users\Administrator\Desktop\launcher-tests\PlayRevora-Launcher-DirectProfile-L1-1-OwnerTest-20260901')
)
foreach ($entry in $replacements) { $recipe = $recipe.Replace($entry[0], $entry[1]) }
& ([ScriptBlock]::Create($recipe))
