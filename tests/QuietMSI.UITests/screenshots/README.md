# UI captures

These captures are test artifacts, not live machine screenshots.

- `readonly-ui.png`: UIHost smoke with a fresh ReadOnlyBridge; no transport and no invented telemetry.
- `synthetic-ui.png`: native MainForm using SyntheticControlBridge, initial 560 × 740 client area.
- `synthetic-cooling.png`: the same synthetic form scrolled to cooling, including mode-only Auto/Advanced Apply and the existing six-point read-only curve.
- `synthetic-compact.png`: the same synthetic form resized to a 440 × 500 client area; lower settings require scrolling.

Regenerate (using the installed .NET 8 SDK):

```text
dotnet run --project tests/QuietMSI.UITests -c Release -- --synthetic-screenshots
dotnet run --project tests/QuietMSI.UITests -c Release -- --smoke --screenshot tests/QuietMSI.UITests/screenshots/readonly-ui.png
```

Tests compile the exact UI/UIHost sources without Program or hardware transport. Synthetic Apply only mutates fixture state and records requests. No hardware calls, elevation, or real hardware writes occur.
