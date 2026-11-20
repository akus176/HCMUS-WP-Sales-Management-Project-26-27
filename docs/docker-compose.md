# Local Docker Compose delivery (#158)

The final API, PostgreSQL 18, and RustFS photo storage run as separate services.
PostgreSQL stores data in the `postgres_data` named volume; RustFS uses `menu_photos`.
The API waits for PostgreSQL health and photo initialization, applies
committed EF Core migrations, then starts listening. `/health` checks the process;
`/health/ready` requires database connectivity and no pending migrations. All
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

## Menu photo storage

RustFS 1.0.5 provides local S3-compatible storage, with the S3 endpoint at
`http://127.0.0.1:9000` and the console at `http://127.0.0.1:9001`.
`STORAGE_PORT` and `STORAGE_CONSOLE_PORT` override those loopback ports.
Set `STORAGE_ACCESS_KEY` and a unique `STORAGE_SECRET_KEY` in the ignored `.env`.
For an existing checkout, generate a secret with
`[Security.Cryptography.RandomNumberGenerator]::GetHexString(48)` in PowerShell.

`docker compose up --detach --wait storage` starts storage, then
`docker compose run --rm storage-init` runs the initializer. Full-stack startup
runs the initializer automatically before the API starts.
It creates the `menu-images` bucket, grants anonymous reads of
known object URLs. Photos are stored only in RustFS and are not checked into
the repository or bundled with WinUI. Rerunning the initializer preserves existing
objects, including pictures replaced through the console. On a fresh volume, run
`python3 scripts/storage/import-unsplash-photos.py` from WSL/Linux to populate the
complete set of 27 seed pictures from Unsplash. This also replaces the original
pictures under their existing storage keys when run against an existing bucket.
The [menu enrichment guide](menu-enrichment.md) records the sources and credits.
The importer keeps downloaded image files outside the checkout and removes them
after upload. Custom pictures can still be uploaded through the console or restored
from a storage backup.
Anonymous requests cannot list, upload, or delete objects.

The pictures live in the `menu_photos` Docker named volume. Stopping or recreating
the container retains them. `docker compose down --volumes` deletes the pictures
and database, so omit `--volumes` to keep them.

Counter cards load pictures from storage rather than the app package. The default
bucket URL is `http://127.0.0.1:9000/menu-images/`. The Windows launcher starts
storage and passes the published URL to WinUI automatically. For a manual
launch with a different host or port, set `COFFEESHOP_MENU_PHOTOS_BASE_URL` in the
WinUI process to the HTTP(S) bucket URL. No storage credentials go into the app.
The current product-to-filename mapping is in `Services/MenuPhotoSource.cs`;
replacing `latte.jpg` updates all drinks mapped to that photo. Decorative app
assets remain packaged.

Sign in to the console using the `.env` storage credentials to upload or replace
pictures. To upload a replacement through the Compose storage client, mount a
local folder containing your pictures (replace `C:/CafePhotos` with that folder):

```powershell
docker compose run --rm --volume 'C:/CafePhotos:/uploads:ro' --entrypoint aws storage-init --endpoint-url http://storage:9000 s3 cp /uploads/latte.jpg s3://menu-images/latte.jpg --content-type image/jpeg --cache-control 'public, max-age=300'
```

Clients may cache photos for up to five minutes. The design reference pages also
load their photographs from the local storage endpoint.

Run `bash scripts/verify-menu-photo-storage.sh` for a disposable storage check.
It creates temporary text objects outside the repository, checks downloaded bytes,
rejects anonymous listing, uploads and deletion, and verifies replacement
preservation and persistence after container recreation. It removes only its own
test containers, volume, and temporary files.

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

Keep logs local until you have checked them for sensitive values. All services
bind to `127.0.0.1`. To stop the stack while keeping data, run `docker compose down`.
Adding `--volumes` deletes the local database and stored photos.
