## Context

The repository is currently an empty Git project with a newly initialized OpenSpec root. See `proposal.md` for motivation and the capability specs for observable behavior.

The system must support two architectural milestones without discarding the first implementation:

1. Midterm: WinUI 3 executes the complete business workflows through direct PostgreSQL access, except API authentication and role authorization.
2. Final: WinUI 3 no longer accesses PostgreSQL and instead uses an ASP.NET Core Web API that owns business logic, authentication, authorization, and persistence.

The implementation is shared by six contributors, so module ownership, stable contracts, migrations, and integration tests are design constraints. The final environment must run PostgreSQL and the API locally with Docker.

## Goals / Non-Goals

**Goals:**

- Preserve core domain and business logic when moving from direct database access to the API architecture.
- Keep inventory deduction, restoration, payment, loyalty, and order transitions transactionally consistent.
- Organize the solution as independently understandable business modules while keeping deployment simple.
- Establish permission enforcement that cannot be bypassed by calling the API directly.
- Make reports and AI tools use the same authorized application services as normal clients.
- Support parallel development by six contributors through explicit project boundaries and testable contracts.

**Non-Goals:**

- Microservices or independently deployed business services.
- Public customer self-service ordering, online payment-gateway settlement, or third-party delivery integration.
- Multi-branch or franchise management.
- Offline synchronization between multiple clients.
- Table reservations, accounting integration, payroll, or employee scheduling.
- AI operations that modify business data.
- Production cloud hosting as part of the initial delivery.

## Decisions

### 1. Use a modular monolith with shared domain and application projects

The solution will separate domain rules, application use cases, infrastructure, WinUI presentation, API hosting, and tests while deploying one API service.

```text
CoffeeShop.Domain
        ↑
CoffeeShop.Application
        ↑
CoffeeShop.Infrastructure.Postgres
        ↑                    ↑
CoffeeShop.WinUI.Direct      CoffeeShop.Api
        │                    ↑
        └── midterm          CoffeeShop.WinUI.ApiClient ── final
```

Recommended solution projects:

- `CoffeeShop.Domain`: entities, value objects, domain rules, and state transitions.
- `CoffeeShop.Application`: use cases, commands/queries, validation, authorization abstractions, and transaction boundaries.
- `CoffeeShop.Infrastructure.Postgres`: PostgreSQL persistence, migrations, repositories, and external provider adapters.
- `CoffeeShop.Contracts`: API request/response contracts shared without exposing persistence entities.
- `CoffeeShop.WinUI`: views, view models, client-side state, and milestone-specific service adapters.
- `CoffeeShop.Api`: HTTP endpoints, JWT authentication, permission policies, and application hosting.
- Separate unit, integration, API, and UI-focused test projects.

This preserves one set of use cases across both milestones. The midterm WinUI composition root hosts the application layer and PostgreSQL infrastructure in-process. In the final milestone, the API becomes the composition root and WinUI replaces direct application calls with an HTTP client.

Alternative considered: build the midterm directly inside WinUI and rewrite logic in the API later. Rejected because it duplicates business rules, increases migration risk, and makes inconsistent inventory behavior likely.

### 2. Use PostgreSQL with EF Core and Npgsql migrations

PostgreSQL is the system of record. EF Core with the Npgsql provider will manage persistence mappings and versioned schema migrations. Database constraints will protect required relationships, unique identifiers, valid numeric ranges, and referential integrity in addition to application validation.

Alternative considered: handwritten SQL for all persistence. Rejected as the default because it increases repetitive mapping work across a large domain, though targeted SQL may be used for reporting or concurrency-sensitive operations when measured benefits justify it.

### 3. Model the database around business modules with immutable transaction history

Core aggregate areas are:

- Catalog: categories, menu items, size variants, add-ons, recipes, recipe ingredients.
- Sales: orders, order lines, line add-ons, discounts, payments, tables, deliveries, status history.
- Inventory: ingredients, suppliers, receipts, receipt lines, stock movements.
- Customers: customer profiles, addresses, loyalty transactions.
- Identity: users, roles, permissions, user-role assignments, refresh tokens, security audit events.

