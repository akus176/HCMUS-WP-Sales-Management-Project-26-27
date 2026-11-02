# Tasks

## 1. Delivery gates and foundations

- [ ] 1.1 Create a mobile delivery baseline that records the current API, identity, OpenAPI, emulator, and device-network readiness; verify every P0 dependency has an owner and a GitHub issue before mobile workflow work starts.
- [ ] 1.2 Scaffold `apps/staff-mobile` as a strict TypeScript Expo application with Expo Router; verify the documented command launches it on an Android emulator.
- [ ] 1.3 Pin package-manager metadata and configure type-checking, linting, formatting, unit tests, component tests, and Android end-to-end tests; verify a clean install and each quality command pass.
- [ ] 1.4 Add validated environment configuration for emulator, physical device, test, and release API base URLs; verify missing or invalid configuration fails without exposing a secret.
- [ ] 1.5 Implement shared mobile theme tokens and controls with 48-dp primary actions, dynamic type, contrast, and accessible labels; verify component tests cover the required states.

## 2. Shared API, identity, and contracts

- [ ] 2.1 Implement the final API authentication and permission endpoints required by mobile sessions; verify valid, invalid, inactive, and multi-role users with API integration tests.
- [ ] 2.2 Expose P0 API queries and mutations for preparation tickets, pending discounts, ingredient balances, stock adjustments, and the authenticated courier's assignments; verify allowed, forbidden, empty, validation, and conflict cases.
- [ ] 2.3 Publish a deterministic OpenAPI document for the mobile-required endpoints and schemas; verify generated contract tests match the API integration tests.
- [ ] 2.4 Add idempotency and audit behavior to payment, stock, and delivery mutations where the existing domain supports them; verify retries do not create duplicate effects.
- [ ] 2.5 Add an authenticated operational-events hub that publishes identifier-only events after committed changes; verify unauthorized subscriptions and rolled-back success events are rejected.

## 3. Generated client and secure sessions

- [ ] 3.1 Add a pinned OpenAPI-to-TypeScript generation command and generated-client boundary; verify regeneration from the same contract produces no diff.
- [ ] 3.2 Implement authenticated transport with RFC 7807 error mapping, cancellation, and safe retry rules; verify validation, forbidden, conflict, business-rule, and network failures.
- [ ] 3.3 Implement login, in-memory access tokens, secure refresh-token storage, and serialized refresh rotation; verify concurrent unauthorized responses trigger only one refresh.
- [ ] 3.4 Clear credentials, server caches, protected routes, and retained delivery data on logout or terminal refresh failure; verify protected content is unavailable after cleanup.

## 4. P0 permission-aware mobile workflows

- [ ] 4.1 Build the authenticated shell from effective permission codes, including loading, forbidden, and multi-role states; verify all P0 roles see only allowed modules.
- [ ] 4.2 Build confirmed and preparing ticket views, ticket detail, start-preparing, and mark-ready actions for baristas; verify Android end-to-end progression from Confirmed to Preparing to Ready.
- [ ] 4.3 Build the manager pending-discount inbox and approve or reject actions; verify a server-confirmed decision changes the shared order state.
- [ ] 4.4 Build inventory balance and low-stock views plus stock adjustment with a required reason; verify valid adjustments update the shared movement history and invalid adjustments do not change stock.
- [ ] 4.5 Build courier assignment views, operational address/contact display, device navigation launch, and delivered or failed actions; verify a courier cannot load another courier's assignment and a failure requires a reason.
- [ ] 4.6 Add online, reconnecting, and offline states with manual refresh and disabled mutations; verify no write is queued or reported successful while disconnected.
- [ ] 4.7 Connect authenticated operational events to cache invalidation and post-reconnect refetch; verify a WinUI or API change refreshes the affected mobile view without overwriting a newer state.

## 5. P1 operational coverage

- [ ] 5.1 Build cashier order search, table occupancy, fulfillment status, and supported payment recording; verify payment idempotency and refreshed balance behavior.
- [ ] 5.2 Build manager cancellation authorization and ready-delivery assignment; verify required reasons, eligible staff, state rules, and audit actors.
- [ ] 5.3 Add device registration, token rotation, and removal on logout; verify ownership, invalid-token cleanup, and notification-permission denial.
- [ ] 5.4 Add privacy-safe actionable notifications and authenticated deep links for urgent work; verify previews omit customer, address, payment, and credential data.
- [ ] 5.5 Build limited administrator account search, eligible deactivation, and security-event views; verify final-administrator protection and session revocation.

## 6. P2 release hardening and evidence

- [ ] 6.1 Run mobile end-to-end coverage for login, permissions, barista progression, discount handling, inventory adjustment, delivery completion, offline-write prevention, and logout cleanup; verify all P0 cases pass on Android.
- [ ] 6.2 Run accessibility checks for each P0 screen with enlarged text, screen-reader semantics, selection feedback, and phone/tablet layout; verify required content remains usable.
- [ ] 6.3 Add CI jobs for installation, type-checking, linting, unit tests, generated-client drift, Android build, and affected backend tests; verify a clean CI run passes.
- [ ] 6.4 Validate simultaneous WinUI and mobile use against one API and PostgreSQL database; verify state, conflicts, permissions, and audit actors remain consistent.
- [ ] 6.5 Document emulator, physical-device LAN, notification, environment, test, release, rollback, and final-submission procedures; verify a new contributor can complete the P0 demo without undocumented steps.
- [ ] 6.6 Produce the final unlisted mobile demonstration and map each shown function to the function list, test evidence, and responsible team member; verify the recording excludes secrets and demonstrates only implemented work.
