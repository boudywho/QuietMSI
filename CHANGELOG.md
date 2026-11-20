# Changelog

## Unreleased

- Rebuilt the native dashboard around Overview, Cooling and Settings. Dark is
  the default for new preferences; existing System and Light choices are kept.
- Show last-reported hardware state separately from drafts. Cooling previews
  unsaved edits without sending hardware commands.
- Open the window while hardware support initializes. Coalesce telemetry reads
  and refresh after sleep or tray reopen without blocking the window.
- Keep drafts when Apply fails. Cancel unsent requests on suspend or exit and
  don't show success when an independent sample cannot confirm the change.
- Remove obsolete design exports, duplicate captures and one-off discovery scripts.

## 0.6.0 — experimental

- Installer with shortcuts, Windows uninstall registration, same-version repair,
  in-place upgrades and downgrade protection.
- Self-contained portable ZIP with runtime licenses and release checksums.
- Preserve startup opt-in when installing over a portable copy; uninstall removes
  only owned tasks pointing to the installed executable.
- Settings includes manual GitHub update checks and release downloads.
- Public repository documentation, experimental status and contribution guidance.

## 0.5.0

- Dedicated Settings page for themes and elevated Windows startup.
- Bundled .NET Desktop runtime and independently authored firmware schema.
- Verified telemetry and current-value control readback after MSI Center removal.

## 0.4.1

- Integrated borderless frame, stable polling/rendering and original app icon.
- GPU temperature fallback through the installed NVIDIA driver utility.
- Removed the Diagnostics tab; retained explicit command-line diagnostic export.
