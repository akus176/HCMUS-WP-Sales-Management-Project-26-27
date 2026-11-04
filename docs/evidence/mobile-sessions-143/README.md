# Generated client and secure mobile sessions (#143)

Verified 5 November 2026 on Windows, from `codex/mobile-sessions-143`, based on
merged scaffold PR #166 (`55c9180`). OpenSpec change: `add-staff-mobile-app`.
Local Mobile tasks **3.1–3.4** map to canonical issue #95 tasks **3.1–3.5**.

## Executed verification

Commands run from `apps/staff-mobile` unless specified otherwise.

| Command | Result |
| --- | --- |
| `npm ci` (app and `contracts/mobile-p0`) | Clean dependency installation passed. |
| `npm run api:generate` and `npm run api:check` | Pinned openapi-typescript 7.10.1 regenerated the committed contract with no content diff, repeatedly. |
| `npm run check` | Strict TypeScript, ESLint with zero warnings, Prettier, and **36/36 tests across 6 suites** passed. |
| `npm run test:unit` | **29/29** transport, session, and SecureStore adapter tests passed. |
| `npm run test:components` | **6/6** login and Expo Router tests passed. The remaining default test covers the public starter. |
| `npm run test:live` | **1/1** real HTTP authentication protocol test passed against an isolated local API/PostgreSQL database; repeated after the final transport change. |
| `npx expo install --check` | Dependencies up to date. |
| `npx expo-doctor` | **20/20 checks** passed. |
| `npx expo export --platform android --output-dir dist` | Final source exported successfully: 1,200 modules, 2.7 MB Hermes bundle. |
| `npm run android -- --no-bundler` | Native debug build with SecureStore succeeded in **9m 27s**, 326 actionable tasks; installed on the API 36 emulator. See the build boundary below. |
| `dotnet build src/CoffeeShop.Api/CoffeeShop.Api.csproj -c Release` (repository root) | Passed with zero warnings/errors before the live protocol test. |
| `git diff --check` | Passed. |

Production dependency audit (`npm audit --omit=dev --json`) reports **22 advisory
entries: 1 low, 21 moderate, 0 high, 0 critical** in the Expo/tooling dependency
tree. The full audit reports 38 entries (1 low, 37 moderate). No force upgrade
or dependency override was applied.

## Acceptance coverage

| Task | Implementation and proof |
| --- | --- |
| Local 3.1 / canonical 3.1 | The app re-exports the existing deterministic generated schema through `src/api/schema.ts`; openapi-fetch 0.17.0 provides typed calls. The app generation command reuses the repository's isolated pinned generator. Regeneration produces no diff. |
| Local 3.2 / canonical 3.2 | Bearer transport maps RFC 7807 failures and supports cancellation. Tests cover 400 validation, 403 forbidden, 409 conflict, 422 business-rule, 500, and network failure; safe read retries, no mutation replay, and cancellation are verified. |
| Local 3.3 / canonical 3.3–3.4 | Login component tests exercise successful/rejected/empty submissions. Access tokens remain private in memory; refresh storage uses Expo SecureStore with device-only accessibility and origin binding. Concurrent and delayed old-token 401 tests perform one rotation. The real API test verifies saved-token rotation. |
| Local 3.4 / canonical 3.5 | Tests verify immediate credential/cache cleanup, terminal refresh rejection, protected-route history removal, rejected stale deep links, late HTTP/body responses, and logout racing a pending secure write. A synthetic retained delivery cache is removed. No business workflow or persistent business store was added. |

The default unit/component tests use synthetic HTTP fixtures and a mocked native
SecureStore module. They test the actual session implementation and generated
client boundary, but do not prove Android keystore behavior on a device.

## Real protocol test boundary

The opt-in `tests/live-session.integration.ts` called the actual ASP.NET Core API
at `http://127.0.0.1:5232/api/v1`, backed by a fresh task-local PostgreSQL cluster
on loopback port 55443. Migrations and documented staff provisioning completed
successfully. Only a disposable synthetic Barista account was used; no existing
shop database or real staff credentials were used. A temporary signing key was
held only in the API process. No tokens or signing secrets are retained here.

The test verifies rejected login, valid login, bearer `/auth/me`, rotation when a
new client restores a saved refresh token, local logout cleanup, and server
rejection of the revoked refresh token. It uses an in-memory storage adapter.
Reproduction and required environment variables are documented in the
[contributor guide](../../../apps/staff-mobile/README.md).

## Native build and runtime boundary

The native build used the short physical checkout `F:\cs143`, JDK 17.0.21,
Android SDK/NDK 27.1.12297006, CMake 3.22.1, and Ninja 1.13.2 from the toolchain
already prepared for #138. It compiled the final native dependencies and
SecureStore configuration. Later JavaScript changes, including the late-body
session guard, were verified by the final checks and Android export above;
the debug APK requires Metro and is not a standalone final JavaScript release.

Debug APK SHA256:
`5E6F31B02609F1DE776628EEE5F1109BB47781E6593F0CC2A6BA99965C1E07AE`.
Generated native projects, APKs, dependencies, and disposable database files are
ignored/local verification artifacts and are not committed.

**The Android session Maestro flow has not run.** Automatic approval review
rejected Metro startup attempts with only `blocked by policy`. No rendered
session screen, device restart/restore, device SecureStore behavior, or session
Maestro screenshot/report is claimed. The optional `.maestro/session.yaml`
documents that follow-up verification using disposable account variables.
The earlier #138 starter launch evidence remains in
[the scaffold evidence](../mobile-scaffold-138/README.md).

## Scope and review

The API contract and existing authentication endpoints are reused. Workflow
screens, permission modules (#144), complete environment/native networking
(#139), push, live events, and offline write queues are outside this issue.
No mobile database access, committed credentials, or production data was added.
Issue #143 stays open until @JakeConal records the required review decision.
