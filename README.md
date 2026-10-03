# Coffee Shop

WinUI 3 desktop client and ASP.NET Core API for a coffee-shop management system. The current repository is a **foundation scaffold**, not a functioning POS. The behavior and milestone plan are in `openspec/changes/build-coffee-shop-management-system/`; the client theme is in `docs/sketch-theme.md`.

## Prerequisites

- Windows 10 1809 or later with the Windows App SDK runtime for the unpackaged client
- .NET 10 SDK; Visual Studio with Windows App SDK tooling is useful for client development
- PostgreSQL 16 or later for applying and verifying migrations

## Build and test

```powershell
dotnet restore CoffeeShop.slnx
dotnet build CoffeeShop.slnx
dotnet test CoffeeShop.slnx
dotnet run --project src/CoffeeShop.Api
```

The API currently exposes only `/health` (and `/openapi/v1.json` in Development). Run the WinUI project from Visual Studio or use `dotnet run --project src/CoffeeShop.WinUI -r win-x64` on Windows. The UI is a themed shell with no database connection yet.

## Project boundaries

| Project                              | Responsibility                                                        |
| ------------------------------------ | --------------------------------------------------------------------- |
| `CoffeeShop.Domain`                  | Business entities and rules; no framework or persistence reference    |
| `CoffeeShop.Application`             | Use cases and abstractions; references Domain only                    |
| `CoffeeShop.Contracts`               | API request/response contracts; independent of persistence            |
| `CoffeeShop.Infrastructure.Postgres` | EF Core/Npgsql persistence, later migrations                          |
| `CoffeeShop.WinUI`                   | Desktop presentation; composes Application and PostgreSQL for midterm |
| `CoffeeShop.Api`                     | Final HTTP composition root, later authentication and permissions     |
| `tests/*`                            | Unit, infrastructure, API, and future UI-facing tests                 |

Midterm runs application use cases inside WinUI against a restricted local PostgreSQL account. Final WinUI must drop its infrastructure reference and use typed HTTP clients; the API then owns authorization and persistence. The PostgreSQL adapter is configured; those application workflows are not implemented yet.

### Catalog lifecycle (T3.1)

Categories support creation, rename, activation, and deactivation. Menu items support creation,
updates, activation, and deactivation. Names are trimmed and limited to the mapped 80/120
characters. Menu-item creation and updates take a resolved active `Category`; activation
requires the item's own active category. `IsAvailableForOrdering` checks both activation
states and category membership. Deactivation retains IDs and links without cascading to variants.

Domain methods validate before mutation and record the supplied change time. Repeated state
transitions are rejected. `CoffeeShop.Application.Catalog.CatalogLifecycle` maps invalid input
to shared validation results and invalid state to shared conflict results, without depending
on EF Core. Repository loading, saving, search, and command/query orchestration belong to
T3.5/T3.6; order snapshots and order-selection enforcement remain sales-module work.

## Local PostgreSQL configuration and migrations

Copy `.env.example` to an ignored `.env` and replace the PostgreSQL placeholders. `.env` is a reference only and is **not** loaded automatically, so set the connection string in the current PowerShell session before using Entity Framework Core:

```powershell
$env:ConnectionStrings__CoffeeShop = "Host=localhost;Port=5432;Database=coffeeshop;Username=coffeeshop;Password=<local-password>"
dotnet tool restore
```

Never commit local credentials. The design-time factory and future runtime composition roots use the same `ConnectionStrings__CoffeeShop` setting.

Create a migration only after rebasing on the latest `main` and confirming that all current entity mappings are present:

```powershell
dotnet ef migrations add <MigrationName> `
  --project src/CoffeeShop.Infrastructure.Postgres `
  --startup-project src/CoffeeShop.Infrastructure.Postgres `
  --output-dir Migrations
```

Apply every pending migration to the configured database:

```powershell
dotnet ef database update `
  --project src/CoffeeShop.Infrastructure.Postgres `
  --startup-project src/CoffeeShop.Infrastructure.Postgres
```

To verify a full rebuild, point the connection string at a newly created empty database, run `dotnet ef database update`, and confirm that a second run reports no pending migrations. Recreate the verification database through PostgreSQL tooling rather than editing tables by hand.

JWT and AI placeholders follow the same environment-variable convention (double underscores denote nested keys). No demo accounts or secrets are committed.

Planned operational roles are Admin, Manager, Cashier, Barista, Inventory Staff, and Delivery Staff; role policies are not yet enforced. Docker Compose, migration/seed commands, and the two end-to-end demonstration paths belong to later tasks in `tasks.md`.

## Team workflow

The design calls for five module owners covering catalog, inventory, customers, sales, identity, and reporting/AI. Assign actual contributors and reviewers before parallel feature work. Cross-cutting contracts (money, errors, identity, audit, migrations, CI) require a reviewer outside the owning module. Commit small feature-scoped migrations, rebase before generating a new migration, and validate a rebuild from an empty database once migrations exist. Use concise, imperative commit subjects naming the module (for example, `inventory: add receipt model`).
