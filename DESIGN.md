---
name: QuietMSI
description: Native Windows read-only telemetry and hardware capability status, with restrained tonal sections and system controls.
colors:
  background: "rgb(242, 244, 247)"
  surface: "rgb(255, 255, 255)"
  ink: "rgb(34, 42, 53)"
  muted: "rgb(82, 94, 108)"
  header: "rgb(31, 40, 53)"
  header-ink: "rgb(242, 246, 250)"
  accent: "rgb(50, 87, 132)"
typography:
  body:
    fontFamily: "Segoe UI"
    fontSize: "13.3333333333px"
    fontWeight: 400
  heading:
    fontFamily: "Segoe UI"
    fontSize: "16px"
    fontWeight: 700
  title:
    fontFamily: "Segoe UI"
    fontSize: "25.3333333333px"
    fontWeight: 700
  reading:
    fontFamily: "Segoe UI"
    fontSize: "26.6666666667px"
    fontWeight: 400
spacing:
  empty: "0px"
  label-top: "3px"
  control-top: "4px"
  text-control-bottom: "5px"
  temperature-bottom: "8px"
  section-vertical-and-gap: "12px"
  scroll-top: "14px"
  common-inset: "16px"
  scroll-horizontal: "18px"
  header-horizontal: "22px"
components:
  window-body:
    backgroundColor: "{colors.background}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
  identity-header:
    backgroundColor: "{colors.header}"
    textColor: "{colors.header-ink}"
  section:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.muted}"
  section-heading:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.muted}"
    typography: "{typography.heading}"
  temperature-reading:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    typography: "{typography.reading}"
  refresh-button:
    textColor: "{colors.ink}"
  export-button:
    textColor: "{colors.accent}"
  diagnostics:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    height: "86px"
---

# Design System: QuietMSI

## Overview

**Creative North Star: "The Quiet Instrument Panel"**

This is an extraction of the built native .NET 8 WinForms visual system, not a proposed redesign. Its charcoal identity header, light neutral sections, and ordered telemetry rows translate the approved music-library detail-view direction into a practical Windows utility. Brand expression resides in hierarchy and restrained color, not a music costume or custom control chrome.

The system uses code-led layout, Windows UI fonts, standard controls and system iconography. Its current release is read-only: apparent hardware settings are disabled capability disclosures, not working configuration features. The document records actual source values; it does not unlock controls, promise calibrated telemetry, or establish runtime accessibility compliance.

**Key Characteristics:**
- Clear device and sampling context above a scrollable single-column workspace.
- Two equally weighted temperature readings, followed by ordered source/status rows.
- Flat tonal separation and one muted blue action accent.
- Native buttons, fields, window chrome and tray behavior.
- Explicit unavailable, stale and locked states instead of fabricated values.

**Extraction authority:** `src/QuietMSI/UI/Theme.cs`, `MainForm.cs` and `TrayContext.cs`; product direction in `PRODUCT.md` and `docs/superpowers/specs/quietmsi-design.md`. Frontmatter colors preserve the built RGB tuples. The DESIGN.md CLI rejects point units, so frontmatter font sizes are calculated 96-DPI equivalents (`points × 96 / 72`), not measured glyph sizes or a runtime font change. Exact source point sizes remain in Typography and the native sidecar. Spacing dimensions serialize WinForms baseline pixel values, not a CSS implementation or physical device measurements. `.impeccable/design.json` holds native-specific extensions and the bounded finish-review record.

## Colors

The normal palette combines cool pale neutrals with a charcoal header and a muted blue accent. Frontmatter is normative; the source remains the runtime authority.

### Primary
- **Muted blue — `accent`:** diagnostics export text. It is not an authored button fill or universal focus-ring color; Windows controls own those treatments.

### Neutral
- **Cool workspace — `background`:** form, scroll host and body stack.
- **Plain surface — `surface`:** section panels, temperature groups and diagnostics field.
- **Instrument ink — `ink`:** form foreground, large readings, Refresh text and diagnostics text.
- **Supporting slate — `muted`:** default labels, including section headings. Do not quietly substitute lighter text.
- **Charcoal identity — `header`:** machine, firmware and sampling header.
- **Light header ink — `header-ink`:** all header labels.

