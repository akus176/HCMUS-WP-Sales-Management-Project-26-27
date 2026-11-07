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

OpenSpec task 11.2 remains unchecked. The Windows and live API checks below confirm build, startup, API reachability, and API authentication. They do not yet verify the WinUI login form, DPAPI persistence, app restart/refresh restore, expiry recovery, or sign-out cleanup. Complete those app-level checks before checking task 11.2 or marking issue #152 ready for closure.

## Windows and live API validation — 2026-11-05

This run supplements the earlier Linux-only evidence. It used PR head `5bae215242bde5aba0c36d579f8cda579de2afa5` on a Windows 11 host, .NET SDK `10.0.403`, and Docker Compose from Ubuntu 24.04. The Compose stack used an isolated PostgreSQL 18 volume and an API image built from this branch. The API and database were healthy. Temporary test ports were `15232` (API) and `15432` (PostgreSQL).

### Verified

- `dotnet build src/CoffeeShop.WinUI/CoffeeShop.WinUI.csproj --configuration Debug --verbosity minimal` — succeeded with 0 warnings and 0 errors.
- `dotnet run --no-build --project src/CoffeeShop.WinUI/CoffeeShop.WinUI.csproj --configuration Debug` — opened the actual `Corner Cafe · Sign in` WinUI window.
- From Windows, API health and readiness returned HTTP 200 (`Healthy`).
- A disposable local Manager account passed live API checks: login 200; `/auth/me` 200; refresh 200 with refresh-token rotation; logout 204; replay of the revoked refresh token 401.
- The empty sign-in screen is captured in [windows-sign-in-2026-11-05.jpg](windows-sign-in-2026-11-05.jpg).

No password, access token, refresh token, signing key, or database password is included in this evidence. The test account and database are local disposable test data.

### Remaining manual acceptance

The live API authentication check does not prove the WinUI sign-in form. The app is open at the sign-in screen, but a person must submit the temporary test credentials locally. After that, verify and capture the DPAPI refresh-token file, app restart/refresh, expiry recovery, and sign-out cleanup. These checks are pending. Keep PR #176 in draft and do not request review until this app-level evidence is complete.

## Windows follow-up — 2026-11-07

On the PR branch, `dotnet test tests/CoffeeShop.WinUI.Tests/CoffeeShop.WinUI.Tests.csproj --configuration Release -m:1` passed 126 tests with no failures or skips. `dotnet build src/CoffeeShop.WinUI/CoffeeShop.WinUI.csproj --configuration Debug --verbosity minimal -m:1` succeeded with no warnings or errors.

A uniquely named Docker Compose project was started with a fresh PostgreSQL volume and generated local-only settings. Both services reported healthy, and the API readiness endpoint returned HTTP 200 from Windows. A disposable Manager was provisioned, and the WinUI sign-in window opened against that isolated stack. The existing user application settings and unrelated database were not changed.

The first container startup failed because Windows checked out the Bash startup scripts with CRLF endings. The repository now specifies LF for shell scripts; after normalizing those scripts, rebuilding and restarting the isolated stack succeeded. This verifies the local recovery, not a fresh checkout on another machine.

The computer-use native connection was unavailable for UI inspection, and the sign-in form requires a human handoff. Form sign-in, protected token persistence, restart, expiry and sign-out remain pending until observed; prior API-only evidence must not be treated as app-level proof.
