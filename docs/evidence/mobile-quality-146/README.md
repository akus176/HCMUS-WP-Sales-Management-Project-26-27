# Mobile quality evidence (#146)

OpenSpec change `add-staff-mobile-app`; this issue contributes to Mobile tasks
6.1 and 6.3. Those tasks cover more than this issue and remain incomplete.

## Baseline inventory

- `mobile-quality` already runs the complete Jest suite, type-check, lint,
  formatting, and an Android JavaScript bundle export.
- `mobile-p0-contract` regenerates the API contract and checks for drift.
- The suite already covers session rotation and cleanup, route permission
  checks, and offline mutation rejection. Further cases must exercise a
  distinct boundary rather than merely rerun existing tests.
- `.maestro/starter.yaml` is an existing non-sensitive native launch flow.
  Its [#138 evidence](../mobile-scaffold-138/README.md) is historical and is
  not evidence of a launch on the current checkout or runner.

The role modules include planned placeholders. Tests of navigation, fixtures,
or bundle generation do not prove that operational workflows are implemented.

## Android launch limitation

On 5 November 2026, `adb devices -l` returned no connected devices. The Android
emulator lists `Pixel_8_Pro_API_36`, but Maestro was not available on PATH or
under the standard user installation directory. The existing starter flow now
also checks the public sign-in entry point. It has not been run on this checkout;
the #138 report documents a prior run before this additional assertion.
The local and CI bundle export is not a native build or emulator launch.
Follow the [Android setup guide](../../../apps/staff-mobile/README.md) to install
Maestro, start the AVD, install the debug app and run `npm run test:e2e:android`.

## Local verification

Executed on 5 November 2026 with Node 26.5.0 and npm 11.17.0:

| Command | Result |
| --- | --- |
| `npm ci --no-audit --no-fund` | Passed with the locked mobile dependencies. |
| `npm run type-check`, `npm run lint`, `npm run format:check` | Passed. |
| `npm test -- --ci --json --outputFile=mobile-test-results.json` | Passed: 14 suites, 126 tests, zero failures. |
| `npm run api:check` | Passed after installing the pinned contract generator; generated schema had no diff. |
| `npm run smoke:android` | Passed Android JavaScript export with the documented emulator environment values; no native launch. |
| Focused session, shell and connectivity Jest suites | Passed; the connectivity regression exposed and verified the stale-reconnect fix. |

The CI job publishes the Jest JSON report with the counts from its own run;
the contract drift check remains in the existing `mobile-p0-contract` job. A
successful local run is not evidence that this branch has passed GitHub Actions.
