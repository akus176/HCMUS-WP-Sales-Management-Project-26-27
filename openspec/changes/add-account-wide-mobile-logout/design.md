# Design

## Context

The shared identity service already issues ten-minute access tokens and rotating fourteen-day refresh tokens. `POST /api/v1/auth/logout-all` revokes every refresh family owned by the authenticated account and returns `204`; ordinary `POST /api/v1/auth/logout` revokes only the presented family. Access tokens are self-contained JWTs and remain valid until their existing expiry, so account-wide sign-out cannot promise immediate token invalidation.

The Expo staff client keeps the access token in memory and the refresh token in secure storage. `createSessionClient` already owns an epoch, a serialized storage queue, request cancellation, cache clearing, device registration and both online and offline ordinary sign-out. The Account screen currently offers only connection details, ordinary sign-out and a cancellation sheet.

## Goals / Non-Goals

**Goals:** expose a distinct, confirmable account-wide action; require server confirmation; keep failures truthful; serialize duplicate attempts; prevent late responses from clearing a newer session; preserve ordinary sign-out and Counter + Preparation navigation; cover the behavior with API, session, component and Maestro checks.

**Non-Goals:** immediate access-token/JWT revocation, a new API route or database change, a global "sessions" management list, remote-device selection, session metadata, background replay, changes to permissions or business workflows, and any Counter/Preparation redesign.

## Decisions

1. **Separate client operation.** Add `logoutAll()` rather than an overload of `logout()` because the success and failure semantics differ. Ordinary logout stays immediate and best-effort offline; account-wide logout requires a confirmed `204`. Sharing one method would make it too easy to clear local state on an unconfirmed global request.

2. **Pre-dispatch renewal, no mutation replay.** Resolve the bearer through the existing `getOperationalAccessToken(userId)` helper. It refreshes an access token inside the established 30-second window. If renewal fails with `401`, the existing refresh path clears the unusable session and the protected UI is removed; this is not an account-wide success claim. The revocation mutation itself is sent at most once per explicit attempt and is never retried automatically.

3. **Server confirmation is the only success signal.** Send the generated operation with the bearer token and a ten-second abort controller. Treat only `response.ok` (expected `204`) as confirmed. Map `401` to an authorization failure, `5xx` and transport/timeout failures to an uncertain outcome, and other non-success statuses to a rejection. Bind the error type to the outcome so the UI cannot accidentally show a generic success.

4. **Single-flight per session epoch.** Keep the in-flight account-wide promise with its epoch. Duplicate confirmations in the same epoch share the promise and therefore one request. After completion the entry is cleared so an explicit retry can run. On success, clear local state only when the epoch is still current; a newer sign-in owns its own credentials and cleanup.

5. **Cleanup order.** After confirmation, and only while the epoch is unchanged, remove this device's push registration with the confirmed bearer on a best-effort basis, then call the existing `clear()` path. `clear()` increments the epoch, drops the in-memory access token, aborts in-flight requests, clears device memory, invokes the supplied cache cleanup and queues secure-storage deletion. A storage failure is surfaced distinctly because the server has already revoked the account's refresh families.

6. **Offline and busy guards live in the UI.** The Account screen disables account-wide sign-out unless connectivity allows writes, explains reconnection, and prevents duplicate taps. Ordinary sign-out remains enabled offline. Errors stay inside the confirmation sheet so the user can retry or cancel without losing context.

7. **No new API or contract generation.** The OpenAPI route and generated TypeScript already exist. The change only wires the client and verification. Contract checks should therefore show zero drift.

## UI and interaction design

More → Account contains, in order: identity, connection details, ordinary **Sign out**, then a separate **Sign out everywhere** action with the caption "Ends your session on every device, including this one." When offline the caption changes to "Reconnect to sign out everywhere." and the action is disabled; ordinary sign-out stays enabled.

The account-wide confirmation sheet is titled "Sign out everywhere?" and explains that every device, including this one, will need to sign in again. It presents **Confirm sign out everywhere** and **Cancel**, keeps the previous sheet behavior for Android Back and the backdrop (blocked while busy), and renders an inline accessible error when the request fails. On success the session clears and the existing protected-route guard returns the app to sign-in. No success banner is required because the protected UI is gone.

The ordinary confirmation remains "Sign out of this device?" with **Confirm sign out** and **Stay signed in**, preserving current Maestro selectors.

## Failure taxonomy

| Outcome | Local session | Message |
| --- | --- | --- |
| Offline (UI guard) | Preserved | Connect to the internet to sign out everywhere. |
| `204` | Cleared | No banner; routed to sign-in. |
| `401` | Preserved unless the refresh path already rejected the session | Your sign-in expired before signing out everywhere was confirmed. Sign in again. |
| Other `4xx` | Preserved | The service did not confirm signing out everywhere. Try again. |
| `5xx`, network, timeout | Preserved | Signing out everywhere could not be confirmed. Check your connection and try again. |
| Storage deletion fails after `204` | In-memory cleared | Signed out everywhere, but this device could not remove its saved sign-in. Try again. |

## Verification plan

- **Session client:** confirmed revocation clears state/storage/caches and aborts pending work; rejection, `401`, network failure and timeout preserve the session without replay; duplicate calls produce one request; a late response cannot clear a newer login; an ordinary logout still works offline and never calls `logout-all`.
- **Account/component:** cancel sends nothing; offline disables only account-wide sign-out; confirmed global sign-out clears the session; failure keeps the session and shows the inline error.
- **API/PostgreSQL:** anonymous `logout-all` is rejected; ordinary logout revokes only its family; account-wide logout revokes every family for the acting account and leaves another account's families usable.
- **Maestro:** focused flows for canceled confirmation, offline guard, ordinary sign-out and confirmed account-wide sign-out. The runner is expected to verify independent refresh-family outcomes through the API without publishing tokens. Native execution is not part of this change's local verification and remains an explicit open checklist item.

## Risks / Trade-offs

- A lost response may leave the server revoked while the client still shows a signed-in session. The next access-token expiry or refresh will clear it; the UI never claims success and offers a retry. This is preferable to claiming a global sign-out that may not have happened.
- Account-wide sign-out does not invalidate access tokens already issued to other devices for up to ten minutes. The documentation states this limitation explicitly; protected requests still re-check account state and permissions.
- Push registration removal is best effort because it must not turn a confirmed revocation into a failure. The server-side refresh revocation remains the security boundary.
- The sequence adds one pre-flight renewal only when the access token is near expiry, so the normal case remains one request.

## Migration / rollout

No migration. The API route already ships on `main`. Roll out with the mobile application; users can still use ordinary sign-out if their build predates the action.
