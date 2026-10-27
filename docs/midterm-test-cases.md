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
