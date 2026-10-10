# REA validation — 2026-10-10

REA 6.2.0 is installed globally and registered for Codex. Its matching shared
`reverse-engineer-anything` skill is version 34. The scoped doctor check passed;
an independent MCP client connected to the installed server, listed 139 tools,
and successfully queried its target-free session. REA tools are also available
in the current Codex chat; a direct `binary_session` call succeeded. Installation
alone is not validation of MSI Center or proof of complete reverse engineering.

## Original MSI software restored

MSI Center 2.0.74.0 was downloaded from the package linked by
[MSI's official page](https://www.msi.com/Landing/MSI-Center) and installed.
Its bundled MSI Center SDK 3.2026.0911.01 and notebook Foundation
2.0.2609.1001 also installed successfully. The installers checked in this pass
have valid MICRO-STAR INTERNATIONAL signatures. Installer hashes, exit codes,
package inventories and logs are retained locally.

MSI Center requires **Restart** to finish notebook initialization. Restart later
was selected; Windows has not been restarted by this validation. Foundation
changed WmiAcpi's `MofImagePath` to the vendor `%windir%\sysWOW64\msiapcfg.dll`.
The owned QuietMSI classes remain visible, but the vendor `MSI_ACPI` and
`Package_32` classes returned Not found in the post-install metadata check.
That describes the pre-restart installation state. The user subsequently
restarted Windows; the new boot is 2026-10-10 00:42:10 Cairo. All three MSI
services are now running and both vendor classes are visible. WmiAcpi's
registration points to QuietMSI's owned schema again in the post-restart
snapshot. QuietMSI's startup support code can claim that registration; this
observation does not establish which process changed it.

## Post-restart comparison and correction

The live schema comparison passes all nine implemented methods: method IDs,
Boolean return types, embedded-instance parameter type, parameter ID0 and
input/output directions match the vendor. Operational class GUID/provider/WMI
qualifiers and packet UInt8 array, MAX32 and WmiDataId1 also match. Embedded
packet class names intentionally differ. The vendor class exposes 40 methods;
QuietMSI implements nine. This is subset-schema compatibility, not full vendor
schema replication. Local evidence is `schema-comparison.json` and
`postrestart-schema-validation.json`; the failed pre-restart snapshot is
preserved as `schema-comparison-preboot.json`.

Fresh static traces corroborate request/header layout, response field offsets,
boost register0x98/bit7, performance register0xD2 and C4/C0/C1/C2 encodings,
fan-mode register0xD4/bit7, CPU curve selector1/eight-byte layout and fixed
breakpoint offsets. Seventeen cited method bodies were checked against raw
original bytes, CIL hashes and REA anchors. Full traces and limitations are in
the local `vendor-protocol-findings.json` and `.md` ledgers.

The comparison found a concrete fan-mode counterexample. Vendor Auto/Advanced
changes only bit7, while QuietMSI's old `0x6F` mask also cleared bit4 and its
guards admitted `0x1D/0x9D`. The control planner and firmware transport now
reject these unvalidated baselines, accepting only the observed `0x0D/0x8D`.
The planner toggles only bit7. A regression reproduced the original failure,
then passed after correction; it checks rejection before any transaction write.
The hardware suite also checks the independent transport guard before WMI
access. This correction is in workspace source; the installed release executable
has not been replaced by this audit.

An existing .NET8 SDK8.0.425 was found at
`%LOCALAPPDATA%\hermes\tools\dotnet8`; the earlier no-SDK result applied to PATH.
Fresh source core tests now pass **18/18**, and the actual application and
hardware-test projects build with zero warnings/errors. Logs are
`postrestart-core-tests.log`, `postrestart-app-build.log` and
`postrestart-hardware-build.log`.

The unelevated hardware suite passed the pre-WMI mode-guard regression, NVIDIA
fallback, worker timeout/cancellation, identity discovery and unsupported-control
refusal. Protected control state is unavailable in that context.

After Windows UAC approval, the fixed vendor/owned getter comparison passed on
2026-10-10 at approximately 01:00:47 Cairo. All nine fixed requests returned
successful 32-byte packets in sequential owned/vendor/owned reads. All seven
stable-field comparisons matched: firmware, WMI version, boost, performance,
fan mode, CPU curve and temperature breakpoints. Dynamic telemetry also happened
to match in these adjacent reads: CPU53°C, GPU44°C and first fan period212
(2,264 vendor-formula RPM); second fan period0 remains unavailable. Sequential
agreement is not independent sensor calibration or an atomic snapshot.

The observed baseline was Medium0xC1, Auto0x0D, boost byte0x06, CPU percentages
38/43/48/54/60/70 with preserved terminal85, and fixed temperature points
0/55/64/73/76/82. The rebuilt elevated hardware suite then passed its protected
firmware getter, complete state reading and all existing read-only checks
(exit0). No firmware setter, schema setup or registry change was invoked by this
helper. Evidence: `postrestart-getters.json` and
`postrestart-protected-tests.log`; helper: `compare-live-getters.ps1`.

Remaining semantic differences are explicit: QuietMSI requires response status1
while the traced vendor paths accept positive status values; it treats zero or
out-of-range RPM as unavailable; and its Auto action changes mode/boost rather
than restoring MSI's stored CPU/GPU default curves. QuietMSI's control labels
therefore describe individual firmware controls, not complete MSI scenarios.

## Evidence collected in this pass

Local results are under `artifacts/rea-validation`, which is ignored and excluded
from distribution. Vendor installers and analysis inputs are kept outside the
repository under `%USERPROFILE%\.local\share\QuietMSI-validation\vendor\2026-10-10`.

- REA inspected the pre-existing `QuietMSI.dll` without loading or executing it:
  SHA-256 `f251d5c4bbb47353d55121cbc635886f42fb35444c8825a5257c95a60c2045db`;
  managed AnyCPU/CIL, 97 types, 491 methods, and 4,127 decoded call edges.
  Member inspection explicitly reports partial coverage.
- Native import inspection found declarations for five Windows libraries and
  16 imports. This verifies declarations, not their runtime resolution.
- The pre-existing compiled core suite passed 17/17. The compiled read-only
  hardware suite passed worker timeout/cancellation, NVIDIA fallback and
  unsupported-control checks. Firmware identity/CPU/fan/control reads were
  unavailable because Windows denied protected access in the unelevated run.
- A fresh `dotnet run` could not start because the shell's .NET installation has
  no SDK. These are compiled-test results, not a fresh source build.
- Before reinstalling MSI Center, an elevated compiled `--protected` read-only
  hardware test passed on the current boot (2026-10-10, Cairo). All three MSI
  services were absent and WmiAcpi still pointed to QuietMSI's owned resource.
  It read EC `16R6EMS1.106`, CPU 51°C, GPU 44°C, reported fan 2,285 RPM and
  complete control state. Local evidence is
  `msi-install-result.json` and `postboot-preinstall-protected-tests.log`.
  This closes post-reboot telemetry/control-state read survival, not changed
  control writes or independently calibrated sensors.
- REA's static MCP audit runner was exercised on the existing core assembly:
  31 types, 255 methods, no declared P/Invoke imports, partial member coverage.
- Six original vendor assemblies were copied into the external analysis folder,
  bound to SHA-256 identities and inspected without loading their code. Their
  member inventories contain 569 types and 3,245 methods in total; member
  inspection explicitly reports partial coverage. Counts establish an inventory,
  not semantic or feature completeness.

| Original assembly | File version | Types | Methods |
| --- | --- | ---: | ---: |
| MSIAPService.exe | 1.1.2609.0901 | 81 | 390 |
| MSIWMIACPI2.dll | 1.0.2606.1501 | 16 | 99 |
| NamedPipeClientLib.dll | 1.1.2607.2701 | 65 | 226 |
| OmApSvcBroker.exe | 1.0.2609.0301 | 106 | 681 |
| API_NB_Base Module.dll | 1.0.2609.0201 | 126 | 951 |
| API_NB_Monitor.dll | 1.0.2609.0301 | 175 | 898 |

The Base Module member result exceeded REA's 10 MiB MCP response budget; the
complete result was recovered through CLI JSON, not truncated. The original
transport failure and subsequent recovery are both retained in the audit ledger.
The runner now exports retained session evidence for this transport condition.

Fresh static evidence corroborates the positive-period RPM calculation in
`MsiPackageDecoder.VendorRpm`: Base Module `UserScenario.CalculateFanSpeed`, token
`0x0600028d`, combines its arguments as `(high << 8) + low` and computes
`60000000 / (period * 2 * 62.5)`, converted to an integer. For byte inputs with
positive period, this agrees with integer `480000 / period`. This is an inference
from decoded CIL and the raw method bytes, not independent fan calibration.
Artifact SHA-256 is
`104e412ff9a95f49b7204f9e43eb33e9c98c18d60712740b84ee21e288331795`,
MVID `c17bac71-d2d1-4139-9a0c-a07f928fc547`; raw CIL SHA-256 is
`fa39159efed15c93313e0ceadd8b661ada22fa4363af3761f061271c471efd1e`.
Local evidence includes `vendor/base-members-cli.json` and `protocol-findings.json`.

These observations establish that the installed tooling works and document the
existing implementation. Protocol encodings now have fresh original-code
corroboration within the traced WMI2 paths; live setting transitions and other
vendor branches still need behavioral comparison.

## Coverage ledger

| Validation question | Current disposition | Evidence still needed |
| --- | --- | --- |
| Exact GF63 Thin 11UCX / EC16R6EMS1.106 / WMI2.8 gate | Fresh protected source-built tests passed; vendor/owned identity and version reads match | Broader firmware/model coverage remains excluded |
| Packet lengths, status, selectors and Set_Data/Set_Fan encodings | Traced WMI2 layouts corroborated by hash-bound original CIL; stricter status policy documented | Compare accepted/error runtime cases and untraced legacy/native branches |
| Performance, boost, Auto/Advanced | Encodings corroborated; extra-bit mode counterexample fixed and regression passes; historical transitions restored | Repeatable vendor-to-QuietMSI setting-transition comparison; Turbo/Eco evidence; full scenarios intentionally differ |
| Conservative CPU curve / session restoration | Historical one-point change and restoration recorded | Full post-write profile/mode/boost/breakpoint invariants and broader valid-case comparison |
| Owned WMI schema | Post-restart live comparison passes all nine method signatures and operational qualifiers, including IDs15/18/25 | Full40-method vendor replication remains outside implemented scope |
| Live fixed getters | All nine requests successful; seven stable-field comparisons match across owned/vendor/owned reads; adjacent temperatures/periods agree | Error-case behavior, simultaneous-state guarantees and unimplemented getters remain unverified |
| MSI independence | Fresh post-reboot protected telemetry/control-state reads passed before reinstall; historical same-value probes passed | Changed-setting acceptance after reboot; distinguish same-value probes from transitions |
| Sensor accuracy | RPM formula corroborated against freshly hashed original CIL; historical near-time NVIDIA comparison documented | Independent synchronized CPU/fan calibration; validate any second fan channel |
| Charge limits, MUX, GPU curves, overclocking, full scenario automation | Outside implemented scope | Separate feature inventory and original behavioral evidence |
| Every MSI Center module and native branch | Package inventory and six key managed originals captured; no exhaustive coverage established | Native provider analysis and feature-by-feature obligations across the full package |

## Source review questions

`MsiCpuCurve.Write` verifies seven stable CPU-table bytes after a write. It does
not itself verify unchanged performance, boost, fan mode or fixed temperature
points. The historical one-point acceptance test and standalone same-value probe
have broader external checks; that is narrower than a general production
complete-state guarantee.

Enabling an existing Advanced curve checks six percentage and breakpoint entries,
percentage bounds and monotonic percentages. It does not validate breakpoint
semantics/order or the terminal table cap in that mode-enable plan. Treat these as
open validation questions, not proof that the vendor curve is unsafe.

The global QuietMSI mutex coordinates cooperating QuietMSI processes. It cannot
prevent MSI Center from writing concurrently; fresh pre/post reads provide
bounded conflict detection.

## Repeating static vendor inspection

The runner calls REA's execution-free managed artifact/member/native-boundary
tools and preserves complete MCP results, exact SHA-256 identities, limitations
and errors. It never reports complete reverse engineering from metadata counts.

```powershell
$reaPackage = Join-Path (npm root --global) 'rea-agents'
node tools/Invoke-ReaAudit.mjs $reaPackage artifacts/rea-validation/vendor `
  'C:\path\to\MSIWMIACPI2.dll' `
  'C:\path\to\API_NB_Base Module.dll' `
  'C:\path\to\API_NB_Monitor.dll'
```

Native binaries need a separately configured supported provider; none is
configured in this pass. Managed inspection is independent of that prerequisite.

**Verdict: tooling verified, implemented schema and fixed getters corroborated
against live vendor behavior, traced protocol encodings corroborated, one concrete
mode-bit defect corrected; full MSI Center equivalence is not established.**
Controlled setting transitions after this restart, calibration and untraced
native/legacy paths remain open. QuietMSI supports a subset of controls on one
exact model/firmware combination, with documented semantic differences.

## MSI Center removed again — 2026-10-11

At the user's request, MSI Center's app was removed for installed users and the
signed SDK uninstaller completed successfully. Its log records that it also
removed NBFoundation successfully. A redundant separate Foundation invocation
returned1 during that removal race; subsequent all-user app/product/service
inventory confirmed that both components were absent. The first-pass result is
preserved alongside the successful final verification.

The remaining Center updater, known Center/notebook caches and logs were removed
using a fixed directory allowlist. The old vendor SysWOW64 `msiapcfg.dll` was
checked against its MSI signature and previously captured SHA-256, copied into
the external analysis archive, and removed from its system location. WmiAcpi
still points to QuietMSI's independently owned resource. Windows' signed ACPI
driver was preserved, as were MSI Afterburner and QuietMSI's startup task.

Final checks found no installed MSI Center app, SDK/Foundation product records,
the three Center services, Center processes or allowlisted residual directories.
The packaged QuietMSI executable passed three protected diagnostic reads after
cleanup, including exact EC identity, CPU/GPU temperatures, fan RPM and complete
control state. No firmware setters or automatic reboot were invoked. This is
immediate post-removal evidence; no new reboot was performed in this pass.

Windows denied future-user provisioning inventory through both PowerShell and
native DISM (exit5). All-user installed app absence is verified; absence of an
image provisioning entry for hypothetical future accounts remains unverified.
No Windows security or permissions were changed to bypass that denial.

Local evidence: `artifacts/rea-validation/removal-2026-10-11/final-removal.json`,
three `quietmsi-after-removal-*.json` samples, `sdk-uninstall.log` and the DISM
log. Analysis copies kept outside the repository are evidence, not installed
Center components. See [performance levels](performance-modes.md) for the
verified mode mapping and the limits of physical-effect claims.
