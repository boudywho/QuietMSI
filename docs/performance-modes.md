# Performance levels on GF63 Thin 11UCX

QuietMSI's **Apply profile** sends a firmware performance-level request. Selecting
a button stages a choice; it takes effect only after Apply and hardware readback.

| QuietMSI level | Intended policy | Firmware encoding | Vendor displayed SHIFT label |
| --- | --- | --- | --- |
| Turbo | Highest performance request | 0xC4 | Turbo |
| High | Favor performance | 0xC0 | Sport |
| Medium | Balance performance and power | 0xC1 | Comfort |
| Eco | Favor lower power consumption | 0xC2 | ECO |

The request changes EC register0xD2 through the guarded Set_Data protocol. It
does not itself switch Windows power plans, set CPU/GPU overclock offsets,
change the fan curve, select Auto/Advanced, or enable Cooler Boost. Automatic
firmware cooling can still react to changing load and temperature.

Under load, the intended tradeoff is performance versus power consumption and
heat. At idle, changing the profile may produce little visible difference.
Exact CPU/GPU wattage limits, clocks, frame rates and temperature/noise changes
have not been measured for these four requests on this machine. In particular,
Turbo has not been shown faster than High here. The firmware owns that behavior;
the managed vendor code establishes the request encoding, not its physical
implementation. The Turbo label does not mean that Intel Turbo Boost is disabled
in every other level, and QuietMSI does not apply MSI's optional GPU overclock.

Fan controls are separate: Auto follows firmware, Advanced uses the existing
curve, and Cooler Boost requests extra cooling. Medium plus Auto with Cooler
Boost off is the baseline observed in the recent validation.

## Evidence and naming

Original Base Module file version1.0.2609.0201, SHA-256
`104e412ff9a95f49b7204f9e43eb33e9c98c18d60712740b84ee21e288331795`,
MVID `c17bac71-d2d1-4139-9a0c-a07f928fc547`, REA Evidence
`ev_7462debf9f8b3fb99aa5f1777f8d1ea2d9e0f19ec998e01648abd0729d5832da`:

- CalculateNewShiftModeValueInEC, token0x06000080: wire encodings.
- SetShiftModeValueInEC, token0x06000083: register0xD2.
- ChangeShiftModeUI, token0x0600008A: displayed mode names.

The vendor's internal enum names are older: SportMode displays Turbo,
ComfortMode displays Sport, and GreenMode displays Comfort. Reading only those
enum names would mislabel the same firmware bytes.

[MSI's official Center explanation](https://www.msi.com/support/technical_details/NB_%20SW_MSI_Center)
also describes user performance level and fan speed as separate controls, with
additional Turbo overclock settings only on supported hardware. These firmware
levels are narrower than MSI's full User Scenario presets.
