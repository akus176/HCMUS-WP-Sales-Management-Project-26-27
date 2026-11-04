# Staff mobile starter (#138)

Strict TypeScript, Expo SDK 58, React Native 0.88, and Expo Router. The only route
is a public starter screen. Authentication, API calls, generated clients, and
staff workflows belong to later issues.

## Install and check

Use **Node 26.5.0** and **npm 11.17.0** (also recorded in `.nvmrc` and
`package.json`). With nvm-windows, run `nvm install 26.5.0` and `nvm use 26.5.0`.
From the repository root:

```powershell
cd apps/staff-mobile
npm ci
npm run check
npm run test:unit
npm run test:components
```

`check` runs strict TypeScript, ESLint, Prettier, and all Jest tests. The unit
command tests the starter component; the component command tests the root route
through Expo Router. Tests live outside `app/` so Router cannot expose them as
screens. Jest fails if no tests are discovered. `npm run format` applies Prettier.
All direct dependencies and the full lockfile are pinned; use `npm ci` for repeat
installs. Run `npx expo install --check` after changing native dependencies.

## Android emulator

**Verification pending:** the emulator boots, but this PR has not yet verified
the native app launch or Maestro flow. The steps below describe the required
reproduction path; see the evidence for the current blocker.

Install Android Studio with its Android SDK, Platform Tools, Emulator, Android
SDK Platform 37.0, Build Tools 37, and an API 36 Google APIs x86_64 system image.
Use JDK 17 and a hardware-accelerated Android virtual device. On Windows, the
Android Emulator must report working WHPX acceleration; do not disable Windows
security features to make it start. Gradle may download its required NDK/CMake
versions during the first build. Accept SDK licenses through the Android tools.

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

## Android E2E smoke test

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

## Shared API convention and scope

The shared API convention is `http://10.0.2.2:5232/api/v1` for the Android emulator;
`10.0.2.2` refers to the host, while emulator `localhost` refers to Android. Check
the host's non-secret `/health` endpoint separately when API integration begins.
There is no API configuration or request in this starter. #139 owns validated
emulator/device/test/release configuration and native networking; #143 owns the
generated client and secure sessions. Never add database credentials, tokens,
or private device addresses to app configuration.

This implements the scaffold/tooling slice of local OpenSpec tasks **1.2/1.3**.
The older parent #95 numbers its scaffold **1.1** and testing foundation **1.4**;
its dependency item **1.2** covers later functionality as well and must stay
unchecked for this PR. #138 remains open for @JakeConal's required review.

See [verification evidence](../../docs/evidence/mobile-scaffold-138/README.md)
for executed commands and the explicitly authorized S0 review waiver.
