## Purpose

Defines final-version staff authentication, token lifecycle, account state, roles, permissions, and server-side enforcement across protected API operations.

## ADDED Requirements

### Requirement: Staff authentication
The final system SHALL authenticate active staff accounts with valid credentials and issue a short-lived access token plus a refresh token after successful login.

#### Scenario: Successful login
- **WHEN** an active staff member submits valid credentials
- **THEN** the system returns access and refresh tokens with the user's identity and effective role information

#### Scenario: Invalid login
- **WHEN** a user submits invalid credentials or uses a deactivated account
- **THEN** authentication is rejected without issuing tokens or revealing which credential was incorrect

### Requirement: Token renewal and revocation
The system SHALL rotate refresh tokens during renewal and SHALL reject expired, revoked, reused, or otherwise invalid refresh tokens.

#### Scenario: Renew an authenticated session
- **WHEN** a valid unrevoked refresh token is submitted
- **THEN** the system issues a new token pair and invalidates the previous refresh token

### Requirement: Role-based access control
The system SHALL support admin, manager, cashier, barista, inventory staff, and delivery staff roles with explicit permissions appropriate to their operational responsibilities.

#### Scenario: Inventory staff posts a receipt
- **WHEN** authenticated inventory staff submit a valid purchase receipt
- **THEN** the server permits the operation because their role includes inventory-receiving permission

#### Scenario: Cashier attempts user administration
- **WHEN** an authenticated cashier calls an API reserved for administrators
- **THEN** the server returns a forbidden result and makes no change

### Requirement: Server-side authorization
Every protected operation MUST enforce authentication and authorization at the API regardless of which controls are visible in the client.

#### Scenario: Call a protected API directly
- **WHEN** a user without the required permission sends a direct request to a protected API
- **THEN** the server rejects the request and does not execute the operation

### Requirement: Account administration
Authorized administrators SHALL be able to create, update, deactivate, reactivate, and assign roles to staff accounts, and MUST NOT be able to remove the final active administrator.

#### Scenario: Protect the final administrator
- **WHEN** an administrator attempts to deactivate or demote the only remaining active administrator
- **THEN** the system rejects the operation

### Requirement: Security event traceability
The system SHALL record auditable events for successful logins, failed logins, refresh-token reuse detection, account state changes, and role changes.

#### Scenario: Record a denied login
- **WHEN** authentication fails
- **THEN** the system records the time, account identifier where available, outcome, and request context without storing the submitted password
