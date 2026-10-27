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