Confirmed order lines store snapshots of names and monetary values instead of resolving current menu prices later. Inventory balances may be cached for efficient reads, but the stock-movement ledger is the audit source for every quantity change. Loyalty uses an append-only transaction ledger for the same reason.

Alternative considered: recalculate historical orders from current catalog data. Rejected because later menu changes would corrupt historical totals and reports.

### 4. Treat order confirmation as the inventory transaction boundary

Order confirmation will execute one database transaction that:

1. Validates the order and current menu availability.
2. Resolves size-specific recipes and add-ons.
3. Locks or concurrency-checks affected ingredient balances in a deterministic order.
4. Verifies all required quantities.
5. Records order pricing snapshots.
6. Writes stock movements and updates balances.
7. Changes the order to confirmed.

If any ingredient is insufficient, the complete transaction rolls back. Cancellation creates compensating stock movements tied to the original deductions. A unique restoration reference prevents duplicate restoration.

PostgreSQL row-level locking or concurrency tokens will protect against simultaneous confirmations overselling the same ingredient. The specific mechanism should be selected through an integration test that confirms correctness under concurrent requests.

Alternative considered: deduct each ingredient separately and compensate on failure. Rejected because partial deductions are harder to reason about and recover from.

### 5. Use explicit state machines for orders, tables, delivery, discounts, and payments

Allowed transitions will be centralized in the domain/application layer rather than inferred by UI screens. Each transition records actor and time. The API and direct midterm adapter invoke the same use cases.

Examples:

```text
Draft → Confirmed → Preparing → Ready → Completed
                  ↘                     ↗
                    Cancelled

Delivery: Unassigned → Assigned → OutForDelivery → Delivered
                                      └────────────→ Failed
```

Dine-in table occupancy is derived from an active dine-in order assignment but persisted with concurrency protection to prevent double assignment.

Alternative considered: allow arbitrary status updates and validate only in the UI. Rejected because direct database or API calls could bypass workflow rules.

### 6. Represent money and quantities with explicit decimal precision

Monetary values use decimal types and a single configured currency. Ingredient quantities use decimal values in a configured base unit per ingredient. Recipes and receipts must convert to the ingredient's base unit before changing stock.

Floating-point types will not be used for money or measured inventory.

### 7. Use permission-based authorization backed by operational roles

The API will authorize named permissions such as `orders.create`, `orders.cancel`, `discounts.approve`, `inventory.receive`, `reports.revenue`, and `users.manage`. The six required roles provide default permission bundles:

- Admin
- Manager
- Cashier
- Barista
- Inventory staff
- Delivery staff

Endpoints check permissions, not hard-coded role names, allowing policy changes without rewriting each endpoint. The WinUI client may hide unauthorized controls for usability, but the API remains authoritative.

Alternative considered: role checks embedded directly in controllers. Rejected because they become duplicated, inflexible, and difficult to audit.

### 8. Use short-lived JWT access tokens and rotated refresh tokens

Access tokens will be short-lived and carry a user identifier plus minimal authorization claims. Refresh tokens will be random opaque values stored as hashes, rotated on each successful renewal, and revocable by logout, account deactivation, password reset, or detected reuse.

The desktop client stores tokens using Windows-protected credential storage rather than plaintext configuration files.

Alternative considered: long-lived access tokens without refresh tokens. Rejected because token theft would create an unnecessarily long exposure period.

### 9. Keep API contracts separate from persistence models

The API will expose versionable request and response contracts and map them to application commands, queries, and results. Persistence entities will never be serialized directly. Validation errors, authentication failures, authorization failures, conflicts, and not-found results will use consistent HTTP statuses and error bodies.

This allows database schema changes without automatically breaking the WinUI client.

### 10. Implement reporting as read models over authoritative transactional data

Operational reports use application queries with explicit date-range and permission inputs. Straightforward reports can use EF Core projections; complex aggregates may use parameterized SQL or database views. Cancelled orders are excluded and revenue is based on successful payments according to one documented rule.

All reporting queries must be testable against seeded PostgreSQL data, including date-boundary and time-zone cases.

### 11. Put the AI assistant behind a read-only tool gateway

