# UI parity review record — 2026-11-01

## Review owner

- Prepared by: `@akus176`
- Required visual reviewer: `@longtoZ`
- Issue: [#119](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/119)

## Evidence set

The review uses the tracked concept artboards, the current native captures, and the native parity report:

- [Concept artboards](../ui-concepts/)
- [Current native captures](README.md)
- [Capture manifest](current-capture-manifest-2026-11-01.json)
- [Native parity report](current-parity-verification-2026-11-01.json)
- [Approved native revisions](../ui-concepts/workspaces-117-native-revision.md)
- [Targeted parity revisions](../ui-concepts/ui-parity-119-targeted-revision.md)

## Review provenance

- Review branch: `codex/midterm-readiness-fixes-2026-11-01`
- Review commit: `2019050df26891393b7c84b922f2aa0e2d9bdbc2`
- Current review worktree: clean
- Capture timestamp: `2026-10-31T17:22:03.4495867Z`
- Capture implementation commit: `f64939b6cebe80ef4533357e2eade46736455ce1`
- Capture concept commit recorded in the manifest: `f64939b6cebe80ef4533357e2eade46736455ce1`
- Latest concept-document commit in the review branch: `3bcce26c086de946ca969d5dadff50c8dfc07bb4`
- Capture manifest working-tree flag: `dirtyWorktree: true`
- Seed: `scripts/postgres/midterm-seed.sql`
- Seed SHA-256: `2C9D2B7FD4F25FE14151BA3A40F888469E9A6491A0284505E4861C7985F1A166`
- Supplement seed: `scripts/postgres/ui-parity-seed.sql`
- Supplement SHA-256: `771D2898874B00252C8840E21AE8BBACE97A9D31ADB0BF9C6B38B73C257032D5`
- Executable SHA-256: `BA00079ECBCEDBCD09DE3E86C858208BFCF051763ADC2983F9909276A268A8AA`

The committed capture is therefore not a clean-current capture. Docker is unavailable in the review environment, PostgreSQL is running but requires a password, and no database connection string is configured in the session. A new capture could not be produced without introducing or exposing credentials.

## Comparison matrix

| Surface | Concept reference | Native evidence | Automated result | Review status |
| --- | --- | --- | --- | --- |
| Counter | [counter.png](../ui-concepts/counter.png) | [native-counter-1440.png](native-counter-1440.png), [native-counter-600.png](native-counter-600.png) | Automated checks pass; wide and narrow captures inspected | PASS with approved data/payment exceptions; formal sign-off pending |
| Service Board | [service-board.png](../ui-concepts/service-board.png) | [native-service-board-1440.png](native-service-board-1440.png), [native-service-board-600.png](native-service-board-600.png) | Automated checks pass; wide and narrow captures inspected | FAIL at narrow width; header controls overlap the title |
| Menu | [menu.png](../ui-concepts/menu.png) | [native-menu-1440.png](native-menu-1440.png), [native-menu-600.png](native-menu-600.png) | Automated checks pass; wide and narrow captures inspected | PASS with approved native composition; formal sign-off pending |
| Stock Room | [stock-room.png](../ui-concepts/stock-room.png) | [native-stock-room-1440.png](native-stock-room-1440.png), [native-stock-room-600.png](native-stock-room-600.png) | Automated checks pass; wide and narrow captures inspected | PASS with approved native composition; formal sign-off pending |
| Customers | [customers.png](../ui-concepts/customers.png) | [native-customers-1440.png](native-customers-1440.png), [native-customers-600.png](native-customers-600.png) | Automated checks pass; wide and narrow captures inspected | PASS with approved native composition; formal sign-off pending |
| Reports | [reports.png](../ui-concepts/reports.png) | [native-reports-1440.png](native-reports-1440.png), [native-reports-600.png](native-reports-600.png) | Automated checks pass; wide and narrow captures inspected | PASS with approved native composition; formal sign-off pending |
| Menu editor | [menu-picture-editor.png](../ui-concepts/menu-picture-editor.png) | [native-menu-editor.png](native-menu-editor.png) | Compact, expanded, and saved-record state preserved | PASS with approved native framing; formal sign-off pending |

## Verified results

- Six destinations are present.
- The Counter is the default destination after connection.
- The report contains 42 layout results and 12 smoke scenarios.
- Automated layout, smoke, and polish checks report zero failures.
- The editor preserves compact state, expanded state, and the saved record.
- The targeted revision explicitly permits real seeded values, the operator selector, the supported payment flow, and native editor framing.

## Unresolved visible difference

- **Service Board at 600 × 900:** the `Operator` label and selector occupy the same header area as the large `Service board` heading. The heading is visibly overlapped and the header is not readable as a composed responsive layout. Required fix: reflow the operator selector and `Open Counter` action above or below the heading at narrow widths, then recapture the 600-DIP state and rerun the parity checks.

The other six reviewed surfaces did not show an additional unapproved blocking difference in the available captures. Their data-driven values, operator control, payment placement, native editor framing, and native composition differences are covered only by the referenced targeted revisions; they are not treated as general approval for future drift.

## Open review gate

## Final review status

**NOT APPROVED. Keep Issue #119 open.**

The review found one unresolved responsive visual defect on Service Board, and the available capture is not clean-current. After the fix and a fresh capture, `@longtoZ` must complete the formal visual review and record the reviewer decision. No approval is claimed for `@longtoZ`.
