# Contributing to QuietMSI

QuietMSI is very experimental and currently has one verified hardware policy.
Please open an issue before expanding hardware support or changing firmware writes.

## Development

Use Windows x64, .NET 8 SDK and Inno Setup 6.6+. Run the test commands in README.md
before a pull request. Keep hardware-independent tests deterministic, and use injected
fixtures for UI behavior. Normal tests must remain read-only on real hardware.
Do not run the opt-in live write modes without informed authorization.

## Hardware changes

A related chassis or matching BIOS prefix is insufficient. Changes need exact live
model/EC/WMI identity evidence, independently documented protocol facts, conservative
bounds, preserved unknown bytes, a fresh pre-state, conflict checks and post-readback.
Do not add guessed defaults, raw EC access, signature bypasses or broad allowlists.
Never include proprietary binaries, copied decompiled implementations, credentials,
serial numbers or private machine dumps in a contribution.

## Pull requests

Describe the user-visible problem, resulting behavior, validation and remaining
limitations. Keep changes focused. Include a screenshot for UI changes and tests for
meaningful transaction or policy behavior. Preserve explicit Apply actions and the
rule that startup/polling/resume/exit never change hardware settings automatically.

## Reporting bugs

Use the bug template. Include app/Windows versions and your model/EC version,
steps, expected behavior and actual behavior. Redact personal information from
screenshots and logs. For security-sensitive issues, follow SECURITY.md.
