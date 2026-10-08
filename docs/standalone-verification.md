# Standalone support and Settings — 0.5.0

QuietMSI now includes the .NET Desktop runtime and independently authored firmware support. It uses Windows' signed `wmiacpi.sys` and WMIProv, without MSI Center services or MSI's schema DLL. Its owned `QuietMSI_ACPI` and `QuietMSI_Packet32` classes describe only the nine protocol methods needed by the existing guarded backend. Class names, parameter declarations and descriptions are independently authored; the GUIDs/method IDs are firmware protocol facts read from live metadata.

First launch installs the owned MOF and a content-addressed resource DLL under `%ProgramFiles%\QuietMSI\Support`, checks MOF syntax, registers the owned classes, verifies exact model/EC/WMI identity, records the former MofImagePath, and adopts its own registration. If MSI's uninstaller subsequently removes the shared value or the owned classes, the next normal launch repeats setup. No proprietary binaries are redistributed and no added service or kernel driver is installed.

Windows documents this registration in [Setting MofImagePath](https://learn.microsoft.com/en-us/windows-hardware/drivers/kernel/setting-the-mofimagepath-registry-value) and [Publishing a WMI schema](https://learn.microsoft.com/en-us/windows-hardware/drivers/kernel/publishing-a-wmi-schema). The resource format uses `MofResource MOFDATA` as described in [Compiling a driver's MOF](https://learn.microsoft.com/en-us/windows-hardware/drivers/kernel/compiling-a-driver-s-mof-file). The build uses Windows' built-in MOF compiler; no WDK validation tool is installed on this development machine.

## Live independence evidence

On MSI GF63 Thin 11UCX, BIOS E16R6IMS.112, EC16R6EMS1.106, WMI2.8:

- The registered schema DLL is QuietMSI-owned under Program Files, not `%windir%\SysWOW64\msiapcfg.dll`.
- `MSI_Center_Service`, `MSI Foundation Service`, and `MSI Sendevsvc` were all stopped during three successive reads through the packaged self-contained executable.
- All reads returned EC identity, CPU/GPU temperatures, fan RPM, battery/AC and complete performance, fan mode, boost and CPU-curve state.
- The services were restored to their original running state in a `finally` block. No hardware settings were changed.
- Raw local evidence is `artifacts/standalone/independence-verification.json` and the three `independent-read-*.json` files. These contain machine identity and are not included in the public package.

## Actual MSI removal

The user explicitly approved removal. MSI Center's Store app was removed for installed users. The signed Center SDK uninstaller completed successfully and also invoked NBFoundation's uninstaller; its own log records successful NBFoundation removal. All MSI Center/SDK/NBFoundation installation records, app registrations and three services were absent afterward. MSI Afterburner was preserved as an unrelated installed application. The shared SysWOW64/msiapcfg.dll remained on disk, but QuietMSI's driver registration points exclusively to its owned resource DLL; the application does not load or reference that shared file.

Windows denied a separate provisioned-image inventory check even from the elevated helper. Current/all-user installed app inventory passed; removal of a provisioning entry for hypothetical future user profiles was not independently verified. No permissions or security settings were changed to bypass that denial.

After removal, QuietMSI's support check and three protected reads passed: exact EC identity, CPU/GPU temperature, first fan RPM, battery/AC and full control state. An explicitly invoked elevated acceptance test then exercised the exact production Set_Data and Set_Fan transport with the **same current** performance byte and CPU table. It held machine write ownership, verified identity/fresh state, journaled writes and checked complete readback. Profile, mode, boost, six curve percentages and temperature points remained unchanged. Normal hardware integration tests still make no live writes; this mode runs only with `--standalone-control-probe <local-result-file>`.

Local evidence is `artifacts/standalone/post-removal-verification.json`, `post-uninstall-read-*.json` and `post-uninstall-controls.json`. It contains machine identity and is excluded from the package. The actual app remained running with live telemetry after removal. The signed Windows ACPI driver and QuietMSI's resource registration survived the uninstall.

**A Windows reboot has not yet been performed.** The user will initiate it. The remaining acceptance check is to start QuietMSI after that reboot and verify both protected telemetry and controls. Do not describe cold-boot survival as passed.

## Settings and startup verification

Appearance moved out of the header into Settings. System/Light/Dark preferences retain the existing per-user persistence. Startup reads Windows' actual task state before enabling its checkbox, serializes changes, shows failures and preserves the previous value on failure. It uses a current-user logon trigger, InteractiveToken/HighestAvailable, a ten-second startup delay, absolute path, `--startup`, battery-friendly settings, no stored credentials and no default 72-hour execution limit. [Microsoft's task security documentation](https://learn.microsoft.com/en-us/windows/win32/taskschd/security-contexts-for-running-tasks) describes this context.

UI tests passed held startup loading, verified enable, failed removal, theme location, compact/wide layout, XML escaping/user scope, hidden tray startup, second-launch reopen and absence of hardware effects. The actual Windows integration test passed registration/readback/removal of a GUID-suffixed verification task; no verification tasks remained and the user's startup setting was left off. That test caught and fixed the translated FileNotFoundException for an absent folder and Windows' replacement of RegistrationInfo.URI. The ownership marker now uses RegistrationInfo.Source. Core tests passed 17/17; hardware and full UI suites passed. The native 1,000-sample stress test passed with stable GDI/USER resources. The packaged executable passed the finite UI smoke test.

Windows' native LoadLibraryEx/FindResource check passed for the **installed** support DLL. MOFDATA/MofResource matched the independently compiled binary exactly (1,160 bytes, SHA256 `BD94351BA5DA06DCAC759E165F98560B61C0EB165EFC738F1B3A18032B6AEA02`). This check caught and fixed resource-name normalization before the final package was installed. The app also validates native resource presence before adopting the driver registration. This verifies resource loading as data, without executing DLL code; it still does not replace a post-uninstall/reboot test.

The package includes the .NET runtime and Microsoft package notices. Windows WMI/ACPI and the installed graphics driver are normal platform prerequisites. Startup and independent firmware setup require administrator access, already requested by the app's manifest.
