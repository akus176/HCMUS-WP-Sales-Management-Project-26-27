# Issue #179: mobile devices and operational events

This evidence maps canonical OpenSpec issue #95 tasks **Mobile 2.2, 2.3 and 2.4** to issue #179. The first three issue commits implement task 2.2, the hub commit implements task 2.3, and the post-commit event commit implements task 2.4. Contract and verification commits cover all three.

The registration endpoints are `POST /api/v1/mobile-devices`, `PUT /api/v1/mobile-devices/{id}` and `DELETE /api/v1/mobile-devices/{id}`. They require a staff bearer token; rotation and revocation are restricted to the registration owner. An active token is unique across users. The invalid-token cleanup service is available for the future push provider to call; push delivery is outside this issue.

The authenticated SignalR hub is `/api/v1/operational-events`. Clients cannot choose groups. Each `OperationalEvent` contains only `type`, `id` and `updatedAt`: order, discount, payment and delivery events use the order ID; inventory events use the ingredient ID; account events use the user ID. Recipients must refetch data through the authorized API. The server rechecks permissions and delivery ownership when dispatching, including for existing connections. PostgreSQL notifications are delivered after commit. They are transient invalidation hints: clients must refetch after reconnecting.

## Verification

Run with `COFFEESHOP_TEST_POSTGRES` set to an isolated PostgreSQL administrative database whose user can create and drop test databases:

```powershell
dotnet test tests/CoffeeShop.Api.Tests --no-restore
dotnet test tests/CoffeeShop.Infrastructure.Tests --no-restore
dotnet build src/CoffeeShop.Api -c Release --no-restore
```

The API suite covers the device endpoints, cleanup after rotation, hub authentication and group selection, revocation of permissions during a connection, courier isolation, event privacy, every mapped event area, and commit versus rollback. The infrastructure suite includes applying migrations to a fresh database and testing the active-token unique constraint.

Regenerate and check the contract using the pinned dependencies in `contracts/mobile-p0`:

```sh
npm ci --prefix contracts/mobile-p0
npm run check --prefix contracts/mobile-p0
```

The PostgreSQL integration tests create and remove temporary databases. Never point them at a production server. Provider delivery and real-device push verification are tracked separately.
