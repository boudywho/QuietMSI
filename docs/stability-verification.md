# QuietMSI 0.4.1 stability verification

Verified locally on Windows, 2026-10-08. The reported duplicate caption, redraw flicker and clipped dashboard were traced to specific native frame and layout defects.

## Changes

- Both WM_NCCALCSIZE forms preserve the full client area; legacy non-client caption painting is suppressed while native sizing, system menu and taskbar actions remain supported. Native caption bits are explicitly cleared.
- Polling no longer collapses the dashboard to one pixel, oscillates temperature fonts, toggles Apply buttons, resets unchanged scroll positions, rebuilds unchanged chart data, or reapplies native button themes. Static panels and live labels use buffered painting.
- Custom painted cards/buttons/chart invalidate their full surface when resized, removing the obsolete rounded edges left after a scrollbar disappeared. The production window exposed this defect even when fixture captures looked correct.
- Capability visibility changes trigger layout only when needed. Hidden descendants do not repeatedly change effective visibility. Layout measures the selected page explicitly while the tray window is hidden, preventing its height from collapsing.
- An Apply request made during an active sample waits for that read, rechecks freshness/capability, then performs one serialized transaction and readback. Duplicate queued requests, suspension and disposal cannot dispatch additional writes. Applying keeps card geometry steady.
- A second launch sends a registered reopening message to the existing instance. System preference callbacks tolerate shutdown races and skip unchanged palettes. Production and test hosts use the same text-rendering setting.
- Recoverable sampling/apply failures remain visible; bounded local support logs retain errors. Fatal UI faults reach the logged application boundary instead of a Continue dialog that could leave indeterminate controller state running.

## Evidence

- Core: 17 passed, zero failures.
- Hardware integration: NVIDIA valid/invalid/failed/missing/timeout/cancellation handling; hung telemetry worker deadline and cancellation termination; real Windows identity discovery and unsupported-control refusal. No live hardware write tests in this stability task.
- Full native UI suite: navigation, compact geometry, fonts/icon, drafts/focus/scroll, curve bounds/restoration, profile/boost/mode transactions, failed/mismatched readback, stale/recovery/suspend, tray lifetime, queued exit and disposal, plus new frame/repaint regressions.
- 200 static/changing-temperature samples: zero bounds, font, enabled-state or handle changes and no redundant chart invalidation. Twenty read-only samples also caused no hidden-reason relayout.
- Resize regression: growing a rounded card by 17 pixels invalidates its entire surface including the old rounded edge, without an explicit Refresh. Screen capture from an occluded test window is not treated as pixel evidence.
- Stress: 1,000 samples through a real Application.Run loop, injected failures every 31 samples, 40 page/theme/size changes and repeated maximize/restore/hide/show. Final run completed in 42.4 seconds; GDI 70 → 68 and USER 156 → 162 after warm-up, within bounded tolerance; zero hardware writes.
- The published 0.4.1 executable was launched with real telemetry on GF63 Thin 11UCX. External Windows capture confirmed full CPU/GPU readings, one integrated caption, clean rounded card edges and no obsolete button fragments after the initial scrollbar transition. Repeated captures showed live samples updating.

## Scope

This is finite regression and stress coverage, not proof of indefinite stability. Actual multi-monitor DPI switching, screen-reader/high-contrast interaction and long hardware endurance runs remain unverified. The Windows automation helper runs below the elevated app's integrity level; interactive window actions are covered by the native UI harness, while production visual verification uses external capture. Firmware writes remain gated to the existing validated model/EC; supported feature scope is documented in README.md.

Run the stress suite with `dotnet run --project tests/QuietMSI.UITests -c Release -- --stability`. Error details remain local at `%LOCALAPPDATA%\QuietMSI\logs\application.log`.
