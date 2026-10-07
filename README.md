# QuietMSI

A compact, independently written Windows tray application for MSI laptop telemetry research. MIT-licensed source, native C#/.NET 8 WinForms, no WebView/Electron, advertisements, analytics, auto-updater, RGB/peripheral utilities, accounts, or bundled driver.

**Version 0.2 corrects the unusable read-only workflow.** One Windows UAC prompt at launch enables actual protected MSI telemetry and verified explicit hardware changes on GF63 Thin 11UCX / EC16R6EMS1.106 / WMI2.8. Performance levels, Cooler Boost and Auto/Advanced fan mode are supported; a conservative CPU-curve editor allows cooling-only increases and exact session-original restoration. Battery limits, GPU/MUX, overclocking and full MSI scenario automation are not implemented. No changes occur on startup, polling, resume or exit.

## Run

Requires Windows x64 and [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0). This machine already has it. Extract the portable package and run QuietMSI.exe. The executable is framework-dependent; its small size does not include the installed runtime.

The executable requests administrator permission at startup using the standard Windows UAC manifest, as explicitly chosen by the user. No added driver/service, credential handling, ACL modification or signature bypass is used. Installed MSI Foundation service rejects unsigned third-party clients; merely loading its DLL does not grant compatibility, so v0.2 uses standard direct WMI. Existing MSI ACPI provider remains required. If UAC is declined the app does not silently run a broken unelevated control UI. Other firmware models remain blocked.

Close/minimize hides the window to tray. Use the tray menu to reopen or Exit. Diagnostics export is local and may contain machine identity; inspect before sharing. No automatic startup registration.

QuietMSI performs sampling in a short-lived read-only child of the same executable, with a10-second parent-enforced deadline. No installed background service is used. Each sample rechecks device and protected firmware identity; stale readings stay labeled stale. This adds brief process-start overhead and its resource usage is separate from the resident tray process.

### Curve and restoration limits

CPU editor is increases-only versus the table captured when this app session first reads the device. It does not lower fan effort, edit fixed temperature points, change GPU/shared-fan routing, or invent factory defaults. Restore returns that exact captured table only if current table still matches our last change. **Restore before exiting if you want the session-original curve back:** exit itself sends no hardware command, and captured baseline is not a persistent factory-reset database. Auto selects controller mode and clears boost; it does not undo a previously edited table automatically. The existing MSI Center may overwrite settings; conflicts/read-back drift are errors, not silently retried.

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

Normal hardware integration tests issue reads and refused unsupported requests only. The separately gated `--workflow-approved` / `--curve-approved` test modes actually change hardware and must never be run without informed approval. Actual user-approved tests and exact original-state restoration are recorded in docs/v2-verification.md. `QuietMSI.exe --diagnostics <path.json>` exports actual snapshot; `--smoke-test <path.png>` is a no-hardware UI fixture.

## Boundaries

Original MIT code only. Proprietary MSI DLLs, decompiled source, downloaded analysis tools and private probe logs are excluded. Linux GPL driver source is cited for attributed hardware facts, not copied into this code. Not affiliated with or endorsed by MSI. See [SECURITY.md](SECURITY.md).

The repository is local; no GitHub publication or remote upload has been performed. Build and runtime verification results are recorded in docs/verification.md. **The under30 MB working-set target is not met:** the resident tray process measured about69 MB before transient worker memory, and no aggregate worker CPU/memory pass is claimed. The portable executable is about551 KB and requires the separate .NET Desktop runtime.
