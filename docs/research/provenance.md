# Research Provenance & Prior Art

This ledger records external knowledge sources, tools, drivers, and reverse-engineering findings.
All external references start at **E1 (Prior art)** and must not be assumed valid for the target laptop until verified at **E3/E4**.

## External Projects & Reference Implementations

| Project | URL / Repository | Primary Focus | Transport / Access Method | Notes / Applicability |
| :--- | :--- | :--- | :--- | :--- |
| **G-Helper** | `seerge/g-helper` | ASUS laptop control | ASUS ACPI / WMI / Win32 | Architectural model for lightweight control |
| **Linux msi-ec** | Linux kernel `drivers/platform/x86/msi-ec.c` | MSI laptop EC driver | Direct ACPI EC (`ec_read` / `ec_write`) | Model-specific EC register mappings; covers older & newer generations |
| **BeardOverflow/msi-ec** | `BeardOverflow/msi-ec` | Linux EC control | EC registers via ACPI | Community driver for various modern MSI laptops |
| **YAMDCC** | `dmitry-s93/YAMDCC` | MSI Dragon Center clone | WMI / ACPI & direct EC | Fan curves, Cooler Boost, power limits |
| **GhostDeck** | `Pr3d4t0r/GhostDeck` | MSI Center alternative | ACPI-WMI without kernel driver | Custom fan curves and telemetry via WMI |
| **ChillSpot** | `skaam/ChillSpot` | MSI laptop fan / battery tool | WMI / ACPI | Fan curves, Cooler Boost, battery thresholds |
| **MControlCenter** | `YoCodingMonster/MControlCenter` | Linux MSI control center | Direct EC / WMI | Fan profiles, battery, hybrid GPU switching |
| **MsiEcRamEditor** | `YoCodingMonster/MsiEcRamEditor` | Windows EC RAM utility | ACPI-WMI | Dumps and modifies EC RAM via Windows WMI |
| **OpenRGB** | `OpenRGB-org/OpenRGB` | Per-key & zone RGB | USB HID / I2C / SMBus | SteelSeries HID vs MSI Mystic Light protocol distinction |
| **LibreHardwareMonitor**| `LibreHardwareMonitor` | Hardware telemetry | Win32 / WMI / Driver | CPU/GPU sensor queries without ring-0 write |
| **RWEverything** | `rweverything.com` | Hardware diagnostic tool | Direct ring-0 kernel driver | EC RAM inspection reference |
| **SubZero** | Community references | Legacy MSI fan tool | Direct EC | Historical reference for legacy EC addresses |

## MSI Internal / Testing Tools

| Tool Candidate | Sources / Leads | Capabilities | Status |
| :--- | :--- | :--- | :--- |
| **MSI Command Center** | Historical MSI desktop utility | Fan curve, thermal tuning | Documented E1 |
| **MSI Fan Test Utility** | Reddit / MSI forums | Service-level fan testing | Investigating E0 |
