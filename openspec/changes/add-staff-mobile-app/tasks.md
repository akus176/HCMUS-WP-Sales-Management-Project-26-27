# Tasks

Current scope: [Counter + Preparation decision](../../../docs/mobile-scope-206.md),
effective 26 November 2026. Checked foundation/P0 items below retain their historical
completion records from #95; they do not certify the current build's native acceptance.
Changed selected tasks remain open for #206/current evidence. Retired IDs are recorded
at the end and must not be reused or treated as missing current-release features.

## 1. Delivery gates and foundations

- [x] 1.1 Create a mobile delivery baseline that records the current API, identity, OpenAPI, emulator, and device-network readiness; verify every P0 dependency has an owner and a GitHub issue before mobile workflow work starts.
      Evidence: [S0 baseline and dependency map](../../../docs/mobile-delivery-baseline.md) ([#136](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/136)); environment and unassigned-work blockers are explicit. Completion remains subject to the issue's required review.
- [x] 1.2 Scaffold `apps/staff-mobile` as a strict TypeScript Expo application with Expo Router; verify the documented command launches it on an Android emulator.
- [x] 1.3 Pin package-manager metadata and configure type-checking, linting, formatting, unit tests, component tests, and Android end-to-end tests; verify a clean install and each quality command pass.
- [x] 1.4 Add validated environment configuration for emulator, physical device, test, and release API base URLs; verify missing or invalid configuration fails without exposing a secret.
- [x] 1.5 Implement shared mobile theme tokens and controls with 48-dp primary actions, dynamic type, contrast, and accessible labels; verify component tests cover the required states.

## 2. Shared API, identity, and contracts

- [x] 2.1 Implement the final API authentication and permission endpoints required by mobile sessions; verify valid, invalid, inactive, and multi-role users with API integration tests.
- [x] 2.2 Expose P0 API queries and mutations for preparation tickets, pending discounts, ingredient balances, stock adjustments, and the authenticated courier's assignments; verify allowed, forbidden, empty, validation, and conflict cases.
- [x] 2.3 Publish a deterministic OpenAPI document for the mobile-required endpoints and schemas; verify generated contract tests match the API integration tests.
- [x] 2.4 Add idempotency and audit behavior to payment, stock, and delivery mutations where the existing domain supports them; verify retries do not create duplicate effects.
- [x] 2.5 Add an authenticated operational-events hub that publishes identifier-only events after committed changes; verify unauthorized subscriptions and rolled-back success events are rejected.

## 3. Generated client and secure sessions

- [x] 3.1 Add a pinned OpenAPI-to-TypeScript generation command and generated-client boundary; verify regeneration from the same contract produces no diff.
- [x] 3.2 Implement authenticated transport with RFC 7807 error mapping, cancellation, and safe retry rules; verify validation, forbidden, conflict, business-rule, and network failures.
- [x] 3.3 Implement login, in-memory access tokens, secure refresh-token storage, and serialized refresh rotation; verify concurrent unauthorized responses trigger only one refresh.
- [x] 3.4 Clear credentials, server caches, protected routes, and retained delivery data on logout or terminal refresh failure; verify protected content is unavailable after cleanup.

## 4. P0 permission-aware mobile workflows

- [ ] 4.1 Build the authenticated shell from effective permission codes, including loading, unavailable, forbidden and multi-role states; expose only permitted Counter/Preparation actions plus Today and More/Account.
- [x] 4.2 Build confirmed and preparing ticket views, ticket detail, start-preparing, and mark-ready actions for baristas; verify Android end-to-end progression from Confirmed to Preparing to Ready.

- [x] 4.6 Add online, reconnecting, and offline states with manual refresh and disabled mutations; verify no write is queued or reported successful while disconnected. Evidence: [#145 connectivity implementation and verification](../../../docs/evidence/mobile-connectivity-145/README.md); full quality check, API contract check, and Android export pass.
- [ ] 4.7 Retain authenticated event invalidation and post-reconnect refetch for Counter/Preparation; verify cross-client updates against current-build evidence under #203 without overwriting newer state.

## 5. P1 operational coverage

- [ ] 5.1 Verify the implemented Counter menu/customization/cart, service modes and available-table picker, existing-customer/saved-address selection, draft save/review/confirm, discount requests, loyalty redemption, order lookup/status and supported payments; prove authoritative totals, validation, explicit reviewed writes, stable retry identities and refreshed balances on Android against the shared API (#206).
- [ ] 5.2 Verify permission-gated Counter cancellation with required reasons and ready-delivery assignment with eligible staff, valid state and audit actors on the selected Android build (#206).
- [ ] 5.3 Verify authenticated device registration/ownership, token rotation, logout removal, invalid-token cleanup and notification-permission denial on the selected Android build/API (#206).
- [ ] 5.4 Verify privacy-safe urgent Preparation notifications and authenticated fresh-permission deep links, including denial of unsupported/retired destinations; previews omit customer, address, payment and credential data (#206).

## 6. P2 release hardening and evidence

- [ ] 6.1 Record current Android acceptance for the retained P0 baseline and every selected Counter/manager/device/urgent-Preparation notification workflow. Reuse revision-matched evidence, link gaps to #206 or the existing P0/shared-state issue, and do not claim retired workflows as current acceptance.
- [ ] 6.2 Run accessibility checks for each P0 screen with enlarged text, screen-reader semantics, selection feedback, and phone/tablet layout; verify required content remains usable.
- [x] 6.3 Add CI jobs for installation, type-checking, linting, unit tests, generated-client drift, Android build, and affected backend tests; verify a clean CI run passes.
- [ ] 6.4 Validate simultaneous WinUI and mobile use against one API and PostgreSQL database; verify state, conflicts, permissions, and audit actors remain consistent.
- [x] 6.5 Document emulator, physical-device LAN, notification, environment, test, release, rollback, and final-submission procedures; verify a new contributor can complete the P0 demo without undocumented steps.
- [ ] 6.6 Produce the final unlisted mobile demonstration and map each shown function to the function list, test evidence, and responsible team member; verify the recording excludes secrets and demonstrates only implemented work.

## Retired task IDs (Out of Scope)

| ID  | Historical requirement                                  | Current disposition                                                                        |
| --- | ------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| 4.3 | Mobile manager discount approval/rejection inbox        | Retired; approval is performed in WinUI. Counter discount requests are selected under 5.1. |
| 4.4 | Mobile inventory/low-stock/adjustment/movement views    | Retired; shared stock rules remain authoritative. Historical #198 evidence is preserved.   |
| 4.5 | Mobile courier assignment execution/navigation/outcomes | Retired; Counter delivery service mode and manager assignment remain selected.             |
| 5.5 | Mobile staff administration/security-event views        | Retired; Account is session/connection/sign-out only.                                      |
| 5.6 | Mobile prepared receipt review/posting                  | Retired; WinUI/shared API retain receipt operations.                                       |

Retirement records a scope decision, not new implementation or new approval. Historical
#198/#201/#202/#203 reports remain unchanged. #206 must stay open until current selected
evidence, scope review and @longtoZ approval are linked. Final video remains Mobile 6.6.
