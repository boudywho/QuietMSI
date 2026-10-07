# Verification results — initial read-only build

Normal source builds/testing and performance references here describe v0.1 unless explicitly stated. Corrected v0.2 actual Apply/elevation tests are documented in docs/v2-verification.md; no v0.1 footprint claims establish a v0.2 aggregate resource budget.

Official user-local .NET SDK 8.0.425, SHA-512 verified against Microsoft's release metadata. Native .NET 8 Windows x64 framework-dependent publish completed with 0 warnings and 0 errors. ILSpy analysis tools and SDK are outside the product distribution.

Core tests: eight named scenarios passed (unknown firmware refuses controls; missing/invalid temperature; target percentage not RPM; every write refused; curve draft constraints; firmware strict packet decoding; V2 temperature/period decoder; fixed read request allowlist). UI tests: eleven named checks passed, including native controls locked, initial visibility, nonoverlapping polling, source labels, suspend/error stale state, no lifecycle writes, JSON export/readback, tray lifecycle, cancellation/disposal, compact resize150% scaling. Unelevated Windows integration test discovered real GF63 Thin 11UCX/BIOS and battery100%/AC; protected MSI getter denied as expected. No real write tests occurred.

An approved elevated independent-reader integration test initially failed with System.Management 'Not found' at optional ReturnValue property lookup. The packet had no ReturnValue property; enumerating optional properties rather than blindly indexing fixed it. The same protected regression then passed and returned EC16R6EMS1.106, CPU76°C, GPU62°C, first fan2823 vendor-reported RPM. Output remains private scratch. This is the actual independent C# reader, not vendor library execution. Protected fan reading uses vendor formula, not an independent HWiNFO calibration.

Earlier approved fixed read-only probe returned CPU75°C/GPU63°C and first period161=>2981 vendor RPM. nvidia-smi reported GPU62°C near that probe. Both values are observed at different times; do not claim synchronized agreement. HWiNFO/LibreHardwareMonitor absent; CPU/fan independent verification outstanding.

## Footprint (measured, not promised)

Published QuietMSI.exe: 546360 bytes at measurement time, framework-dependent. .NET8 Desktop runtime already installed; this number excludes it. Measurements are unelevated normal UI/provider-access-denied mode, not sustained elevated telemetry mode. Windows11,12 logical processors. Each mode sampled15 times at2-second intervals.

| Mode | Interval | CPU, normalized all logical CPUs | CPU relative to one core | Avg working set | Max working set | Avg private bytes |
|---|---:|---:|---:|---:|---:|---:|
| Window visible |30.160s|0.190%|2.280%|68645956 bytes|68988928 bytes|21179870 bytes|
| Tray hidden |30.131s|0.0216%|0.259%|69067844 bytes|69283840 bytes|21124437 bytes|

**The <30 MB RAM target is NOT met if RAM means working set.** Private allocation is below30 MB, but substituting it for working set would misrepresent the requirement. The tray idle CPU target<0.5% is met on this observed interval, even relative to one core. Visible mode is below0.5% normalized, not one-core. Longer and elevated-provider workloads are not measured. No forced working-set trimming was used to manipulate the benchmark.

## Native UI

Built app launched and became input-idle. Real PrintWindow capture shows actual device identity, read-only state, battery100%/AC, unavailable protected sensors and source reason. All visible text fits at600x700 and long provider status wraps; vertical scrollbar provides lower sections. UI fixture smoke uses ReadOnlyBridge with unavailable data and is not evidence of live sensor readings.

Screen-copy probes were initially invalid due to DPI/occlusion and showed incomplete/wrong underlying window. They were discarded; validated PrintWindow capture is the visual evidence. Native control tests cover440px compact resize and programmatic150% scaling. Real multi-monitor transitions, screen-reader use, and physical highcontrast mode remain unverified. Theme initializes from Windows highcontrast state; dynamic mode switching is not attested.

## Safety and scope

No driver installation, vendor constructor invocation, port I/O, arbitrary EC sweep, WMI setter, service change, calibration, MSI Center removal, registry/ACL repair, startup registration, remote publication or networking occurs in app. All control requests denied before hardware transport. No claim of full MSI Center replacement. Signed ACPI driver/provider dependency chain and behavior after MSI Center removal remain unresolved.

Remaining feature blockers: exact installed vendor charge threshold routine, safe cached-default fan restoration, independent CPU/fan telemetry check, cross-process conflict behavior, verified writable policy/pre/post logging and actual explicitly authorized write tests. MUX hardware support not established. Do not unlock these through a configuration override.

Independent code review found no writable/network/driver-modification path, but rejected unbounded synchronous WMI calls, freshness during blocked reads, and cached identity failures. Each material issue was addressed in one correction pass:

- Read-only sampling is isolated in a subprocess of the same executable, terminated at a 10-second deadline or cancellation. A deterministic deliberately blocked test worker proves termination before releasing single-flight ownership. If termination cannot complete, future reads are quarantined instead of overlapping abandoned work.
- UI updates sample age independently of provider completion on polling ticks and diagnostics export. Added failing OngoingFreshness test, then verified it passes.
- Every worker takes fresh device/firmware/WMI identity; no validation or transient failure persists across refresh/resume. Exact model checked before protected getters; added current identity/schema policy test.

Final fresh execution:9 core tests,3 hardware integration checks (timeout/cancellation/actual read-only identity),12 named native UI checks pass; publish 0 warnings/errors. Published executable now550968 bytes. Final parent-process measurements over30s each: window average working set68056678 bytes, normalized CPU0.2634%; tray average working set69258718 bytes, normalized CPU0.02591%, one-core CPU0.31092%, private bytes22240188. The brief-lived telemetry worker's peak working set/CPU is NOT included in these parent-only measurements, so no aggregate resource-target pass is claimed. Under30 MB working-set target still missed. Pre-isolation measurements above are historical, not final benchmarks.

Sampler admin telemetry proof above predates worker isolation. Same sampler implementation remains in the final read-only worker; final published unelevated diagnostics must also be exercised. Final elevated wrapper/prolonged telemetry workload, full provider/firmware changes and OS-level hung ACPI behavior are not certified by the blocked-process test.

Native independent finish review: SHIP for honest read-only presentation; no material visual blocker. Scope: two validated initial screenshots plus native UI source, not new behavior tests. Nonblocking follow-ups: clearer partial snapshot timestamp, in-flight feedback, unknown identity wording, earlier tray explanation and physical accessibility verification. DESIGN.md and .impeccable/design.json record built tokens; these documents were read back by parent. No full writable-replacement or accessibility certification claim.

Final published worker diagnostics and UI smoke each exited0; readback confirmed actual GF63 Thin11UCX identity/battery100% in unelevated mode. Final worker-isolated admin telemetry workload was not re-run; direct sampler admin proof remains the elevated evidence.
