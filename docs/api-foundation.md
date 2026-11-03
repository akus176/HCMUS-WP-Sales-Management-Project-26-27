# Shared API foundation (#149)

For current authentication, token provisioning, and effective-permission behavior added by #150, see [shared staff identity](staff-identity.md). The foundation verification below records the pre-identity #149 delivery.

The API now composes the existing Application and PostgreSQL services and exposes a versioned `/api/v1` route group. The current HTTP surface is a small catalog slice: authenticated category search, manager-permitted category creation, and manager-permitted deactivation. It uses contract DTOs, RFC 7807 errors, and the existing catalog application handlers. OpenAPI documents only these implemented routes; [the captured development document](evidence/api-149/openapi.json) is stable across request hostnames. Authentication and effective permission issuance belong to #150; the P0 mobile operation routes and complete mobile OpenAPI contract remain in #142. Do not generate a mobile client for planned routes from this partial document.

Set `ConnectionStrings__CoffeeShop` to a local PostgreSQL connection before launching. The API fails clearly at startup when it is absent. Apply migrations from an empty database before using business routes:

```sh
dotnet tool restore
dotnet ef database update --project src/CoffeeShop.Infrastructure.Postgres --startup-project src/CoffeeShop.Infrastructure.Postgres
dotnet run --project src/CoffeeShop.Api
```

`GET /health` checks process liveness. `GET /health/ready` checks PostgreSQL connectivity and returns 503 while the database is unavailable. In Development, `GET /openapi/v1.json` serves a document without a request-dependent `servers` value. Business routes require bearer authentication; no production staff login/token validation exists yet, so #150 must land before they can be used by staff clients. API tests inject a test-only authentication scheme and never expose that scheme in the running API.

## Verification on 2026-11-02

- A temporary PostgreSQL 18 container hosted a fresh `coffeeshop` database. `dotnet ef database update --project src/CoffeeShop.Infrastructure.Postgres --startup-project src/CoffeeShop.Infrastructure.Postgres` applied all migrations through `20261015021700_RepairDeliveryFulfillmentSchema` and exited 0.
- `COFFEESHOP_TEST_POSTGRES=<local PostgreSQL admin connection> dotnet test tests/CoffeeShop.Api.Tests --no-restore` passed 9/9 tests, including fresh-database HTTP responses for 201, 400, 401, 403, 404 and 409, plus liveness and readiness.
- Local runtime probes returned `200` for `/health`, `200` for `/health/ready`, and `401` for anonymous `/api/v1/categories`. Two OpenAPI requests with different `Host` headers had the same SHA-256: `a7e6471b21fdd727145cb12c69888490c4eb1a7a51356e1921f7f87377626dff`.
- The captured OpenAPI document contains `/api/v1/categories` and `/api/v1/categories/{id}/deactivate`, with category, create-request, and problem schemas. It contains no planned auth or mobile P0 operation paths.

The PostgreSQL CI job now runs API tests with `COFFEESHOP_TEST_POSTGRES` configured, while the Windows job runs the non-database API tests. The mobile task 2.3 checkbox remains open until #142 publishes and verifies all required P0 schemas.