**The System Contrast Rule.** High contrast replaces authored colors with Windows system colors: Background/Surface/Header → `SystemColors.Window`; Ink/Muted/HeaderInk → `SystemColors.WindowText`; Accent → `SystemColors.Highlight`. No fixed high-contrast RGB values are specified. Control construction assigns colors from these getters; runtime mode-switch propagation is not established.

Calculated normal-theme text/background ratios from the source tuples: Ink/Surface **14.48:1**; Muted/Surface **6.61:1**; Muted/Background **6.00:1**; HeaderInk/Header **13.68:1**; Accent/Surface **7.42:1**. These clear the body-text contrast floor for these pairs only. They do not certify native disabled controls, OS focus chrome, or physical high-contrast rendering.

## Typography

Segoe UI is the only authored family. There are no downloaded fonts, vendor font files, mono styling, tracking tokens or authored line-height values. Text measurement and fallback remain Windows/.NET behavior.

### Hierarchy
- **Title:** source `Theme.Title`, bold, 19 points; application name in the header.
- **Heading:** source `Theme.Heading`, bold, 12 points; machine identity and section titles.
- **Reading:** source `Theme.Reading`, regular, 20 points; CPU/GPU value or `Unavailable`.
- **Body:** source `Theme.Body`, regular, 10 points; labels, sources, reasons and inherited control text.

**The Instrument Type Rule.** Use the installed Windows UI face and established hierarchy. Do not introduce an external display face, oversized marketing typography, arbitrary font smoothing, or decorative motion to this native Operate surface. Web craft prescriptions are adapted to the explicitly approved native platform.

## Layout

The form starts centered with a **560 × 740 client area** and **440 × 480 minimum outer-window size**. These are distinct coordinate domains; the minimum is not a minimum client size. `AutoScaleMode.Dpi` is set. Neither value should be confused with the supplied **600 × 700 full-window live capture**.

The header is top-docked and autosized. The fill-docked scroll panel hosts a top-docked, autosized one-column body. Both header and sections use `TableLayoutPanel` stacks with a single 100% column, `GrowAndShrink`, and empty stack margins. No web breakpoints, sidebar, custom canvas, or fixed card-height grid exists.

Source `Padding` uses **left, top, right, bottom** order; frontmatter uses **top, right, bottom, left** serialization:

| Location | Exact source value / behavior |
|---|---|
| Header padding | `(22, 16, 22, 16)` |
| Scroll host padding | `(18, 14, 18, 16)` |
| Section padding | `(16, 12, 16, 12)` |
| Section margin | `(0, 0, 0, 12)` |
| Default label margin | `(0, 3, 0, 5)` |
| Temperature table margin | `(0, 4, 0, 8)` |
| Button margin | `(0, 4, 0, 5)` |
| Temperature columns | Two equal 50% columns |
| Action row | Autosized `FlowLayoutPanel`, wrapping enabled, empty margin |
| Other stack/group padding | `Padding.Empty` where explicitly set |

Native autosizing permits wrapped labels. `AutoScroll` makes lower Performance, Cooling, Battery care and Safety & diagnostics sections available beyond the viewport. A section crossing the viewport boundary is not text clipping. Preserve the logical sequence and label/value grouping rather than forcing every control into the first screen.

Compact-resize and programmatic 150% scaling checks are reported in existing verification. They do not establish actual multi-monitor DPI transitions, full enlarged-copy fit, localization behavior or screen-reader usability.

## Elevation & Depth

Sections are flat, separated by neutral background and spacing. There are no authored shadows, glows, gradients, blur, textures, entry animations or elevation tokens. Standard Windows window/control rendering may supply its own depth; that is not an app-authored shadow system.

## Shapes

The application uses rectangular layout panels and standard WinForms controls. No custom corner-radius, stroke-width, hover-color or focus-ring token is authored. Do not extract a radius from a screenshot of OS-themed controls and present it as a source constant. Window chrome, scrollbars, button edges, selection/caret and disabled appearance remain platform-owned.

