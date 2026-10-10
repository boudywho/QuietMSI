# QuietMSI design

User approved autonomous native C# development and intermediate approval bypass. Build independently written MIT source; local repository only, no publishing, driver installation, service removal, or elevation without a specific later need.

## Architecture

Use .NET 8 WinForms, a single STA tray application with a compact resizable native control panel. No WebView, UI packages, network client, MSI assembly loading, or third-party drivers in the product. System.Management is the only hardware package if the standard COM WMI bridge is impractical. Separate Hardware bridge, Safety policy, and UI; keep units small and observable.

WMI class metadata and vendor decompilation establish the transport. Read only proven getters/property providers, bounded to the detected MSI model. Do not perform arbitrary EC sweeps. Where safe getter decoding is proven, support snapshot telemetry with timestamps, provider names, bounds, and unavailable states. Battery percentage/power status use Windows APIs; vendor fan targets must not be labeled measured RPM.

## Safety

IHardwareBridge exposes ReadSnapshotAsync(CancellationToken) and ApplyAsync(ControlRequest,CancellationToken). No public raw-register or arbitrary-method surface. Capability states carry a reason and independent evidence status. Every writable capability needs model + exact EC firmware match, installed-vendor mapping, applicable Linux mapping, pre-read/post-read design, and independently checked telemetry; absent evidence denies the command before invoking transport. No user-configurable force override. Unknown models receive Windows battery and informational diagnostics only.

Implement performance, boost, charge-limit and fan-curve UI as requested, but unlock only verified capabilities. A disabled control is explicitly unavailable, not a mock success. A factory/auto action is enabled only when its exact semantics are proved; never reset calibration, UEFI, or BIOS settings. Log only allowlisted register states before accepted writes, preserving unrelated bits. No automatic rollback using guessed offsets, no retry of ambiguous writes, no auto-write at startup/resume/exit.

## UI

Operate mode. Working name QuietMSI. Compact one-window composition: machine and connection status; a large temperature/fan readout row; clear performance selector; cooling/charge sections; lower capability evidence and Diagnostics/Auto actions. Show sampling state and source. Keyboard navigation and Windows accessibility names, visible focus, high contrast compatibility, DPI scaling, scroll if needed. Standard window chrome minimizes to tray; explicit Quit disposes polling and tray icon. System UI fonts only. No decorative animation, textures, synthetic hardware values, or automatic startup registration.

Design direction follows a contemporary music-library detail view (candidate 7 of seed 82c1c406): restrained charcoal and light neutral regions, ordered track-like telemetry rows, one muted blue accent for deliberate actions, sectioned settings with persistent labels. Translate the form to a native utility, not a music costume. Challengers: map declined (spatial navigation harms settings), lexicon declined (dense prose slows operation), developer surface competitive (panel clarity retained, not neon), boarding pass competitive (fixed label/value alignment retained), catalog declined (tiny cells harm accessibility), type specimen declined (large display emphasis useful for two readings only). Raises: map's stable state legend; lexicon's clear label hierarchy; catalog's compact ordered rhythm; specimen's strong numeric hierarchy. No decision-page round because the user explicitly delegated all design choices. Code-led for native work; no shipping raster assets.

## Packaging and constraints

Framework-dependent Windows x64 single-file build, under-10 MB target excluding runtime already installed. Publish source ZIP separately, MIT LICENSE, README, security/safety boundaries, docs/hardware-specs.md and verification report. Do not include decompiled MSI source, binary copies, personal logs, or analysis tool downloads. Self-contained build is optional but cannot be represented as sub-10 MB.

Under-30 MB working-set and under-0.5% idle CPU are measurable targets, not promises. Poll every 5 seconds visible and at most 30 seconds hidden; never overlap calls; suspend on sleep and mark stale after failed samples. Missing provider or access denied must keep UI responsive and produce actionable diagnostics.

## Acceptance

- Build and tests run against real SDK; portable app launches on this machine.
- Safety regression tests prove unknown firmware, missing evidence, invalid values, failed/malformed reads and write conflicts fail closed.
- Live read-only snapshot exercised without escalation; unavailable sensors stay honest.
- All implemented controls enumerated as usable or blocked, with exact blockers documented.
- Tray behavior, shutdown, DPI layout and screenshot reviewed; source has no ad/network/driver installation functionality.
- Binary size, working set, idle CPU measured and actual results documented; targets missed are not hidden.
- No external publication or MSI Center removal.
