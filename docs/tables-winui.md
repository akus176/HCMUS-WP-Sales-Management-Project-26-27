# Tables workspace

The desktop client has a **Tables** workspace under **Manage** for dining-table
setup. It requires the `tables.manage` permission, which is granted to the
Admin and Manager role bundles. Cashiers and floor staff keep using the Counter
table picker and the Service board overview without this setup surface.

Existing databases need one `dotnet CoffeeShop.Api.dll --seed-staff-roles` run
(or the provisioning command) so the new permission link is created before the
workspace appears.

## Surface

- A list of every dining table with its number, seat count, live
  occupied/available state, and the active order number when occupied.
- **New table** creates a table from a number (required, 20 characters or
  fewer) and an optional positive seat count.
- **Edit** changes the number and seat count. Renaming a table updates the
  Counter and Service board labels; it does not rewrite past orders.
- **Remove** deletes the table after confirmation. An occupied table cannot be
  deleted; release it by completing its order first. Deleting a table clears the
  table link on historical orders through the `ON DELETE SET NULL` foreign key,
  so past orders keep their totals and lines while losing the table reference.
- The empty state prompts the first table so a fresh database can enable
  dine-in ordering without SQL.

## API

All routes require `tables.manage`.

| Route | Purpose |
|---|---|
| `GET /api/v1/table-management` | List tables with active order numbers |
| `POST /api/v1/table-management` | Create a table `{ tableNumber, seats }` |
| `PUT /api/v1/table-management/{id}` | Update number and seats |
| `DELETE /api/v1/table-management/{id}` | Delete an unoccupied table |

`GET /api/v1/tables` and `GET /api/v1/pos/tables` keep returning the tables the
Counter and Service board need. They are unaffected by the management routes.

## Verification

`TableCommandHandlerTests` cover creation, duplicate numbers, validation, edits,
occupied-table deletion conflicts, successful deletion, and number reuse.
`TableManagementWorkspaceTests` cover loading, refresh after create/delete, and
conflict feedback. Native Windows layout and screen-reader review should confirm
the list rows and dialogs at wide and narrow sizes.
