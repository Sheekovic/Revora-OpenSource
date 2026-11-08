$ErrorActionPreference = 'Stop'

# Test 2 is immutable failure evidence. Reuse its reviewed packaging recipe in
# memory while producing distinct Test-3 paths, labels, hashes, and artifacts.
$templatePath = Join-Path $PSScriptRoot 'Build-NativeLoginOwnerTest2.ps1'
$recipe = [IO.File]::ReadAllText($templatePath)
$replacements = @(
    @('native-login-packet-binding-test2-package', 'native-login-packet-binding-test3-package'),
    @('PlayRevora-Native-Login-Packet-Binding-OwnerTest-2-20260901', 'PlayRevora-Native-Login-Packet-Binding-OwnerTest-3-20260901'),
    @('out\bridge-test2\Release', 'out\bridge-test3\Release'),
    @('owner-test-7630-native-login-packet-binding-test-2', 'owner-test-7630-native-login-packet-binding-test-3'),
    @('DirectX 9 - NATIVE LOGIN OWNER-TEST 2', 'DirectX 9 - NATIVE LOGIN OWNER-TEST 3'),
    @('NATIVE LOGIN PACKET BINDING OWNER-TEST 2', 'NATIVE LOGIN PACKET BINDING OWNER-TEST 3'),
    @('Native Login Packet Binding Owner-Test 2', 'Native Login Packet Binding Owner-Test 3'),
    @('native-login-owner-test-2.jsonl', 'native-login-owner-test-3.jsonl')
)
foreach ($entry in $replacements) {
    $recipe = $recipe.Replace($entry[0], $entry[1])
}
& ([ScriptBlock]::Create($recipe))
