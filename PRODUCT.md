# QuietMSI

<!-- impeccable:product-schema 1 -->

## Platform

Windows desktop (native WinForms). This platform is outside Impeccable's web/iOS/Android schema; do not mislabel it as web or load mobile guidance.

## Stack

C# .NET 8 native tray app with MIT license. No WebView/Electron or external fonts.
The installer and portable ZIP are self-contained and require no separate .NET
Desktop Runtime installation. Build instructions are in [README.md](README.md).

## Users

The owner of an MSI GF63 Thin 11UCX who wants laptop telemetry and validated hardware controls without MSI Center's bundled utilities.

## Product Purpose

Replace the necessary control surface with a compact modern tray application. Reduce background work and dependencies without compromising thermal or battery safety.

## Operating Context

Windows x64, with hardware support validated on GF63 Thin 11UCX, EC
16R6EMS1.106 and WMI 2.8. MSI Center is not required. See the
[standalone evidence](docs/standalone-verification.md) for validation scope.

## Capabilities and Constraints

- Supported controls: performance levels, Cooler Boost, Auto/Advanced fan mode, and the bounded CPU fan curve. No battery charge limits, MUX or GPU curve control.
- All hardware writes require decompiled vendor evidence, applicable Linux mapping, and validated pre/post reads. No raw I/O port access, blind EC scans, unverified drivers, calibration operations, or guessed defaults.
- Unknown/unverified controls are disabled with specific reasons. Missing telemetry renders unavailable, never zero or invented values.
- No ads, analytics, accounts, automatic downloads, bundled drivers, peripheral/RGB utilities, or installer service.
- Resource targets: under 10 MB portable binary, under 30 MB working set, below 0.5% idle CPU. Measure actual results; these are goals, not established claims. A self-contained .NET distribution is not expected to fit 10 MB.
- Do not bundle or publish proprietary MSI binaries or decompiled source. MIT applies to independently written code only. Refer to Linux facts without copying GPL implementation.
- No automatic writes on startup, polling, resume, UI selection, or process exit. Changes require an explicit Apply action; restoration must be a verified operation, not a presumed factory reset.

## Evidence on Hand

See [hardware support](docs/hardware-specs.md) and [standalone verification](docs/standalone-verification.md) for demonstrated model and firmware behavior. RPM is firmware-reported, not independently calibrated.

## Product Principles

1. Fail closed on unknown hardware and firmware.
2. Honest capability status outranks feature count.
3. Local operation and reproducible source, no proprietary runtime dependency by default.
4. Explicit, logged, bounded hardware changes only.
5. Modern Windows conventions without decorative animation or heavy UI dependencies.
