# Local API regression collection

This Postman v2.1 collection checks API liveness/readiness, anonymous access,
successful manager/cashier sign-in, catalog reads, forbidden writes, validation,
and not-found responses. Its write requests are expected to fail with 400 or 403
before changing data. Requests use environment variables and session tokens
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

The environment file contains blank credential fields only. Do not export or
commit an environment after entering local passwords. The live-API Postman run
is separate from the isolated PostgreSQL integration tests.

## Automated local and CI run

The runner is shared with [#202](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/202).
It uses pinned Postman CLI 1.90.0, with cloud reporting disabled, and rejects
missing, incomplete, skipped or failed results. Node.js 20 or newer is required;
the repository's mobile Node pin is used in CI.

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

The nine requests use temporary private environment/report files that are removed
after execution. Published evidence contains only request names, methods, paths,
statuses and assertion outcomes. Never publish raw CLI exports or environment
files. `bash scripts/verify-compose.sh` performs provisioning, both positive and
intentional invalid-password runs, and disposable-stack cleanup automatically.
