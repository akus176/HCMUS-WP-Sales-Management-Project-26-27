# Physical Android workflow acceptance (#201)

On 14 November 2026, one fresh run **77116d65 passed 19/19 focused cases**
and **42/42 native Maestro steps**, with zero failed, missing or skipped
cases and passing independent server verification. [Exact results](summary.json)
include this run's seed IDs, actor IDs, authoritative snapshots and real API
statuses. Preliminary failed attempts are excluded from this passing result.

This supplies Mobile **4.2**, **4.3**, **4.5** and the focused subset of **6.1**
under canonical OpenSpec #95. Issue [#201](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/201)
remains open for **@longtoZ's approval**.

## Device, source and environment

| Item                  | Recorded value                                                                                              |
| --------------------- | ----------------------------------------------------------------------------------------------------------- |
| Device                | Physical Samsung SM-A155F, arm64-v8a                                                                        |
| Android               | Android 16, API 36                                                                                          |
| App                   | `com.coffeeshop.staffmobile`, development APK; versionCode=1 minSdk=24 targetSdk=36; versionName=0.1.0      |
| App JavaScript source | `1b855a57fcb6104262ae300aff436e82f3b79895`                                                                  |
| Acceptance harness    | `1b855a57fcb6104262ae300aff436e82f3b79895`                                                                  |
| Shared API source     | `105df6036ff86d5929c16006b9069fb849751b82`                                                                  |
| Installed APK SHA256  | `4d20f33f0dfdddde66fe64aecb03eff4918d5ebc94b5d1d2896853b0c0eaa213`                                          |
| API/database          | Shared ASP.NET Core API on loopback port 5232; separate migrated disposable `issue201_` PostgreSQL database |
| Network               | Device profile; real-API forwarding listener on host LAN port 5233; Metro on USB-forwarded port 8081        |
| Toolchain             | Node 26.5.0, npm 11.17.0, JDK 17.0.21, Maestro 2.12.1, .NET SDK 10.0.401                                    |
| Accounts              | Dedicated synthetic Admin, Manager, Barista and two separate DeliveryStaff accounts                         |

The installed native binary was reused from #198; no native dependency or
configuration changed. Metro served the current workspace JavaScript, including
the stale-discount fix. `git diff 105df60 -- src contracts tests` was empty,
confirming that the reused API source and backend tests match this workspace.
The development APK hash identifies native packaging; it is separate from the
JavaScript source commit. No production release is claimed.

Seed state: five barista orders start Confirmed with total 10; six discount
orders start Draft with total 10 and a pending fixed discount of 1; seven
courier orders start Ready/Assigned with total 10, six assigned to the Android
courier and one to the second courier. All use the unique `I201 77116d65` catalog
fixture; exact order/discount IDs are recorded under `seed` in the summary.

## Reproduction and executed checks

Follow the [focused runbook](../../mobile-workflow-verification-201.md).
The execution used a private process-environment wrapper which invoked:

```text
node scripts/verify-android-workflows.mjs
```

The equivalent public entry point from `apps/staff-mobile`, after setting the
documented environment variables, is `npm run test:e2e:workflows`.
ADB operations include `adb devices -l`, `adb reverse tcp:8081 tcp:8081`,
`adb shell getprop`, `adb shell pm path com.coffeeshop.staffmobile`,
`adb shell sha256sum <installed-apk>` and `adb exec-out screencap -p`.
The runner invokes Maestro with `test --format junit --output <fresh-report.xml>`
and the committed flows under `.maestro/workflows-focused`.

`npm run check` passed TypeScript, zero-warning ESLint, formatting, **26 Jest
suites / 220 tests**, **4 inventory harness regressions** and **3 workflow harness
regressions**. `openspec validate add-staff-mobile-app --strict` passed.
The added regression verifies that a conflict refetch removes resolved discount
actions while retaining clear conflict feedback and a refresh action.

## Results and original captures

Each case requires all listed fresh native step reports, its independent server
assertions, and an original ADB screenshot. JUnit results/timing are preserved;
only the device serial attribute is omitted. PNG files are byte-identical to
the original captures. [Artifact hashes](artifacts.json) identify the published
files. Raw Maestro logs and private configuration are excluded.

