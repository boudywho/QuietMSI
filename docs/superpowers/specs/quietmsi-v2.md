# QuietMSI correction: verified hardware workflow

User rejected the read-only release as not meeting needs. Approved required scope: performance levels, Auto/Advanced fan modes/curves, Cooler Boost and telemetry. Existing MSI DLLs/services allowed if minimal; extra service allowed if necessary. Selected startup model: one UAC prompt, elevated app, direct WMI; no added service unless a verified need appears. Previous design approvals remain delegated.

## Root cause and transport

Protected WMI denied unelevated. Existing MSI Foundation pipe was tested through installed client DLL and independent protocol; service rejects unsigned callers through protected-path/allowlist/Authenticode checks (Foundation.cs1951–2001). Do not bypass/patch. Direct elevated WMI telemetry already works. Product should requestAdministrator in manifest and clearly disclose elevation. Test final published elevated app—not only reader fixtures.

## Working acceptance

Actual device identity/CPU temp/first fan reported RPM available after UAC; GPU missing when asleep remains unavailable. Display first channel as Fan1, not invented GPU fan. Show current performance/boost/mode from validated getters. Profiles label firmware performance levels (Turbo/High/Medium/Eco), not claim full MSI scenario side effects/overclocking. Current curves and firmware temperature points read-only first; six fixed breakpoints, bounds never cast unchecked.

Enable CoolerBoost0x98mask80, performanceD2 validatedC4/C0/C1/C2, autoD4 current baseline0D for exact GF63Thin11UCX EC16R6EMS1.106 WMI2.8 only after reversible live tests. All writes pre-read+fresh conflict re-read, append-only local state journal before invocation, successful status and exact masked post-read; no retries of uncertain hardware writes. Restore only exact saved state when current value still matches our change. Never blind-register scans/ports, fallback zero or full arbitrary setter API. Hold write/read ownership; known-user Apply only; no writes startup/exit.

Original Linux corroboration remains required. Vendor advanced Set_Fan selector1/2 payload is traced but the Linux project's advanced-mode selector alone is not editable-curve corroboration. Custom curve write remains blocked unless that gap is resolved; present current curve honestly and explain exact blocker, not grey fictional global UI. Auto means clear advanced/silent and boost with verified baseline mask; does not imply default/factory curve reset. Don't overwrite curve tables when switching mode without verified captured original/default table.

Actual write tests require explicit user approval, pre-state recorded and post-state verified; stop on mismatch/no retry. Existing MSI scenario agents may overwrite values: don't kill/stop services without separate approval; detect drift and state possible conflict.

## Engineering

Native UI compact redesigned functional control rows, busy/error/permission states. Backend interfaces return capability based on verified live model and state; mock ReadOnlyBridge always denies. Keep killable read worker; separate fixed Apply worker request using stdin, no shell or arbitrary method. Never kill an in-flight write and presume no change; timeout marks indeterminate, blocks further writes until new verified read; journal pending before each step. Manifest inherited child elevation means no repeated UAC.

Tests must prove actual control workflow, not merely locked buttons/window launch. Reversible live UAC diagnostics and selected write tests; source/portable metadata updated with real scope. No MSI redistributable DLL included unless license permits; runtime installed files only. No fake normal-user service compatibility claim. No budget claim unless measured across resident+worker processes.
