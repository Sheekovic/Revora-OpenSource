$ErrorActionPreference='Stop'
$expected='6A70EE5CB0A84B41821523490012086EC2448348F8B86757F6B78ADC15F49341'
$source=Join-Path (Split-Path $PSScriptRoot -Parent) 'bin/Debug/signin.ini'
$target='C:/Revora/DisposablePostgres/moon-box-candidate/game/database/slayer/DayliSignIn/signin.ini'
if(!(Test-Path -LiteralPath $source) -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $expected){throw 'Reviewed sign-in source catalog is unavailable or changed.'}
if(Test-Path -LiteralPath $target) {
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $expected){throw 'Refusing to replace a different private sign-in catalog.'}
} else {
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
}
if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $expected){throw 'Private sign-in catalog verification failed.'}
Write-Output 'Reviewed sign-in catalog ready in the fixed private candidate only.'
