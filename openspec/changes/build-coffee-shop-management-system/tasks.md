## 1. Repository and Solution Foundation

- [x] 1.1 Create the .NET solution and Domain, Application, Contracts, PostgreSQL Infrastructure, WinUI, API, and test projects; verify `dotnet build` succeeds for the empty solution
- [x] 1.2 Configure project references so Domain has no infrastructure dependency and Application depends only on Domain abstractions; verify the dependency graph builds without circular references
- [x] 1.3 Add central package/version management for WinUI 3, EF Core, Npgsql, validation, JWT, and test libraries; verify package restore succeeds on a clean checkout
- [x] 1.4 Add repository formatting, analyzer, nullable-reference, and warning policies; verify the solution builds with the agreed warning threshold
- [x] 1.5 Add ignored environment templates for PostgreSQL, API, JWT, and AI settings without real secrets; verify secret and connection-string values are absent from tracked files
- [x] 1.6 Add a CI workflow that restores, builds, and runs tests; verify it succeeds on the initial scaffold

## 2. Shared Domain and Persistence Foundation

- [x] 2.1 Implement shared entity identifiers, money values, measured quantities, timestamps, and audit metadata; verify unit tests cover invalid money and quantity values
- [x] 2.2 Define module-oriented domain entities and persistence mappings for catalog, sales, inventory, customers, and identity; verify EF Core can construct the model without mapping errors
- [x] 2.3 Configure PostgreSQL, Npgsql, the application database context, and design-time migration support; verify a migration can be generated from the solution
- [x] 2.4 Create the initial database migration with keys, foreign keys, indexes, precision, and check constraints; verify it applies successfully to an empty PostgreSQL database
- [x] 2.5 Add transaction, clock, current-user, and audit abstractions to the Application layer; verify unit tests can substitute deterministic implementations
- [x] 2.6 Add consistent application result and error contracts for validation, not-found, conflict, authentication, and authorization outcomes; verify mapping tests cover every error type

## 3. Menu and Recipe Capability

- [x] 3.1 Implement category and menu-item domain models with activation and deactivation rules; verify unit tests preserve historical references after deactivation
- [x] 3.2 Implement size variants with independent prices and availability; verify tests select the correct variant price and reject invalid prices
- [x] 3.3 Implement add-on definitions, compatibility, price, and availability; verify tests reject unavailable or incompatible add-ons
- [x] 3.4 Implement size-specific recipe models linked to active ingredients; verify tests reject missing ingredients and non-positive quantities
- [x] 3.5 Implement menu and recipe application commands and queries for create, update, deactivate, search, and detail views; verify application tests cover successful and invalid operations
- [x] 3.6 Implement PostgreSQL persistence for menu and recipe workflows; verify integration tests save and reload a complete item with sizes, add-ons, and recipes
- [x] 3.7 Build WinUI menu, variant, add-on, and recipe management screens; verify an operator can create, edit, deactivate, and search a complete drink definition

## 4. Inventory and Procurement Capability

- [x] 4.1 Implement ingredient models with base units, balances, reorder thresholds, and activation state; verify unit tests reject invalid thresholds and incompatible unit changes
- [x] 4.2 Implement supplier management while preserving historical receipt references; verify integration tests prevent deactivated suppliers from new receipt selection
- [x] 4.3 Implement draft and posted purchase receipts with ingredient quantities and costs; verify posting a receipt increases stock exactly once
- [x] 4.4 Implement positive and negative stock adjustments with mandatory reasons and actor identity; verify tests reject missing reasons and invalid resulting quantities
- [x] 4.5 Implement immutable stock-movement records for receipts, adjustments, order consumption, and restoration; verify every stock-changing integration test creates the expected movement
- [x] 4.6 Implement low-stock queries using active ingredient thresholds; verify boundary tests include quantities above, equal to, and below the threshold
- [x] 4.7 Build WinUI ingredient, supplier, purchase-receipt, stock-adjustment, movement-history, and low-stock screens; verify each workflow updates the displayed balance

## 5. Customers and Loyalty Capability

- [x] 5.1 Implement optional customer profiles with contact data, activation state, and search; verify walk-in orders remain possible without a profile
- [x] 5.2 Implement reusable and order-specific delivery-address models; verify delivery validation accepts either source and rejects missing required fields
- [x] 5.3 Implement configurable loyalty earning, redemption, and maximum-redemption rules; verify unit tests cover balances, conversion, limits, and inactive customers
- [x] 5.4 Implement append-only loyalty transactions with order references and reversal support; verify repeated reversal requests do not alter the balance twice
- [x] 5.5 Implement customer and loyalty application commands and queries with PostgreSQL persistence; verify integration tests cover earning, redemption, cancellation reversal, and history
- [x] 5.6 Build WinUI customer profile, order attachment, address, balance, and loyalty-history screens; verify a completed eligible order changes the visible balance once

## 6. Sales, Payments, Tables, and Fulfillment

