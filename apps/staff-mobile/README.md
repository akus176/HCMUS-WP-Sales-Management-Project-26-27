# Staff mobile development

Strict TypeScript, Expo SDK 58, React Native 0.88, and Expo Router. The public
starter links to staff sign-in. An authenticated session screen supports sign-out;
operational workflows and the permission-aware module shell belong to later issues.

## Install and check

For the complete native workflow suite, P0 accessibility matrix, synthetic API
setup, device notifications, release and rollback gates, use the
[#186 contributor runbook](../../docs/mobile-quality-and-release.md).

Use **Node 26.5.0** and **npm 11.17.0** (also recorded in `.nvmrc` and
`package.json`). With nvm-windows, run `nvm install 26.5.0` and `nvm use 26.5.0`.
From the repository root:

```powershell
npm ci --prefix contracts/mobile-p0
cd apps/staff-mobile
npm ci
npm run api:generate
npm run api:check
npm run check
npm run test:unit
npm run test:components
```

`check` runs strict TypeScript, ESLint, Prettier, and all default Jest tests. The
unit command covers transport, rotation and secure storage; the component command
covers sign-in and protected Expo Router navigation. Tests live outside `app/` so Router cannot expose them as
screens. Jest fails if no tests are discovered. `npm run format` applies Prettier.
All direct dependencies and the full lockfile are pinned; use `npm ci` for repeat
installs. Run `npx expo install --check` after changing native dependencies.

### CI quality checks (#141)

The `mobile-quality` job in `.github/workflows/ci.yml` uses the versions above
and runs the following from `apps/staff-mobile` after a clean checkout:

```powershell
npm ci
npm run type-check
npm run lint
npm run format:check
npm test -- --ci --json --outputFile=mobile-test-results.json
$env:EXPO_PUBLIC_APP_ENV = 'emulator'
$env:EXPO_PUBLIC_API_BASE_URL = 'http://10.0.2.2:5232/api/v1'
npm run smoke:android
```

The smoke check exports an Android JavaScript bundle to ignored
`dist/android-smoke`. It checks bundling and configuration only: it does not
build an APK, start an emulator, or run Maestro. Use the Android emulator and
E2E instructions below for native launch evidence. The other CI jobs continue
to run independently, including the shared contract drift and backend checks.
CI uploads the ignored Jest JSON report as `mobile-test-results`, including
the actual suite and test totals from that run.

## Generated client and sessions (#143)

`api:generate` uses the existing **openapi-typescript 7.10.1** installation and
lockfile in `contracts/mobile-p0` to regenerate its `schema.d.ts` from the
committed `openapi.json`. `api:check` fails on a generated diff. The app's
`src/api/schema.ts` re-exports those generated types; **openapi-fetch 0.17.0**
provides the typed client. Do not hand-edit generated schemas. Updating the
snapshot from the running API remains the shared contract pipeline documented
in [the API baseline](../../docs/mobile-api-contract-137.md). The generator's
TypeScript 5 dependency stays isolated from the app's TypeScript 6 dependency.

Set the non-secret URL **before starting Metro**, in its terminal:

```powershell
$env:EXPO_PUBLIC_APP_ENV = 'emulator'
$env:EXPO_PUBLIC_API_BASE_URL = 'http://10.0.2.2:5232/api/v1'
```

The URL must end in `/api/v1`, without credentials, query or fragment.
[Environment and device-network setup](../../docs/mobile-environment-139.md)
defines emulator, physical-device, test and release profiles. Copy the app's
`.env.example` to ignored `.env.local` for local setup, or set both public values
in the terminal. Build-time validation rejects invalid configuration, and the
runtime startup gate blocks session restoration/navigation with a safe message.
Release bundles require the release profile and a public HTTPS endpoint.
`EXPO_PUBLIC_*` values are bundled publicly; never place credentials or tokens in them.

The API must be running with its database, JWT configuration and an explicitly
provisioned active staff account; the app creates no accounts and seeds no data.
Access tokens stay in memory. Only refresh tokens and their API origin are saved
in Expo SecureStore with device-only accessibility; Android backup excludes the
SecureStore data. A saved token is never sent to a different API origin. Startup
rotates the saved refresh token before showing protected content.

One refresh mutex covers concurrent and delayed old-token 401s. Safe reads may
retry once for network/502/503/504 errors and replay once after renewal. Writes,
login, and refresh are never retried automatically. A write that receives 401
may renew the session, but still returns its original error for an explicit
user retry. Errors preserve RFC 7807 fields and use generic UI messages.

Logout and terminal renewal failure immediately clear user/access state,
cancel requests and queries, clear the TanStack Query cache, and remove protected
navigation history. Responses finishing after cleanup are discarded. Serialized
secure-storage writes ensure a late rotation cannot restore a deleted token.
Logout also attempts server revocation, with a ten-second timeout; local cleanup
still runs offline. A secure-storage deletion failure is reported for retry.
Transient renewal network/server failures preserve the saved token for a later
attempt; protected content is not shown if startup renewal fails.

The default suite uses synthetic transport fixtures. To verify the real protocol
against an isolated local API, provision a disposable account using the shared
API's documented provisioning command and set these process-local variables:

```powershell
$env:COFFEESHOP_MOBILE_TEST_API_BASE_URL = 'http://127.0.0.1:5232/api/v1'
$env:COFFEESHOP_MOBILE_TEST_USERNAME = '<disposable account>'
# Set COFFEESHOP_MOBILE_TEST_PASSWORD locally without committing it.
npm run test:live
```

This opt-in test requires loopback, rejects invalid login, checks `/auth/me`,
rotates a saved token, and verifies logout revocation. Its storage adapter is
in memory; it does not prove device SecureStore behavior.

## Android emulator

The native build, installed starter screen, and Maestro smoke flow were verified
on an API 36 emulator. See the linked evidence for results and a screenshot.

Install Android Studio with its Android SDK, Platform Tools, Emulator, Android
SDK Platform 37.0, Build Tools 37, and an API 36 Google APIs x86_64 system image.
Use JDK 17 and a hardware-accelerated Android virtual device. On Windows, the
Android Emulator must report working WHPX acceleration; do not disable Windows
security features to make it start. Gradle may download its required NDK/CMake
versions during the first build. Accept SDK licenses through the Android tools.

On Windows, use a short physical checkout path such as `F:\cs138` before
installing dependencies or generating native projects. Deep workspace paths can
exceed native-build path limits. A `subst` drive alone does not fix this because
native dependencies resolve their physical paths.

The SDK's CMake 3.22.1 bundles Ninja 1.10.2, which rejects long generated
filenames even from a short checkout. Use the Windows executable from
[Ninja 1.13.2](https://github.com/ninja-build/ninja/releases/tag/v1.13.2) in
`<SDK>\cmake\3.22.1\bin\ninja.exe`, keeping a backup of the original executable.
The `ninja-win.zip` SHA256 is
`07fc8261b42b20e71d1720b39068c2e14ffcee6396b76fb7a795fb460b78dc65`.
Verify `ninja.exe --version` prints `1.13.2`. The verified Windows host already
has `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled=1`.

Create and start an API 36 phone in Android Studio's Device Manager. In
PowerShell, set the SDK/JDK paths for the current terminal (adjust JDK location):

```powershell
$env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"
$env:JAVA_HOME = '<local JDK 17 directory>'
$env:PATH = "$env:JAVA_HOME\bin;$env:ANDROID_HOME\platform-tools;$env:PATH"
$env:REACT_NATIVE_PACKAGER_HOSTNAME = '127.0.0.1'
adb devices -l
npm run android
```

Run this from `apps/staff-mobile`. `android` generates ignored native projects,
builds and installs the debug app, starts Metro, and opens **Coffee Shop Staff**.
The first build downloads Gradle and native artifacts. Keep Metro running for
debug builds. The starter must show **Staff mobile** and **No staff account or
shop data is loaded.** Stop Metro with Ctrl+C when finished.

For an already installed build, `npm start` starts Metro on localhost. If needed,
run `adb reverse tcp:8081 tcp:8081`, then reopen the app on the emulator. Native
projects are generated from `app.json`; do not commit `android/` or `ios/`.

If Metro is already running in another terminal, use
`npm run android -- --no-bundler` to build, install, and open the app without
starting a second server. On a memory-constrained host, set
`$env:GRADLE_OPTS = '-Dorg.gradle.workers.max=2 -Dorg.gradle.parallel=false'`
and `$env:CMAKE_BUILD_PARALLEL_LEVEL = '2'` for the current terminal first.

## Android E2E smoke test

For cashier payment and manager cancellation/assignment, see the
[#182 workflow evidence and reproduction steps](../../docs/evidence/mobile-order-operations-182/README.md).
The flows use synthetic data in a disposable shared API and verify server-confirmed outcomes.

Install **Maestro CLI 2.12.1** from the official
[release](https://github.com/mobile-dev-inc/Maestro/releases/tag/cli-2.12.1).
Extract its distribution outside this repository and put its `bin` directory on
PATH (`maestro.bat` on Windows). `maestro --version` must print `2.12.1`.
With the debug app installed, exactly one emulator connected, and Metro running:

```powershell
npm run test:e2e:android
```

The flow clears and launches `com.coffeeshop.staffmobile`, waits for the starter,
and asserts its visible text. JUnit results go to ignored
`.maestro/results/junit.xml`. A disconnected emulator, missing CLI, or missing
starter fails the command; a JS test or successful bundle is not Android launch
evidence. This smoke flow is the foundation for later authenticated E2E flows.

The opt-in session flow is `npm run test:e2e:session`. With Metro configured and
the real API reachable, set `MAESTRO_STAFF_USERNAME`, `MAESTRO_STAFF_PASSWORD`,
and `MAESTRO_STAFF_NAME` for a **disposable test account** in the test terminal.
The flow checks rejected/valid login, native process restart with session
restoration, and logout. Reports/screenshots go to `.maestro/results/`.
Maestro debug artifacts can record input, so use only isolated test credentials.
This session flow has not been executed here: automatic approval review rejected
starting Metro, returning only `blocked by policy`. See the #143 evidence for
the executed checks and this runtime limit.

## Shared API convention and scope

The shared API convention is `http://10.0.2.2:5232/api/v1` for the Android emulator;
`10.0.2.2` refers to the host, while emulator `localhost` refers to Android.
With the configured emulator and API running, `npm run check:android-health`
checks the real shared API from Android's network stack without a staff account.
See [the #139 setup guide](../../docs/mobile-environment-139.md) for physical-device
LAN, HTTPS, cleartext policy and troubleshooting. The session client uses the
same validated configuration. Never add database credentials, tokens, or actual
private device addresses to tracked configuration.

The scaffold is #138 (local OpenSpec **1.2/1.3**). #143 implements local Mobile
**3.1–3.4**, corresponding to the older canonical #95 **3.1–3.5**. Business
workflows, permission module navigation (#144), and offline write handling are
outside #143. Its issue remains open for @JakeConal's required review.

See [verification evidence](../../docs/evidence/mobile-scaffold-138/README.md)
for the scaffold and [session evidence](../../docs/evidence/mobile-sessions-143/README.md)
for #143's executed commands and limits.

## Shared mobile design foundation (#140)

`src/shell/designTokens.ts` exports the mobile color, typography, spacing, shape,
and elevation tokens. The shell loads the repository's OFL-licensed Kalam and
Patrick Hand fonts before displaying authenticated routes. `src/shell/controls.tsx`
exports shared responsive page/text/heading components, state feedback, and
accessible 48-dp buttons for S2 workflow screens. Controls preserve system font
scaling, wrap at enlarged sizes, and expose loading, empty, error, warning,
selected, disabled, and busy states.

See [#140 design-system evidence](../../docs/evidence/mobile-design-system-140/README.md)
for contrast ratios, touch-target checks, component coverage, and the native
accessibility verification limits.

## Connectivity and online-only writes (#145)

The app root observes device connectivity through Expo Network and displays
checking, online, reconnecting, and offline feedback. Manual refresh/check actions
invalidate active TanStack Query results; business data remains in the in-memory
query cache and the shared API remains authoritative. State-changing requests must
use `src/connectivity/connectivity.tsx`'s `useSafeMutation`; it disables writes
while offline/reconnecting, rejects before sending, never pauses/replays a write,
and refreshes server data after success or a `409` conflict. See [#145 evidence](../../docs/evidence/mobile-connectivity-145/README.md)
for the test cases and native verification limits.

## Permission-aware shell (#144)

After sign-in, `/session` lists only modules permitted by effective API permission
codes. Role names are never authorization inputs. `/modules/[module]` rechecks
`/api/v1/auth/me` before mounting module content, including on route focus and app
resume. Unknown routes, malformed/cross-account responses and missing grants fail
closed. Failed checks offer retry without displaying protected content. The API
remains authoritative for every later business request.

The Preparation module now hosts the #155 barista workflow described below.
Other modules still label their business screens/actions **planned**.
The home also checks current server permissions before displaying navigation,
including when a previously empty home regains focus or the app resumes.
Home verification uses an explicit scope, so `/modules/home` is not a valid module.

The permission-aware shell uses these shared controls and preserves the sign-in
palette. Its scrollable phone/tablet layout, scalable text, accessible labels,
48-dp action targets, and focus/selection feedback are covered by the #140
foundation and the #144 shell integration evidence.

`npm test` includes the six-backend-role policy matrix, module action filtering,
real-router deep links, revocation, permission-check failures, terminal sessions,
background/resume and late-response checks. [Evidence and remaining verification](../../docs/evidence/mobile-shell-144/README.md) distinguish browser visual fixtures
from native Android checks. Required reviewer: @vt-Toan. Contributor: @akus176
(with Codex assistance); the existing ticket owner remains @JakeConal.

## Barista preparation (#155)

Open **Preparation** from staff home with `orders.fulfill`. The queue filters
confirmed/preparing tickets and paginates ten tickets at a time. Open a ticket
to inspect its items, sizes, quantities, add-ons and table. **Start preparing**
and **Mark ready** each require confirmation and a successful service response.
Success and conflicts refresh authoritative data; a ticket that has left the
queue exposes no further action. Shared connectivity feedback and manual refresh
remain available, while offline/reconnecting writes are disabled and never queued.

The feature uses the generated API client, permission boundary, shared controls
and in-memory TanStack Query cache. It adds no backend transitions, database
access, persistent business storage or live-event subscription.

Run `npm run check` for automated coverage. The opt-in Android flow is
`npm run test:e2e:barista`; its disposable API fixture, exact setup, executed
results and remaining emulator acceptance gate are in [#155 evidence](../../docs/evidence/mobile-barista-155/README.md).
