param(
    [Parameter(Mandatory)][string]$PreviousSetup,
    [Parameter(Mandatory)][string]$CurrentSetup,
    [Parameter(Mandatory)][string]$StartupTest,
    [Parameter(Mandatory)][string]$Output
)
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run this isolated installer verification elevated.' }
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$outputRoot = (Resolve-Path -LiteralPath $Output).Path
$testDirectory = [IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'QuietMSI-Installer-Verification'))
if (Test-Path -LiteralPath $testDirectory) { throw 'The isolated verification directory already exists; refusing to overwrite it.' }
$key = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\QuietMSI.Verification_is1'
if (Test-Path -LiteralPath $key) { throw 'A verification installation is already registered.' }
$result = [ordered]@{ Success = $false; StartedAt = [DateTimeOffset]::UtcNow }
$scheduler = New-Object -ComObject Schedule.Service
$scheduler.Connect()
$folder = $scheduler.GetFolder('\QuietMSI')
$realName = 'Startup-' + $identity.User.Value
$beforeTask = try { $folder.GetTask($realName).Xml } catch { $null }
$themePath = Join-Path $env:LOCALAPPDATA 'QuietMSI/appearance.json'
$themeBefore = if (Test-Path -LiteralPath $themePath) { (Get-FileHash -LiteralPath $themePath).Hash } else { $null }
$testTasks = @()
function Run-Setup([string]$Path, [string]$LogName, [bool]$Initial = $false) {
    $arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + (Join-Path $outputRoot $LogName) + '"'),'/TASKS=desktopicon')
    if ($Initial) { $arguments += '/DIR="' + $testDirectory + '"' }
    $process = Start-Process -FilePath $Path -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    return $process.ExitCode
}
function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
try {
    Assert ((Run-Setup $PreviousSetup 'install.log' $true) -eq 0) 'Initial installation failed.'
    $exe = Join-Path $testDirectory 'QuietMSI.exe'
    Assert ((Get-Item -LiteralPath $exe).VersionInfo.ProductVersion -like '0.5.0*') 'Initial file version mismatch.'
    Assert ((Get-ItemProperty -LiteralPath $key).DisplayVersion -eq '0.5.0') 'Initial uninstall entry mismatch.'
    $startLink = Join-Path ([Environment]::GetFolderPath('CommonStartMenu')) 'Programs/QuietMSI Verification.lnk'
    $desktopLink = Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'QuietMSI Verification.lnk'
    Assert ((Test-Path -LiteralPath $startLink) -and (Test-Path -LiteralPath $desktopLink)) 'Requested shortcuts are missing.'
    $shell = New-Object -ComObject WScript.Shell
    Assert ($shell.CreateShortcut($startLink).TargetPath -eq $exe) 'Start menu shortcut target mismatch.'
    $result.InitialInstall = $true

    Assert ((Run-Setup $CurrentSetup 'upgrade.log') -eq 0) 'Upgrade failed.'
    Assert ((Get-Item -LiteralPath $exe).VersionInfo.ProductVersion -like '0.6.0*') 'Upgrade file version mismatch.'
    Assert ((Get-ItemProperty -LiteralPath $key).DisplayVersion -eq '0.6.0') 'Upgrade did not reuse the uninstall entry.'
    $result.InPlaceUpgrade = $true
    $result.PreviousDirectoryReused = $true
    $read = Start-Process -FilePath $exe -ArgumentList @('--diagnostics', ('"' + (Join-Path $outputRoot 'installed-telemetry.json') + '"')) -WindowStyle Hidden -PassThru -Wait
    Assert ($read.ExitCode -eq 0) 'Installed standalone telemetry failed.'
    $result.InstalledTelemetry = $true

    Set-Content -LiteralPath (Join-Path $testDirectory 'README.md') -Value 'repair fixture'
    Assert ((Run-Setup $CurrentSetup 'repair.log') -eq 0) 'Same-version repair failed.'
    Assert ((Get-Content -LiteralPath (Join-Path $testDirectory 'README.md') -Raw) -ne "repair fixture`r`n") 'Repair did not replace application content.'
    $result.Repair = $true
    Assert ((Run-Setup $PreviousSetup 'downgrade.log') -ne 0) 'Older installer unexpectedly succeeded.'
    Assert ((Get-Item -LiteralPath $exe).VersionInfo.ProductVersion -like '0.6.0*') 'Downgrade changed the executable.'
    Assert ((Get-Content -LiteralPath (Join-Path $outputRoot 'downgrade.log') -Raw) -like '*newer version*') 'Downgrade failed for an unexpected reason.'
    $result.DowngradeBlocked = $true

    $startup = Start-Process -FilePath $StartupTest -ArgumentList @(('"' + $exe + '"'), ('"' + (Join-Path $outputRoot 'startup-migration.json') + '"')) -WindowStyle Hidden -PassThru -Wait
    Assert ($startup.ExitCode -eq 0) 'Isolated real startup migration failed.'
    $result.StartupMigration = $true

    # Disabled fixtures cannot run at logon. Use the real task only as an XML template.
    if ($beforeTask) {
        $suffix = [Guid]::NewGuid().ToString('N')
        foreach ($kind in @('Installed','Portable','Foreign')) {
            $name = 'InstallerVerification-' + $kind + '-' + $suffix
            [xml]$xml = $beforeTask
            $xml.Task.Actions.Exec.Command = if ($kind -eq 'Portable') { $xml.Task.Actions.Exec.Command } else { $exe }
            if ($kind -eq 'Foreign') { $xml.Task.RegistrationInfo.Source = 'QuietMSI.Test.Foreign' }
            $task = $folder.RegisterTask($name, $xml.OuterXml, 6, $identity.User.Value, $null, 3, $null)
            $task.Enabled = $false
            $testTasks += $name
        }
    }
    $uninstaller = Join-Path $testDirectory 'unins000.exe'
    $remove = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + (Join-Path $outputRoot 'uninstall.log') + '"')) -WindowStyle Hidden -PassThru -Wait
    Assert ($remove.ExitCode -eq 0) 'Uninstall failed.'
    Assert (-not (Test-Path -LiteralPath $exe)) 'Uninstall retained the installed executable.'
    Assert (-not (Test-Path -LiteralPath $key)) 'Uninstall retained the registration.'
    Assert (-not (Test-Path -LiteralPath $startLink) -and -not (Test-Path -LiteralPath $desktopLink)) 'Uninstall retained shortcuts.'
    if ($testTasks.Count) {
        $ownedRemoved = try { $null = $folder.GetTask($testTasks[0]); $false } catch { $true }
        Assert $ownedRemoved 'Uninstall retained its owned startup task.'
        Assert ($null -ne $folder.GetTask($testTasks[1])) 'Uninstall removed a portable task.'
        Assert ($null -ne $folder.GetTask($testTasks[2])) 'Uninstall removed a foreign task.'
        $result.UninstallTaskOwnership = $true
    }
    $result.Uninstall = $true
    $afterTask = try { $folder.GetTask($realName).Xml } catch { $null }
    Assert ($beforeTask -eq $afterTask) 'The actual user startup setting changed.'
    $themeAfter = if (Test-Path -LiteralPath $themePath) { (Get-FileHash -LiteralPath $themePath).Hash } else { $null }
    Assert ($themeBefore -eq $themeAfter) 'The actual appearance preference changed.'
    $result.UserPreferencesPreserved = $true
    $result.Success = $true
}
catch { $result.Error = $_.Exception.ToString() }
finally {
    foreach ($name in $testTasks) { try { $folder.DeleteTask($name, 0) } catch { } }
    $result.CompletedAt = [DateTimeOffset]::UtcNow
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outputRoot 'installer-verification.json')
}
if (-not $result.Success) { exit 1 }
