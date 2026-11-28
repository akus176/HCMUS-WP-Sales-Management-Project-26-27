## Purpose

Let authenticated staff end every refresh session owned by their account from the mobile Account screen, with an explicit confirmation and honest outcomes, without changing ordinary per-device sign-out or the Counter + Preparation workspace model.

## ADDED Requirements

### Requirement: Distinct account-wide sign-out action

The application SHALL present an account-wide sign-out action under More → Account that is visually and textually distinct from ordinary device sign-out. The action SHALL explain that it ends the signed-in account's sessions on every device, including the current device.

#### Scenario: Discover both session actions
- **WHEN** staff open More → Account
- **THEN** **Sign out** remains available for the current device and **Sign out everywhere** is presented as a separate action
- **AND** the account-wide action states that every device, including this one, will be signed out

#### Scenario: Cancel account-wide sign-out
- **WHEN** staff choose **Sign out everywhere** and then cancel the confirmation
- **THEN** the confirmation closes, no revocation request is sent, and the current session and protected data remain available

#### Scenario: Attempt account-wide sign-out while offline
- **WHEN** connectivity is not online
- **THEN** account-wide sign-out is unavailable with an instruction to reconnect
- **AND** ordinary device sign-out remains available because it can clear local state offline

### Requirement: Server-confirmed account-wide revocation

The application MUST send `POST /api/v1/auth/logout-all` with the current bearer access token and MUST claim account-wide success only after a `204` response. On confirmation it SHALL clear the initiating device's protected state.

#### Scenario: Confirm account-wide sign-out
- **WHEN** staff confirm the action while online and the service returns `204`
- **THEN** the application removes this device's push registration where possible, aborts in-flight work, clears cached protected data, clears in-memory credentials and deletes the saved refresh token
- **AND** the application returns to the signed-out sign-in state without claiming that already-issued access tokens were revoked immediately

#### Scenario: Duplicate confirmation
- **WHEN** staff confirm the action more than once while a request is in flight
- **THEN** the application sends one account-wide revocation request and shares its outcome across the duplicate attempts

### Requirement: Truthful account-wide failure outcomes

The application MUST NOT claim global success for offline, unauthorized, rejected, network-failed, timed-out or otherwise unknown outcomes. It MUST keep the current session usable and MUST NOT replay the revocation automatically.

#### Scenario: Service rejects or does not authorize the request
- **WHEN** account-wide sign-out receives a `401` or another non-success response
- **THEN** the session remains signed in, the confirmation presents an accurate error and no protected state is cleared as if revocation succeeded

#### Scenario: Network or timeout outcome
- **WHEN** the request fails in transport or exceeds the bounded request time
- **THEN** the application reports that account-wide sign-out could not be confirmed, keeps the local session and does not resend the request automatically
- **AND** a later explicit retry may send a new request

#### Scenario: Local credential cleanup fails after confirmation
- **WHEN** the service confirms revocation but the device cannot remove its saved refresh token
- **THEN** the application reports the local cleanup failure rather than presenting a fully successful sign-out

### Requirement: Race-safe cleanup

A late or superseded account-wide response MUST NOT clear credentials or caches that belong to a newer session.

#### Scenario: A new session starts while revocation is in flight
- **WHEN** a newer sign-in replaces the session before the account-wide response completes
- **THEN** completion does not clear the newer session's credentials, caches or device registration

#### Scenario: Navigation and permissions remain intact
- **WHEN** the account-wide action is added and not used
- **THEN** Counter and Preparation, permission-derived navigation, fresh access checks and ordinary workflows remain unchanged

### Requirement: Ordinary sign-out isolation

Ordinary device sign-out SHALL remain session-scoped, SHALL revoke only the presented refresh family when reachable, and SHALL continue to clear local state immediately when offline.

#### Scenario: Sign out this device
- **WHEN** staff confirm ordinary **Sign out**
- **THEN** the application calls the session-scoped logout contract and does not call the account-wide contract
- **AND** the saved refresh token and protected caches are cleared even if the service is unreachable

### Requirement: Contract and documentation agreement

The specification, design, API description and mobile function list SHALL agree that account-wide sign-out is exposed under Account, uses `POST /api/v1/auth/logout-all`, and does not invalidate already-issued access tokens before their natural expiry.

#### Scenario: Reconcile the documented contract
- **WHEN** the change is reviewed
- **THEN** the OpenSpec artifacts and the mobile API/identity/function documentation identify the same route, placement and access-token limitation
- **AND** generated mobile contract checks show no drift because the route already exists
