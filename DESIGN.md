---
name: QuietMSI
description: Native laptop dashboard, matching light/dark themes and explicit verified Apply actions.
colors:
  background: "rgb(245, 246, 248)"
  surface: "rgb(255, 255, 255)"
  ink: "rgb(17, 30, 48)"
  muted: "rgb(85, 103, 123)"
  border: "rgb(226, 229, 233)"
  selected: "rgb(230, 247, 246)"
  accent: "rgb(0, 127, 125)"
  accent-ink: "rgb(255, 255, 255)"
  warning: "rgb(176, 92, 0)"
  dark-background: "rgb(20, 23, 29)"
  dark-surface: "rgb(28, 32, 39)"
  dark-ink: "rgb(235, 242, 247)"
  dark-muted: "rgb(169, 186, 201)"
  dark-border: "rgb(48, 54, 63)"
  dark-selected: "rgb(32, 65, 68)"
  dark-accent: "rgb(73, 207, 197)"
  dark-accent-ink: "rgb(15, 34, 36)"
  dark-warning: "rgb(255, 181, 85)"
typography:
  small:
    fontFamily: "Segoe UI"
    fontSize: "12.000000px"
    fontWeight: 400
  body:
    fontFamily: "Segoe UI"
    fontSize: "13.333333px"
    fontWeight: 400
  heading:
    fontFamily: "Segoe UI"
    fontSize: "17.333333px"
    fontWeight: 700
  title:
    fontFamily: "Segoe UI"
    fontSize: "22.666667px"
    fontWeight: 700
  reading:
    fontFamily: "Segoe UI"
    fontSize: "34.666667px"
    fontWeight: 700
rounded:
  button: "8px"
  card: "14px"
spacing:
  gap: "12px"
  card-inset: "18px"
  workspace-inset: "22px"
components:
  card:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.card}"
    padding: "18px"
  card-dark:
    backgroundColor: "{colors.dark-surface}"
    textColor: "{colors.dark-ink}"
    rounded: "{rounded.card}"
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.accent-ink}"
    rounded: "{rounded.button}"
    typography: "{typography.body}"
  button-primary-dark:
    backgroundColor: "{colors.dark-accent}"
    textColor: "{colors.dark-accent-ink}"
    rounded: "{rounded.button}"
  navigation-selected:
    backgroundColor: "{colors.selected}"
    textColor: "{colors.accent}"
    rounded: "{rounded.button}"
---

# Design System: QuietMSI 0.4

The interface is a small native Windows laptop control panel. G-Helper's [compact grouped settings](https://github.com/seerge/g-helper) informed the hierarchy; implementation, artwork and hardware semantics are original QuietMSI code. There is no borrowed source or branding.

## Structure

The default client area is 560 × 700 logical units, adjusted for Windows DPI and the screen working area. Controls is the opening view: one telemetry section, visible High / Medium / Turbo / Eco performance buttons, then fan mode and Cooler Boost. Keep the common actions in the opening viewport. At wide sizes, telemetry and curve preview sit beside the controls; small windows scroll vertically.

Fan curve is a separate view with a read-only chart above a six-point percentage editor. Show all six inputs on one row where space permits, otherwise use three columns. Temperatures stay fixed. The editor explains session-original restoration and exposes explicit Apply and Restore actions.

There is no Diagnostics navigation, diagnostics text panel or export button. Firmware identity, sensor sources and detailed unavailable reasons use tooltips and accessibility descriptions. Current hardware state stays visible separately from editable choices.

## Visual roles

Use the frontmatter palette and source Theme.cs. Background is a neutral gray or deep charcoal; cards use a subtly lighter surface without outlines outside high contrast. Teal identifies the selected profile, current state, curve and primary Apply actions. Heading labels use Ink, supporting copy uses Muted. Keep the palette restrained and avoid duplicate gauges or decorative status graphics.

Use Segoe UI: Small 9pt, Body 10pt, Heading 13pt bold, Title 17pt bold, Reading 26pt bold, CompactReading 18pt bold. Temperature units stay with their value. Unknown sensors display Unavailable; never render a guessed value or treat a missing fan channel as stopped.

Cards have an 18-unit inset and 12-unit spacing; the viewport has a 22-unit side inset. Manual geometry uses LogicalToDeviceUnits so the header, readings, chart, footer and editor track Windows DPI. The minimum outer window remains 440 × 480 pixels, with scrolling and keyboard reveal for off-screen controls.

The native selector/spinner skins use the same palette in both themes. The window has an integrated caption with minimize, maximize/restore and close-to-tray buttons; native resizing, drag/snap and taskbar-aware maximization remain available. DWM supplies rounded corners on Windows 11. System follows the current Windows appearance; settings are stored locally. High contrast uses Windows system colors.

## Interaction

Performance levels are four native focusable buttons. Selection changes the draft and shows a confirmation hint; Apply sends the request. Fan mode and Cooler Boost retain separate explicit Apply actions. Navigation and theme selection never request hardware changes.

Polling preserves the selected draft, focused control and scroll position. Keyboard Tab and Shift+Tab reveal their target. Invalid, stale or suspended state disables hardware changes; detailed reasons remain available to assistive technology and pointer users. The status footer reports freshness, pending operation, success or failure without requiring another view.

Hardware transport, capability checks, curve floors/cap, monotonic validation and post-apply readback remain authoritative. UI layout changes must never bypass these checks.

## Identity

The original Q/airflow mark uses a mint Q with three white airflow strokes on a rounded charcoal tile. SVG is editable artwork; the PowerShell generator reproduces its original geometry. The Windows ICO contains 16, 24, 32, 48, 64, 128 and 256 pixel frames; the 16 pixel frame omits the interior strokes for clarity. Embed the ICO in the executable and use the same identity in the title bar, header and tray.

Source authority: MainForm.cs, ProfileSelector.cs, Theme.cs, DashboardControls.cs, NativeAppearance.cs, ThemeSettings.cs, AppIcon.cs and TrayContext.cs. Current verification is recorded in docs/ui-overhaul.md. Earlier dashboard/finish review files describe the superseded 0.3 layout.