# Bounded dashboard correction evidence

All data is synthetic. No hardware transport, elevation, driver/service changes or backend/Core edits were used. Fixture appearance settings use a null (nonpersisting) path. No commits were made.

## Vertical RED → GREEN regressions

- `--keyboard-regression`: before fix, `Tab must automatically reveal Hardware_ApplyProfile; scroll {X=0,Y=0}`. After scoped suppression: Tab and Shift+Tab reveal all lower Overview Apply buttons and Cooling Apply/Restore; navigation while ReadSnapshotAsync is held remains available. Full native NumericUpDown reveal replaces inset edit-child reveal (initial inset-child reproduction left the outer control 2px above the viewport).
- `--compact-regression`: before fix, `55.0 °C needs 122px at width 159, allocated 64`. Width-constrained wrapped measurement and content-derived sensor-card height now fit CPU units, full target(not RPM), source/calibration caveat and battery without overlap or shortening.
- `--brand-regression`: before fix, `QuietMSI needs {Width=171, Height=46}, allocated {Width=150, Height=39}`. Header allocates measured size and places machine identity after a 16px gap.
- `--ring-regression`: before fix, readout rectangle crossed the arc. Rings now use 100px geometry with measured small readout inside the stroke clearance; 100.0°C fits.
- `--chart-regression`: before fix, annotation rectangles exceeded edges. Six exact temperature/target pairs now render in a measured companion table; line vertices retain actual temperature spacing, not an equally spaced substitute axis.

## Native arrow adjudication

`--native-dropdowns` uses Graphics.CopyFromScreen on an activated visible native fixture with PerMonitorV2 awareness. GetComboBoxInfo supplies actual closed arrow rectangles and list HWNDs. Profile, FanMode and ThemeMode closed arrow compartments and open lists pass dark pixel assertions. The suspected white compartment was not reproduced on-screen, so NativeAppearance.cs was not changed. MainForm.DrawToBitmap/WM_PRINT captures are not authoritative evidence of native arrow chrome; use the companion actual-screen PNGs.

## Verification

Fresh final execution: 41 UI PASS cases, 17 core PASS cases / 0 failed, Release build 0 warnings / 0 errors, finite UIHost ReadOnlyBridge smoke PASS. Complete safety, bounds, serialization, mismatch/readback, disposal and tray tests remain intact. Compact Overview and Cooling heldread/error/stale/recovery tests retain drafts and scroll. See `finish-verification.json` for exact outputs.

Eight approved views were regenerated in `../../../.impeccable/review/`: `synthetic-{light,dark}-{overview,cooling,diagnostics,compact}.png`. Six actual-screen selector crops are `native-dark-{Hardware_Profile,Hardware_FanMode,ThemeMode}-{closed,open}.png`. `synthetic-provenance.json` and `native-dropdown-provenance.json` record origin, fixture, DPI, capture timestamp, assembly and image SHA-256 hashes. All 14 image hashes were independently checked.

Compact at 440×600 intentionally scrolls a growing sensor card; its first viewport shows CPU °C fully, while lower fan/source text is below the fold. Tests prove the full measured rows remain inside the card and lower actions are keyboard-reachable. This is viewport cropping, not permanent telemetry clipping. Actual multi-monitor DPI/high-contrast and physical hardware are not certified by these synthetic checks.

Mechanical detector output: `[]`. `git diff --check` passes; Git reports normal LF→CRLF notices only. The Impeccable context loader misclassifies native Windows WinForms as web and displays the older DESIGN.md; this did not authorize changing platform/docs or the approved concept. Parent owns final documentation and independent review.
