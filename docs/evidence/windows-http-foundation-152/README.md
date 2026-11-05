# Windows HTTP foundation (#152)

Implementation branch: `codex/windows-http-foundation-152`  
Issue owner: `akus176`  
OpenSpec tasks: 11.1, 11.2, 11.6 in `build-coffee-shop-management-system`

## Implemented

- Added a typed identity API adapter for `/auth/login`, `/auth/refresh`, and `/auth/logout`, a reusable authenticated HTTP client, typed problem response mapping, and network error mapping.
- Added an initial WinUI sign-in window, access-token attachment, one-time retry after refresh, serialized refresh-token rotation, expired-session cleanup, and sign-out.
- Persisted only the refresh token in a DPAPI-protected file under `%LOCALAPPDATA%/CoffeeShop`; access tokens remain in memory. API base URL configuration accepts `COFFEESHOP_API_BASE_URL` or `Api.BaseUrl`, defaulting to the local API launch URL. No token is written to appsettings or emitted through a logger.
- Derived workspace navigation from API-issued permissions and restricted stock-management controls by permission. API endpoints remain protected independently of client visibility.

## Verification

Commands run from the repository root:

```text
dotnet test tests/CoffeeShop.WinUI.Tests/CoffeeShop.WinUI.Tests.csproj --configuration Release --no-restore -m:1
Passed: 126, Failed: 0, Skipped: 0.

dotnet format tests/CoffeeShop.WinUI.Tests/CoffeeShop.WinUI.Tests.csproj --verify-no-changes --no-restore
Passed.

dotnet test tests/CoffeeShop.Api.Tests/CoffeeShop.Api.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~HostTests -m:1
Passed: 8, Failed: 0, Skipped: 0. Includes anonymous GET checks for /api/v1/auth/me and /api/v1/categories returning 401.

dotnet format tests/CoffeeShop.Api.Tests/CoffeeShop.Api.Tests.csproj --verify-no-changes --no-restore
Passed.

dotnet build tests/CoffeeShop.Infrastructure.Tests/CoffeeShop.Infrastructure.Tests.csproj --configuration Release --no-restore -m:1
Succeeded: 0 warnings, 0 errors. Confirms the shared WinUI composition helper still compiles in the PostgreSQL test project.
```

The broader database-backed API filter selected 21 tests; all 21 skipped because PostgreSQL/Testcontainers is unavailable in this environment. The Windows WinUI project restored with `EnableWindowsTargeting=true`, but Linux cannot execute the Windows `XamlCompiler.exe` (`Exec format error`), so the WinUI application build and interactive startup could not be verified here.

## Remaining acceptance evidence

OpenSpec task 11.2 remains unchecked. The sign-in and refresh flows have mocked HTTP coverage, but the issue requires a Windows WinUI login against the real API. Run the app on Windows with the API/database available, verify login, restart/refresh restore, expiry cleanup, and logout cleanup, then attach that result before checking 11.2 or marking issue #152 ready for closure.
