## Why

The project needs a complete coffee-shop management and point-of-sale system that can demonstrate realistic sales, inventory, customer, delivery, reporting, and staff workflows. Building it in two milestones allows the team to prove the business functions with direct PostgreSQL access first, then evolve the same system into a secure API-based architecture for the final assessment.

## What Changes

- Create a WinUI 3 desktop application for dine-in, takeaway, and internally delivered coffee-shop orders.
- Add menu categories, drinks, size variants, add-ons, and size-specific ingredient recipes.
- Add ingredient inventory, suppliers, purchase receipts, stock adjustments, low-stock detection, and auditable stock movements.
- Deduct recipe ingredients when an order is confirmed and restore them when a confirmed order is cancelled.
- Add table occupancy, payment recording, manager-approved discounts, order preparation, and delivery assignment workflows.
- Add optional customer profiles, loyalty points, and delivery addresses.
- Add revenue, best-seller, order-channel, and low-stock reporting.
- Deliver the midterm milestone as a WinUI 3 client connecting directly to PostgreSQL, with all planned business functions except API-based authentication and role authorization.
- Evolve the final milestone to an ASP.NET Core Web API with JWT access/refresh tokens and server-enforced role-based access for admins, managers, cashiers, baristas, inventory staff, and delivery staff.
- Add a permission-aware, read-only AI manager assistant that answers questions through reporting APIs rather than inventing or directly accessing data.
- Add unit tests, integration tests, and an API test collection, with PostgreSQL and the final API runnable through local Docker services.

## Capabilities

### New Capabilities

- `menu-and-recipes`: Manage menu categories, items, size variants, add-ons, ingredient recipes, pricing, and availability.
- `sales-and-fulfillment`: Create and manage dine-in, takeaway, and delivery orders, including tables, preparation, payments, discounts, cancellation, and delivery assignment.
- `inventory-and-procurement`: Track ingredients, suppliers, purchase receipts, adjustments, low-stock conditions, recipe consumption, restoration, and stock history.
- `customers-and-loyalty`: Manage optional customer profiles, delivery information, loyalty earning, and loyalty redemption.
- `identity-and-access`: Authenticate users and enforce role-based permissions for all staff roles in the final API architecture.
- `reporting-and-ai`: Produce operational reports and expose permission-aware reporting tools to a read-only AI manager assistant.

### Modified Capabilities

None.

## Impact

- Introduces a new WinUI 3 C# client, PostgreSQL schema, data-access layer, and ASP.NET Core Web API.
- Requires PostgreSQL connectivity for the midterm architecture and HTTP API integration for the final architecture.
- Introduces JWT authentication, refresh-token handling, role/permission policies, transactional inventory updates, and reporting endpoints.
- Introduces local Docker configuration for PostgreSQL and the final API.
- Establishes shared domain contracts that five contributors must follow across UI, database, API, testing, and AI integration work.
