# MSI-Helper Phase 0 Bootstrap Inventory Collector
# Execution Policy: Safe Read-Only Queries (CIM / WMI / Appx / Filesystem)

$ErrorActionPreference = "Continue"
$Out = "$env:USERPROFILE\Desktop\MSI-RE-Inventory"
New-Item -ItemType Directory -Force $Out | Out-Null

Write-Host "[+] Collecting system information..." -ForegroundColor Cyan

"=== OS ===" | Out-File "$Out\system.txt"
Get-CimInstance Win32_OperatingSystem |
    Select-Object Caption, Version, BuildNumber, OSArchitecture |
    Format-List | Out-File "$Out\system.txt" -Append

"`n=== COMPUTER ===" | Out-File "$Out\system.txt" -Append
Get-CimInstance Win32_ComputerSystem |
    Select-Object Manufacturer, Model, SystemType |
    Format-List | Out-File "$Out\system.txt" -Append

"`n=== PRODUCT ===" | Out-File "$Out\system.txt" -Append
Get-CimInstance Win32_ComputerSystemProduct |
    Select-Object Vendor, Name, Version |
    Format-List | Out-File "$Out\system.txt" -Append

"`n=== BIOS ===" | Out-File "$Out\system.txt" -Append
Get-CimInstance Win32_BIOS |
    Select-Object Manufacturer, SMBIOSBIOSVersion, ReleaseDate |
    Format-List | Out-File "$Out\system.txt" -Append

"`n=== BASEBOARD ===" | Out-File "$Out\system.txt" -Append
Get-CimInstance Win32_BaseBoard |
    Select-Object Manufacturer, Product, Version |
    Format-List | Out-File "$Out\system.txt" -Append

"`n=== CPU ===" | Out-File "$Out\system.txt" -Append
Get-CimInstance Win32_Processor |
    Select-Object Name |
    Format-List | Out-File "$Out\system.txt" -Append

"`n=== GPU ===" | Out-File "$Out\system.txt" -Append
Get-CimInstance Win32_VideoController |
    Select-Object Name, DriverVersion, PNPDeviceID |
    Format-List | Out-File "$Out\system.txt" -Append

Write-Host "[+] Collecting MSI Appx packages..." -ForegroundColor Cyan
Get-AppxPackage |
    Where-Object {
        $_.Name -match 'MSI|Dragon|Creator' -or
        $_.Publisher -match 'Micro-Star'
    } |
    Select-Object Name, PackageFullName, Version, InstallLocation |
    Format-List |
    Out-File "$Out\msi-appx.txt"

Write-Host "[+] Collecting MSI Services..." -ForegroundColor Cyan
Get-CimInstance Win32_Service |
    Where-Object {
        $_.Name -match 'MSI|Dragon|Creator|Mystic' -or
        $_.DisplayName -match 'MSI|Dragon|Creator|Mystic'
    } |
    Select-Object Name, DisplayName, State, StartMode, PathName |
    Format-Table -AutoSize |
    Out-File "$Out\msi-services.txt"

Write-Host "[+] Collecting MSI Drivers..." -ForegroundColor Cyan
Get-CimInstance Win32_SystemDriver |
    Where-Object {
        $_.Name -match 'MSI|Dragon|Mystic' -or
        $_.DisplayName -match 'MSI|Dragon|Mystic' -or
        $_.PathName -match 'MSI'
    } |
    Select-Object Name, DisplayName, State, StartMode, PathName |
    Format-Table -AutoSize |
    Out-File "$Out\msi-drivers.txt"

Get-CimInstance Win32_PnPSignedDriver |
    Where-Object {
        $_.DriverProviderName -match 'Micro-Star|MSI'
    } |
    Select-Object DeviceName, DriverProviderName, DriverVersion, InfName |
    Sort-Object DeviceName |
    Format-Table -AutoSize |
    Out-File "$Out\msi-pnp-drivers.txt"

Write-Host "[+] Collecting MSI Processes..." -ForegroundColor Cyan
Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -match 'MSI|Dragon|Creator|Mystic'
    } |
    Select-Object ProcessId, Name, ExecutablePath, CommandLine |
    Format-List |
    Out-File "$Out\msi-processes.txt"

Write-Host "[+] Enumerating root\wmi MSI classes..." -ForegroundColor Cyan
Get-CimClass -Namespace root\wmi -ErrorAction SilentlyContinue |
    Where-Object {
        $_.CimClassName -match 'MSI'
    } |
    ForEach-Object {
        [PSCustomObject]@{
            Class   = $_.CimClassName
            Methods = ($_.CimClassMethods.Keys -join ', ')
        }
    } |
    Format-Table -AutoSize |
    Out-File "$Out\msi-wmi-classes.txt"

Write-Host "[+] Cataloging MSI installed files..." -ForegroundColor Cyan
$MSIPaths = @(
    'C:\Program Files\MSI',
    'C:\Program Files (x86)\MSI',
    'C:\ProgramData\MSI'
)

foreach ($Path in $MSIPaths) {
    if (Test-Path $Path) {
        Get-ChildItem $Path -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.Extension -in '.exe','.dll','.sys','.json','.xml','.config' } |
            Select-Object FullName, Length,
                @{N='FileVersion';E={$_.VersionInfo.FileVersion}},
                @{N='ProductVersion';E={$_.VersionInfo.ProductVersion}} |
            Export-Csv "$Out\msi-files-$((Split-Path $Path -Leaf) -replace '[^a-zA-Z0-9]','_').csv" `
                -NoTypeInformation
    }
}

Write-Host "`n[✓] Inventory collection complete. Results saved to:" -ForegroundColor Green
Write-Host "    $Out" -ForegroundColor Green
