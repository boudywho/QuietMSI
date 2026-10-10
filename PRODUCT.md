# QuietMSI

<!-- impeccable:product-schema 1 -->

## Platform

Windows desktop (native WinForms). This platform is outside Impeccable's web/iOS/Android schema; do not mislabel it as web or load mobile guidance.

## Stack

Delegated and approved: C# .NET 8 native tray app with MIT license. Build directly in code for this native utility; the user delegated design and intermediate approvals. No WebView/Electron or external fonts. Publish a framework-dependent portable executable using the installed .NET Desktop runtime, with source and reproducible build instructions.

## Users

The owner of an MSI GF63 Thin 11UCX who wants laptop telemetry and validated hardware controls without MSI Center's bundled utilities.

## Product Purpose

Replace the necessary control surface with a compact modern tray application. Reduce background work and dependencies without compromising thermal or battery safety.

## Operating Context

Windows 11, GF63 Thin 11UCX, BIOS E16R6IMS.112. MSI Center UI 2.0.74.0 and SDK 3.2026.0911.01 are installed. Existing MSI services remain untouched until a separately approved migration. Local analysis and build run unelevated.

## Capabilities and Constraints

- Intended controls: performance profiles, Cooler Boost, battery charge limits, fan curves, return to BIOS automatic fans; MUX only if genuinely supported.
- All hardware writes require decompiled vendor evidence, applicable Linux mapping, and validated pre/post reads. No raw I/O port access, blind EC scans, unverified drivers, calibration operations, or guessed defaults.
- Unknown/unverified controls are disabled with specific reasons. Missing telemetry renders unavailable, never zero or invented values.
- No ads, analytics, accounts, automatic downloads, bundled drivers, peripheral/RGB utilities, or installer service.
- Resource targets: under 10 MB portable binary, under 30 MB working set, below 0.5% idle CPU. Measure actual results; these are goals, not established claims. A self-contained .NET distribution is not expected to fit 10 MB.
- Do not bundle or publish proprietary MSI binaries or decompiled source. MIT applies to independently written code only. Refer to Linux facts without copying GPL implementation.
- No automatic writes on startup, polling, resume, UI selection, or process exit. Changes require an explicit Apply action; restoration must be a verified operation, not a presumed factory reset.

## Evidence on Hand

Discovery report and inventories under C:/Users/WindowsHostage/AppData/Local/hermes/cache/scratch/. MSI_ACPI WMI metadata is present. EC firmware identity, telemetry scaling, writable payloads, and independent telemetry validation are not established yet.

## Product Principles

1. Fail closed on unknown hardware and firmware.
2. Honest capability status outranks feature count.
3. Local operation and reproducible source, no proprietary runtime dependency by default.
4. Explicit, logged, bounded hardware changes only.
5. Modern Windows conventions without decorative animation or heavy UI dependencies.
