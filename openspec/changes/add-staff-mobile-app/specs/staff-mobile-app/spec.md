## Purpose

Defines a secure, role-aware mobile companion that lets coffee-shop staff perform time-sensitive operational work against the same authoritative backend used by the Windows client.

## ADDED Requirements

### Requirement: Shared authoritative backend
The staff mobile application SHALL use the coffee-shop ASP.NET Core API as its only business-data interface and SHALL present state from the same system of record used by the final Windows client.

#### Scenario: Mobile update appears to desktop users
- **WHEN** authorized mobile staff successfully change an order, delivery, approval, payment, inventory, or account state
- **THEN** the change is persisted by the shared backend and is visible to authorized Windows and mobile users

#### Scenario: Backend rejects a mobile action
- **WHEN** the API rejects a mobile action because of validation, authorization, business rules, or a concurrency conflict
- **THEN** the application leaves the authoritative state unchanged and explains the rejection to the user

### Requirement: Secure staff sessions
The application SHALL authenticate individual active staff accounts through the shared API, SHALL renew sessions using rotated refresh tokens, and MUST remove locally held credentials and protected business data after logout or terminal session failure.

#### Scenario: Staff member signs in
- **WHEN** an active staff member submits valid credentials
- **THEN** the application establishes a session and loads the navigation and actions permitted for that user

#### Scenario: Refresh token is rejected
- **WHEN** session renewal fails because the refresh token is expired, revoked, reused, or otherwise invalid
- **THEN** the application clears the session and requires the staff member to authenticate again without exposing protected data

### Requirement: Permission-aware mobile experience
The application SHALL support admin, manager, cashier, barista, inventory staff, and delivery staff accounts, SHALL derive visible modules and actions from effective permissions, and MUST treat API authorization as authoritative.

#### Scenario: User has multiple roles
- **WHEN** a staff member signs in with permissions from more than one role
- **THEN** the application presents the combined set of permitted mobile modules and actions

#### Scenario: Hidden action is invoked directly
- **WHEN** a user without the required permission attempts a protected action through a stale screen, deep link, or crafted request
- **THEN** the server rejects the action and the application displays an authorization-safe result

### Requirement: Barista preparation workflow
Authorized baristas SHALL be able to view confirmed and preparing orders, inspect preparation details, start a confirmed order, and mark a preparing order ready while preserving the server-defined order state machine.

#### Scenario: Barista advances a ticket
- **WHEN** a barista starts a confirmed order and later marks it ready
- **THEN** the shared order transitions through preparing to ready and records the acting staff member and transition times

#### Scenario: Ticket was changed elsewhere
- **WHEN** a barista attempts a transition after another client has moved or cancelled the order
- **THEN** the application does not overwrite the newer state and refreshes the ticket with an explanatory conflict message

### Requirement: Manager approval and oversight workflow
Authorized managers SHALL be able to review pending discount requests, approve or reject them, authorize supported cancellations, assign ready delivery orders, and inspect urgent operational items.

#### Scenario: Manager handles a discount request
- **WHEN** a manager opens a pending discount and approves or rejects it
- **THEN** the application displays the server-confirmed decision and the affected order reflects that decision on all clients

### Requirement: Cashier operational workflow
Authorized cashiers SHALL be able to search and inspect orders, view table occupancy and fulfillment state, and record supported payments without exposing full desktop administration functions.

#### Scenario: Cashier records a payment
- **WHEN** a cashier records a valid payment for an eligible order
- **THEN** the server persists the payment once and the application displays the updated paid amount and balance

### Requirement: Inventory mobile workflow
Authorized inventory staff SHALL be able to view ingredient balances and low-stock items, post prepared purchase receipts, and record stock adjustments with mandatory reasons.

#### Scenario: Inventory staff records an adjustment
- **WHEN** authorized inventory staff submit a valid quantity adjustment with a reason
- **THEN** the shared inventory balance and movement history reflect the server-confirmed adjustment

#### Scenario: Adjustment lacks a reason
- **WHEN** inventory staff attempt to submit an adjustment without a reason
- **THEN** the application identifies the required reason and no stock change is made

### Requirement: Delivery staff workflow
Authorized delivery staff SHALL be able to view deliveries assigned to themselves, access the customer contact and address required for an active assignment, launch device navigation, and record out-for-delivery, delivered, or failed outcomes allowed by the server state machine.

#### Scenario: Delivery is completed
- **WHEN** assigned delivery staff mark an out-for-delivery assignment as delivered
- **THEN** the server records the delivered state, actor, and time and the application removes it from active assignments

#### Scenario: Delivery fails
- **WHEN** assigned delivery staff mark a delivery failed
- **THEN** the application requires a failure reason and displays the server-confirmed failed state after submission

### Requirement: Limited mobile administration
Authorized administrators SHALL be able to find staff accounts, deactivate a compromised account subject to final-administrator protection, and inspect security events; full role configuration and account creation SHALL remain outside the first mobile release.

#### Scenario: Administrator deactivates a compromised account
- **WHEN** an authorized administrator deactivates an eligible active staff account
- **THEN** the server revokes that account's sessions, records the security event, and the application displays the resulting inactive state

### Requirement: Near-real-time operational updates
While connected, the application SHALL receive authenticated operational updates relevant to the current user's permissions and active role workflows, and SHALL provide manual refresh when live delivery is unavailable.

#### Scenario: Order changes on another client
- **WHEN** an authorized Windows or mobile user changes an order represented on the current mobile screen
- **THEN** the application refreshes the affected view without requiring the user to sign in again

#### Scenario: Live connection is interrupted
- **WHEN** the real-time connection becomes unavailable
- **THEN** the application communicates the degraded state and allows the user to request a current server snapshot

### Requirement: Actionable notifications
The system SHALL deliver permission-filtered notifications for assigned or time-sensitive work and SHALL deep-link an authenticated recipient to the relevant record without exposing sensitive customer details in the notification preview.

#### Scenario: Manager receives an approval notification
- **WHEN** a discount requires manager approval and an eligible manager has registered a mobile device
- **THEN** the manager receives a notification that opens the pending approval after authentication

#### Scenario: Unauthorized recipient opens an old notification
- **WHEN** a user opens a notification after losing permission to the referenced record
- **THEN** the application does not display the protected record and communicates that access is unavailable

### Requirement: Online-first and safe degraded behavior
State-changing mobile actions MUST require a confirmed API response. When connectivity is lost, the application MAY continue showing data already loaded in the current session but MUST disable or reject writes rather than silently queueing them for later synchronization.

#### Scenario: Staff attempts a write while offline
- **WHEN** a staff member attempts a state-changing action without backend connectivity
- **THEN** the application makes no local success claim, does not queue the write, and instructs the user to reconnect and retry

### Requirement: Mobile privacy and local-data protection
The application MUST store session secrets using platform-protected storage, MUST minimize persisted business data, and MUST remove delivery contact and address information when it is no longer required for an active assignment or session.

#### Scenario: Staff member logs out
- **WHEN** a staff member logs out
- **THEN** the application removes tokens and locally retained protected business data before returning to the login screen

### Requirement: Accessible responsive interface
The application SHALL provide portrait-first phone layouts and responsive tablet layouts with readable contrast, scalable text, screen-reader labels, visible focus or selection states, and touch targets of at least 48 device-independent pixels for primary actions.

#### Scenario: Staff uses enlarged text
- **WHEN** the operating system text scale is increased
- **THEN** operational content and primary actions remain readable and usable without hiding required information
