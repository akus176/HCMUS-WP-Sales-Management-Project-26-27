# Coffee Shop

A coffee-shop management system with a WinUI 3 desktop app, an Android staff app,
and a shared ASP.NET Core API. PostgreSQL stores shop data; RustFS stores menu
pictures. The desktop app covers Counter, Service board, Menu, Stock room,
Customers, and Reports.

## Prerequisites

- Windows 10 1809 or later, x64, with the .NET 10 SDK.
- PowerShell 7.4 or later.
- Docker Desktop running Linux containers with Docker Compose.
- Python 3.9 or later to import menu pictures and run storage checks.

Android development has separate requirements in the [staff-mobile guide](apps/staff-mobile/README.md).

## Start the API and WinUI on Windows

Follow these steps in PowerShell from a checkout on a Windows drive. The first
setup creates a dedicated `corner_cafe_windows_demo` database for the enriched
28-item menu.

### 1. Configure local settings

```powershell
if (-not (Test-Path -LiteralPath .env)) {
    $settings = (Get-Content -LiteralPath .env.example -Raw).Replace(
        'replace-with-local-password', [Security.Cryptography.RandomNumberGenerator]::GetHexString(48)
    ).Replace(
        'replace-with-a-long-random-local-key', [Security.Cryptography.RandomNumberGenerator]::GetHexString(64)
    ).Replace(
        'replace-with-local-storage-password', [Security.Cryptography.RandomNumberGenerator]::GetHexString(48)
    ).Replace(
        'POSTGRES_DB=coffeeshop', 'POSTGRES_DB=corner_cafe_windows_demo'
    )
    Set-Content -LiteralPath .env -Value $settings -Encoding utf8NoBOM
    Remove-Variable settings
}
```

If `.env` already exists, check that it contains the settings in `.env.example`.
For a fresh Windows demo, set `POSTGRES_DB=corner_cafe_windows_demo` before starting
the backend. Keep existing populated databases for their own workflow; the Windows
base seed below requires an empty database with this name. Change occupied ports
in `.env` and keep local credentials out of Git.

If this checkout already uses another database, changing `POSTGRES_DB` alone does
not create a database in its existing volume. Start this walkthrough with fresh
volumes by setting `$env:COMPOSE_PROJECT_NAME = 'coffeeshop-windows-demo'` in your
PowerShell session. Use unused ports and keep that project name set for subsequent
commands and launches.

### 2. Start the backend

```powershell
docker compose up --build --detach --wait --wait-timeout 180
docker compose ps
```

The API applies migrations automatically. The API, PostgreSQL, and storage
services should be healthy; `storage-init` exits successfully after preparing the
image bucket. The default API address is `http://127.0.0.1:5232`.

### 3. Create the test account

Run once per database. Choose and remember a local password of at least 12
characters at the masked prompt:

