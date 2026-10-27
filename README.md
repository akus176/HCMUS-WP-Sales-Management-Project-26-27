# Coffee Shop

WinUI 3 desktop client and ASP.NET Core API for a coffee-shop management system. The midterm client supports direct PostgreSQL workflows for catalog, customers, inventory, orders, fulfillment, and reports. The final API architecture remains in progress. The behavior and milestone plan are in `openspec/changes/build-coffee-shop-management-system/`; the client theme is in `docs/luxury-theme.md`.

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

The API currently exposes only `/health` (and `/openapi/v1.json` in Development). Run the WinUI project from Visual Studio or use `dotnet run --project src/CoffeeShop.WinUI -r win-x64` on Windows. The desktop client hosts Application services and PostgreSQL infrastructure in-process. Configure its database connection with the `ConnectionStrings__CoffeeShop` environment variable or a user-local `%LOCALAPPDATA%\CoffeeShop\appsettings.json` file containing:

```json
{
  "ConnectionStrings": {
    "CoffeeShop": "Host=127.0.0.1;Port=5432;Database=coffeeshop;Username=coffeeshop_midterm;Password=<local-password>"
  }
}
```

The environment variable takes precedence. Keep this user-local file out of source control.

## Project boundaries

The first stage of the service-console UI migration is documented in [the native foundation and shell guide](docs/ui-foundation.md), including the component gallery, supported routes, and verification commands.

| Project                              | Responsibility                                                        |
| ------------------------------------ | --------------------------------------------------------------------- |
| `CoffeeShop.Domain`                  | Business entities and rules; no framework or persistence reference    |
| `CoffeeShop.Application`             | Use cases and abstractions; references Domain only                    |
| `CoffeeShop.Contracts`               | API request/response contracts; independent of persistence            |
| `CoffeeShop.Infrastructure.Postgres` | EF Core/Npgsql persistence, later migrations                          |
| `CoffeeShop.WinUI`                   | Desktop presentation; composes Application and PostgreSQL for midterm |
| `CoffeeShop.Api`                     | Final HTTP composition root, later authentication and permissions     |
| `tests/*`                            | Unit, infrastructure, API, and future UI-facing tests                 |

Midterm runs application use cases inside WinUI against a restricted local PostgreSQL account. Follow [restricted midterm PostgreSQL setup](docs/midterm-postgres-role.md) to provision `coffeeshop_midterm`, separate migration credentials, and restrict networking to the local machine. Final WinUI must drop its infrastructure reference and use typed HTTP clients; the API then owns authorization and persistence.

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

### Loyalty rules (T5.3)

Configurable earning and redemption calculations enforce active customers,
order eligibility, available balances, and percentage caps. See
[the loyalty calculation contract](docs/loyalty-rules.md) for rounding, settings
validation, and integration with the later transaction/persistence workflows.

### Customer screens (T5.6)

Customer profiles, saved addresses, balances, paginated loyalty history, and next-order
customer/delivery selection are available under **Customers and Loyalty**. Walk-ins
remain available without a profile. See [customer WinUI workflows](docs/customers-winui.md)
for verification and the POS/order-completion integration contracts.

### Discounts (T6.5)

Fixed and percentage requests remain pending until an actor with `discounts.approve`
approves them. Reasons and decision actors/times are recorded, and approval rechecks
the payable total in a transaction. See [discount rules](docs/discount-rules.md) for
rounding, loyalty interaction, permissions, persistence, and migration behavior.

### Cancellation (T6.7)

Order cancellation restores consumed inventory, reverses affected loyalty entries,
records the reason and actor, releases its dine-in table, and stops delivery updates
in one transaction. Retries return a conflict without duplicate effects. See
[cancellation rules](docs/cancellation-rules.md) for states, permissions, migrations,
rollback behavior, and PostgreSQL verification.

## Local PostgreSQL configuration and migrations

Copy `.env.example` to an ignored `.env` and replace the PostgreSQL placeholders. `.env` is a reference only and is **not** loaded automatically, so set the connection string in the current PowerShell session before using Entity Framework Core:

```powershell
$env:ConnectionStrings__CoffeeShop = "Host=localhost;Port=5432;Database=coffeeshop;Username=coffeeshop;Password=<local-password>"
dotnet tool restore
```

Never commit local credentials. The design-time factory uses `ConnectionStrings__CoffeeShop`; the WinUI client accepts that environment variable or its user-local configuration file.

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

For the midterm demonstration, apply migrations and then run the repeatable dataset in
[`scripts/postgres/midterm-seed.sql`](scripts/postgres/midterm-seed.sql). The dataset contents,
run command, and PostgreSQL integration validation are documented in
[`docs/midterm-seed-data.md`](docs/midterm-seed-data.md).

JWT and AI placeholders follow the same environment-variable convention (double underscores denote nested keys). No demo accounts or secrets are committed.

Planned operational roles are Admin, Manager, Cashier, Barista, Inventory Staff, and Delivery Staff; final API role policies are not yet enforced. Docker Compose automation remains future work. The canonical implementation checklist is [issue #26](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/26).

## Midterm demonstration

Follow the [executed demonstration checklist](docs/midterm-demonstration.md) and [midterm test-case report](docs/midterm-test-cases.md) for inputs,
actual results, native client evidence, defect resolutions, and reproduction instructions for #103 and #104.
With `COFFEESHOP_TEST_POSTGRES` set to an isolated PostgreSQL administrator connection, run:

```powershell
./scripts/verify-midterm.ps1
```

The script creates temporary databases and restricted runtime logins, executes the actual
client composition/workspaces, and removes its test databases and logins. Missing database
configuration or skipped checks fail verification. Raw TRX evidence is written to
`artifacts/midterm/`; CI also retains PostgreSQL test results.

## Team workflow

The design calls for five module owners covering catalog, inventory, customers, sales, identity, and reporting/AI. Assign actual contributors and reviewers before parallel feature work. Cross-cutting contracts (money, errors, identity, audit, migrations, CI) require a reviewer outside the owning module. Commit small feature-scoped migrations, rebase before generating a new migration, and validate a rebuild from an empty database once migrations exist. Use concise, imperative commit subjects naming the module (for example, `inventory: add receipt model`).
