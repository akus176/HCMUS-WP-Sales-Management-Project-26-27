# Restricted midterm PostgreSQL connection

Use a dedicated local PostgreSQL database for the direct WinUI milestone. An administrator
owns the database and runs migrations/seeding; `coffeeshop_midterm` is only the runtime login.
This account is shared by midterm workflows, not a substitute for the final API's staff RBAC.

## Provision after migrations

1. Create `coffeeshop` as an administrator, then apply the committed EF migrations using
   the administrator's connection in `ConnectionStrings__CoffeeShop`. See the README's
   migration commands. Keep that credential out of the WinUI environment and user settings.
2. Run from the repository root (use your own administrator login and local port):

   ```powershell
   psql -X -h 127.0.0.1 -U postgres -d coffeeshop -v ON_ERROR_STOP=1 -f scripts/postgres/midterm-role.sql
   psql -X -h 127.0.0.1 -U postgres -d coffeeshop
   ```

   In the interactive administrator session, use `\password coffeeshop_midterm` to set a
   unique local password through the hidden prompt, then `\q`. No password is in the SQL
   file or command arguments. The new role has no password until this step.
3. Configure WinUI with that runtime login using `ConnectionStrings__CoffeeShop`, or the
   existing `%LOCALAPPDATA%\CoffeeShop\appsettings.json` configuration source:

   ```json
   {
     "ConnectionStrings": {
       "CoffeeShop": "Host=127.0.0.1;Port=5432;Database=coffeeshop;Username=coffeeshop_midterm;Password=<local-password>"
     }
   }
   ```

   Environment configuration takes precedence. Remove any old administrator environment
   value before using the user-local file. `.env.example` contains placeholders; WinUI
   does not load `.env` automatically. Never commit real passwords or local configuration.
4. Start WinUI and confirm the existing startup health check reports a successful connection.

The setup script runs transactionally and intentionally fails if the role already exists.
It revokes PUBLIC database privileges and table grants in this dedicated database, including
temporary-table creation; other non-owner logins need explicit grants. Do not run it against
a shared database. It does not transfer ownership, change passwords, or modify other databases.
Future migrations remain administrator operations. Review new tables and grant only the
operations their workflows need; no automatic default grants are made for future objects.

## Local networking

On the dedicated local server, set `listen_addresses = 'localhost'` in `postgresql.conf`
and restart PostgreSQL. For containers, publish only `127.0.0.1:5432:5432`, not all interfaces.
Keep these `pg_hba.conf` records **before** broader host or local rules (replace the database
name if needed):

```text
host coffeeshop coffeeshop_midterm 127.0.0.1/32 scram-sha-256
host coffeeshop coffeeshop_midterm ::1/128      scram-sha-256
host all        coffeeshop_midterm 0.0.0.0/0    reject
host all        coffeeshop_midterm ::0/0        reject
local all       coffeeshop_midterm             reject
```

The `local` record applies to Unix-domain sockets; omit it on Windows. Reload PostgreSQL
after editing HBA rules. Use SCRAM password encryption before setting the role password.
The reject records prevent this login from reaching other databases through PostgreSQL's
usual PUBLIC CONNECT grant. Binding and HBA rules are server configuration: a loopback
connection string alone does not restrict server exposure. For a container server, use an
appropriate restricted bridge-address HBA rule for the loopback-published connection.

## Permission boundary and verification

The role can read/write current operational data, replace recipes/compatibility, append
history, and read only staff identity columns needed by POS/delivery. It cannot read password
hashes or refresh tokens, administer staff/roles, rewrite ledgers, truncate tables, create
schemas/tables/temporary tables, run migrations, create databases/roles, or assume the owner.
UUID identifiers need no sequence grants. Deactivation preserves history; physical deletion
is limited to recipe/compatibility replacement and draft receipt-line removal.

Run the automated check against a disposable PostgreSQL server with an administrator login
that can create/drop databases and roles:

```powershell
# Set COFFEESHOP_TEST_POSTGRES locally to the disposable server administrator connection.
dotnet test tests/CoffeeShop.Infrastructure.Tests --configuration Release --filter FullyQualifiedName~MidtermRoleIntegrationTests
```

Tests create uniquely named databases and logins, apply real migrations and this exact SQL
script (substituting only the test login name), then connect as the runtime login. They check
application operations and PostgreSQL permission-denied results, and clean up only their own
resources. Missing configuration reports explicit skips; configured connection errors fail.
CI runs these checks with PostgreSQL 18. These tests verify database permissions; server
binding/HBA configuration must be checked on the actual demo installation.
