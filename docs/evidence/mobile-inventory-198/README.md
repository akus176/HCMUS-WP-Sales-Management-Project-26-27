# Physical Android inventory and receipt verification (#198)

On 14 November 2026, fresh run **13402d30 passed 17/17 native cases**, with
zero failed, missing, or skipped cases and passing independent API state
verification. [Exact results, seed IDs, snapshots and mutation statuses](summary.json)
and the original screenshots/JUnit reports below belong to this one complete
run. Preliminary partial runs and isolated diagnostics are excluded.

This supplies Mobile **4.4**, **5.6**, and the inventory/receipt subset of
**6.1**, plus mobile evidence for later **6.4** acceptance under canonical
OpenSpec [#95](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/95).
Issue [#198](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/198)
remains open for **@longtoZ's approval**.

## Device, build and environment

| Item             | Recorded value                                                                                                                                |
| ---------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| Device           | Physical Samsung SM-A155F, ARM64 (`arm64-v8a`)                                                                                                |
| Android          | Android 16, API 36                                                                                                                            |
| Display          | 1080 × 2340 pixels, density 450, font scale 1.0                                                                                               |
| App              | `com.coffeeshop.staffmobile`, version 0.1.0, version code 1, development APK                                                                  |
| App/API source   | `3043e2dc2b18dd6db11a5e2cc722f4ea81f6d902`                                                                                                    |
| Executed harness | `ae4d6a75a11c92454fa1f8b557836293263d8e7d`                                                                                                    |
| APK SHA256       | `4d20f33f0dfdddde66fe64aecb03eff4918d5ebc94b5d1d2896853b0c0eaa213`                                                                            |
| API/database     | ASP.NET Core/.NET SDK 10.0.401; isolated Docker PostgreSQL 18.6 database                                                                      |
| Toolchain        | Node 26.5.0, npm 11.17.0, Temurin 17.0.21+9, Maestro 2.12.1                                                                                   |
| Network          | Device profile, local HTTP forwarding listener on port 5233, upstream shared API on host loopback port 5232; Metro on USB-forwarded port 8081 |
| Accounts         | Dedicated synthetic `issue198InventoryStaff` for Android; independent authenticated `issue198Admin` API session for verification              |

[Device/tool profile](device-profile.json) records the observed device settings.
The native APK and application/API business source remain identical to the
listed build source; this change adds verification tooling and evidence.
The real API's readiness endpoint returned 200 from the host and phone during
setup. Private addresses, device serials, passwords, tokens and signing values
are retained only in the local environment, outside the published evidence.

## Reproduction and commands

Follow the [focused contributor runbook](../../mobile-inventory-verification-198.md)
for the disposable database, synthetic account provisioning, exact environment
variables, forwarding listener and Metro configuration. Every run seeds new
records and writes a fresh, initially failing summary.

Executed from `apps/staff-mobile`, with the documented local configuration:

```text
npm run check
npm run test:inventory-harness
node --env-file=<private-local-file> scripts/verify-android-inventory.mjs
```

- Mobile type-check, lint and formatting passed; **26 Jest suites / 219 tests**
  passed, with no failures or skips.
- Forwarding-harness regressions: **4 passed**, including response loss after
  commit, real competing-write conflict, unexercised-fault protection, and
  receipt transport replay before deliberate recovery. These regressions also
  run in the existing `mobile-quality` CI job.
- Focused native run: **17 passed / 17 expected**, no failed/missing/skipped
  reports, and `serverVerificationPassed: true`.
- `openspec validate add-staff-mobile-app --strict` passed.

The unchanged API source also passed these two PostgreSQL-backed tests during
device setup on the same date, each using a separate disposable test database:

```text
dotnet test tests/CoffeeShop.Api.Tests/CoffeeShop.Api.Tests.csproj -c Release --filter "FullyQualifiedName~InventoryAdjustment_EnforcesPermissionValidationAndIdempotency|FullyQualifiedName~ReceiptPosting_ChangesSharedBalanceAndAddsOneAuditedMovement" --logger "trx;LogFileName=inventory-setup.trx"
```

Result: **2 passed, 0 failed, 0 skipped**. The API Release build passed with zero
warnings and errors. CI and Android export remain separate checks; the device
results here come from actual native interaction.

## Native results and genuine captures

Each row requires a successful Maestro execution, exactly one fresh passing
JUnit case, its server assertions where applicable, and an unmodified ADB
screenshot. Text at a scrolled viewport edge can be partly outside the capture;
the native flows scroll to and assert the required controls and feedback.

| Case                     | Result and observation                                                                                                | Screenshot                     | JUnit                          |
| ------------------------ | --------------------------------------------------------------------------------------------------------------------- | ------------------------------ | ------------------------------ |
| Sign-in                  | Pass; dedicated inventory account reaches staff home                                                                  | [PNG](login.png)               | [XML](login.xml)               |
| Browse                   | Pass; current search, below/equal threshold emphasis, above-threshold exclusion and empty results                     | [PNG](browse.png)              | [XML](browse.xml)              |
| Refresh baseline         | Pass; initial server balance 1 l is visible                                                                           | [PNG](refresh-before.png)      | [XML](refresh-before.xml)      |
| Manual refresh           | Pass; independent API adjustment changes 1 → 2 l and native refresh reads 2 l                                         | [PNG](refresh-after.png)       | [XML](refresh-after.xml)       |
| Adjustment               | Pass; reasoned +2 changes 1 → 3 l once, with visible movement history                                                 | [PNG](adjustment.png)          | [XML](adjustment.xml)          |
| Invalid adjustment       | Pass; missing reason disables Save; -9999 is rejected with real HTTP 400 and unchanged stock/history                  | [PNG](invalid.png)             | [XML](invalid.xml)             |
| Forbidden adjustment     | Pass; removing only the synthetic account's role produces real HTTP 403 and unchanged stock/history; role is restored | [PNG](forbidden.png)           | [XML](forbidden.xml)           |
| Adjustment conflict      | Pass; a committed competing +1 produces 5 → 6 l, then the original request gets real HTTP 409 with no further effect  | [PNG](adjustment-conflict.png) | [XML](adjustment-conflict.xml) |
| Lost adjustment response | Pass; real HTTP 200 is lost after commit, UI reports uncertainty, server has one +2 movement                          | [PNG](adjustment-lost.png)     | [XML](adjustment-lost.xml)     |
| Adjustment retry         | Pass; deliberate retry reuses the exact request ID/body and leaves balance 3 l with one movement                      | [PNG](adjustment-retry.png)    | [XML](adjustment-retry.xml)    |
| Empty receipt            | Pass; native posting action is absent, receipt remains draft with zero movements                                      | [PNG](receipt-empty.png)       | [XML](receipt-empty.xml)       |
| Receipt post/history     | Pass; reviewed +4 line posts once, with posted status and visible receipt movement                                    | [PNG](receipt-post.png)        | [XML](receipt-post.xml)        |
| Receipt conflict         | Pass; competing real post commits once; original request gets HTTP 409 and refreshes posted state                     | [PNG](receipt-conflict.png)    | [XML](receipt-conflict.xml)    |
| Lost receipt response    | Pass; committed post/replay responses are lost, UI shows unconfirmed result, server has one +4 movement               | [PNG](receipt-lost.png)        | [XML](receipt-lost.xml)        |
| Receipt refresh          | Pass; explicit refresh reveals posted state without another post or extra stock effect                                | [PNG](receipt-refresh.png)     | [XML](receipt-refresh.xml)     |
| Second lost receipt      | Pass; a separate fresh receipt reaches the unconfirmed state after one committed +4 effect                            | [PNG](receipt-retry-lost.png)  | [XML](receipt-retry-lost.xml)  |
| Receipt deliberate retry | Pass; retry returns real HTTP 409, shows posted/conflict state, and retains one movement                              | [PNG](receipt-retry.png)       | [XML](receipt-retry.xml)       |

## Seed and authoritative checks

All records have unique prefix **I198 13402d30**. Exact identifiers and initial
DTOs are under `seed` in [summary.json](summary.json). Initial balances are milk
1 l, boundary 3 l, normal 10 l, refresh 1 l, conflict 5 l, forbidden 1 l, lost
adjustment 1 l, and zero for each receipt ingredient. Thresholds are 3 l.
Prepared receipts each have one +4 l line at unit cost 2; the empty receipt has
no lines. No existing shop records or real customer data are used.

An independently authenticated administrator reads persisted balances,
movement IDs/counts, reasons, actor IDs, receipt references and server status.
Successful adjustments/receipts have one movement attributed to the Android
inventory account. Rejected requests are compared with the authoritative
snapshot immediately before rejection. For conflicts this snapshot follows
the competing commit, so that legitimate change is distinguished from the
rejected request's absence of an additional effect.

The forwarding listener fabricates no business response. The lost-response
window starts only after the upstream API has committed and returned its
response. This phone automatically replayed one empty receipt POST; the API
returned 409 and retained one effect. That replay response was also dropped
until native uncertainty was visible, then normal forwarding resumed before
the explicit refresh/retry. The sanitized `exchanges` array records actual
upstream statuses, response-loss flags, and adjustment request/body identity.

## Scope, follow-ups and review

The [#186 quality baseline](../mobile-quality-186/README.md) remains the
aggregate all-role/accessibility evidence. These focused physical-device
results supplement it. Matching Windows UI/network execution belongs to
[#200](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/200);
its [existing API evidence](../issue-200-winui-api.md) does not establish a
simultaneous run against this seed. Shared-state acceptance remains split
between [#202](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/202)
and [#203](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/203).

The separate source-level inventory event/query-key mismatch is explicitly
tracked in [the #203 coordination note](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/203#issuecomment-6874402246).
This run proves manual refresh, not that ticket's automatic event refresh,
reconnect/offline or cross-client acceptance. Historical #180 references do
not substitute for those current gates. Parent Mobile 6.4 and final-video 6.6
remain incomplete; no parent checklist was changed by this focused run.

Published screenshots and text reports were reviewed for scope and secrets.
Raw Maestro logs, environment files and credentials are excluded. There are
no remaining failures in the focused run. Named reviewer **@longtoZ** must
record the approval decision before #198 is closed.
