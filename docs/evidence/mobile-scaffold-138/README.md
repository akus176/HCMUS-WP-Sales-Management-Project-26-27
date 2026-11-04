# Staff-mobile scaffold evidence (#138)

Recorded on **4 November 2026** from `codex/expo-scaffold-138`, based on
`main` at `c7280a8`. Scope: local Mobile OpenSpec tasks **1.2 and 1.3**.
The native Android build, emulator launch, and Maestro smoke test pass.

## Executed verification

Commands run from `apps/staff-mobile` on Windows 11:

| Command                                          | Observed result                                                                                 |
| ------------------------------------------------ | ----------------------------------------------------------------------------------------------- |
| `node --version` / `npm --version`               | `v26.5.0` / `11.17.0`; Expo 58's published Node engine supports this version.                   |
| `npm ci --no-audit --no-fund`                    | Exit 0; clean lockfile install, 1,306 packages added.                                           |
| `npm run check`                                  | Exit 0; TypeScript, ESLint (zero warnings), formatting, and 2/2 Jest tests pass.                |
| `npm run test:unit`                              | Exit 0; 1/1 starter component test passes.                                                      |
| `npm run test:components`                        | Exit 0; 1/1 Expo Router root-route test passes.                                                 |
| `npx expo install --check`                       | Dependencies are up to date.                                                                    |
| `npx expo-doctor` (1.21.6)                       | Exit 0; 20/20 checks pass, including after the clean install.                                   |
| `npm start -- --max-workers 2`                   | Metro runs on localhost:8081.                                                                   |
| Android bundle request to Metro                  | HTTP 200; 1,312 modules bundled, 6,173,572 bytes. This is a bundle check, not device execution. |
| `emulator -accel-check`                          | WHPX is installed and usable; no virtualization/security setting changed.                       |
| API 36 Google APIs x86_64 emulator               | Boots with `adb devices -l` reporting `device`, `sys.boot_completed=1`, Android 16 / SDK 36.    |
| `maestro --version`                              | `2.12.1`; the official archive SHA256 matches its published checksum.                           |
| SDK CMake's `ninja.exe --version`                | `1.13.2`; official Windows archive SHA256 verified.                                             |
| `npm run android -- --no-bundler`                | Exit 0; `BUILD SUCCESSFUL in 3m 28s`, debug APK installed and opened on the API 36 emulator.    |
| `npm run test:e2e:android`                       | Exit 0; 1/1 flow passes in 12 seconds. JUnit: 1 test, 0 failures.                               |
| `git diff --check` / `git diff --cached --check` | Pass.                                                                                           |

The tests exercise the public starter and the actual `/` route. There are no
fake login or operational screens, API mocks presented as implemented behavior,
or skipped tests. Jest explicitly fails when no tests are discovered.

## Native Android acceptance

JDK 17.0.21, Android command-line tools, Platform Tools, Emulator, SDK platforms,
and the test AVD were provisioned under a task-local tools directory outside the
repository. Only process-local environment variables were set. The emulator ran
headless with software graphics and WHPX; Android Studio was not installed.

Native verification used a clean short-path clone at `F:\cs138`, initially at
`ed583e9`, with identical tracked app sources and lockfile. The only subsequent
tooling change adds an npm pretest hook that creates Maestro's ignored report
directory; the passing E2E run includes that hook. From `apps/staff-mobile`,
with the booted emulator and Metro on port 8081:

```powershell
$env:GRADLE_OPTS = '-Dorg.gradle.workers.max=2 -Dorg.gradle.parallel=false'
$env:CMAKE_BUILD_PARALLEL_LEVEL = '2'
adb reverse tcp:8081 tcp:8081
npm run android -- --no-bundler
npm run test:e2e:android
```

The build uses JDK 17.0.21, Gradle 9.4.1, SDK 37, Build Tools 37.0.0, target SDK
36, NDK 27.1.12297006, CMake 3.22.1, Ninja 1.13.2, and the x86_64 ABI.
The earlier stalled NDK provisioning was resolved with the standard Android SDK
installer. Deep Windows paths and bundled Ninja 1.10.2 caused native path-length
failures; the short physical checkout and verified Ninja update resolve them.
The [contributor guide](../../../apps/staff-mobile/README.md) records both steps.
No generated native source or dependency was patched.

The first Maestro attempt was obstructed by an Android System UI ANR dialog;
its report also revealed that Maestro does not create the output directory.
The final run passes after dismissing the system dialog, stopping the Gradle
daemon, and adding the pretest directory creation hook. The flow itself retains
all assertions and clears app state on launch.

- [Passing JUnit report](maestro-junit.xml): 1 test, 0 failures, 11.819 seconds.
- [Starter screenshot](android-starter.png), visually checked after the passing
  flow: all starter text is visible, with no error dialog or account/shop data.
- Debug APK SHA256:
  `7905c441eb834c2b819ec36ef11a8c50e65b27ffea6e9a881939eaf0d84807ab`.
  The generated APK and debug keystore are excluded from Git.

![Verified Android starter](android-starter.png)

## Scope, review, and dependency notes

- The owner explicitly authorized proceeding with #138 despite the missing
  @longtoZ S0 review decision on #136. That prerequisite is waived for this work;
  no missing review is represented as having occurred.
- #138 stays open for @JakeConal's required review decision. The missing S0
  review and team-agreement checkboxes are not represented as reviewed/approved.
- The implementation and checks satisfy local Mobile tasks 1.2/1.3, corresponding
  to scaffold 1.1 and tooling 1.4 in the older canonical parent #95 checklist.
  Its broader dependency item 1.2 remains incomplete. Legacy local task files
  are unchanged; no future authentication/notification dependency is claimed.
- Only the app scaffold, developer tooling, and documentation change. No backend,
  database, generated API client, account/session behavior, or workflow screen
  is added. #139 owns runtime API configuration; #143 owns client and sessions.
- `npm audit --omit=dev` exits 1 with 21 moderate and 1 low advisory findings,
  zero high/critical. Findings include Expo/Jest tooling dependencies such as
  glob/minimatch and deepmerge. No forced SDK downgrade or unverified dependency
  override was applied. This audit is not reported as passing.
- No secrets, database connection strings, private device addresses, native
  projects, downloaded tools, debug keystores, or dependency directories are
  included. An app-local `.gitattributes` keeps text LF on Windows checkouts.

The [contributor guide](../../../apps/staff-mobile/README.md) contains setup and
the verified Android reproduction steps.
