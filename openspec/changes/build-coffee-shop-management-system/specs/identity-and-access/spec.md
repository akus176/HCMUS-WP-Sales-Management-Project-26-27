## Purpose

Defines final-version staff authentication, token lifecycle, account state, roles, permissions, and server-side enforcement across protected API operations.

## ADDED Requirements

### Requirement: Staff authentication
The final deployment SHALL authenticate active non-administrator staff with a username and password and SHALL authenticate administrators through their linked Google identity in WinUI. Both flows SHALL issue the same application access and refresh tokens. Isolated local and test deployments MAY run with Google sign-in disabled and use existing credential fixtures.

#### Scenario: Successful login
- **WHEN** an active non-administrator staff member submits valid credentials
- **THEN** the system returns access and refresh tokens with the user's identity and effective role information

#### Scenario: Invalid login
- **WHEN** a user submits invalid credentials or uses a deactivated account
- **THEN** authentication is rejected without issuing tokens or revealing which credential was incorrect

#### Scenario: Linked administrator signs in on Windows
- **WHEN** WinUI completes Google's system-browser authorization-code flow with PKCE and the API verifies the Google ID token signature, issuer, configured audience, expiry, nonce, and verified email
- **THEN** the API issues application tokens only if the configured Google subject and email match an existing active account with the Admin role and the same email

#### Scenario: Administrator sign-in is denied
- **WHEN** a Google identity is unlinked, has invalid claims, or maps to an inactive or non-administrator account, or an administrator attempts to use a local password in the final deployment
- **THEN** the API returns a generic denial without issuing tokens or creating any account or role assignment

### Requirement: Token renewal and revocation
The system SHALL rotate refresh tokens during renewal and SHALL reject expired, revoked, reused, or otherwise invalid refresh tokens.
In the final deployment, administrator access and refresh tokens MUST originate from the current verified Google link. Removing the link or Admin role invalidates those sessions.

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
Google links SHALL be provisioned explicitly in private server configuration by the deployment operator. In the final deployment the last active administrator with a valid configured link MUST also be protected from deactivation.

#### Scenario: Protect the final administrator
- **WHEN** an administrator attempts to deactivate or demote the only remaining active administrator
- **THEN** the system rejects the operation

### Requirement: Security event traceability
The system SHALL record auditable events for successful logins, failed logins, refresh-token reuse detection, account state changes, and role changes.

#### Scenario: Record a denied login
- **WHEN** authentication fails
- **THEN** the system records the time, account identifier where available, outcome, and request context without storing passwords, OAuth codes, provider tokens, or application tokens
