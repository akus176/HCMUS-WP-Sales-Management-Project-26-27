# Local Docker Compose delivery (#158)

The final API and PostgreSQL 18 run as separate services. PostgreSQL stores data
in the `postgres_data` named volume. The API waits for PostgreSQL health, applies
committed EF Core migrations, then starts listening. `/health` checks the process;
`/health/ready` requires database connectivity and no pending migrations. Both
services publish only to host loopback. WinUI remains a Windows host process.

## Start from a clean checkout

Install Docker with Compose v2. Copy `.env.example` to the ignored `.env` and
replace `POSTGRES_PASSWORD` and `Jwt__SigningKey` with unique local values.
The JWT key must be at least 32 bytes. Keep the issuer and audience as local
development identifiers. Set `POSTGRES_PORT` or `API_PORT` if the default host
ports 5432 or 5232 are already occupied. The example's
`ConnectionStrings__CoffeeShop` value is for direct WinUI development; Compose
constructs its own internal `Host=postgres` connection string.

```sh
cp .env.example .env
# Edit .env locally; do not commit it.
docker compose up --build --detach --wait
docker compose ps
curl --fail http://127.0.0.1:5232/health
curl --fail http://127.0.0.1:5232/health/ready
```

Both services should report `healthy` and both requests should return
`Healthy`. The first API startup applies the eight migrations currently in the
repository; later starts detect that they are already applied. To inspect the
result with default database/user names:

```sh
docker compose exec postgres psql -U coffeeshop -d coffeeshop -c 'SELECT count(*) FROM "__EFMigrationsHistory";'
docker compose logs api --tail=40
```

`docker compose down` stops the services and retains the named volume.
`docker compose down --volumes` also **deletes the local database**; use it only
when intentionally resetting the demo. The API uses only local environment
values and no real credentials are tracked. Do not publish port 5432 to a LAN;
this Compose file binds it to `127.0.0.1`.

## Automated clean-start check

`bash scripts/verify-compose.sh` uses a unique project name, a temporary
private environment file with generated local credentials, and ephemeral host
ports. It builds the committed Dockerfile, starts with a new named volume,
waits for both health checks, verifies both HTTP health endpoints and the
migration-history table, then removes only that test project's containers and
volume. CI runs the same command in the `compose-smoke` job.

The application container runs as an unprivileged user. The Compose startup
script runs migrations once per startup before binding the HTTP port; a failed
migration stops the API instead of reporting a healthy process. This path is
for one local API instance. Coordinated migrations for multiple deployed
replicas and demo seeding are separate work under OpenSpec task 13.2.
