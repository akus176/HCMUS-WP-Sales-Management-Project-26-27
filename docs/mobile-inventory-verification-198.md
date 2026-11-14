# Focused Android inventory and receipt verification (#198)

This runner covers Mobile 4.4 and 5.6, and the inventory/receipt subset of
6.1. It records mobile evidence for later shared-state acceptance under 6.4.
The canonical OpenSpec checklist is issue #95. Windows UI/network evidence
belongs to #200; simultaneous shared-state acceptance belongs to #202/#203.
The aggregate Android baseline remains #186. None of those broader gates is
completed by this focused suite.

## Prerequisites

Use the pinned Node/npm, JDK 17, Maestro 2.12.1, ADB, the installed development
APK, and a running Metro server. Follow [the environment guide](mobile-environment-139.md)
and [the contributor runbook](mobile-quality-and-release.md) for device setup,
API migration, and account provisioning. Use a disposable PostgreSQL 18 database
in Docker and dedicated synthetic `issue198Admin` and `issue198InventoryStaff`
accounts. The inventory account must have exactly the `InventoryStaff` role.
Keep credentials in an ignored local file or process environment.

The runner seeds fresh, uniquely named records on every execution. It never
resets an existing shop database. For the live 403 case it temporarily removes
only the dedicated account's InventoryStaff role through `docker exec ... psql`,
then restores that association in `finally`. Other accounts and role permission
bundles are untouched. Explicit `COFFEESHOP_INVENTORY_DISPOSABLE=1` is required.
If a runner is forcibly terminated, restore that synthetic association before
reusing the environment; a normal failure records restoration failures.

## Configure and run

Configure these values in ignored `.env.inventory.local` under
`apps/staff-mobile` (or set them in the process environment):

| Variable                                                                        | Value                                                                 |
| ------------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| `COFFEESHOP_INVENTORY_DISPOSABLE`                                               | `1`, only for the disposable environment                              |
| `COFFEESHOP_INVENTORY_API_URL`                                                  | Upstream `http://127.0.0.1:5232/api/v1`                               |
| `COFFEESHOP_INVENTORY_PROXY_HOST`                                               | The host's private LAN IPv4                                           |
| `EXPO_PUBLIC_APP_ENV`                                                           | `device` for a physical device                                        |
| `EXPO_PUBLIC_API_BASE_URL`                                                      | `http://<HOST-LAN-IP>:5233/api/v1` for a physical device              |
| `COFFEESHOP_INVENTORY_USERNAME` / `COFFEESHOP_INVENTORY_PASSWORD`               | Dedicated `issue198InventoryStaff` credentials                        |
| `COFFEESHOP_INVENTORY_ADMIN_USERNAME` / `COFFEESHOP_INVENTORY_ADMIN_PASSWORD`   | Synthetic administrator credentials                                   |
| `COFFEESHOP_INVENTORY_POSTGRES_CONTAINER`                                       | Disposable Docker container name                                      |
| `COFFEESHOP_INVENTORY_POSTGRES_DATABASE` / `COFFEESHOP_INVENTORY_POSTGRES_USER` | Disposable database and PostgreSQL role                               |
| `JAVA_HOME` / `MAESTRO_HOME`                                                    | JDK directory and extracted Maestro directory containing `lib`        |
| `ADB_PATH`                                                                      | Optional explicit ADB executable                                      |
| `ANDROID_SERIAL`                                                                | Optional device selection; required if multiple devices are connected |
| `COFFEESHOP_INVENTORY_RESULTS`                                                  | Optional local result root, preferably on a drive with free space     |
| `COFFEESHOP_INVENTORY_APP_COMMIT` / `COFFEESHOP_INVENTORY_API_COMMIT`           | Exact application and API source commits                              |

Configure Metro with the same public profile and **forwarding listener URL**
on port 5233; restart it after changing configuration. For a physical phone,
the forwarding listener binds only the supplied LAN interface. The upstream API
remains loopback-only for the harness. This configuration uses the normal
development cleartext policy; it does not change release networking rules.
ADB reverse for port 8081 keeps Metro reachable over USB.

From `apps/staff-mobile`:

```text
npm run test:inventory-harness
npm run test:e2e:inventory
```

To keep secrets in a separate local file instead:

```text
node --env-file=<private-local-file> scripts/verify-android-inventory.mjs
```

The runner invokes Maestro's Java entry point with an argument array, without
a shell or credentials in command-line arguments. It runs only the focused
YAML flows under `.maestro/inventory-focused`, not the all-role quality suite.

## What is verified

- Current search, empty results, low-stock emphasis at and below the threshold,
  and exclusion of above-threshold stock from the low-stock view.
- Manual refresh after an independent API session changes a seeded balance.
- Required adjustment reason, one successful adjustment, visible movement
  history, and one actor-attributed persisted movement.
- Real 400 validation, 403 permission, and 409 adjustment conflict responses;
  independent API snapshots prove each rejected request adds no stock effect.
- An adjustment committed before its response is lost, followed by an explicit
  retry with the exact same request ID and body and no duplicate movement.
- Prepared receipt review and posting, empty-receipt prevention, a competing
  real receipt post, refresh after an ambiguous result, and deliberate retry.
- Server receipt status, balance, movement count, receipt reference, and actor
  are independently read through the administrator's authenticated API session.

The forwarding listener does not fabricate business responses. A conflict is
created by committing a competing request to the real API before forwarding
the original one. For a lost-response case it consumes a successful upstream
response, verifies committed state, and only then drops the downstream socket.
The relevant behavior is commit-then-response-loss, rather than disconnecting
before submission. A fault must be consumed and return the expected upstream
status before its case can pass.

## Evidence and failure handling

Each invocation creates a new result directory and an initially failing
`summary.json`. Every flow must exit successfully and produce exactly one fresh
passing JUnit case; missing, failed, or skipped reports cannot count as passes.
Each case also requires its independent server assertions and a genuine ADB
screenshot. A failure stops the run and leaves later cases as not run.

The summary records model/Android/API/ABI, installed app version and APK hash,
app/API/harness commits, fresh seed IDs, per-case outcomes, authoritative
snapshots, and sanitized mutation paths/statuses. Screenshots are original
device captures. The `private` directory contains raw Maestro/debug output,
which can contain typed credentials; never publish it. Review screenshots,
JUnit and JSON before copying selected evidence into the repository.

Do not combine partial runs into a passing report. Keep #198 open for the named
reviewer's approval, and keep parent 6.4 incomplete until matching Windows and
Android shared-state evidence is available.