- [x] 6.1 Implement order aggregates, line snapshots, size/add-on selection, totals, and channel-specific validation; verify tests cover dine-in, takeaway, and delivery requirements
- [x] 6.2 Implement the order state machine and status history for draft, confirmed, preparing, ready, completed, and cancelled states; verify every allowed and rejected transition is unit tested
- [x] 6.3 Implement table management and concurrency-safe occupancy; verify two simultaneous assignments cannot occupy the same table
- [x] 6.4 Implement cash, card, and e-wallet payment records and paid-balance calculation; verify an order cannot complete with an unpaid balance
- [x] 6.5 Implement fixed and percentage discount requests with manager approval and non-negative total validation; verify unapproved discounts do not affect payable totals
- [x] 6.6 Implement atomic order confirmation that validates recipes, checks stock, snapshots pricing, deducts all ingredients, and rolls back on shortage; verify PostgreSQL integration tests prove all-or-nothing behavior
- [x] 6.7 Implement idempotent cancellation that records a reason, stops fulfillment, restores consumed inventory, and reverses affected loyalty entries; verify repeated cancellation processing has no duplicate effects
- [x] 6.8 Implement delivery assignment and ready, out-for-delivery, delivered, and failed status transitions; verify only eligible active delivery staff can receive assignments
- [x] 6.9 Build WinUI POS screens for channel selection, product configuration, customer attachment, table/address details, totals, discounts, and confirmation; verify one order can be completed for each channel
- [x] 6.10 Build WinUI barista queue, table overview, payment, cancellation, manager approval, and delivery work screens; verify observable controls follow the order state machine

## 7. Reporting Capability

- [x] 7.1 Define the authoritative revenue rule for successful payments, cancellations, discounts, and date boundaries; verify the rule is documented in code tests and shared by all revenue queries
- [x] 7.2 Implement revenue and order-count queries with daily and payment-method breakdowns; verify seeded integration tests cover month and time-zone boundaries
- [x] 7.3 Implement best-seller queries by quantity and revenue while excluding cancelled orders; verify ranking tests use item and variant snapshots
- [x] 7.4 Implement dine-in, takeaway, and delivery channel summaries; verify totals reconcile with the same seeded orders used by revenue tests
- [x] 7.5 Implement current low-stock reporting from ingredient balances and thresholds; verify results match the inventory low-stock query
- [x] 7.6 Build WinUI report filters and views for revenue, best sellers, order channels, and low stock; verify each report renders a known seeded dataset correctly

## 8. Midterm Direct-Database Milestone

- [x] 8.1 Implement the WinUI direct composition root that hosts Application services and PostgreSQL infrastructure in-process; verify the client starts and completes a database health check
- [x] 8.2 Configure a restricted PostgreSQL role for the midterm client and local-only connection settings; verify the role cannot perform unneeded database-administration operations
- [x] 8.3 Add loading, validation, empty, conflict, and database-error states across midterm WinUI workflows; verify UI tests or scripted checks exercise each state
- [x] 8.4 Create realistic seed data with at least 20 menu products, 5 categories, 10 customers, 20 varied orders, ingredients, recipes, suppliers, and tables; verify a seed validation test checks all minimum counts
- [x] 8.5 Execute the full midterm demonstration path for menu, customers, inventory, all order channels, reports, and cancellation restoration; verify the documented checklist passes against a rebuilt database
- [x] 8.6 Produce the midterm test-case report with inputs, expected results, actual results, and pass/fail status; verify it includes valid, invalid, and boundary cases for every core module

## 9. ASP.NET Core API and Contracts

- [x] 9.1 Define versioned request, response, paging, filtering, and error contracts without exposing EF Core entities; verify contract serialization tests cover representative payloads
- [x] 9.2 Configure the API host, dependency injection, PostgreSQL infrastructure, exception handling, validation, OpenAPI, and health checks; verify the API starts against a clean migrated database
- [x] 9.3 Expose catalog and recipe endpoints through existing application use cases; verify API integration tests cover success, validation, not-found, and conflict responses
- [x] 9.4 Expose inventory, supplier, receipt, adjustment, and movement endpoints; verify API tests confirm stock-changing operations are transactional
- [x] 9.5 Expose customer, address, and loyalty endpoints; verify API tests cover optional customers and loyalty balance changes
- [x] 9.6 Expose order, table, payment, discount, preparation, cancellation, and delivery endpoints; verify API tests cover each channel and invalid state transitions
- [x] 9.7 Expose report endpoints with date-range validation and consistent metrics; verify API results match direct application-query integration tests
- [x] 9.8 Add an API test collection and local environment definition covering representative successful and failing requests; verify it runs against the Docker API without manual payload editing

## 10. Authentication and Authorization

