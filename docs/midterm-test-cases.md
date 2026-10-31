# Midterm test-case report — T8.6 / #104

Produced on **2026-10-27** for [issue #104](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/104).  
Owner: @longtoZ. Required reviewer: @akus176. Parent milestone: [#26](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/issues/26).

---

## 1. Executive summary

This document provides the canonical test-case record for the **Coffee Shop Management System** midterm milestone, satisfying OpenSpec task `T8.6`. It covers functional verification across all eight core modules:
1. **Catalog & Menu**
2. **Customers & Delivery Addresses**
3. **Loyalty Program**
4. **Inventory & Procurement**
5. **Orders & Sales Lifecycle**
6. **Payments & Cashiering**
7. **Fulfillment (Dine-in, Takeaway, Delivery)**
8. **Operational Reports & Analytics**

Every test case records its **Test ID, feature, category (Valid / Invalid / Boundary), precondition, input, expected result, actual result, status**, and **traceable evidence link** to automated tests, CI artifacts, or demonstration evidence.

### 1.1 Test execution metrics

| Metric | Target | Actual | Evaluation |
| :--- | :--- | :--- | :--- |
| **Total Test Suite** | > 1,000 automated tests | **1,132 tests** | Complete |
| **Domain Tests** | Core business invariants | **413 passed**, 0 failed, 0 skipped | 100% pass |
| **Application Tests** | Use cases, handlers, orchestration | **422 passed**, 0 failed, 0 skipped | 100% pass |
| **Infrastructure Tests** | EF Core, PostgreSQL persistence, migrations | **257 passed**, 0 failed, 0 skipped | 100% pass |
| **WinUI Workspace Tests** | Native view-models, state machines | **39 passed**, 0 failed, 0 skipped | 100% pass |
| **API Smoke Tests** | Composition root & health check | **1 passed**, 0 failed, 0 skipped | 100% pass |
| **Build Warnings & Errors** | 0 warnings, 0 errors | **0 warnings, 0 errors** (`dotnet build -c Release`) | Clean |
| **Code Formatting** | Exact repository style | **Verified** (`dotnet format --verify-no-changes`) | Clean |
| **Midterm Demonstration** | All 13 scripted & native steps | **13 / 13 passed** (`PASS DEMO-01` to `DEMO-09`) | Verified |

---

## 2. Test environment & configuration

- **Operating System:** Windows 10/11 x64
- **Runtime Environment:** .NET 10.0 SDK (`net10.0`, `net10.0-windows10.0.19041.0`)
- **Desktop Framework:** Windows App SDK 1.8.260804001 (WinUI 3 unpackaged)
- **Database Engine:** PostgreSQL 16 / 18
- **Database Runtime Role:** Restricted `coffeeshop_midterm` runtime login (least-privilege DML access only, no DDL/DROP rights)
- **Seed Dataset:** Standard repeatable seed in [`scripts/postgres/midterm-seed.sql`](../scripts/postgres/midterm-seed.sql) (20 products, 5 categories, 12 customers, 24 baseline orders)
- **Primary Automated Script:** [`scripts/verify-midterm.ps1`](../scripts/verify-midterm.ps1) (TRX evidence retained in `artifacts/midterm/midterm.trx`)

---

## 3. Module traceability matrix

| Module | Core Domain Entities | Application Handlers / Services | Infrastructure Persistence | Primary Automated Test Classes |
| :--- | :--- | :--- | :--- | :--- |
| **Catalog** | `Category`, `Product`, `SizeVariant`, `Addon`, `Recipe` | `CatalogLifecycle`, `AddProduct`, `UpdateProduct` | `CatalogRepository`, `CoffeeShopDbContext` | `CatalogLifecycleTests.cs`, `AddonTests.cs`, `SizeVariantTests.cs`, `CatalogPersistenceIntegrationTests.cs` |
| **Customers** | `Customer`, `CustomerAddress`, `PhoneNumber` | `CustomerLifecycle`, `AddressManagement` | `CustomerRepository` | `CustomerTests.cs`, `CustomerAddressTests.cs`, `CustomerLoyaltyPersistenceIntegrationTests.cs` |
| **Loyalty** | `LoyaltyLedger`, `LoyaltySettings`, `LoyaltyTier` | `LoyaltyCalculationEngine`, `EarningWorkflow` | `LoyaltyLedgerRepository` | `LoyaltyRulesTests.cs`, `LoyaltySettingsTests.cs`, `LoyaltyLedgerTests.cs` |
| **Inventory** | `Ingredient`, `Supplier`, `PurchaseReceipt`, `StockMovement` | `PurchaseReceiptLifecycle`, `StockAdjustmentService` | `InventoryRepository`, `StockAuditRepository` | `IngredientTests.cs`, `PurchaseReceiptTests.cs`, `SupplierTests.cs`, `ReceiptPostingIntegrationTests.cs` |
| **Orders** | `Order`, `OrderLine`, `TableOccupancy`, `Discount` | `OrderDraftFactory`, `OrderConfirmationService` | `OrderRepository`, `TableRepository` | `OrderStateMachineTests.cs`, `TableOccupancyTests.cs`, `OrderConfirmationIntegrationTests.cs` |
| **Discounts** | `DiscountRule`, `DiscountApproval` | `DiscountCommandHandler`, `ApprovalService` | `DiscountRepository` | `DiscountTests.cs`, `DiscountCommandHandlerTests.cs`, `DiscountPersistenceIntegrationTests.cs` |
| **Payments** | `Payment`, `PaymentMethod`, `CashierDrawer` | `PaymentCommandHandler`, `PaymentAllocation` | `PaymentRepository` | `PaymentTests.cs`, `PaymentCommandHandlerTests.cs`, `PaymentPersistenceIntegrationTests.cs` |
| **Fulfillment** | `DeliveryOrder`, `CourierAssignment`, `TableState` | `DeliveryCommandHandler`, `TableReleaseService` | `DeliveryRepository` | `DeliveryTests.cs`, `DeliveryCommandHandlerTests.cs`, `OrderCancellationIntegrationTests.cs` |
| **Reports** | `RevenueRecognition`, `BestSellerAggregator` | `RevenueReportQuery`, `LowStockQuery` | `RevenueReportRepository`, `LowStockRepository` | `RevenueRecognitionRuleTests.cs`, `BestSellerReportQueryHandlerTests.cs`, `LowStockReportIntegrationTests.cs` |

---

## 4. Catalog & Menu test cases

Covers category management, menu items, size variants, add-ons/toppings, ingredient recipes, and ordering availability rules (T3.1–T3.6).

| Test ID | Feature | Type | Precondition | Input | Expected Result | Actual Result | Status | Evidence Link |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `TC-CAT-01` | Category creation | Valid | System initialized | Name: `"Coffee"`, Active: `true` | Category created with trimmed name and unique ID | Category created with ID and active state | **PASS** | `CatalogLifecycleTests.cs`, DEMO-03 |
| `TC-CAT-02` | Category blank name | Invalid | System initialized | Name: `""` or `"   "` | Validation error: name cannot be empty | Rejected with validation error | **PASS** | `CatalogLifecycleTests.cs` |
| `TC-CAT-03` | Category max name length | Boundary | System initialized | Name: 80 characters string | Category created successfully | Accepted at exact 80 chars | **PASS** | `CatalogLifecycleTests.cs` |
| `TC-CAT-04` | Category excessive length | Invalid | System initialized | Name: 81 characters string | Validation error: name exceeds 80 characters limit | Rejected with length validation error | **PASS** | `CatalogLifecycleTests.cs` |
| `TC-CAT-05` | Product creation with category | Valid | Category `"Coffee"` exists and is active | Name: `"Espresso"`, Category: `"Coffee"`, BasePrice: `35.00` | Product created linked to category | Product persisted with category link | **PASS** | `CatalogLifecycleTests.cs`, DEMO-03 |
| `TC-CAT-06` | Product with inactive category | Invalid | Category `"Seasonal"` is deactivated | Attempt to create product under `"Seasonal"` | Creation rejected: category must be active | Rejected with conflict error | **PASS** | `CatalogLifecycleTests.cs` |
| `TC-CAT-07` | Product name boundary | Boundary | Active category exists | Name: 120 characters string | Product created successfully | Accepted at exact 120 chars | **PASS** | `CatalogLifecycleTests.cs` |
| `TC-CAT-08` | Product name over-length | Invalid | Active category exists | Name: 121 characters string | Validation error: name exceeds 120 characters | Rejected with length validation error | **PASS** | `CatalogLifecycleTests.cs` |
| `TC-CAT-09` | Size variant pricing | Valid | Product `"Latte"` exists with base price `40.00` | Add size variant `"Large"` with delta price `+10.00` | Variant saved; effective price resolves to `50.00` | Effective price calculated as `50.00` | **PASS** | `SizeVariantTests.cs`, DEMO-03 |
| `TC-CAT-10` | Negative base price | Invalid | Active category exists | BasePrice: `-5.00` | Validation error: price must be non-negative | Rejected with non-negative validation error | **PASS** | `CatalogLifecycleTests.cs` |
| `TC-CAT-11` | Zero base price | Boundary | Active category exists (e.g. complimentary item) | BasePrice: `0.00` | Product created with zero price | Allowed and stored as `0.00` | **PASS** | `MoneyTests.cs` |
| `TC-CAT-12` | Addon compatibility link | Valid | Drink `"Latte"` and addon `"Oat Milk"` (price `8.00`) exist | Link `"Oat Milk"` as compatible topping for `"Latte"` | Compatibility stored; available in POS for `"Latte"` | Linked and reloaded in POS menu | **PASS** | `AddonTests.cs`, DEMO-03 |
| `TC-CAT-13` | Size recipe ingredient specification | Valid | Ingredient `"Coffee Beans"` exists | Recipe: `10.00 g` of `"Coffee Beans"` for Large Latte | Recipe persisted with unit `g` and quantity `10.00` | Recipe reloaded and consumed upon order | **PASS** | `RecipeIngredientTests.cs`, DEMO-03 |
| `TC-CAT-14` | Unsupported recipe unit | Invalid | Ingredient measured in `ml` | Recipe specifies unit `kg` without conversion | Recipe creation rejected: incompatible unit | Incompatible unit rejected | **PASS** | `RecipeIngredientTests.cs` |
| `TC-CAT-15` | Menu availability filter | Valid | Active product in active category | Query POS menu items | Item marked `IsAvailableForOrdering = true` | Displayed and selectable in POS | **PASS** | `CatalogPersistenceIntegrationTests.cs`, DEMO-03 |
| `TC-CAT-16` | Deactivated product excluded | Boundary | Product deactivated via management screen | Query POS menu items | Item marked `IsAvailableForOrdering = false` | Excluded from new POS order selection | **PASS** | `CatalogLifecycleTests.cs`, DEMO-08 |

---

## 5. Customer management test cases

Covers customer profiles, contact info validation, delivery addresses, and walk-in guest handling (T5.1, T5.6).

| Test ID | Feature | Type | Precondition | Input | Expected Result | Actual Result | Status | Evidence Link |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `TC-CUST-01` | Customer profile creation | Valid | System initialized | Name: `"Alice Nguyen"`, Phone: `"+84901234567"`, Email: `"alice@example.com"` | Profile created with unique customer ID, zero loyalty balance | Customer persisted with correct profile data | **PASS** | `CustomerTests.cs`, DEMO-04 |
| `TC-CUST-02` | Invalid phone number format | Invalid | System initialized | Phone: `"invalid-phone"`, Name: `"Bob"` | Rejected: phone number fails format validation | Validation error returned | **PASS** | `CustomerTests.cs` |
| `TC-CUST-03` | Duplicate phone number | Invalid | Customer with phone `"+84901234567"` exists | Attempt to register new customer with same phone | Rejected: duplicate phone number conflict | Conflict error returned | **PASS** | `CustomerLoyaltyPersistenceIntegrationTests.cs` |
| `TC-CUST-04` | Saved delivery address | Valid | Customer profile exists | Address: `"123 Le Loi, D1, HCMC"`, IsDefault: `true` | Address linked to customer profile, marked as default | Address saved and auto-populated in delivery order | **PASS** | `CustomerAddressTests.cs`, DEMO-04 |
| `TC-CUST-05` | Multiple addresses / default switch | Boundary | Customer already has default address A | Add address B marked as default | Address B becomes default; Address A unflagged as default | Exactly one default address maintained | **PASS** | `CustomerAddressTests.cs` |
| `TC-CUST-06` | Customer profile update | Valid | Customer profile exists | Update name to `"Alice M. Nguyen"` | Name updated; loyalty balance and address links preserved | Changes saved without affecting loyalty points | **PASS** | `CustomerTests.cs`, DEMO-04 |
| `TC-CUST-07` | Customer search by phone | Valid | Customer `"+84901234567"` seeded | POS customer search query: `"0901234567"` | Customer profile matched and loaded into POS order draft | Customer loaded with current loyalty balance | **PASS** | `CustomerWorkspaceTests.cs`, DEMO-05 |
| `TC-CUST-08` | Walk-in guest order | Boundary | POS order draft opened | Select no customer (Guest / Walk-in) | Allowed: order proceeds without customer profile | Order completes with null customer ID, no points | **PASS** | `OrderDraftFactoryTests.cs`, DEMO-05-takeaway |

---

## 6. Loyalty program test cases

Covers earning rules, redemption rules, maximum discount caps, ledger transactions, and rollback behavior (T5.2–T5.5).

| Test ID | Feature | Type | Precondition | Input | Expected Result | Actual Result | Status | Evidence Link |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `TC-LOY-01` | Loyalty earning calculation | Valid | Earning rate: 1 point per 1.00 currency unit | Customer attached to completed order of `40.00` | Customer earns `40` points; ledger records positive credit | Points balance increased by 40; 1 ledger entry | **PASS** | `LoyaltyRulesTests.cs`, DEMO-06 |
| `TC-LOY-02` | Earning rounding policy | Boundary | Earning rate: 1 point per 1.00; Order total: `45.80` | Complete order with fractional total | Rounded down (floor) to `45` points earned | Points credited as 45 according to rule | **PASS** | `LoyaltyRulesTests.cs` |
| `TC-LOY-03` | Walk-in order zero points | Boundary | Walk-in order completed (no customer ID) | Complete order of `45.00` | No points awarded; no loyalty ledger entries generated | Zero points awarded | **PASS** | `LoyaltyLedgerTests.cs`, DEMO-06 |
| `TC-LOY-04` | Redemption deduction | Valid | Customer has 85 points; rule: 1 point = 1.00; Cap: 50% | Order total `40.00`; customer redeems 10 points | Order payable total reduced to `30.00`; 10 points deducted | Total becomes 30.00; balance becomes 75 | **PASS** | `LoyaltyRulesTests.cs`, DEMO-07 |
| `TC-LOY-05` | Redemption exceeding balance | Invalid | Customer has 20 points | Attempt to redeem 25 points on `50.00` order | Rejected: redemption points exceed available customer balance | Rejected with insufficient balance error | **PASS** | `LoyaltyRulesTests.cs` |
| `TC-LOY-06` | Redemption exceeding percentage cap | Boundary | Order total `40.00`, Max redemption cap: 50% (`20.00`); Customer has 30 points | Attempt to redeem 25 points (`25.00` discount) | Rejected: discount exceeds 50% cap of `20.00` | Rejected with cap violation error | **PASS** | `LoyaltyRulesTests.cs` |
| `TC-LOY-07` | Exact cap redemption | Boundary | Order total `40.00`, Cap: 50% (`20.00`); Customer has 50 points | Redeem exactly 20 points (`20.00` discount) | Accepted: payable total becomes `20.00`; exactly 20 points deducted | Succeeded; balance reduced by 20 | **PASS** | `LoyaltyRulesTests.cs` |
| `TC-LOY-08` | Inactive customer redemption | Invalid | Customer profile is deactivated | Attempt to redeem points for order | Rejected: inactive customer cannot redeem points | Rejected with customer inactive error | **PASS** | `LoyaltyRulesTests.cs` |
| `TC-LOY-09` | Order cancellation restores points | Valid | Order completed with 10 points redeemed; Balance is 75 | Cancel order with valid reason | Exactly 10 points restored; customer balance returns to 85; reversal ledger entry written | Balance restored to 85; 1 reversal entry | **PASS** | `OrderCancellationCommandHandlerTests.cs`, DEMO-07 |
| `TC-LOY-10` | Atomic failure rollback | Invalid | Order ready; loyalty settings temporarily removed in DB | Attempt to complete order | Completion fails due to missing rules; order stays ready, table occupied, NO points or completion recorded | Order stays ready; 0 points awarded; zero partial commits | **PASS** | `MidtermDemonstrationIntegrationTests.cs`, DEMO-09 |

---

## 7. Inventory & Procurement test cases

Covers ingredients, unit definitions, suppliers, purchase receipts, stock adjustments, movement auditing, and low-stock threshold queries (T4.1–T4.7, T7.5).

| Test ID | Feature | Type | Precondition | Input | Expected Result | Actual Result | Status | Evidence Link |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `TC-INV-01` | Ingredient creation | Valid | System initialized | Name: `"Arabica Coffee Beans"`, Unit: `g`, MinThreshold: `50.00` | Ingredient persisted with initial balance `0.00` | Persisted with unit `g` and balance 0 | **PASS** | `IngredientTests.cs`, DEMO-02 |
| `TC-INV-02` | Supported units validation | Boundary | System initialized | Test units: `g` (weight), `ml` (volume), `pcs` (discrete) | All three units accepted and stored | Successfully mapped to domain unit | **PASS** | `IngredientLifecycleTests.cs`, DEMO-09 |
| `TC-INV-03` | Negative reorder threshold | Invalid | System initialized | MinThreshold: `-10.00` | Rejected: threshold cannot be negative | Rejected with validation error | **PASS** | `IngredientTests.cs` |
| `TC-INV-04` | Supplier creation & update | Valid | System initialized | Create supplier `"Highland Farms"`, update contact | Supplier details saved and updated successfully | Persisted and reloaded | **PASS** | `SupplierTests.cs`, DEMO-02 |
| `TC-INV-05` | Purchase receipt draft | Valid | Active supplier and ingredient exist | Create receipt with line item: 100g at cost 20.00 | Draft receipt created in `Draft` status; inventory not yet modified | Status `Draft`, stock unchanged | **PASS** | `PurchaseReceiptTests.cs`, DEMO-02 |
| `TC-INV-06` | Purchase receipt posting | Valid | Draft receipt with 100g beans exists; Initial balance: 0g | Post receipt with selected staff actor | Receipt transitions to `Posted`; stock increases by 100g to 100g; immutable movement logged | Status `Posted`; balance 100g; movement recorded | **PASS** | `ReceiptPostingIntegrationTests.cs`, DEMO-02 |
| `TC-INV-07` | Duplicate receipt posting rejection | Invalid | Receipt is already in `Posted` status | Attempt to re-post the same receipt | Rejected: cannot post an already posted receipt; no duplicate inventory added | Rejected with conflict; balance stays 100g | **PASS** | `ReceiptPostingIntegrationTests.cs`, DEMO-02 |
| `TC-INV-08` | Stock adjustment blank reason | Invalid | Ingredient balance: 100g | Adjustment: `-5.00g`, Reason: `""` (empty) | Rejected: non-empty reason is mandatory for stock adjustment | Rejected with missing reason validation error | **PASS** | `InventoryCommandHandlerTests.cs`, DEMO-02 |
| `TC-INV-09` | Manual stock reduction | Valid | Ingredient balance: 100g | Adjustment: `-5.00g`, Reason: `"Spillage during calibration"` | Balance becomes `95.00g`; audit log records negative movement | Balance 95g; adjustment movement logged | **PASS** | `InventoryCommandHandlerTests.cs`, DEMO-02 |
| `TC-INV-10` | Manual stock addition | Valid | Ingredient balance: 95g | Adjustment: `+5.00g`, Reason: `"Found misplaced bag"` | Balance becomes `100.00g`; audit log records positive movement | Balance 100g; adjustment movement logged | **PASS** | `InventoryCommandHandlerTests.cs`, DEMO-02 |
| `TC-INV-11` | Consecutive adjustments audit trail | Boundary | Initial balance: 95g | Execute sequence: `-5g`, `+5g`, `-5g` with reasons | Balance resolves to `90.00g`; exactly three distinct immutable movement records created | Balance 90g; all movements preserved in ledger | **PASS** | `StockAuditRepository.cs`, DEMO-02 |
| `TC-INV-12` | Low stock threshold detection | Boundary | Ingredient threshold: `50.00g`; Current balance: `50.00g` | Query low-stock ingredients | Ingredient returned in low-stock list (`balance <= threshold`) | Listed with status Low Stock | **PASS** | `LowStockQueryTests.cs`, DEMO-08 |
| `TC-INV-13` | Normal stock excluded from low stock | Valid | Ingredient threshold: `50.00g`; Current balance: `51.00g` | Query low-stock ingredients | Ingredient excluded from low-stock list (`balance > threshold`) | Excluded from alert list | **PASS** | `LowStockReportTests.cs` |
| `TC-INV-14` | Zero stock condition | Boundary | Ingredient balance: `0.00g`, Threshold: `10.00g` | Query low-stock ingredients | Ingredient flagged as critical out-of-stock | Displayed with zero balance warning | **PASS** | `LowStockReportIntegrationTests.cs` |
| `TC-INV-15` | External update isolation | Valid | Inventory screen loaded; order confirmed in background | Refresh inventory page | Fresh service scope queries DB; updated stock balance reflected immediately | Updated balance observed without app restart | **PASS** | `OperationsWorkspaceTests.cs`, `inventory-refresh.txt` |

---

## 8. Orders & State machine test cases

Covers dine-in table assignment, takeaway, delivery draft workflows, status transitions, inventory reservation/consumption, unpaid completion guards, and cancellation restoration (T6.1–T6.3, T6.6, T6.7).

| Test ID | Feature | Type | Precondition | Input | Expected Result | Actual Result | Status | Evidence Link |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `TC-ORD-01` | Dine-in order draft creation | Valid | Active table 1 is available | Create dine-in order for Table 1 with 1 Large Latte (`45.00`) | Draft created with channel `DineIn`, Table 1 assigned | Draft persisted in `Draft` status | **PASS** | `OrderDraftFactoryTests.cs`, DEMO-05-dine_in |
| `TC-ORD-02` | Table double occupancy prevention | Invalid | Table 1 already occupied by active order | Attempt to confirm second order on Table 1 | Rejected: Table 1 is currently occupied | Rejected with conflict error | **PASS** | `TableOccupancyTests.cs` |
| `TC-ORD-03` | Atomic confirmation inventory consumption | Valid | 100g coffee beans in stock; Recipe requires 10g | Confirm draft order with Large Latte | Order status -> `Confirmed`; exactly 10g beans decremented; stock -> 90g | Stock decremented by 10g; status `Confirmed` | **PASS** | `OrderConfirmationIntegrationTests.cs`, DEMO-05-dine_in |
| `TC-ORD-04` | Insufficient stock confirmation failure | Boundary | 5g coffee beans in stock; Recipe requires 10g | Attempt to confirm draft order | Rejected: insufficient inventory; order remains in `Draft`; stock remains 5g | Rejected with out-of-stock; no stock changed | **PASS** | `OrderConfirmationCommandHandlerTests.cs` |
| `TC-ORD-05` | Kitchen preparation state flow | Valid | Order is in `Confirmed` state | Move order: `Confirmed` -> `Preparing` -> `Ready` | Status transitions successfully; audit timestamps recorded | Status reaches `Ready` | **PASS** | `OrderStateMachineTests.cs`, DEMO-05-dine_in |
| `TC-ORD-06` | Unpaid order completion rejection | Invalid | Order is `Ready` with payable balance `40.00`; no payments | Attempt to mark order as `Completed` | Rejected: order cannot be completed while balance is unpaid | Rejected with unpaid balance error | **PASS** | `OrderStateMachineTests.cs`, DEMO-05-dine_in |
| `TC-ORD-07` | Completed order idempotence | Invalid | Order paid and completed | Attempt to re-complete or modify the completed order | Rejected: completed orders are immutable and cannot transition | Rejected with invalid state transition error | **PASS** | `OrderStateMachineTests.cs`, DEMO-05-dine_in |
| `TC-ORD-08` | Dine-in completion releases table | Valid | Order completed for Table 1 | Verify Table 1 status post-completion | Table 1 automatically released to `Available` | Table released and available for new seating | **PASS** | `TableOccupancyTests.cs`, DEMO-05-dine_in |
| `TC-ORD-09` | Order cancellation with restoration | Valid | Order confirmed with 10g beans consumed, Table 1 occupied | Cancel order with reason: `"Customer emergency departure"` | Status -> `Cancelled`; 10g beans restored to stock; Table 1 released; loyalty entries reversed | 10g restored; table freed; cancellation recorded | **PASS** | `OrderCancellationIntegrationTests.cs`, DEMO-07 |
| `TC-ORD-10` | Cancellation idempotence | Boundary | Order already `Cancelled` | Repeat cancellation command on same order | Rejected: order already cancelled; no duplicate inventory restoration | Rejected without duplicate stock addition | **PASS** | `OrderCancellationCommandHandlerTests.cs`, DEMO-07 |
| `TC-ORD-11` | Blank cancellation reason | Invalid | Order in `Confirmed` state | Attempt cancellation with empty reason `""` | Rejected: cancellation reason is required | Validation error returned | **PASS** | `OrderCancellationCommandHandlerTests.cs` |

---

## 9. Discounts & Approvals test cases

Covers discount requests, supervisor approval permissions, total recalculations, and rejected discount policies (T6.5).

| Test ID | Feature | Type | Precondition | Input | Expected Result | Actual Result | Status | Evidence Link |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `TC-DISC-01` | Fixed discount request | Valid | Order total `45.00` | Request fixed discount of `5.00`, Reason: `"Opening promo"` | Discount status set to `Pending`; order total remains `45.00` until approval | Discount recorded as `Pending`; total `45.00` | **PASS** | `DiscountTests.cs`, DEMO-05-dine_in |
| `TC-DISC-02` | Supervisor discount approval | Valid | Discount `5.00` is pending; Supervisor has `discounts.approve` permission | Approve discount with supervisor actor credentials | Discount status -> `Approved`; order payable total recalculated to `40.00` | Total updated to `40.00` | **PASS** | `DiscountCommandHandlerTests.cs`, DEMO-05-dine_in |
| `TC-DISC-03` | Percentage discount calculation | Valid | Order total `100.00` | Request 15% discount | Calculated discount value is `15.00`; payable total becomes `85.00` upon approval | Discount applied as 15.00 | **PASS** | `DiscountCommandHandlerTests.cs` |
| `TC-DISC-04` | Discount exceeding order total | Boundary | Order total `30.00` | Request fixed discount of `35.00` | Rejected: discount cannot exceed order subtotal | Rejected with validation error | **PASS** | `DiscountTests.cs` |
| `TC-DISC-05` | Unauthorized approval rejection | Invalid | Cashier lacks `discounts.approve` permission | Cashier attempts to approve pending discount | Rejected: actor does not possess required approval permission | Rejected with forbidden/permission error | **PASS** | `DiscountPersistenceIntegrationTests.cs` |
| `TC-DISC-06` | Discount rejection | Valid | Discount is `Pending` | Supervisor rejects discount request with reason | Discount status -> `Rejected`; order payable total remains unchanged | Status `Rejected`; original total preserved | **PASS** | `DiscountCommandHandlerTests.cs` |

---

## 10. Payments & Fulfillment test cases

Covers cash payments (with change), credit/debit card, e-wallet, split payments, delivery courier assignments, and delivery lifecycle (T6.4, T6.8).

| Test ID | Feature | Type | Precondition | Input | Expected Result | Actual Result | Status | Evidence Link |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `TC-PAY-01` | Exact cash payment | Valid | Order payable balance `40.00` | Tender `40.00` Cash | Payment recorded; change `0.00`; remaining balance `0.00`; order ready for completion | Payment captured; remaining 0.00 | **PASS** | `PaymentCommandHandlerTests.cs`, DEMO-05-dine_in |
| `TC-PAY-02` | Cash payment with change | Boundary | Order payable balance `45.00` | Tender `50.00` Cash | Payment recorded; calculated change is `5.00`; remaining balance `0.00` | Change calculated as 5.00; balance 0.00 | **PASS** | `PaymentTests.cs` |
| `TC-PAY-03` | Electronic payment exact amount | Valid | Takeaway order balance `45.00` | Process Card payment for `45.00` | Payment captured; transaction ID stored; remaining balance `0.00` | Card payment recorded; balance 0.00 | **PASS** | `PaymentCommandHandlerTests.cs`, DEMO-05-takeaway |
| `TC-PAY-04` | E-Wallet delivery payment | Valid | Delivery order balance `45.00` | Process E-Wallet payment for `45.00` | Payment captured; payment method marked `e_wallet` | E-Wallet payment recorded | **PASS** | `PaymentPersistenceIntegrationTests.cs`, DEMO-05-delivery |
| `TC-PAY-05` | Partial payment tracking | Valid | Order balance `100.00` | Tender `40.00` Cash | Payment accepted; remaining balance `60.00`; order cannot yet complete | Remaining balance 60.00 | **PASS** | `PaymentCommandHandlerTests.cs` |
| `TC-FUL-01` | Delivery staff assignment | Valid | Delivery order is `Ready`; active courier exists | Assign courier with role `DeliveryStaff` | Courier assigned; delivery record updated | Active courier linked to delivery | **PASS** | `DeliveryCommandHandlerTests.cs`, DEMO-05-delivery |
| `TC-FUL-02` | Ineligible courier assignment | Invalid | Staff member has role `Barista` (not `DeliveryStaff`) | Attempt to assign staff member to delivery | Rejected: assigned staff must have delivery eligibility | Rejected with role validation error | **PASS** | `DeliveryTests.cs` |
| `TC-FUL-03` | Delivery dispatch and completion | Valid | Courier assigned to ready order | Dispatch delivery -> mark as `Delivered` | Delivery reaches `Delivered`; order reaches `Completed` | Order completed; delivery timestamps recorded | **PASS** | `DeliveryCommandHandlerTests.cs`, DEMO-05-delivery |

---

## 11. Reports & Analytics test cases

Covers revenue reporting, recognition rules, best-seller rankings, order channel reconciliation, low-stock alerts, and concurrent execution safety (T7.1–T7.5).

| Test ID | Feature | Type | Precondition | Input | Expected Result | Actual Result | Status | Evidence Link |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `TC-REP-01` | Revenue recognition on completed orders | Valid | 3 orders completed (`40.00`, `45.00`, `45.00`); Total: `130.00` | Run Revenue Report for current date range | Recognized revenue increases by exactly `130.00` | Revenue reflects `+130.00` increase | **PASS** | `RevenueRecognitionRuleTests.cs`, DEMO-08 |
| `TC-REP-02` | Uncompleted orders excluded from revenue | Boundary | 1 order `Draft`, 1 order `Confirmed`, 1 order `Cancelled` | Run Revenue Report | None of these uncompleted orders are counted in recognized revenue | Uncompleted orders excluded | **PASS** | `RevenueReportQueryHandlerTests.cs`, DEMO-08 |
| `TC-REP-03` | Channel revenue reconciliation | Valid | Completed orders across Dine-In, Takeaway, and Delivery | Query revenue by channel | Dine-In + Takeaway + Delivery revenues exactly equal Total Revenue | Sum of channel revenues matches total | **PASS** | `OrderChannelReportIntegrationTests.cs`, DEMO-08 |
| `TC-REP-04` | Best seller quantity ranking | Valid | 3 Large Lattes sold, 1 Espresso sold | Query top-selling products report | Large Latte ranked #1 with quantity 3; Espresso ranked #2 | Quantities aggregated and ranked correctly | **PASS** | `BestSellerReportIntegrationTests.cs`, DEMO-08 |
| `TC-REP-05` | Best seller ranking tie-breaker | Boundary | Two products have identical sold quantity (e.g. 5 units) | Query best sellers | Deterministic sorting applied (higher total revenue or alphabetical name) | Deterministic order maintained | **PASS** | `BestSellerReportQueryHandlerTests.cs` |
| `TC-REP-06` | Low stock report aggregation | Valid | 2 ingredients at or below reorder threshold | Generate low stock report | Both ingredients listed with current stock, threshold, and unit | Exactly 2 rows returned with alert badges | **PASS** | `LowStockReportIntegrationTests.cs`, DEMO-08 |
| `TC-REP-07` | Concurrent report query safety | Valid | Multiple report widgets (Revenue, Best Seller, Channels) on screen | Trigger simultaneous refresh of all reports | Queries execute in parallel without DbContext concurrency collision | Reports refresh cleanly without threading errors | **PASS** | `ReportingDatabaseFixture.cs`, `reports.png` |

---

## 12. Defect resolution log

The table below records defects identified during integration testing and the end-to-end demonstration (T8.5 / #103), along with the root causes and permanent fixes applied before milestone sign-off.

| Defect ID | Discovered In | Description & Root Cause | Resolution & Verification | Fixed By |
| :--- | :--- | :--- | :--- | :--- |
| `DEF-01` | Infrastructure DI | `IInventoryRepository` was not registered in direct client composition root, causing DI resolution failure on startup. | Registered repository in `AddCoffeeShopDirectClient`. Verified by `DirectClientCompositionTests`. | commit `349fe95` |
| `DEF-02` | Reporting Screen | Concurrent execution of Revenue, Best-Seller, and Channel reports collided on a shared single EF Core `DbContext`. | Updated report services to use factory/scoped DbContext per query. Verified by `RevenueReportIntegrationTests`. | commit `0b32817` |
| `DEF-03` | Midterm Seed Data | Seed script lacked an active staff member with `DeliveryStaff` role, causing delivery assignment to fail. | Added dedicated `DeliveryStaff` user in `midterm-seed.sql`. Verified by `MidtermSeedDataIntegrationTests`. | commit `036df4e` |
| `DEF-04` | Recipe Resolution | Pastries using discrete unit `pcs` failed recipe validation because only `g` and `ml` were supported. | Added `pcs` to recognized recipe units in domain validator. Verified by `RecipeIngredientTests`. | commit `036df4e` |
| `DEF-05` | Sales Completion | POS order completion did not invoke loyalty earning and redemption persistence in the same transaction. | Unified completion, loyalty earning, and table release under a single unit of work. Verified by DEMO-06 and DEMO-09. | commit `0b32817` |
| `DEF-06` | UI Audit Screen | Stock adjustments and movement history controls were disabled by default on the inventory page. | Enabled controls and bound them to `OperationsWorkspace`. Verified by native audit screenshot. | commit `294a6df` |
| `DEF-07` | Procurement | Receipt posting used a hard-coded actor identifier rather than the currently authenticated staff operator. | Wired active operator context from session into receipt command. Verified by DEMO-02. | commit `e291fff` |
| `DEF-08` | WinUI Reports View | Filter controls (dates, channels, payment methods) clipped the Refresh button on smaller display resolutions. | Applied wrapping layout (`WrapPanel`) for report filter headers. Verified by `reports-tree.txt`. | commit `e291fff` |
| `DEF-09` | Inventory Refresh | Inventory screen retained a stale database context, failing to observe stock deductions made by POS. | Implemented fresh service scope instantiation on each screen refresh. Verified by `inventory-refresh.txt`. | commit `349fe95` |

---

## 13. Remaining limitations & blockers

### 13.1 Midterm architectural limitations
1. **In-Process Composition:** The midterm desktop client directly hosts the Application and PostgreSQL Infrastructure in-process via `coffeeshop_midterm`. The final phase will transition to typed HTTP clients consuming the standalone `CoffeeShop.Api`.
2. **Simplified Authentication:** Staff operators are selected from an in-process active session switcher rather than bearer JWT tokens with cryptographic signature validation.
3. **Simulated Payment Gateways:** Card and E-Wallet payments are captured synchronously as approved tenders rather than calling live external banking webhooks.

### 13.2 Critical blockers
- **None.** All 1,132 tests pass, all 13 midterm demonstration checklist steps succeed, and zero build warnings or format violations exist.

---

## 14. Reproducible evidence links

All test cases and results in this report are verifiable using the following committed repository artifacts:

- **Automated Verification Script:** [`scripts/verify-midterm.ps1`](../scripts/verify-midterm.ps1)
- **Automated Test Log (TRX):** `artifacts/midterm/midterm.trx`
- **Scripted Demonstration Trace:** [`docs/evidence/midterm/scripted-run.txt`](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/blob/3a43986b29dd2462868f9fadc11c11bd8125514b/docs/evidence/midterm/scripted-run.txt)
- **Full Test Suite Summary:** [`docs/evidence/midterm/verification.txt`](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/blob/3a43986b29dd2462868f9fadc11c11bd8125514b/docs/evidence/midterm/verification.txt)
- **Native UI Reports Evidence:**
  - Visual rendering: [`docs/evidence/midterm/reports.png`](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/blob/3a43986b29dd2462868f9fadc11c11bd8125514b/docs/evidence/midterm/reports.png)
  - Accessibility tree snapshot: [`docs/evidence/midterm/reports-tree.txt`](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/blob/3a43986b29dd2462868f9fadc11c11bd8125514b/docs/evidence/midterm/reports-tree.txt)
- **Native Inventory Audit Evidence:**
  - Balance & ledger view: [`docs/evidence/midterm/inventory-audit.png`](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/blob/3a43986b29dd2462868f9fadc11c11bd8125514b/docs/evidence/midterm/inventory-audit.png)
  - Multi-connection refresh trace: [`docs/evidence/midterm/inventory-refresh.txt`](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/blob/3a43986b29dd2462868f9fadc11c11bd8125514b/docs/evidence/midterm/inventory-refresh.txt)
- **Native Smoke Trace:** [`docs/evidence/midterm/native-smoke.txt`](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/blob/3a43986b29dd2462868f9fadc11c11bd8125514b/docs/evidence/midterm/native-smoke.txt)
- **Demonstration Checklist:** [`docs/midterm-demonstration.md`](midterm-demonstration.md)




