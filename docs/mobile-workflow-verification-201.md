# Focused Android workflow verification (#201)

This runner verifies barista, manager discount and courier acceptance under
[#201](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/201).
It supplies Mobile 4.2, 4.3, 4.5 and their subset of 6.1 under canonical
OpenSpec #95. Inventory/receipts remain with #198; the aggregate baseline is
#186. Simultaneous Windows/mobile verification remains with #202/#203.

## Environment and isolated accounts

Follow the [contributor runbook](mobile-quality-and-release.md) for the pinned
Node 26.5.0/npm 11.17.0, JDK 17, Maestro 2.12.1, Android SDK, native development
APK and Metro. Use a separate disposable PostgreSQL database whose name begins
`issue201_`, migrated by the shared API's `--migrate` command. Start that API
on host loopback; `/health/ready` must return 200.

Provision five dedicated synthetic accounts using the existing
`--provision-staff` command: Admin, Manager, Barista and two DeliveryStaff
accounts. Usernames must begin `issue201`, each account must have exactly its
specified role, and they must share a synthetic test password. Never point the
runner at a shop database or existing staff accounts.

The permission cases temporarily remove only the matching dedicated account's
role association in this disposable database. They restore it after the case
and again during cleanup. A forcibly terminated process may require restoring
that association before reuse. No role's permission bundle is changed.

Configure ignored `apps/staff-mobile/.env.workflows.local` or process variables:

| Variable                                                                      | Purpose                                                                             |
| ----------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| `COFFEESHOP_WORKFLOW_DISPOSABLE`                                              | `1`, explicit disposable-environment acknowledgment                                 |
| `COFFEESHOP_WORKFLOW_API_URL`                                                 | Upstream loopback API, for example `http://127.0.0.1:5232/api/v1`                   |
| `COFFEESHOP_WORKFLOW_PROXY_HOST`                                              | Physical phone: host private LAN IPv4; emulator: `127.0.0.1`                        |
| `EXPO_PUBLIC_APP_ENV`                                                         | `device` or `emulator`                                                              |
| `EXPO_PUBLIC_API_BASE_URL`                                                    | Phone: `http://<host-LAN-IP>:5233/api/v1`; emulator: `http://10.0.2.2:5233/api/v1`  |
| `COFFEESHOP_WORKFLOW_ADMIN_USERNAME`                                          | Dedicated synthetic administrator                                                   |
| `COFFEESHOP_WORKFLOW_MANAGER_USERNAME`                                        | Dedicated synthetic manager                                                         |
| `COFFEESHOP_WORKFLOW_BARISTA_USERNAME`                                        | Dedicated synthetic barista                                                         |
| `COFFEESHOP_WORKFLOW_DELIVERYSTAFF_USERNAME`                                  | Dedicated synthetic courier                                                         |
| `COFFEESHOP_WORKFLOW_OTHERCOURIER_USERNAME`                                   | Second dedicated synthetic courier                                                  |
| `COFFEESHOP_WORKFLOW_PASSWORD`                                                | Shared synthetic test password, kept private                                        |
| `COFFEESHOP_WORKFLOW_POSTGRES_CONTAINER`                                      | Disposable Docker PostgreSQL container                                              |
| `COFFEESHOP_WORKFLOW_POSTGRES_DATABASE` / `COFFEESHOP_WORKFLOW_POSTGRES_USER` | Isolated database and PostgreSQL role                                               |
| `JAVA_HOME` / `MAESTRO_HOME`                                                  | JDK directory and Maestro directory containing `lib`                                |
| `ADB_PATH` / `ANDROID_SERIAL`                                                 | Optional explicit ADB executable/device; select a device when several are connected |
| `COFFEESHOP_WORKFLOW_APP_COMMIT` / `COFFEESHOP_WORKFLOW_API_COMMIT`           | Exact app JavaScript and API source commits                                         |
| `COFFEESHOP_WORKFLOW_RESULTS`                                                 | Optional local results directory                                                    |
| `COFFEESHOP_WORKFLOW_USB`                                                     | Optional `1` for an explicitly selected USB phone using the loopback test profile   |

Start Metro with the same two public values, pointing at the forwarding
listener rather than the upstream API. Restart Metro after configuration or
source changes when CI mode has disabled file watching. The runner forwards
Metro over USB port 8081. Keep the API and Metro running throughout the run.
Verify the phone is unlocked and authorized with `adb devices -l`.

For USB without a LAN listener, set `COFFEESHOP_WORKFLOW_USB=1`,
`ANDROID_SERIAL`, `EXPO_PUBLIC_APP_ENV=test`,
`COFFEESHOP_WORKFLOW_PROXY_HOST=127.0.0.1`, and a loopback proxy URL such as
`EXPO_PUBLIC_API_BASE_URL=http://127.0.0.1:5233/api/v1`. Start Metro with those
same public values. The runner reverses both Metro and the proxy port over ADB.
The upstream API and proxy ports must differ. Disposable-database, account-name,
single-role and independent-state checks remain required in this mode.

## Execute

From `apps/staff-mobile`:

```text
npm run check
npm run test:e2e:workflows
```

Alternatively keep secrets in a separate private file:

```text
node --env-file=<private-local-file> scripts/verify-android-workflows.mjs
```

The runner invokes Maestro's Java entry point with argument arrays. Credentials
are passed through the process environment, not shell command arguments. It
seeds uniquely identified fresh orders and discounts on every invocation.
It never runs inventory, receipt or account-deactivation workflows.

## Required native cases

| Workflow | Cases                                                                                                                                                                                                      |
| -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Barista  | Confirmed → Preparing → Ready; real competing-start conflict; stale Ready detail; revoked permission; disabled offline start with no replay after reconnection                                             |
| Discount | Approve and reject with server totals; real competing decision; already-decided detail; revoked approval permission; disabled offline decision with no replay                                              |
| Courier  | Assigned-only list; another courier's protected detail; delivered; failed with required reason; real competing-start conflict; completed detail; revoked permission; disabled offline start with no replay |

There are **19 required cases**. A case may contain several native steps. Every
step must exit successfully and produce exactly one fresh passing JUnit case;
missing, failed or skipped reports fail the run. All steps and server assertions
must pass before their containing case can pass. Later cases remain `not run`
after a failure. Never combine partial runs into a passing result.

Barista progression also opens the whole-ticket ready confirmation and chooses
**Keep preparing** before the final confirmed transition. The runner compares
independent API/history snapshots and forwarded business-request counts before
and after cancellation: both must remain unchanged. A fresh passing
`barista-cancel-ready` JUnit step is required; tapping the cancel action alone
does not establish no-write behavior.

The forwarding listener fabricates no business response. For conflicts it
commits a competing real API action immediately before forwarding Android's
original mutation. For permission rejection it removes the dedicated account's
role immediately before forwarding. A stale-detail check completes the real
record before forwarding the read. Each fault must be exercised, must return
the expected upstream HTTP status, and must leave the authoritative snapshot
unchanged relative to the state **after** the competing action.

Independent administrator API reads verify order status, totals and discount
state. Read-only SQL queries in the disposable test harness supplement those
reads with history actors/counts and the persisted delivery failure reason,
which the current API does not expose. The mobile application continues to use
only the shared API. Other-courier rejection uses the API's actual 404 privacy
behavior, rather than expecting a 403 that would disclose record existence.

Offline cases temporarily disable airplane-mode connectivity, Wi-Fi and mobile
data, observe disabled native controls, then restore the original radio settings.
They verify unchanged state and zero forwarded business writes through
reconnection. Normal cleanup restores the original radio settings on failure too.

## Evidence and review

Each run starts with a new directory and a failing `summary.json`. The final
summary records device/Android/API/ABI, installed app version/APK hash, app/API/
harness commits, seed IDs, actors, native step reports, per-case snapshots and
sanitized upstream exchanges. A development APK's native hash identifies the
installed binary; `appCommit` identifies the JavaScript served by Metro.

Every passing case also requires an original ADB screenshot. Offline cases
retain an additional screenshot while the action is disabled. Raw Maestro
logs/debug output stays in the ignored `private` subdirectory: it can contain
typed synthetic passwords. Review screenshots, JUnit and JSON before publishing
selected artifacts. Never publish private environment files, device serials,
tokens or raw logs.

Link the focused evidence to #201 and the #186 quality baseline. Record any
unrun scenarios honestly. Only mark issue criteria complete after their live
checks pass; keep #201 open until reviewer **@longtoZ** records approval.
