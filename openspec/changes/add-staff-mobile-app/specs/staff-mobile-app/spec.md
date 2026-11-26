## Purpose

Defines the owner-selected Counter + Preparation mobile release against the shared Windows backend. The 26 November 2026 scope decision is recorded in `docs/mobile-scope-206.md`; reviewer and current Android acceptance remain separate gates.

## ADDED Requirements

### Requirement: Shared authoritative backend

The staff mobile application SHALL use the coffee-shop ASP.NET Core API as its only business-data interface and SHALL present state from the same system of record used by the final Windows client.

#### Scenario: Mobile update appears to desktop users

- **WHEN** authorized mobile staff successfully change a selected Counter order/payment/adjustment or Preparation state
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

The application SHALL derive Counter and Preparation availability and their actions from effective permissions, regardless of role name, and MUST treat API authorization as authoritative. Today and More/Account SHALL remain the personal navigation surfaces. More SHALL contain Account only. A role without a selected operational permission SHALL NOT receive a retired workspace.

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

### Requirement: Counter sales and payment workflow

Authorized staff SHALL be able to search the menu, choose active sizes and compatible add-ons, maintain independent cart lines, choose takeaway/dine-in/delivery, select an existing customer and saved address, save a draft and review its authoritative snapshots and total before confirming it. Counter SHALL provide order search, fulfillment state and supported payments. Table selection SHALL show available dine-in tables within the ticket form; a standalone Tables/occupancy overview is excluded.

#### Scenario: Staff saves and confirms a ticket

- **WHEN** authorized staff save a valid cart and confirm the reviewed server total
- **THEN** the draft is saved through the shared API and confirmation applies the existing stock/table rules before the order appears in Preparation

#### Scenario: Saved total changes during review

- **WHEN** another client changes the total or a discount awaits a decision
- **THEN** confirmation is blocked until staff refresh and review the current authoritative ticket

#### Scenario: Save response is lost

- **WHEN** the outcome of a draft save is unknown
- **THEN** the app retains the same immutable request and identifier for read-back or explicit retry and does not allow a second edited ticket to stand in for that uncertain request

#### Scenario: Cashier records a payment

- **WHEN** authorized staff record a valid payment for an eligible order
- **THEN** the server persists the payment once and the application displays the updated paid amount and balance

### Requirement: Counter customer and adjustment workflow

Authorized staff SHALL be able to select an existing active customer and saved address, request a discount and redeem loyalty points on an eligible saved draft. Each adjustment SHALL require explicit review, effective permission checks and a server-confirmed result. Customer/address creation, editing, deactivation and loyalty-settings administration are excluded.

#### Scenario: Staff requests a discount

- **WHEN** authorized staff submit a reviewed discount request with a reason
- **THEN** the app displays the server request state without claiming approval; the manager decision is made in WinUI and the mobile ticket is refreshed before confirmation

#### Scenario: Staff edits a delivery address

- **WHEN** staff manually change an existing customer's selected saved address
- **THEN** the submitted delivery details no longer claim the saved-address identifier

#### Scenario: Adjustment review is canceled

- **WHEN** staff cancel a discount or loyalty review
- **THEN** no adjustment is sent and no authoritative state changes

### Requirement: Manager Counter oversight workflow

Authorized managers SHALL be able to cancel eligible orders with a required reason and assign ready unassigned delivery orders to eligible staff from Counter order details. Delivery is a Counter service mode; this requirement does not include courier execution screens.

#### Scenario: Manager assigns a ready delivery

- **WHEN** an authorized manager selects eligible staff and confirms an assignment for a ready unassigned delivery
- **THEN** the server confirms the assignment and records the actor without allowing stale eligibility or state to overwrite newer data

### Requirement: Retired mobile workflows

The current mobile release SHALL NOT expose discount approval/rejection, inventory/low-stock/adjustment/movement, purchase-receipt, courier execution or staff-administration workspaces. Unsupported direct and notification links SHALL show an authorization-safe unavailable result without fetching or revealing retired records. WinUI and shared backend capabilities remain unchanged.

