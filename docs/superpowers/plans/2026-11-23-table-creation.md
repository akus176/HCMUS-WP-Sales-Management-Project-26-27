# Dining-table creation implementation plan

> **For agentic workers:** Use the issue #229 acceptance criteria and implement the three tasks in order, with focused regression checks before each commit.

**Goal:** A manager can create a table through the API and immediately select it for a dine-in ticket in Counter.

**Architecture:** Application validates the command and owns the creation result. PostgreSQL persists through the existing unique table-number index and maps only that constraint's collision to conflict. The API enforces a dedicated manager/admin permission; the WinUI client uses the authenticated API and refreshes available tables.

**Tech stack:** .NET 10, ASP.NET Core minimal API, EF Core/Npgsql, WinUI 3, xUnit.

**Spec:** Issue #229, `openspec/changes/build-coffee-shop-management-system/proposal.md`, tasks 6.3/9.6/11.4, sales/identity specs, and `docs/ui-foundation.md`.

## Constraints and review focus

- Keep persistence behind API and preserve existing occupancy/order references. No migration or floor plan.
- Number: trim, require 1–20 characters; seats: positive integer. A database unique index arbitrates concurrent duplicate creation.
- Manager and Admin may create; Cashier may still list/select for orders but may not create.
- Check duplicate, invalid, forbidden, concurrent, and newly available table flows. Do not claim local Linux tests if WSL/Docker are inaccessible.

## Task 1: Failing behavior checks

- [ ] Add application tests for trimmed creation, invalid number/seats, and duplicate result.
- [ ] Add PostgreSQL API tests for create/list, unauthorized/forbidden, invalid/duplicate and parallel duplicate, including unchanged occupancy.
- [ ] Add WinUI API adapter/workspace tests for authorized create, refreshed options and permission gating.
- [ ] Run each focused test red in Ubuntu where available; otherwise record the environment block and use Ubuntu CI as evidence.

## Task 2: Backend

- [ ] Add `CreateDiningTableCommand`, `ITableManagementRepository`, and `TableManagementCommandHandler` in Application/Sales.
- [ ] Add PostgreSQL repository using the existing `sales.tables` unique index, and register it/handler in dependency injection.
- [ ] Add `CreateTableRequest` and protected `POST /api/v1/tables`, with 201/400/401/403/409 responses; add `tables.manage` to Admin and Manager only.
- [ ] Run focused and broader relevant checks; commit backend and tests.

## Task 3: Counter and delivery

- [ ] Add `ApiPosSource.CreateTableAsync`, workspace create/refresh/selection, and manager-only Add table dialog in Counter.
- [ ] Keep feedback and validation accessible, responsive, and safe on unknown network outcome.
- [ ] Run Windows build and native/UI checks where available; use Linux CI for local-test substitute only as separately labeled CI evidence.
- [ ] Commit WinUI work, inspect diff, publish three commits, and open the pull request with actual evidence and limitations.