```powershell
$env:ProvisionStaff__Password = Read-Host 'Manager password' -MaskInput
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

| Sign-in field | Value |
| --- | --- |
| Username | `local-manager` |
| Password | The password you entered above |
| Role | `Manager` |

Provisioning also creates the staff roles needed by the seed. There is no shared
default password. If the account already exists, use its original password;
provisioning refuses to overwrite an existing username or email. For another test
role, change the username, email, and `ProvisionStaff__Roles` together; see
[staff identity](docs/staff-identity.md#local-setup) for role codes.

### 4. Apply seed data and pictures

Load the Windows base dataset once into the empty demo database, then apply menu
enrichment. Run the next command only after the preceding command succeeds:

```powershell
docker compose cp scripts/postgres/windows-demo-seed.sql postgres:/tmp/windows-demo-seed.sql
docker compose exec -T postgres sh -c 'exec psql -X -v ON_ERROR_STOP=1 --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" --file=/tmp/windows-demo-seed.sql'
docker compose run --rm menu-enrichment
python scripts/storage/import-unsplash-photos.py
```

The result is **28 menu items, 48 sizes, 23 inventory ingredients**, plus sample
customers, tables, orders, and reporting history. The importer restores all **27
Unsplash pictures** to local storage. Each teammate runs these steps locally; no
database dump or copied photo folder is needed.

The Windows base seed refuses a populated database. On later runs, skip that base
seed; menu enrichment can be reapplied, and the photo importer refreshes its named
objects. Seed users such as `history_manager` are history placeholders and cannot
sign in. Use the test account created in step 3.

Use `python3` in WSL/Linux. Pictures are downloaded outside the checkout and
uploaded to RustFS. See [menu enrichment and photo credits](docs/menu-enrichment.md)
for the dataset details.

### 5. Open the desktop app

Build and open the app, then sign in with the test account above:

```powershell
./scripts/run-winui.ps1
```

Open **Menu** to see the enriched catalog, **Stock room** for ingredients, and
**Reports** for the sample history. On later launches, run the same launcher; you
do not need to provision the account or load the base seed again.

`docker compose down` stops the backend while keeping the database and pictures.
Adding `--volumes` deletes that stored data. For connection problems and custom
ports, see [Compose troubleshooting](docs/docker-compose.md#troubleshooting).

## Alternative 20-item fixture

For the separate midterm dataset, use a fresh Compose project and set
`POSTGRES_DB=coffeeshop` in its `.env`, start the backend, and run:

```powershell
docker compose run --rm demo-data
```

This fixture replaces its own sample rows on rerun. Its categories differ from
the Windows demo, so use it separately from `windows-demo-seed.sql` and
`menu-enrichment`. It does not create an API sign-in; provision a test account as
in step 3. See [Compose demo data](docs/docker-compose.md#demo-dataset).

## Build and test

```powershell
dotnet build CoffeeShop.slnx -c Release
dotnet format CoffeeShop.slnx --verify-no-changes --no-restore
dotnet test tests/CoffeeShop.WinUI.Tests -c Release
./scripts/verify-winui-api-release.ps1 -SkipBuild
python scripts/verify-menu-photo-import.py
```

The storage check uses a disposable volume and verifies imports, access rules,
replacement preservation, and persistence. Database integration tests require
`COFFEESHOP_TEST_POSTGRES` pointing to a disposable administrator connection.
See [development and migration commands](docs/docker-compose.md#development-and-migrations)
and the [quality report](docs/final-quality-report.md) for the complete test matrix.

## Documentation

| Topic | Guide |
| --- | --- |
| Local services and troubleshooting | [Docker Compose](docs/docker-compose.md) |
| Menu seeds and photo sources | [Menu enrichment](docs/menu-enrichment.md) |
| Android setup and checks | [Staff-mobile app](apps/staff-mobile/README.md) |
| Roles and account provisioning | [Staff identity](docs/staff-identity.md) |
| API request collection | [API tests](tests/api/README.md) |
| UI structure and theme | [UI foundation](docs/ui-foundation.md), [theme](docs/luxury-theme.md) |
| Legacy direct database setup | [Restricted PostgreSQL role](docs/midterm-postgres-role.md) |
| Specifications and delivery evidence | [OpenSpec](openspec/changes/), [final submission](docs/final-submission/), [quality report](docs/final-quality-report.md) |

## Team

| Student ID | Member | GitHub |
| --- | --- | --- |
| 23120124 | Nguyễn Minh Hiếu | [JakeConal](https://github.com/JakeConal) |
| 23120111 | Phạm Quốc Nam Anh | [kyrie25](https://github.com/kyrie25) |
| 23120143 | Tô Thành Long | [longtoZ](https://github.com/longtoZ) |
| 23120097 | Võ Tất Toàn | [vt-Toan](https://github.com/vt-Toan) |
| 23120135 | Trần Anh Khoa | [akus176](https://github.com/akus176) |

Track ownership and review in [GitHub issues](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues).
Keep commits focused and reference the issue they implement. Rebase before creating
migrations and verify changes against a fresh database.