The assistant receives the authenticated user's identity and can invoke only an allowlist of reporting tools. Tool handlers call the same permission-aware reporting application queries used by normal endpoints. No write command, database connection, or unrestricted SQL tool is exposed.

The AI provider is hidden behind an interface so development can use a configured provider or a deterministic fake in tests. Tool inputs and outputs use structured schemas. Interaction audits record tool names and outcomes but exclude secrets and access tokens.

Alternative considered: allow the model to generate SQL. Rejected because it creates authorization, data-leakage, correctness, and auditability risks.

### 12. Use Docker Compose for final local infrastructure

Docker Compose will run PostgreSQL and the ASP.NET Core API with health checks, named volumes, and environment-based configuration. The WinUI application remains a Windows process outside containers. Secrets are supplied through ignored local environment files or secret storage, never committed.

The midterm may run the same containerized PostgreSQL instance while WinUI connects directly to it.

### 13. Organize team work by vertical business modules and shared contracts

Each module owner is responsible for its schema changes, application use cases, UI/API integration, and tests. Cross-cutting contracts—money, errors, identity, auditing, migrations, and CI—require review by at least one contributor outside the owning module.

Schema migrations are ordered, committed individually with their feature, and validated from an empty database in CI to reduce six-person integration conflicts.

## Risks / Trade-offs

- **[Midterm direct database credentials exist on the client]** → Use a restricted PostgreSQL account, local-only networking, environment-based configuration, and remove all direct database access from the final client.
- **[Business logic accidentally remains in WinUI]** → Put state transitions and transactional workflows in Domain/Application projects from the first milestone; keep view models limited to presentation orchestration.
- **[Concurrent orders oversell ingredients]** → Use database transactions and row locking or tested concurrency control, with dedicated concurrent integration tests.
- **[Recipe unit inconsistencies corrupt stock]** → Require a base unit per ingredient and centralize validated conversion before persistence.
- **[Cancellation or retries duplicate stock and loyalty changes]** → Use idempotency keys or unique business references for compensating transactions.
- **[Six contributors create migration conflicts]** → Assign schema ownership, keep migrations small, rebase before generating migrations, and test rebuilding from an empty database.
- **[The midterm scope is unusually broad]** → Deliver thin end-to-end vertical slices first, then add secondary screens and refinements; avoid final-only API and RBAC work until core flows are stable.
- **[AI produces unsupported conclusions]** → Restrict it to structured reporting tools, display source period/metrics, handle empty results explicitly, and test with deterministic tool responses.
- **[Reports disagree with operational totals]** → Define revenue, cancellation, discount, and loyalty accounting rules once and reuse shared queries with seeded integration tests.
- **[Refresh-token theft or reuse]** → Store only token hashes, rotate tokens, revoke token families on reuse detection, and audit security events.

## Migration Plan

1. Create the solution structure, domain model, PostgreSQL schema, migrations, and shared application use cases.
2. Build midterm WinUI screens against an in-process application host using the PostgreSQL infrastructure adapter.
3. Seed and verify the required demonstration data and complete midterm test cases.
4. Add API contracts and host the existing application use cases in ASP.NET Core.
5. Add identity data, JWT authentication, refresh-token rotation, permission policies, and API authorization tests.
6. Implement the WinUI HTTP adapter and move each module from direct application calls to API calls behind the same client-facing service interfaces.
7. Disable and remove the final WinUI composition path that references PostgreSQL infrastructure.
8. Add reporting endpoints, the read-only AI tool gateway, Docker Compose services, and final end-to-end verification.

Rollback during migration is module-based: until a module's API adapter passes parity tests, the team can switch that module's development configuration back to the direct adapter. After final acceptance, the direct adapter is removed from release builds and is not a supported production fallback.

## Open Questions

- The loyalty earning rate, redemption conversion, and maximum redeemable percentage remain configurable business values to finalize before demo-data seeding.
- Tax and service-charge rules are not currently required; if introduced, they should be represented as explicit order adjustments rather than hidden arithmetic.
- The selected AI provider and model can be chosen later because the assistant is isolated behind a provider interface and structured tool contract.
