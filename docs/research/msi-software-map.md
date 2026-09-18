# MSI Software Architecture Map

## Component Architecture

| Component | Type | Version | Runs as | Purpose | Talks to | Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| *Awaiting Phase 0 discovery* | | | | | | |

## Call Graph / Control Chain

```text
[ MSI Center UI ]
       │
       ▼ (IPC / Named Pipe / RPC / Localhost HTTP)
[ MSI Background Service ]
       │
       ▼ (WMI / DeviceIOControl / ACPI)
[ MSI WMI Provider / Kernel Driver ]
       │
       ▼
[ ACPI / Embedded Controller (EC) / BIOS ]
```
*(To be updated based on Phase 0 and Phase 1 traces)*
