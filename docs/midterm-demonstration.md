# Midterm demonstration checklist — T8.5 / #103

Executed on **2026-10-26** for [issue #103](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/103).
Owner: @vt-Toan. Required reviewer: @akus176. All steps below passed; no critical
blockers remain for this demonstrated path. Milestone acceptance still requires review.

## Environment and evidence

- Windows x64, .NET SDK 10.0.401, Windows App SDK 1.8.260804001, PostgreSQL 18.6.
- PostgreSQL ran in an isolated container published only on `127.0.0.1:55433`.
- Every scripted demonstration test created a new uniquely named database, applied all
  eight committed migrations, loaded the real seed, provisioned the real restricted role
  script under a unique login name, and then ran client workflows with that restricted login.
  Database creation, migrations, seed, and role provisioning used the separate owner connection.
  Each test removed its database and login afterward.
- The actual `AddCoffeeShopDirectClient` registration is compiled into the tests. POS,
  customer, inventory audit, and operations steps execute the same workspace methods as
  the WinUI pages. Catalog/procurement/report steps execute the registered application services.
- The scripted demonstration is reproduced by [complete path and rollback checks](../tests/CoffeeShop.Infrastructure.Tests/MidtermDemonstrationIntegrationTests.cs),
  [seed validation](../tests/CoffeeShop.Infrastructure.Tests/MidtermSeedDataIntegrationTests.cs), and
  [client registration check](../tests/CoffeeShop.Infrastructure.Tests/DirectClientCompositionTests.cs).
- The [current UI evidence index](ui-evidences/README.md) contains the six workspace captures and
  form captures from the UI parity verification. The captures were regenerated on 2026-11-01
  from a clean database using the current midterm seed and the documented parity supplement.
  Native checks verify startup, navigation, loaded controls/data, and report rendering.
  The full sequence of mutations is demonstrated by the scripted workspace run; this is
  not a claim that every transaction was repeated through pointer/keyboard automation.

## Executed checklist

Evidence IDs below refer to the `PASS DEMO-*` lines in the scripted run output.

| Step | Input / expected behavior | Actual result | Status / evidence |
| --- | --- | --- | --- |
| 1. Rebuild and seed | Start empty; apply every migration; load minimum dataset; connect as restricted runtime login. | No pending migrations; 20 products, 5 categories, 12 customers, 24 orders. Seed also passed a separate two-run repeatability/relationship/unit-conversion check. | PASS — DEMO-01, seed validation, composition check |
| 2. Supplier and receipt | Create/update supplier; create draft receipt; receive 100g; retry posting. | Receipt added 100g once; second post rejected without increasing stock. Receipt-linked movement recorded under the selected actor. | PASS — DEMO-02 |
| 3. Stock adjustment and history | Reject a blank reason; apply -5g, +5g, -5g with reasons; refresh history. | Blank reason left stock unchanged. Balance 95g; four immutable movements including the receipt. | PASS — DEMO-02 |
| 4. Menu and recipes | Create category, 40.00 regular drink, 5.00 compatible topping, and 10g recipe; update drink. | Persisted drink/variant/compatibility/recipe reloaded by POS. Drink with topping totals 45.00. Deactivation later excluded it from new POS selection. | PASS — DEMO-03, DEMO-05, DEMO-08 |
| 5. Customer and address | Create/update profile, add default saved delivery address, configure earning/redemption rules. | Profile and address reloaded; earning 1 point per currency unit, redemption value 1.00, cap 50%. | PASS — DEMO-04 |
| 6. Dine-in and discount | Save 45.00 ticket with customer/table; request fixed 5.00 discount; approve; confirm; prepare; mark ready; pay cash; complete. | Pending request left total 45.00. Approval changed total to 40.00. Unpaid completion rejected; payment/completion succeeded; table released; completion retry rejected. | PASS — DEMO-05-dine_in |
| 7. Walk-in takeaway | Save/confirm 45.00 walk-in ticket; prepare/ready; pay by card; complete. | Ticket completed for 45.00 without customer or delivery data; no customer points awarded. Unpaid completion and completion retry rejected. | PASS — DEMO-05-takeaway |
| 8. Delivery | Use saved customer address; confirm/prepare/ready; pay 45.00 by e-wallet; assign eligible courier; dispatch; deliver; complete. | Active seeded `DeliveryStaff` selected; delivery reached `delivered`; order reached `completed`. | PASS — DEMO-05-delivery |
| 9. Loyalty earning | Refresh customer after attached dine-in and delivery orders complete. | Balance increased by 40 + 45 = 85 points; exactly two earning entries despite completion retries. | PASS — DEMO-06 |
| 10. Cancellation restoration | On another dine-in ticket redeem 10 points; confirm; cancel with reason; repeat cancellation. | Redemption reduced total 40.00 → 30.00; confirmation consumed 10g. Cancellation restored exactly 10g, released table, restored points to 85, and retained four loyalty entries. Retry had no duplicate effects; one restoration guard and one consumption/restoration pair. | PASS — DEMO-07 |
| 11. Reports and final stock | Query revenue, best sellers, channels, and low stock after three completed and one cancelled tickets. | Revenue increased exactly 130.00; channel revenues reconciled; new drink quantity 3; cancelled sale excluded; low-stock rows present; stock 65g. Audit workspace displayed nine immutable movements including cancellation restoration. | PASS — DEMO-08 |
| 12. Completion rollback | Remove loyalty configuration as owner after a paid pastry order reaches ready; attempt completion; restore rules; retry. | Missing settings returned a failure; order stayed ready, table stayed occupied, and no completion history or earning entry persisted. Restoring rules allowed completion/table release and exactly one earning entry. Pastry confirmation used the supported `pcs` recipe unit. | PASS — DEMO-09 |
| 13. Native client | Launch against a separately rebuilt/seeded database using the restricted login; open every module. | Startup connected; POS, operations, catalog, customers, ingredients, suppliers, receipts, low stock, and audit pages loaded. The current parity capture shows 20 menu items, 9 ingredients, current low-stock rows, and all six workspaces. | PASS — native parity capture 2026-11-01; [capture manifest](ui-evidences/current-capture-manifest-2026-11-01.json) |

## Reproduce

1. Install the prerequisites in the README and make an isolated PostgreSQL server available.
   Set `COFFEESHOP_TEST_POSTGRES` locally to an administrator connection that can create/drop
   databases and roles. Do not use a shared or production server.
2. From the repository root, run:

   ```powershell
   ./scripts/verify-midterm.ps1
   ```

   Expected: all five test projects pass with zero failures/skips. The wrapper rejects missing
   configuration, failed tests, zero executed tests, or skipped tests. Raw evidence is
   `artifacts/midterm/<test-project>.trx`; detailed output contains the step IDs above.
3. To repeat the desktop smoke check, create a separate empty demo database, apply migrations,
   run [seed setup](midterm-seed-data.md) as owner, and provision the
   [restricted runtime role](midterm-postgres-role.md). Set the client's connection to that role
   and launch WinUI. Select an active operator in POS or Stock Adjustments before receipt posting
   or stock adjustments. Inspect all navigation destinations and refresh October 2026 reports.
   Report date/payment/channel controls wrap so the refresh button remains visible.

The seed is a baseline; do not rerun it between steps in a demonstration. Rebuild a fresh
database for a new run so post-demo records and ledgers are not mistaken for baseline data.

## Verification and resolved defects

[Verification summary](evidence/midterm/verification.txt) records the combined suite: **1,202 passed,
zero failed, zero skipped** (413 Domain, 422 Application, 258 Infrastructure, 108 WinUI workspace,
1 API). The verification script runs all five test projects. Release solution build passed with zero
warnings/errors. Repository format verification passed. The previous GitHub Actions run reported
121 non-PostgreSQL tests passed and 89 PostgreSQL-dependent tests skipped because the Windows job
ran the whole solution without a PostgreSQL service. This PR changes the Windows job to select the
four non-PostgreSQL projects explicitly; the separate PostgreSQL job remains responsible for the
258 Infrastructure tests. Do not claim the new CI counts until the PR workflow completes.

The demonstration uncovered and fixed the inventory repository registration, concurrent report
queries on one context, missing eligible courier seed, unsupported pastry units, missing completion
earning/POS redemption integration, disabled adjustment/history screens, hard-coded receipt actor,
clipped report refresh controls, and inventory pages retaining stale database contexts across operations.
Inventory/procurement/low-stock pages now create a fresh service scope for each operation so
refreshes observe stock changes committed by POS and audit workflows.
Completion and loyalty remain in the same database transaction;
the rollback check proves a loyalty failure cannot partially complete the order or release its table.

Final API authentication/authorization remains separate work; the full valid/invalid/boundary test-case record across all core modules is documented in the [midterm test-case report](midterm-test-cases.md) for [T8.6 / #104](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/104).