- [x] 10.1 Implement staff accounts, password hashing, role, permission, and assignment persistence; verify seed tests create all six operational roles with explicit permission sets
- [x] 10.2 Implement login with generic failure responses and active-account checks; verify tests cover valid credentials, invalid passwords, unknown users, and deactivated accounts
- [x] 10.3 Implement short-lived JWT access tokens and hashed refresh-token storage; verify token tests check claims, expiry, and absence of sensitive data
- [x] 10.4 Implement refresh-token rotation, logout revocation, family revocation on reuse, and account-wide revocation; verify security integration tests cover each invalidation path
- [x] 10.5 Implement permission-based API policies and endpoint requirements; verify authorization tests cover allowed and forbidden operations for admin, manager, cashier, barista, inventory, and delivery roles
- [ ] 10.6 Implement account administration with final-active-administrator protection; verify attempts to remove the last administrator fail without state changes
- [x] 10.7 Implement security audit events for login, token reuse, account state, and role changes; verify audits omit passwords, access tokens, and refresh tokens

## 11. Final WinUI API Migration

- [x] 11.1 Implement a typed HTTP client, consistent error mapping, and API service interfaces matching the WinUI-facing direct adapters; verify mocked client tests cover success and every standard error category
- [x] 11.2 Implement login, secure Windows token storage, automatic access-token attachment, refresh, logout, and expired-session handling; verify plaintext tokens do not appear in configuration or logs
- [ ] 11.3 Migrate catalog, recipe, inventory, supplier, and customer screens from direct services to API clients; verify parity tests complete the same workflows through the API
- [ ] 11.4 Migrate POS, order, table, payment, discount, barista, cancellation, and delivery screens to API clients; verify all channel demonstrations pass through HTTP only
- [x] 11.5 Migrate reports and loyalty workflows to API clients; verify results match the seeded direct-query baselines
- [x] 11.6 Add role/permission-aware navigation and control visibility for usability; verify server tests still reject direct unauthorized requests regardless of UI visibility
- [ ] 11.7 Remove PostgreSQL infrastructure references and connection settings from final WinUI release builds; verify dependency inspection and runtime network tracing show only API communication

## 12. AI Manager Assistant

- [x] 12.1 Define structured read-only tool contracts for revenue, best sellers, order channels, and low-stock reports; verify schemas reject malformed date ranges and unsupported parameters
- [x] 12.2 Implement a permission-aware tool gateway that calls existing report queries with the authenticated manager context; verify unauthorized tool calls return no restricted data
- [x] 12.3 Implement the AI provider abstraction and a deterministic fake provider for tests; verify assistant orchestration tests run without an external AI service
- [x] 12.4 Implement grounded assistant behavior for tool selection, empty results, unsupported questions, and refusal of state-changing requests; verify scripted tests cover each behavior
- [x] 12.5 Implement AI interaction auditing for user, question, tools, outcomes, and duration while redacting secrets; verify audit tests confirm tokens and provider credentials are never stored
- [ ] 12.6 Build the WinUI manager-assistant interface with visible reporting period and tool-result context; verify a manager can ask a report question and see a grounded answer

## 13. Deployment, Quality, and Final Delivery

- [x] 13.1 Create Dockerfiles and Docker Compose services for PostgreSQL and the API with health checks, named volumes, and environment configuration; verify a clean `docker compose up` reaches healthy status
- [x] 13.2 Add automated migration and seed procedures for development and demo environments; verify rebuilding from empty volumes produces the expected schema and demo counts
- [x] 13.3 Add concurrent integration tests for order confirmation, table assignment, cancellation restoration, loyalty reversal, and refresh-token reuse; verify all race-condition tests pass repeatedly
- [x] 13.4 Add regression coverage for menu deactivation, historical snapshots, invalid quantities, insufficient stock, payment balance, and authorization boundaries; verify the complete automated suite passes
- [x] 13.5 Run secret scanning, dependency auditing, formatting, build, migration, unit, integration, and API collection checks in CI; verify a clean pipeline succeeds and intentional violations fail it
- [ ] 13.6 Write README documentation for architecture, prerequisites, setup, Docker services, migrations, demo accounts, roles, tests, and both milestone modes; verify a new contributor can follow it from a clean checkout
- [x] 13.7 Document the five-person module ownership, review rules, migration-conflict procedure, and Git commit conventions; verify every module and cross-cutting responsibility has an owner and reviewer
- [x] 13.8 Produce the final quality report listing tested functions, representative cases, pass/fail results, defects and fixes, boundary tests, API tests, and authorization tests; verify each claimed result links to reproducible evidence
- [ ] 13.9 Execute the final end-to-end demonstration from a clean Docker environment covering login, RBAC, menu, inventory receipt, all order channels, payment, cancellation restoration, loyalty, reports, delivery, and AI; verify the complete acceptance checklist passes
- [ ] 13.10 Prepare submission artifacts including repository link, read-only access token handling instructions, unlisted demo-video link, build instructions, and implemented/unimplemented feature summary; verify the submission archive contains no runtime secrets except the separately required access-token file
