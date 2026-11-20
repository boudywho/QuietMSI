# QuietMSI interface design

QuietMSI is a small native Windows control panel. The dashboard prioritizes
daily adjustments and never makes a draft look like a hardware change.

## Views

- **Overview:** system telemetry first, then Performance and Cooling actions.
  CPU and GPU temperatures, firmware-reported fans, battery and power source
  use fixed-size cells. At wide widths the performance and cooling panels sit
  side by side. They stack at narrow widths and scroll without hiding actions.
- **Cooling:** the chart uses a real temperature axis and lists the six observed
  values. The solid line is the reported curve; the dashed line is the local
  draft. The six percentage inputs are keyboard editable, with fixed temperature
  breakpoints. Restore means the captured session curve, not a factory reset.
- **Settings:** appearance, startup, manual update checks, and device/app support
  details. Settings do not affect hardware state.
- **Status:** a persistent footer reports freshness and the latest action result.
  Unknown values are **Unavailable**; stale values are last-known readings, not
  permission to write. Extra provider context is available in tooltips and
  accessibility descriptions.

## Visual system

Dark by default; persisted System and Light preferences retain their meanings.
The dark palette pairs graphite `#0d1418` and `#172227` with mint `#73e6be`.
Use Segoe UI, thin dividers, square panels, visible keyboard focus and restrained
spacing. System high contrast uses Windows colors. Windows owns the caption,
window controls, snap and resize frame. No animated transitions, web view,
external fonts or rendering dependencies are used.

The logical client size is 820 × 650, adjusted to the monitor working area.
When the inner layout reaches 650 logical units, Overview controls and Settings
use two columns. Navigation and status remain outside the scrolling area.

## Hardware interaction

Selection updates the draft locally. Only explicit Apply or Restore can send a
command after a fresh capability check. Failed or unconfirmed commands preserve
the draft and never trigger automatic retries. Startup, navigation, refresh,
suspend, resume and exit never issue control commands. Existing hardware
allowlists and readback remain authoritative.

Source: `src/QuietMSI/UI/MainForm*.cs`, `DashboardControls.cs`, `NativeAppearance.cs`
and `Theme.cs`. See the [static layout reference](docs/quietmsi-preview.html) and
[Windows acceptance checklist](docs/overhaul-windows-checklist.md).
