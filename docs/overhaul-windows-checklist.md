# QuietMSI overhaul: Windows acceptance

This branch was prepared on Linux using source inspection only. The application,
installer, hardware polling and native UI have **not** been built or run for
this overhaul. Older verification files and screenshots describe previous builds.

## Compile and package

On Windows x64, with the .NET 8 SDK and Windows WMI tools installed, run the
compile-only gate from the repository root:

```powershell
dotnet build .\src\QuietMSI\QuietMSI.csproj -c Release
```

For a separate acceptance run on Windows (`build.ps1` also needs Inno Setup):

```powershell
dotnet run --project .\tests\QuietMSI.UITests -c Release
dotnet run --project .\tests\QuietMSI.UITests -c Release -- --synthetic-screenshots
.\build.ps1
```

The UI fixture does not access the hardware transport. Screenshot and pixel
checks need an interactive desktop. The full packaging script also runs
read-only integration and UI stability checks. Don't enable the separate
hardware-write acceptance options unless explicitly approved.

## Desktop checks

1. **Start and tray:** open normally and via `--startup`; confirm the window and
   tray are responsive while setup runs. Confirm a second launch restores the
   same window. Close and minimize to tray; use the tray menu for actual exit.
2. **Resize and access:** at 100%, 125%, and 150% display scaling, check Overview,
   Cooling and Settings with both a compact and a wide window. Move between
   monitors if available. Check Tab/Shift+Tab reveal, focus indicators, native
   window controls, high contrast, and Narrator's readings and status text.
3. **Polling:** with a draft selected, wait through several samples. Values may
   change, but focus, draft and scroll position should not. Minimize and reopen;
   confirm a fresh sample. Sleep and resume; report stale state until a fresh
   sample arrives. Compare idle CPU and working set against the same hardware
   and driver setup on the previous release, both visible and in the tray.
4. **Apply feedback:** use the UI fixture to check successful and failed readback,
   duplicate clicks, unsent request cancellation, and draft retention. Check
   that a stalled operation shows an unconfirmed status and is not retried.
5. **Cooling:** check that the dashed draft line tracks edits and the solid line
   stays on the last reported values. Verify the six inputs, fixed temperatures,
   invalid draft feedback, and clearly labeled session-original Restore.
6. **Read-only and packages:** confirm unknown or unsupported firmware does not
   enable writes. Check appearance persistence, manual updates, startup opt-in,
   installer upgrade and portable launch. Re-capture the README image after
   confirming the new native UI if a current screenshot is wanted.