| Case                 | Result | Screenshot                      | Native step reports                                                                                                                                                   |
| -------------------- | ------ | ------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| barista-progression  | Pass   | [PNG](barista-progression.png)  | [1](barista-progression-1-login.xml), [2](barista-progression-2-open.xml), [3](barista-progression-3-barista-start.xml), [4](barista-progression-4-barista-ready.xml) |
| barista-conflict     | Pass   | [PNG](barista-conflict.png)     | [1](barista-conflict-1-open.xml), [2](barista-conflict-2-barista-start.xml)                                                                                           |
| barista-stale        | Pass   | [PNG](barista-stale.png)        | [1](barista-stale-1-unavailable.xml)                                                                                                                                  |
| barista-forbidden    | Pass   | [PNG](barista-forbidden.png)    | [1](barista-forbidden-1-open.xml), [2](barista-forbidden-2-barista-start.xml)                                                                                         |
| barista-offline      | Pass   | [PNG](barista-offline.png)      | [1](barista-offline-1-open.xml), [2](barista-offline-2-offline.xml), [3](barista-offline-3-reconnected.xml)                                                           |
| discount-approve     | Pass   | [PNG](discount-approve.png)     | [1](discount-approve-1-login.xml), [2](discount-approve-2-open.xml), [3](discount-approve-3-discount-decision.xml)                                                    |
| discount-reject      | Pass   | [PNG](discount-reject.png)      | [1](discount-reject-1-open.xml), [2](discount-reject-2-discount-decision.xml)                                                                                         |
| discount-conflict    | Pass   | [PNG](discount-conflict.png)    | [1](discount-conflict-1-open.xml), [2](discount-conflict-2-discount-decision.xml)                                                                                     |
| discount-stale       | Pass   | [PNG](discount-stale.png)       | [1](discount-stale-1-unavailable.xml)                                                                                                                                 |
| discount-forbidden   | Pass   | [PNG](discount-forbidden.png)   | [1](discount-forbidden-1-open.xml), [2](discount-forbidden-2-discount-denied.xml)                                                                                     |
| discount-offline     | Pass   | [PNG](discount-offline.png)     | [1](discount-offline-1-open.xml), [2](discount-offline-2-offline.xml), [3](discount-offline-3-reconnected.xml)                                                        |
| courier-own-list     | Pass   | [PNG](courier-own-list.png)     | [1](courier-own-list-1-login.xml), [2](courier-own-list-2-delivery-list.xml)                                                                                          |
| courier-other-detail | Pass   | [PNG](courier-other-detail.png) | [1](courier-other-detail-1-unavailable.xml)                                                                                                                           |
| courier-delivered    | Pass   | [PNG](courier-delivered.png)    | [1](courier-delivered-1-open.xml), [2](courier-delivered-2-delivery-start.xml), [3](courier-delivered-3-delivery-complete.xml)                                        |
| courier-failed       | Pass   | [PNG](courier-failed.png)       | [1](courier-failed-1-open.xml), [2](courier-failed-2-delivery-start.xml), [3](courier-failed-3-delivery-fail.xml)                                                     |
| courier-conflict     | Pass   | [PNG](courier-conflict.png)     | [1](courier-conflict-1-open.xml), [2](courier-conflict-2-delivery-start.xml)                                                                                          |
| courier-stale        | Pass   | [PNG](courier-stale.png)        | [1](courier-stale-1-unavailable.xml)                                                                                                                                  |
| courier-forbidden    | Pass   | [PNG](courier-forbidden.png)    | [1](courier-forbidden-1-open.xml), [2](courier-forbidden-2-delivery-start.xml)                                                                                        |
| courier-offline      | Pass   | [PNG](courier-offline.png)      | [1](courier-offline-1-open.xml), [2](courier-offline-2-offline.xml), [3](courier-offline-3-reconnected.xml)                                                           |

Additional offline captures show disabled native actions before radios were
restored: [barista](barista-offline-disabled.png),
[discount](discount-offline-disabled.png), [courier](courier-offline-disabled.png).
The final per-case screenshots show the reconnected state; snapshots and
forwarded mutation counts prove no write or replay occurred.

## Authoritative outcomes and negative cases

- Barista progression reaches Ready, with exactly the expected actor-attributed
  Preparing and Ready history entries. Competing start returns real HTTP 409;
  Ready detail returns real 404; revoked permission returns real 403.
- Discount approval changes the server total from 10 to 9; rejection leaves 10.
  A competing rejection returns real 409 to Android's approval attempt. The
  resolved request no longer exposes stale action buttons. Already-decided
  detail shows no pending request. Revoked approval permission returns real 403.
- Courier lists are scoped to the signed-in courier. Another courier's detail
  and mutation return privacy-preserving 404. Delivered and failed outcomes
  are persisted once under the correct courier; failure requires and persists
  `Customer unavailable`. Completed detail is unavailable. Competing start
  returns real 409; revoked courier permission returns real 403.
- All three offline cases observe disabled actions, unchanged authoritative
  snapshots and zero forwarded business writes through reconnection.

Independent administrator API reads verify order and discount state. Read-only
SQL in the disposable harness supplies history actors/counts and persisted
delivery failure reasons that the current API does not expose. Mobile still
uses only the shared API. Conflict snapshots are taken after the legitimate
competing commit, so rejecting the original request must add no further effect.
All temporary role associations and original radio settings were restored;
`cleanupErrors` is empty.

## Scope and review

There are no unrun cases in this focused 19-case matrix. The [#186 aggregate
baseline](../mobile-quality-186/README.md) remains the all-role/accessibility
evidence. Inventory and receipts remain with #198. Simultaneous Windows/mobile,
live-event and broader reconnect acceptance remain with #202/#203; independent
API reads here do not establish Windows UI behavior. Final recording is Mobile
6.6. No parent checklist is changed by this focused run.

Published text reports and screenshots were reviewed for scope and secrets.
Named reviewer **@longtoZ** must record approval before #201 is closed.
