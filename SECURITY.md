# Safety and security

QuietMSI is an independently written native Windows utility. It is not affiliated with MSI. MIT licensing covers QuietMSI code only; .NET and dependencies retain their own licenses. MSI binaries/decompiled source and GPL driver implementation are not included.

Version0.2 has a narrowly writable backend for exact GF63Thin11UCX/EC16R6EMS1.106/WMI2.8. Explicit Apply supports firmware performance levels, CoolerBoost, Auto/Advanced with validated current curve, and conservative CPU curve increases plus exact captured-original restoration. Unknown identity/failed state denies writes. No raw EC port/memory access, arbitrary selector API, driver/service installation, calibration or BIOS/UEFI setters. No hardware changes on startup/poll/resume/exit.

Do not mistake a related chassis, BIOS string, or MSI_ACPI method name for a verified writable firmware policy. The local machine EC firmware was read in a separately approved administrator probe as 16R6EMS1.106. A normal unelevated process cannot use the protected firmware getter here. BIOS E16R6IMS.112 is a separate identifier.

Telemetry failures remain unavailable. Target percentage is not RPM. The first tachometer uses decompiled vendor period conversion and is labeled reportedRPM; independent calibration is not certified. CPU curve edits preserve unknown table bytes/terminal ceiling and fixed temperatures; no lower-than-session-original speed, GPU curve, fan-stop or factory-reset claim.

Version0.2 explicitly requests administrator permission with standard Windows UAC as approved by the user. It does not bypass UAC, use/install a helper service, lower ACLs, modify registry security, repair WMI, start/stop MSI services, or weaken their signature checks. Existing MSI Foundation pipe rejects unsigned clients; no bypass is implemented. Never disable antivirus, vulnerable-driver protections, or Windows integrity controls.

The product launches only its own short-lived read-only sampling process; no arbitrary command/shell execution or writable helper service. The parent enforces a10-second deadline and confirms termination before allowing another read; failed termination quarantines future reads. Every worker verifies identity afresh. UI age checks mark overdue snapshots stale independently of completion.

No network functionality, advertising, analytics, accounts, auto-updater, or automatic startup registration is implemented. Diagnostics are exported only to an explicit local destination and may contain device identity and timestamps; inspect before sharing. Do not redistribute private MSI analysis files.

Hardware operations require exact live model/EC/schema matching, successful fresh pre-state and conflict read, flushed local pending journal, known mask/table bounds, exact post-read, and no retry/guessed rollback. User approved reversible real tests; original states restored. MSI Center may compete for settings, so drift is treated as conflict. An in-flight setter is not canceled/abandoned on user exit; exit waits for result/readback. No guarantee of atomic rollback across firmware calls or thermal behavior is claimed. Additional unsupported controls need their own evidence and approval.
