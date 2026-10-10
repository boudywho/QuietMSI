# Dashboard verification (0.3)

## Scope
User-pinned dashboard concept, native WinForms, Light/Dark/System, existing hardware workflows unchanged. No new package dependency or service/driver. No hardware write tests performed for this redesign.

## Scroll root cause
Incumbent `289165a` was reproduced in a real message-loop isolated fixture: focused curve input at scroll Y=-220; held read disabled the input, transferred focus to Diagnostics and moved Y=-572; releasing the same snapshot moved Y=-599. Disabling focused controls triggers WinForms SelectNextIfFocused and automatic focus reveal. Capability reason visibility adds layout churn. Immutable isolated probe and differential evidence remain private scratch, not distributed in source.

Current UI regression holds an actual asynchronous fixture read: draft input remains enabled/focused, Apply disabled/public Apply refused, then focus/draft/viewport unchanged after completion. Failure disables unsafe controls and labels stale; recovery preserves staged draft. Existing independent readback/error and deferred-exit tests remain.

## Executed verification
Parent executed full build.ps1: Core17 passed/0 failed; Windows integration identity/read denial+worker deadline/cancellation passed; 34 named UI checks passed. Parent reproduced obsolete UIHost smoke count6 failure, updated expected14 controls, re-ran finite no-hardware smoke with exit0. Framework-dependent publish succeeded. Theme settings file readback/reopen/fallback/write-error tests pass with test-isolated paths.

Eight DrawToBitmap captures in `.impeccable/review/` carry synthetic provenance: Light/Dark Overview/Cooling/Diagnostics/compact. They are native UI fixtures, not live hardware proof. Independent finish review returned **FIX**: compact telemetry loses units/calibration qualifiers; unconditional scroll suppression prevents keyboard focus reveal; clipped brand, undersized rings and crowded true-axis chart labels. Dark-arrow compartments require on-screen fixture evidence because whole-form DrawToBitmap can differ from displayed native controls. These findings are being corrected in a bounded UI-only batch. Native HTML/CSS detector not applicable.

## Corrective verification
Parent reran full build.ps1 after the bounded corrections: Core17 passed/0 failed; Windows integration read-denial/worker deadline/cancellation passed; **41 named UI checks passed**, including compact keyboard Tab/Shift+Tab revealing lower Apply/Restore, focus/navigation during a held read, unchanged scroll/draft after healthy/error/stale/recovery transitions, measured compact telemetry/brand/ring/chart fit. Publish succeeded. No hardware write mode was run.

Viewport preservation now applies only to synchronous programmatic render/state/layout scopes, not the read await or deliberate navigation. Complete compact telemetry is scrollable within a content-sized card rather than clipped. Native actual-screen fixture crops prove closed arrows and open lists dark for all three selectors; whole-form DrawToBitmap cannot adjudicate native arrow chrome. Eight corrected view fixtures and six dropdown screen crops retain provenance.

## Final review and delivery
Independent verdict **SHIP** for the bounded six-fix batch: all six resolved; exact 14 captures and provenance hashes checked, eight targeted native PASS checks exit0. See `dashboard-finish-review.md`; no further UI correction required by that verdict. DESIGN.md and the native sidecar now record both palettes, actual typography/geometry and scoped scroll behavior. Parent documentation substituted for the rate-limited documenter.

Final full build and portable/source ZIP verification are recorded in `artifacts/checksums.json` and the delivered build output. No new hardware-write tests were performed. Actual high contrast, multi-monitor DPI transitions, screen reader, fan calibration and new resource-budget measurements remain unestablished.
