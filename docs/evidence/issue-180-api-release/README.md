# Issue #180: API-only WinUI Release evidence

## Automated checks

Run the Release artifact check on Windows:

```powershell
./scripts/verify-winui-api-release.ps1
```

The check builds the WinUI Release project. It then inspects the generated dependency manifest, output files, and assembly. It fails if the artifact contains PostgreSQL infrastructure, Npgsql, Entity Framework, or PostgreSQL connection-setting references.

The configuration regression test is in `tests/CoffeeShop.WinUI.Tests/ApiOnlyReleaseConfigurationTests.cs`. It verifies that API-only configuration reads the API URL only. A legacy direct-mode value and a PostgreSQL connection value do not change the selected mode or provide a connection string.

CI restores the solution in Release mode, builds it, and then runs the artifact check. The Release project does not reference the PostgreSQL infrastructure project. Debug builds retain the direct development adapter.

## Cross-client verification status

The local Release build, configuration regression test, and dependency-manifest inspection passed on 2026-11-10. These checks do not prove a simultaneous Windows and Android session against one live database.

The live cross-client check remains pending. This environment has no running Docker daemon and no Android emulator or `adb`. The local PostgreSQL service accepts connections, but its authentication requires a credential that was not available for this run.

Before closing Issue #180, run the WinUI Release client and the Android staff app against the same API and database. Record evidence for a state update visible in both clients, a server conflict that preserves the newer value, an authorization rejection, and the audit actor recorded for each write. Keep OpenSpec tasks 11.7 and 6.4 unchecked until their complete requirements and review evidence pass.
