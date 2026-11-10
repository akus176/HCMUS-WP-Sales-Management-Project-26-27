# Tasks

## 1. Delivery gates and foundations

- [ ] 1.1 Create a mobile delivery baseline that records the current API, identity, OpenAPI, emulator, and device-network readiness; verify every P0 dependency has an owner and a GitHub issue before mobile workflow work starts.
      Evidence: [S0 baseline and dependency map](../../../docs/mobile-delivery-baseline.md) ([#136](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/136)); environment and unassigned-work blockers are explicit. Completion remains subject to the issue's required review.
- [x] 1.2 Scaffold `apps/staff-mobile` as a strict TypeScript Expo application with Expo Router; verify the documented command launches it on an Android emulator. Evidence: [PR #166](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/166).
- [x] 1.3 Pin package-manager metadata and configure type-checking, linting, formatting, unit tests, component tests, and Android end-to-end tests; verify a clean install and each quality command pass. Evidence: [PR #166](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/166).
- [x] 1.4 Add validated environment configuration for emulator, physical device, test, and release API base URLs; verify missing or invalid configuration fails without exposing a secret. Evidence: [PR #169](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/169).
- [x] 1.5 Implement shared mobile theme tokens and controls with 48-dp primary actions, dynamic type, contrast, and accessible labels; verify component tests cover the required states. Evidence: [PR #172](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/172).

## 2. Shared API, identity, and contracts

- [x] 2.1 Implement the final API authentication and permission endpoints required by mobile sessions; verify valid, invalid, inactive, and multi-role users with API integration tests. Evidence: [PR #160](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/160), [PR #161](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/161).
- [x] 2.2 Expose P0 API queries and mutations for preparation tickets, pending discounts, ingredient balances, stock adjustments, and the authenticated courier's assignments; verify allowed, forbidden, empty, validation, and conflict cases. Evidence: [PR #163](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/163), [PR #164](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/164).
- [x] 2.3 Publish a deterministic OpenAPI document for the mobile-required endpoints and schemas; verify generated contract tests match the API integration tests. Evidence: [PR #161](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/161), [PR #164](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/164).
- [x] 2.4 Add idempotency and audit behavior to payment, stock, and delivery mutations where the existing domain supports them; verify retries do not create duplicate effects. Evidence: [PR #163](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/163), [PR #164](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/164).
- [x] 2.5 Add an authenticated operational-events hub that publishes identifier-only events after committed changes; verify unauthorized subscriptions and rolled-back success events are rejected. Evidence: [Evidence #181](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/blob/main/docs/evidence/mobile-events-181.md).

## 3. Generated client and secure sessions

- [x] 3.1 Add a pinned OpenAPI-to-TypeScript generation command and generated-client boundary; verify regeneration from the same contract produces no diff. Evidence: [PR #168](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/168).
- [x] 3.2 Implement authenticated transport with RFC 7807 error mapping, cancellation, and safe retry rules; verify validation, forbidden, conflict, business-rule, and network failures. Evidence: [PR #168](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/168).
- [x] 3.3 Implement login, in-memory access tokens, secure refresh-token storage, and serialized refresh rotation; verify concurrent unauthorized responses trigger only one refresh. Evidence: [PR #168](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/168).
- [x] 3.4 Clear credentials, server caches, protected routes, and retained delivery data on logout or terminal refresh failure; verify protected content is unavailable after cleanup. Evidence: [PR #168](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/168).

## 4. P0 permission-aware mobile workflows

- [x] 4.1 Build the authenticated shell from effective permission codes, including loading, forbidden, and multi-role states; verify all P0 roles see only allowed modules. Evidence: [PR #171](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/171).
- [ ] 4.2 Build confirmed and preparing ticket views, ticket detail, start-preparing, and mark-ready actions for baristas; verify Android end-to-end progression from Confirmed to Preparing to Ready.
- [ ] 4.3 Build the manager pending-discount inbox and approve or reject actions; verify a server-confirmed decision changes the shared order state.
- [x] 4.4 Build inventory balance and low-stock views plus stock adjustment with a required reason; verify valid adjustments update the shared movement history and invalid adjustments do not change stock. Evidence: `apps/staff-mobile/tests/inventory.test.tsx`; PostgreSQL-backed API test `InventoryAdjustment_EnforcesPermissionValidationAndIdempotency` verifies valid/idempotent adjustment, required reason, forbidden access, unchanged balance, and unchanged movement count.
- [ ] 4.5 Build courier assignment views, operational address/contact display, device navigation launch, and out-for-delivery, delivered, or failed actions; verify a courier cannot load another courier's assignment and a failure requires a reason.
- [x] 4.6 Add online, reconnecting, and offline states with manual refresh and disabled mutations; verify no write is queued or reported successful while disconnected. Evidence: [#145 connectivity implementation and verification](../../../docs/evidence/mobile-connectivity-145/README.md); full quality check, API contract check, and Android export pass.
- [x] 4.7 Connect authenticated operational events to cache invalidation and post-reconnect refetch; verify a WinUI or API change refreshes the affected mobile view without overwriting a newer state. Evidence: [Evidence #181](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/blob/main/docs/evidence/mobile-events-181.md).

## 5. P1 operational coverage

- [x] 5.1 Build cashier order search, table occupancy, fulfillment status, and supported payment recording; verify payment idempotency and refreshed balance behavior. Evidence: [Evidence #182](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/blob/main/docs/evidence/mobile-order-operations-182/README.md).
- [x] 5.2 Build manager cancellation authorization and ready-delivery assignment; verify required reasons, eligible staff, state rules, and audit actors. Evidence: [Evidence #182](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/blob/main/docs/evidence/mobile-order-operations-182/README.md).
- [x] 5.3 Add device registration, token rotation, and removal on logout; verify ownership, invalid-token cleanup, and notification-permission denial. Evidence: [Evidence #181](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/blob/main/docs/evidence/mobile-events-181.md).
- [x] 5.4 Add privacy-safe actionable notifications and authenticated deep links for urgent work; verify previews omit customer, address, payment, and credential data. Evidence: [Evidence #181](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/blob/main/docs/evidence/mobile-events-181.md).
- [x] 5.5 Build limited administrator account search, eligible deactivation, and security-event views; verify final-administrator protection and session revocation.
- [x] 5.6 Build prepared purchase-receipt review and posting; verify a retry cannot increase shared stock more than once and the resulting movement history is visible. Evidence: `apps/staff-mobile/tests/inventory.test.tsx`; PostgreSQL-backed API test `ReceiptPosting_ChangesSharedBalanceAndAddsOneAuditedMovement` verifies the retry conflict, one balance increase, and one movement.

## 6. P2 release hardening and evidence

- [ ] 6.1 Run mobile end-to-end coverage for all P0 workflows and every implemented P1 workflow, including payment, cancellation, assignment, receipt posting, notifications, and administration; verify P0 passes on Android before a final demo and P1 passes before it is claimed as implemented.
- [ ] 6.2 Run accessibility checks for each P0 screen with enlarged text, screen-reader semantics, selection feedback, and phone/tablet layout; verify required content remains usable.
- [ ] 6.3 Add CI jobs for installation, type-checking, linting, unit tests, generated-client drift, Android build, and affected backend tests; verify a clean CI run passes.
- [ ] 6.4 Validate simultaneous WinUI and mobile use against one API and PostgreSQL database; verify state, conflicts, permissions, and audit actors remain consistent.
- [ ] 6.5 Document emulator, physical-device LAN, notification, environment, test, release, rollback, and final-submission procedures; verify a new contributor can complete the P0 demo without undocumented steps.
- [ ] 6.6 Produce the final unlisted mobile demonstration and map each shown function to the function list, test evidence, and responsible team member; verify the recording excludes secrets and demonstrates only implemented work.
