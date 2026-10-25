# Midterm demonstration seed data

The PostgreSQL seed is a deterministic, repeatable dataset for the midterm demo. It is intended for a local demo database after the current EF migrations have been applied. It does not run from the WinUI startup path.

## Seed a database

Run the seed as the database owner against the migrated demo database:

```powershell
psql -X -h 127.0.0.1 -U postgres -d coffeeshop -v ON_ERROR_STOP=1 -f scripts/postgres/midterm-seed.sql
```

The script uses stable IDs and a `MIDTERM-` / `Midterm ` namespace. Re-running it replaces only its own records. It keeps a demo manager row with a placeholder password hash; this user is sample audit data and cannot be used to authenticate.

The dataset contains five categories, 20 products with two sizes and recipes each, nine ingredients, three suppliers, two posted receipts, 12 customers, eight tables, and 24 orders. Orders include dine-in, takeaway, and delivery channels, successful payments, approved discounts, cancelled orders, loyalty earning and redemption, all five delivery statuses, and a low-stock ingredient.

## Validate a rebuilt database

Set `COFFEESHOP_TEST_POSTGRES` to a disposable PostgreSQL connection whose user can create and drop databases. Then run:

```powershell
dotnet test tests/CoffeeShop.Infrastructure.Tests/CoffeeShop.Infrastructure.Tests.csproj --filter FullyQualifiedName~MidtermSeedDataIntegrationTests
```

The integration test creates a uniquely named database, applies all migrations, runs the seed twice, checks minimum counts and required relationships, and confirms the second run has the same dataset counts. It drops the temporary database afterward. Without the environment variable, the PostgreSQL integration test is skipped.
