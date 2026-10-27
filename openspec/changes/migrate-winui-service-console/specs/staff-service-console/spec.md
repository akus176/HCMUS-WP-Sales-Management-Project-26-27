# Spec Delta

## Purpose

Provides staff with a cohesive, task-oriented desktop console that presents existing coffee-shop workflows through consistent, responsive, and accessible workspaces.

## ADDED Requirements

### Requirement: Task-oriented application shell
The system SHALL present the staff client as one Corner Cafe service console with primary navigation for Counter, Service board, Menu, Stock room, Customers, and Reports.

#### Scenario: Staff opens the connected client
- **WHEN** the client completes its connection check successfully
- **THEN** the Counter workspace is available as the default workspace
- **AND** all six primary workspaces are reachable from the shared navigation

#### Scenario: Database connection is unavailable
- **WHEN** the client cannot establish its required data connection
- **THEN** the shell displays an actionable connection failure and retry action
- **AND** prevents staff from entering data-dependent workspaces until connection succeeds

### Requirement: Counter workspace
The Counter workspace SHALL preserve the supported order-entry workflow while presenting menu discovery and the current ticket together when space permits.

#### Scenario: Cashier builds an order on an expanded display
- **WHEN** a cashier opens Counter at the Expanded breakpoint
- **THEN** available menu items and the current ticket are visible in the same workspace
- **AND** the cashier can perform every currently supported draft, customer, fulfillment-channel, discount, loyalty, and confirmation action

#### Scenario: Cashier uses a smaller display
- **WHEN** Counter is displayed below the Expanded breakpoint
- **THEN** the menu and ticket remain reachable without horizontal page scrolling
- **AND** no supported order action is removed

### Requirement: Service board workspace
The Service board SHALL present active fulfillment work by operational state and provide contextual actions only when the selected order permits them.

#### Scenario: Staff reviews active work
- **WHEN** active orders are loaded
- **THEN** confirmed, preparing, and ready work is visually distinguishable
- **AND** selecting an order reveals its details and valid fulfillment, payment, discount, cancellation, table, or delivery actions

#### Scenario: No active work exists
- **WHEN** no active orders are available
- **THEN** the workspace shows a clear empty state without displaying misleading queue counts or enabled order actions

### Requirement: Menu workspace
The Menu workspace SHALL provide Drinks and recipes, Categories, and Add-ons views with searchable list/detail interaction and all currently supported catalog lifecycle actions.

#### Scenario: Manager selects a drink
- **WHEN** a manager selects a drink from the filtered catalog
- **THEN** the workspace displays its category, description, availability, sizes, prices, recipes, and compatible add-ons that are available from existing data
- **AND** exposes the currently supported edit and lifecycle actions

#### Scenario: Catalog search has no matches
- **WHEN** no drinks match the entered search and availability filter
- **THEN** the workspace explains how to clear the filter or create a drink
- **AND** does not present a stale drink detail as the current selection

### Requirement: Consolidated Stock room workspace
The Stock room SHALL consolidate Balances, Low stock, Receipts, Suppliers, Adjustments, and Movements under one workspace without removing existing inventory and procurement actions.

#### Scenario: Inventory staff changes stock-room function
- **WHEN** inventory staff chooses a Stock room section
- **THEN** the selected section is displayed within the Stock room workspace
- **AND** the staff member can complete the same supported workflow previously available from the corresponding standalone page

#### Scenario: Ingredient is selected for adjustment
- **WHEN** inventory staff selects an ingredient from Balances and starts an adjustment
- **THEN** its current balance, base unit, recent movements, required reason, and projected balance are presented together before submission
- **AND** the adjustment remains subject to existing validation and audit behavior

### Requirement: Customers workspace
The Customers workspace SHALL combine customer search, selected profile details, saved addresses, loyalty history, recent orders, and next-order selection in one coherent workspace.

#### Scenario: Staff selects an active customer
- **WHEN** staff selects an active customer from search results
- **THEN** the profile and its available contact, address, loyalty, and recent-order information are shown
- **AND** the customer can be selected for the next order using existing customer-order rules

#### Scenario: Staff continues without a profile
- **WHEN** staff chooses the walk-in option
- **THEN** the next-order context is cleared of the selected customer
- **AND** the workspace confirms that the next order will be a walk-in

### Requirement: Reports workspace
The Reports workspace SHALL present existing revenue, best-seller, order-channel, and low-stock results as a unified dashboard for the selected reporting period.

#### Scenario: Report data is available
- **WHEN** a valid reporting period is loaded
- **THEN** the dashboard displays the existing report measures with consistent period context
- **AND** excludes or includes records according to the existing reporting rules

#### Scenario: A report has no matching data
- **WHEN** one report contains no matching records
- **THEN** that report displays a local explanatory empty state
- **AND** other available report results remain visible

### Requirement: Responsive workspace composition
The console SHALL support Compact widths below 641 pixels, Medium widths from 641 through 1007 pixels, and Expanded widths of at least 1008 pixels without losing supported behavior.

#### Scenario: Window crosses a layout breakpoint
- **WHEN** the window width crosses a supported breakpoint
- **THEN** navigation, columns, detail panes, and action regions reflow to the layout defined for the new width
- **AND** entered values and the current workspace selection are preserved

#### Scenario: Content exceeds available space
- **WHEN** workspace content cannot fit in the available viewport
- **THEN** content scrolls in the intended region without clipping primary actions or requiring horizontal page scrolling

### Requirement: Accessible staff interaction
The console SHALL retain visible keyboard focus, meaningful control names, readable contrast, and touch targets of at least 48 device-independent pixels for primary interactive controls.

#### Scenario: Staff navigates with a keyboard
- **WHEN** staff traverses navigation, filters, lists, detail panes, and actions using the keyboard
- **THEN** focus order follows the visible workflow
- **AND** the focused control has a visible focus indicator

#### Scenario: Assistive technology identifies a control
- **WHEN** an icon-only or visually abbreviated control receives accessibility inspection
- **THEN** it exposes a meaningful accessible name describing its action or state

### Requirement: Consistent workflow feedback
Every workspace SHALL communicate loading, empty, success, validation, conflict, and unexpected failure states near the affected workflow without discarding the staff member's recoverable context.

#### Scenario: A recoverable command fails
- **WHEN** an operation returns validation, conflict, or transient failure feedback
- **THEN** the workspace explains the outcome and available recovery action
- **AND** preserves valid selections and entered values that can be retried

#### Scenario: A command succeeds
- **WHEN** a staff action completes successfully
- **THEN** the affected workspace refreshes the relevant presentation state
- **AND** communicates completion without requiring navigation to another technical page

### Requirement: Supported affordances only
The migrated console SHALL display an actionable control only when the client provides the behavior required to complete that action.

#### Scenario: Concept-only capability is unavailable
- **WHEN** a concept includes federated search, notifications, persisted menu pictures, report export, or authenticated shift management that the client does not support
- **THEN** the migrated production workspace omits the corresponding action
- **AND** no inert or simulated control is presented to staff
