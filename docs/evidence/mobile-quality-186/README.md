# Mobile quality verification (#186)

This evidence covers Mobile 6.1, 6.2, 6.3 and 6.5 under canonical issue
[#95](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/95).
The final Android suite counts and final-commit CI run are recorded in
[PR #196](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/196).

## Reproduction and automated checks

Follow the [contributor runbook](../../mobile-quality-and-release.md), including
the disposable PostgreSQL database, synthetic accounts, running API/Metro and
one API 36 Android emulator. Run `npm run check` in `apps/staff-mobile`:
26 suites / 219 tests pass, including 20 P0 accessibility component cases.
The PostgreSQL-backed API suite passes 53/53 with no skipped cases.

`npm run test:e2e:suite` runs ten separate Android workflows, requires fresh
one-case JUnit reports, and verifies committed business state through independent
API sessions. It fails on missing/skipped reports or server-state mismatches.
The [final complete run](android-suite-summary.json) passed **10/10 workflows**, with zero failed, missing or
skipped cases and passing independent API state verification. It used fresh
records and a still-active synthetic deactivation target. The earlier 9/10
attempt and focused recovery are not combined into this passing result.

Native verification found and corrected a keyboard-autocorrect bug in staff
search and a keyboard overlap at 200% text. The root keyboard-avoiding container
keeps the scrollable form viewport above the keyboard, including below the
connectivity banner. It also exposed workflow selectors that needed the full rendered
movement label, scrolling to delivery actions after success feedback, and
bounded waits for radio connectivity changes. Assertions and server outcome
checks remain required.

## Native accessibility audit

Audit date: 2026-11-10. Android API 36, x86_64 development build, Maestro 2.12.1.
Phone: 1080×2400 pixels at 480 dpi (360×800 dp). Tablet: 1600×2560 pixels at
320 dpi (800×1280 dp). Both use system `font_scale=2.0`.
These are native emulator layouts, not browser renderings or component snapshots.

The four role flows in [audit-flows](audit-flows) cover the ten P0 screens:
sign-in, home, preparation queue/detail, approval inbox/detail, inventory
balances/adjustment, and delivery list/detail. Each visits required content and
scrolls to the primary action. Inventory also checks the quantity field with
the keyboard open; delivery checks the failure-reason form. Text inside an
editable single-line field scrolls horizontally; long delivery reasons wrap.
Viewport-edge cropping in a scrolled screenshot is not truncated content.

| Screen | Phone at 200% | Tablet at 200% |
| --- | --- | --- |
| Sign-in | [Inputs and submit](phone-login-controls.png) | [Inputs and submit](tablet-login-controls.png) |
| Home | [Role navigation](phone-home.png) | [Role navigation](tablet-home.png) |
| Preparation queue | [Selected filter](phone-preparation-queue.png) | [Selected filter](tablet-preparation-queue.png) |
| Preparation detail | [Content](phone-preparation-detail.png), [action](phone-preparation-action.png) | [Content and action](tablet-preparation-detail.png) |
| Approval inbox | [Pending record](phone-approval-inbox.png) | [Pending record](tablet-approval-inbox.png) |
| Approval detail | [Content](phone-approval-detail.png), [actions](phone-approval-actions.png) | [Content and actions](tablet-approval-detail.png) |
| Inventory balances | [Low stock and adjustment entry](phone-inventory-balances.png) | [Low stock and adjustment entry](tablet-inventory-balances.png) |
| Stock adjustment | [Fields and Save](phone-inventory-adjustment.png), [keyboard](phone-adjustment-keyboard.png) | [Fields and Save](tablet-inventory-adjustment.png), [keyboard](tablet-adjustment-keyboard.png) |
| Delivery list | [Selection and navigation](phone-delivery-list.png) | [Selection and navigation](tablet-delivery-list.png) |
| Delivery detail | [Contact/address](phone-delivery-detail.png), [failure form](phone-delivery-failure.png) | [Contact/address](tablet-delivery-detail.png), [failure form](tablet-delivery-failure.png) |

For each profile, set the size/density/font commands from the runbook, then run
the role YAML using `maestro test --format junit --output <report.xml> <flow.yaml>`.
Set these process environment variables first:

- `MAESTRO_STAFF_USERNAME` and `MAESTRO_STAFF_PASSWORD`: synthetic account for the role.
- `MAESTRO_AUDIT_ORDER_ID`: a confirmed ticket from `barista-fixture.ps1 -Action Seed`.
- `MAESTRO_AUDIT_APPROVAL_ORDER`: a pending discount's order number from `quality-fixture.ps1 -Action Seed`.
- `MAESTRO_AUDIT_INGREDIENT`: that fixture's ingredient name.
- `MAESTRO_AUDIT_DELIVERY_ID`: an assignment from `courier-fixture.ps1 -Action Seed`, started by its assigned courier.

Use Maestro's `--test-output-dir` to retain screenshots; do not publish its raw
debug logs, which can include typed synthetic credentials. The checked-in JUnit
files contain only case outcomes. Screenshot filenames identify profile and
screen; the recorded audit does not mutate stock or finish deliveries.

## Screen-reader evidence

TalkBack 16.0.0.738667889 was enabled on Android with its developer **Display
speech output** setting. The `talkback-*-sequence.png` files concatenate actual
successive speech-caption regions, rather than synthesizing spoken text.
Keyboard traversal checked button names/roles, edit-box labels, filter selection
and feedback. Keyboard activation opened preparation confirmation, changed a
queue filter, approved a synthetic discount, and triggered the required delivery
failure-reason alert. Component tests check the corresponding semantics on all
ten screens for both layouts.

Representative retained captions show Username/Edit box, Open Preparation/Button,
selected All active/Button, Approve discount/Button, Search ingredients/Edit box,
Mark delivered/Button, Failure reason/Edit box, and the required-reason alert.
This is evidence of TalkBack output and keyboard operation; it does not claim
human listening or a touch-gesture audit. UI-tree dumping was kept separate:
`uiautomator dump` can temporarily disconnect TalkBack and reset its focus.

The retained [preparation dialog caption](talkback-start-preparing-sequence.png)
is **Keep current state**, the cancel action reached after opening the dialog;
the filename describes the originating workflow. The
[approval feedback](talkback-approved-feedback.png) shows the server-confirmed
result. [Delivery validation](talkback-failure-validation-sequence.png) shows
the required-reason alert and its Alert role.

## Scope and review

The user successfully installed/opened the native APK after correcting the
packaged SDK's physical path. The contributor runbook includes that discovery,
clean setup, LAN/device configuration, notifications, tests, release and rollback.
Real push delivery still needs Expo project/native credentials; deterministic
provider tests and the real-device procedure are separate evidence. A debug APK
is not a production release. The selective mobile feature gate/rollback drill
from the [OpenSpec design's migration plan](../../../openspec/changes/add-staff-mobile-app/design.md#migration-plan) remains outside #186; the current
shared backend has no such gate.
The final demo recording is also outside this ticket. Named reviewer vt-Toan's
approval remains a merge requirement.
