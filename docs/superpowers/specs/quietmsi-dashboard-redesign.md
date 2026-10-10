# QuietMSI dashboard redesign

User-approved: closely follow `image_6d9028.png`, preserve existing verified hardware controls, add Light/Dark/System themes. Scope: native WinForms presentation; backend/Core protocols unchanged. No new dependencies, service, driver, hardware experiments, or automatic writes.

## Direction contract

THESIS: A readable laptop dashboard matching the supplied concept, replacing the narrow continuously relayouting settings stack.

OWN-WORLD: Segoe UI, pale blue-gray background, white rounded bordered surfaces, navy ink, slate secondary text, restrained teal actions/chart/selection. Dark: deep neutral background, charcoal surfaces, soft white text, brighter teal; no neon/glow. Native keyboard and input behavior retained.

STORY: See live sensors and actual profile/mode/boost at a glance, stage a choice, explicitly Apply, observe backend plus fresh readback. Missing/stale readings remain honest. Firmware levels remain High/Medium/Turbo/Eco, not invented Silent/Balanced mapping.

FIRST VIEWPORT: Wide default dashboard (approximately 1160x780 client, fit working area) with top identity/navigation/theme selection, left telemetry and current six-point chart, right profile and cooling controls; footer status, firmware details, export/refresh. Overview/Cooling/Diagnostics tabs. Cooling has bounded six-input editor and restore. Smaller usable windows stack panels; content may scroll manually but polling must preserve position, drafts, selected tab, and focus when eligible.

FORM: User-pinned concept; no random direction needed. Code-led native implementation under the existing delegated code-led preference. Temperature rings and curve are custom-painted from actual sampled data, not screenshot assets. Curve uses real temperature spacing or explicitly labels firmware slots if equally spaced.

SIGNATURE: Matching teal curve visualization plus selected-vs-current profile distinction. Theme switching live and persisted in local settings with test-isolated path; no hardware effects. Prefer System default. High contrast uses system colors.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance.

## Acceptance and verification
- Preserve existing 14 hardware controls and correct requests, fail-closed guards, freshness, explicit Apply, independent readback, deferred tray Exit; tests updated only for navigation/layout assumptions.
- Themes affect all visible native surfaces/controls/chart/navigation, persist mode, resolve System safely; persistence failure must not crash. No fake hardware verified badge.
- Reproduce scroll/focus bug against incumbent if possible; deterministic regression covers focused input, held polls and reason/layout transitions. Record unproven aspects honestly.
- Overview/Cooling/Diagnostics captures in light/dark; compact and scaling checks; no missing Apply actions/clipped editor. Synthetic captures labeled fixture; real elevated read-only check only, no new write tests.
- Core and Windows integration suite + native UI suite/build pass; package app+MIT source with instructions.
