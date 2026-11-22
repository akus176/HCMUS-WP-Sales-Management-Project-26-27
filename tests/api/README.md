# Local API regression collection

This Postman v2.1 collection checks API liveness/readiness, anonymous access,
successful manager/cashier sign-in, catalog reads, forbidden writes, validation,
not-found responses, and an unsupported HTTP method (DELETE on categories).
The existing category write probes fail with 400 or 403; the unsupported DELETE
fails with 405. None changes catalog data.
Requests use environment variables and session tokens
captured by the collection scripts, so request bodies do not need per-run editing.

## Run against the local Docker API

1. Follow [the Docker Compose setup](../../docs/docker-compose.md) and start a
   disposable local PostgreSQL/API stack. The collection defaults to
   `http://127.0.0.1:5232`.
2. Provision two synthetic local staff accounts using the README's
   `--provision-staff` instructions: a manager with `menu.manage` and a cashier
   without it. Use passwords created locally; do not put them in the repository.
3. Import `coffee-shop-local.postman_collection.json` and
   `coffee-shop-local.postman_environment.json` into Postman. Select the
   **Coffee Shop local Docker API** environment and set the manager/cashier
   usernames and passwords in its local values.
4. Run the complete collection in order. The login requests save access tokens
   as collection variables. The manager read and cashier forbidden-write checks
   use those tokens automatically.

The unsupported DELETE must return HTTP 405 with an `application/problem+json`
body whose `status` is 405. The Compose runner's post-collection count check confirms
the catalog still has five categories. This distinguishes a registered path with
the wrong method from the collection's separate unknown-path 404 case.

The environment file contains blank credential fields only. Do not export or
commit an environment after entering local passwords. The live-API Postman run
is separate from the isolated PostgreSQL integration tests.

## Automated Docker verification

From a Linux checkout with Docker Compose, run:

```sh
npm ci --prefix tests/api
bash scripts/verify-compose.sh
```

This starts an isolated PostgreSQL/API project with generated local credentials,
checks health and migrations, applies the demo seed twice, provisions synthetic
Manager/Cashier accounts, and executes the unchanged collection. The host runs the Postman CLI
pinned in `package-lock.json`; Node.js follows `apps/staff-mobile/.nvmrc`. The run needs no payload editing or Postman
account. It also verifies that intentionally invalid credentials make the runner
fail. Its own temporary services and database volume are removed on exit.

The command publishes only sanitized `TestResults/api-collection.json` and
`TestResults/api-collection-negative.json`: request names/methods/paths/statuses, assertion names and
outcomes, and aggregate counts. It never publishes tokens, credentials, headers,
response bodies, query strings, raw CLI reports, or assertion error messages.
CLI event reporting is disabled. Private environment/report files are created
in an owned temporary directory and removed after the run. A configuration or
execution failure cannot leave an earlier passing result behind.

For an already running disposable API, set these variables **privately** in the
local process environment:

- `COFFEESHOP_COLLECTION_API_HOST`: API origin, such as `http://127.0.0.1:5232`.
- `COFFEESHOP_COLLECTION_MANAGER_USERNAME` and `COFFEESHOP_COLLECTION_MANAGER_PASSWORD`.
- `COFFEESHOP_COLLECTION_CASHIER_USERNAME` and `COFFEESHOP_COLLECTION_CASHIER_PASSWORD`.

```sh
npm ci --prefix tests/api
npm test --prefix tests/api
npm run verify --prefix tests/api -- /tmp/collection.json
```

One host setting derives both health and `/api/v1` addresses. Use a physical
Windows checkout for npm scripts; Windows command shells cannot use a WSL UNC
directory as their working directory. Credentials stay out of command-line
arguments and tracked environment files.

The CLI runs a local v2.1 collection with local JSON reporting; see the
[official Postman CLI documentation](https://learning.postman.com/docs/postman-cli/postman-cli-collections/).

## Repeated concurrency verification

With `COFFEESHOP_TEST_POSTGRES` privately set to a disposable PostgreSQL admin
connection, run:

```sh
COFFEESHOP_CONCURRENCY_ITERATIONS=5 \
  COFFEESHOP_CONCURRENCY_RESULTS=/tmp/coffee-shop-concurrency \
  bash scripts/verify-concurrency.sh
```

The existing integration tests create isolated databases. The runner repeats all
tests named `Concurrent` and the named cross-order restoration, cancelled
replacement and stale-ingredient lock cases in the Infrastructure and API suites, rejects missing
or skipped results, and requires the original confirmation, table occupancy,
cancellation/restoration, loyalty reversal and refresh-reuse cases in every
iteration. It does not alter the existing test assertions. CI repeats three
iterations and retains the TRX files plus sanitized summary.

These automated results do not prove a native Windows/Android session. That
shared-state evidence remains tracked in #202/#203.

## Windows collection example

```powershell
npm ci --prefix tests/api
npm test --prefix tests/api
$env:COFFEESHOP_COLLECTION_API_HOST = 'http://127.0.0.1:5232'
$env:COFFEESHOP_COLLECTION_MANAGER_USERNAME = 'local-manager'
$env:COFFEESHOP_COLLECTION_CASHIER_USERNAME = 'local-cashier'
$env:COFFEESHOP_COLLECTION_MANAGER_PASSWORD = Read-Host 'Manager password' -MaskInput
$env:COFFEESHOP_COLLECTION_CASHIER_PASSWORD = Read-Host 'Cashier password' -MaskInput
try {
    New-Item -ItemType Directory -Path TestResults -Force | Out-Null
    node tests/api/run-collection.mjs TestResults/api-collection.json
    if ($LASTEXITCODE -ne 0) { throw 'API collection failed.' }
} finally {
    Remove-Item Env:COFFEESHOP_COLLECTION_MANAGER_PASSWORD
    Remove-Item Env:COFFEESHOP_COLLECTION_CASHIER_PASSWORD
}
```
