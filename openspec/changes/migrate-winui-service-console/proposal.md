# Proposal

## Why

The WinUI client exposes the required business workflows through a fragmented, form-heavy interface whose navigation and visual hierarchy no longer match the approved service-console concepts in `docs/ui-concepts`. Migrating to the new task-oriented console will make daily cashier, fulfillment, catalog, inventory, customer, and reporting work faster to understand while preserving the existing business behavior.

## What Changes

- Replace the current eleven-entry technical navigation with six staff workspaces: Counter, Service board, Menu, Stock room, Customers, and Reports.
- Introduce a shared Corner Cafe shell with consistent branding, connection status, workspace navigation, operator context, responsive layout behavior, keyboard focus treatment, and reusable visual components derived from `docs/ui-concepts`.
- Recompose the existing point-of-sale and fulfillment experiences into the Counter and Service board layouts without changing order, payment, discount, cancellation, delivery, or inventory business rules.
- Consolidate ingredients, low stock, purchase receipts, suppliers, adjustments, and movement history into the tabbed Stock room workspace.
- Recompose the existing catalog, customer, and reporting experiences into the list/detail and dashboard layouts shown by the concepts.
- Preserve all currently supported workflows and actionable loading, empty, success, validation, and failure states during the migration.
- Adapt the fixed 1440 by 900 visual concepts into accessible Compact, Medium, and Expanded WinUI layouts with keyboard operation and minimum touch targets.
- Exclude concept-only capabilities that lack existing application support, including federated global search, notifications, persisted menu pictures, report export, and authenticated shift management; their controls SHALL not appear as non-functional affordances.

## Capabilities

### New Capabilities

- `staff-service-console`: Defines the task-oriented WinUI shell, six staff workspaces, responsive behavior, accessible interaction, and preservation of existing workflow feedback during the UI migration.

### Modified Capabilities

None.

## Impact

- Primarily affects `src/CoffeeShop.WinUI`, including `MainWindow`, page composition, presentation models, shared theme resources, and reusable controls.
- Reuses the existing application services, domain rules, PostgreSQL adapters, and midterm direct-database composition path without contract or schema changes.
- Requires updates to WinUI-focused tests for navigation mapping, workspace presentation state, responsive composition, and accessible interaction.
- Uses the HTML, CSS, images, icons, and typography under `docs/ui-concepts` as the visual reference while translating browser-specific effects into supported WinUI techniques.
