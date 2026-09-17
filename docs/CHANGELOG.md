# QuickZoom Changelog

## Version 3.0, Build 345

- Preserve slow in-flight settings saves during shutdown and retry temporary file sharing failures.
- Recover failed cursor restoration, magnifier updates, stale display geometry, and modifier/shortcut release state.
- Harden startup handoff, installation ordering, task ownership checks, and helper timeouts.
- Keep hidden Settings hidden during theme changes; fix narrow sliders and dropdown cleanup.
- Require screenshot-free regression checks before release packaging; contain test failures without Windows crash dialogs.
- Raise the SDK baseline to 10.0.401 so releases bundle the serviced .NET 10.0.12 runtime.

## Version 3.0, Build 344

- Harden runtime shutdown, initialization failures, delayed callbacks, and settings-save recovery in both data modes.
- Fix invalid shortcut values, stale input state after resume, oversized square/round lenses, and cursor restoration when disabling wiggle-to-locate.
- Add nonvisual reliability and native lifecycle regression checks; remove machine-specific .NET reference paths.

## Version 3.0, Build 343

- Simplify the Large setup switch: remove the enclosing capsule, add switch padding and separation from the header divider, and validate label/track bounds.
- Avoid full wizard layout passes on normal startup-stage changes and repaint only the progress area during animation.
- Stop the busy animation timer during completion and flush only the final frame; add repaint-scope and single-timer regression checks.

## Version 3.0, Build 342

- Restore three compact theme cards with centered icons above their labels; retain language-independent sizing.
- Continue startup fill from the rendered position across installation/verification, settle the wave at completion, and keep the progress-card/footer geometry stable across states.
- Use an amber selection for Strict Data, with localized guidance on its logging restrictions and intended users; Standard remains the recommended default.

## Version 3.0, Build 341

- Reserve setup header, text rows, cards and navigation space across all five translations so language changes do not move controls.
- Keep responsive layout decisions independent of the selected language in both setup views.
- Add exact visible-control geometry comparisons across languages to every setup screenshot scenario.

## Version 3.0, Build 340

- Added selectable Strict Data mode to setup and About, default OFF.
- Restored explicit, session-only local troubleshooting logging under About; logging and crash logging default OFF and are blocked by Strict Data.
- Added bounded diagnostic storage, raw-message exclusion, mode/persistence regression tests and five-language UI coverage.
- Fixed a setup header wrap-threshold mismatch found in responsive validation.

## Version 3.0, Build 339

- Keep the full localized setup heading instead of silently dropping it when a translation is wider.
- Fit setup content to its viewport and remove the vertical-scroll fallback and stale scroll offsets.
- Smooth verified startup completion through a brief pause, eased final fill, green transition, and hold before showing success.
- Validate all setup steps in both views, five languages, dark/light themes, multiple resolutions/text scales, and high contrast.

## Version 3.0, Build 338

- Fix elevated preference saves failing with an invalid Windows impersonation level.
- Use the same user's non-administrator desktop token for profile writes; never fall back to privileged writes.
- Add elevated regression coverage for background GUI saves, reloads, identity restoration, and write-failure recovery.

## Version 3.0, Build 337

- Load preferences without requiring profile-write permission or an elevated linked token.
- Preserve unreadable preferences instead of replacing them with defaults; show localized read/save failure messages.
- Reject stale background saves so they cannot overwrite newer preferences or a shutdown flush.
- Preserve the saved appearance when reopening setup, and refuse to overwrite unreadable settings during setup.
- Drain Task Scheduler output concurrently and bound subprocess waits to avoid pipe deadlocks.
- Remove the startup task's default runtime limit, and contain Settings-activation callback failures during shutdown.
- Expand regression checks for preference recovery, write ordering, subprocess timeouts, callback failures, and error-dialog layouts.

## Version 3.0, Build 336

- Setup offered at launch now starts at the language screen; autostart-only setup is reserved for Settings.
- Closed the task-definition file before Task Scheduler reads it, fixing a sharing violation that prevented startup installation.
- Accepted Windows' omitted enabled-by-default XML values while still rejecting explicitly disabled tasks and triggers.
- Added regression checks for the first wizard screen and the real task-definition file's handle lifetime.

## Version 3.0, Build 335

- Removed the legacy startup-install prompt, progress window, success window, and unused translations.
- Routed automatic startup setup and older setup shortcuts through the current wizard, as Settings already does.
- Reused the modern startup-message layout for setup errors; retained silent elevated installation and startup migration.
- Added standalone autostart-wizard state/navigation captures and modern error-message layout checks.

## Version 3.0, Build 334

- Refined setup, Settings, and tray spacing, typography, icons, colour contrast, and action hierarchy.
- Improved large-text layouts, setup progress labels, compact navigation, search, and localized instructions.
- Added Identify displays and an Open Settings action to the already-running dialog.
- Expanded off-screen screenshot, layout, locale, and activation regression checks. See `ui-ux-build-334.md` for coverage and manual-test limits.

## Version 3.0, Build 295

- Promoted QuickZoom to version 3.0 after the settings, tray, accessibility, keyboard-navigation, and visual-refinement work.

## Version 2, Build 197

- Improved UI startup and interface load times.

## Version 2, Build 193

- Hardened startup service setup, repair, and verification so QuickZoom confirms the scheduled task points to the current managed install before reporting success.
- Improved running-instance handling so newer builds replace older tray instances while current or newer instances show the already-running dialog.
- Strengthened diagnostic logging with better startup timing, uptime, system details, and safer crash/debug log behavior.
- Improved shortcut handling and validation, including AltGr pass-through, Windows key behavior, Nordic keys, function keys, and punctuation keys.
- Polished settings and startup UI styling, translations, About diagnostics, and startup repair actions.
- Cleaned up internal formatting and safety checks around managed install paths, task cleanup, and UI controls.
