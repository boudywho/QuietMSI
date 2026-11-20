# UI capture records

This directory retains the 0.3 verification records, not the reviewed image set.

- `finish-verification.json`: recorded 41 UI PASS cases, 17 core cases, Release build and smoke results.
- `parent-final-verification.json`: parent-run acceptance output and portable publish evidence.
- `finish-review-notes.md`: bounded dashboard correction evidence and RED → GREEN notes.

The reviewed 0.3 images and their matching SHA-256 provenance live in
`.impeccable/review/`: eight `synthetic-{light,dark}-{overview,cooling,diagnostics,compact}.png`
fixtures and six `native-dark-{Hardware_Profile,Hardware_FanMode,ThemeMode}-{closed,open}.png`
actual-screen crops. Those files and `synthetic-provenance.json` /
`native-dropdown-provenance.json` are the coherent pair to cite; do not copy
unprovenanced recaptures back into this directory.

For current UI evidence, regenerate into an untracked build directory rather than
committing a second image set:

```text
dotnet run --project tests/QuietMSI.UITests -c Release -- --synthetic-screenshots
dotnet run --project tests/QuietMSI.UITests -c Release -- --smoke --screenshot artifacts/ui-overhaul/readonly.png
```

Tests compile the exact UI/UIHost sources without Program or hardware transport.
Synthetic Apply only mutates fixture state and records requests. No hardware calls,
elevation, or real hardware writes occur.
