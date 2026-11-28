# Proposal

## Why

Staff can sign out the current device from More → Account, and the shared API already exposes `POST /api/v1/auth/logout-all`, but the mobile app does not use it. A staff member who loses a phone or suspects a stolen refresh token therefore cannot end that account's sessions on other devices from the app. Issue #234's owner-approved scope selects a distinct account-wide sign-out action while preserving Counter + Preparation navigation and ordinary device sign-out.

## What Changes

- Add a distinct **Sign out everywhere** action in More → Account with a clear description, a confirmation sheet, cancellation, and an offline/busy guard. Keep the existing **Sign out** action for the current device.
- Reuse `POST /api/v1/auth/logout-all`. The application MUST treat only a `204` response as confirmation that the account's refresh families were revoked.
- Before dispatch, renew an access token that is within the existing 30-second renewal window. Never replay the revocation mutation automatically.
- After confirmation, remove this device's push registration where possible, abort in-flight work, cancel and clear cached protected data, clear in-memory credentials, and delete the saved refresh token.
- Keep failure outcomes truthful: offline, `401`, rejection, network failure, timeout and unknown transport outcomes do not claim global success and do not clear the local session. A post-confirmation local storage failure is reported as such.
- Serialize duplicate confirmations into one request and ignore a late response once a newer session exists, so a stale completion cannot clear new credentials.
- Cover the behavior with API/PostgreSQL regressions, session-client regressions, Account/navigation component regressions and focused Maestro flows.
- Align the OpenSpec artifacts, API description, identity description and mobile function list. State explicitly that already-issued short-lived access tokens retain the existing natural-expiry behavior; this change does not promise immediate JWT invalidation.

## Capabilities

### New Capabilities

- `mobile-session-actions`: Account-wide and per-device mobile session termination, confirmation and cancellation, server-confirmed outcomes, offline guards and protected-state cleanup.

### Modified Capabilities

None. There are no archived main specs; the parent mobile application behavior and shared identity contract remain authoritative.

## Impact

Implementation affects `apps/staff-mobile/src/session/session.ts`, `src/shell/NativeWorkspaceHost.tsx`, session/component/navigation tests, and focused Maestro flows. API regressions extend `tests/CoffeeShop.Api.Tests/StaffIdentityHttpTests.cs`. Contract/function documentation updates `docs/mobile-api-contract-137.md`, `docs/staff-identity.md` and `docs/final-submission/mobile-function-list.md`. No API route, database schema, migration or permission change is required. Counter and Preparation tabs, business workflows, ordinary offline sign-out and permissions are unchanged. Issue #234 is the single canonical checklist; no local `tasks.md` is created.
