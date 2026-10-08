param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$assets = Get-Content (Join-Path $projectRoot 'src/QuietMSI/obj/project.assets.json') -Raw | ConvertFrom-Json
$frameworks = $assets.project.frameworks.PSObject.Properties | Select-Object -First 1 -ExpandProperty Value
$runtimePackages = @($frameworks.downloadDependencies | Where-Object { $_.name -in @('Microsoft.NETCore.App.Runtime.win-x64', 'Microsoft.WindowsDesktop.App.Runtime.win-x64') })
if ($runtimePackages.Count -ne 2) { throw 'Could not resolve published runtime packages.' }
$target = Join-Path $Destination 'licenses'
New-Item -ItemType Directory -Path $target -Force | Out-Null
foreach ($package in $runtimePackages) {
    $version = $package.version.Trim('[', ']').Split(',')[0].Trim()
    $relative = Join-Path $package.name.ToLowerInvariant() $version
    $source = $null
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $candidate = Join-Path $folder $relative
        if (Test-Path -LiteralPath $candidate) { $source = $candidate; break }
    }
    if (!$source) { throw "Runtime license package missing: $relative" }
    $notices = @(Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Name -like 'LICENSE*' -or $_.Name -like '*THIRD-PARTY-NOTICES*' })
    if (!$notices.Count) { throw "Runtime license missing: $relative" }
    foreach ($notice in $notices) { Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $target ($package.name + '-' + $notice.Name)) }
}
