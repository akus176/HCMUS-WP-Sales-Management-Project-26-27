## Context

The repository is currently an empty Git project with a newly initialized OpenSpec root. See `proposal.md` for motivation and the capability specs for observable behavior. Database schema and API endpoint design are detailed in their own sections below.

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

## Database Schema

PostgreSQL, EF Core + Npgsql. One physical database, one Postgres **schema per module**
(matches decision #13 above): `identity`, `catalog`, `inventory`, `sales`, `customers`, `reporting`.

### Conventions

- Primary keys: `uuid` (`gen_random_uuid()` via `pgcrypto`), except pure join tables which use composite PKs.
- `created_at` / `updated_at`: `timestamptz not null default now()`.
- Money: `numeric(12,2)`, one configured currency (decision #6 — never floating point).
- Quantities: `numeric(14,3)` in the ingredient's base unit.
- Enums are modeled as `varchar` + `check` constraint (not native Postgres enum) so allowed values can change without a type migration.
- Optimistic concurrency: EF Core `xmin` system column on rows that need it (`sales.tables`, `sales.orders`) — no extra column required.
- Every stock- or loyalty-changing table that must not double-apply has an explicit uniqueness guard (see Idempotency section).

### Schema: `identity`

#### `identity.users`
| Column | Type | Constraints |
|---|---|---|
| id | uuid | PK |
| username | varchar(50) | unique, not null |
| email | varchar(255) | unique, not null |
| password_hash | varchar(255) | not null |
| full_name | varchar(150) | not null |
| phone | varchar(30) | null |
| is_active | boolean | not null default true |
| created_at, updated_at | timestamptz | not null |
| deactivated_at | timestamptz | null |

#### `identity.roles`
`id uuid PK`, `code varchar(30) unique not null` (`Admin`,`Manager`,`Cashier`,`Barista`,`InventoryStaff`,`DeliveryStaff`), `name varchar(50)`, `description text`.

#### `identity.permissions`
`id uuid PK`, `code varchar(60) unique not null`, `description text`.

Seed codes: `orders.create`, `orders.confirm`, `orders.cancel`, `orders.complete`, `discounts.request`, `discounts.approve`, `payments.record`, `menu.manage`, `inventory.receive`, `inventory.adjust`, `inventory.manage`, `customers.manage`, `loyalty.redeem`, `delivery.assign`, `delivery.update`, `reports.revenue`, `reports.bestsellers`, `reports.channels`, `reports.lowstock`, `assistant.ask`, `users.manage`.

#### `identity.role_permissions`
`(role_id, permission_id)` composite PK, both FK with `on delete cascade`.

#### `identity.user_roles`
`(user_id, role_id)` composite PK, `assigned_at timestamptz not null default now()`, `assigned_by uuid FK users.id null`.

#### `identity.refresh_tokens`
`id uuid PK`, `user_id FK users.id`, `token_hash varchar(255) unique not null`, `family_id uuid not null`, `created_at`, `expires_at not null`, `revoked_at null`, `replaced_by_id uuid FK refresh_tokens.id null`, `created_by_ip varchar(45) null`.

Indexes: `(user_id)`, `(family_id)`. Reuse detection: on refresh, if the presented token's `family_id` row is already `revoked_at is not null`, revoke the whole family and write a `token_reuse_detected` audit event.

#### `identity.security_audit_events`
`id uuid PK`, `event_type varchar(40) not null` (`login_success`,`login_failed`,`token_refresh`,`token_reuse_detected`,`account_deactivated`,`account_reactivated`,`role_changed`), `user_id FK users.id null`, `occurred_at`, `outcome varchar(20)`, `ip_address varchar(45) null`, `metadata jsonb null`.

Indexes: `(user_id, occurred_at)`, `(event_type, occurred_at)`. Never stores passwords or tokens.

### Schema: `catalog`

#### `catalog.categories`
id uuid PK, name varchar(80) not null, display_order int default 0, is_active boolean default true, created_at/updated_at.

#### `catalog.menu_items`
id uuid PK, category_id FK categories.id, name varchar(120) not null, description text null, is_active boolean default true, created_at/updated_at. Index `(category_id)`.

#### `catalog.size_variants`
id uuid PK, menu_item_id FK menu_items.id, size_name varchar(40) not null, price numeric(12,2) not null check (price >= 0), is_active boolean default true. Unique `(menu_item_id, size_name)`.

#### `catalog.addons`
id uuid PK, name varchar(80) not null, price numeric(12,2) not null check (price >= 0), is_active boolean default true.

#### `catalog.menu_item_addons` (compatibility)
`(menu_item_id, addon_id)` composite PK.

#### `catalog.recipe_ingredients`
`id uuid PK`, `size_variant_id FK size_variants.id`, `ingredient_id FK inventory.ingredients.id`, `quantity numeric(12,3) not null check (quantity > 0)`, `unit varchar(20) not null`.

Unique `(size_variant_id, ingredient_id)`. Application validates `unit` converts cleanly to the ingredient's `base_unit` and that `ingredient_id` references an active ingredient before insert (spec: reject missing ingredient, non-positive quantity, incompatible unit).

### Schema: `inventory`

#### `inventory.ingredients`
id uuid PK, name varchar(100) unique not null, base_unit varchar(20) not null, available_quantity numeric(14,3) not null default 0 check (available_quantity >= 0) — cached read balance, reorder_threshold numeric(14,3) not null default 0 check (>= 0), is_active boolean default true, created_at/updated_at. Index `(is_active)`.

#### `inventory.suppliers`
id uuid PK, name varchar(150) not null, contact_name varchar(100) null, phone varchar(30) null, email varchar(255) null, is_active boolean default true.

#### `inventory.purchase_receipts`
`id uuid PK`, `supplier_id FK suppliers.id`, `receipt_reference varchar(60) unique not null`, `status varchar(10) not null check (status in ('draft','posted')) default 'draft'`, `received_at timestamptz null`, `posted_at timestamptz null`, `posted_by FK users.id null`, `created_by FK users.id not null`, `created_at`.

Indexes: `(supplier_id)`, `(status)`.

#### `inventory.purchase_receipt_lines`
id uuid PK, receipt_id FK purchase_receipts.id, ingredient_id FK ingredients.id, quantity numeric(14,3) not null check (quantity > 0), unit_cost numeric(12,2) not null check (unit_cost >= 0). Index `(receipt_id)`.

#### `inventory.stock_adjustments`
id uuid PK, ingredient_id FK ingredients.id, quantity_delta numeric(14,3) not null check (quantity_delta <> 0), reason text not null, actor_id FK users.id not null, created_at. Index `(ingredient_id, created_at)`.

#### `inventory.stock_movements` (immutable ledger — the audit source of truth)
`id uuid PK`, `ingredient_id FK ingredients.id`, `quantity_delta numeric(14,3) not null`, `balance_after numeric(14,3) not null`, `source_type varchar(20) not null check (in ('receipt','adjustment','order_consumption','order_restoration'))`, `source_id uuid not null`, `reason varchar(255) null`, `actor_id FK users.id null`, `occurred_at`.

Indexes: `(ingredient_id, occurred_at)`, `(source_type, source_id)`. Rows are never updated or deleted (enforced by omitting UPDATE/DELETE grants for the application role in Infrastructure; see decision #2 trade-off above).

#### `inventory.order_inventory_consumptions`
id uuid PK, order_id FK sales.orders.id, ingredient_id FK ingredients.id, quantity_deducted numeric(14,3) not null check (quantity_deducted > 0), stock_movement_id FK stock_movements.id not null. **Unique `(order_id, ingredient_id)`.** Records exactly what was deducted per order so cancellation restores the same amounts, not a recomputation from the (possibly since-changed) recipe.

#### `inventory.order_inventory_restorations`
`order_id uuid PK FK sales.orders.id`, `restored_at timestamptz not null default now()`, `restored_by FK users.id null`. Using `order_id` as the primary key means a second restoration attempt is a duplicate-key conflict the application treats as a no-op success — this is the idempotency guard required by the inventory spec.

### Schema: `sales`

#### `sales.tables`
id uuid PK, table_number varchar(20) unique not null, seats int null, is_occupied boolean not null default false, active_order_id uuid FK orders.id null. **Partial unique index** `(active_order_id) where active_order_id is not null` prevents the same order from occupying two tables; occupancy assignment itself runs inside a transaction with `select ... for update` on the table row to prevent double-assignment races.

#### `sales.orders`
`id uuid PK`, `order_number varchar(30) unique not null`, `channel varchar(10) not null check (in ('dine_in','takeaway','delivery'))`, `status varchar(15) not null check (in ('draft','confirmed','preparing','ready','completed','cancelled')) default 'draft'`, `customer_id FK customers.customers.id null`, `table_id FK sales.tables.id null`, `subtotal numeric(12,2) not null default 0`, `discount_total numeric(12,2) not null default 0 check (>= 0)`, `total numeric(12,2) not null default 0 check (total >= 0)`, `paid_amount numeric(12,2) not null default 0 check (>= 0)`, `loyalty_points_redeemed int not null default 0`, `loyalty_discount_amount numeric(12,2) not null default 0`, `created_by FK users.id not null`, `created_at`, `confirmed_at null`, `completed_at null`, `cancelled_at null`, `cancel_reason text null`.

Indexes: `(status)`, `(customer_id)`, `(channel, created_at)`. `table_id` required by application validation for `dine_in` at confirmation time (not a DB-level check, since drafts may not have one yet — see decision #5 above).

#### `sales.order_status_history`
id uuid PK, order_id FK orders.id, from_status varchar(15) null, to_status varchar(15) not null, changed_by FK users.id not null, changed_at. Index `(order_id, changed_at)`.

#### `sales.order_lines`
id uuid PK, order_id FK orders.id, menu_item_id FK catalog.menu_items.id null, size_variant_id FK catalog.size_variants.id null, item_name_snapshot varchar(120) not null, size_name_snapshot varchar(40) not null, unit_price_snapshot numeric(12,2) not null check (>= 0), quantity int not null check (quantity > 0), line_total numeric(12,2) not null check (>= 0). Index `(order_id)`. `menu_item_id`/`size_variant_id` are kept only for traceability — pricing and display always read the `*_snapshot` columns (decision #3 above).

#### `sales.order_line_addons`
id uuid PK, order_line_id FK order_lines.id, addon_id FK catalog.addons.id null, addon_name_snapshot varchar(80) not null, addon_price_snapshot numeric(12,2) not null check (>= 0). Index `(order_line_id)`.

#### `sales.discounts`
id uuid PK, order_id FK orders.id, type varchar(10) not null check (in ('fixed','percentage')), value numeric(12,2) not null check (value > 0), status varchar(10) not null check (in ('pending','approved','rejected')) default 'pending', requested_by FK users.id not null, requested_at, approved_by FK users.id null, approved_at null, computed_amount numeric(12,2) null. Index `(order_id, status)`. `computed_amount` is written once approved and validated to keep `orders.total >= 0`.

#### `sales.payments`
id uuid PK, order_id FK orders.id, method varchar(10) not null check (in ('cash','card','e_wallet')), amount numeric(12,2) not null check (amount > 0), status varchar(10) not null check (in ('pending','succeeded','failed')) default 'succeeded', reference varchar(100) null, paid_at, recorded_by FK users.id not null. Index `(order_id)`.

#### `sales.deliveries`
id uuid PK, order_id FK orders.id **unique**, recipient_name varchar(120) not null, contact_number varchar(30) not null, address_line text not null, customer_address_id FK customers.customer_addresses.id null, status varchar(15) not null check (in ('unassigned','assigned','out_for_delivery','delivered','failed')) default 'unassigned', assigned_staff_id FK users.id null, assigned_at null, out_for_delivery_at null, delivered_at null, failed_at null, failure_reason text null. Indexes: `(status)`, `(assigned_staff_id)`.

### Schema: `customers`

#### `customers.customers`
id uuid PK, full_name varchar(120) not null, phone varchar(30) unique not null, email varchar(255) null, is_active boolean default true, loyalty_points_balance int not null default 0 check (>= 0) — cached, `loyalty_transactions` is the ledger of record, created_at/updated_at.

#### `customers.customer_addresses`
id uuid PK, customer_id FK customers.id, label varchar(40) null, address_line text not null, is_default boolean default false. Index `(customer_id)`.

#### `customers.loyalty_transactions`
`id uuid PK`, `customer_id FK customers.id`, `order_id FK sales.orders.id null`, `type varchar(15) not null check (in ('earn','redeem','reverse_earn','reverse_redeem'))`, `points int not null check (points <> 0)`, `balance_after int not null check (balance_after >= 0)`, `reverses_transaction_id FK loyalty_transactions.id null`, `created_at`, `created_by FK users.id null`.

**Unique `(order_id, type) where order_id is not null`** — guards against an order earning, redeeming, or being reversed twice. Index `(customer_id, created_at)`.

#### `customers.loyalty_settings` (singleton row)
id uuid PK, earn_points_per_currency_unit numeric(8,4) not null, redeem_value_per_point numeric(8,4) not null, max_redeemable_percentage numeric(5,2) not null check (between 0 and 100), updated_at, updated_by FK users.id null. Open question below — values finalized before demo-data seeding.

### Schema: `reporting`

Reports are read models (EF Core projections or parameterized SQL/views) over `sales`, `catalog`, `inventory` — no physical report tables except the AI audit log.

#### `reporting.ai_interaction_audits`
id uuid PK, user_id FK identity.users.id not null, question text not null, tools_invoked jsonb not null default '[]', outcome varchar(20) not null check (in ('answered','refused','error')), started_at timestamptz not null, duration_ms int not null. Index `(user_id, started_at)`. Never stores tokens or provider credentials.

### Idempotency & Concurrency Summary

| Concern | Mechanism |
|---|---|
| Order confirmation deducts each ingredient once | Single DB transaction; `order_inventory_consumptions` unique `(order_id, ingredient_id)` |
| Cancellation restores stock exactly once | `order_inventory_restorations` PK on `order_id` |
| Order earns/redeems loyalty exactly once, reversal exactly once | `loyalty_transactions` unique `(order_id, type)` |
| Two dine-in orders can't occupy one table | Partial unique index on `tables.active_order_id` + row lock at assignment |
| Concurrent order/table updates don't silently overwrite | EF Core optimistic concurrency via Postgres `xmin` on `orders` and `tables` |
| Refresh-token reuse | `family_id` + `revoked_at`; reuse of a revoked token revokes the whole family |
| Purchase receipt increases stock only once | `status` transition `draft → posted` is a guarded, transactional single-fire operation (posting a non-draft receipt is rejected) |

## API Design

ASP.NET Core Web API (final milestone). Base path `/api/v1`. JSON, `application/problem+json`
for errors (RFC 7807), consistent with decision #9 above (contracts never expose EF entities).

### Conventions

- **Auth:** `Authorization: Bearer <access_token>` on every endpoint except `POST /auth/login`, `POST /auth/refresh`.
- **Errors:** `400` validation, `401` unauthenticated, `403` unauthorized (permission-based, see identity-and-access spec), `404` not found, `409` conflict (state transition / concurrency), `422` used only for business-rule rejections that aren't plain field validation (e.g. insufficient stock). Body:
  ```json
  { "type": "https://...", "title": "...", "status": 409, "detail": "...", "errors": { "field": ["..."] } }
  ```
- **Paging:** `?page=1&pageSize=20` → `{ "items": [...], "page":1, "pageSize":20, "totalCount": N }`.
- **Money/qty:** JSON numbers serialized with fixed decimal precision (never float parsing on the client side).
- **Permission codes** referenced below match `identity.permissions` in the Database Schema section above.
- **Idempotency:** endpoints that must not double-apply (`confirm`, `cancel`, `post` receipt, `complete`) are safe to retry — a repeat call after success returns the same resulting state (`200`/`409` as noted per-endpoint), never a duplicate side effect, per the DB-level idempotency guards.

### Auth — `identity-and-access`

| Method | Path | Permission | Notes |
|---|---|---|---|
| POST | `/auth/login` | none | `{username, password}` → `{accessToken, refreshToken, expiresAt, user:{id,fullName,roles[]}}`. Generic `401` on bad credentials or deactivated account — never reveals which. |
| POST | `/auth/refresh` | none | `{refreshToken}` → new pair; rotates and invalidates the old one. `401` if expired/revoked/reused (reuse also revokes the whole token family + audit event). |
| POST | `/auth/logout` | authenticated | Revokes the presented refresh token family. |
| GET | `/users` | `users.manage` | Paged, filter `?isActive=`. |
| POST | `/users` | `users.manage` | Create staff account + role assignment. |
| PATCH | `/users/{id}` | `users.manage` | Update profile / roles. |
| POST | `/users/{id}/deactivate` | `users.manage` | `409` if target is the last active Admin. |
| POST | `/users/{id}/reactivate` | `users.manage` | |
| GET | `/audit/security-events` | `users.manage` | Paged, filter `?userId=&type=&from=&to=`. |

### Catalog — `menu-and-recipes`

| Method | Path | Permission | Notes |
|---|---|---|---|
| GET | `/categories` | authenticated | |
| POST | `/categories` | `menu.manage` | |
| PATCH | `/categories/{id}` | `menu.manage` | |
| GET | `/menu-items` | authenticated | `?categoryId=&isActive=&search=`; includes size variants + addon compatibility summary. |
| GET | `/menu-items/{id}` | authenticated | Full detail incl. per-size recipes. |
| POST | `/menu-items` | `menu.manage` | Body includes initial size variants. |
| PATCH | `/menu-items/{id}` | `menu.manage` | |
| POST | `/menu-items/{id}/deactivate` | `menu.manage` | Historical orders unaffected (snapshot columns). |
| POST | `/menu-items/{id}/size-variants` | `menu.manage` | `{sizeName, price}`; `400` if price < 0. |
| PATCH | `/size-variants/{id}` | `menu.manage` | |
| POST | `/size-variants/{id}/deactivate` | `menu.manage` | |
| GET | `/addons` | authenticated | |
| POST | `/addons` | `menu.manage` | |
| PATCH | `/addons/{id}` | `menu.manage` | |
| POST | `/menu-items/{id}/addons/{addonId}` | `menu.manage` | Attach compatible add-on. |
| DELETE | `/menu-items/{id}/addons/{addonId}` | `menu.manage` | |
| GET | `/size-variants/{id}/recipe` | authenticated | Ingredient lines with quantity + unit. |
| PUT | `/size-variants/{id}/recipe` | `menu.manage` | Replaces recipe lines atomically. `422` listing every invalid line if any ingredient is missing/inactive, quantity ≤ 0, or unit incompatible — matches the menu-and-recipes spec's reject-on-any-invalid-line behavior. |

### Inventory — `inventory-and-procurement`

| Method | Path | Permission | Notes |
|---|---|---|---|
| GET | `/ingredients` | authenticated | `?isActive=&search=&lowStockOnly=`. |
| GET | `/ingredients/{id}` | authenticated | |
| POST | `/ingredients` | `inventory.manage` | `{name, baseUnit, reorderThreshold}`. |
| PATCH | `/ingredients/{id}` | `inventory.manage` | `409` if changing `baseUnit` would invalidate existing recipes (per the base-unit conversion rule). |
| POST | `/ingredients/{id}/deactivate` | `inventory.manage` | |
| GET | `/ingredients/{id}/movements` | authenticated | Paged ledger: `sourceType, sourceId, quantityDelta, balanceAfter, actor, occurredAt`. |
| GET | `/suppliers` | authenticated | |
| POST | `/suppliers` | `inventory.manage` | |
| PATCH | `/suppliers/{id}` | `inventory.manage` | |
| POST | `/suppliers/{id}/deactivate` | `inventory.manage` | Cannot be selected on new receipts; historical receipts unaffected. |
| GET | `/purchase-receipts` | authenticated | `?status=&supplierId=`. |
| POST | `/purchase-receipts` | `inventory.receive` | Creates a `draft` receipt with lines. |
| PATCH | `/purchase-receipts/{id}` | `inventory.receive` | Edit lines while `draft`; `409` if already posted. |
| POST | `/purchase-receipts/{id}/post` | `inventory.receive` | Transactional: increases stock, writes movements, sets `posted`. `409` if not currently `draft` (idempotent retry-safe — a retry after success sees `posted` and returns `409`, not a duplicate increase). |
| POST | `/stock-adjustments` | `inventory.adjust` | `{ingredientId, quantityDelta, reason}`. `400` if reason blank or resulting balance < 0. |
| GET | `/reports/low-stock` | `reports.lowstock` | Active ingredients where `available_quantity <= reorder_threshold`. |

### Sales & Fulfillment — `sales-and-fulfillment`

| Method | Path | Permission | Notes |
|---|---|---|---|
| GET | `/tables` | authenticated | Includes `isOccupied`, `activeOrderId`. |
| POST | `/tables` | `menu.manage` | |
| GET | `/orders` | authenticated | `?status=&channel=&customerId=&from=&to=`, paged. |
| GET | `/orders/{id}` | authenticated | Full detail: lines, addons, discounts, payments, delivery, status history. |
| POST | `/orders` | `orders.create` | `{channel, tableId?, customerId?, delivery?, lines:[{menuItemId,sizeVariantId,quantity,addonIds[]}]}`. Creates `draft`; rejects unavailable item/size/addon (`400`). |
| PATCH | `/orders/{id}/lines` | `orders.create` | Add/remove/update lines while `draft`. `409` if not draft. |
| POST | `/orders/{id}/confirm` | `orders.confirm` | Validates channel-specific requirements (table for dine-in, address for delivery — `400` naming the missing field), then the atomic recipe-consumption transaction (decision #4 above). `422` with per-ingredient shortage detail if stock insufficient — order stays `draft`, nothing deducted. On success: `confirmed`, pricing snapshot written, table occupied if dine-in. |
| POST | `/orders/{id}/start-preparing` | `orders.create` | `confirmed → preparing`. `409` on invalid transition. |
| POST | `/orders/{id}/ready` | `orders.create` | `preparing → ready`. |
| POST | `/orders/{id}/complete` | `orders.complete` | `422` if `paid_amount < total`. On success: `completed`, table released if dine-in, triggers loyalty earning (same transaction) if eligible customer attached. |
| POST | `/orders/{id}/cancel` | `orders.cancel` | `{reason}` required. Transactional: `cancelled`, table released, inventory restoration requested (idempotent — `order_inventory_restorations` PK guard), loyalty reversal for any earn/redeem tied to the order. Repeat calls after success return `409` (already cancelled), not a duplicate restoration. |
| POST | `/orders/{id}/payments` | `payments.record` | `{method, amount, reference?}`. `400` if amount ≤ 0. |
| POST | `/orders/{id}/discounts` | `discounts.request` | `{type, value}` → `pending`. `422` if it would drive total below zero (checked again at approval, since prior discounts may have changed the base). |
| POST | `/discounts/{id}/approve` | `discounts.approve` | Recomputes `computed_amount`; `422` if it would make total negative. |
| POST | `/discounts/{id}/reject` | `discounts.approve` | |
| POST | `/orders/{id}/delivery/assign` | `delivery.assign` | `{staffId}`; `409` if order not `ready` or staff not an active DeliveryStaff. |
| POST | `/deliveries/{id}/out-for-delivery` | `delivery.update` | |
| POST | `/deliveries/{id}/delivered` | `delivery.update` | |
| POST | `/deliveries/{id}/failed` | `delivery.update` | `{reason}`. |

### Customers & Loyalty — `customers-and-loyalty`

| Method | Path | Permission | Notes |
|---|---|---|---|
| GET | `/customers` | authenticated | `?isActive=&search=`. |
| GET | `/customers/{id}` | authenticated | Includes `loyaltyPointsBalance`, addresses, order history summary. |
| POST | `/customers` | `customers.manage` | |
| PATCH | `/customers/{id}` | `customers.manage` | |
| POST | `/customers/{id}/deactivate` | `customers.manage` | Excluded from selection; historical orders unaffected. |
| GET | `/customers/{id}/addresses` | authenticated | |
| POST | `/customers/{id}/addresses` | `customers.manage` | |
| GET | `/customers/{id}/loyalty-transactions` | authenticated | Paged ledger. |
| POST | `/orders/{id}/loyalty/redeem` | `loyalty.redeem` | `{points}`. `422` if exceeds balance or `maxRedeemablePercentage` of order total. Only valid while order is `draft`/`confirmed` (pre-completion). |
| GET | `/loyalty-settings` | authenticated | Current earn/redeem rules. |
| PATCH | `/loyalty-settings` | `users.manage` | Admin-only config change. |

### Reporting & AI — `reporting-and-ai`

| Method | Path | Permission | Notes |
|---|---|---|---|
| GET | `/reports/revenue` | `reports.revenue` | `?from=&to=&groupBy=day\|paymentMethod`. Excludes cancelled orders; revenue = successful payments (documented rule, decision #10 above). |
| GET | `/reports/best-sellers` | `reports.bestsellers` | `?from=&to=&limit=`. Ranked by quantity and revenue, excludes cancelled. |
| GET | `/reports/channels` | `reports.channels` | `?from=&to=`. Order count + revenue per `dine_in/takeaway/delivery`. |
| GET | `/reports/low-stock` | `reports.lowstock` | Same result contract as the inventory low-stock endpoint (single shared query). |
| POST | `/assistant/ask` | `assistant.ask` | `{question}` → `{answer, toolsInvoked:[{tool,args,outcome}], groundedOn:{period,metrics}}`. Server resolves caller's permissions; the tool gateway silently omits/refuses tools the caller lacks (`reports.*`). Refuses (no tool call) if the question asks to create/update/delete/approve/cancel data. Every call writes a `reporting.ai_interaction_audits` row. |

### Cross-cutting

- **OpenAPI:** generated from contracts (`CoffeeShop.Contracts`), served at `/swagger` in non-prod.
- **Health checks:** `GET /health` (liveness), `GET /health/ready` (DB connectivity) — used by Docker Compose (decision #12 above).
- **API versioning:** URL-segment (`/api/v1`) so `CoffeeShop.Contracts` can evolve without breaking the WinUI client mid-migration (decision #9 above).
- **Rate limiting / abuse control:** out of scope per proposal.md non-goals (no public customer-facing surface).

## UI Design System

`CoffeeShop.WinUI` uses a single "Hand-Drawn" visual theme across both milestones (midterm direct-DB and final API-backed builds share the same client project and styling). The full authoring reference — including copy-pasteable XAML tokens, WinUI techniques for effects CSS-based design systems assume (offset shadows, wobbly radii, dashed dividers), and an agent-usable role prompt for consistently applying the style — lives in [`docs/sketch-theme.md`](../../../docs/sketch-theme.md) at the repo root. Summary for design/implementation planning:

### Philosophy

Organic, playful imperfection — sketches on paper, sticky notes, napkin diagrams — applied consistently to every screen (POS, table map, barista queue, inventory, customers, reports, AI assistant, login), not just customer-facing surfaces. The whole app is one coffee-shop-branded product; manager/report screens keep the same tokens but dial down rotation and decoration density so numbers stay quick to scan.

### Tokens

- **Colors**: Warm Paper `#FDFBF7` background, Soft Pencil Black `#2D2D2D` foreground/border (never pure black), Old Paper `#E5E0D8` muted, Red Correction Marker `#FF4D4D` accent (destructive/urgent), Blue Ballpoint `#2D5DA1` secondary accent (informational/in-progress), Post-it Yellow `#FFF9C4` for featured/highlighted cards only.
- **Typography**: `Kalam` (headings), `Patrick Hand` (body) — embedded `.ttf` assets referenced via `ms-appx:///Assets/Fonts/...#FamilyName` (WinUI doesn't resolve web font names). A documented exception allows `Segoe UI Variable` on dense numeric report tables if handwritten digits reduce scan speed.
- **Shape**: Uneven 4-value `CornerRadius` per element (WinUI has no elliptical 8-value `border-radius`); reserve hand-authored Path-geometry blobs for a few decorative spots (login backdrop) rather than every card.
- **Shadow**: No native `box-shadow` — emulated with a second solid-color `Border`, offset via `Translation`, no blur. Buttons "press flat" (shadow collapses) on click; cards "lift" (gap shrinks) on hover.

### Screen Application

POS/order entry and the table map get full decoration density (tape/tack, rotation) since they're fast-tap staff surfaces; the barista kanban slides tickets between status columns; inventory low-stock rows get a left red accent bar rather than a full-row fill; reports and the AI assistant chat panel intentionally reduce rotation/decoration for manager readability; the login screen is the one place allowed a slow (~3s) idle decorative animation.

### Responsiveness

WinUI `AdaptiveTrigger` breakpoints (Compact `<641px`, Medium `641–1007px`, Expanded `≥1008px`, the primary POS-terminal target) scale menu-grid column count and decoration density, never removing the core wobble/shadow/handwritten-font language. Touch targets stay ≥48px at every breakpoint for touchscreen POS terminals.

### Accessibility

Focus visuals are reskinned (blue `FocusVisualPrimaryBrush`/`SecondaryBrush`), never removed, since cashier workflows are keyboard-driven. Contrast is kept readable despite the pencil/paper palette; functional icons use Fluent/Segoe Fluent Icons rather than custom hand-drawn glyphs, wrapped in a wobbly circle border to fit the aesthetic without sacrificing legibility.

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
