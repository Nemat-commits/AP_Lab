# Quality review and verification

## Findings and changes

- **Budget generation could freeze the interface**, and replacing a build had no confirmation. The search now runs in the background, asks before replacement, and discards a result if the build or target changed during the search. It marshals updates onto the WPF dispatcher. This still uses a bounded sample, not a globally optimal search or a performance benchmark.
- **Invalid prices left a plausible but stale total on screen.** Invalid entries now hide the total, block saving and budget generation, and display the allowed range. The last valid value is retained until corrected.
- **A partial write could damage a saved build or custom catalog.** These saves now write a temporary file in the same directory and replace the destination only after the write succeeds. This reduces, but cannot eliminate, risk from hardware or operating-system failure; back up important files.
- **Malformed or extreme component data could cause crashes or misleading calculations.** Persistence checks reject null, negative, non-finite, and extreme values; newly added custom components require essential non-zero specifications. This validation is not a certification of accuracy.
- **WPF bindings and restoration needed real window coverage.** A Windows smoke test opens the window, manipulates controls, checks the total and status, restores a build, and exercises budget generation, cancellation, and stale-result protection.

## Automated test cases

The executable test harnesses are `Tests/Program.cs` (domain and persistence) and `SmokeTests/Program.cs` (WPF). CI runs both on Windows before publishing. Representative cases:

| Area | Case | Expected |
|---|---|---|
| Rules R-01–R-04 | Mismatched sockets, RAM type, RAM limits, or excessive RAM speed | Corresponding error, or speed advisory |
| Rules R-05–R-09 | Board/GPU/cooler fit, cooler capacity, and missing NVMe/SATA ports | Correct error/advisory |
| PSU R-10 | Below estimated, exactly estimated, below recommended, exactly recommended | Error, warning, warning, compatible respectively |
| Partial build | Empty or PSU-only selection | No false pairwise error; zero estimated power |
| Catalog | At least 20 in each category, combined filters, reference build | Expected count, filtering, PKR 169,000 compatible reference |
| Pricing and persistence | Override, reload, custom part across sessions, cloned parts | Price restored and catalog isolated |
| Invalid input | Negative/excessive prices, invalid numeric fields and malformed specs | Reject input; invalid save leaves existing file unchanged |
| Budget | Multiple targets and missing category | Eight compatible parts near target, or no result |
| WPF | Selection, filter, price edit, reset, saved build restoration | Visible selections, totals, and power track state |
| WPF budget | Cancel confirmation and edit while searching | Existing build preserved |
| WPF repeat generation | Generate for two budgets, with filters active | All eight dropdowns contain and display the newly generated selections |
| Clear all | Cancel and confirm reset after a generated build and filters | Cancel preserves work; confirm resets selections, overrides, filters, target, totals and progress |

## Remaining limitations

A human visual and full end-to-end review on actual Windows hardware is still needed, including file dialogs, DPI scaling and accessibility. The catalog uses illustrative prices and approximate specifications; exact GPU dimensions, cooler socket mounting kits, CPU boost power, BIOS support, connector counts and other real-world factors must be checked against the specific products. The checker covers only its ten defined rules. An incomplete build can be green, with the banner explicitly saying it is incomplete. No automated check can establish that every combination physically works. The app is unsigned, and its GitHub Actions build artifact expires; keep a separate backup.
