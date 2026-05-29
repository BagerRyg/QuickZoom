# QuickZoom Changelog

## Version 2, Build 197

- Improved UI startup and interface load times.

## Version 2, Build 193

- Hardened startup service setup, repair, and verification so QuickZoom confirms the scheduled task points to the current managed install before reporting success.
- Improved running-instance handling so newer builds replace older tray instances while current or newer instances show the already-running dialog.
- Strengthened diagnostic logging with better startup timing, uptime, system details, and safer crash/debug log behavior.
- Improved shortcut handling and validation, including AltGr pass-through, Windows key behavior, Nordic keys, function keys, and punctuation keys.
- Polished settings and startup UI styling, translations, About diagnostics, and startup repair actions.
- Cleaned up internal formatting and safety checks around managed install paths, task cleanup, and UI controls.
