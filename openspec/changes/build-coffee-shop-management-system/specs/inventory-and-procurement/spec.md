## Purpose

Defines ingredient stock, replenishment, recipe consumption, restoration, low-stock detection, suppliers, and auditable inventory movements.

## ADDED Requirements

### Requirement: Ingredient inventory
The system SHALL track each ingredient's name, base measurement unit, available quantity, reorder threshold, and active status.

#### Scenario: View an ingredient balance
- **WHEN** authorized staff open an active ingredient
- **THEN** the system displays its current available quantity in its configured base unit

### Requirement: Supplier management
The system SHALL allow authorized staff to create, update, deactivate, and search suppliers while preserving suppliers referenced by historical receipts.

#### Scenario: Deactivate a supplier
- **WHEN** authorized staff deactivate a supplier
- **THEN** the supplier cannot be selected for new receipts but remains on historical receipts

### Requirement: Purchase receipts
The system SHALL record supplier purchase receipts containing received ingredients, quantities, unit costs, receipt references, and receipt time, and SHALL increase stock only when a receipt is posted.

#### Scenario: Post a purchase receipt
- **WHEN** authorized inventory staff post a valid receipt
- **THEN** ingredient balances increase by the received quantities and corresponding stock movements are recorded

### Requirement: Stock adjustments
The system SHALL allow authorized inventory staff to record positive or negative stock adjustments with a mandatory reason and actor identity.

#### Scenario: Reject an unexplained adjustment
- **WHEN** inventory staff attempt to save a stock adjustment without a reason
- **THEN** the system rejects the adjustment and stock remains unchanged

### Requirement: Atomic recipe consumption
Confirming an order MUST atomically validate and deduct all ingredient quantities required by its size-specific recipes and add-ons.

#### Scenario: Confirm an order with sufficient inventory
- **WHEN** all ingredients required by the order are available
- **THEN** the system confirms the order and deducts every required ingredient as one successful operation

#### Scenario: Reject an order with insufficient inventory
- **WHEN** any required ingredient has insufficient available quantity
- **THEN** the system does not confirm the order, identifies the shortage, and does not deduct any ingredients

### Requirement: Idempotent inventory restoration
Cancelling a confirmed order SHALL restore exactly the ingredient quantities deducted for that order and MUST NOT restore them more than once.

#### Scenario: Repeat cancellation processing
- **WHEN** inventory restoration is requested again for an order already restored
- **THEN** no additional stock is added and the existing restoration remains recorded

### Requirement: Low-stock detection
The system SHALL identify active ingredients whose available quantity is at or below their reorder threshold.

#### Scenario: Ingredient reaches threshold
- **WHEN** an ingredient balance becomes equal to or less than its reorder threshold
- **THEN** the ingredient appears in the low-stock view and reporting data

### Requirement: Inventory movement history
Every stock-changing operation MUST produce an immutable movement record containing ingredient, quantity change, reason or source, time, actor, and related business record when applicable.

#### Scenario: Trace order consumption
- **WHEN** authorized staff inspect movements for an ingredient consumed by an order
- **THEN** the system shows the deduction linked to that order and the responsible operation
