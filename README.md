# Coffee Shop

WinUI 3 desktop client and ASP.NET Core API for a coffee-shop management system. The current repository is a **foundation scaffold**, not a functioning POS. The behavior and milestone plan are in `openspec/changes/build-coffee-shop-management-system/`; the client theme is in `docs/sketch-theme.md`.

## Prerequisites

- Windows 10 1809 or later with the Windows App SDK runtime for the unpackaged client
- .NET 10 SDK; Visual Studio with Windows App SDK tooling is useful for client development
- PostgreSQL for future persistence work (not needed to build this scaffold)

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

Midterm runs application use cases inside WinUI against a restricted local PostgreSQL account. Final WinUI must drop its infrastructure reference and use typed HTTP clients; the API then owns authorization and persistence. Neither adapter nor those workflows are implemented yet.

## Local configuration

Copy `.env.example` to an ignored `.env` and replace placeholders when PostgreSQL, JWT, and AI integration are introduced. ASP.NET Core does **not** load `.env` automatically: export the required values as environment variables (double underscores denote nested keys), or configure a local launch profile. Never commit local values. No database, migrations, Docker services, demo accounts, or secrets are present in this scaffold.

Planned operational roles are Admin, Manager, Cashier, Barista, Inventory Staff, and Delivery Staff; role policies are not yet enforced. Docker Compose, migration/seed commands, and the two end-to-end demonstration paths belong to later tasks in `tasks.md`.

## Team workflow

The design calls for five module owners covering catalog, inventory, customers, sales, identity, and reporting/AI. Assign actual contributors and reviewers before parallel feature work. Cross-cutting contracts (money, errors, identity, audit, migrations, CI) require a reviewer outside the owning module. Commit small feature-scoped migrations, rebase before generating a new migration, and validate a rebuild from an empty database once migrations exist. Use concise, imperative commit subjects naming the module (for example, `inventory: add receipt model`).
