# Installer, portable package and upgrades

Both editions contain the same x64 executable, embedded icon, .NET Desktop runtime,
and independently authored firmware support. Neither needs MSI Center or a separate
.NET installation. The current hardware allowlist remains GF63 Thin 11UCX.

## Installer

Run `QuietMSI-0.6.0-Setup-x64.exe`, approve Windows UAC, and follow the wizard.
It installs into Program Files by default, adds a Start menu shortcut, optionally
adds a desktop shortcut, and registers QuietMSI in Windows Installed apps.
The wizard follows the Windows light/dark theme. Hardware support is prepared
during installation and failures are reported, rather than silently ignored.

Updates are installed by running a newer QuietMSI installer. The stable application
identity reuses the installation directory and uninstall record. Your appearance
preference and startup opt-in are preserved. An enabled startup task for the
installing Windows user is migrated to the installed executable; disabled or absent
tasks stay off. This also handles moving from a portable copy to the installed edition.
Settings offers manual checks against `boudywho/QuietMSI` GitHub releases, including
experimental releases. Only newer releases with both packages attached are offered.
The releases button opens GitHub in the browser; the app never downloads or executes
an update in the background. Checks have time and response-size limits.

Exit QuietMSI using its tray menu before installing an update or uninstalling.
Setup checks the application mutex and requests closure if it is still running,
so it cannot replace the executable during a hardware transaction. A newer
installed executable blocks an older installer from downgrading it. Rerunning
the same version repairs the application files.

Uninstall using Windows Installed apps. This removes application files, shortcuts
and QuietMSI-owned startup tasks pointing to that installed executable, including
tasks for other users. Tasks for portable copies are left intact. Per-user preferences
and logs are retained so reinstalling preserves them. The small shared firmware
support folder and its WMI registration are retained: portable copies use the same
resource and deleting it would leave Windows pointing to a missing DLL. No service
or additional kernel driver is installed. Hardware settings are not reset on uninstall.

## Portable

Extract `QuietMSI-0.6.0-Portable-win-x64.zip` into a stable folder, then launch
`QuietMSI.exe`. It creates no Installed apps entry or shortcuts. Exit the tray app
before replacing the folder with a newer portable release. If the executable path
changes, enable Start with Windows again to update the task's path.

Portable describes application deployment, not a promise of zero machine changes:
hardware access still requires UAC and the shared firmware support under Program
Files. Preferences and logs use `%LOCALAPPDATA%\QuietMSI` in both editions.
Disable Start with Windows before deleting a portable folder.

## Release build

`build.ps1` runs the core, read-only hardware, UI and stability checks, publishes
a fresh self-contained payload into a versioned staging folder, validates the native
firmware resource, bundles runtime licenses, creates the portable ZIP, compiles
the installer and writes SHA-256 checksums. It does not overwrite a running copy.

Install Inno Setup 6.6 or newer from the official JRSoftware download page.
Pass `-Iscc C:\path\ISCC.exe` if it is not on PATH, and `-Dotnet` for a local .NET SDK.
The installer is compiled from `installer/QuietMSI.iss`. Its fixed AppId must remain
unchanged in future releases. These local artifacts are unsigned; no signing
certificate or release hosting is configured.
