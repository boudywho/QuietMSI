# Safety and security

QuietMSI is an independently written native Windows utility. It is not affiliated with MSI. MIT licensing covers QuietMSI code only; .NET and dependencies retain their own licenses. MSI binaries/decompiled source and GPL driver implementation are not included.

The initial backend is deliberately read-only. ApplyAsync refuses every ControlKind and contains no writable WMI method, EC port access, driver installation, service control, BIOS/UEFI setter, or calibration routine. Disabled controls cannot be enabled by configuration.

Do not mistake a related chassis, BIOS string, or MSI_ACPI method name for a verified writable firmware policy. The local machine EC firmware was read in a separately approved administrator probe as 16R6EMS1.106. A normal unelevated process cannot use the protected firmware getter here. BIOS E16R6IMS.112 is a separate identifier.

Telemetry failures must remain unavailable. Target fan percentage is not measured RPM. Zero target does not prove a stopped fan. No fan tachometer mapping or custom curve is approved by this build.

Windows may restrict WMI despite the provider metadata being present. The product does not silently request UAC, use a helper service, lower ACLs, modify registry security, repair WMI, or start an MSI service. Never disable antivirus, vulnerable-driver protections, or Windows integrity controls to get telemetry.

The product launches only its own short-lived read-only sampling process; no arbitrary command/shell execution or writable helper service. The parent enforces a10-second deadline and confirms termination before allowing another read; failed termination quarantines future reads. Every worker verifies identity afresh. UI age checks mark overdue snapshots stale independently of completion.

No network functionality, advertising, analytics, accounts, auto-updater, or automatic startup registration is implemented. Diagnostics are exported only to an explicit local destination and may contain device identity and timestamps; inspect before sharing. Do not redistribute private MSI analysis files.

A future writable backend requires exact model/EC firmware matching, installed-vendor and applicable Linux evidence, bounded pre/post reads, a verified restoration operation, independent sensor checks, concurrency/conflict handling, and tests showing deny-before-transport for unsupported commands. Runtime elevation and actual write testing require separate, explicit user approval.
