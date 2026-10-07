# Native UI verification

This dependency-free executable compiles the exact shipped UI files, while excluding Program.cs and all transport sources. It exercises real WinForms controls on an STA thread. Synthetic snapshots exist only in the test harness and are explicitly labeled test fixtures.

## Run

From the repository root, using the installed .NET 8 SDK:

```
dotnet run --project tests/QuietMSI.UITests/QuietMSI.UITests.csproj
dotnet run --project tests/QuietMSI.UITests/QuietMSI.UITests.csproj -- --smoke
dotnet run --project tests/QuietMSI.UITests/QuietMSI.UITests.csproj -- --smoke --screenshot <existing-directory>/smoke.png
```

The first command verifies real disabled hardware controls, initial telemetry visibility, non-overlapping asynchronous polling, honest temperature/fan source rendering, stale suspension/error state, no lifecycle writes, JSON export/readback, minimize/close-to-tray, explicit exit, cancellation during disposal, compact resizing and 150% control scaling. Scaling is a layout exercise, not proof of physical multi-monitor DPI switching.

The smoke commands run a finite real message loop using only ReadOnlyBridge. Screenshot output is created only when explicitly requested. The regular application never auto-closes.

## Integration

Program's STA entry point calls `QuietMSI.UIHost.Run(bridge, smokeTest, screenshotPath)`. UIHost enables visual styles and PerMonitorV2 DPI awareness before constructing controls. Regular mode uses the provided bridge; smoke ignores it and uses ReadOnlyBridge. The production project references Core plus System.Management 8.0.0 as requested by the transport owner. No UI source calls ApplyAsync or performs any transport operation.

## TDD evidence

Observed expected RED failures before implementation: missing native MainForm; missing PollAsync; missing JSON export; missing UIHost; missing TrayContext. Corresponding implementations passed those tests. A screenshot exposed initial scroll-to-focused-diagnostics hiding telemetry; InitialLayout first failed and then passed after initial focus moved to the header Refresh action. Updated vendor-RPM source semantics first failed FanSource and passed after the UI changed from “RPM measured” to “RPM reported,” retaining the supplied calibration caveat. TrayLifecycle also caught repeated-disposal of a CTS; the resource teardown is now idempotent.

## Visual direction

Native Operate surface: Segoe UI, charcoal machine/sampling header, light neutral telemetry and setting sections, muted blue diagnostics action. Familiar native selectors, buttons and accessibility names; one compact scrollable column. Hardware controls remain disabled with the Core policy's explicit firmware/evidence reasons. High contrast uses Windows system colors; no custom-painted controls, decorative assets or non-native dependencies.

Visual review used the actual read-only smoke screenshot. Initial telemetry, source/status and standard window chrome are visible; lower hardware/evidence sections scroll. Missing values in this mode are expected, not simulated machine readings. Parent owns final whole-project review, actual multi-monitor DPI/high-contrast verification and packaging documentation.
