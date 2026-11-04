# Staff-mobile scaffold evidence (#138)

Recorded on **4 November 2026** from `codex/expo-scaffold-138`, based on
`main` at `10f097d`. Scope: local Mobile OpenSpec tasks **1.2 and 1.3**.
This is a **draft implementation with Android acceptance evidence pending**.

## Executed verification

Commands run from `apps/staff-mobile` on Windows 11:

| Command | Observed result |
| --- | --- |
| `node --version` / `npm --version` | `v26.5.0` / `11.17.0`; Expo 58's published Node engine supports this version. |
| `npm ci --no-audit --no-fund` | Exit 0; clean lockfile install, 1,306 packages added. |
| `npm run check` | Exit 0; TypeScript, ESLint (zero warnings), formatting, and 2/2 Jest tests pass. |
| `npm run test:unit` | Exit 0; 1/1 starter component test passes. |
| `npm run test:components` | Exit 0; 1/1 Expo Router root-route test passes. |
| `npx expo install --check` | Dependencies are up to date. |
| `npx expo-doctor` (1.21.6) | Exit 0; 20/20 checks pass, including after the clean install. |
| `npm start -- --max-workers 2` | Metro runs on localhost:8081. |
| Android bundle request to Metro | HTTP 200; 1,312 modules bundled, 6,173,572 bytes. This is a bundle check, not device execution. |
| `emulator -accel-check` | WHPX is installed and usable; no virtualization/security setting changed. |
| API 36 Google APIs x86_64 emulator | Boots with `adb devices -l` reporting `device`, `sys.boot_completed=1`, Android 16 / SDK 36. |
| `maestro --version` | `2.12.1`; the official archive SHA256 matches its published checksum. |
| `git diff --check` / `git diff --cached --check` | Pass. |

The tests exercise the public starter and the actual `/` route. There are no
fake login or operational screens, API mocks presented as implemented behavior,
or skipped tests. Jest explicitly fails when no tests are discovered.

## Android acceptance blocker

JDK 17.0.21, Android command-line tools, Platform Tools, Emulator, SDK platforms,
and the test AVD were provisioned under a task-local tools directory outside the
repository. Only process-local environment variables were set. The emulator ran
headless with software graphics and WHPX; Android Studio was not installed.

`npx expo prebuild --platform android --no-install` succeeds. The subsequent
native build:

```powershell
./android/gradlew.bat -p android app:assembleDebug -PreactNativeArchitectures=x86_64 --max-workers=2 --console=plain
```

compiled the Gradle plugins and configured SDK 37, Build Tools 37.0.0, target SDK
36 and NDK 27.1.12297006. Its NDK download stalled, and the build was stopped.
Automatic approval review rejected the parallel official NDK downloader and a
command to download/install Expo Go 58.0.5, returning only **blocked by policy**.
No further reason was returned. No alternative installation was attempted after
that rejection.

There is **no successful APK build, app installation, starter-screen launch,
Android screenshot, or Maestro test result**. `npm run android` and
`npm run test:e2e:android` are documented command foundations, not passing
acceptance evidence. Finish them on an environment permitted to install the
required Android runtime before marking local tasks 1.2/1.3 complete or making
the PR ready for review.

## Scope, review, and dependency notes

- The owner explicitly authorized proceeding with #138 despite the missing
  @longtoZ S0 review decision on #136. That prerequisite is waived for this work;
  no missing review is represented as having occurred.
- #138 stays open. @JakeConal must record the issue's required review decision
  after the missing acceptance evidence is supplied.
- Local OpenSpec task checkboxes and the older parent #95 checklist remain
  unchanged. #95 uses older numbering and broader dependency items; this
  scaffold does not satisfy its future authentication/notification dependencies.
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
the remaining Android reproduction steps.
