# QuietMSI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans inline; user delegated execution and intermediate approval bypass. Each feature uses RED→GREEN and an independent whole-project review before packaging.

**Goal:** A working MIT-licensed native Windows tray utility with real telemetry and fail-closed hardware controls.
**Architecture:** .NET 8 WinForms UI, independent core models/policy, and standard Windows/WMI transport. No proprietary runtime assemblies or driver. A read-only release is acceptable when evidence does not justify writes, but must state this clearly.
**Tech Stack:** C# .NET 8; built-in WinForms, System.Management only if needed. Custom dependency-free console test runner.
**Spec:** docs/superpowers/specs/quietmsi-design.md

## Global Constraints

- No raw port access, EC sweeps, driver/service installation, MSI execution, proprietary source/binary distribution, or network functionality in product.
- No write without exact model/firmware match, vendor/Linux evidence, pre/post validation, and independent sensor verification; no force overrides.
- Controls disabled with concrete reasons when unverified; absent sensor values must be null/unavailable, not fabricated zeros.
- MIT independently authored code only; no copied MSI/GPL implementation.
- No startup/resume/exit writes; user-requested Apply only; asynchronous non-overlapping polling and honest stale states.
- Target <10 MB portable framework-dependent executable, <30 MB working set, <0.5% idle CPU; report actual measurement.
- Keep MSI Center installed and services untouched. Local git develop branch; no push/publication.

## Review Focus

- Unknown/changed firmware never inherits a known hardware policy.
- Missing, malformed, implausible, stale WMI responses never become plausible telemetry.
- Provider hang/access denied never causes overlapping work or a frozen UI.
- Denied or ambiguous write never retries, logs success, or performs guessed restoration.
- DPI/high contrast and tray shutdown retain readable controls and release resources.

### Task 1: Core policy and telemetry contracts

**Files:** src/QuietMSI.Core/{Models,HardwarePolicy,TelemetryRules,FanCurve}.cs; tests/QuietMSI.Tests/{Program,CoreTests}.cs; project files.
**Interfaces:** DeviceIdentity(string Manufacturer,string Model,string Bios,string? EcFirmware); HardwareSnapshot with nullable telemetry, source/status/timestamp; ControlKind enum; ControlRequest; Capability with blocking reason. IHardwareBridge.ReadSnapshotAsync(CancellationToken), ApplyAsync(ControlRequest,CancellationToken).
- [ ] Write failing UnknownFirmwareDeniesControl test. Run console harness; expected missing policy or refusal mismatch.
- [ ] Implement fail-closed policy; run test green. No runtime writable allowlist until all evidence is established.
- [ ] Add vertical RED→GREEN tests for missing/out-of-bounds temps, fan target versus RPM distinction, read-only Apply refusal, and curve point monotonicity/count/range rules.
- [ ] Run suite and commit core plus tests.

### Task 2: Read-only transport

**Files:** src/QuietMSI/{Hardware/WindowsHardwareBridge.cs,Hardware/MsiWmiReader.cs,Program.cs}; docs/hardware-specs.md; tests transport decoder cases.
**Consumes:** Task 1 models/policy. **Produces:** bridge returning actual device identity, Windows battery/power status and verified vendor getters when proven; provider errors reported per sensor.
- [ ] Document decompiled method anchors and Linux mappings. Add failing decoder test for exact observed package structure before implementing parser.
- [ ] Independently implement only getter transport established by evidence. Use finite WMI timeout, cancellation and one sample at a time. Never load MSI binaries or call vendor initializer.
- [ ] Run malformed response/access-denied/unknown hardware tests and a real read-only diagnostic snapshot.
- [ ] Only add live writes if every safety gate passes, with pre/post and failure tests first. Otherwise deny in backend and UI; document exact blockers.
- [ ] Run suite and commit transport.

### Task 3: Native UI and tray

**Files:** src/QuietMSI/UI/{MainForm,Theme,TrayContext}.cs; .impeccable/surface.md; UI smoke test/diagnostic mode.
**Consumes:** bridge snapshots/capabilities. **Produces:** modern compact keyboard-accessible window, minimize-to-tray, explicit Quit, diagnostics export and disabled unsafe controls.
- [ ] Add failing read-only control/view-model assertions before building behavior.
- [ ] Build machine header, honest telemetry rows, profile/fan/charge settings with explicit capability reasons, diagnostics and verified-auto action slot.
- [ ] Implement finite non-overlapping polling (5 sec visible/30 sec hidden), stale state, sleep handling and disposal.
- [ ] Build/run native app; screenshot standard and higher-DPI layouts, inspect once, batch fixes and confirm.
- [ ] Run suite and commit.

### Task 4: Verification and distribution

**Files:** LICENSE, README.md, SECURITY.md, build.ps1, docs/verification.md, DESIGN.md, .impeccable/design.json; artifacts/portable and source ZIP.
**Consumes:** all prior tasks. **Produces:** executable + independently authored source, documented results.
- [ ] Build Release framework-dependent single-file executable and run diagnostic mode from published artifact.
- [ ] Measure file bytes, working/private bytes, normalized idle CPU over a real interval; distinguish UI-open/tray results and report misses.
- [ ] Independently review source safety and native UI screenshot; fix important issues with RED→GREEN tests. Never claim unsupported controls work.
- [ ] Document built visual system and scope, actual sensors/control availability, dependency and licensing boundaries, measured budgets.
- [ ] Package source excluding proprietary decompilation, private logs, downloaded tools, bin/obj and git. Verify ZIP members and executable checksum.
- [ ] Fresh full suite/build/live diagnostic; commit verified deliverables and give downloads.
