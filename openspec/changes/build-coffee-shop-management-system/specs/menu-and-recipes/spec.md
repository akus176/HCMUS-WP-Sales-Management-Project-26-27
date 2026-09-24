## Purpose

Defines the coffee-shop menu, purchasable variants, add-ons, prices, availability, and ingredient recipes used by ordering and inventory workflows.

## ADDED Requirements

### Requirement: Menu catalog management
The system SHALL allow authorized staff to create, update, deactivate, and search menu categories and menu items without deleting historical order data.

#### Scenario: Deactivate an existing drink
- **WHEN** authorized staff deactivate a menu item
- **THEN** the item is unavailable for new orders but remains visible in historical orders

### Requirement: Size variants and pricing
Each sellable menu item SHALL have one or more size variants, and each variant SHALL define its own selling price and availability.

#### Scenario: Select a drink size
- **WHEN** a cashier selects an available size for a menu item
- **THEN** the order line uses the price configured for that exact item and size

### Requirement: Simple add-ons
The system SHALL allow authorized staff to define optional add-ons with independent prices and availability, and SHALL allow compatible add-ons to be attached to an order line.

#### Scenario: Add an available topping
- **WHEN** a cashier adds an available compatible add-on to a drink
- **THEN** the add-on is recorded on the order line and its price is included in the line total

### Requirement: Size-specific recipes
Each menu-item size variant SHALL support a recipe containing ingredient quantities and units required to produce one unit of that variant.

#### Scenario: View a large drink recipe
- **WHEN** authorized staff view the recipe for a large drink
- **THEN** the system displays the ingredient quantities configured specifically for the large variant

### Requirement: Recipe validation
The system MUST reject recipes containing missing ingredients, zero or negative quantities, or incompatible measurement units.

#### Scenario: Reject an invalid recipe quantity
- **WHEN** authorized staff attempt to save a recipe ingredient with a quantity less than or equal to zero
- **THEN** the recipe is not saved and a validation error identifies the invalid quantity

### Requirement: Menu availability
The system SHALL prevent unavailable menu items, size variants, and add-ons from being added to new orders.

#### Scenario: Attempt to order an unavailable size
- **WHEN** a cashier selects a size variant marked unavailable
- **THEN** the system rejects the selection and does not add it to the order
