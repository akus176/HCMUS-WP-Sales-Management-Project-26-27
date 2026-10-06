## Purpose

Defines optional customer profiles, delivery contact information, loyalty balances, earning, redemption, and reversals connected to completed sales.

## ADDED Requirements

### Requirement: Optional customer profiles
The system SHALL allow walk-in orders without a customer profile and SHALL allow staff to create or attach a customer profile when identification is useful.

#### Scenario: Create a walk-in takeaway order
- **WHEN** staff create a takeaway order without selecting a customer
- **THEN** the order can proceed without a customer profile

### Requirement: Customer profile management
The system SHALL allow authorized staff to create, update, search, and deactivate customer profiles containing contact information while preserving their order history.

#### Scenario: Deactivate a customer
- **WHEN** authorized staff deactivate a customer profile
- **THEN** the profile is excluded from normal selection but remains linked to historical orders

### Requirement: Delivery information
A delivery order MUST contain a recipient name, contact number, and delivery address, whether stored on a customer profile or entered for that order only.

#### Scenario: Use an order-specific address
- **WHEN** staff enter a valid address for a delivery order without saving it to the customer profile
- **THEN** the address is stored on the order and can be used for fulfillment

### Requirement: Loyalty earning
The system SHALL award loyalty points to an attached active customer only after an eligible order is both fully paid and completed, using a configurable earning rule.

#### Scenario: Complete an eligible customer order
- **WHEN** a fully paid eligible order attached to an active customer is completed
- **THEN** the calculated loyalty points are added once and the earning transaction references the order

### Requirement: Loyalty redemption
The system SHALL allow eligible customers to redeem available loyalty points toward an order according to a configurable conversion rule, and MUST prevent redemption above the available balance or allowed order value.

#### Scenario: Redeem available points
- **WHEN** staff apply a valid number of available points to an eligible order
- **THEN** the payable total is reduced according to the conversion rule and the redemption is recorded

### Requirement: Loyalty reversal
The system MUST reverse loyalty points earned or redeemed when the associated transaction is cancelled or refunded in a way that invalidates those points.

#### Scenario: Cancel an order that earned points
- **WHEN** an order is cancelled after loyalty points were awarded
- **THEN** the system records a reversing loyalty transaction and updates the customer's balance without deleting history

#### Scenario: Cancel an order that redeemed points
- **WHEN** an order is cancelled after loyalty points were redeemed against it
- **THEN** the system restores the redeemed points to the customer's balance through a reversing loyalty transaction

#### Scenario: Reject a reversal that would overdraw the balance
- **WHEN** reversing earned points would make the customer's balance negative because those points have already been spent
- **THEN** the system returns a conflict and leaves the balance and transaction history unchanged

#### Scenario: Retry a completed reversal
- **WHEN** a reversal is requested again for the same original transaction
- **THEN** the system returns the existing reversal without appending another entry or changing the balance again
