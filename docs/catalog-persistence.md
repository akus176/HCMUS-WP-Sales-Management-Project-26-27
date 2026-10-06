# Catalog PostgreSQL adapter

T3.6 / issue #45 implements `ICatalogRepository` and `ICatalogReadRepository` behind the T3.5 Application handlers. Register them with `AddCoffeeShopPostgres(connectionString)`; both repositories share the scoped `CoffeeShopDbContext`, `IUnitOfWork`, and transaction manager. Application and Domain do not reference EF Core.

- Command lookups track entities so validated updates and deactivation are saved by the unit of work. Variant lookups include inactive sizes for duplicate-name validation.
- Compatibility replacement retains overlapping composite keys, removes obsolete links, and adds new links. Recipe replacement deletes the previous size-specific lines and inserts the validated replacement in the handler's transaction. A failure rolls back the entire replacement.
- Searches filter and limit in PostgreSQL, use case-insensitive literal substring matching on names (and menu descriptions), and return deterministically ordered results. Category ordering uses display order, then name and identifier; menu ordering uses name and identifier.
- Management reads include inactive definitions and existing recipes after deactivation. This is not the ordering-availability query. Read repositories return Application read models without tracking entities; menu searches batch-load variants and add-ons rather than querying once per item.
- Recipe writes use current ingredient definitions and Domain validation for activity, quantities, units, and conversion precision. Database foreign keys, unique indexes, and quantity checks remain a second integrity boundary. Configuring recipes does not change stock.

## Migration repair

The pre-existing reservations mapping defined a unique `(table_id, reserved_for)` index filtered to exclude cancelled/completed reservations, but the initial migration/snapshot contained an unfiltered, non-unique index. EF therefore refused normal migration application with `PendingModelChangesWarning`.

`FilterActiveReservations` replaces only that index and refreshes the generated snapshot. Its down migration restores the original index. This repair was explicitly authorized while implementing #45; no catalog schema change is needed. Tests check model/snapshot alignment, clean migration application, and downgrade/upgrade of the index. Existing databases with duplicate active reservation slots must resolve those duplicates before applying the unique index; the migration does not delete business records.

## Real PostgreSQL verification

Point the test configuration at an isolated PostgreSQL server using a role allowed to create/drop databases:

```powershell
$env:COFFEESHOP_TEST_POSTGRES = 'Host=localhost;Port=5432;Database=postgres;Username=test;Password=test'
dotnet test tests/CoffeeShop.Infrastructure.Tests --configuration Release --filter FullyQualifiedName~CatalogPersistenceIntegrationTests
```

Each case creates a uniquely named `coffeeshop_catalog_<guid>` database, applies the committed migrations, exercises the Application handlers/adapter, and drops only its own database. Nothing recreates or deletes the database named in the supplied connection string. With no configuration, cases report explicit skips; an unreachable configured server fails the tests.

The `catalog-postgres` CI job supplies PostgreSQL 18 and executes these tests. Coverage includes complete-drink round trips, updates/deactivation, overlapping recipe and compatibility replacements, empty compatibility, invalid recipes preserving prior data, rollback after save, database constraints, filtering/limits, missing records, empty reads, no tracking, cancellation, and migration upgrade. WinUI screens and HTTP endpoints remain later tasks.
