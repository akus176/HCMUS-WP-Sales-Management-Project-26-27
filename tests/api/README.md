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
