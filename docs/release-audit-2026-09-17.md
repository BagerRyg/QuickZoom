# QuickZoom release audit — 17 September 2026

Scope: current source and the complete working-tree candidate against commit `250729d` on `codex/backup-build-333-20260908`. GitHub returned no open pull requests for `BagerRyg/QuickZoom`, so this is a local candidate review, not approval of a hosted PR. Existing unrelated changes and deleted image assets were preserved.

Review covered runtime and native resources, keyboard/mouse hooks, settings persistence, normal and Strict Data mode, setup and elevated handoff, installation ordering, UI ownership, subprocesses, and release packaging. Independent reviewers cross-checked startup changes. No screenshots were taken. Tests use isolated files, hidden controls and simulated failure callbacks; the live installation and scheduled tasks were not changed.

## Confirmed findings addressed

| Priority | Finding | Fix and regression evidence |
| --- | --- | --- |
| P1 | Shutdown could discard a settings snapshot already taken by a worker that exceeded the one-second wait. | Keep snapshots pending until committed; shutdown finishes the latest revision. Tests hold the write lock beyond the deadline in both data modes. |
| P1 | Failed native cursor restoration discarded recovery state and could leave a hidden cursor. | Retain flags, retry while running, and make three bounded attempts during shutdown. Simulated transient and permanent failures cover both modes. |
| P1 | Failed magnifier color/transform/source/filter updates could leave a black topmost host. | Keep hosts hidden until the first successful frame; failure destroys owned hosts, resets zoom and queues a notification. Sixteen injected failure paths cover monitor/lens hosts and both modes. |
| P1 | Elevated startup treated process existence as readiness; instance arbitration could exit both processes or start a duplicate after timeout. | Per-process yielding/readiness markers bind SID, PID and process start time. Fallback requires reacquiring the mutex; otherwise the launcher exits. Tests cover marker lifetime and timeout decisions; complete UAC handoff still requires clean-machine validation. |
| P1 | Failed installation retries could advance the current pointer and prune the working payload before task verification; concurrent installers could prune each other's stages. | Serialize installation transactions, verify staged task targets before committing the pointer, and retain the previous working payload. Isolated lock/retention tests pass; no live task was registered. |
| P2 | AltGr release ordering, simultaneous left/right modifiers and partial SendInput success could leave incorrect shortcut state. | Track physical modifiers and successful replay prefixes. Simulated input tests cover both modes, suppression settings, release orders and zero/one/two inserted events without injecting real input. |
| P2 | Synchronous magnifier error dialogs could block low-level input callbacks. | Queue notifications after the hook returns and discard queued work after shutdown. |
| P2 | Cached locked-screen geometry survived resolution/position changes. | Refresh the matching Screen instance or release a removed monitor; regression uses stale geometry without changing displays. |
| P2 | Theme/accessibility refresh reopened hidden Settings; narrow sliders threw; dropdown disposal could be lost during owner destruction. | Preserve visibility, bound knob geometry, retain deferred disposal ownership. Tests cover hidden forms, two dropdown disposal orders and 720 slider cases without rendering. |
| P2 | Another Windows user's startup task appeared ready for the current user; helper waits were unbounded. | Check task ownership and bound helper waits to three minutes. Retry reuses a still-running helper instead of starting another installer. |
| P2 | Brief file sharing violations aborted atomic replacement immediately. | Retry only sharing/lock violations with a 375 ms maximum delay; permanent failures preserve the previous file and remove temporary files. |
| P2 | Release packaging had no required regression gate; failed test assertions could produce Windows crash dialogs. | Run bounded screenshot-free checks before packaging. All three harnesses report exceptions to stderr and exit with code 1. |
| P2 | The installed SDK bundled .NET 10.0.7, behind the serviced runtime. | Raise the SDK baseline to 10.0.401 and validate/package with the isolated updated SDK. Microsoft lists .NET 10.0.12 as including security and non-security fixes; this audit does not establish exploitability of individual runtime CVEs in QuickZoom. |

