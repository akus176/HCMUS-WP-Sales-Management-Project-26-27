# Local Docker Compose delivery (#158)

The final API and PostgreSQL 18 run as separate services. PostgreSQL stores data
in the `postgres_data` named volume. The API waits for PostgreSQL health, applies
committed EF Core migrations, then starts listening. `/health` checks the process;
`/health/ready` requires database connectivity and no pending migrations. Both
services publish only to host loopback. WinUI remains a Windows host process.

## Start from a clean checkout

Follow the [Windows quickstart](../README.md#start-the-api-and-winui-on-windows)
for executable PowerShell steps, including generating local credentials, provisioning
a staff account, and launching WinUI. The JWT key must be at least 32 bytes.
Compose constructs the API's internal connection string with `Host=postgres`.

## Demo dataset

After the API is ready, `docker compose run --rm demo-data` starts a one-off
PostgreSQL client and runs the [existing seed SQL](../scripts/postgres/midterm-seed.sql).
Normal Compose startup leaves the demo dataset unloaded. Use this command only
on a disposable demo database. A rerun replaces the sample rows, so it is useful
for verifying a clean dataset before you start making changes. Create another
disposable volume if you need to preserve changes to the first one.

| Data | Count on a clean demo database |
| --- | ---: |
| Categories, products | 5, 20 |
| Sizes, recipe rows | 40, 40 |
| Ingredients, suppliers, purchase receipts | 9, 3, 2 |
| Customers, tables, orders | 12, 8, 24 |

The seed contains two midterm operators with placeholder password hashes. Provision
a separate account to sign in through the API. For development without demo data,
follow the same quickstart and skip the `demo-data` command.

## Automated clean-start check

`bash scripts/verify-compose.sh` uses a unique project name, a temporary
private environment file with generated local credentials, and ephemeral host
ports. It builds the committed Dockerfile, starts with a new named volume,
waits for both health checks, and verifies both HTTP endpoints and the migration
history. It verifies the exact demo counts on two seed runs and checks that staff
role provisioning installs all six roles. It removes only the test project's
containers and volume. CI runs the same command in the `compose-smoke` job.

Install the pinned collection runner first with `npm ci --prefix tests/api`.
The smoke check also provisions temporary Manager/Cashier accounts and runs the
actual Postman collection without editing its payloads. A second run must reject
an intentionally invalid manager password while the cashier still signs in.
Only sanitized request/status/assertion results are retained in
`TestResults/api-collection*.json`; raw responses and token-bearing runner reports
are deleted. The catalog must still contain exactly five categories afterwards.

The application container runs as an unprivileged user. The Compose startup
script runs migrations before binding the HTTP port. A failed migration stops
startup. The Compose stack runs one local API instance.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Docker is unavailable or reports `windows` | Start Docker Desktop, switch to Linux containers, and run `docker info --format '{{.OSType}}'` again. |
| Port is already in use | Change `API_PORT` or `POSTGRES_PORT` in `.env`, then rerun `docker compose up --build --detach --wait`. Update the health URLs and `COFFEESHOP_API_BASE_URL` if you change the API port. |
| API remains unhealthy | Run `docker compose ps` and `docker compose logs --tail=40 api postgres`. Check for migration failures or stale credentials. PostgreSQL uses the password from first initialization. If you change it, restore the original value for that volume or reset a disposable volume. |
| WinUI cannot reach the API | Check `/health/ready` and set `COFFEESHOP_API_BASE_URL` to the loopback address, correct port, and `/api/v1/` suffix. Restart the WinUI process after editing environment variables. |
| WinUI rejects the sign-in | Use the account created by `--provision-staff` and the password you entered at the prompt. If you forgot the password, provision another disposable account with a new username and email. |

Keep logs local until you have checked them for sensitive values. Both services
bind to `127.0.0.1`. To stop the stack while keeping data, run `docker compose down`.
Adding `--volumes` deletes the local database.
