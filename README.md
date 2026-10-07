# QuietMSI

A compact, independently written Windows tray application for MSI laptop telemetry research. MIT-licensed source, native C#/.NET 8 WinForms, no WebView/Electron, advertisements, analytics, auto-updater, RGB/peripheral utilities, accounts, or bundled driver.

**Initial release is read-only, not a complete MSI Center replacement.** Performance, Cooler Boost, charge limits, curves, Auto and MUX controls are unavailable until their full safety contracts are verified. They are blocked in the backend, not just grayed out in the UI. No hardware setting changes occur on startup, polling, resume or exit.

## Run

Requires Windows x64 and [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0). This machine already has it. Extract the portable package and run QuietMSI.exe. The executable is framework-dependent; its small size does not include the installed runtime.

The app does not automatically request administrator access. Windows battery/power status and model/BIOS identification work unelevated. This machine requires elevated permission for protected MSI WMI getters; normal mode reports unavailable sensors and the precise error. If you choose to run the verified read-only build as administrator, the narrow telemetry path requires the observed GF63 Thin 11UCX / EC 16R6EMS1.106 / WMI 2.8 match. Other firmware remains unsupported for protected sensors. No elevation bypass, MSI helper service or ACL modification is used.

Close/minimize hides the window to tray. Use the tray menu to reopen or Exit. Diagnostics export is local and may contain machine identity; inspect before sharing. No automatic startup registration.

QuietMSI performs sampling in a short-lived read-only child of the same executable, with a10-second parent-enforced deadline. No installed background service is used. Each sample rechecks device and protected firmware identity; stale readings stay labeled stale. This adds brief process-start overhead and its resource usage is separate from the resident tray process.

## Telemetry interpretation

- Temperature getter: CPU/GPU Celsius, zero/invalid readings unavailable.
- First fan channel: **vendor-reported RPM**, reciprocal tachometer-period formula; not independently calibrated against HWiNFO/LibreHardwareMonitor yet.
- Zero second fan period does not imply a stopped fan. Second channel is unavailable until validated.
- Fan target percentage and measured/vendor RPM are different quantities; no percentage-to-RPM guess.
- Battery percentage and AC power: standard Windows power API.

See [hardware-specs.md](docs/hardware-specs.md) for exact mappings, static evidence, live probe results and unresolved safety gates. Do not uninstall MSI Center or its provider based on this build: provider survival after removal has not been tested.

## Build from source

Install .NET 8 SDK, then from this directory:

```powershell
.\build.ps1
```

Or pass a local SDK path with `-Dotnet 'C:\path\dotnet.exe'`. NuGet.Config enables the official NuGet feed for build-time packages. Product operation has no network functionality. System.Management 8.0.0 is the only package; WinForms and core runtime come from Microsoft .NET.

Independent tests:

```text
dotnet run --project tests/QuietMSI.Tests -c Release
dotnet run --project tests/QuietMSI.HardwareTests -c Release
dotnet run --project tests/QuietMSI.UITests -c Release
```

Hardware integration tests issue allowed reads only and assert all writes remain refused. Tests do not exercise live write commands. `QuietMSI.exe --diagnostics <path.json>` writes a real local snapshot and exits; `--smoke-test <path.png>` uses unavailable-data fixtures to test UI without touching hardware.

## Boundaries

Original MIT code only. Proprietary MSI DLLs, decompiled source, downloaded analysis tools and private probe logs are excluded. Linux GPL driver source is cited for attributed hardware facts, not copied into this code. Not affiliated with or endorsed by MSI. See [SECURITY.md](SECURITY.md).

The repository is local; no GitHub publication or remote upload has been performed. Build and runtime verification results are recorded in docs/verification.md. **The under30 MB working-set target is not met:** the resident tray process measured about69 MB before transient worker memory, and no aggregate worker CPU/memory pass is claimed. The portable executable is about551 KB and requires the separate .NET Desktop runtime.