#### Scenario: Staff opens a retired route

- **WHEN** any staff account opens a retired mobile workspace or notification destination
- **THEN** the app displays an unavailable result rather than exposing the retired record or advertising a missing mobile action

### Requirement: Device notification lifecycle

The app SHALL register and rotate device tokens for the authenticated owner, attempt removal on logout, handle notification permission denial without blocking core workflows and support invalid-token cleanup through the existing backend.

#### Scenario: Notification permission is denied

- **WHEN** staff deny notification permission
- **THEN** Counter and Preparation remain usable and the app does not claim that push delivery is configured

### Requirement: Near-real-time operational updates

While connected, the application SHALL receive authenticated operational updates relevant to the current user's permissions and active role workflows, and SHALL provide manual refresh when live delivery is unavailable.

#### Scenario: Order changes on another client

- **WHEN** an authorized Windows or mobile user changes an order represented on the current mobile screen
- **THEN** the application refreshes the affected view without requiring the user to sign in again

#### Scenario: Live connection is interrupted

- **WHEN** the real-time connection becomes unavailable
- **THEN** the application communicates the degraded state and allows the user to request a current server snapshot

### Requirement: Actionable notifications

The system SHALL deliver permission-filtered notifications for urgent Preparation work and SHALL deep-link an authenticated recipient to the relevant record without exposing sensitive customer details in the notification preview.

#### Scenario: Staff receives an urgent Preparation notification

- **WHEN** an eligible staff member with a registered device opens an urgent-order notification
- **THEN** the app authenticates the user, checks current Preparation access and opens the referenced ticket without exposing customer, address, payment or credential data in the preview

#### Scenario: Unauthorized recipient opens an old notification

- **WHEN** a user opens a notification after losing permission to the referenced record
- **THEN** the application does not display the protected record and communicates that access is unavailable

### Requirement: Online-first and safe degraded behavior

State-changing mobile actions MUST require a confirmed API response. When connectivity is lost, the application MAY continue showing data already loaded in the current session but MUST disable or reject writes rather than silently queueing them for later synchronization.

#### Scenario: Staff attempts a write while offline

- **WHEN** a staff member attempts a state-changing action without backend connectivity
- **THEN** the application makes no local success claim, does not queue the write, and instructs the user to reconnect and retry

### Requirement: Mobile privacy and local-data protection

The application MUST store session secrets using platform-protected storage, MUST minimize persisted business data, and MUST remove delivery contact and address information when it is no longer required for the current Counter ticket or authenticated session.

#### Scenario: Staff member logs out

- **WHEN** a staff member logs out
- **THEN** the application removes tokens and locally retained protected business data before returning to the login screen

### Requirement: Accessible responsive interface

The application SHALL provide portrait-first phone layouts and responsive tablet layouts with readable contrast, scalable text, screen-reader labels, visible focus or selection states, and touch targets of at least 48 device-independent pixels for primary actions.

#### Scenario: Staff uses enlarged text

- **WHEN** the operating system text scale is increased
- **THEN** operational content and primary actions remain readable and usable without hiding required information

### Requirement: Delivery traceability and evidence

The team SHALL maintain a release-facing function list that labels each mobile capability as implemented, planned, or out of scope and SHALL trace every implemented P0 capability to its API contract, automated verification, GitHub work item, and mobile demonstration segment. The application MUST NOT present a planned or unverified capability as implemented.

#### Scenario: P0 capability is claimed in the final demonstration

- **WHEN** the team records a P0 mobile workflow for the final demonstration
- **THEN** the function list identifies the responsible role, required API contract, automated evidence, and exact video segment for that workflow

#### Scenario: Retired or unverified capability appears in documentation

- **WHEN** a capability is excluded by the selected scope or lacks current acceptance evidence
- **THEN** the function list labels the excluded capability Out of Scope and the unverified selected capability Planned, preserves historical evidence revisions and does not claim current acceptance or reviewer approval
