# Coffee Shop

WinUI 3 desktop client and ASP.NET Core API for a coffee-shop management system. The midterm client supports direct PostgreSQL workflows for catalog, customers, inventory, orders, fulfillment, and reports. The final API architecture remains in progress. The behavior and milestone plan are in `openspec/changes/build-coffee-shop-management-system/`; the client theme is in `docs/luxury-theme.md`.

## Prerequisites

- Windows 10 1809 or later, x64, and the .NET 10 SDK. Visual Studio with Windows App SDK tooling is optional for the commands below.
- PowerShell 7.4 or later for local setup.
- Docker Desktop with its Linux engine running and the `docker compose` plugin available. Compose supplies PostgreSQL 18 and the API. Install `psql` separately if you use the direct PostgreSQL instructions.

## Start the API and WinUI on Windows

Run from the root of a clean checkout in PowerShell. Create `.env` once per checkout with locally generated credentials. The setup uses the default loopback ports 5432 for PostgreSQL and 5232 for the API. If needed, edit `POSTGRES_PORT` or `API_PORT` in `.env` before startup and update the URLs in the examples below.

```powershell
docker info --format '{{.OSType}}'  # linux
docker compose version
if (-not (Test-Path -LiteralPath .env)) {
    $settings = (Get-Content -LiteralPath .env.example -Raw).Replace(
        'replace-with-local-password', [Security.Cryptography.RandomNumberGenerator]::GetHexString(48)
    ).Replace(
        'replace-with-a-long-random-local-key', [Security.Cryptography.RandomNumberGenerator]::GetHexString(64)
    )
    Set-Content -LiteralPath .env -Value $settings -Encoding utf8NoBOM
    Remove-Variable settings
}
git check-ignore -- .env
docker compose config --quiet
docker compose up --build --detach --wait --wait-timeout 180
docker compose ps
Invoke-RestMethod http://127.0.0.1:5232/health
Invoke-RestMethod http://127.0.0.1:5232/health/ready
```

Check for `healthy` on both services and `Healthy` from both HTTP requests. The API applies committed EF Core migrations on startup. The PostgreSQL named volume retains data across restarts. Keep `.env` out of commits and GitHub attachments. A team copy belongs only in the private channel restricted to the five project members.

