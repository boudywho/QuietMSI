# Hardware protocol specification

## Evidence and local identity

Observed machine: Micro-Star International GF63 Thin 11UCX, BIOS E16R6IMS.112. Separately approved read-only elevated probes returned EC 16R6EMS1.106 and WMI 2.8, normal 32-byte response packets with success flag 1. BIOS and EC identifiers are distinct.

Vendor assemblies statically decompiled with ILSpy 9.1.0.7988 outside this repository: MSIWMIACPI2.dll 1.0.2606.1501; MSIAPService.exe 1.1.2609.0901; API_NB_Base Module.dll 1.0.2609.0201; API_NB_Monitor.dll 1.0.2609.0301; NamedPipeClientLib.dll 1.1.2607.2701, feature/support modules and OmApSvcBroker.exe. No vendor assemblies executed or distributed. Documentation is independently worded protocol facts, not copied implementation.

Sources for model correspondence:
- [Linux kernel pinned msi-ec.c](https://raw.githubusercontent.com/torvalds/linux/0b30e6e583b2988864ffa7aeb2905bc2d0c7d575/drivers/platform/x86/msi-ec.c), CONF12 accepts 16R6EMS1.104, not .106.
- [Upstream msi-ec pinned source](https://raw.githubusercontent.com/BeardOverflow/msi-ec/d7fbbd88e6831e56801b860e46475cbf8ddbc7c1/msi-ec.c), CONF_G2_6 accepts .103/.104/.106/.107; comment GF63 Thin 11UC / 11SC, not explicit 11UCX certification.
- [Support guide](https://raw.githubusercontent.com/BeardOverflow/msi-ec/d7fbbd88e6831e56801b860e46475cbf8ddbc7c1/docs/device_support_guide.md) warns incorrect EC writes may damage hardware.

## Windows WMI contract

Namespace root\wmi; actual enumerated instance of MSI_ACPI. Fail closed if none or more than one. Do not assume the vendor's hardcoded InstanceName for all hardware.

Parameterized getters use embedded Data instance of Package_32, property Bytes as uint8[32]. Selector is Bytes[0], rest zero. Package is in-memory only: never Put(). Get_WMI/Get_EC use no input object. Output Data.Bytes[0] is status; require exactly 1. Other values must not be treated as success. Remaining output payload follows status. Require exact 32-byte packet, no artificial padding.

| Method | Input | Proven output interpretation | Product use |
|---|---|---|---|
| Get_WMI | None | Raw bytes 1/2 major/minor | Identity gate; only observed 2.8 schema supported |
| Get_EC | None | Raw byte 1 platform/format flags; raw 2–29 firmware/build ASCII on this machine | Firmware identity; exact string validation |
| Get_Temperature | Selector 0 | Raw byte 1 CPU °C, raw 2 GPU °C; zero/255 unavailable | Only matched GF63 Thin 11UCX/.106/WMI2.8 read-only path |
| Get_Fan | Selector 0 | Raw 1/2 first fan period high/low; raw 3/4 second period | First channel vendor-reported RPM; second not validated |
| Get_Thermal | Selector 3 | Payload byte 0 maps boost register bitfield | Investigation only; no product setter |
| Get_AP | Selector 0 | Payload 2 scenario byte | Investigation only |
| Get_AP | Selector 1 | Payload 0 fan mode byte | Investigation only |

Vendor anchors: Wmi.cs 471–522 request formation; 1045–1071 setters (excluded); 1337–1359 status stripping; 998–1031 firmware decoding. Monitor.cs 6122–6127 current temperatures and 6809–6824 tachometer pairs; BaseModule.cs 15905–15916 period conversion.

Vendor RPM formula is integer 480000/period (big-endian). It is not target percentage times a guessed maximum. Zero period here remains unavailable: it may mean no channel, invalid reading, or fan stopped; exact fan count is not inferred. Product labels the source vendor-reported, not independently calibrated.

Approved telemetry probe values: Get_Temperature(0) CPU 75°C/GPU 63°C; Get_Fan(0) first period 161 -> vendor formula 2981 RPM; second period zero. NVIDIA nvidia-smi reported RTX 2050 GPU 62°C moments later. This is a near-time GPU temperature cross-check, not synchronized calibration. HWiNFO/LibreHardwareMonitor are not installed; no independent fan/CPU comparison performed.

## EC feature map — research only, not a writable allowlist

| Feature | Offset | Values/masks | Evidence and disposition |
|---|---|---|---|
| Cooler Boost | 0x98 | bit 7, mask 0x80; preserve other bits | Vendor BaseModule.cs 12365–12437 and upstream G2_6 agree. Getter raw state 0x06 observed. No writable transport shipped. |
| Performance/shift | 0xD2 | 0xC4 extreme/turbo, 0xC1 balanced/comfort, 0xC2 eco; vendor also has other modes | Vendor enum labels differ from wire/UI labels; current raw 0xC1 observed. No automatic scenario replication. |
| Fan mode | 0xD4 | advanced bit7, silent bit4; upstream auto0x0D/silent0x1D/advanced0x8D | Current raw 0x0D. Vendor auto clears bit7 but can preserve silent; restoring curves also required. Locked. |
| Charge threshold | 0xD7 | upstream linked start/end, end+0x80; 60/80/100 -> 0xBC/0xD0/0xE4 | Exact installed vendor charge-limit setter not found in analyzed feature modules. Linux facts alone do not approve writes. Locked. |
| CPU temperature | 0x68 | upstream temperature byte | Product uses documented WMI telemetry, never raw offset. |
| CPU fan target | 0x71 | target %, not measured RPM | Not used as tachometer, do not infer stopped fan from zero. |
| GPU fan target | 0x89 | target %, not measured RPM | Not used as tachometer. |
| GPU temperature / super battery | 0x80 / 0xEB | differing support between kernel and upstream profiles | Do not combine profiles into an unverified superset. |
| Advanced curve channels | WMI selector 1 CPU / 2 GPU | Eight-byte block, preserve bytes0/7, six percent slots1–6 | BaseModule.cs 15716–15850; firmware-specific temperature breakpoints learned elsewhere. No custom-curve writes. |

Upstream GPL sources are cited for numeric facts only. Their implementations are not copied/relicensed into MIT source. Do not treat this table as instructions to write any offset.

## Auto / factory semantics

BaseModule.cs 15528–15555 restores cached Default_Fan curves and calls Auto_Fan when selecting auto. Auto_Fan (15653–15699) clears advanced bit only in the WMI path; it does not necessarily clear silent mode. Several vendor routines still write zeros/default buffers after prerequisite read failure. QuietMSI must not inherit those unsafe fallbacks.

A safe factory restoration cannot be created from guessed curves or by simply clearing boost. It is disabled in this build, with all writes.

## Driver/provider dependency

The traced managed path uses standard Windows System.Management/WMI. It has no vendor user-mode DLL or proprietary service requirement in the client. An existing MSI ACPI MOF provider remains a prerequisite; removal of MSI Center has not been tested and is not promised.

Vendor installation logic points WmiAcpi service MofImagePath at msiapcfg.dll; resource-only DLL holds MOF for ASL. Product never installs/copies it or changes registry. Standard ACPI/provider signature chain has not yet been fully attested.

Legacy branches reference WMILib.dll and KernCoreLib64.dll/.sys with port and physical-memory exports. Those branches are excluded. WinRing0/NTIOLib/RTCore were not found by bounded registered-driver search. Microsoft msisadrv.sys is ISA driver, not MSI hardware control. Bundled mouse/headset drivers are not an EC bridge.

Serial sampling uses a killable read-only child of the same executable. WMI timeout options alone do not reliably bound synchronous calls; parent cancellation/deadline terminates worker and confirms exit before releasing ownership. No abandoned overlapping reads. Identity/protected schema refreshed each sample; hardware writes remain denied.

## Capability state and remaining work

This section records v0.1. Version0.2 enables only the separately verified performance/boost/mode and conservative CPU-curve paths; details and actual restore tests are in v2-verification.md. Unknown firmware, charge limits, GPU/MUX, temperature edits and fan-stop remain unsupported. No force override or raw EC port API.

Before enabling a control: exact model + live EC firmware/WMI identity; exact installed vendor payload + applicable Linux corroboration; valid successful pre-read; masks/bounds; cross-process conflict handling; exact post-read; independently verified sensors; logged allowlisted state only; verified restoration and explicit user authorization for actual write tests. An unknown/failed read prevents the write. Never iterate registers or raw ports. No calibration or BIOS reset.
