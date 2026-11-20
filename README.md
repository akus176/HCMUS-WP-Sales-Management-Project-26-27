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

Run from a checkout on a Windows drive. Create your local settings and start the
backend:

```powershell
if (-not (Test-Path -LiteralPath .env)) {
    $settings = (Get-Content -LiteralPath .env.example -Raw).Replace(
        'replace-with-local-password', [Security.Cryptography.RandomNumberGenerator]::GetHexString(48)
    ).Replace(
        'replace-with-a-long-random-local-key', [Security.Cryptography.RandomNumberGenerator]::GetHexString(64)
    ).Replace(
        'replace-with-local-storage-password', [Security.Cryptography.RandomNumberGenerator]::GetHexString(48)
    )
    Set-Content -LiteralPath .env -Value $settings -Encoding utf8NoBOM
    Remove-Variable settings
}
docker compose up --build --detach --wait --wait-timeout 180
docker compose ps
```

The API applies migrations automatically. The API, PostgreSQL, and storage
services should be healthy; `storage-init` exits successfully after preparing the
image bucket. The default API address is `http://127.0.0.1:5232`.
Change occupied ports in `.env`; keep local credentials out of Git.

Create a manager sign-in with a unique password of at least 12 characters:

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

Build and open the desktop app, then sign in as `local-manager`:

```powershell
./scripts/run-winui.ps1
```

`docker compose down` stops the backend while keeping the database and pictures.
Adding `--volumes` deletes that stored data. For connection problems and custom
ports, see [Compose troubleshooting](docs/docker-compose.md#troubleshooting).

## Sample data and pictures

For the standard 20-item sample catalog on a disposable database:

```powershell
docker compose run --rm demo-data
```

This fixture replaces its sample rows on rerun. The separate
[Windows menu enrichment guide](docs/menu-enrichment.md) covers the 28-item
Windows demo catalog and its prerequisites.

Import all 27 seed pictures from Unsplash into local storage:

```powershell
python scripts/storage/import-unsplash-photos.py
```

Use `python3` in WSL/Linux. Pictures are downloaded outside the checkout and
uploaded to RustFS. Manage them in the [storage console](http://127.0.0.1:9001)
using the storage credentials from `.env`. See [photo storage](docs/docker-compose.md#menu-photo-storage)
for upload commands and [photo credits](docs/menu-enrichment.md#unsplash-photograph-credits).

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
