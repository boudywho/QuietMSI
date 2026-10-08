# Correction evidence and progress

The earlier read-only build failed the requested replacement workflow. User screenshots showed normal launch denied telemetry and all controls disabled. UI/source unit passes did not establish actual end-to-end usefulness; correction acceptance is real launch/read/apply/readback/restore.

## Approved permission/dependency model

User allows installed MSI DLLs/services and additional service if necessary, but selected one UAC startup via requireAdministrator manifest. Existing Foundation service already running. Narrow read probes through its installed NamedPipeClientLib and an independent named-pipe client failed. Static Foundation CertificationPass2 checks protected installation directory, executable allowlist and authorized Authenticode certificate. Loading a vendor DLL does not make caller trusted. No validation bypass, patch, certificate forgery or service security weakening performed. Direct Windows WMI works under user-approved administrator privileges; no extra service is currently necessary.

## Real executed tests, original state restored

- Final manifest-enabled published-v2 telemetry read: EC16R6EMS1.106/WMI2.8, CPU87°C, GPU61°C, first vendor fan3057RPM. Actual portable worker, not mock. UAC approved.
- CoolerBoost exact reversible test, user approved:0x98before0x06, after0x86, fresh getter confirms; period166=>2891RPM before,74=>6486RPM after3s; restored0x06 and confirmed.
- Performance exact reversible test, user approved:D2original0xC1(Medium), switch0xC0(High), readbackC0, restoreC1, confirmed. No overclocking/power-plan/service change.
- Fan mode exact reversible test, user approved:D4original0x0D(Auto), current CPU curve38/43/48/54/60/70% and temperature points0/55/64/73/76/82°C successfully read. Only advanced bit set=>0x8D, readback8D, fan period132 observed, restore0D, confirmed. No curve table writes/no Silent.

Tests use installed vendor mappings and upstream msi-ec G2_6 offsets/modes. Log original state before writes, fresh pre-read conflict check, setter success and exact getter post-check; no fallback zeros/retry. Private original logs remain scratch, not redistributed. CPU/fan independent HWiNFO check remains outstanding; fan is correctly labeled reported vendor RPM.

## Core/backend progress

12 core behavioral tests pass, including bit preservation, exact encodings, denied missing pre-read, conflict prevents write, journal precedes write, mismatch never retries, Advanced requires conservative monotonic six-slot curve. Hardware tests read identity and deny unsupported charge/customcurve/MUX only; they do not execute actual setter. Real approved PowerShell write probes prove protocol, not final C# Apply/UI integration yet.

Backend has narrow Set_Data transformations for98/D2/D4, exact model/EC/schema validation, flushed local append-only log, pre/post reads and serial ownership. No automatic changes. Snapshot has current performance/boost/mode/curve. UI correcting to real bridge-provided capabilities and explicit Apply with busy/result states. Custom curve editing awaits independent driver mapping and channel/restore evidence; do not claim completed.

Core/backend updated:13 behavioral tests pass. Actual C# WindowsHardwareBridge.ApplyAsync workflow executed under explicitly approved UAC: boosttrue/false,High/Medium,Advanced/Auto each applied and matched snapshot readback; original Performance193/FanMode13/Boost6 and CpuCurve38/43/48/54/60/70 restored. This is same backend used by UI, not only PowerShell.

User separately approved one-point cooling-only curve test. Protocol Set_Fan1 current eight-byte table60,38,43,48,54,60,70,85 changed only payloadslot3 48->49 and restored48; fixed temperature block/mode unchanged. Actual C# FanCurve Apply CSV38,43,49,54,60,70 then Restore passed exact readback and original curve/mode restored. CPU only, no lower-than-captured original values, monotonic, no above captured terminal cap85, preserve noneditable bytes0/7. Upper-limit interpretation is conservative observed bound, not general manufacturer certification. Baseline means session original, not factory. Journal stored LocalAppData/QuietMSI/logs/hardware-changes.jsonl before each write and flushed; no retries or automatic writes.

Primary Linux WMI patch04/10 independently matches selectors1/2 six percentage slots and preservation; unmerged/not exact .106 certified. Upstream msi-ec G2_6 firmware allowlist/mode map merged but no generic curve editor. Shared CPU/GPU arbitration unknown; GPU table untouched, no temp-threshold edits. Source fact research remains separate from MIT implementation.

Offline review corrections:Advanced now compares/revalidates current curve+breakpoints immediately before mode writes; full final multi-register state checked; explicit false setter ReturnValue rejected; hardware ownership Global admin/SYSTEM-only mutex (same-thread acquire/release, abandoned owner denies), acquisition failures release local gate; failure journal best-effort while pending journal mandatory. Core16/16 passes with new race/status regressions. Actual elevated machine ownership test blocked competing worker; no hardware writes in that ownership test.

Final field-layout correction:live Get_Fan1 byte0 changed60->70 during mode/fan activity, while six editable percentages+terminal85 stayed stable. Byte0 is treated as dynamic noneditable state, preserved from fresh prewrite (not replayed from stale baseline), excluded from stable curve conflict/readback checks. Regression proves dynamic header tolerated and editable drift rejected; Core17/17. UI excludes dynamic header from its stale-original comparison too. One UI fixture run hit5-second async timeout under concurrent workload; immediate standalone re-run passed all26; recorded as timing flake, not silently erased.

Normal source/test/build verification:16 core tests pass,26 named native UI checks pass,actual read-only integration/timeout checks pass. Functional UI includes current profile/boost/mode,explicit Apply,result+independent poll readback,stale/busy gating,deferred Exit and six cooling-only CPU percentages plus captured-original Restore. Synthetic screenshots are labeled fixtures, not advertised as hardware evidence. Actual UAC-enabled published screenshot verified live ECidentity,CPU90°C,Fan13404RPM,currentMedium/Auto/BoostOff,enabled Apply; GPU was unavailable in that sample. No thermal stress/cooling performance claim. Earlier v1 footprint claims not reapplied; final packaging/launch verification recorded below.