The window and tray use `SystemIcons.Application`. No proprietary logo, vendor assets, raster artwork or shipping screenshot decoration is required. The supplied PNG files are evidence artifacts, not product UI assets.

## Components

### Identity header
The app title, manufacturer/model, BIOS/EC and read-only sampling line form one vertical stack, followed by Refresh. Header labels use HeaderInk. Missing identity begins as explicit unavailable text; a sampled unknown identity currently becomes `Unknown Unknown`. Refresh receives initial focus so the content does not open scrolled to diagnostics.

### Telemetry section
The built heading is **Live telemetry**. CPU/GPU labels precede large readings. Valid temperatures render to one decimal place with `°C`; absent or invalid readings render `Unavailable`. Fan rows distinguish **RPM reported** from **Target …% (not RPM)** and retain the source line. Battery/AC data is independently available from Windows and does not establish protected temperature/fan access. Provider/status text wraps below the power row.

### Native actions
Buttons autosize with `GrowAndShrink`, **minimum height 32**, and the recorded button margin; width is content-dependent. `UseVisualStyleBackColor = true` leaves their background/state styling to Windows. Refresh uses Ink text; Export diagnostics uses Accent text. `&Refresh` and `Export &diagnostics…` carry native mnemonics. Do not document invented hover or pressed colors.

### Locked hardware fields
Performance is a **280-wide** drop-down-list selector with an unavailable option selected, plus disabled Apply profile. Cooling includes a disabled checkbox, Apply Cooler Boost, a read-only curve field, Apply fan curve and Return fans to BIOS automatic. Charge limit uses a **90-wide** numeric field with range **50–100** and proposed value **80**; nearby copy explicitly says this is not the current hardware limit.

Every `Hardware_*` control starts disabled and remains disabled in `RefreshCapabilities`, regardless of a policy result. The UI contains no write handlers. Each capability has an adjacent `Locked · …` reason. These are deliberate safety states, not a partially implemented success interaction. MUX has no built UI control; do not fabricate one in this system document.

### Diagnostics and recovery
The multiline diagnostics field is read-only, **86 high**, top-docked, uses Surface/Ink, and has its own vertical scrollbar. Local export uses a Windows save dialog and completion/error dialogs, including an identity-sharing caution. Poll failure retains prior readings and marks them stale; diagnostics says no hardware command was sent and offers Refresh/export. Initial waiting, suspension and resumed-waiting copy also exist in source. There is no authored visible busy/progress state during an in-flight poll.

### Tray navigation
Standard menu items are Open QuietMSI, Refresh telemetry and Exit. The icon tooltip includes Read-only. Close/minimize hides the window; explicit tray Exit disposes it. This unusual close behavior is explained in the lower Safety & diagnostics section, not in the first visible viewport.

### Accessibility hooks and limits
The form, scroll region, readings, hardware fields and diagnostics have explicit `AccessibleName` assignments; native mnemonics, controls and initial focus provide a useful baseline. `TextLabel` names are set when constructed while dynamic text subsequently changes, so source inspection alone does not guarantee correct current announcements. No screen-reader validation is claimed. Physical high contrast and monitor transitions remain unverified; do not treat palette branching or a static screenshot as equivalent evidence.

## Do's and Don'ts

### Do:
- **Do** preserve Segoe UI and the exact extracted Theme roles.
- **Do** retain visible labels, units, sources, capability reasons and scroll-managed sections.
- **Do** distinguish unavailable readings, stale retained readings and proposed setting values.
- **Do** leave native control chrome and keyboard affordances intact.
- **Do** describe this build as a read-only telemetry/capability utility.

### Don't:
- **Don't** invent readings, current hardware settings, calibrated RPM or write success.
- **Don't** unlock hardware controls without the separately validated safety contract.
- **Don't** advertise the unavailable-data smoke fixture as live device telemetry.
- **Don't** claim full MSI Center replacement, verified physical high contrast or tested multi-monitor behavior.
- **Don't** add vendor fonts/assets, raster decoration, web UI dependencies or decorative animation.
- **Don't** turn OS-derived button styling into invented authored tokens.
