[← Back to main README](../README.md)

# Lessons learned

Every bug recorded here actually happened in this project. Each entry follows
the same shape - what broke, how it was found, the fix, and the transferable
lesson - and the full detail is in the git history, where each fix landed as
its own commit with the reasoning in the message.

There are 38 of them, so they are grouped by the kind of failure rather than
kept in one wall of text:

| Where | What it covers | Entries |
|---|---|---|
| [Game correctness](lessons/correctness.md) | Rules, solver, generator and game state - bugs that produced a *wrong answer* rather than a wrong appearance | 6 |
| [Security](lessons/security.md) | The DevOps security review, plus the two defects the review itself introduced while fixing things | 4 |
| [Frontend and rendering](lessons/frontend.md) | CSS, layout, input handling and browser behaviour - the largest cluster, because this is where "looks fine" and "works" diverge most | 13 |
| [Hosting, tooling and deployment](lessons/operations.md) | Configuration, cross-platform scripting and the Azure rollout | 8 |
| [Testing discipline](lessons/testing.md) | What the tests got wrong about themselves, and the verification habits that resulted | 7 |

## What catches each of these now

A lesson nobody can regress against is a story, not a control. This table maps
every defect to the thing that would catch it today - and, honestly, names the
ones where that thing is a human following a procedure rather than a test.

| Defect | What detects it now |
|---|---|
| Hints broken by a wrong entry | `HintTests` - hint offered for every empty cell after a deliberate wrong entry |
| Difficulty labels not matching technique | `PuzzleGraderTests.Generated_puzzles_land_in_their_difficulty_band` |
| Medium fallback handing out an Advanced board | Same band test - Medium must never require advanced techniques |
| Undo not reverting swept notes atomically | `UndoRedoAndNotesTests.Undo_restores_swept_notes` |
| Mistake count forgiven by undo | `MistakeTests.Undo_does_not_forgive_a_mistake` |
| Auto-solve setting a best time | `GameSessionTests.An_auto_solved_win_never_sets_a_record` |
| Corrupt save breaking startup | `GameSessionTests.Initialize_falls_back_to_a_new_game_when_the_snapshot_is_corrupt` |
| Fast solver disagreeing with the naive one | `SudokuSolverTests.Fast_counter_agrees_with_the_reference_counter` |
| Total outage with green unit tests | Smoke test - boots the built DLL and fetches the real assets, in CI and again against the live URL after deploy |
| Antiforgery cookie losing `Secure` behind the proxy | `SecurityPostureTests.Antiforgery_cookie_is_secure_and_locked_down_in_production` + live smoke check |
| `SecurePolicy.Always` 500-ing every POST | `SecurityPostureTests.Antiforgery_cookie_policy_does_not_break_plain_http_development` + the smoke test's POST check that first caught it |
| Forwarded headers removed | `SecurityPostureTests.Forwarded_headers_are_processed` |
| Duplicate CSP header | `SecurityPostureTests.Exactly_one_content_security_policy_header_is_sent` + smoke check |
| Security headers dropped | Five `SecurityPostureTests` assertions + the smoke test's header block |
| `AllowedHosts` widened to `*`, or narrowed to nothing | Paired `Unknown_host_headers_are_rejected` / `The_configured_host_is_still_served` |
| Error pages leaking stack traces | `SecurityPostureTests.Not_found_responses_do_not_leak_diagnostics` |
| An inline handler the CSP blocks | `FrontendPostureTests.Markup_contains_no_inline_event_handler_attributes` |
| A stylesheet or script that 404s | `FrontendPostureTests.Every_referenced_static_asset_exists` |
| A class collision making a control click-transparent | `FrontendPostureTests` - the generic-class `pointer-events` rule, plus the pencil-marks/Notes check by name |
| Mojibake from a Windows-1252 save | `FrontendPostureTests.No_source_file_contains_mojibake_from_a_bad_encoding_save` |
| PowerShell edition differences in tooling | The CI smoke-test job runs on Ubuntu/pwsh - both edition bugs were caught there, not locally |
| An SDK pin no runner can resolve | Any CI run - it fails at `setup-dotnet` |
| **A board colour combination nobody can read** | **Runtime guard, not a test**: the colour panel measures live contrast and warns with a repair. Contrast itself is browser-verified, because compositing translucent layers is a rendering job |
| **A control shipping under WCAG AA** | **Browser verification only** - measured across every difficulty state and with the panel open, per the procedure in [testing](testing.md#browser-verification) |
| **CSS transitions, overlay alignment, flex sizing, picker anchoring, touch drags** | **Browser verification only** - the procedure is written down in [testing](testing.md#browser-verification). No automated guard; these need a real rendering engine |
| **Timer callbacks off the sync context** | **Code review only** - threading correctness here has no cheap assertion |
| **A false finding from a misread API or a bad measurement** | **Procedure only** - verify in the system that owns the setting, and sanity-check surprising numbers before reporting |

The bolded rows are the honest gaps: real defects with no automated detection.
They are the argument for keeping the browser-verification checklist rather
than trusting a green build.


See also: [testing and CI](testing.md) · [security posture](security.md) ·
[architecture review](architecture-review.md)

[← Back to main README](../README.md)