Runtime source: [Microsoft .NET 10.0.12 release notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.12/10.0.12.md). The SDK archive is downloaded from Microsoft's official release metadata and checked against its SHA-512 before use.

## Validation and release status

Final command: `pwsh -NoProfile -NonInteractive -File scripts/Test-Release.ps1 -NoRestore -IncludeNative`, with the isolated SDK directory first on process PATH and DOTNET_ROOT/DOTNET_ROOT_X64 set to that directory. SDK location: `.codex-temp/dotnet`; no global SDK installation is required to reproduce this run. A normal developer machine can use the SDK required by `global.json` from PATH.

- All four Release projects built with zero warnings and zero errors.
- Runtime and privacy harnesses reported .NET 10.0.12. Both normal and Strict Data mode checks passed.
- Settings persistence: malformed/read-only/locked files, revision ordering, slow in-flight shutdown saves, transient and permanent atomic-replacement failures, and recovery without overwriting unreadable preferences.
- UI lifetime/layout: 140 settings page/language/theme/data-mode combinations, 720 narrow-slider geometry cases, hidden Settings refresh and deferred dropdown disposal. No rendering or screenshots.
- Native acceptance/lifecycle: 20 hidden magnifier lifecycles, 80 transforms, color effects, source/filter calls and USER-handle cleanup; ten application-engine/input-hook lifecycles with real input passed through unchanged. Simulated native failure tests never pass fake handles to native APIs.
- Privacy: local standard-user writes and identity restoration; logging off by default; Strict Data prohibits diagnostics; bounded/redacted logging, concurrent writes and storage rejection.
- Startup/process checks: readiness/yielding marker lifetime including Pending retention, mutex contention and retention selection, current-user ownership, bounded helper/output waits, callback containment and setup completion timing. Task XML writer, escaping, Enabled defaults and unlimited task runtime checks passed without registration.
- All five locale files passed validation (481 keys each).
- RuntimeChecks and PrivacyChecks were also deliberately run without arguments and both returned exit code 1 with console diagnostics, without Windows crash dialogs.
- `git diff --check` passed. The independent final startup review found no remaining blocker in the addressed handoff/transaction paths.

Final output: `test-validation/release-audit-345-final.txt`; package output: `test-validation/release-audit-345-publish.txt`. The pre-update runtime suite also passed on 10.0.7; release validation and packaging use 10.0.12.

Packaged **Version 3.0, Build 345** as `Builds/Build 345/QuickZoom.exe`: self-contained Windows x64, single file, 173,479,955 bytes. The published runtime configuration includes Microsoft.NETCore.App and Microsoft.WindowsDesktop.App **10.0.12**. SHA-256: `D9DC4797D04173446FF20B652BEB54881FCD469B6E396B983F1ABDCE8D3124B9`. `SHA256SUMS.txt` and `release-validation.json` are beside the executable. Signature status is **NotSigned**; no signing certificate was configured. The packaged executable was not launched as the live application.

Disposition: automated checks pass and the local release candidate is built. Public-release sign-off remains conditional on the checks below.

## Remaining public-release checks

- Perform a clean-machine standard-user and UAC install/update/sign-in cycle, including failed installation and retry. Tests here do not exercise actual Task Scheduler replacement or elevated-to-standard token writes.
- Exercise physical monitor hotplug, mixed DPI, suspend/resume, secure desktop/RDP, and a sustained zoom/cursor soak. Hidden native acceptance and lifecycle checks cannot prove compositor output on every GPU.
- Autostart retains one machine-wide task, so only one Windows user can own it at a time. Other users now see an accurate unconfigured/broken status. A task registered successfully before pointer-commit failure is retained with both payloads; setup reports incomplete and requires retry.
- The Build 345 source commit includes the new production helpers and regression tests that were untracked during the audit. No merge or public release was performed as part of the audit itself.
- Use a trusted signing certificate for public distribution. A local unsigned candidate does not establish publisher trust.

These checks support a tested release candidate, not a claim that every Windows configuration is bulletproof. No blanket public-release approval is implied while the checks above remain outstanding.
