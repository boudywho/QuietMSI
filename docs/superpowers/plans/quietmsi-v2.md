# QuietMSI v2 implementation plan

Spec: docs/superpowers/specs/quietmsi-v2.md. Autonomous execution retained. No service bypass or proprietary redistribution.

1. Fix real startup permissions: requestAdministrator manifest; publish isolated v2; actual published elevated diagnostics readback required. Completed probe EC.106/CPU87/GPU61/Fan13057. Installed MSI service denied unsigned client under real Authenticode gate; don't bypass.
2. Add control state getters Thermal3/AP0/AP1/curve1/temperature1, decode exact status/length. Test packet/mask/unknown-state behavior before implementation. State is original raw values, not fabricated defaults.
3. Define narrow plans: boost98bit7, performanceD2c0/c1/c2/c4, autoD4knownbaseline0d. Require current exact identity,WMI2.8, fresh pre-read/conflict-read, local flushed journal, exact postread. No arbitrary selector/value API. Curve write awaits Linux corroboration, known breakpoint bounds and original table restoration. Original read-only bridge remains denied.
4. Add UI current levels/boost/fanmode and explicit apply with busy/error/result; show permission/provider problems prominently; display Fan1. Read worker and write serialization nonoverlap, no auto writes. Clarify auto controller mode vs factory curve reset.
5. User approves individual reversible actual write tests; preserve original state, postverify, restore exact state, document result. Don't infer safety from synthetic unit tests. Detect MSI Center conflicts, no service stops without approval.
6. Review source and screenshot from final elevated published build; test launch/read/control/readback/restore, pack source + exe, updated scope/dependencies/elevation/footprint. Do not issue another disabled-placeholder replacement.
