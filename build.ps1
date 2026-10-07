param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $PSScriptRoot
try {
    & $Dotnet run --project tests/QuietMSI.Tests/QuietMSI.Tests.csproj -c Release
    if($LASTEXITCODE -ne 0){throw 'Core tests failed'}
    & $Dotnet run --project tests/QuietMSI.HardwareTests/QuietMSI.HardwareTests.csproj -c Release
    if($LASTEXITCODE -ne 0){throw 'Read-only Windows integration tests failed'}
    & $Dotnet run --project tests/QuietMSI.UITests/QuietMSI.UITests.csproj -c Release
    if($LASTEXITCODE -ne 0){throw 'UI tests failed'}
    $root=Join-Path $PSScriptRoot 'artifacts/portable'
    if(Test-Path -LiteralPath $root){Get-ChildItem -LiteralPath $root -File | Remove-Item}
    & $Dotnet publish src/QuietMSI/QuietMSI.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/portable
    if($LASTEXITCODE -ne 0){throw 'Publish failed'}
    Copy-Item LICENSE,README.md,SECURITY.md,THIRD-PARTY-NOTICES.md -Destination artifacts/portable
    New-Item -ItemType Directory -Path artifacts/portable/docs -Force | Out-Null
    Copy-Item docs/hardware-specs.md,docs/verification.md -Destination artifacts/portable/docs
    Get-Item artifacts/portable/QuietMSI.exe | Select-Object FullName,Length
    Get-FileHash artifacts/portable/QuietMSI.exe -Algorithm SHA256
} finally { Pop-Location }