For a fresh disposable demo database, load the sample catalog, stock, customers, and sales after both services are healthy. The seed replaces its sample rows on rerun, so use it before making demo changes. See the [Compose guide](docs/docker-compose.md#demo-dataset) for counts and the clean-volume check.

```powershell
docker compose run --rm demo-data
```

Provision a disposable `Manager` sign-in. Use a unique local password of at least 12 characters and enter it at the masked prompt. To provision another account on the same volume, choose a different username and email. The provisioning command installs the role bundles if needed.

```powershell
$env:ProvisionStaff__Password = Read-Host 'Disposable staff password' -MaskInput
try {
    docker compose exec -T `
      -e ProvisionStaff__Username=local-manager `
      -e ProvisionStaff__Email=local-manager@example.test `
      -e ProvisionStaff__FullName=LocalManager `
      -e ProvisionStaff__Roles=Manager `
      -e ProvisionStaff__Password api dotnet CoffeeShop.Api.dll --provision-staff
    if ($LASTEXITCODE -ne 0) { throw 'Staff provisioning failed.' }
} finally {
    Remove-Item Env:ProvisionStaff__Password
}
```

Launch the Release client, sign in as `local-manager`, and open **Menu** to see the sample data if you loaded it. The client uses the API URL with `/api/v1/` and a trailing slash. The [staff identity guide](docs/staff-identity.md) explains the six roles and how to provision accounts with different permissions.

```powershell
$env:COFFEESHOP_CLIENT_MODE = 'api'
$env:COFFEESHOP_API_BASE_URL = 'http://127.0.0.1:5232/api/v1/'
dotnet run --project src/CoffeeShop.WinUI -c Release -r win-x64 -p:WindowsAppSDKSelfContained=true
```

Close WinUI and run `docker compose down` to stop the services and keep the database. Use `docker compose down --volumes` only to delete your disposable database and start again. For port conflicts, unhealthy services, or sign-in issues, see [Compose troubleshooting](docs/docker-compose.md#troubleshooting).

## Build and test

```powershell
dotnet restore CoffeeShop.slnx
dotnet build CoffeeShop.slnx
dotnet test CoffeeShop.slnx
dotnet run --project src/CoffeeShop.Api
```

The API foundation and its current routes are documented in [the shared API foundation guide](docs/api-foundation.md). Use the Compose setup above for a configured backend. `dotnet run --project src/CoffeeShop.Api` requires its own connection and JWT environment settings when running outside Compose.

## Midterm direct PostgreSQL mode

WinUI in direct mode hosts Application services and PostgreSQL infrastructure in-process. Apply migrations and load the sample data into a dedicated local database using the administrator account, then follow the [restricted midterm PostgreSQL setup](docs/midterm-postgres-role.md) to provision the `coffeeshop_midterm` runtime login. Configure that restricted connection with `ConnectionStrings__CoffeeShop` or a user-local `%LOCALAPPDATA%\CoffeeShop\appsettings.json` file containing:

```json
{
  "ConnectionStrings": {
    "CoffeeShop": "Host=127.0.0.1;Port=5432;Database=coffeeshop;Username=coffeeshop_midterm;Password=<local-password>"
  }
}
```

The environment variable takes precedence. Keep this user-local file out of source control. Launch the client and select an operator in the midterm UI.

```powershell
$env:COFFEESHOP_CLIENT_MODE = 'direct'
dotnet run --project src/CoffeeShop.WinUI -c Release -r win-x64 -p:WindowsAppSDKSelfContained=true
```

## Project boundaries

The current client uses six workspaces: Counter, Service board, Menu, Stock room, Customers, and Reports. The [native foundation and shell guide](docs/ui-foundation.md) explains the shared controls and verification commands. [OpenSpec issue #111](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/111) tracks the UI migration; [issue #119](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/119) tracks the final all-workspace visual audit.

| Project                              | Responsibility                                                        |
| ------------------------------------ | --------------------------------------------------------------------- |
| `CoffeeShop.Domain`                  | Business entities and rules; no framework or persistence reference    |
| `CoffeeShop.Application`             | Use cases and abstractions; references Domain only                    |
| `CoffeeShop.Contracts`               | API request/response contracts; independent of persistence            |
| `CoffeeShop.Infrastructure.Postgres` | EF Core/Npgsql persistence and schema migrations                      |
| `CoffeeShop.WinUI`                   | Desktop presentation with API and midterm direct modes                 |
| `CoffeeShop.Api`                     | HTTP services, staff authentication, and server-side authorization    |
| `tests/*`                            | Unit, PostgreSQL integration, API, and WinUI presentation tests       |

In API mode WinUI calls the shared HTTP services, which own authorization and persistence. The current WinUI project still includes the midterm direct composition for local use. The [canonical implementation checklist](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/26) tracks the remaining final delivery work.

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
customer/delivery selection are available under **Customers**. Walk-ins
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

Compose reads `.env` and applies migrations when the API starts. For manual EF Core operations outside Compose, set the administrator connection string in a separate PowerShell session. EF and WinUI do not read Compose's `.env`.

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

The SQL seed supplies two demo operators for direct mode. Neither can sign in through the API. For API mode, provision a local account with one or more of `Admin`, `Manager`, `Cashier`, `Barista`, `InventoryStaff`, or `DeliveryStaff` using the command above.

## Midterm demonstration

Follow the [executed demonstration checklist](docs/midterm-demonstration.md) and [midterm test-case report](docs/midterm-test-cases.md) for inputs,
actual results, native client evidence, defect resolutions, and reproduction instructions for #103 and #104.
With `COFFEESHOP_TEST_POSTGRES` set to an isolated PostgreSQL administrator connection, run:

```powershell
./scripts/verify-midterm.ps1
```

The script restores, builds, checks formatting, and runs all five test projects. Its PostgreSQL
tests create temporary databases and restricted runtime logins, exercise the actual client
composition/workspaces, and remove their own test resources. Missing database configuration,
failed tests, or skipped checks fail verification. Per-project TRX evidence is written to
`artifacts/midterm/`; CI also retains PostgreSQL test results.

The [midterm submission files](docs/midterm-submission/) contain build instructions and an honest
feature summary. `scripts/prepare-midterm-submission.ps1` creates the required five-file archive
outside the repository after a team member supplies an unlisted YouTube URL and a read-only
repository token stored outside the repository. Never commit `pat.txt` or the submission archive.

## Final staff-mobile delivery

The [Expo staff-mobile starter](apps/staff-mobile/README.md) documents pinned tooling,
local checks, Android launch, and the Maestro smoke test for #138.

The final deliverable adds an Android-first staff companion. The complete product target is in the
[staff-mobile OpenSpec](openspec/changes/add-staff-mobile-app/), with the dependency-ordered
[implementation task list](openspec/changes/add-staff-mobile-app/tasks.md). The team must complete
the P0 shared-state vertical slice before presenting P1 or P2 capabilities as implemented.

Start with the [S0 delivery baseline and dependency map](docs/mobile-delivery-baseline.md)
for verified API readiness, P0 owners, Android/LAN setup, and current blockers.

The [final-submission evidence index](docs/final-submission/) links the mobile function list,
continuous demo script, and evidence-based contribution record. These documents deliberately mark
unimplemented functions and unassigned self-scores as pending rather than claiming completion.

## Team

We are grateful to all team members for their contributions to this project. The table below
shows the current ownership areas and GitHub accounts.

| Student ID | Member | GitHub | Recent midterm scope |
| --- | --- | --- | --- |
| 23120124 | Nguyễn Minh Hiếu | [JakeConal](https://github.com/JakeConal) | Service-console shell, shared UI, and workspace polish |
| 23120111 | Phạm Quốc Nam Anh | [kyrie25](https://github.com/kyrie25) | Menu, Customers, and Reports UI |
| 23120143 | Tô Thành Long | [longtoZ](https://github.com/longtoZ) | Stock room UI and test-case report |
| 23120097 | Võ Tất Toàn | [vt-Toan](https://github.com/vt-Toan) | Midterm demonstration and final UI verification |
| 23120135 | Trần Anh Khoa | [akus176](https://github.com/akus176) | Repository coordination, Counter, and Service board |

Please use the [GitHub issues](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues)
for ownership and review. Keep commits focused and link each commit to the issue it implements.
Please review cross-cutting contracts outside the owning module, rebase before generating migrations,
and verify a rebuild from an empty database.
