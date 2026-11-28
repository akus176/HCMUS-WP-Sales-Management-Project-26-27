# Account-wide mobile sign-out (#234)

## Status

The owner-approved account-wide sign-out action is implemented on branch
`codex/mobile-account-wide-logout-234` in [PR #245](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/245).
Automated verification is complete in GitHub CI. Native/Maestro execution is
**not run**: this repository has no Android emulator/Maestro CI job, and local
native business workflows were not authorized for this change. Checklist items
3.2 and 3.3 therefore remain open, and named-reviewer approval remains separate.

## Source

| Item | Value |
| --- | --- |
| Base | `main` |
| Branch | `codex/mobile-account-wide-logout-234` |
| Implementation commit | `f90ff5c` |
| Verification revision | `5eae8a7` |
| Pull request | [#245](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/pull/245) |

## Implemented behavior

- More → Account retains ordinary **Sign out** and adds a distinct **Sign out
  everywhere** action with a description, confirmation sheet, cancel action,
  offline guard, busy guard and inline errors.
- `logoutAll()` calls the existing `POST /api/v1/auth/logout-all` with the
  current bearer token. Only a `204` is treated as confirmation.
- The client renews an access token inside the existing 30-second window before
  dispatch. The revocation mutation is never replayed automatically.
- Duplicate confirmations in one session epoch share one promise and one
  request. A late completion cannot clear a newer session.
- On confirmation, device registration removal is best effort, in-flight work
  is aborted, cached protected data is cancelled/cleared, in-memory credentials
  are dropped and the saved refresh token is deleted.
- `401`, other rejections, `5xx`, network failure, timeout and unknown outcomes
  keep the local session and show an accurate error. A post-confirmation local
  storage failure is reported distinctly.
- Ordinary sign-out stays session-scoped and continues to clear local state
  offline without calling `logout-all`.
- Already-issued access tokens keep their existing ten-minute natural expiry;
  immediate JWT invalidation is not claimed.
- Counter and Preparation tabs, permissions and business workflows are
  unchanged.

## GitHub CI verification

Run: [CI 46100975796](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/46100975796)

| Job | Result | Evidence |
| --- | --- | --- |
| `mobile-quality` | Passed | [job](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/46100975796/job/140843096696): TypeScript, ESLint, Prettier, **373 tests / 29 suites**, harnesses and Android export |
| `catalog-postgres` | Passed | [job](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/46100975796/job/140843096671): **86 API tests passed, 0 failed, 0 skipped**; Infrastructure 266 passed; concurrency repeats passed |
| `mobile-p0-contract` | Passed | [job](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/46100975796/job/140843096793): generated OpenAPI/TypeScript contract had no drift |
| `mobile-android` | Passed | [job](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/46100975796/job/140843096587): native debug APK build |
| `build-and-test` | Passed | [job](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/46100975796/job/140843096441) |
| `compose-smoke` | Passed | [job](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/46100975796/job/140843096660) |
| `dependencies` | Passed | [job](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/46100975796/job/140843096642) |
| `secrets` | Passed | [job](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/46100975796/job/140843096670) |
| `windows-e2e` | Passed | [job](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/actions/runs/46100975796/job/140843096633) |

The PostgreSQL `api-postgres.trx` artifact records
`StaffIdentityHttpTests.Logout_EnforcesOwnershipAndAccountWideRevocation` as
passed in 0.986 s. That test now covers:

- anonymous `logout-all` is `401`;
- ordinary logout revokes only the presented family and leaves another family
  of the same account usable;
- ordinary logout cannot revoke another account's refresh token;
- account-wide logout revokes the acting account's rotated remaining family;
- two refresh families belonging to another account remain usable after the
  acting account's account-wide logout.

## Mobile regressions

The session suite covers confirmed revocation, `401`/rejection/`5xx`, network
failure, timeout, duplicate confirmation, a newer-session race, offline token
renewal, storage deletion failure and ordinary-logout isolation. The
Account/navigation suite covers cancellation, confirmed global sign-out,
accurate failure feedback, and the offline guard while ordinary sign-out stays
enabled.

Pre-push development checks (not acceptance evidence) also passed locally:
TypeScript, ESLint, Prettier, 373 Jest tests / 29 suites, 7 forwarding-harness
checks, API test Release build and strict OpenSpec validation.

## Maestro cases added

Focused flows are in `apps/staff-mobile/.maestro/session-account`:

| Flow | Expected outcome |
| --- | --- |
| `cancel.yaml` | Confirmation cancels without a request; session remains |
| `offline.yaml` | Account-wide action disabled and explained; ordinary sign-out remains enabled; no request |
| `ordinary.yaml` | Session-scoped sign-out; no account-wide action in the sheet |
| `global.yaml` | Confirmed `204` sign-out returns to sign-in and a relaunch still shows sign-in |

Run them with:

```sh
npm run test:e2e:session-account --prefix apps/staff-mobile
```

These flows are **not executed** by the current CI workflow. The build artifact
alone does not prove emulator interaction. Independent refresh-family outcomes
after a future native run must be checked through separate API sessions, with
tokens kept in memory and excluded from evidence.

## Evidence hygiene and limits

- No passwords, bearer tokens, refresh tokens, raw auth bodies or private
  Maestro logs are included.
- Native/Maestro results, device/API profiles and captures are absent because
  the run was not performed; they are not implied by the passing jobs.
- The API tests establish refresh-family behavior against real PostgreSQL, not
  a mobile-device user journey.
- Named-reviewer approval under the project workflow remains open.
