param([string]$Dotnet = 'dotnet', [string]$Iscc = '')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $PSScriptRoot
try {
    if (-not $Iscc) {
        $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
        $candidates = @(
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'),
            (Join-Path $PSScriptRoot 'artifacts/packaging/inno/ISCC.exe')
        )
        $Iscc = if ($compiler) { $compiler.Source } else { $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1 }
    }
    if (-not $Iscc -or -not (Test-Path -LiteralPath $Iscc)) { throw 'Install Inno Setup 6.6+ and pass -Iscc with its ISCC.exe path.' }
    $Iscc = (Resolve-Path -LiteralPath $Iscc).Path
    $version = ([xml](Get-Content src/QuietMSI/QuietMSI.csproj -Raw)).Project.PropertyGroup.Version
    foreach ($project in @('QuietMSI.Tests', 'QuietMSI.HardwareTests', 'QuietMSI.UITests', 'QuietMSI.UpdateTests')) {
        & $Dotnet run --project "tests/$project/$project.csproj" -c Release
        if ($LASTEXITCODE -ne 0) { throw "$project failed" }
    }
    & $Dotnet run --project tests/QuietMSI.UITests/QuietMSI.UITests.csproj -c Release --no-build -- --stability
    if ($LASTEXITCODE -ne 0) { throw 'UI stability stress tests failed' }
    $root = Join-Path $PSScriptRoot "artifacts/releases/$version/payload"
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    & $Dotnet publish src/QuietMSI/QuietMSI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $root
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    & ./tools/Test-MofResource.ps1 -Dll (Resolve-Path src/QuietMSI.Firmware/bin/Release/net8.0/QuietMSI.Firmware.dll) -ExpectedBinary src/QuietMSI.Firmware/obj/Release/net8.0/QuietMSI.bmf -Result "artifacts/releases/$version/firmware-resource-verification.json"
    Copy-Item LICENSE,README.md,SECURITY.md,THIRD-PARTY-NOTICES.md -Destination $root
    & ./tools/Copy-RuntimeNotices.ps1 -Destination $root
    New-Item -ItemType Directory -Path (Join-Path $root 'docs') -Force | Out-Null
    Copy-Item docs/hardware-specs.md,docs/stability-verification.md,docs/standalone-verification.md,docs/distribution.md,docs/release-verification.md -Destination (Join-Path $root 'docs')
    Copy-Item -LiteralPath docs/images -Destination (Join-Path $root 'docs') -Recurse -Force
    $output = Join-Path $PSScriptRoot 'artifacts'
    $zip = Join-Path $output "QuietMSI-$version-Portable-win-x64.zip"
    Compress-Archive -Path (Join-Path $root '*') -DestinationPath $zip -Force
    & $Iscc "/DPayloadDir=$root" "/DOutputPath=$output" "/DAppVersion=$version" installer/QuietMSI.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
    $setup = Join-Path $output "QuietMSI-$version-Setup-x64.exe"
    $files = @($setup, $zip)
    $checksums = $files | ForEach-Object { Get-FileHash -LiteralPath $_ -Algorithm SHA256 }
    $checksums | ForEach-Object { "$($_.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($_.Path))" } | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
    $checksums | Select-Object @{Name='File';Expression={[IO.Path]::GetFileName($_.Path)}},Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'checksums.json')
    Get-Item -LiteralPath $files | Select-Object FullName,Length
    $checksums | Select-Object Hash,Path
} finally { Pop-Location }
