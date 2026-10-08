# 0.6.0 packaging verification

Verified locally on Windows on 2026-10-09. This is an experimental release.

- Core: 17 passed, zero failures.
- Read-only hardware suite, full native UI suite and six release-parser cases passed.
- 1,000-sample native message-loop stress passed in 37.2 seconds with injected
  failures, page/theme/size changes and zero hardware writes. Warm GDI 68 → 66,
  USER 130 → 129.
- The native firmware schema resource validated after self-contained publishing.
- Isolated Inno installers using a separate verification identity passed initial
  0.5.0 install, Start menu/desktop shortcut targets, 0.6.0 upgrade using the previous
  directory and uninstall record, same-version repair, older-version refusal,
  installed protected telemetry, and uninstall of files/shortcuts/registration.
- A GUID-scoped real Task Scheduler test verified absent startup stays off and
  enabled startup migrates to the new path, followed by removal and cleanup.
- Uninstall removed a marked installed-path startup task, preserved marked portable
  and foreign-source fixtures, and left the user's actual startup XML and appearance
  preference unchanged. Fixture tasks were removed afterward.

The verification installers disable the application mutex to coexist with the
user's running portable copy, and invoke support setup without migrating the user's
real task. The startup migration itself is tested separately against the exact
backend with a GUID-suffixed task. Production Setup uses the application mutex
and performs both support setup and current-user startup migration.

Machine-specific logs and telemetry remain local under artifacts and are excluded
from public source and release packages. No firmware setter was invoked by these
packaging tests. No cold boot, indefinite endurance run or signed installer is claimed.
