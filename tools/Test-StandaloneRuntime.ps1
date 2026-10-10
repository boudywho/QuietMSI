$ErrorActionPreference = 'Stop'
$root = 'C:\MyStuff\projects\QuietMSI'
$exe = "$root\artifacts\release-0.5.0\QuietMSI.exe"
$output = "$root\artifacts\standalone"
$names = @('MSI_Center_Service', 'MSI Foundation Service', 'MSI Sendevsvc')
$before = @(Get-Service -Name $names -ErrorAction SilentlyContinue | Select-Object Name,Status)
$result = [ordered]@{ StartedAt=[DateTimeOffset]::UtcNow; Success=$false; ServicesBefore=$before; Restored=$false }
try {
    $setup = Start-Process -FilePath $exe -ArgumentList '--install-support' -PassThru -Wait -WindowStyle Hidden
    if ($setup.ExitCode -ne 0) { throw "Support setup failed with code $($setup.ExitCode). See application.log." }
    $result.MofImagePath = (Get-ItemProperty -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Services\WmiAcpi').MofImagePath
    if ($result.MofImagePath -notlike 'C:\Program Files\QuietMSI\Support\QuietMSI.Firmware-*.dll') { throw 'Support resource was not independently registered.' }
    foreach ($name in $names) {
        $service = Get-Service -Name $name -ErrorAction SilentlyContinue
        if (!$service) { continue }
        if (@($service.DependentServices | Where-Object { $_.Status -eq 'Running' -and $_.Name -notin $names }).Count) { throw "Cannot isolate $name without stopping unrelated services." }
        if ($service.Status -eq 'Running') { Stop-Service -InputObject $service; $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(15)) }
    }
    $result.ServicesDuring = @(Get-Service -Name $names -ErrorAction SilentlyContinue | Select-Object Name,Status)
    $snapshots = foreach ($index in 1..3) {
        $path = "$output\independent-read-$index.json"
        $read = Start-Process -FilePath $exe -ArgumentList '--diagnostics', $path -PassThru -Wait -WindowStyle Hidden
        if ($read.ExitCode -ne 0) { throw "Read failed with code $($read.ExitCode)." }
        $snapshot = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if ($snapshot.Identity.EcFirmware -ne '16R6EMS1.106' -or $null -eq $snapshot.CpuCelsius -or $null -eq $snapshot.GpuCelsius -or $null -eq $snapshot.Controls) { throw 'Protected telemetry or control state unavailable without MSI services.' }
        $snapshot
    }
    if (@(Get-Service -Name $names -ErrorAction SilentlyContinue | Where-Object Status -ne 'Stopped').Count) { throw 'An MSI service restarted during the test.' }
    $result.Snapshots = @($snapshots)
    $result.Success = $true
} catch { $result.Error = $_.Exception.ToString() }
finally {
    $restoreErrors = @()
    foreach ($service in ($before | Where-Object { $_.Status -eq 'Running' })) {
        try { Start-Service -Name $service.Name; (Get-Service -Name $service.Name).WaitForStatus('Running', [TimeSpan]::FromSeconds(15)) }
        catch { $restoreErrors += $_.Exception.Message }
    }
    $result.ServicesAfter = @(Get-Service -Name $names -ErrorAction SilentlyContinue | Select-Object Name,Status)
    $result.Restored = $restoreErrors.Count -eq 0
    $result.RestoreErrors = $restoreErrors
    $result.FinishedAt = [DateTimeOffset]::UtcNow
    $result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath "$output\independence-verification.json"
}
if (!$result.Success -or !$result.Restored) { exit 1 }
