# Issue #153 WinUI API Migration Evidence

Issue: [#153](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/153)

## Verified

- `dotnet build src/CoffeeShop.WinUI/CoffeeShop.WinUI.csproj --no-restore -p:EnableWindowsTargeting=true -v:minimal` passed with .NET SDK 10.0.401, 0 warnings, and 0 errors.
- `dotnet test tests/CoffeeShop.WinUI.Tests/CoffeeShop.WinUI.Tests.csproj --no-restore -v:minimal` passed: 133 tests, 0 failed, 0 skipped. The tests include authenticated catalog, customer, inventory adjustment, receipt mapping, and movement requests.
- `dotnet test tests/CoffeeShop.Api.Tests/CoffeeShop.Api.Tests.csproj -v:minimal` passed with .NET SDK 10.0.403 against disposable PostgreSQL 18.6: 39 tests, 0 failed, 0 skipped. The suite covers catalog writes, stock adjustments and receipt posting, customer/address workflows, and authorization/error responses.
- The task-specific API was migrated and provisioned with a disposable local staff account. The credentials and signing key were not saved in the repository or evidence.

## Manual WinUI acceptance evidence: pending

No screenshot or manual parity result is claimed. The desktop host could not reach the API published from Docker in the Ubuntu/WSL environment, so the authenticated WinUI workflows could not be exercised in this run.

Before Issue #153 can be approved, run the WinUI client in API mode against a clean, migrated test database and capture evidence for:

1. Catalog and recipe create, edit, search, and validation/error feedback.
2. Ingredient adjustment, supplier create/edit, receipt line entry/posting, and movement history.
3. Customer create/edit/search, address management, and customer selection.
4. The API-mode configuration and request path that demonstrate these screens do not use direct PostgreSQL access.

Keep Issue #153 open until this evidence exists and its named reviewer records a decision.
