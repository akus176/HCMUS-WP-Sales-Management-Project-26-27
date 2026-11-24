# Compact Preparation header and specified function discovery (#219)

The review asked for less space above the queue and the functions required by the specification. This follow-up checks the original [mobile specification](../../../../openspec/changes/add-staff-mobile-app/specs/staff-mobile-app/spec.md) and [usability specification](../../../../openspec/changes/polish-staff-mobile-ux/specs/staff-mobile-usability/spec.md), rather than deriving functionality from the concept art alone.

## Header correction

Preparation and Staff home share a row at normal text. The redundant subtitle is removed; filters fit their labels. Enlarged text stacks navigation and filters. Fresh access verification still gates the working heading and content; return navigation remains available when access is checking, denied or unavailable.

| Native measurement | Before | After |
| --- | --- | --- |
| First ticket top, full screenshot | 1016px | 666px |
| Filter group width | 342dp | 247.33dp |
| Each filter target height, normal text | 48dp | 48dp |
| Each filter target height, 200% text | Not measured in this baseline | 56.33dp |

The queue recovers **116.67dp vertically** and filters use **27.7% less width**. Normal captures have the same four live tickets and no selection. All active, Confirmed and Preparing were each selected on the emulator. The reserved action region and existing queue/detail workflows remain intact.

- [Before](before.png), [after](after.png), [200% text](after-200-text.png).
- [Native bounds and calculations](geometry-verification.json), [source/device provenance](manifest.json).

Captures are unedited, 1170×2676px at 480dpi: 390×844dp app content plus 24dp system bars at each edge. The Tools control belongs to Expo Go. The [attempt capture](attempts/profile-returned-home.png) records Expo Go returning to Home during a text-scale change; the successful enlarged queue capture enters Preparation after configuring that profile.

## Specified functions and entry paths

No specified mobile workflow was removed in the route/source comparison. Home descriptions now name the permitted functions instead of generic workspace descriptions. The original Manager demo account lacks `delivery.update` and `users.manage`, so it hides Deliveries and Administration. A separate local Admin preview account exposes all six workspaces using existing backend permissions; no existing account or role policy was changed. Credentials stay outside the repository.

The native audit found table occupancy buried below the complete Counter order list. Counter now has explicit Orders and Tables views, making tables reachable immediately. Opening an occupied table and returning restores Tables; switching back to Orders preserves the search and status filter. See the [native Tables capture](counter-tables.png).

The [initial diagnostic](attempts/navigation-initial.txt) is retained separately from the corrected navigation pass. A [second diagnostic](attempts/navigation-loading-state.txt) inspected selection before route access checking finished; the final pass waits for the expected view and selected state. Neither diagnostic is a passing run.

| Required function | Entry path | Implementation / regression evidence |
| --- | --- | --- |
| Secure sign-in, renewal, sign-out | Sign-in; Staff home → Sign out | `session`, `secure-storage`, `login`, `navigation`, `session-renewal-form` suites |
| Effective permission navigation and revoked-link protection | Staff home; guarded module/notification routes | `permissions`, `shell`, `module-navigation`, `notification-route` suites |
| Active tickets; details; start and ready confirmation | Preparation → select ticket → Open | [PreparationScreen](../../../../apps/staff-mobile/src/barista/PreparationScreen.tsx); `barista` suite |
| Pending discount approval/rejection | Approvals → Review order discount | [DiscountApprovals](../../../../apps/staff-mobile/src/features/approvals/DiscountApprovals.tsx); `discount-approvals` suite |
| Supported cancellations; ready-delivery assignment | Approvals → Manage cancellations and deliveries → open order; Ready unassigned deliveries filter | [ManagerOrderActions](../../../../apps/staff-mobile/src/features/orders/ManagerOrderActions.tsx); `order-operations` suite |
| Order search, fulfillment, occupied tables, payments | Counter → Orders / Tables → order or occupied table → payment review | [OrdersScreen](../../../../apps/staff-mobile/src/features/orders/OrdersScreen.tsx), [OrderPayment](../../../../apps/staff-mobile/src/features/orders/OrderPayment.tsx); `order-operations` suite |
| Ingredient balances and low stock | Inventory → Ingredient balances / Low stock | [InventoryWorkspace](../../../../apps/staff-mobile/src/features/inventory/InventoryWorkspace.tsx); `inventory` suite |
| Reasoned stock adjustment and movement history | Inventory → ingredient Adjust; Movement history / ingredient History | Same inventory implementation and suite |
| Prepared receipt review and posting | Inventory → Purchase receipts → receipt → posting review | Same inventory implementation and suite |
| Own delivery assignments, contact/address, navigation and allowed outcomes | Deliveries → Active / Completed → assigned delivery | [DeliveryScreen](../../../../apps/staff-mobile/src/features/delivery/DeliveryScreen.tsx), [DeliveryDetail](../../../../apps/staff-mobile/src/features/delivery/DeliveryDetail.tsx); `delivery`, `delivery-detail` suites |
| Account search, detail, eligible deactivation, security events | Administration → Staff accounts / Security events | [StaffAdministration](../../../../apps/staff-mobile/src/features/administration/StaffAdministration.tsx); `staff-administration` suite |
| Operational updates and manual refresh | Shared connection details and workspace refresh actions | `operational-events`, `operational-updates`, `connectivity` suites |
| Permission-filtered notification links and token lifecycle | OS notification → guarded record route | `device-notifications`, `notification-route` suites; actual push delivery is not reverified by this Expo Go pass |
| Offline write prevention, protected-data cleanup, accessible responsive controls | Shared shell and workflow guards | `connectivity`, `navigation`, `delivery-detail`, `accessibility`, plus workflow/session suites |

The parent [delivery boundaries](../../../../openspec/changes/add-staff-mobile-app/proposal.md#delivery-boundaries) exclude a mobile replacement for the complete desktop administration/reporting experience. Full Menu/customer editing and general revenue-report pages are outside the current mobile spec. Low-stock reporting is specified and remains under Inventory. The submission function list's release statuses also require exact video evidence; this source/navigation audit does not promote those statuses to release acceptance.

## Validation and limits

- `npm run check --prefix apps/staff-mobile`: **344 tests / 28 suites**, TypeScript, ESLint and formatting pass; **7 forwarding-harness checks** pass. Regressions cover permitted home summaries, direct Tables access and search/filter retention through table-detail navigation. Existing route regressions verify denial/revocation hides the working heading while retaining return navigation.
- `openspec validate polish-staff-mobile-ux --strict` and `git diff --check`: pass.
- The existing Maestro cashier flow now checks Tables entry, absence of order search in that view, and return to Orders before exercising the existing payment scenario. YAML parsing and formatting pass; a fresh standalone Maestro execution remains part of the open native gate.
- [Native navigation checks](spec-navigation-verification.json): actual authenticated page and section entry using the separate preview login. These checks exercise discovery, not business writes.
- [Authenticated API reads](api-read-verification.json): all 12 relevant read endpoints return 200 for the preview session, without recording tokens or record contents.
- Local JavaScript toolchain: Node 22.14.0 with TypeScript stripping; pinned Node 26.5.0 CI remains distinct. Native checks use ADB and Expo Go 58.0.5 on Android API 36.

The complete standalone-build Maestro run, full layout/keyboard matrix, TalkBack and named reviewer approval remain open under issue #219 tasks 2.2 and 3.1. Earlier full-suite results remain historical. No order, stock, delivery or account-deactivation mutation was used for this navigation pass.
