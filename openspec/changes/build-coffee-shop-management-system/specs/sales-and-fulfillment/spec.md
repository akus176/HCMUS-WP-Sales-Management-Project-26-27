## Purpose

Defines observable behavior for dine-in, takeaway, and delivery sales from order entry through preparation, payment, completion, cancellation, and delivery fulfillment.

## ADDED Requirements

### Requirement: Multi-channel order creation
The system SHALL allow staff to create dine-in, takeaway, and delivery orders, and SHALL require channel-specific information before confirmation.

#### Scenario: Confirm a dine-in order
- **WHEN** staff confirm a dine-in order with at least one item and an available table
- **THEN** the system records the order as dine-in and associates it with that table

#### Scenario: Reject incomplete delivery order
- **WHEN** staff attempt to confirm a delivery order without a delivery address
- **THEN** the system rejects confirmation and identifies the missing delivery information

### Requirement: Order lifecycle
The system SHALL maintain controlled order states for draft, confirmed, preparing, ready, completed, cancelled, and delivery-specific fulfillment where applicable.

#### Scenario: Start preparing a confirmed order
- **WHEN** a barista starts a confirmed order
- **THEN** the order transitions to preparing and records when preparation began

#### Scenario: Reject an invalid transition
- **WHEN** a user attempts a transition not allowed from the current order state
- **THEN** the system rejects the transition without changing the order

### Requirement: Table occupancy
The system SHALL track whether each table is available or occupied and SHALL release an occupied table when its active dine-in order is completed or cancelled.

#### Scenario: Occupy a table
- **WHEN** a dine-in order is confirmed for an available table
- **THEN** the table becomes occupied and cannot be assigned to another active order

### Requirement: Payments
The system SHALL record cash, card, and e-wallet payments, including amount, method, status, and payment time, and MUST prevent a completed order from having an unpaid balance.

#### Scenario: Complete a fully paid order
- **WHEN** recorded successful payments cover the order total
- **THEN** authorized staff can complete the order and the system records it as fully paid

### Requirement: Controlled discounts
The system SHALL support percentage and fixed-amount discounts and MUST require manager approval before an unapproved discount affects the payable total.

#### Scenario: Cashier requests a discount
- **WHEN** a cashier applies a fixed or percentage discount
- **THEN** the discount remains pending until a manager approves or rejects it

#### Scenario: Prevent excessive discount
- **WHEN** a requested discount would reduce the payable subtotal below zero
- **THEN** the system rejects the discount

### Requirement: Order cancellation
Authorized staff SHALL be able to cancel a confirmed order with a reason, and cancellation MUST trigger restoration of inventory previously consumed by that order.

#### Scenario: Cancel a confirmed order
- **WHEN** authorized staff cancel a confirmed order and provide a reason
- **THEN** the order becomes cancelled, cannot continue fulfillment, and requests inventory restoration exactly once

### Requirement: Internal delivery fulfillment
The system SHALL allow managers or authorized staff to assign a confirmed delivery order to an active delivery staff member and track ready, out-for-delivery, delivered, and failed-delivery outcomes.

#### Scenario: Assign a delivery order
- **WHEN** an authorized user assigns a ready delivery order to active delivery staff
- **THEN** the assignment is recorded and the delivery staff member can update its delivery status

### Requirement: Immutable order pricing snapshot
Confirmed orders MUST retain the item names, variant names, quantities, unit prices, add-on prices, and approved discounts used at confirmation even if the menu changes later.

#### Scenario: Change menu price after confirmation
- **WHEN** an item price changes after an order was confirmed
- **THEN** the confirmed order total and line-item pricing remain unchanged
