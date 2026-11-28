## Why

Counter and Preparation staff need a focused mobile client for taking and reviewing
sales and preparing tickets against the same authoritative backend as WinUI.

## What Changes

- Keep Android-first React Native/Expo with TypeScript, secure staff sessions and permission-derived navigation.
- Use the approved native mobile presentation: permission-derived persistent Today, Counter, Preparation and More destinations; retained workspace state; Account under More; Inter typography, original vector icons, warm paper surfaces, focused records and confirmable sheets.
- Limit operational workspaces to Counter and Preparation. Today is the entry surface; More contains Account only.
- Include Counter catalog/customization/cart, service modes, dine-in available-table selection, existing-customer/address lookup, draft save/review/confirm, discount requests, loyalty redemption, order lookup/status and payments.
- Keep permission-gated manager cancellation and ready-delivery assignment inside Counter.
- Keep Preparation queue/detail, start/ready confirmation, live/manual refresh and online-only writes.
- Retain device lifecycle and privacy-safe urgent Preparation notifications with authenticated, freshly authorized deep links.
- Provide ordinary device sign-out and a distinct account-wide **Sign out everywhere** action under Account. Account-wide revocation is server-confirmed and clears the initiating device's protected state; ordinary sign-out remains session-scoped and can clear local state offline.
- Continue using the shared API, business rules and PostgreSQL state; generate client types from shared OpenAPI.

## Delivery Boundaries

The owner's 26 November 2026 Counter + Preparation decision supersedes the previous
six-workspace target. [Scope decision and acceptance map](../../../docs/mobile-scope-206.md)
records included workflows, explicit exclusions, owners and remaining gates.

- **P0:** sessions, permission-derived shell, Preparation, online-only protection and shared-state verification for retained workflows. Historical #201/#198/#202/#203 evidence stays tied to its recorded revisions.
- **P1:** Counter sales/order/payment and manager actions, device lifecycle and urgent Preparation notification/deep links (Mobile 5.1–5.4, selected by #206).
- **P2:** current Android acceptance, accessibility, CI, physical-device/release procedures and final demonstration evidence. CI success alone does not complete native acceptance.

Mobile approval inboxes, Stock/inventory/receipts, courier workspaces, staff
administration, a standalone Tables occupancy view, and full customer/address/settings
administration are excluded. WinUI/shared backend workflows remain available. Delivery
service mode and manager assignment remain Counter actions. Discount requests remain
in Counter; approval decisions are made in WinUI.

#95 tracks current tasks, #206 remains open for selected P1 verification and @longtoZ's
approval, and #232 owns operation-to-scope traceability. This scope decision is not
reviewer acceptance or final release/demo completion.

## Current implementation reconciliation (28 November 2026)

The implemented application now follows the selected native redesign: permission-derived
persistent Today, Counter, Preparation and More destinations; retained mounted workspace
state with fresh access checks; Account under More; Inter typography; original vector
icons; warm paper surfaces; focused record and detail views; and confirmable sheets. The
parent task ledger records the implemented shell and presentation while selected-device
acceptance remains under #206.

Session actions now distinguish ordinary device sign-out from account-wide sign-out.
Ordinary sign-out remains session-scoped and can clear local state offline. Account-wide
sign-out is a separate Account action implemented in #234/PR #245: it requires a confirmed
`204`, clears this device's protected state, preserves the session on failure/timeout, and
does not claim immediate access-token invalidation. Its implementation is complete; its
selected-device acceptance remains part of #206.

## Capabilities

### New Capabilities

- `staff-mobile-app`: Secure, permission-aware Counter and Preparation against the shared backend, with safe connectivity, notifications and accessible mobile presentation.

### Modified Capabilities

None. No domain rules, API permissions or Windows requirements change.

## Impact

Uses `apps/staff-mobile`, its generated API boundary, operational events and device
registration. Shared API documentation distinguishes selected mobile operations from
WinUI-only operations without removing endpoints. Retired mobile code/tests and prior
evidence may remain for traceability; their existence does not expose a supported
workspace. No direct database access, second backend or offline write queue is added.
