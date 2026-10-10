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

Program's STA entry point calls `QuietMSI.UIHost.Run(bridge, smokeTest, screenshotPath)`. UIHost enables visual styles and PerMonitorV2 DPI awareness before constructing controls. Regular mode uses the provided bridge; smoke ignores it and uses ReadOnlyBridge. The production project references Core plus System.Management 8.0.0 as requested by the transport owner. MainForm sends explicit requests through IHardwareBridge.ApplyAsync and verifies fresh readback. UI code never performs raw hardware transport operations.

## TDD evidence

Observed expected RED failures before implementation: missing native MainForm; missing PollAsync; missing JSON export; missing UIHost; missing TrayContext. Corresponding implementations passed those tests. A screenshot exposed initial scroll-to-focused-diagnostics hiding telemetry; InitialLayout first failed and then passed after initial focus moved to the header Refresh action. Updated vendor-RPM source semantics first failed FanSource and passed after the UI changed from “RPM measured” to “RPM reported,” retaining the supplied calibration caveat. TrayLifecycle also caught repeated-disposal of a CTS; the resource teardown is now idempotent.

## Visual direction (0.4)

The native Controls view contains a single set of telemetry readings, four visible firmware profile buttons and grouped cooling controls. Fan curve holds the chart and six bounded inputs. Diagnostics navigation and the GUI export action are removed. Sensor/provider/capability details remain available through tooltips and accessibility descriptions; the local JSON API is still verified.

Light and Dark use Segoe UI, neutral surfaces, thin borders and teal selections/Apply actions. The executable, window, header and tray share the original Q/airflow icon. Small windows scroll; manual geometry follows device DPI. See docs/ui-overhaul.md and DESIGN.md.

    dotnet run --project tests/QuietMSI.UITests -c Release -- --synthetic-screenshots

This produces eight explicitly labeled synthetic fixture views and provenance hashes in artifacts/ui-overhaul, including Settings in both themes. Actual desktop captures verify the remaining fan-mode and appearance dropdowns. Native Windows arrow styling must be judged from desktop capture, not DrawToBitmap. Full physical multi-monitor DPI switching and screen-reader/high-contrast verification remain untested.

Settings tests cover pending startup loading, verified enable, failed removal rollback, theme location and compact/wide card layout. Startup definition tests cover current-user scope, elevation without passwords, XML escaping, battery operation, unlimited execution time, and the stable Source ownership marker. Tray startup remains hidden, creates its message receiver, polls every 30 seconds, and reopens on a normal second launch. The separately invoked `QuietMSI.StartupTests` integration harness uses a temporary GUID-suffixed Windows task and removes it; it never enables the user's actual startup preference.

The test harness restores the WinForms synchronization context after each DoEvents pump so held-read continuations remain on the STA thread, matching the production Application.Run loop.

## Stability regression (0.4.1)

    dotnet run --project tests/QuietMSI.UITests -c Release -- --stability

The suite checks full-card invalidation after scrollbar-width expansion, 200 unchanged/changing-temperature samples without bounds/font/enabled/handle churn, unchanged hidden capability reasons, hidden-window layout retention, queued Apply/read serialization and cancellation, actual HWND caption styles and client extent after activation/theme/resize, reopening messages, and bounded error logging. A finite real Application.Run stress test runs 1,000 samples with repeated failures/recovery and page/theme/window changes, comparing GDI and USER resources after warm-up. All hardware writes in these workflows go to a synthetic test bridge. build.ps1 includes this stress suite.

Physical multi-monitor DPI transitions and prolonged hardware endurance remain separate from these finite regressions. See docs/stability-verification.md.
