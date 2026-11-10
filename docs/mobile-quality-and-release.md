# Mobile verification and contributor runbook

Issue [#186](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/186)
covers Mobile 6.1, 6.2, 6.3 and 6.5. Use a fresh disposable database and synthetic
accounts. The native suite changes orders, stock and one account.

## Clean checkout and tools

Use .NET 10, Node 26.5.0, npm 11.17.0, PowerShell 7, JDK 17, Android SDK and
Maestro 2.12.1. Clone to a short physical Windows path such as `F:\coffee`;
a drive alias may still resolve to a long path in CMake. Follow the
[Android toolchain guide](../apps/staff-mobile/README.md#android-emulator)
for SDK/NDK/CMake, Ninja and emulator setup. Set `JAVA_HOME`, `ANDROID_HOME`
and, for nonstandard AVD storage, `ANDROID_AVD_HOME`. Put Java, SDK
`platform-tools`, `emulator` and Maestro `bin` on the terminal's PATH.
Verify `java -version`, `adb version`, `emulator -accel-check`,
`emulator -list-avds` and `maestro --version` before building.

Resolve the SDK in the terminal that will install the app. Confirm
`Test-Path "$env:ANDROID_HOME/platform-tools/adb.exe"` returns `True` there.
A packaged desktop tool can see a virtualized SDK path that an ordinary terminal
cannot see; use the SDK's physical location instead. An intact SDK does not need
to be downloaded again.

From the repository root:

```powershell
npm ci --prefix contracts/mobile-p0
npm ci --prefix apps/staff-mobile
dotnet build src/CoffeeShop.Api -c Release
# On Windows, select Git Bash rather than WSL Bash for contract generation.
$env:PATH = 'C:\Program Files\Git\bin;' + $env:PATH
npm run check --prefix contracts/mobile-p0
npm run check --prefix apps/staff-mobile
```

## Disposable API and role accounts

Start PostgreSQL 18 using the [repository setup](../README.md), or a new local
cluster. Set `ConnectionStrings__CoffeeShop` to that disposable database in the
API terminal. Do not use an existing shop database. In the same terminal:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Jwt__Issuer = 'mobile-quality-local'
$env:Jwt__Audience = 'mobile-quality-local'
$env:Jwt__SigningKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
# Choose a synthetic password of at least 12 characters; enter it again later.
$credential = [pscredential]::new('synthetic', (Read-Host 'Synthetic test password' -AsSecureString))
$env:ProvisionStaff__Password = $credential.GetNetworkCredential().Password
$api = 'src/CoffeeShop.Api/bin/Release/net10.0/CoffeeShop.Api.dll'
dotnet $api --migrate
if ($LASTEXITCODE) { throw 'Migration failed' }
try {
    foreach ($role in @('Admin','Manager','Cashier','Barista','InventoryStaff','DeliveryStaff','Target')) {
        $env:ProvisionStaff__Username = "quality186$role"
        $env:ProvisionStaff__FullName = "Quality $role 186"
        $env:ProvisionStaff__Email = "quality186$role@example.test"
        $env:ProvisionStaff__Roles = if ($role -eq 'Target') { 'Cashier' } else { $role }
        dotnet $api --provision-staff
        if ($LASTEXITCODE) { throw "Provisioning failed for $role" }
    }
} finally { Get-ChildItem Env:ProvisionStaff__* | Remove-Item }
dotnet $api --urls http://127.0.0.1:5232
```

Provisioning refuses existing accounts. Use a new disposable database when
repeating the whole setup. Through PostgreSQL tooling connected to **that
database**, create one available synthetic table:

```sql
INSERT INTO sales.tables (id, table_number, seats)
VALUES ('00000000-0000-4000-8000-000000000186', 'QUALITY-186', 2);
```

`http://127.0.0.1:5232/health/ready` must return 200. `/health` alone does not
verify database readiness. Users get their permissions through the existing
provisioning command; no authorization rule is bypassed.

## Android launch and complete suite

Boot exactly one API 36 emulator and wait until `adb devices -l` shows `device`,
rather than `offline`. From `apps/staff-mobile` in the Metro terminal:

```powershell
$env:EXPO_PUBLIC_APP_ENV = 'emulator'
$env:EXPO_PUBLIC_API_BASE_URL = 'http://10.0.2.2:5232/api/v1'
npm start
```

In a second terminal, set the same two public values and run:

```powershell
adb reverse tcp:8081 tcp:8081
npm run android -- --no-bundler
npm run check:android-health
$env:COFFEESHOP_MOBILE_TEST_API_BASE_URL = 'http://127.0.0.1:5232/api/v1'
$env:COFFEESHOP_MOBILE_TEST_TABLE_ID = '00000000-0000-4000-8000-000000000186'
$env:COFFEESHOP_MOBILE_TEST_COURIER_NAME = 'Quality DeliveryStaff 186'
$env:COFFEESHOP_MOBILE_TEST_TARGET_USERNAME = 'quality186Target'
foreach ($role in @('Admin','Manager','Cashier','Barista','InventoryStaff','DeliveryStaff')) {
    [Environment]::SetEnvironmentVariable("COFFEESHOP_E2E_${role}_USERNAME", "quality186$role", 'Process')
}
$credential = [pscredential]::new('synthetic', (Read-Host 'Synthetic test password' -AsSecureString))
$env:COFFEESHOP_E2E_PASSWORD = $credential.GetNetworkCredential().Password
try { npm run test:e2e:suite }
finally { Remove-Item Env:COFFEESHOP_E2E_PASSWORD }
```

Keep API and Metro running. On constrained hosts set
`GRADLE_OPTS=-Dorg.gradle.workers.max=2 -Dorg.gradle.parallel=false` and
`CMAKE_BUILD_PARALLEL_LEVEL=2` in the build terminal. Recover System UI ANRs
before retrying; do not remove assertions or count runner failures as passes.
For a physical device, follow the [LAN/TLS guide](mobile-environment-139.md),
use the `device` profile and host private LAN address, bind the API to that
interface, and scope any firewall rule to the intended private network.
`10.0.2.2` is emulator-only. Use USB `adb reverse` or a reachable Metro host.
Verify readiness from Android, not only the host.

The suite seeds fresh records and requires **10 flows**: session/restore/logout,
barista progression, discount approve/reject, inventory adjustment/receipt/history,
courier delivered/failed, cashier payment, cancellation/assignment, administration,
offline no-write/no-replay, and notification permission links. Independent
manager/admin API sessions verify authoritative outcomes afterwards. Each flow
must return success and exactly one passing JUnit case; missing reports, skipped
cases and failed server assertions fail the command. Read ignored
`.maestro/results/quality/summary.json` for exact pass/fail/missing counts.
Repeat with fresh data after a state-changing failure.

Use the P0 flows in that order for a contributor demo. For shared Windows state,
open the same record IDs in the final API-backed Windows client and record its
result separately; an independent API read is not a Windows UI result. Inventory
Windows parity remains tracked by #153. The final recording remains Mobile 6.6.
Raw Maestro debug outputs can contain typed synthetic passwords. Keep them
ignored; publish only reviewed screenshots, sanitized JUnit and aggregate counts.

## Accessibility matrix

`npm run test:accessibility` renders sign-in, home, preparation queue/detail,
approval inbox/detail, inventory balances/adjustment and courier list/detail at
phone 360×800 and tablet 800×1280 dimensions with 200% font scale. It checks
named actions/inputs, selected filters, scalable untruncated text, scrollability
and primary targets. Component rendering does not perform native text layout.

Record the current `adb shell wm size`, `adb shell wm density` and
`adb shell settings get system font_scale` values. For the phone set
`wm size 1080x2400`, `wm density 480`; for the tablet set `wm size 1600x2560`,
`wm density 320`. Set `settings put system font_scale 2.0` for both. Prefix each
command with `adb shell`. Visit every listed screen, the adjustment keyboard and
delivery failure form; scroll to every required action and capture screenshots
showing no overlap, clipping or hidden fields. Enable TalkBack in Android Settings
→ Accessibility and traverse/activate each screen's primary controls. Check names,
roles, selection/disabled/busy state and feedback announcements. A dumped tree
does not prove spoken traversal. Restore the recorded settings afterwards; use
`wm size reset`/`wm density reset` only if no earlier overrides existed.

The [#186 native audit evidence](evidence/mobile-quality-186/README.md) includes
the phone/tablet screen matrix, reproducible role flows, JUnit outcomes and
TalkBack speech-caption sequences. Keep native layout evidence separate from
component semantics and full business-workflow results.

## Notifications and CI

The Android notification flow checks protected links. Delivery through the
deterministic provider is covered separately by backend API tests. With
`COFFEESHOP_TEST_POSTGRES` pointing at a disposable administrative database, run:

```powershell
dotnet test tests/CoffeeShop.Api.Tests -c Release --logger 'trx;LogFileName=api.trx'
```

For actual Android push delivery configure the Expo project ID and native push
credentials outside Git, enable `StaffPush__EnableExpo=true` only on the test API,
and follow the [device procedure](evidence/mobile-events-181.md#development-device-procedure).
Test granted/denied permission, token rotation, foreground/background/locked
generic previews, authorized and revoked deep links, and logout removal. Without
project credentials/provider connectivity, record device delivery as unexecuted;
a deep link is not push-delivery evidence.

CI extends #141/#146. `mobile-quality` checks clean install, TypeScript, lint,
format, Jest and JS export; `mobile-p0-contract` checks API/OpenAPI/TypeScript
drift; `mobile-android` generates native Android and builds an x86_64 debug APK
with JDK 17; `catalog-postgres` runs affected backend tests. Windows and Compose
jobs remain required. APK artifacts do not prove emulator interaction or a signed
production release.

## Release and rollback

Require recorded native P0/P1/accessibility results, passing CI and named reviewer
approval. Record commit, APK hash/certificate, API/migration versions, exact test
counts and device profiles. Use `EXPO_PUBLIC_APP_ENV=release`, an approved public
HTTPS API URL and `EAS_BUILD_PROFILE=production`, then rebuild native configuration
and bundle. The release owner supplies the approved signing identity outside Git
and checks `apksigner verify --print-certs`. Retain the previous approved build;
never distribute a CI debug APK as a production release.

Withdraw internal distribution on regression. Set `StaffPush__EnableExpo=false`
and restart the affected API deployment to stop provider sends; verify readiness
and a Windows read/write afterwards. This does **not** disable registration,
mobile HTTP or hub access. The current backend has no selective mobile
registration/hub gate. The full mobile-only rollback drill remains blocked on
canonical #95 task 11.6; do not invent a switch or claim that drill passed.
Disabling the shared hub or revoking shared users can affect WinUI. Do not drop
shared data or reverse business migrations to roll back a client. Keep release
approval pending until that gate and drill are delivered.
